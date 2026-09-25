using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BubbleFruitLoop.EditorScripts
{
    public class AlignGameplayUI : EditorWindow
    {
        [MenuItem("BubbleFruit/Align Gameplay UI")]
        public static void AlignUI()
        {
            GameObject canvasObj = GameObject.Find("UICanvasGameplay");
            if (canvasObj == null)
            {
                Debug.LogError("Could not find UICanvasGameplay in the scene!");
                return;
            }

            // Get the Panel
            Transform panel = canvasObj.transform.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("Could not find Panel under UICanvasGameplay!");
                return;
            }

            // Make panel full screen
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                
                Image panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0, 0, 0, 0); 
                    panelImage.raycastTarget = false;
                }
            }

            // Align Btn_Setting (Top Left)
            Transform btnSetting = panel.Find("Btn_Setting");
            if (btnSetting != null)
            {
                RectTransform rect = btnSetting.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(30, -30); 
                rect.sizeDelta = new Vector2(100, 100);
            }

            // Align Btn_retry (Top Left, below Setting)
            Transform btnRetry = panel.Find("Btn_retry");
            if (btnRetry != null)
            {
                RectTransform rect = btnRetry.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(30, -150); 
                rect.sizeDelta = new Vector2(100, 100);
            }

            // Align Image (Level indicator) (Top Right)
            Transform imgLevel = panel.Find("Image");
            if (imgLevel != null)
            {
                RectTransform rect = imgLevel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-30, -30); 
                rect.sizeDelta = new Vector2(200, 70); 

                Transform textLevel = imgLevel.Find("Text (TMP)");
                if (textLevel != null)
                {
                    RectTransform textRect = textLevel.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                    
                    TMP_Text tmpText = textLevel.GetComponent<TMP_Text>();
                    if (tmpText != null)
                    {
                        tmpText.alignment = TextAlignmentOptions.Center;
                        tmpText.enableAutoSizing = true;
                        tmpText.fontSizeMin = 24;
                        tmpText.fontSizeMax = 50;
                    }
                }
            }

            // Create Coin Indicator if it doesn't exist
            Transform pnlCoin = panel.Find("Pnl_Coin");
            if (pnlCoin == null)
            {
                GameObject coinObj = new GameObject("Pnl_Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                coinObj.transform.SetParent(panel, false);
                pnlCoin = coinObj.transform;
                
                GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(pnlCoin, false);
                
                TMP_Text tmpText = textObj.GetComponent<TMP_Text>();
                tmpText.text = "64";
                tmpText.alignment = TextAlignmentOptions.Right;
                tmpText.enableAutoSizing = true;
                tmpText.fontSizeMin = 20;
                tmpText.fontSizeMax = 45;
                tmpText.color = Color.white;
                
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0, 0);
                textRect.anchorMax = new Vector2(1, 1);
                textRect.offsetMin = new Vector2(60, 10);
                textRect.offsetMax = new Vector2(-20, -10);
            }
            
            if (pnlCoin != null)
            {
                RectTransform rect = pnlCoin.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-30, -120); 
                rect.sizeDelta = new Vector2(180, 60);
                
                // Try to assign a sprite
                Image img = pnlCoin.GetComponent<Image>();
                img.color = new Color(1, 1, 1, 0.5f); // Placeholder color
            }

            // Create Progress Bar if it doesn't exist
            Transform pnlProgress = panel.Find("Pnl_Progress");
            if (pnlProgress == null)
            {
                GameObject progObj = new GameObject("Pnl_Progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                progObj.transform.SetParent(panel, false);
                pnlProgress = progObj.transform;
                
                GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillObj.transform.SetParent(pnlProgress, false);
                Image fillImg = fillObj.GetComponent<Image>();
                fillImg.color = Color.green;
                
                RectTransform fillRect = fillObj.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(1, 1);
                fillRect.offsetMin = new Vector2(10, 10);
                fillRect.offsetMax = new Vector2(-50, -10);
                
                GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(pnlProgress, false);
                TMP_Text tmpText = textObj.GetComponent<TextMeshProUGUI>();
                tmpText.text = "8/30";
                tmpText.alignment = TextAlignmentOptions.Center;
                tmpText.enableAutoSizing = true;
                tmpText.color = Color.black;
                
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }

            if (pnlProgress != null)
            {
                RectTransform rect = pnlProgress.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.35f); // Positioned above the boxes grid
                rect.anchorMax = new Vector2(0.5f, 0.35f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, 0); 
                rect.sizeDelta = new Vector2(400, 60);
                
                Image img = pnlProgress.GetComponent<Image>();
                img.color = new Color(0.9f, 0.9f, 0.9f, 1f); 
            }

            Debug.Log("Gameplay UI Aligned Successfully! Check the scene.");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
