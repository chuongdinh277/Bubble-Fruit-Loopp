using UnityEngine;
using UnityEngine.UI;

namespace BubbleFruitLoop.UI
{
    [DisallowMultipleComponent]
    public sealed class UICanvasLoading : UICanvas
    {
        [Header("Loading artwork")]
        [SerializeField] private Image artwork;
        [SerializeField] private int sortingOrder = 32760;

        public Image Artwork
        {
            get
            {
                if (artwork == null) artwork = GetComponentInChildren<Image>(true);
                return artwork;
            }
        }

        public override void OnInit()
        {
            base.OnInit();
            if (artwork == null) artwork = GetComponentInChildren<Image>(true);
        }

        public void PrepareForReveal()
        {
            gameObject.SetActive(true);
            RectTransform root = transform as RectTransform;
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
                root.localScale = Vector3.one;
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = sortingOrder;
            }

            if (Artwork != null)
            {
                Artwork.gameObject.SetActive(true);
                Artwork.enabled = true;
                Color color = Artwork.color;
                color.a = 1f;
                Artwork.color = color;
            }
        }

        public void FinishReveal()
        {
            if (Artwork != null) Artwork.enabled = true;
            OnClose();
        }
    }
}
