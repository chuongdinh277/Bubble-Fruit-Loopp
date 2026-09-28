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
        private static Sprite GetParallelogramShadowSprite()
        {
            if (parallelogramShadowSprite != null)
                return parallelogramShadowSprite;
            Texture2D authoredTexture = Resources.Load<Texture2D>("Shadows/BoxGroundShadow");
            if (authoredTexture != null)
            {
                // Use the PNG's own transparent, feathered silhouette. Setting
                // PPU to its width gives stable 1:2 world-space sprite bounds.
                parallelogramShadowSprite = Sprite.Create(authoredTexture, new Rect(0f, 0f, authoredTexture.width, authoredTexture.height), new Vector2(0.5f, 0.5f), authoredTexture.width);
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
                    float distanceInside = Mathf.Min(Mathf.Min(px - leftEdge, rightEdge - px), Mathf.Min(topEdge - py, py - bottomEdge));
                    // Feather the exposed left side more heavily like a real projected
                    // shadow; the other edges remain softly rounded but readable.
                    float leftBlend = Mathf.SmoothStep(-0.085f, 0.045f, px - leftEdge);
                    float bodyBlend = Mathf.SmoothStep(-0.018f, 0.028f, distanceInside);
                    float alpha = leftBlend * bodyBlend;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            parallelogramShadowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 64f);
            parallelogramShadowSprite.name = "Runtime Rounded Parallelogram Shadow";
            parallelogramShadowSprite.hideFlags = HideFlags.HideAndDontSave;
            return parallelogramShadowSprite;
        }

        private void CreateFrontLipMask()
        {
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
    }
}
