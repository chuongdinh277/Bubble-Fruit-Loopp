using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class CollectingState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit)
        {
        }

        public static void OnExecute(Fruit fruit)
        {
        }

        internal static IEnumerator FlyToBox(BoxManager manager, Fruit fruit, BoxView box, int slotIndex, BoxManager.Column column)
        {
            SpriteRenderer flightRenderer = fruit != null ? fruit.VisualSpriteRenderer : null;
            Sprite flightSprite = flightRenderer != null ? flightRenderer.sprite : null;
            if (fruit == null || box == null || !box.gameObject.activeInHierarchy || flightRenderer == null || flightSprite == null)
            {
                if (box != null)
                    box.Runtime.CancelReservation(slotIndex);
                yield break;
            }

            Transform target = box.GetSlotTransform(slotIndex);
            int originalSortingOrder = flightRenderer.sortingOrder;
            flightRenderer.sortingOrder = BoxManager.FlightFruitOrder;
            Vector3 start = fruit.CachedTransform.position;
            Vector3 startScale = fruit.CachedTransform.localScale;
            Quaternion landingRotation = target.rotation;
            Vector3 flightScale = startScale * 1.34f;
            Vector3 lifted;
            CalculateLiftPosition(target, start, out lifted);
            TrailRenderer flightTrail = BoxManager.CreateFruitFlightTrail(fruit);
            // First make the selected fruit visibly pop out of the moving lane.
            const float liftDuration = 0.11f;
            float elapsed = 0f;
            IEnumerator lift = AnimateLift(fruit, box, start, lifted, startScale, flightScale, flightTrail, liftDuration, elapsed);
            while (lift.MoveNext())
                yield return lift.Current;
            if (fruit == null || box == null)
            {
                BoxManager.ReleaseFruitFlightTrail(flightTrail);
                if (flightRenderer != null)
                    flightRenderer.sortingOrder = originalSortingOrder;
                if (box != null)
                    box.Runtime.CancelReservation(slotIndex);
                yield break;
            }

            fruit.CachedTransform.position = lifted;
            fruit.CachedTransform.localScale = flightScale;
            // Then retain that larger silhouette for the entire curved flight.
            // The apex must clear the carton lip. Use the vertical distance as
            // part of the arc so boxes at different rows still get a clean,
            // visibly elevated jump instead of cutting through the front edge.
            Vector3 control;
            CalculateArcControl(target, lifted, out control);
            const float duration = 0.32f;
            elapsed = 0f;
            IEnumerator arc = AnimateArc(fruit, box, target, landingRotation, flightScale, lifted, flightTrail, control, duration, elapsed);
            while (arc.MoveNext())
                yield return arc.Current;
            BoxManager.ReleaseFruitFlightTrail(flightTrail);
            if (fruit == null || box == null || !box.gameObject.activeInHierarchy)
            {
                if (flightRenderer != null)
                    flightRenderer.sortingOrder = originalSortingOrder;
                if (box != null)
                    box.Runtime.CancelReservation(slotIndex);
                yield break;
            }

            // Count the fruit only after it has visibly reached and occupied a slot.
            DockAndCommitFruit(manager, fruit, box, flightRenderer, flightSprite, slotIndex, originalSortingOrder, column);
        }

        private static void ApplyBoxFlightPose(Fruit fruit, Transform target, Quaternion landingRotation, Vector3 flightScale, Vector3 lifted, TrailRenderer flightTrail, Vector3 control, float t)
        {
            float eased = t * t * (3f - 2f * t);
            float inverse = 1f - eased;
            fruit.CachedTransform.position = inverse * inverse * lifted + 2f * inverse * eased * control + eased * eased * target.position;
            fruit.CachedTransform.localScale = flightScale;
            // Let the fruit spin during the jump, then settle into the
            // slot's authored orientation instead of stopping randomly.
            SettleBoxFlightRotation(fruit, landingRotation, t);
            if (flightTrail != null)
                flightTrail.transform.position = fruit.CachedTransform.position;
        }

        private static void ApplyLiftPose(Fruit fruit, Vector3 start, Vector3 lifted, Vector3 startScale, Vector3 flightScale, TrailRenderer flightTrail, float t)
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            fruit.CachedTransform.position = Vector3.LerpUnclamped(start, lifted, eased);
            fruit.CachedTransform.localScale = Vector3.LerpUnclamped(startScale, flightScale, eased);
            if (flightTrail != null)
                flightTrail.transform.position = fruit.CachedTransform.position;
            fruit.CachedTransform.Rotate(0f, 0f, 300f * Time.deltaTime);
        }

        private static void StartCompletedBoxExit(BoxManager manager, BoxView box, BoxManager.Column column)
        {
            column.Active = null;
            manager.boxesExiting++;
            box.PlayCompleteAndExit(() => manager.PrepareNextBox(column), () => manager.MovePreparedBoxIntoActivePosition(column), () =>
            {
                box.ReleaseCollectedFruits(BoxManager.DeactivateCollectedFruit);
                box.gameObject.SetActive(false);
                manager.boxesExiting = Mathf.Max(0, manager.boxesExiting - 1);
                manager.TryPlayWinCelebration();
            });
        }

        private static void SettleBoxFlightRotation(Fruit fruit, Quaternion landingRotation, float t)
        {
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));
            Quaternion spinning = fruit.CachedTransform.rotation * Quaternion.Euler(0f, 0f, 420f * Time.deltaTime);
            fruit.CachedTransform.rotation = Quaternion.Slerp(spinning, landingRotation, settle);
        }

        private static IEnumerator AnimateLift(Fruit fruit, BoxView box, Vector3 start, Vector3 lifted, Vector3 startScale, Vector3 flightScale, TrailRenderer flightTrail, float liftDuration, float elapsed)
        {
            while (elapsed < liftDuration && fruit != null && box != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / liftDuration);
                ApplyLiftPose(fruit, start, lifted, startScale, flightScale, flightTrail, t);
                yield return null;
            }
        }

        private static IEnumerator AnimateArc(Fruit fruit, BoxView box, Transform target, Quaternion landingRotation, Vector3 flightScale, Vector3 lifted, TrailRenderer flightTrail, Vector3 control, float duration, float elapsed)
        {
            while (elapsed < duration && fruit != null && box != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyBoxFlightPose(fruit, target, landingRotation, flightScale, lifted, flightTrail, control, t);
                yield return null;
            }
        }

        private static void CalculateLiftPosition(Transform target, Vector3 start, out Vector3 lifted)
        {
            Vector3 flightDirection = (target.position - start).normalized;
            lifted = start + Vector3.up * 0.50f + flightDirection * 0.12f;
        }

        private static void CalculateArcControl(Transform target, Vector3 lifted, out Vector3 control)
        {
            float verticalGap = Mathf.Abs(lifted.y - target.position.y);
            float arcHeight = Mathf.Max(1.28f, verticalGap * 0.38f + 0.82f);
            Vector3 forwardCurvePoint = Vector3.Lerp(lifted, target.position, 0.68f);
            control = forwardCurvePoint + Vector3.up * arcHeight;
        }

        private static void DockAndCommitFruit(BoxManager manager, Fruit fruit, BoxView box, SpriteRenderer flightRenderer, Sprite flightSprite, int slotIndex, int originalSortingOrder, BoxManager.Column column)
        {
            bool docked = box.DockFruit(fruit, flightRenderer, flightSprite, slotIndex);
            if (!docked)
            {
                flightRenderer.sortingOrder = originalSortingOrder;
                box.Runtime.CancelReservation(slotIndex);
                return;
            }

            bool full = box.Runtime.CommitReservedFruit(slotIndex);
            if (!full)
                return;
            StartCompletedBoxExit(manager, box, column);
        }
    }
}
