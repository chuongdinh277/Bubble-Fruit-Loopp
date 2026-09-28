using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class MergingToLaneState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {
            if (fruit.ExecutionPhase == FruitExecutionPhase.Merge) ExecuteMerge(fruit);
        }

        internal static void ExecuteMerge(Fruit fruit)
        {
            FruitLoopManager loop = fruit.LoopOwner;
            FruitLoopData item = fruit.LoopData;
            float laneSpeed = Mathf.Min(loop.speed, loop.maxComfortableLoopSpeed);
            float elapsed = item.MergeElapsed + Time.deltaTime;
            float overflow = Mathf.Max(0f, elapsed - item.MergeDuration);
            item.MergeElapsed = Mathf.Min(elapsed, item.MergeDuration);
            float progress = Mathf.Clamp01(item.MergeElapsed / item.MergeDuration);
            loop.path.Evaluate(item.MergeTargetDistance,
                out Vector3 targetPosition, out Quaternion targetRotation);
            Vector2 endTangent = ((Vector2)(targetRotation * Vector3.up)).normalized;
            Vector2 startTangent = item.MergeStartVelocity * item.MergeDuration;
            Vector2 finishTangent = endTangent * laneSpeed * item.MergeDuration;
            fruit.CachedTransform.position = HermitePosition(
                item.MergeStartPosition, targetPosition,
                startTangent, finishTangent, progress)
                + Vector2.up * (loop.entryLiftHeight * Mathf.SmoothStep(0f, 1f, progress));
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            fruit.CachedTransform.rotation = Quaternion.Slerp(
                item.MergeStartRotation, targetRotation, smoothProgress);

            if (progress < 1f) return;
            float completedDistance = Mathf.Repeat(
                item.MergeTargetDistance + laneSpeed * overflow, loop.path.Length);
            loop.path.Evaluate(completedDistance, out Vector3 completedPosition,
                out Quaternion completedRotation);
            item.EntryLift = loop.entryLiftHeight;
            item.VerticalOffset = loop.entryLiftHeight;
            fruit.CachedTransform.position = completedPosition + Vector3.up * loop.entryLiftHeight;
            Vector2 tangent = completedRotation * Vector3.up;
            fruit.CurrentSpeed = laneSpeed;
            fruit.SetBodyColliderEnabled(true);
            loop.SetLoopFruitContactsIgnored(fruit, true);
            fruit.CompleteLaneCapture(
                completedDistance, tangent.normalized * laneSpeed);
            loop.pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
        }
        private static Vector2 HermitePosition(Vector2 start, Vector2 end,
            Vector2 startTangent, Vector2 endTangent, float time)
        {
            float time2 = time * time;
            float time3 = time2 * time;
            return (2f * time3 - 3f * time2 + 1f) * start
                + (time3 - 2f * time2 + time) * startTangent
                + (-2f * time3 + 3f * time2) * end
                + (time3 - time2) * endTangent;

        }


        internal static void BeginLaneCapture(Fruit fruit, float distance)
        {
            fruit.pathDistance = distance;
            fruit.ChangeFruitStateTo(FruitStates.For(FruitStatus.MergingToLane), false);
            fruit.body.simulated = true;
            fruit.body.bodyType = RigidbodyType2D.Dynamic;
            fruit.body.gravityScale = 1f;
            fruit.body.linearDamping = 0.7f;
            fruit.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            fruit.body.interpolation = RigidbodyInterpolation2D.Interpolate;

        }
    }
}
