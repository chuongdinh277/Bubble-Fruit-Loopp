using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace BubbleFruitLoop.UI
{
    public sealed partial class UICanvasLose
    {
        private void RestoreGameplayCanvases()
        {
            for (int index = 0; index < hiddenCanvases.Count; index++)
            {
                CanvasVisibilityState state = hiddenCanvases[index];
                if (state.canvas != null)
                    state.canvas.enabled = state.wasEnabled;
            }

            hiddenCanvases.Clear();
        }

        private void Retry()
        {
            if (transitioning)
                return;
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
                if (prefab == null)
                    return null;
                GameObject instance = Instantiate(prefab);
                view = instance.GetComponent<UICanvasLose>() ?? instance.AddComponent<UICanvasLose>();
            }

            view.Setup();
            view.OnOpen(null);
            return view;
        }

        private void ResolveLoseButtons()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (btnRetry == null && buttons[index].name == "Btn_retry")
                    btnRetry = buttons[index];
                if (btnClose == null && buttons[index].name == "BTNClose")
                    btnClose = buttons[index];
                if (btnHold == null && buttons[index].name == "Btn_hold")
                    btnHold = buttons[index];
            }

            if (failPanel == null)
            {
                Transform panel = transform.Find("Fail");
                if (panel != null)
                    failPanel = panel.gameObject;
            }

            if (failPanelCanvasGroup == null && failPanel != null)
                failPanelCanvasGroup = failPanel.GetComponent<CanvasGroup>() ?? failPanel.AddComponent<CanvasGroup>();
            if (dimBackground == null)
            {
                Transform background = transform.Find("Image");
                if (background != null)
                    dimBackground = background.GetComponent<Graphic>();
            }

            if (holdHint == null && btnHold != null)
            {
                TMP_Text hintText = btnHold.GetComponentInChildren<TMP_Text>(true);
                if (hintText != null)
                    holdHint = hintText.rectTransform;
            }

            failPanelWasActive = failPanel != null && failPanel.activeSelf;
            dimBackgroundWasActive = dimBackground != null && dimBackground.gameObject.activeSelf;
            if (holdHint != null)
                hintBaseScale = holdHint.localScale;
        }

        private void RegisterLoseActions()
        {
            if (btnRetry != null)
                btnRetry.onClick.AddListener(Retry);
            if (btnClose != null)
                btnClose.onClick.AddListener(OnClose);
            if (btnHold != null)
            {
                // The authored hold button has no Graphic, so Unity cannot
                // raycast its full RectTransform. Add an invisible hit target.
                Graphic hitTarget = btnHold.targetGraphic;
                if (hitTarget == null)
                {
                    Image hitImage = btnHold.GetComponent<Image>();
                    if (hitImage == null)
                        hitImage = btnHold.gameObject.AddComponent<Image>();
                    hitImage.color = new Color(1f, 1f, 1f, 0f);
                    hitImage.raycastTarget = true;
                    btnHold.targetGraphic = hitImage;
                }

                EventTrigger trigger = btnHold.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = btnHold.gameObject.AddComponent<EventTrigger>();
                trigger.triggers ??= new System.Collections.Generic.List<EventTrigger.Entry>();
                AddHoldTrigger(trigger, EventTriggerType.PointerDown, BeginHold);
                AddHoldTrigger(trigger, EventTriggerType.PointerUp, EndHold);
                AddHoldTrigger(trigger, EventTriggerType.PointerExit, EndHold);
            }
        }
    }
}
