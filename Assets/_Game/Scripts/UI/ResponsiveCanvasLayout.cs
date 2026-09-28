using BubbleFruitLoop.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleFruitLoop.UI
{
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class ResponsiveCanvasLayout : MonoBehaviour
    {
        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform gameplayPanel;

        private void OnEnable()
        {
            canvas = GetComponent<Canvas>();
            scaler = GetComponent<CanvasScaler>();
            if (GetComponent<UICanvasGameplay>() != null)
                gameplayPanel = transform.Find("Panel") as RectTransform;
            ApplyLayout();
        }

        private void LateUpdate() => ApplyLayout();

        public void ApplyLayout()
        {
            if (canvas == null || scaler == null || canvas.renderMode == RenderMode.WorldSpace
                || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = ResponsiveGameplayCamera.GetSafeScreenRect();
            float scale = ResponsiveGameplayCamera.CalculateDesignScale(safe);
            // One pixel scale shared with the world camera, including safe-area insets.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale;
            canvas.scaleFactor = scale;
            if (gameplayPanel == null) return;
            gameplayPanel.anchorMin = gameplayPanel.anchorMax = new Vector2(0.5f, 0.5f);
            gameplayPanel.pivot = new Vector2(0.5f, 0.5f);
            // HUD anchors follow the safe screen edges even when the world
            // keeps its original gameplay proportions in the screen centre.
            gameplayPanel.sizeDelta = safe.size / scale;
            gameplayPanel.anchoredPosition = (safe.center
                - new Vector2(Screen.width, Screen.height) * 0.5f) / scale;
        }
    }
}
