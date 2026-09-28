using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Managers;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxManager
    {
        internal static TrailRenderer CreateFruitFlightTrail(Fruit fruit)
        {
            if (fruit == null)
                return null;
            if (fruitFlightTrailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    return null;
                fruitFlightTrailMaterial = new Material(shader)
                {
                    name = "Runtime Fruit Flight Trail",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            SpriteRenderer sprite = fruit.GetComponentInChildren<SpriteRenderer>(true);
            // Imported fruit sprites are normally rendered with a white tint, so
            // SpriteRenderer.color cannot identify the fruit hue. Drive the trail
            // from the gameplay type—the same mapping used by its target box.
            Color color = ColorFor(fruit.Type);
            GameObject trailObject = new($"{fruit.name} Flight Trail");
            trailObject.transform.position = fruit.CachedTransform.position;
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            ConfigureFlightTrail(sprite, color, trail);
            return trail;
        }

        internal static void ReleaseFruitFlightTrail(TrailRenderer trail)
        {
            if (trail == null)
                return;
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.05f);
        }

        private static void ConfigureFlightTrail(SpriteRenderer sprite, Color color, TrailRenderer trail)
        {
            trail.sharedMaterial = fruitFlightTrailMaterial;
            trail.time = 0.24f;
            trail.minVertexDistance = 0.025f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 0.13f), new Keyframe(1f, 0.035f));
            trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.sortingOrder = sprite != null ? sprite.sortingOrder - 1 : 19;
            trail.emitting = true;
        }
    }
}
