using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BubbleFruitLoop.Gameplay", "Assembly-CSharp", "BubbleFruitMotion")]
    public sealed class BubbleManager : MonoBehaviour
    {
        [SerializeField] internal Fruit[] fruits;
        [SerializeField] internal float orbitStrength = 0.42f;
        [SerializeField] internal float driftStrength = 0.16f;
        [SerializeField] private float maxSpeed = 0.85f;
        [SerializeField, Min(0.1f)] internal float containmentRadius = 1.6f;
        [SerializeField] internal int direction = 1;
        [SerializeField] internal float seed = 7.3f;
        private bool isRunning;

        internal Vector2[] localVelocities;
        internal Vector2[] localPositions;
        internal float[] orbitMultipliers;
        internal float[] driftMultipliers;
        internal float[] speedLimits;
        internal float[] noiseRates;
        internal float[] phaseOffsets;
        internal float[] accelerationMultipliers;
        internal int[] orbitDirections;

        public void Configure(Fruit[] controlledFruits, int orbitDirection, float motionSeed,
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

                FruitManager.ExecuteBubble(fruits[i], this, i, dt, time);

            }
        }
    }
}

