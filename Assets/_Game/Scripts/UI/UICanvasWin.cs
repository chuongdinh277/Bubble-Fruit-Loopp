using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BubbleFruitLoop.Managers;

namespace BubbleFruitLoop.UI
{
    public sealed class UICanvasWin : UICanvas
    {
        [SerializeField] private Button btnNext;
        [SerializeField] private Button btnClose;
        private bool transitioning;

        public override void OnInit()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (btnNext == null && buttons[index].name == "Btn_Next") btnNext = buttons[index];
                if (btnClose == null && buttons[index].name == "BTNClose") btnClose = buttons[index];
            }
            if (btnNext != null) btnNext.onClick.AddListener(NextLevel);
            if (btnClose != null) btnClose.onClick.AddListener(OnClose);
        }

        public override void OnOpen(object data)
        {
            transitioning = false;
            base.OnOpen(data);
        }

        private void NextLevel()
        {
            if (transitioning) return;
            transitioning = true;
            int current = DataManager.Instance != null ? DataManager.Instance.GetLevel() : 1;
            if (DataManager.Instance != null) DataManager.Instance.SetLevel(current == 1 ? 2 : 1);
            LoadingScreenController.Play();
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        public static UICanvasWin Show()
        {
            UICanvasWin view = FindFirstObjectByType<UICanvasWin>(FindObjectsInactive.Include);
            if (view == null)
            {
                GameObject prefab = Resources.Load<GameObject>("UI/UICanvasWin");
                if (prefab == null) return null;
                GameObject instance = Instantiate(prefab);
                view = instance.GetComponent<UICanvasWin>() ?? instance.AddComponent<UICanvasWin>();
            }
            view.Setup();
            view.OnOpen(null);
            return view;
        }
    }
}
