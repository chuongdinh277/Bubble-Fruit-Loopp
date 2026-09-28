using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class CollectingState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {

        }

        internal static IEnumerator FlyToBox(BoxManager manager, Fruit fruit, BoxView box,
            int slotIndex, BoxManager.Column column)
        {
            SpriteRenderer flightRenderer = fruit != null ? fruit.VisualSpriteRenderer : null;
            Sprite flightSprite = flightRenderer != null ? flightRenderer.sprite : null;
            if (fruit == null || box == null || !box.gameObject.activeInHierarchy ||
                flightRenderer == null || flightSprite == null)
            {
                if (box != null) box.Runtime.CancelReservation(slotIndex);
                yield break;
            }

            Transform target = box.GetSlotTransform(slotIndex);
            int originalSortingOrder = flightRenderer.sortingOrder;
            flightRenderer.sortingOrder = BoxManager.FlightFruitOrder;
            Vector3 start = fruit.CachedTransform.position;
            Vector3 startScale = fruit.CachedTransform.localScale;
            Quaternion landingRotation = target.rotation;
            Vector3 flightScale = startScale * 1.34f;
            Vector3 flightDirection = (target.position - start).normalized;
            Vector3 lifted = start + Vector3.up * 0.50f + flightDirection * 0.12f;
            TrailRenderer flightTrail = BoxManager.CreateFruitFlightTrail(fruit);

            // First make the selected fruit visibly pop out of the moving lane.
            const float liftDuration = 0.11f;
            float elapsed = 0f;
            while (elapsed < liftDuration && fruit != null && box != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / liftDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                fruit.CachedTransform.position = Vector3.LerpUnclamped(start, lifted, eased);
                fruit.CachedTransform.localScale = Vector3.LerpUnclamped(startScale, flightScale, eased);
                if (flightTrail != null) flightTrail.transform.position = fruit.CachedTransform.position;
                fruit.CachedTransform.Rotate(0f, 0f, 300f * Time.deltaTime);
                yield return null;
            }

            if (fruit == null || box == null)
            {
                BoxManager.ReleaseFruitFlightTrail(flightTrail);
                if (flightRenderer != null) flightRenderer.sortingOrder = originalSortingOrder;
                if (box != null) box.Runtime.CancelReservation(slotIndex);
                yield break;
            }
            fruit.CachedTransform.position = lifted;
            fruit.CachedTransform.localScale = flightScale;

            // Then retain that larger silhouette for the entire curved flight.
            // The apex must clear the carton lip. Use the vertical distance as
            // part of the arc so boxes at different rows still get a clean,
            // visibly elevated jump instead of cutting through the front edge.
            float verticalGap = Mathf.Abs(lifted.y - target.position.y);
            float arcHeight = Mathf.Max(1.28f, verticalGap * 0.38f + 0.82f);
            Vector3 forwardCurvePoint = Vector3.Lerp(lifted, target.position, 0.68f);
            Vector3 control = forwardCurvePoint + Vector3.up * arcHeight;
            const float duration = 0.32f;
            elapsed = 0f;
            while (elapsed < duration && fruit != null && box != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float inverse = 1f - eased;
                fruit.CachedTransform.position = inverse * inverse * lifted
                    + 2f * inverse * eased * control
                    + eased * eased * target.position;
                fruit.CachedTransform.localScale = flightScale;
                // Let the fruit spin during the jump, then settle into the
                // slot's authored orientation instead of stopping randomly.
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));
                Quaternion spinning = fruit.CachedTransform.rotation
                    * Quaternion.Euler(0f, 0f, 420f * Time.deltaTime);
                fruit.CachedTransform.rotation = Quaternion.Slerp(spinning, landingRotation, settle);
                if (flightTrail != null) flightTrail.transform.position = fruit.CachedTransform.position;
                yield return null;
            }
            BoxManager.ReleaseFruitFlightTrail(flightTrail);
            if (fruit == null || box == null || !box.gameObject.activeInHierarchy)
            {
                if (flightRenderer != null) flightRenderer.sortingOrder = originalSortingOrder;
                if (box != null) box.Runtime.CancelReservation(slotIndex);
                yield break;
            }

            // Count the fruit only after it has visibly reached and occupied a slot.
            bool docked = box.DockFruit(fruit, flightRenderer, flightSprite, slotIndex);
            if (!docked)
            {
                flightRenderer.sortingOrder = originalSortingOrder;
                box.Runtime.CancelReservation(slotIndex);
                yield break;
            }
            bool full = box.Runtime.CommitReservedFruit(slotIndex);
            if (!full) yield break;

            column.Active = null;
            manager.boxesExiting++;
            box.PlayCompleteAndExit(
                () => manager.PrepareNextBox(column),
                () => manager.MovePreparedBoxIntoActivePosition(column),
                () =>
                {
                    box.ReleaseCollectedFruits(BoxManager.DeactivateCollectedFruit);
                    box.gameObject.SetActive(false);
                    manager.boxesExiting = Mathf.Max(0, manager.boxesExiting - 1);
                    manager.TryPlayWinCelebration();
                });

        }

    }
}
