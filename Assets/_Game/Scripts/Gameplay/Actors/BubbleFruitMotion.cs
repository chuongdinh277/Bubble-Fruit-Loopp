using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class BubbleFruitMotion : MonoBehaviour
    {
        [SerializeField] private FruitActor[] fruits;
        [SerializeField] private float orbitStrength = 0.42f;
        [SerializeField] private float driftStrength = 0.16f;
        [SerializeField] private float maxSpeed = 0.85f;
        [SerializeField, Min(0.1f)] private float containmentRadius = 1.6f;
        [SerializeField] private int direction = 1;
        [SerializeField] private float seed = 7.3f;
        private bool isRunning;

        private Vector2[] localVelocities;
        private Vector2[] localPositions;
        private float[] orbitMultipliers;
        private float[] driftMultipliers;
        private float[] speedLimits;
        private float[] noiseRates;
        private float[] phaseOffsets;
        private float[] accelerationMultipliers;
        private int[] orbitDirections;
        
        public void Configure(FruitActor[] controlledFruits, int orbitDirection, float motionSeed,
            float newContainmentRadius = 1.6f)
        {
            fruits = controlledFruits;
            direction = orbitDirection >= 0 ? 1 : -1;
            seed = motionSeed;
            containmentRadius = Mathf.Max(0.1f, newContainmentRadius);
            localPositions = null;
            localVelocities = null;
            orbitMultipliers = null;
            driftMultipliers = null;
            speedLimits = null;
            noiseRates = null;
            phaseOffsets = null;
            accelerationMultipliers = null;
            orbitDirections = null;
            InitializeArrays();
        }

        private void InitializeArrays()
        {
            if (fruits != null && localPositions == null)
            {
                localVelocities = new Vector2[fruits.Length];
                localPositions = new Vector2[fruits.Length];
                orbitMultipliers = new float[fruits.Length];
                driftMultipliers = new float[fruits.Length];
                speedLimits = new float[fruits.Length];
                noiseRates = new float[fruits.Length];
                phaseOffsets = new float[fruits.Length];
                accelerationMultipliers = new float[fruits.Length];
                orbitDirections = new int[fruits.Length];
                for (int i = 0; i < fruits.Length; i++)
                {
                    if (fruits[i] != null)
                    {
                        localPositions[i] = fruits[i].CachedTransform.localPosition;
                        float orbitRandom = Mathf.PerlinNoise(seed + i * 13.17f, 0.31f);
                        float driftRandom = Mathf.PerlinNoise(seed + i * 7.43f, 2.17f);
                        float speedRandom = Mathf.PerlinNoise(seed + i * 19.61f, 4.73f);
                        orbitMultipliers[i] = Mathf.Lerp(0.48f, 1.08f, orbitRandom);
                        driftMultipliers[i] = Mathf.Lerp(0.60f, 1.55f, driftRandom);
                        speedLimits[i] = maxSpeed * Mathf.Lerp(0.38f, 0.82f, speedRandom);
                        accelerationMultipliers[i] = Mathf.Lerp(0.65f, 1.35f,
                            Mathf.PerlinNoise(seed + i * 11.23f, 12.67f));
                        orbitDirections[i] = Mathf.PerlinNoise(seed + i * 17.91f, 15.31f) > 0.76f ? -1 : 1;
                        noiseRates[i] = Mathf.Lerp(0.16f, 0.38f,
                            Mathf.PerlinNoise(seed + i * 5.91f, 7.29f));
                        phaseOffsets[i] = Mathf.PerlinNoise(seed + i * 3.77f, 9.41f) * 24f;

                        Vector2 radial = localPositions[i];
                        int localDirection = direction * orbitDirections[i];
                        Vector2 tangent = radial.sqrMagnitude > 0.001f
                            ? new Vector2(-radial.y, radial.x).normalized * localDirection
                            : Vector2.right * localDirection;
                        localVelocities[i] = tangent * speedLimits[i] * 0.15f;
                    }
                }
            }
        }

        private void Start()
        {
            InitializeArrays();
        }

        public void StartMotion() => isRunning = true;
        public void StopMotion() => isRunning = false;

        private void FixedUpdate()
        {
            if (!isRunning || fruits == null || localPositions == null || localVelocities == null) return;
            
            float dt = Time.fixedDeltaTime;
            float time = Time.time;
            
            for (int i = 0; i < fruits.Length; i++)
            {
                if (fruits[i] == null) continue;
                
                Vector2 pos = localPositions[i];
                
                // 1. Calculate tangent and noise forces relative to bubble center (0,0)
                Vector2 radial = pos;
                int localDirection = direction * orbitDirections[i];
                Vector2 tangent = radial.sqrMagnitude > 0.001f
                    ? new Vector2(-radial.y, radial.x).normalized * localDirection
                    : Vector2.right * localDirection;
                    
                float localTime = time * noiseRates[i] + phaseOffsets[i];
                float noiseX = Mathf.PerlinNoise(seed + i * 0.37f, localTime) * 2f - 1f;
                float noiseY = Mathf.PerlinNoise(seed + 20f + i * 0.29f, localTime) * 2f - 1f;
                
                Vector2 force = tangent * (orbitStrength * orbitMultipliers[i])
                    + new Vector2(noiseX, noiseY) * (driftStrength * driftMultipliers[i]);
                
                // 2. Integrate velocity (multiplying by a factor to match the old AddForce feel)
                localVelocities[i] += force * dt * (16f * accelerationMultipliers[i]);
                
                // 3. Apply soft drag
                localVelocities[i] *= 0.975f;
                
                // 4. Clamp velocity
                float speedLimit = speedLimits[i];
                if (localVelocities[i].sqrMagnitude > speedLimit * speedLimit)
                {
                    localVelocities[i] = localVelocities[i].normalized * speedLimit;
                }
                
                // 5. Fake collisions between fruits to prevent overlapping
                for (int j = 0; j < fruits.Length; j++)
                {
                    if (i == j || fruits[j] == null) continue;
                    Vector2 diff = pos - localPositions[j];
                    float distSqr = diff.sqrMagnitude;
                    if (distSqr < 0.6f * 0.6f && distSqr > 0.001f)
                    {
                        float dist = Mathf.Sqrt(distSqr);
                        Vector2 push = diff.normalized * (0.6f - dist);
                        pos += push * 0.5f; // Resolve overlap slightly
                        
                        // Transfer a bit of velocity
                        localVelocities[i] += push * 5f; 
                    }
                }
                
                // 6. Integrate position
                pos += localVelocities[i] * dt;
                
                // 7. Keep the fruit centre inside the resized shell. The radius is
                // supplied by the level builder after subtracting fruit clearance.
                if (pos.sqrMagnitude > containmentRadius * containmentRadius)
                {
                    pos = pos.normalized * containmentRadius;
                    // Remove outward velocity so a fruit does not visually stick
                    // through the rim for several frames.
                    float outwardSpeed = Vector2.Dot(localVelocities[i], pos.normalized);
                    if (outwardSpeed > 0f)
                        localVelocities[i] -= pos.normalized * outwardSpeed;
                }
                
                // 8. Apply visual state
                localPositions[i] = pos;
                fruits[i].CachedTransform.localPosition = pos;
            }
        }
    }
}

