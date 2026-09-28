using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class InsideBubbleState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit)
        {
        }

        public static void OnExecute(Fruit fruit)
        {
            if (fruit.ExecutionPhase == FruitExecutionPhase.InsideBubble)
                ExecuteMotion(fruit);
        }

        internal static void ExecuteMotion(Fruit fruit)
        {
            BubbleManager bubble = fruit.BubbleOwner;
            int i = fruit.BubbleIndex;
            float dt = fruit.BubbleDeltaTime;
            float time = fruit.BubbleTime;
            Vector2 pos = bubble.localPositions[i];
            // 1. Calculate tangent and noise forces relative to bubble center (0,0)
            ApplyOrbitVelocity(bubble, i, dt, time, pos);
            // 5. Fake collisions between bubble.fruits to prevent overlapping
            ResolveBubbleContactsAndAdvance(bubble, i, dt, ref pos);
            // 7. Keep the fruit centre inside the resized shell. The radius is
            // supplied by the level builder after subtracting fruit clearance.
            KeepFruitInsideBubble(bubble, i, ref pos);
        }

        private static void ApplyOrbitVelocity(BubbleManager bubble, int i, float dt, float time, Vector2 pos)
        {
            Vector2 radial = pos;
            int localDirection = bubble.direction * bubble.orbitDirections[i];
            Vector2 tangent = radial.sqrMagnitude > 0.001f ? new Vector2(-radial.y, radial.x).normalized * localDirection : Vector2.right * localDirection;
            float localTime = time * bubble.noiseRates[i] + bubble.phaseOffsets[i];
            float noiseX = Mathf.PerlinNoise(bubble.seed + i * 0.37f, localTime) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(bubble.seed + 20f + i * 0.29f, localTime) * 2f - 1f;
            Vector2 force = tangent * (bubble.orbitStrength * bubble.orbitMultipliers[i]) + new Vector2(noiseX, noiseY) * (bubble.driftStrength * bubble.driftMultipliers[i]);
            // 2. Integrate velocity (multiplying by a factor to match the old AddForce feel)
            IntegrateOrbitVelocity(bubble, i, dt, force);
        }

        private static void ResolveBubbleContactsAndAdvance(BubbleManager bubble, int i, float dt, ref Vector2 pos)
        {
            for (int j = 0; j < bubble.fruits.Length; j++)
            {
                if (i == j || bubble.fruits[j] == null)
                    continue;
                Vector2 diff = pos - bubble.localPositions[j];
                float distSqr = diff.sqrMagnitude;
                if (distSqr < 0.6f * 0.6f && distSqr > 0.001f)
                {
                    float dist = Mathf.Sqrt(distSqr);
                    Vector2 push = diff.normalized * (0.6f - dist);
                    pos += push * 0.5f; // Resolve overlap slightly
                    // Transfer a bit of velocity
                    bubble.localVelocities[i] += push * 5f;
                }
            }

            // 6. Integrate position
            pos += bubble.localVelocities[i] * dt;
        }

        private static void KeepFruitInsideBubble(BubbleManager bubble, int i, ref Vector2 pos)
        {
            if (pos.sqrMagnitude > bubble.containmentRadius * bubble.containmentRadius)
            {
                pos = pos.normalized * bubble.containmentRadius;
                // Remove outward velocity so a fruit does not visually stick
                // through the rim for several frames.
                float outwardSpeed = Vector2.Dot(bubble.localVelocities[i], pos.normalized);
                if (outwardSpeed > 0f)
                    bubble.localVelocities[i] -= pos.normalized * outwardSpeed;
            }

            // 8. Apply visual state
            bubble.localPositions[i] = pos;
            bubble.fruits[i].CachedTransform.localPosition = pos;
        }

        private static void IntegrateOrbitVelocity(BubbleManager bubble, int i, float dt, Vector2 force)
        {
            bubble.localVelocities[i] += force * dt * (16f * bubble.accelerationMultipliers[i]);
            // 3. Apply soft drag
            bubble.localVelocities[i] *= 0.975f;
            // 4. Clamp velocity
            float speedLimit = bubble.speedLimits[i];
            if (bubble.localVelocities[i].sqrMagnitude > speedLimit * speedLimit)
            {
                bubble.localVelocities[i] = bubble.localVelocities[i].normalized * speedLimit;
            }
        }
    }
}
