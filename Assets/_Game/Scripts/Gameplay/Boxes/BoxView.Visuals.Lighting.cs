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
        private static void EnsureRimLighting(Transform visualRoot)
        {
            Sprite soft = GetSoftRoundedSprite();
            CreateLightingLayerIfMissing("Top Rim Highlight", visualRoot, soft,
                new Vector3(0f, 0.515f, -0.01f), new Vector2(0.86f, 0.052f),
                new Color(1f, 1f, 1f, 0.56f));
            CreateLightingLayerIfMissing("Left Rim Highlight", visualRoot, soft,
                new Vector3(-0.465f, 0.015f, -0.01f), new Vector2(0.032f, 0.96f),
                new Color(1f, 1f, 1f, 0.34f));
            CreateLightingLayerIfMissing("Right Rim Shade", visualRoot, soft,
                new Vector3(0.465f, -0.01f, -0.01f), new Vector2(0.04f, 0.98f),
                new Color(0f, 0f, 0f, 0.30f));
            CreateLightingLayerIfMissing("Bottom Rim Shade", visualRoot, soft,
                new Vector3(0f, -0.515f, -0.01f), new Vector2(0.86f, 0.07f),
                new Color(0f, 0f, 0f, 0.38f));
            CreateLightingLayerIfMissing("Bright Corner", visualRoot, soft,
                new Vector3(-0.43f, 0.48f, -0.02f), new Vector2(0.12f, 0.12f),
                new Color(1f, 1f, 1f, 0.42f));
            CreateLightingLayerIfMissing("Dark Corner", visualRoot, soft,
                new Vector3(0.43f, -0.48f, -0.02f), new Vector2(0.13f, 0.13f),
                new Color(0f, 0f, 0f, 0.25f));
        }

        private static void CreateLightingLayerIfMissing(string objectName, Transform parent,
            Sprite sprite, Vector3 position, Vector2 size, Color color)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.color = color;
                return;
            }
            CreateRuntimeSprite(objectName, parent, sprite, position, size, color, DividerOrder + 2);
        }

        private void EnsureClosedDepthLayers(Transform visualRoot)
        {
            if (visualRoot == null) return;
            closedDepthRoot = visualRoot.Find("Closed Carton Depth");
            if (closedDepthRoot == null)
            {
                closedDepthRoot = new GameObject("Closed Carton Depth").transform;
                closedDepthRoot.SetParent(visualRoot, false);
                closedDepthBack = CreateRuntimeSprite("Back Extrusion", closedDepthRoot,
                    GetSoftRoundedSprite(), new Vector3(0.055f, -0.075f, 0.06f),
                    new Vector2(1.03f, 1.14f), Color.white, DoorOrder - 3);
                closedBottomBevel = CreateRuntimeSprite("Bottom Bevel", closedDepthRoot,
                    GetSoftRoundedSprite(), new Vector3(0.01f, -0.555f, 0.03f),
                    new Vector2(0.94f, 0.14f), Color.white, DoorOrder + 2);
                closedRightBevel = CreateRuntimeSprite("Right Bevel", closedDepthRoot,
                    GetSoftRoundedSprite(), new Vector3(0.49f, -0.025f, 0.03f),
                    new Vector2(0.11f, 0.98f), Color.white, DoorOrder + 1);
            }
            else
            {
                closedDepthBack = FindSprite(closedDepthRoot, "Back Extrusion");
                closedBottomBevel = FindSprite(closedDepthRoot, "Bottom Bevel");
                closedRightBevel = FindSprite(closedDepthRoot, "Right Bevel");
            }
            ApplyClosedDepthColor(configuredColor);
            UpdateClosedDepth(0f);
        }

        private void ApplyClosedDepthColor(Color color)
        {
            Color bright = MakeBrightColor(color);
            SetSpriteColor(closedDepthBack, Color.Lerp(bright, Color.black, 0.34f));
            SetSpriteColor(closedBottomBevel, Color.Lerp(bright, Color.black, 0.48f));
            SetSpriteColor(closedRightBevel, Color.Lerp(bright, Color.black, 0.42f));
        }

        private static void SetSpriteColor(SpriteRenderer renderer, Color color)
        {
            if (renderer == null) return;
            color.a = renderer.color.a;
            renderer.color = color;
        }

        private void UpdateClosedDepth(float closed)
        {
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.92f, closed));
            SetSpriteAlpha(closedDepthBack, alpha);
            SetSpriteAlpha(closedBottomBevel, alpha * 0.95f);
            SetSpriteAlpha(closedRightBevel, alpha * 0.9f);

            // Open-tray highlights used to remain visible behind the shut doors,
            // producing little tabs and spikes outside the closed silhouette.
            // Fade every tray-only detail as the doors meet.
            Transform visualRoot = closedDepthRoot != null ? closedDepthRoot.parent : null;
            float openAlpha = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.30f, 0.88f, closed));
            SetNamedLayerAlpha(visualRoot, "Top Rim Highlight", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Left Rim Highlight", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Right Rim Shade", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Bottom Rim Shade", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Bright Corner", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Dark Corner", openAlpha);
        }

        private static void SetNamedLayerAlpha(Transform root, string childName, float alpha)
        {
            if (root == null) return;
            Transform child = root.Find(childName);
            if (child == null) return;
            SetSpriteAlpha(child.GetComponent<SpriteRenderer>(), alpha);
        }

        private static void SetSpriteAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }

        private void LayoutFruitSlots(Transform visualRoot)
        {
            if (fruitSlots == null) return;
            for (int index = 0; index < fruitSlots.Length && index < 4; index++)
            {
                Transform slot = fruitSlots[index];
                if (slot == null) continue;
                slot.SetParent(visualRoot, false);
                int row = index / 2;
                int column = index % 2;
                slot.localPosition = new Vector3(column == 0 ? -0.235f : 0.235f,
                    row == 0 ? 0.31f : -0.31f, -0.08f);
            }
        }

        private static SpriteRenderer FindSprite(Transform root, string objectName)
        {
            Transform child = root.Find(objectName);
            return child != null ? child.GetComponent<SpriteRenderer>() : null;
        }
        private void EnsureFrontLip()
        {
            if (frontLipArtwork != null || openArtwork == null) return;

            GameObject maskObject = new GameObject("Front Lip Mask");
            maskObject.transform.SetParent(transform, false);
            // Occlude only the lowest slice of the bottom-row fruit. The old
            // 0.40-high mask swallowed slot 3 almost completely.
            maskObject.transform.localPosition = new Vector3(0f, editorCapacity <= 4 ? -0.49f : -0.50f, -0.15f);
            maskObject.transform.localScale = new Vector3(1.08f, editorCapacity <= 4 ? 0.20f : 0.24f, 1f);
            frontLipMask = maskObject.AddComponent<SpriteMask>();
            frontLipMask.sprite = GetLipMaskSprite();
            frontLipMask.alphaCutoff = 0.01f;
            frontLipMask.isCustomRangeActive = true;
            frontLipMask.frontSortingLayerID = openArtwork.sortingLayerID;
            frontLipMask.backSortingLayerID = openArtwork.sortingLayerID;
            frontLipMask.frontSortingOrder = FrontWallOrder + 1;
            frontLipMask.backSortingOrder = FrontWallOrder - 1;

            GameObject lipObject = new GameObject("Front Lip Artwork");
            lipObject.transform.SetParent(transform, false);
            lipObject.transform.localPosition = openArtwork.transform.localPosition;
            lipObject.transform.localRotation = openArtwork.transform.localRotation;
            lipObject.transform.localScale = openArtwork.transform.localScale;
            frontLipArtwork = lipObject.AddComponent<SpriteRenderer>();
            frontLipArtwork.sprite = openArtwork.sprite;
            frontLipArtwork.sharedMaterial = openArtwork.sharedMaterial;
            frontLipArtwork.color = openArtwork.color;
            frontLipArtwork.sortingLayerID = openArtwork.sortingLayerID;
            frontLipArtwork.sortingOrder = FrontWallOrder;
            frontLipArtwork.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        private static Sprite GetLipMaskSprite()
        {
            if (lipMaskSprite != null) return lipMaskSprite;
            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "Runtime Box Front Lip Mask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[64];
            for (int index = 0; index < pixels.Length; index++) pixels[index] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            lipMaskSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            lipMaskSprite.name = "Runtime Box Front Lip Mask";
            lipMaskSprite.hideFlags = HideFlags.HideAndDontSave;
            return lipMaskSprite;
        }

        private static Sprite GetSoftRoundedSprite()
        {
            if (softRoundedSprite != null) return softRoundedSprite;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Soft Rounded Rectangle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            // Softer toy-like corners for the carton silhouette and its shadows.
            const float radius = 0.29f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                Vector2 delta = new Vector2(Mathf.Abs(point.x - 0.5f), Mathf.Abs(point.y - 0.5f));
                Vector2 corner = new Vector2(Mathf.Max(delta.x - (0.5f - radius), 0f),
                    Mathf.Max(delta.y - (0.5f - radius), 0f));
                float distance = corner.magnitude - radius;
                float alpha = 1f - Mathf.SmoothStep(-0.025f, 0.035f, distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            softRoundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            softRoundedSprite.name = "Runtime Soft Rounded Rectangle";
            softRoundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return softRoundedSprite;
        }

        private static Sprite GetParallelogramShadowSprite()
        {
            if (parallelogramShadowSprite != null) return parallelogramShadowSprite;
            Texture2D authoredTexture = Resources.Load<Texture2D>("Shadows/BoxGroundShadow");
            if (authoredTexture != null)
            {
                // Use the PNG's own transparent, feathered silhouette. Setting
                // PPU to its width gives stable 1:2 world-space sprite bounds.
                parallelogramShadowSprite = Sprite.Create(authoredTexture,
                    new Rect(0f, 0f, authoredTexture.width, authoredTexture.height),
                    new Vector2(0.5f, 0.5f), authoredTexture.width);
                parallelogramShadowSprite.name = "Box Ground Shadow (PNG)";
                parallelogramShadowSprite.hideFlags = HideFlags.HideAndDontSave;
                return parallelogramShadowSprite;
            }

            const int width = 64;
            const int height = 128;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime Rounded Parallelogram Shadow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) / width - 0.5f;
                float py = (y + 0.5f) / height - 0.5f;
                // A hanging cast-shadow silhouette: the right edge stays almost
                // vertical while the soft left edge and sloped crown droop down.
                float leftEdge = -0.32f + (py + 0.5f) * 0.075f;
                const float rightEdge = 0.31f;
                float across = Mathf.InverseLerp(leftEdge, rightEdge, px);
                float topEdge = Mathf.Lerp(0.27f, 0.43f, Mathf.SmoothStep(0f, 1f, across));
                const float bottomEdge = -0.49f;

                float distanceInside = Mathf.Min(
                    Mathf.Min(px - leftEdge, rightEdge - px),
                    Mathf.Min(topEdge - py, py - bottomEdge));
                // Feather the exposed left side more heavily like a real projected
                // shadow; the other edges remain softly rounded but readable.
                float leftBlend = Mathf.SmoothStep(-0.085f, 0.045f, px - leftEdge);
                float bodyBlend = Mathf.SmoothStep(-0.018f, 0.028f, distanceInside);
                float alpha = leftBlend * bodyBlend;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            parallelogramShadowSprite = Sprite.Create(texture,
                new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 64f);
            parallelogramShadowSprite.name = "Runtime Rounded Parallelogram Shadow";
            parallelogramShadowSprite.hideFlags = HideFlags.HideAndDontSave;
            return parallelogramShadowSprite;
        }
    }
}
