using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubbleFruitLoop.UI
{
    public sealed class UICanvasLose : UICanvas
    {
        [SerializeField] private Button btnRetry;
        [SerializeField] private Button btnClose;
        private bool transitioning;

        public override void OnInit()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (btnRetry == null && buttons[index].name == "Btn_retry") btnRetry = buttons[index];
                if (btnClose == null && buttons[index].name == "BTNClose") btnClose = buttons[index];
            }
            if (btnRetry != null) btnRetry.onClick.AddListener(Retry);
            if (btnClose != null) btnClose.onClick.AddListener(OnClose);
        }

        public override void OnOpen(object data)
        {
            transitioning = false;
            base.OnOpen(data);
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
