using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class OnLoopState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {
            if (fruit.ExecutionPhase == FruitExecutionPhase.LoopAdvance) Advance(fruit);
            else if (fruit.ExecutionPhase == FruitExecutionPhase.LoopPose) ApplyPose(fruit);
        }

        internal static void Advance(Fruit fruit)
        {
            FruitLoopManager loop = fruit.LoopOwner;
            float loopSpeed = Mathf.Min(loop.speed, loop.maxComfortableLoopSpeed);
            fruit.CurrentSpeed = loopSpeed;
            fruit.PathDistance = Mathf.Repeat(
                fruit.PathDistance + loopSpeed * Time.deltaTime, loop.path.Length);
        }
        internal static void ApplyPose(Fruit fruit)
        {
            FruitLoopManager loop = fruit.LoopOwner;
            FruitLoopData item = fruit.LoopData;
            loop.path.Evaluate(fruit.PathDistance,
                out Vector3 pathPosition, out Quaternion pathRotation);
            float clearance = loop.GetEntryClearance(item);
            // Settle the new fruit only as the neighbours make room. Incoming
            // pressure gently dips the older fruit and then releases it.
            float settleWeight = Mathf.Clamp01(clearance / Mathf.Max(0.01f, loop.minimumFruitGap));
            item.EntryLift = Mathf.MoveTowards(item.EntryLift, 0f,
                loop.entryLiftHeight / 0.18f * settleWeight * Time.deltaTime);
            float targetOffset = item.EntryLift - loop.GetEntryPressure(item) * loop.entryPressDepth;
            item.VerticalOffset = Mathf.SmoothDamp(item.VerticalOffset, targetOffset,
                ref item.VerticalVelocity, loop.entryPressureSmoothTime,
                Mathf.Infinity, Time.deltaTime);
            Vector2 desiredPosition = (Vector2)pathPosition + Vector2.up * item.VerticalOffset;
            Vector2 constrainedPosition = loop.pathAuthoring.ConstrainFruitCenterToTrack(
                desiredPosition,
                pathPosition, fruit.GetWorldCollisionRadius());
            // Hand-edited wall vertices can abruptly change the closest edge.
            // Smooth only that correction, keeping conveyor travel immediate.
            item.WallOffset = Vector2.SmoothDamp(item.WallOffset,
                constrainedPosition - desiredPosition, ref item.WallOffsetVelocity,
                0.12f, 1.2f, Time.deltaTime);
            float laneSpeed = Mathf.Max(0.1f, Mathf.Min(loop.speed, loop.maxComfortableLoopSpeed));
            float targetRollSpeed = -loop.fruitRollDegreesPerSecond * item.RollSpeedFactor
                * Mathf.Clamp(fruit.CurrentSpeed / laneSpeed, 0.8f, 1.3f);
            // Roll speeds ease in and respond softly when neighbours push.
            item.RollSpeed = Mathf.Lerp(item.RollSpeed, targetRollSpeed,
                1f - Mathf.Exp(-6f * Time.deltaTime));
            item.RollAngle = Mathf.Repeat(item.RollAngle + item.RollSpeed * Time.deltaTime, 360f);
            Quaternion rollingRotation = pathRotation
                * Quaternion.AngleAxis(item.RollAngle, Vector3.forward);
            fruit.SetSmoothLoopPose(desiredPosition + item.WallOffset, rollingRotation, 9f);
        }


        internal static void CompleteLaneCapture(Fruit fruit, float distance, Vector2 entryVelocity)
        {
            fruit.pathDistance = distance;
            fruit.ChangeFruitStateTo(FruitStates.For(FruitStatus.OnLoop), false);
            // Intake, merge, and conveyor share one render clock and transform owner.
            // Loop contacts were already ignored by the controller.
            fruit.body.simulated = false;
            fruit.body.bodyType = RigidbodyType2D.Kinematic;
            fruit.body.gravityScale = 0f;
            fruit.body.linearVelocity = Vector2.zero;
            fruit.body.angularVelocity = 0f;
            fruit.body.interpolation = RigidbodyInterpolation2D.None;

        }
    }
}
