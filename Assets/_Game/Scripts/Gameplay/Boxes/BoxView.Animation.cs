using System;
using System.Collections;
using System.Collections.Generic;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Pooling;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxView
    {
        public void PlayPromote(Vector3 targetPosition)
        {
            StopMotion();
            EnsureLiftGroundShadow(targetPosition);
            SetLidProgress(1f);
            float lidProgress = 1f;
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.Join(transform.DOMove(targetPosition, slideDuration).SetEase(Ease.OutCubic));
            motionSequence.Join(DOTween.To(() => lidProgress, value =>
            {
                lidProgress = value;
                SetLidProgress(value);
            }, 0f, lidDuration).SetEase(Ease.OutCubic));
            motionSequence.OnComplete(() =>
            {
                transform.position = targetPosition;
                SetLidProgress(0f);
                motionSequence = null;
            });
        }

        public void PlayOpenForPromotion()
        {
            StopMotion();
            SetLidProgress(1f);
            float lidProgress = 1f;
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.Append(DOTween.To(() => lidProgress, value =>
            {
                lidProgress = value;
                SetLidProgress(value);
            }, 0f, 0.18f).SetEase(Ease.OutCubic));
            motionSequence.OnComplete(() =>
            {
                SetLidProgress(0f);
                motionSequence = null;
            });
        }

        public void PlayPromoteFromOpen(Vector3 targetPosition)
        {
            StopMotion();
            EnsureLiftGroundShadow(targetPosition);
            SetLidProgress(0f);
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.Append(transform.DOMove(targetPosition, slideDuration).SetEase(Ease.OutCubic));
            motionSequence.OnComplete(() =>
            {
                transform.position = targetPosition;
                motionSequence = null;
            });
        }

        public void PlayShiftTo(Vector3 targetPosition)
        {
            StopMotion();
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.Append(transform.DOMove(targetPosition, slideDuration).SetEase(Ease.InOutCubic));
            motionSequence.OnComplete(() => motionSequence = null);
        }

        private void PlayFillAnimation(int slotIndex)
        {
            // The completed box owns a larger, timed punch after its doors close.
            // Avoid two scale animations fighting over the fourth fruit.
            if (Runtime != null && Runtime.CurrentCount >= Runtime.Capacity) return;
            StartCoroutine(PunchBox());
        }

        // Completion VFX is intentionally fired by PlayCompleteAndExit after the
        // doors meet, so the burst reads as the lid locking rather than fruit impact.
        private void PlayCloseAnimation() { }

        private IEnumerator PromoteRoutine(Vector3 target)
        {
            Vector3 start = transform.position;
            SetLidProgress(1f);
            float elapsed = 0f;
            float duration = Mathf.Max(slideDuration, lidDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float positionT = Smooth(Mathf.Clamp01(elapsed / slideDuration));
                float lidT = Smooth(Mathf.Clamp01(elapsed / lidDuration));
                transform.position = Vector3.LerpUnclamped(start, target, positionT);
                SetLidProgress(1f - lidT);
                yield return null;
            }
            transform.position = target;
            SetLidProgress(0f);
            motionRoutine = null;
        }

        private IEnumerator ShiftRoutine(Vector3 target)
        {
            Vector3 start = transform.position;
            yield return TweenPosition(start, target, slideDuration);
            motionRoutine = null;
        }

        private IEnumerator CompleteRoutine(Action completed)
        {
            // Hold briefly so the fourth fruit is readable, then visibly fold
            // both side doors inward before the completed box moves away.
            yield return new WaitForSeconds(0.10f);
            yield return TweenLids(0f, 1f, closeLidDuration);
            yield return new WaitForSeconds(0.08f);
            Vector3 start = transform.position;
            Vector3 lifted = start + Vector3.up * 0.3f;
            yield return TweenPosition(start, lifted, 0.16f);
            yield return PunchBox();
            yield return TweenPosition(lifted, lifted + new Vector3(2.8f, 0.15f, 0f), exitDuration);
            completed?.Invoke();
            motionRoutine = null;
        }
        private IEnumerator TweenPosition(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(from, to, Smooth(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            transform.position = to;
        }

        private IEnumerator TweenLids(float from, float to, float duration)
        {
            float elapsed = 0f;
            float totalDuration = duration + rightDoorDelay;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float leftT = Mathf.Clamp01(elapsed / duration);
                float rightT = Mathf.Clamp01((elapsed - rightDoorDelay) / duration);
                float leftEase = to < from ? EaseOutCubic(leftT) : Smooth(leftT);
                float rightEase = to < from ? EaseOutCubic(rightT) : Smooth(rightT);
                SetLidProgress(Mathf.Lerp(from, to, leftEase), Mathf.Lerp(from, to, rightEase));
                yield return null;
            }
            SetLidProgress(to);
        }

        private IEnumerator PunchBox()
        {
            baseScale = transform.localScale;
            Vector3 punch = new(baseScale.x * 1.06f, baseScale.y * 0.94f, baseScale.z);
            float elapsed = 0f;
            while (elapsed < 0.14f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.14f);
                transform.localScale = t < 0.45f ? Vector3.Lerp(baseScale, punch, t / 0.45f) : Vector3.Lerp(punch, baseScale, (t - 0.45f) / 0.55f);
                yield return null;
            }
            transform.localScale = baseScale;
        }

        private static IEnumerator PunchSlot(Transform slot)
        {
            if (slot == null) yield break;
            Vector3 start = slot.localScale;
            float elapsed = 0f;
            while (elapsed < 0.16f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.16f);
                slot.localScale = start * (1f + Mathf.Sin(t * Mathf.PI) * 0.16f);
                yield return null;
            }
            slot.localScale = start;
        }

        private void SetLidProgress(float closed)
        {
            SetLidProgress(closed, closed);
        }

        private void SetLidProgress(float leftClosed, float rightClosed)
        {
            float closed = (Mathf.Clamp01(leftClosed) + Mathf.Clamp01(rightClosed)) * 0.5f;
            // Once the doors have met, no part of the packed fruit should remain
            // visible above the closed carton (tall stems can exceed the lid art).
            bool showPackedFruit = !hidePackedFruitsOnClose || closed < 0.98f;
            for (int index = 0; index < dockedVisuals.Count; index++)
            {
                GameObject packedFruit = dockedVisuals[index]?.fruit?.gameObject;
                if (packedFruit != null && packedFruit.activeSelf != showPackedFruit)
                    packedFruit.SetActive(showPackedFruit);
            }
            if (leftLid != null && rightLid != null)
            {
                if (leftLidOpenPosition == Vector3.zero && rightLidOpenPosition == Vector3.zero)
                {
                    leftLidOpenPosition = leftLid.localPosition;
                    rightLidOpenPosition = rightLid.localPosition;
                }

                // Real hinge motion. The pivots remain attached to the outer
                // walls; edge-on panels sit flush beside the open tray and both
                // half-width panels rotate flat to meet exactly at the centre.
                leftLid.localPosition = leftLidOpenPosition;
                rightLid.localPosition = rightLidOpenPosition;
                // Rotate around the two fixed outer-edge pivots. No positional
                // interpolation is used: the doors physically fold out/in.
                leftLid.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-doorOpenAngle, 0f, leftClosed), 0f);
                rightLid.localRotation = Quaternion.Euler(0f, Mathf.Lerp(doorOpenAngle, 0f, rightClosed), 0f);
                UpdateDoorShadow(leftDoorShadow, true, leftClosed);
                UpdateDoorShadow(rightDoorShadow, false, rightClosed);

                if (openArtwork != null)
                {
                    Color trayColor = openArtwork.color;
                    float openness = 1f - (leftClosed + rightClosed) * 0.5f;
                    trayColor.a = Mathf.SmoothStep(0f, 1f, openness);
                    openArtwork.color = trayColor;
                }
                if (closedArtwork != null)
                {
                    Color hidden = closedArtwork.color;
                    hidden.a = 0f;
                    closedArtwork.color = hidden;
                }
                if (frontLipArtwork != null)
                {
                    Color lip = frontLipArtwork.color;
                    lip.a = 1f - closed;
                    frontLipArtwork.color = lip;
                }
                UpdateClosedDepth(closed);
                return;
            }

            if (openArtwork != null && closedArtwork != null)
            {
                EnsureFrontLip();
                if (openArtworkScale == Vector3.zero)
                {
                    openArtworkScale = openArtwork.transform.localScale;
                    closedArtworkScale = closedArtwork.transform.localScale;
                    openArtworkPosition = openArtwork.transform.localPosition;
                    closedArtworkPosition = closedArtwork.transform.localPosition;
                }
                Color openColor = openArtwork.color;
                Color closedColor = closedArtwork.color;
                // A staged close reads as an actual lid movement instead of one
                // sprite suddenly replacing another.
                float lidReveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.78f, closed));
                float trayFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, closed));
                openColor.a = trayFade;
                closedColor.a = lidReveal;
                openArtwork.color = openColor;
                closedArtwork.color = closedColor;
                if (frontLipArtwork != null)
                {
                    Color lipColor = openColor;
                    lipColor.a = 1f - closed;
                    frontLipArtwork.color = lipColor;
                }
                openArtwork.transform.localScale = new Vector3(
                    openArtworkScale.x * Mathf.Lerp(1f, 0.98f, closed),
                    openArtworkScale.y * Mathf.Lerp(1f, 0.82f, closed),
                    openArtworkScale.z);
                closedArtwork.transform.localScale = new Vector3(
                    closedArtworkScale.x * Mathf.Lerp(1.05f, 1f, lidReveal),
                    closedArtworkScale.y * Mathf.Lerp(0.58f, 1f, lidReveal),
                    closedArtworkScale.z);
                openArtwork.transform.localPosition = openArtworkPosition + Vector3.down * (0.05f * closed);
                closedArtwork.transform.localPosition = closedArtworkPosition + Vector3.up * (0.24f * (1f - lidReveal));
                if (frontLipArtwork != null)
                    frontLipArtwork.transform.localScale = openArtwork.transform.localScale;
                UpdateClosedDepth(closed);
                return;
            }
            UpdateClosedDepth(closed);
        }

        public void SetEditorClosed(bool closed) => SetLidProgress(closed ? 1f : 0f);

        private static void UpdateDoorShadow(SpriteRenderer shadow, bool left, float closed)
        {
            if (shadow == null) return;
            float openness = 1f - Mathf.Clamp01(closed);
            Transform shadowTransform = shadow.transform;
            shadowTransform.localPosition = new Vector3(
                (left ? -1f : 1f) * Mathf.Lerp(0.255f, 0.51f, openness),
                Mathf.Lerp(-0.02f, -0.05f, openness), 0.02f);
            Vector2 spriteSize = shadow.sprite != null ? shadow.sprite.bounds.size : Vector2.one;
            float targetWidth = Mathf.Lerp(0.44f, 0.16f, openness);
            float targetHeight = Mathf.Lerp(1.00f, 1.06f, openness);
            shadowTransform.localScale = new Vector3(targetWidth / Mathf.Max(0.0001f, spriteSize.x),
                targetHeight / Mathf.Max(0.0001f, spriteSize.y), 1f);
            Color shadowColor = shadow.color;
            shadowColor.a = Mathf.Lerp(0.05f, 0.16f, openness);
            shadow.color = shadowColor;
        }
        private static float Smooth(float value) => value * value * (3f - 2f * value);
        private static float EaseOutCubic(float value) => 1f - Mathf.Pow(1f - value, 3f);
        private void StopMotion()
        {
            if (motionRoutine != null) StopCoroutine(motionRoutine);
            motionRoutine = null;
            if (motionSequence != null && motionSequence.IsActive()) motionSequence.Kill();
            motionSequence = null;
        }
    }
}
