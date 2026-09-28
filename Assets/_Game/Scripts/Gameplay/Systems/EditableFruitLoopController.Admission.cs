using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private void BeginWaterfallMerge(FruitActor fruit, IntakeFlight flight, float overflow)
        {
            FindMergeTargetDistance(out float targetDistance, out float duration);
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
                MergeStartVelocity = flight.ArrivalVelocity,
                // UpdateLaneMerges runs later in this frame. Preserve sub-frame arrival time.
                MergeElapsed = overflow - Time.deltaTime,
                MergeDuration = duration,
                MergeTargetDistance = targetDistance
            });
        }

        private void FindMergeTargetDistance(out float targetDistance, out float duration)
        {
            float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            Vector2 start = GetWaterfallStartPosition();
            // Enter immediately at the same short section after P00, even when
            // occupied. Conveyor spacing handles overlap once fruit is on the lane.
            targetDistance = Mathf.Repeat(entryDistance + laneMergeAdvance, path.Length);
            path.Evaluate(targetDistance, out Vector3 target, out _);
            duration = Mathf.Max(laneMergeDuration,
                Vector2.Distance(start, target) / Mathf.Max(0.1f, laneSpeed));
        }
    }
}
