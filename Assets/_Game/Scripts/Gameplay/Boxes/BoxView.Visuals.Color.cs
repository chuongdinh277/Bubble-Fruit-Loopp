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
        private void ApplyColor(Color color)
        {
            color = MakeBrightColor(color);
            ApplyClosedDepthColor(color);
            if (openArtwork != null)
            {
                Color openColor = color;
                openColor.a = openArtwork.color.a;
                openArtwork.color = openColor;
                if (closedArtwork != null)
                {
                    Color closedColor = color;
                    closedColor.a = closedArtwork.color.a;
                    closedArtwork.color = closedColor;
                }
                if (frontLipArtwork != null) frontLipArtwork.color = openColor;
                if (depthArtwork != null)
                {
                    Color depth = Color.Lerp(color, Color.black, 0.30f);
                    depth.a = 1f;
                    depthArtwork.color = depth;
                }
                if (contactShadowArtwork != null)
                    contactShadowArtwork.color = new Color(0f, 0f, 0f, 0.12f);
                SetLidColor(leftLid, color);
                SetLidColor(rightLid, color);
                return;
            }
            Color rim = Color.Lerp(color, Color.black, 0.24f);
            Color shadow = Color.Lerp(color, Color.black, 0.52f);
            Color tray = Color.Lerp(color, Color.black, 0.14f);
            Color slot = Color.Lerp(color, Color.white, 0.12f);
            Color highlight = Color.Lerp(color, Color.white, 0.34f);
            Color sideShade = Color.Lerp(color, Color.black, 0.18f);
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
            for (int index = 0; index < sprites.Length; index++)
            {
                string objectName = sprites[index].gameObject.name;
                if (objectName == "Box Shadow") sprites[index].color = shadow;
                else if (objectName == "Inner Tray") sprites[index].color = tray;
                else if (objectName == "Slot Plate") sprites[index].color = slot;
                else if (objectName.Contains("Top Wall") || objectName.Contains("Left Wall")) sprites[index].color = highlight;
                else if (objectName.Contains("Bottom Wall") || objectName.Contains("Right Wall")) sprites[index].color = sideShade;
                else if (objectName.Contains("Lid")) sprites[index].color = color;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] is SpriteRenderer) continue;
                string objectName = renderers[index].gameObject.name;
                SetRendererColor(renderers[index], objectName == "Box Shadow" ? shadow
                    : objectName.Contains("Inner Tray") ? tray
                    : objectName.Contains("Slot Plate") ? slot
                    : objectName.Contains("Top Wall") || objectName.Contains("Left Wall") ? highlight
                    : objectName.Contains("Bottom Wall") || objectName.Contains("Right Wall") ? sideShade
                    : objectName.Contains("Divider") ? rim
                    : objectName.Contains("Lid") ? highlight
                    : color);
            }
        }

        private static void SetLidColor(Transform lid, Color color)
        {
            if (lid == null) return;
            Renderer[] renderers = lid.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                string objectName = renderers[index].gameObject.name;
                Color partColor = objectName.Contains("Highlight")
                    ? Color.Lerp(color, Color.white, 0.36f)
                    : objectName.Contains("Inner") || objectName.Contains("Edge")
                        ? Color.Lerp(color, Color.black, 0.22f)
                        : color;
                SetRendererColor(renderers[index], partColor);
            }
        }

        private static Color MakeBrightColor(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            // Keep distinct fruit colours but give every box the bright toy-like
            // finish used by the reference UI.
            saturation = Mathf.Clamp(saturation * 1.08f, 0.68f, 0.94f);
            value = Mathf.Clamp(Mathf.Max(value, 0.96f), 0f, 1f);
            Color bright = Color.HSVToRGB(hue, saturation, value);
            bright.a = color.a;
            return bright;
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            if (renderer is SpriteRenderer sprite) sprite.color = color;
            else
            {
                colorPropertyBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(colorPropertyBlock);
                colorPropertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(colorPropertyBlock);
                colorPropertyBlock.Clear();
            }
        }

    }
}
