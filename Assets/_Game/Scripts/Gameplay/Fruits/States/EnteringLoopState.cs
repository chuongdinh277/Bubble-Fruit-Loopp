using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class EnteringLoopState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit)
        {
        }

        public static void OnExecute(Fruit fruit)
        {
            if (fruit.ExecutionPhase == FruitExecutionPhase.Intake)
                ExecuteFlight(fruit);
        }

        internal static void ExecuteFlight(Fruit fruit)
        {
            FruitIntakeData flight = fruit.IntakeData;
            flight.Elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(flight.Elapsed / flight.Duration);
            fruit.CachedTransform.position = SmoothWaterfallPosition(flight.StartPosition, flight.LandingPosition, flight.StartVelocity, flight.ArrivalVelocity, flight.Duration, progress);
        }

        internal static Vector2 SmoothWaterfallPosition(Vector2 start, Vector2 end, Vector2 incomingVelocity, Vector2 arrivalVelocity, float duration, float progress)
        {
            Vector2 offset = end - start;
            float distance = offset.magnitude;
            if (distance < 0.0001f)
                return end;
            Vector2 direction = offset / distance;
            // Preserve the chute's incoming motion without letting a fast or
            // sideways collision velocity fling the curve past its landing point.
            Vector2 startTangent;
            CalculateWaterfallStartTangent(incomingVelocity, direction, duration, distance, out startTangent);
            Vector2 finishTangent = arrivalVelocity * duration;
            return EvaluateWaterfallCurve(start, offset, startTangent, finishTangent, progress);
        }

        internal static Vector2 GetWaterfallArrivalVelocity(Vector2 start, Vector2 end, float duration, float laneSpeed)
        {
            Vector2 offset = end - start;
            float distance = offset.magnitude;
            if (distance < 0.0001f)
                return Vector2.zero;
            // Gradually bend the end of the fall toward the rightward conveyor.
            Vector2 direction = Vector2.Lerp(offset / distance, Vector2.right, 0.35f).normalized;
            return direction * Mathf.Min(laneSpeed * 0.35f, distance * 0.4f / Mathf.Max(0.01f, duration));
        }

        private static void CalculateWaterfallStartTangent(Vector2 incomingVelocity, Vector2 direction, float duration, float distance, out Vector2 startTangent)
        {
            float forward = Mathf.Clamp(Vector2.Dot(incomingVelocity, direction) * duration, 0f, distance * 1.6f);
            Vector2 lateral = incomingVelocity * duration - direction * Vector2.Dot(incomingVelocity, direction) * duration;
            startTangent = direction * forward + Vector2.ClampMagnitude(lateral, distance * 0.3f);
        }

        private static Vector2 EvaluateWaterfallCurve(Vector2 start, Vector2 offset, Vector2 startTangent, Vector2 finishTangent, float progress)
        {
            float t = Mathf.Clamp01(progress);
            float t2 = t * t;
            float t3 = t2 * t;
            float t4 = t3 * t;
            float t5 = t4 * t;
            // Match arrival velocity to the lane merge instead of decelerating to
            // rest at the marker. Both ends have zero acceleration.
            return start + offset * (10f * t3 - 15f * t4 + 6f * t5) + startTangent * (t - 6f * t3 + 8f * t4 - 3f * t5) + finishTangent * (-4f * t3 + 7f * t4 - 3f * t5);
        }
    }
}
