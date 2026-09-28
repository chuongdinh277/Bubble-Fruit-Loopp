using UnityEngine;
using TMPro;
namespace BubbleFruitLoop.Gameplay
{
    [ExecuteAlways]
    public sealed class LoopProgressDisplay : MonoBehaviour
    {
        [SerializeField] private FruitLoopManager loop;
        [SerializeField] private SpriteRenderer frameRenderer;
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Sprite orangeFillSprite;
        [SerializeField] private Sprite redFillSprite;
        private Sprite greenFillSprite;
        private SpriteRenderer transitionRenderer;
        private Sprite transitionTarget;
        private Color baseFillColor = Color.white;
        private float transitionElapsed;
        private const float FillColorTransitionDuration = 0.28f;
        [Header("Scene Preview")]
        [SerializeField, Range(0f, 1f)] private float previewFillAmount;
        [SerializeField, Min(1)] private int previewCapacity = 30;
        private Vector3 fullFillScale;
        private Vector3 fullFillPosition;
        private float fullFillWidth;
        private float fullFillLeftEdge;
        private float displayedFillRatio;
        private float fillRatioVelocity;
        private bool hasDisplayedFillRatio;
        private SpriteMask fillMask;
        private static Sprite maskSprite;

        public void Configure(SpriteRenderer frame, SpriteRenderer fill, TMP_Text label,
            FruitLoopManager controller = null, Sprite orangeFill = null, Sprite redFill = null)
        {
            frameRenderer = frame;
            fillRenderer = fill;
            countLabel = label;
            loop = controller;
            if (orangeFill != null) orangeFillSprite = orangeFill;
            if (redFill != null) redFillSprite = redFill;
            PrepareCountLabel();
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
            // This is an explicit helper only. Preserve the authored Y position
            // so artists can place the bar directly in the Scene view.
            transform.position = new Vector3(
                safeArea.center.x, transform.position.y, transform.position.z);
        }

        private void Awake()
        {
            if (loop == null) loop = FindFirstObjectByType<FruitLoopManager>();
            PrepareCountLabel();
            PrepareRoundedFill();
            CacheFullFillGeometry();
            Refresh();
        }

        private void Update()
        {
            if (loop == null) loop = FindFirstObjectByType<FruitLoopManager>();
            // In edit mode the Fill transform is authored by hand. Read it every
            // frame, but never write it back from the preview logic.
            if (!Application.isPlaying) CacheFullFillGeometry();
            Refresh();
            AdvanceFillColorTransition(Time.deltaTime);
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
        }

        private void CacheFullFillGeometry()
        {
            if (fillRenderer == null || fillRenderer.sprite == null) return;
            if (greenFillSprite == null)
            {
                greenFillSprite = fillRenderer.sprite;
                baseFillColor = fillRenderer.color;
            }
            fullFillScale = fillRenderer.transform.localScale;
            fullFillPosition = fillRenderer.transform.localPosition;
            fullFillLeftEdge = fullFillPosition.x
                + fillRenderer.sprite.bounds.min.x * fullFillScale.x;
            fullFillWidth = Mathf.Abs(
                fillRenderer.sprite.bounds.size.x * fullFillScale.x);
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
            if (!Application.isPlaying || !hasDisplayedFillRatio)
            {
                displayedFillRatio = ratio;
                fillRatioVelocity = 0f;
                hasDisplayedFillRatio = true;
            }
            else
            {
                displayedFillRatio = Mathf.SmoothDamp(displayedFillRatio, ratio,
                    ref fillRatioVelocity, 0.22f, Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(displayedFillRatio - ratio) < 0.001f)
                {
                    displayedFillRatio = ratio;
                    fillRatioVelocity = 0f;
                }
            }
            if (countLabel != null) countLabel.text = $"{count}/{capacity}";
            if (fillRenderer == null || fullFillWidth <= 0f) return;
            UpdateFillColor(count);
            float visibleRatio = Mathf.Max(0.001f, displayedFillRatio);
            if (fillMask != null)
            {
                float maskWidth = fullFillWidth * visibleRatio;
                fillMask.transform.localPosition = new Vector3(
                    fullFillLeftEdge + maskWidth * 0.5f,
                    fullFillPosition.y, -0.02f);
                fillMask.transform.localScale = new Vector3(maskWidth,
                    fullFillWidth * 0.28f, 1f);
                fillRenderer.enabled = displayedFillRatio > 0.001f;
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
            fillRenderer.enabled = displayedFillRatio > 0.001f;
        }

        private void UpdateFillColor(int count)
        {
            if (greenFillSprite == null) greenFillSprite = fillRenderer.sprite;
            if (count == 0)
            {
                fillRenderer.sprite = greenFillSprite;
                fillRenderer.color = baseFillColor;
                transitionTarget = null;
                if (transitionRenderer != null) transitionRenderer.enabled = false;
                return;
            }

            Sprite target = count <= 15 ? greenFillSprite
                : count <= 25 ? orangeFillSprite
                : redFillSprite;
            if (target == null || transitionTarget == target) return;
            if (fillRenderer.sprite == target)
            {
                if (transitionRenderer != null && transitionRenderer.enabled)
                {
                    transitionRenderer.enabled = false;
                    fillRenderer.color = baseFillColor;
                    transitionTarget = null;
                }
                return;
            }

            EnsureTransitionRenderer();
            transitionTarget = target;
            transitionElapsed = 0f;
            transitionRenderer.sprite = target;
            transitionRenderer.enabled = true;
            transitionRenderer.color = new Color(baseFillColor.r, baseFillColor.g,
                baseFillColor.b, 0f);
            fillRenderer.color = baseFillColor;
        }

        private void EnsureTransitionRenderer()
        {
            if (transitionRenderer != null) return;
            GameObject overlay = new("Fill Color Transition");
            overlay.transform.SetParent(fillRenderer.transform.parent, false);
            overlay.transform.SetSiblingIndex(fillRenderer.transform.GetSiblingIndex() + 1);
            transitionRenderer = overlay.AddComponent<SpriteRenderer>();
            transitionRenderer.spriteSortPoint = fillRenderer.spriteSortPoint;
            transitionRenderer.sortingLayerID = fillRenderer.sortingLayerID;
            transitionRenderer.sortingOrder = fillRenderer.sortingOrder;
            transitionRenderer.maskInteraction = fillRenderer.maskInteraction;
            transitionRenderer.sharedMaterial = fillRenderer.sharedMaterial;
            transitionRenderer.flipX = fillRenderer.flipX;
            transitionRenderer.flipY = fillRenderer.flipY;
            transitionRenderer.enabled = false;
        }

        private void AdvanceFillColorTransition(float deltaTime)
        {
            if (transitionRenderer == null || !transitionRenderer.enabled) return;
            transitionElapsed += deltaTime;
            float progress = Mathf.Clamp01(transitionElapsed / FillColorTransitionDuration);
            progress = Mathf.SmoothStep(0f, 1f, progress);
            fillRenderer.color = new Color(baseFillColor.r, baseFillColor.g,
                baseFillColor.b, 1f - progress);
            transitionRenderer.color = new Color(baseFillColor.r, baseFillColor.g,
                baseFillColor.b, progress);
            SyncTransitionTransform();
            if (progress < 1f) return;

            fillRenderer.sprite = transitionTarget;
            fillRenderer.color = baseFillColor;
            transitionRenderer.enabled = false;
            transitionTarget = null;
        }

        private void SyncTransitionTransform()
        {
            if (transitionRenderer == null) return;
            Transform source = fillRenderer.transform;
            Transform overlay = transitionRenderer.transform;
            overlay.localPosition = source.localPosition;
            overlay.localRotation = source.localRotation;
            overlay.localScale = source.localScale;
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

