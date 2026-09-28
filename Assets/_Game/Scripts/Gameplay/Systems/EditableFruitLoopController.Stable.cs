using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private void UpdateStableLoop()
        {
            if (active.Count == 0) return;

            float loopSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                FruitActor fruit = item.Fruit;
                if (fruit == null || fruit.State != FruitState.OnLoop) continue;

                // Measure the real Rigidbody position against the lane. Nothing
                // teleports here: colliders own motion and contact immediately.
                float distance = path.FindClosestDistance(fruit.CachedTransform.position);
                fruit.PathDistance = distance;
                path.Evaluate(distance, out Vector3 lanePosition, out Quaternion rotation);
                Vector2 tangent = rotation * Vector3.up;

                fruit.FollowPhysicalPath(
                    lanePosition,
                    tangent,
                    loopSpeed,
                    laneCenteringStrength,
                    physicalLoopMaxSpeed);
            }
        }
    }
}
