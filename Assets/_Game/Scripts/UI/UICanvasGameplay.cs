using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Managers;

namespace BubbleFruitLoop.UI
{
    public class UICanvasGameplay : UICanvas
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text txtLevel;
        [SerializeField] private TMP_Text txtCoin;
        [SerializeField] private Image imgProgressFill;
        [SerializeField] private TMP_Text txtProgress;
        [SerializeField] private Button btnSetting;
        [SerializeField] private Button btnRetry;

        public override void OnInit()
        {
            base.OnInit();
            
            // Auto bind if not set in Inspector
            if (btnSetting == null) btnSetting = transform.Find("Panel/Btn_Setting")?.GetComponent<Button>();
            if (btnRetry == null) btnRetry = transform.Find("Panel/Btn_retry")?.GetComponent<Button>();
            
            if (txtLevel == null) txtLevel = transform.Find("Panel/Image/Text (TMP)")?.GetComponent<TMP_Text>();
            if (txtCoin == null) txtCoin = transform.Find("Panel/Pnl_Coin/Text (TMP)")?.GetComponent<TMP_Text>();
            
            if (btnSetting != null) btnSetting.onClick.AddListener(OnClickSetting);
            if (btnRetry != null) btnRetry.onClick.AddListener(OnClickRetry);
        }

        public override void OnOpen(object data)
        {
            base.OnOpen(data);
            
            // Setup default data using DataManager
            if (DataManager.Instance != null)
            {
                UpdateLevel(DataManager.Instance.GetLevel());
                UpdateCoin(DataManager.Instance.GetCoin());
            }
            
            UpdateProgress(0, 30);
        }

        public void UpdateLevel(int level)
        {
            if (txtLevel != null) txtLevel.text = $"Level {level}";
        }

        public void UpdateCoin(int coin)
        {
            if (txtCoin != null) txtCoin.text = coin.ToString();
        }

        public void UpdateProgress(int current, int max)
        {
            if (txtProgress != null) txtProgress.text = $"{current}/{max}";
            if (imgProgressFill != null) imgProgressFill.fillAmount = (float)current / max;
        }

        private void OnClickSetting()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.OpenUI<UICanvasGameSetting>();
        }

        private void OnClickRetry()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
    }
}
