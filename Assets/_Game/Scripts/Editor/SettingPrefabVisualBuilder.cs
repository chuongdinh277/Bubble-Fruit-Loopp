#if UNITY_EDITOR
using BubbleFruitLoop.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleFruitLoop.Editor
{
    [InitializeOnLoad]
    public static class SettingPrefabVisualBuilder
    {
        private const string PrefabPath = "Assets/_Game/Resources/UI/UICanvasGameSetting.prefab";
        private const string TextureRoot = "Assets/_Game/Texture/Gameplay/";

        static SettingPrefabVisualBuilder() => EditorApplication.delayCall += BakeMissingVisuals;

        [MenuItem("BubbleFruit/Bake Editable Setting Toggles")]
        public static void BakeMissingVisuals()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath)
            {
                if (Bake(stage.prefabContentsRoot))
                {
                    EditorSceneManager.MarkSceneDirty(stage.scene);
                    AssetDatabase.SaveAssets();
                    SceneView.RepaintAll();
                    Debug.Log("Editable Setting toggle visuals baked into the open prefab.");
                }
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return;
            try
            {
                if (!Bake(root)) return;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Editable Setting toggle visuals baked into UICanvasGameSetting.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool Bake(GameObject root)
        {
            Sprite bar = AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "fill-removebg-preview.png");
            Sprite fruit = AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "icnf-removebg-preview.png");
            if (bar == null || fruit == null) return false;

            // Also repair an already-open Prefab Stage. It can retain the three
            // invalid SettingToggle components in memory even after the asset YAML
            // was fixed, which otherwise keeps Auto Save permanently blocked.
            bool changed = RemoveMissingScriptsRecursive(root.transform) > 0;
            string[] rows = { "IconSound", "IconMusic", "IconPhone" };
            for (int index = 0; index < rows.Length; index++)
            {
                Transform row = FindChild(root.transform, rows[index]);
                Button button = row != null ? row.GetComponentInChildren<Button>(true) : null;
                if (button == null || button.transform.Find("SwitchVisual") != null) continue;
                BakeToggle(button, bar, fruit);
                changed = true;
            }
            return changed;
        }

        private static int RemoveMissingScriptsRecursive(Transform root)
        {
            int removed = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root.gameObject);
            if (removed > 0)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root.gameObject);
            for (int index = 0; index < root.childCount; index++)
                removed += RemoveMissingScriptsRecursive(root.GetChild(index));
            return removed;
        }

        private static void BakeToggle(Button button, Sprite bar, Sprite fruit)
        {
            RectTransform buttonRect = (RectTransform)button.transform;
            buttonRect.sizeDelta = new Vector2(410f, 104f);
            Image hitArea = button.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);

            GameObject visualObject = new("SwitchVisual", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            RectTransform visual = (RectTransform)visualObject.transform;
            visual.SetParent(button.transform, false);
            visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
            visual.anchoredPosition = Vector2.zero;
            visual.sizeDelta = new Vector2(410f, 156f);
            Image barImage = visualObject.GetComponent<Image>();
            barImage.sprite = bar;
            barImage.type = Image.Type.Simple;
            barImage.preserveAspect = false;
            barImage.raycastTarget = false;

            GameObject knobObject = new("FruitKnob", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            RectTransform knob = (RectTransform)knobObject.transform;
            knob.SetParent(visual, false);
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.anchoredPosition = new Vector2(166f, 0f);
            knob.sizeDelta = new Vector2(112f, 112f);
            Image knobImage = knobObject.GetComponent<Image>();
            knobImage.sprite = fruit;
            knobImage.preserveAspect = true;
            knobImage.raycastTarget = false;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.name = "StateLabel (ON OFF)";
                label.transform.SetAsLastSibling();
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(34f, 0f);
                labelRect.offsetMax = new Vector2(-105f, 0f);
                labelRect.localScale = Vector3.one;
                label.text = "ON";
                label.fontSize = 34f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = Color.white;
                label.outlineWidth = 0.18f;
                label.outlineColor = new Color32(71, 58, 24, 210);
                label.raycastTarget = false;
            }

            EditorUtility.SetDirty(button.gameObject);
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindChild(root.GetChild(index), objectName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
