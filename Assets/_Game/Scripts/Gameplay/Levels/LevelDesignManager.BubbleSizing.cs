using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using BubbleFruitLoop.Gameplay;

namespace BubbleFruitLoop.Editor
{
    public partial class LevelDesignManager
    {
        public float CalculateBubbleScale(int fruitCount)
        {
            float low = Mathf.Min(minBubbleScale, maxBubbleScale);
            float high = Mathf.Max(minBubbleScale, maxBubbleScale);
            int minCount = Mathf.Max(1, minFruitForSizing);
            int maxCount = Mathf.Max(minCount + 1, maxFruitForSizing);
            float t = Mathf.InverseLerp(minCount, maxCount, Mathf.Max(0, fruitCount));
            return Mathf.Lerp(low, high, t);
        }

        public void ApplyBubbleScale(BubbleConfig cfg)
        {
            if (cfg?.instance == null) return;
            // Never scale the BubbleActor root: FruitRoot is its sibling and must
            // keep the authored fruit size. Only the bubble shell visual changes.
            Vector3 prefabRootScale = bubblePrefab != null
                ? bubblePrefab.transform.localScale
                : Vector3.one * 0.55f;
            cfg.instance.transform.localScale = prefabRootScale;

            Transform visual = cfg.instance.transform.Find("Visual");
            float rootScale = Mathf.Max(0.0001f, Mathf.Abs(prefabRootScale.x));
            float visualMultiplier = Mathf.Max(0.1f, cfg.bubbleScale) / rootScale;
            if (visual != null)
            {
                Transform prefabVisual = bubblePrefab != null ? bubblePrefab.transform.Find("Visual") : null;
                Vector3 baseVisualScale = prefabVisual != null ? prefabVisual.localScale : Vector3.one;
                visual.localScale = baseVisualScale * visualMultiplier;
            }

            // Draw the guide slightly inside the glossy shell. It must not sit on
            // the outer edge, otherwise fruit sprites appear to pierce the bubble.
            Transform boundary = cfg.instance.transform.Find("InnerBoundary");
            Transform prefabBoundary = bubblePrefab != null ? bubblePrefab.transform.Find("InnerBoundary") : null;
            if (boundary != null)
                boundary.localScale = (prefabBoundary != null ? prefabBoundary.localScale : Vector3.one)
                    * (visualMultiplier * InnerBoundaryInset);

            if (cfg.instance.ObstacleCollider is CircleCollider2D circle)
            {
                CircleCollider2D prefabCircle = bubblePrefab != null
                    ? bubblePrefab.ObstacleCollider as CircleCollider2D
                    : null;
                if (prefabCircle != null) circle.radius = prefabCircle.radius * visualMultiplier;
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(cfg.instance);
#endif
        }

    }
}
