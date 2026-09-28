using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private void BeginWaterfallMerge(FruitActor fruit, IntakeFlight flight, float overflow)
        {
            FindMergeTargetDistance(fruit, out float targetDistance, out float duration);
            owned.Add(fruit);
            float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            fruit.CurrentSpeed = laneSpeed;
            fruit.SetState(FruitState.MergingToLane);
            fruit.DisablePhysics();
            active.Add(new LoopFruit
            {
                Fruit = fruit,
                MergeStartPosition = flight.LandingPosition,
                MergeStartRotation = fruit.CachedTransform.rotation,
                // Continue the fall's velocity through the handoff without a pause.
                MergeStartVelocity = flight.ArrivalVelocity,
                MergeElapsed = overflow - Time.deltaTime,
                MergeDuration = duration,
                MergeTargetDistance = targetDistance,
                // Small stable variations keep fruit from rotating in lockstep.
                RollSpeedFactor = 0.8f + (unchecked((uint)fruit.GetInstanceID()) % 41u) * 0.01f
            });
        }

        private void FindMergeTargetDistance(FruitActor incoming,
            out float targetDistance, out float duration)
        {
            float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            Vector2 start = GetWaterfallStartPosition();
            targetDistance = entryDistance;
            duration = laneMergeDuration;
            float bestClearance = float.NegativeInfinity;
            float radius = incoming.GetWorldCollisionRadius();
            // Search only the first rightward section, never a remote gap around
            // the loop. This avoids the earlier leftward/cross-centre shortcuts.
            float firstAdvance = Mathf.Max(0.2f, laneMergeAdvance - 0.12f);
            float lastAdvance = Mathf.Min(0.72f, laneMergeAdvance + 0.20f);
            const int candidates = 9;
            for (int candidate = 0; candidate < candidates; candidate++)
            {
                float advance = Mathf.Lerp(firstAdvance, lastAdvance,
                    candidate / (float)(candidates - 1));
                float distance = Mathf.Repeat(entryDistance + advance, path.Length);
                path.Evaluate(distance, out Vector3 target, out _);
                float mergeTime = Mathf.Max(laneMergeDuration,
                    Vector2.Distance(start, target) / Mathf.Max(0.1f, laneSpeed));
                float clearance = path.Length;
                for (int index = 0; index < active.Count; index++)
                {
                    LoopFruit other = active[index];
                    if (other.Fruit == null) continue;
                    float predicted;
                    if (other.Fruit.State == FruitState.MergingToLane)
                    {
                        float remaining = Mathf.Max(0f,
                            other.MergeDuration - other.MergeElapsed - Time.deltaTime);
                        predicted = other.MergeTargetDistance
                            + laneSpeed * Mathf.Max(0f, mergeTime - remaining);
                    }
                    else
                        predicted = other.Fruit.PathDistance
                            + Mathf.Max(laneSpeed, other.Fruit.CurrentSpeed) * mergeTime;
                    float required = (radius + other.Fruit.GetWorldCollisionRadius()
                        + minimumFruitGap) * 1.08f;
                    path.Evaluate(predicted, out Vector3 otherPosition, out _);
                    float separation = Mathf.Min(CircularSeparation(distance, predicted),
                        Vector2.Distance(target, otherPosition));
                    clearance = Mathf.Min(clearance, separation - required);
                }
                // Prefer the nominal entry when the local lane is equally empty.
                if (clearance < bestClearance) continue;
                if (Mathf.Approximately(clearance, bestClearance)
                    && Mathf.Abs(advance - laneMergeAdvance) >= Mathf.Abs(
                        Mathf.Repeat(targetDistance - entryDistance, path.Length) - laneMergeAdvance))
                    continue;
                bestClearance = clearance;
                targetDistance = distance;
                duration = mergeTime;
            }
        }
    }
}
