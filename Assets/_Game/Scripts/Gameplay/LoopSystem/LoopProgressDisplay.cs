using UnityEngine;
using TMPro;
namespace BubbleFruitLoop.Gameplay
{
    [ExecuteAlways]
    public sealed class LoopProgressDisplay : MonoBehaviour
    {
        private const float CorrectWorldY = 0.49f;

        [SerializeField] private EditableFruitLoopController loop;
        [SerializeField] private SpriteRenderer frameRenderer;
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private TMP_Text countLabel;
        [Header("Scene Preview")]
        [SerializeField, Range(0f, 1f)] private float previewFillAmount;
        [SerializeField, Min(1)] private int previewCapacity = 30;
        private Vector3 fullFillScale;
        private Vector3 fullFillPosition;
        private float fullFillWidth;
        private float fullFillLeftEdge;
        private SpriteMask fillMask;
        private static Sprite maskSprite;

        public void Configure(SpriteRenderer frame, SpriteRenderer fill, TMP_Text label,
            EditableFruitLoopController controller = null)
        {
            frameRenderer = frame;
            fillRenderer = fill;
            countLabel = label;
            loop = controller;
            PrepareCountLabel();
            AlignInsideLoopBoundary();
            CacheFullFillGeometry();
            Refresh();
        }

        public void AlignInsideLoopBoundary()
        {
            LoopPathAuthoring authoring = loop != null ? loop.GetComponent<LoopPathAuthoring>()
                : FindFirstObjectByType<LoopPathAuthoring>();
            EdgeCollider2D inner = authoring != null
                ? authoring.transform.Find("Loop Track Colliders/Inner Boundary")?.GetComponent<EdgeCollider2D>()
                : null;
            if (inner == null || frameRenderer == null || fillRenderer == null) return;
            Bounds safeArea = inner.bounds;
            transform.position = new Vector3(safeArea.center.x, CorrectWorldY, transform.position.z);
            // Width is authored from BoardVisual by the installer/factory. Only
            // use the inner track to centre the bar; resizing it here made the
            // previously correct frame unexpectedly tiny.
        }

        private void Awake()
        {
            if (loop == null) loop = FindFirstObjectByType<EditableFruitLoopController>();
            PrepareCountLabel();
            PrepareRoundedFill();
            AlignInsideLoopBoundary();
            CacheFullFillGeometry();
            Refresh();
        }

        private void Update()
        {
            if (loop == null) loop = FindFirstObjectByType<EditableFruitLoopController>();
            Refresh();
        }

        private void OnValidate()
        {
            previewFillAmount = Mathf.Clamp01(previewFillAmount);
            previewCapacity = Mathf.Max(1, previewCapacity);
            PrepareCountLabel();
            if (fillRenderer != null && frameRenderer != null)
            {
                CacheFullFillGeometry();
                Refresh();
            }
        }

        private void PrepareCountLabel()
        {
            if (countLabel is not TextMeshProUGUI uiLabel) return;

            Canvas canvas = uiLabel.GetComponent<Canvas>();
            if (canvas == null) canvas = uiLabel.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = -6;

            RectTransform rect = uiLabel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 110f);
            rect.localPosition = new Vector3(0f, 0f, -0.05f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * 0.005f;
            uiLabel.alignment = TextAlignmentOptions.Center;
            uiLabel.textWrappingMode = TextWrappingModes.NoWrap;
            uiLabel.overflowMode = TextOverflowModes.Overflow;
        }

        private void CacheFullFillGeometry()
        {
            if (fillRenderer == null || fillRenderer.sprite == null) return;
            // Never treat the current fill transform as its full size: at 0/30
            // that transform is deliberately almost zero and may be serialized
            // by an editor repair. Reconstruct 100% width from the stable frame.
            if (frameRenderer != null && frameRenderer.sprite != null)
            {
                float frameWidth = frameRenderer.sprite.bounds.size.x
                    * Mathf.Abs(frameRenderer.transform.localScale.x);
                // Fill the beige opening closely while retaining only a hairline
                // gap from the white border at both rounded ends.
                float horizontalInset = frameWidth * 0.06f;
                float fillWidth = frameWidth - horizontalInset * 2f;
                float fillScale = fillWidth / fillRenderer.sprite.bounds.size.x;
                fullFillScale = new Vector3(fillScale, fillScale * 0.92f, fillScale);
                float frameLeft = frameRenderer.transform.localPosition.x
                    + frameRenderer.sprite.bounds.min.x * frameRenderer.transform.localScale.x;
                fullFillLeftEdge = frameLeft + horizontalInset;
                fullFillPosition = new Vector3(
                    fullFillLeftEdge - fillRenderer.sprite.bounds.min.x * fillScale,
                    -0.005f, fillRenderer.transform.localPosition.z);
            }
            else
            {
                fullFillScale = fillRenderer.transform.localScale;
                fullFillPosition = fillRenderer.transform.localPosition;
                fullFillLeftEdge = fullFillPosition.x
                    + fillRenderer.sprite.bounds.min.x * fullFillScale.x;
            }
            fullFillWidth = fillRenderer.sprite.bounds.size.x * fullFillScale.x;
            EnsureFillMask();
        }

        private void Refresh()
        {
            int capacity;
            int count;
            float ratio;
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
            if (countLabel != null) countLabel.text = $"{count}/{capacity}";
            if (fillRenderer == null || fullFillWidth <= 0f) return;
            float visibleRatio = Mathf.Max(0.001f, ratio);
            if (fillMask != null)
            {
                fillRenderer.transform.localScale = fullFillScale;
                fillRenderer.transform.localPosition = fullFillPosition;
                float maskWidth = fullFillWidth * visibleRatio;
                fillMask.transform.localPosition = new Vector3(
                    fullFillLeftEdge + maskWidth * 0.5f,
                    fullFillPosition.y, -0.02f);
                fillMask.transform.localScale = new Vector3(maskWidth,
                    fullFillWidth * 0.28f, 1f);
                fillRenderer.enabled = count > 0;
                return;
            }

            Vector3 scale = fullFillScale;
            scale.x = fullFillScale.x * visibleRatio;
            fillRenderer.transform.localScale = scale;
            Vector3 position = fullFillPosition;
            // Sprite artwork has transparent padding and its visible bounds are
            // not guaranteed to be centred on the pivot. Anchor using the real
            // bounds minimum so the green bar stays inside the frame.
            position.x = fullFillLeftEdge
                - fillRenderer.sprite.bounds.min.x * scale.x;
            fillRenderer.transform.localPosition = position;
            fillRenderer.enabled = count > 0;
        }

        private void PrepareRoundedFill()
        {
            if (fillRenderer == null || fillRenderer.sprite == null) return;
            fillRenderer.drawMode = SpriteDrawMode.Simple;
            EnsureFillMask();
        }

        private void EnsureFillMask()
        {
            if (fillRenderer == null) return;
            Transform maskTransform = transform.Find("Fill Amount Mask");
            if (maskTransform == null)
            {
                maskTransform = new GameObject("Fill Amount Mask").transform;
                maskTransform.SetParent(transform, false);
            }
            fillMask = maskTransform.GetComponent<SpriteMask>();
            if (fillMask == null) fillMask = maskTransform.gameObject.AddComponent<SpriteMask>();
            if (maskSprite == null)
            {
                maskSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f), 1f);
                maskSprite.name = "Runtime Fill Amount Mask";
            }
            fillMask.sprite = maskSprite;
            fillMask.isCustomRangeActive = true;
            fillMask.frontSortingOrder = -6;
            fillMask.backSortingOrder = -8;
            fillRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
    }
}

