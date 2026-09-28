using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace BubbleFruitLoop.UI
{
    public sealed partial class UICanvasLose : UICanvas
    {
        [SerializeField]
        private Button btnRetry;
        [SerializeField]
        private Button btnClose;
        [SerializeField]
        private Button btnHold;
        [SerializeField]
        private GameObject failPanel;
        [SerializeField]
        private CanvasGroup failPanelCanvasGroup;
        [SerializeField]
        private Graphic dimBackground;
        [SerializeField]
        private RectTransform holdHint;
        private bool transitioning;
        private bool isHolding;
        private bool failPanelWasActive;
        private bool dimBackgroundWasActive;
        private Coroutine hintPulse;
        private Vector3 hintBaseScale;
        private readonly List<CanvasVisibilityState> hiddenCanvases = new();
        private readonly List<UICanvasVisibilityState> hiddenUICanvases = new();
        private struct CanvasVisibilityState
        {
            public Canvas canvas;
            public bool wasEnabled;
        }

        private struct UICanvasVisibilityState
        {
            public UICanvas canvas;
            public bool wasActive;
        }

        public override void OnInit()
        {
            ResolveLoseButtons();
            RegisterLoseActions();
        }

        public override void OnOpen(object data)
        {
            transitioning = false;
            isHolding = false;
            SetFailUiVisible(true);
            base.OnOpen(data);
            HideGameplayCanvases();
            HideOtherUICanvases();
            StartHintPulse();
        }

        public override void OnClose()
        {
            isHolding = false;
            SetFailUiVisible(true);
            RestoreGameplayCanvases();
            RestoreOtherUICanvases();
            StopHintPulse();
            base.OnClose();
        }

        private void OnDisable()
        {
            StopHintPulse();
            RestoreGameplayCanvases();
            RestoreOtherUICanvases();
        }

        private void AddHoldTrigger(EventTrigger trigger, EventTriggerType eventType, System.Action callback)
        {
            EventTrigger.Entry entry = new()
            {
                eventID = eventType
            };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
        }

        private void BeginHold()
        {
            isHolding = true;
            StopHintPulse();
            SetBoardPreview(true);
        }

        private void EndHold()
        {
            if (!isHolding)
                return;
            isHolding = false;
            SetBoardPreview(false);
            if (gameObject.activeInHierarchy)
                StartHintPulse();
        }

        private void StartHintPulse()
        {
            StopHintPulse();
            if (holdHint == null || !gameObject.activeInHierarchy)
                return;
            hintPulse = StartCoroutine(HintPulseRoutine());
        }

        private void StopHintPulse()
        {
            if (hintPulse != null)
            {
                StopCoroutine(hintPulse);
                hintPulse = null;
            }

            if (holdHint != null)
                holdHint.localScale = hintBaseScale;
        }

        private IEnumerator HintPulseRoutine()
        {
            const float period = 0.8f;
            const float scaleAmount = 0.10f;
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = (1f - Mathf.Cos(elapsed * (2f * Mathf.PI / period))) * 0.5f;
                holdHint.localScale = hintBaseScale * (1f + pulse * scaleAmount);
                yield return null;
            }
        }

        private void SetBoardPreview(bool visible)
        {
            SetFailUiVisible(!visible);
            if (!visible)
                return;
            HideGameplayCanvases();
            HideOtherUICanvases();
        }

        private void SetFailUiVisible(bool visible)
        {
            if (visible)
            {
                if (failPanel != null && failPanelWasActive)
                    failPanel.SetActive(true);
                if (dimBackground != null && dimBackgroundWasActive)
                    dimBackground.gameObject.SetActive(true);
            }
            else
            {
                failPanelWasActive = failPanel != null && failPanel.activeSelf;
                dimBackgroundWasActive = dimBackground != null && dimBackground.gameObject.activeSelf;
                if (failPanel != null)
                    failPanel.SetActive(false);
                if (dimBackground != null)
                    dimBackground.gameObject.SetActive(false);
            }

            if (failPanelCanvasGroup != null)
            {
                failPanelCanvasGroup.alpha = 1f;
                failPanelCanvasGroup.interactable = true;
                failPanelCanvasGroup.blocksRaycasts = true;
            }
        }

        private void HideOtherUICanvases()
        {
            if (hiddenUICanvases.Count > 0)
                return;
            UICanvas[] canvases = FindObjectsByType<UICanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                UICanvas canvas = canvases[index];
                if (canvas == null || canvas == this || canvas.transform.IsChildOf(transform))
                    continue;
                hiddenUICanvases.Add(new UICanvasVisibilityState { canvas = canvas, wasActive = canvas.gameObject.activeSelf });
                if (canvas.gameObject.activeSelf)
                    canvas.gameObject.SetActive(false);
            }
        }

        private void RestoreOtherUICanvases()
        {
            for (int index = 0; index < hiddenUICanvases.Count; index++)
            {
                UICanvasVisibilityState state = hiddenUICanvases[index];
                if (state.canvas != null && state.wasActive)
                    state.canvas.gameObject.SetActive(true);
            }

            hiddenUICanvases.Clear();
        }

        private void HideGameplayCanvases()
        {
            if (hiddenCanvases.Count > 0)
                return;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                if (canvas == null || canvas.transform == transform || canvas.transform.IsChildOf(transform))
                    continue;
                hiddenCanvases.Add(new CanvasVisibilityState { canvas = canvas, wasEnabled = canvas.enabled });
                if (canvas.enabled)
                    canvas.enabled = false;
            }
        }
    }
}
