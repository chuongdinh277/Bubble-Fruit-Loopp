using System;
using System.Collections;
using System.Collections.Generic;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Pooling;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif
namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxView
    {
        private GameObject CreateFruitLabel()
        {
            // Derive the carton label from this box's own first packed visual.
            // No sprite, tint, or material is shared through static/global state.
            SpriteRenderer labelSource = null;
            FitFruitLabelArtwork(ref labelSource);
            if (labelSource == null || labelSource.sprite == null)
                return null;
            Sprite labelSprite = labelSource.sprite;
            Color labelColor = labelSource.color;
            GameObject root = new GameObject($"{name} Fruit Label");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.24f, -0.22f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, -13f);
            root.transform.localScale = Vector3.zero;
            int sortingLayer = openArtwork != null ? openArtwork.sortingLayerID : 0;
            EnsureFruitLabelMaterial();
            GameObject shadowObject = new GameObject("Fruit Label Shadow");
            ConfigureFruitLabelShadow(labelSprite, root, sortingLayer, shadowObject);
            GameObject outlineObject = new GameObject("White Fruit Outline");
            outlineObject.transform.SetParent(root.transform, false);
            ConfigureFruitLabelOutline(labelSprite, labelColor, root, sortingLayer, shadowObject, outlineObject);
            return root;
        }

        private void PlayLabelFirework(Vector3 worldPosition)
        {
            if (labelFireworkPrefab == null)
                return;
            GameObject effect = Instantiate(labelFireworkPrefab, worldPosition, Quaternion.identity);
            effect.name = "Cute Label Firework";
            effect.transform.localScale = Vector3.one * 0.20f;
            ParticleSystemRenderer[] renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
            int sortingLayer = openArtwork != null ? openArtwork.sortingLayerID : 0;
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].sortingLayerID = sortingLayer;
                renderers[index].sortingOrder = 135 + index;
            }

            Destroy(effect, 2.4f);
        }

        private Vector3 GetOffscreenExitTarget(Vector3 start, float direction)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return start + Vector3.right * direction * 5f;
            float depth = Mathf.Abs(camera.transform.position.z - transform.position.z);
            Vector3 edge = camera.ViewportToWorldPoint(new Vector3(direction > 0f ? 1.12f : -0.12f, 0.5f, depth));
            return new Vector3(edge.x, start.y + 0.20f, start.z);
        }

        private void PlayImpactBurst(Vector3 worldPosition, Color color, int count, float radius, float duration)
        {
            GameObject root = new GameObject($"{name} Color Burst");
            root.transform.position = worldPosition;
            Sequence burst = DOTween.Sequence().SetLink(root);
            Color bright = Color.Lerp(MakeBrightColor(color), Color.white, 0.18f);
            for (int index = 0; index < count; index++)
            {
                float angle = (index + 0.35f) / count * Mathf.PI * 2f;
                Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                GameObject dot = new GameObject("Burst Dot");
                dot.transform.SetParent(root.transform, false);
                dot.transform.localScale = Vector3.one * Mathf.Lerp(0.055f, 0.09f, (index % 3) / 2f);
                SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSoftRoundedSprite();
                renderer.color = bright;
                renderer.sortingLayerID = openArtwork != null ? openArtwork.sortingLayerID : 0;
                renderer.sortingOrder = 180;
                burst.Join(dot.transform.DOLocalMove(direction * radius * Mathf.Lerp(0.72f, 1.12f, (index % 4) / 3f), duration).SetEase(Ease.OutQuad));
                burst.Join(dot.transform.DOScale(0f, duration).SetEase(Ease.InQuad));
                burst.Join(renderer.DOFade(0f, duration).SetEase(Ease.InQuad));
            }

            burst.OnComplete(() => Destroy(root));
        }

        private GameObject CreateExitTrails(float direction)
        {
            GameObject root = new GameObject($"{name} Wavy Exit Trails");
            ConfigureExitTrailRoot(root);
            CreateTrailLayers(direction, root);
            return root;
        }

        private static void UpdateExitTrailWaves(GameObject root, float progress, float direction)
        {
            if (root == null)
                return;
            for (int index = 0; index < root.transform.childCount; index++)
            {
                Transform line = root.transform.GetChild(index);
                float phase = progress * Mathf.PI * (5.2f + index * 0.7f) + index * 2.1f;
                line.localPosition = new Vector3(-direction * 0.48f + Mathf.Cos(phase * 0.55f) * 0.035f, (index - 1) * 0.22f + Mathf.Sin(phase) * (0.075f + index * 0.012f), 0f);
            }
        }

        private void ReleaseExitTrails(GameObject root)
        {
            if (root == null)
                return;
            root.transform.SetParent(null, true);
            TrailRenderer[] trails = root.GetComponentsInChildren<TrailRenderer>();
            float lifetime = 0f;
            for (int index = 0; index < trails.Length; index++)
            {
                trails[index].emitting = false;
                lifetime = Mathf.Max(lifetime, trails[index].time);
            }

            Destroy(root, lifetime + 0.08f);
        }

        private void ConfigureFruitLabelOutline(Sprite labelSprite, Color labelColor, GameObject root, int sortingLayer, GameObject shadowObject, GameObject outlineObject)
        {
            outlineObject.transform.localScale = Vector3.one * 1.18f;
            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sprite = labelSprite;
            outline.color = Color.white;
            outline.sharedMaterial = spriteSilhouetteMaterial;
            outline.sortingLayerID = sortingLayer;
            outline.sortingOrder = DoorOrder + 9;
            GameObject iconObject = new GameObject("Original Fruit Artwork");
            iconObject.transform.SetParent(root.transform, false);
            iconObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            SpriteRenderer icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = labelSprite;
            icon.color = labelColor;
            icon.sortingLayerID = sortingLayer;
            icon.sortingOrder = DoorOrder + 10;
            Vector2 spriteSize = labelSprite.bounds.size;
            float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
            if (largestSide > 0.0001f)
            {
                // Make the fruit sticker a strong, readable mark on the closed
                // carton instead of a small icon lost between the two doors.
                float fittedScale = 1.52f / largestSide;
                iconObject.transform.localScale = Vector3.one * fittedScale;
                outlineObject.transform.localScale = Vector3.one * (fittedScale * 1.18f);
                shadowObject.transform.localScale = Vector3.one * (fittedScale * 1.18f);
            }
        }

        private void EnsureFruitLabelMaterial()
        {
            if (spriteSilhouetteMaterial == null)
            {
                Shader silhouetteShader = Shader.Find("BubbleFruit/SpriteSilhouette");
                if (silhouetteShader != null)
                {
                    spriteSilhouetteMaterial = new Material(silhouetteShader)
                    {
                        name = "Runtime Fruit Label Outline",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
        }

        private void FitFruitLabelArtwork(ref SpriteRenderer labelSource)
        {
            for (int index = 0; index < dockedVisuals.Count; index++)
            {
                if (dockedVisuals[index] == null || dockedVisuals[index].slotIndex != 0)
                    continue;
                labelSource = dockedVisuals[index].fruit != null ? dockedVisuals[index].fruit.VisualSpriteRenderer : null;
                break;
            }

            if (labelSource == null && dockedVisuals.Count > 0 && dockedVisuals[0]?.fruit != null)
                labelSource = dockedVisuals[0].fruit.VisualSpriteRenderer;
        }

        private void ConfigureFruitLabelShadow(Sprite labelSprite, GameObject root, int sortingLayer, GameObject shadowObject)
        {
            shadowObject.transform.SetParent(root.transform, false);
            shadowObject.transform.localPosition = new Vector3(0.035f, -0.045f, 0.02f);
            SpriteRenderer shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = labelSprite;
            shadow.color = new Color(0.12f, 0.05f, 0.04f, 0.28f);
            shadow.sharedMaterial = spriteSilhouetteMaterial;
            shadow.sortingLayerID = sortingLayer;
            shadow.sortingOrder = DoorOrder + 8;
        }

        private void CreateTrailLayers(float direction, GameObject root)
        {
            for (int index = 0; index < 3; index++)
            {
                GameObject line = new GameObject($"Color Trail {index + 1}");
                line.transform.SetParent(root.transform, false);
                line.transform.localPosition = new Vector3(-direction * 0.48f, (index - 1) * 0.22f, 0f);
                TrailRenderer trail = line.AddComponent<TrailRenderer>();
                trail.sharedMaterial = runtimeTrailMaterial;
                trail.time = 0.42f;
                trail.minVertexDistance = 0.015f;
                trail.widthMultiplier = 1f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.18f, 0.055f), new Keyframe(1f, 0.025f));
                Color lineColor = index == 1 ? Color.Lerp(configuredColor, Color.white, 0.35f) : Color.Lerp(configuredColor, Color.black, index == 0 ? 0.08f : 0.18f);
                trail.startColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.95f);
                trail.endColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0f);
                trail.sortingOrder = 110 + index;
                trail.emitting = true;
            }
        }

        private void ConfigureExitTrailRoot(GameObject root)
        {
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            if (runtimeTrailMaterial == null)
            {
                runtimeTrailMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Runtime Box Exit Trail",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }
    }
}
