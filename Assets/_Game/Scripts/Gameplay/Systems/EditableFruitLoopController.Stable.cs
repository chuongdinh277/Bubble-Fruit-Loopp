using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private readonly float[] spacingPositions = new float[MaxLoopCapacity];
        private readonly float[] spacingGaps = new float[MaxLoopCapacity];

        private void UpdateStableLoop()
        {
            float loopSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            for (int index = 0; index < active.Count; index++)
            {
                FruitActor fruit = active[index].Fruit;
                if (fruit == null || fruit.State != FruitState.OnLoop) continue;
                fruit.CurrentSpeed = loopSpeed;
                fruit.PathDistance = Mathf.Repeat(
                    fruit.PathDistance + loopSpeed * Time.deltaTime, path.Length);
            }
        }

        private void UpdateLoopSpacingAndPoses()
        {
            stableFruits.Clear();
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                if (item.Fruit != null && item.Fruit.State == FruitState.OnLoop)
                    stableFruits.Add(item);
            }
            ResolveFruitSpacing();
            for (int index = 0; index < stableFruits.Count; index++)
            {
                FruitActor fruit = stableFruits[index].Fruit;
                path.Evaluate(fruit.PathDistance,
                    out Vector3 pathPosition, out Quaternion pathRotation);
                Vector2 constrainedPosition = pathAuthoring.ConstrainFruitCenterToTrack(
                    pathPosition, pathPosition, fruit.GetWorldCollisionRadius());
                fruit.SetSmoothLoopPose(constrainedPosition, pathRotation, 9f);
            }
        }

        private void ResolveFruitSpacing()
        {
            int count = stableFruits.Count;
            if (count < 2 || path.Length <= 0f) return;
            stableFruits.Sort((left, right) =>
                left.Fruit.PathDistance.CompareTo(right.Fruit.PathDistance));

            float totalRequired = 0f;
            for (int index = 0; index < count; index++)
            {
                FruitActor behind = stableFruits[index].Fruit;
                FruitActor ahead = stableFruits[(index + 1) % count].Fruit;
                spacingPositions[index] = behind.PathDistance;
                // A small curvature margin keeps sprites apart on the rounded ends.
                spacingGaps[index] = (behind.GetWorldCollisionRadius()
                    + ahead.GetWorldCollisionRadius() + minimumFruitGap) * 1.08f;
                totalRequired += spacingGaps[index];
            }

            // Oversized authored fruit cannot physically fit at full capacity.
            // In that case use all available track space without runaway pushing.
            if (totalRequired >= path.Length)
            {
                float fitScale = path.Length * 0.999f / totalRequired;
                for (int index = 0; index < count; index++)
                    spacingGaps[index] *= fitScale;
            }

            SolveCircularSpacing(spacingPositions, spacingGaps, count, path.Length);
            for (int index = 0; index < count; index++)
                stableFruits[index].Fruit.PathDistance = Mathf.Repeat(
                    spacingPositions[index], path.Length);
        }

        // Propagate only the missing clearance forward through each contact chain.
        // Unwrapped distances preserve fruit order, including coincident arrivals
        // and contact between the last and first fruit across P00.
        private static void SolveCircularSpacing(float[] positions, float[] gaps,
            int count, float length)
        {
            for (int pass = 0; pass < count; pass++)
            {
                bool changed = false;
                for (int index = 0; index < count - 1; index++)
                {
                    float minimum = positions[index] + gaps[index];
                    if (positions[index + 1] >= minimum) continue;
                    positions[index + 1] = minimum;
                    changed = true;
                }
                float firstMinimum = positions[count - 1] + gaps[count - 1] - length;
                if (positions[0] < firstMinimum)
                {
                    positions[0] = firstMinimum;
                    changed = true;
                }
                if (!changed) break;
            }
        }
    }
}
