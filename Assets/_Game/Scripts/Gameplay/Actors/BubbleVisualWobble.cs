using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class BubbleVisualWobble : MonoBehaviour
    {
        [SerializeField] private Transform bubbleBack;
        [SerializeField] private Transform bubbleFront;
        [SerializeField] private float speed = 1.25f;
        [SerializeField] private float strength = 0.028f;
        [SerializeField] private float breathing = 0.009f;
        [SerializeField] private float phase = 2.4f;
        private Vector3 backBaseScale;
        private Vector3 frontBaseScale;

        public void Configure(Transform back, Transform front, float wobbleSpeed, float wobbleStrength, float phaseOffset)
        {
            bubbleBack = back;
            bubbleFront = front;
            speed = wobbleSpeed;
            strength = wobbleStrength;
            phase = phaseOffset;
            CacheBaseScales();
        }

        private void Awake() => CacheBaseScales();

        private void LateUpdate()
        {
            float time = Time.time * speed + phase;
            float waveA = Mathf.Sin(time) * strength;
            float waveB = Mathf.Sin(time * 1.73f + 1.2f) * strength * 0.42f;
            float breath = Mathf.Sin(time * 0.63f) * breathing;
            ApplyScale(bubbleBack, backBaseScale, waveA + waveB + breath, -waveA * 0.72f + breath);
            ApplyScale(bubbleFront, frontBaseScale, waveA * 0.82f - waveB + breath, -waveA * 0.58f + breath);
        }

        private void CacheBaseScales()
        {
            if (bubbleBack != null) backBaseScale = bubbleBack.localScale;
            if (bubbleFront != null) frontBaseScale = bubbleFront.localScale;
        }

        private static void ApplyScale(Transform target, Vector3 baseScale, float xOffset, float yOffset)
        {
            if (target == null) return;
            target.localScale = new Vector3(baseScale.x * (1f + xOffset), baseScale.y * (1f + yOffset), baseScale.z);
        }
    }
}

