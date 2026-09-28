using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private readonly float[] spacingPositions = new float[MaxLoopCapacity];
        private readonly float[] spacingGaps = new float[MaxLoopCapacity];
        private readonly float[] spacingStartPositions = new float[MaxLoopCapacity];
        private readonly float[] spacingSteps = new float[MaxLoopCapacity];

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
            spacingFruits.Clear();
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                if (item.Fruit != null && item.Fruit.State == FruitState.OnLoop)
                    stableFruits.Add(item);
                if (item.Fruit != null && (item.Fruit.State == FruitState.OnLoop
                    || item.Fruit.State == FruitState.MergingToLane))
                    spacingFruits.Add(item);
            }
            ResolveFruitSpacing();
            for (int index = 0; index < stableFruits.Count; index++)
            {
                LoopFruit item = stableFruits[index];
                FruitActor fruit = item.Fruit;
                path.Evaluate(fruit.PathDistance,
                    out Vector3 pathPosition, out Quaternion pathRotation);
                float clearance = GetEntryClearance(item);
                // Settle the new fruit only as the neighbours make room. Incoming
                // pressure gently dips the older fruit and then releases it.
                float settleWeight = Mathf.Clamp01(clearance / Mathf.Max(0.01f, minimumFruitGap));
                item.EntryLift = Mathf.MoveTowards(item.EntryLift, 0f,
                    entryLiftHeight / 0.18f * settleWeight * Time.deltaTime);
                float targetOffset = item.EntryLift - GetEntryPressure(item) * entryPressDepth;
                item.VerticalOffset = Mathf.SmoothDamp(item.VerticalOffset, targetOffset,
                    ref item.VerticalVelocity, entryPressureSmoothTime,
                    Mathf.Infinity, Time.deltaTime);
                Vector2 desiredPosition = (Vector2)pathPosition + Vector2.up * item.VerticalOffset;
                Vector2 constrainedPosition = pathAuthoring.ConstrainFruitCenterToTrack(
                    desiredPosition,
                    pathPosition, fruit.GetWorldCollisionRadius());
                // Hand-edited wall vertices can abruptly change the closest edge.
                // Smooth only that correction, keeping conveyor travel immediate.
                item.WallOffset = Vector2.SmoothDamp(item.WallOffset,
                    constrainedPosition - desiredPosition, ref item.WallOffsetVelocity,
                    0.12f, 1.2f, Time.deltaTime);
                float laneSpeed = Mathf.Max(0.1f, Mathf.Min(speed, maxComfortableLoopSpeed));
                float targetRollSpeed = -fruitRollDegreesPerSecond * item.RollSpeedFactor
                    * Mathf.Clamp(fruit.CurrentSpeed / laneSpeed, 0.8f, 1.3f);
                // Roll speeds ease in and respond softly when neighbours push.
                item.RollSpeed = Mathf.Lerp(item.RollSpeed, targetRollSpeed,
                    1f - Mathf.Exp(-6f * Time.deltaTime));
                item.RollAngle = Mathf.Repeat(item.RollAngle + item.RollSpeed * Time.deltaTime, 360f);
                Quaternion rollingRotation = pathRotation
                    * Quaternion.AngleAxis(item.RollAngle, Vector3.forward);
                fruit.SetSmoothLoopPose(desiredPosition + item.WallOffset, rollingRotation, 9f);
            }
        }

        private void ResolveFruitSpacing()
        {
            int count = spacingFruits.Count;
            if (count == 0 || path.Length <= 0f) return;
            spacingFruits.Sort((left, right) =>
            {
                int comparison = GetSpacingDistance(left).CompareTo(GetSpacingDistance(right));
                return comparison != 0 ? comparison
                    : left.Fruit.GetInstanceID().CompareTo(right.Fruit.GetInstanceID());
            });

            float totalRequired = 0f;
            for (int index = 0; index < count; index++)
            {
                FruitActor behind = spacingFruits[index].Fruit;
                FruitActor ahead = spacingFruits[(index + 1) % count].Fruit;
                spacingPositions[index] = GetSpacingDistance(spacingFruits[index]);
                spacingStartPositions[index] = spacingPositions[index];
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
            float smoothTime = Mathf.Max(0.04f, spacingSmoothTime);
            float maxPushSpeed = Mathf.Min(speed, maxComfortableLoopSpeed)
                * maxSpacingPushSpeedRatio;
            for (int index = 0; index < count; index++)
            {
                LoopFruit item = spacingFruits[index];
                if (item.Fruit.State != FruitState.OnLoop)
                {
                    spacingSteps[index] = -1f; // Reservation, not a fruit driven by the lane.
                    continue;
                }
                float correction = Mathf.Max(0f,
                    spacingPositions[index] - spacingStartPositions[index]);
                float desiredPushSpeed = Mathf.Min(maxPushSpeed, correction / smoothTime);
                // Each fruit owns its velocity. A changing neighbour or a new
                // arrival cannot instantly redistribute a shared blend factor.
                item.SpacingPushSpeed = Mathf.SmoothDamp(item.SpacingPushSpeed,
                    desiredPushSpeed, ref item.SpacingPushAcceleration,
                    smoothTime, Mathf.Infinity, Time.deltaTime);
                // Keep integrating while pressure eases off, instead of clipping
                // displacement to zero as soon as a gap is a fraction wider.
                spacingSteps[index] = item.SpacingPushSpeed * Time.deltaTime;
            }
            LimitSpacingSteps(spacingStartPositions, spacingSteps, count, path.Length);
            for (int index = 0; index < count; index++)
            {
                FruitActor fruit = spacingFruits[index].Fruit;
                // The incoming fruit reserves space during its descent. Push the
                // lane ahead of it without dragging its merge destination away.
                if (fruit.State != FruitState.OnLoop) continue;
                fruit.CurrentSpeed += Time.deltaTime > 0f
                    ? spacingSteps[index] / Time.deltaTime
                    : 0f;
                fruit.PathDistance = Mathf.Repeat(
                    spacingStartPositions[index] + spacingSteps[index], path.Length);
            }
        }

        private float GetSpacingDistance(LoopFruit item)
        {
            if (item.Fruit.State == FruitState.OnLoop) return item.Fruit.PathDistance;
            float remaining = Mathf.Max(0f, item.MergeDuration - item.MergeElapsed);
            return Mathf.Repeat(item.MergeTargetDistance
                - Mathf.Min(speed, maxComfortableLoopSpeed) * remaining, path.Length);
        }

        private float GetEntryClearance(LoopFruit item)
        {
            float clearance = path.Length;
            for (int index = 0; index < spacingFruits.Count; index++)
            {
                LoopFruit other = spacingFruits[index];
                if (other == item) continue;
                float required = item.Fruit.GetWorldCollisionRadius()
                    + other.Fruit.GetWorldCollisionRadius();
                clearance = Mathf.Min(clearance, CircularSeparation(
                    item.Fruit.PathDistance, GetSpacingDistance(other)) - required);
            }
            return clearance;
        }

        private float GetEntryPressure(LoopFruit item)
        {
            float pressure = 0f;
            for (int index = 0; index < spacingFruits.Count; index++)
            {
                LoopFruit other = spacingFruits[index];
                if (other == item) continue;
                bool merging = other.Fruit.State == FruitState.MergingToLane;
                if (!merging && other.EntryLift <= 0.001f) continue;
                // A settling fruit only presses fruit below it, not another higher layer.
                if (!merging && other.EntryLift <= item.EntryLift) continue;
                float radius = item.Fruit.GetWorldCollisionRadius()
                    + other.Fruit.GetWorldCollisionRadius() + minimumFruitGap;
                float separation = CircularSeparation(
                    item.Fruit.PathDistance, GetSpacingDistance(other));
                float weight = merging
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(other.MergeElapsed / other.MergeDuration) * 2f)
                    : Mathf.Clamp01(other.EntryLift / entryLiftHeight);
                float contact = 1f - Mathf.Clamp01(separation / Mathf.Max(0.01f, radius));
                pressure = Mathf.Max(pressure, contact * contact * weight);
            }
            return pressure;
        }

        private static void LimitSpacingSteps(float[] starts, float[] steps,
            int count, float length)
        {
            // Preserve stable-fruit order while independent velocities settle.
            // Incoming upper-layer reservations do not act as stationary barriers.
            for (int pass = 0; pass < count; pass++)
            {
                bool changed = false;
                for (int index = count - 1; index >= 0; index--)
                {
                    if (steps[index] < 0f) continue;
                    int next = (index + 1) % count;
                    while (next != index && steps[next] < 0f) next = (next + 1) % count;
                    if (next == index) continue;
                    float gap = starts[next] - starts[index];
                    if (next < index) gap += length;
                    float limit = gap + steps[next];
                    if (steps[index] <= limit) continue;
                    steps[index] = limit > 0f ? limit : 0f;
                    changed = true;
                }
                if (!changed) break;
            }
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
