using UnityEngine;
using TMPro;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class LoopProgressDisplay
    {
        private void PrepareRoundedFill()
        {
            if (fillRenderer == null || fillRenderer.sprite == null)
                return;
            fillRenderer.drawMode = SpriteDrawMode.Simple;
            EnsureFillMask();
        }

        private void EnsureFillMask()
        {
            if (fillRenderer == null)
                return;
            Transform maskTransform = transform.Find("Fill Amount Mask");
            if (maskTransform == null)
            {
                maskTransform = new GameObject("Fill Amount Mask").transform;
                maskTransform.SetParent(transform, false);
            }

            fillMask = maskTransform.GetComponent<SpriteMask>();
            if (fillMask == null)
                fillMask = maskTransform.gameObject.AddComponent<SpriteMask>();
            if (maskSprite == null)
            {
                maskSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                maskSprite.name = "Runtime Fill Amount Mask";
            }

            fillMask.sprite = maskSprite;
            fillMask.isCustomRangeActive = true;
            fillMask.frontSortingOrder = -6;
            fillMask.backSortingOrder = -8;
            fillRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        private void UpdateDisplayedFill(int capacity, int count, float ratio)
        {
            if (!Application.isPlaying || !hasDisplayedFillRatio)
            {
                displayedFillRatio = ratio;
                fillRatioVelocity = 0f;
                hasDisplayedFillRatio = true;
            }
            else
            {
                displayedFillRatio = Mathf.SmoothDamp(displayedFillRatio, ratio, ref fillRatioVelocity, 0.22f, Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(displayedFillRatio - ratio) < 0.001f)
                {
                    displayedFillRatio = ratio;
                    fillRatioVelocity = 0f;
                }
            }

            if (countLabel != null)
                countLabel.text = $"{count}/{capacity}";
        }

        private void ReadProgressCount(out int capacity, out int count, out float ratio)
        {
            if (Application.isPlaying && loop != null)
            {
                capacity = Mathf.Max(1, loop.Capacity);
                count = Mathf.Clamp(loop.Count, 0, capacity);
                ratio = count / (float)capacity;
            }
            else
            {
                capacity = Mathf.Max(1, previewCapacity);
                ratio = Mathf.Clamp01(previewFillAmount);
                count = Mathf.RoundToInt(ratio * capacity);
            }
        }

        private void ApplyProgressFillScale(float visibleRatio)
        {
            Vector3 scale = fullFillScale;
            scale.x = fullFillScale.x * visibleRatio;
            fillRenderer.transform.localScale = scale;
            Vector3 position = fullFillPosition;
            // Sprite artwork has transparent padding and its visible bounds are
            // not guaranteed to be centred on the pivot. Anchor using the real
            // bounds minimum so the green bar stays inside the frame.
            position.x = fullFillLeftEdge - fillRenderer.sprite.bounds.min.x * scale.x;
            fillRenderer.transform.localPosition = position;
            fillRenderer.enabled = displayedFillRatio > 0.001f;
        }

        private void ApplyTransitionColor(Sprite target)
        {
            EnsureTransitionRenderer();
            transitionTarget = target;
            transitionElapsed = 0f;
            transitionRenderer.sprite = target;
            transitionRenderer.enabled = true;
            transitionRenderer.color = new Color(baseFillColor.r, baseFillColor.g, baseFillColor.b, 0f);
            fillRenderer.color = baseFillColor;
        }
    }
}
