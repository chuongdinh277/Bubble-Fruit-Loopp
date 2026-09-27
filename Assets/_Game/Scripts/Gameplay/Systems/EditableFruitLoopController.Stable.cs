using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private void UpdateStableLoop()
        {
            if (active.Count == 0) return;

            float loopSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);

            stableFruits.Clear();
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                if (item.Fruit != null && item.Fruit.State == FruitState.OnLoop)
                    stableFruits.Add(item);
            }
            if (stableFruits.Count == 0) return;

            stableFruits.Sort((left, right) =>
                left.Fruit.PathDistance.CompareTo(right.Fruit.PathDistance));
            float idealSpacing = path.Length / stableFruits.Count;

            // Every fruit shares one fixed cruising speed. Removing a fruit for a
            // box creates only a visual gap and never changes the pace of the
            // remaining stream.
            for (int index = 0; index < stableFruits.Count; index++)
            {
                LoopFruit item = stableFruits[index];
                item.DesiredSpeed = loopSpeed;
            }

            // Equalise gaps by smoothly varying velocity, never by rewriting path
            // positions. This prevents the visible hitch from the old snap-based
            // spacing pass while preserving continuous forward motion.
            // Use one continuous speed solver for spacing. The old contact pass
            // also rewrote path positions, which fought this solver and made
            // tightly packed fruit jitter against each other.
            UpdateEvenSpacing(idealSpacing);

            for (int index = 0; index < stableFruits.Count; index++)
            {
                LoopFruit item = stableFruits[index];
                
                float phase = Time.time * 1.15f + item.Fruit.GetInstanceID() * 0.017f;
                // Keep one shared cruising speed after the entry overlap has been
                // resolved. Per-fruit pace variation made tidy gaps drift over time.
                float adjustedSpeed = Mathf.SmoothDamp(
                    item.Fruit.CurrentSpeed,
                    item.DesiredSpeed,
                    ref item.SpeedSmoothVelocity,
                    Mathf.Lerp(0.32f, 0.18f, Mathf.Clamp01(contactVelocitySharing)),
                    loopSpeed,
                    Time.fixedDeltaTime);

                // No random speed variation, pure even rotation
                item.Fruit.CurrentSpeed = adjustedSpeed;
                item.Fruit.PathDistance += item.Fruit.CurrentSpeed * Time.fixedDeltaTime;
                
                if (item.Fruit.PathDistance >= path.Length) item.Fruit.PathDistance -= path.Length;
                if (item.Fruit.PathDistance < 0) item.Fruit.PathDistance += path.Length;

                path.Evaluate(item.Fruit.PathDistance, out Vector3 position, out Quaternion rotation);
                Vector2 tangent = rotation * Vector3.up;
                Vector2 normal = new(-tangent.y, tangent.x);

                // A controlled micro-sway makes the lane feel alive without letting
                // physics contacts turn the fruit stream into a fight.
                float laneWobble = Mathf.Sin(phase) * 0.022f;
                float forwardWobble = Mathf.Sin(phase * 0.63f + 1.1f) * 0.008f;
                float angleWobble = Mathf.Sin(phase * 0.83f + 0.7f) * 2.8f;
                Vector3 displayPosition = position
                    + (Vector3)(normal * laneWobble)
                    + (Vector3)(tangent.normalized * forwardWobble);
                Quaternion displayRotation = rotation * Quaternion.Euler(0f, 0f, angleWobble);
                item.Fruit.MoveStable(displayPosition, displayRotation);
            }
        }

        private void UpdateEvenSpacing(float idealSpacing)
        {
            if (stableFruits.Count < 2) return;
            if (spacingDelayRemaining > 0f)
            {
                spacingDelayRemaining -= Time.fixedDeltaTime;
                return;
            }

            float correctionBlend = 0.58f;
            if (spacingRelaxRemaining > 0f)
            {
                spacingRelaxRemaining = Mathf.Max(
                    0f, spacingRelaxRemaining - Time.fixedDeltaTime);
                float progress = 1f - Mathf.Clamp01(
                    spacingRelaxRemaining / Mathf.Max(0.01f, spacingRelaxDuration));
                float rampIn = Mathf.SmoothStep(0f, 0.05f, progress);
                float fadeOut = 1f - Mathf.SmoothStep(0.85f, 1f, progress);
                correctionBlend = Mathf.Lerp(0.58f, 0.92f, rampIn * fadeOut);
            }
            float maximumSpeedOffset = Mathf.Max(1.8f, maxSpacingShiftPerStep * 90f);
            float spacingScale = Mathf.Max(0.1f, Mathf.Max(minimumSpacing, idealSpacing));
            float spacingGain = Mathf.Lerp(1.8f, 3.2f,
                Mathf.InverseLerp(0.5f, 12f, softContactStrength));
            for (int index = 0; index < stableFruits.Count; index++)
            {
                LoopFruit previous = stableFruits[(index - 1 + stableFruits.Count) % stableFruits.Count];
                LoopFruit current = stableFruits[index];
                LoopFruit next = stableFruits[(index + 1) % stableFruits.Count];
                float gapBehind = Mathf.Repeat(
                    current.Fruit.PathDistance - previous.Fruit.PathDistance, path.Length);
                float gapAhead = Mathf.Repeat(
                    next.Fruit.PathDistance - current.Fruit.PathDistance, path.Length);

                // More room ahead than behind => accelerate gently; the reverse
                // slows down gently. All fruit remain moving forward at all times.
                float balance = gapAhead - gapBehind;
                float speedOffset = Mathf.Clamp(
                    balance / spacingScale * Mathf.Max(spacingCorrection, 0.65f) * spacingGain,
                    -maximumSpeedOffset,
                    maximumSpeedOffset);
                current.DesiredSpeed = Mathf.Max(speed * 0.62f,
                    current.DesiredSpeed + speedOffset * correctionBlend);
            }
        }

        private void BeginSpacingRelaxation()
        {
            spacingDelayRemaining = 0f;
            float loopSpeed = Mathf.Max(0.1f, Mathf.Min(speed, maxComfortableLoopSpeed));
            float lapDuration = path != null ? path.Length / loopSpeed : 2f;
            spacingRelaxDuration = lapDuration * Mathf.Clamp(spacingSettleLapFraction, 0.25f, 0.4f);
            spacingRelaxRemaining = spacingRelaxDuration;
        }

    }
}
