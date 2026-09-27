using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace BubbleFruitLoop.UI
{
    public sealed class UICanvasLose : UICanvas
    {
        [SerializeField] private Button btnRetry;
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnHold;
        [SerializeField] private GameObject failPanel;
        [SerializeField] private CanvasGroup failPanelCanvasGroup;
        [SerializeField] private Graphic dimBackground;
        [SerializeField] private RectTransform holdHint;
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
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (btnRetry == null && buttons[index].name == "Btn_retry") btnRetry = buttons[index];
                if (btnClose == null && buttons[index].name == "BTNClose") btnClose = buttons[index];
                if (btnHold == null && buttons[index].name == "Btn_hold") btnHold = buttons[index];
            }
            if (failPanel == null)
            {
                Transform panel = transform.Find("Fail");
                if (panel != null) failPanel = panel.gameObject;
            }
            if (failPanelCanvasGroup == null && failPanel != null)
                failPanelCanvasGroup = failPanel.GetComponent<CanvasGroup>() ?? failPanel.AddComponent<CanvasGroup>();
            if (dimBackground == null)
            {
                Transform background = transform.Find("Image");
                if (background != null) dimBackground = background.GetComponent<Graphic>();
            }
            if (holdHint == null && btnHold != null)
            {
                TMP_Text hintText = btnHold.GetComponentInChildren<TMP_Text>(true);
                if (hintText != null) holdHint = hintText.rectTransform;
            }
            failPanelWasActive = failPanel != null && failPanel.activeSelf;
            dimBackgroundWasActive = dimBackground != null && dimBackground.gameObject.activeSelf;
            if (holdHint != null) hintBaseScale = holdHint.localScale;
            if (btnRetry != null) btnRetry.onClick.AddListener(Retry);
            if (btnClose != null) btnClose.onClick.AddListener(OnClose);
            if (btnHold != null)
            {
                // The authored hold button has no Graphic, so Unity cannot
                // raycast its full RectTransform. Add an invisible hit target.
                Graphic hitTarget = btnHold.targetGraphic;
                if (hitTarget == null)
                {
                    Image hitImage = btnHold.GetComponent<Image>();
                    if (hitImage == null) hitImage = btnHold.gameObject.AddComponent<Image>();
                    hitImage.color = new Color(1f, 1f, 1f, 0f);
                    hitImage.raycastTarget = true;
                    btnHold.targetGraphic = hitImage;
                }

                EventTrigger trigger = btnHold.GetComponent<EventTrigger>();
                if (trigger == null) trigger = btnHold.gameObject.AddComponent<EventTrigger>();
                trigger.triggers ??= new System.Collections.Generic.List<EventTrigger.Entry>();
                AddHoldTrigger(trigger, EventTriggerType.PointerDown, BeginHold);
                AddHoldTrigger(trigger, EventTriggerType.PointerUp, EndHold);
                AddHoldTrigger(trigger, EventTriggerType.PointerExit, EndHold);
            }
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
            EventTrigger.Entry entry = new() { eventID = eventType };
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
            if (!isHolding) return;
            isHolding = false;
            SetBoardPreview(false);
            if (gameObject.activeInHierarchy) StartHintPulse();
        }

        private void StartHintPulse()
        {
            StopHintPulse();
            if (holdHint == null || !gameObject.activeInHierarchy) return;
            hintPulse = StartCoroutine(HintPulseRoutine());
        }

        private void StopHintPulse()
        {
            if (hintPulse != null)
            {
                StopCoroutine(hintPulse);
                hintPulse = null;
            }
            if (holdHint != null) holdHint.localScale = hintBaseScale;
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
            if (!visible) return;
            HideGameplayCanvases();
            HideOtherUICanvases();
        }

        private void SetFailUiVisible(bool visible)
        {
            if (visible)
            {
                if (failPanel != null && failPanelWasActive) failPanel.SetActive(true);
                if (dimBackground != null && dimBackgroundWasActive) dimBackground.gameObject.SetActive(true);
            }
            else
            {
                failPanelWasActive = failPanel != null && failPanel.activeSelf;
                dimBackgroundWasActive = dimBackground != null && dimBackground.gameObject.activeSelf;
                if (failPanel != null) failPanel.SetActive(false);
                if (dimBackground != null) dimBackground.gameObject.SetActive(false);
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
            if (hiddenUICanvases.Count > 0) return;
            UICanvas[] canvases = FindObjectsByType<UICanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                UICanvas canvas = canvases[index];
                if (canvas == null || canvas == this || canvas.transform.IsChildOf(transform)) continue;
                hiddenUICanvases.Add(new UICanvasVisibilityState
                {
                    canvas = canvas,
                    wasActive = canvas.gameObject.activeSelf
                });
                if (canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
            }
        }

        private void RestoreOtherUICanvases()
        {
            for (int index = 0; index < hiddenUICanvases.Count; index++)
            {
                UICanvasVisibilityState state = hiddenUICanvases[index];
                if (state.canvas != null && state.wasActive) state.canvas.gameObject.SetActive(true);
            }
            hiddenUICanvases.Clear();
        }

        private void HideGameplayCanvases()
        {
            if (hiddenCanvases.Count > 0) return;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                if (canvas == null || canvas.transform == transform || canvas.transform.IsChildOf(transform)) continue;
                hiddenCanvases.Add(new CanvasVisibilityState { canvas = canvas, wasEnabled = canvas.enabled });
                if (canvas.enabled) canvas.enabled = false;
            }
        }

        private void RestoreGameplayCanvases()
        {
            for (int index = 0; index < hiddenCanvases.Count; index++)
            {
                CanvasVisibilityState state = hiddenCanvases[index];
                if (state.canvas != null) state.canvas.enabled = state.wasEnabled;
            }
            hiddenCanvases.Clear();
        }

        private void Retry()
        {
            if (transitioning) return;
            transitioning = true;
            LoadingScreenController.Play();
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        public static UICanvasLose Show()
        {
            UICanvasLose view = FindFirstObjectByType<UICanvasLose>(FindObjectsInactive.Include);
            if (view == null)
            {
                GameObject prefab = Resources.Load<GameObject>("UI/UICanvasFailed");
                if (prefab == null) return null;
                GameObject instance = Instantiate(prefab);
                view = instance.GetComponent<UICanvasLose>() ?? instance.AddComponent<UICanvasLose>();
            }
            view.Setup();
            view.OnOpen(null);
            return view;
        }
    }
}
