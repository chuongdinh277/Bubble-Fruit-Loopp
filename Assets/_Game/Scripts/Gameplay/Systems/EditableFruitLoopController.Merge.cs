using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private void UpdateMergingFruit()
        {
            // Keep the physics tick focused on dispatching the merge update.
            UpdateMergingFruitPositions();
        }

        // Advance and render every fruit still travelling into the loop lane.
        private void UpdateMergingFruitPositions()
        {
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                if (item.Fruit == null || item.Fruit.State != FruitState.MergingToLane) continue;

                item.MergeElapsed += Time.fixedDeltaTime;
                float effectiveMergeDuration = Mathf.Max(0.05f, Mathf.Min(mergeDuration, fastMergeDuration));
                float progress = Mathf.Clamp01(item.MergeElapsed / effectiveMergeDuration);
                float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
                const float startingSpeedFactor = 0.68f;
                float duration = effectiveMergeDuration;
                float mergeDistance = Mathf.Max(
                    jumpLandingAdvance + LaneCarryDistance,
                    laneSpeed * duration * 0.42f);
                item.TargetDistance = Mathf.Repeat(entryDistance + mergeDistance, path.Length);

                // Keep one fixed capture point so the curve does not chase a
                // moving lane target while the fruit is already in flight.
                path.Evaluate(item.TargetDistance, out Vector3 lanePoint, out Quaternion rotation);
                Vector2 endTangent = ((Vector2)(rotation * Vector3.up)).normalized;
                Vector2 throatCenter = loopStart != null
                    ? (Vector2)loopStart.position
                    : (Vector2)GetEntryPosition();
                Vector2 laneMouth = GetEntryPosition();
                Vector2 approachPosition = item.MergeStartPosition;
                float sideOffset = approachPosition.x - throatCenter.x;
                if (Mathf.Abs(sideOffset) < 0.12f)
                    sideOffset = item.MergeStartVelocity.x;
                if (Mathf.Abs(sideOffset) < 0.01f)
                    sideOffset = lanePoint.x - approachPosition.x;
                float approachSide = sideOffset >= 0f ? 1f : -1f;
                float fruitVariation = 0.5f
                    + 0.5f * SignedVariation(item.Fruit.GetInstanceID());
                float neckOffset = approachSide < 0f
                    ? Mathf.Lerp(-0.24f, -0.12f, fruitVariation)
                    : Mathf.Lerp(-0.02f, 0.20f, fruitVariation);
                Vector2 neckMidpoint = new Vector2(
                    throatCenter.x + neckOffset,
                    Mathf.Lerp(throatCenter.y, laneMouth.y, 0.5f));
                Vector2 position = EvaluateNeckEntryCurve(item.MergeStartPosition,
                    lanePoint, throatCenter, neckMidpoint, approachSide, endTangent, progress);
                item.Fruit.CurrentSpeed = Mathf.Lerp(
                    laneSpeed * startingSpeedFactor, laneSpeed, progress);
                item.Fruit.CachedTransform.position = position;

                if (progress < 1f) continue;

                item.Fruit.CurrentSpeed = laneSpeed;
                item.DesiredSpeed = laneSpeed;
                item.Fruit.CompleteLaneCapture(item.TargetDistance, endTangent * laneSpeed);
                // The fruit is already travelling at lane speed on contact, so
                // there is no artificial settle/pause before spacing takes over.
                BeginSpacingRelaxation();
                pathAuthoring.SetOuterBoundaryIgnored(item.Fruit.BodyCollider, false);
            }
        }

        // Rise toward the narrow neck line, then fall on a tangent-matched curve.
        private static Vector2 EvaluateNeckEntryCurve(Vector2 start, Vector2 end,
            Vector2 throatCenter, Vector2 neckMidpoint, float approachSide,
            Vector2 endTangent, float progress)
        {
            float forwardSign = Mathf.Abs(throatCenter.x - start.x) > 0.08f
                ? Mathf.Sign(throatCenter.x - start.x)
                : -approachSide;
            Vector2 forward = Vector2.right * forwardSign;
            Vector2 middleTangent = ((end - neckMidpoint) + forward * 0.18f).normalized;
            float middleHandle = Mathf.Clamp(
                Vector2.Distance(start, neckMidpoint) * 0.28f, 0.22f, 0.46f);
            Vector2 launchDirection = (forward * 0.78f + Vector2.up * 0.62f).normalized;
            Vector2 liftControl = start + launchDirection * 0.50f;
            Vector2 beforeMidpoint = neckMidpoint - middleTangent * middleHandle;
            Vector2 afterMidpoint = neckMidpoint + middleTangent * middleHandle;
            Vector2 captureControl = end - endTangent * 0.48f;

            if (progress < 0.5f)
                return CubicBezier(start, liftControl, beforeMidpoint,
                    neckMidpoint, progress * 2f);

            return CubicBezier(neckMidpoint, afterMidpoint, captureControl,
                end, (progress - 0.5f) * 2f);
        }

        private static Vector2 CubicBezier(Vector2 start, Vector2 control1,
            Vector2 control2, Vector2 end, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * start
                + 3f * inverse * inverse * t * control1
                + 3f * inverse * t * t * control2
                + t * t * t * end;
        }

    }
}
