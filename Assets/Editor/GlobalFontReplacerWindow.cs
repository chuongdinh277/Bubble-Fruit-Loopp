using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GlobalFontReplacerWindow : EditorWindow
{
    private const string DefaultTmpFontPath = "Assets/_Game/Font/LilitaOne-Regular Outline 54 SDF.asset";

    [SerializeField] private TMP_FontAsset tmpFont;
    [SerializeField] private Font legacyFont;
    [SerializeField] private DefaultAsset searchFolder;
    [SerializeField] private bool replaceTmpDefault = true;

    [MenuItem("Colony Flow/Replace All Fonts", priority = 20)]
    [MenuItem("Colony Flow/UI/Replace All Fonts")]
    private static void Open() => GetWindow<GlobalFontReplacerWindow>("Replace Fonts").Show();

    private void OnEnable()
    {
        if (tmpFont == null)
            tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultTmpFontPath);
        if (searchFolder == null)
            searchFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/_Game");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Replace fonts in scenes and prefabs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "TMP Font thay TextMeshPro. Legacy Font là tùy chọn cho UI Text/TextMesh. " +
            "Tool chỉ sửa asset bên trong Search Folder.", MessageType.Info);

        tmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font", tmpFont, typeof(TMP_FontAsset), false);
        legacyFont = (Font)EditorGUILayout.ObjectField("Legacy Font (optional)", legacyFont, typeof(Font), false);
        searchFolder = (DefaultAsset)EditorGUILayout.ObjectField("Search Folder", searchFolder, typeof(DefaultAsset), false);
        replaceTmpDefault = EditorGUILayout.Toggle("Set TMP Default Font", replaceTmpDefault);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(tmpFont == null && legacyFont == null))
        {
            if (GUILayout.Button("Replace All Fonts", GUILayout.Height(36f))) ReplaceAll();
        }
    }

    private void ReplaceAll()
    {
        string root = searchFolder != null ? AssetDatabase.GetAssetPath(searchFolder) : "Assets/_Game";
        if (!AssetDatabase.IsValidFolder(root))
        {
            EditorUtility.DisplayDialog("Replace Fonts", "Search Folder không hợp lệ.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Replace All Fonts",
                $"Thay font trong toàn bộ prefab và scene bên trong:\n{root}\n\nNên commit/backup trước khi tiếp tục.",
                "Replace", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int tmpChanged = 0;
        int legacyChanged = 0;
        int assetChanged = 0;

        try
        {
            string[] folders = { root };
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", folders);
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", folders);
            int total = prefabGuids.Length + sceneGuids.Length;
            int progress = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Replacing fonts", path, total == 0 ? 1f : (float)progress++ / total);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (ReplaceInHierarchy(prefabRoot, ref tmpChanged, ref legacyChanged))
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        assetChanged++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Replacing fonts", path, total == 0 ? 1f : (float)progress++ / total);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                bool changed = false;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                    changed |= ReplaceInHierarchy(sceneRoot, ref tmpChanged, ref legacyChanged);

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    assetChanged++;
                }
                EditorSceneManager.CloseScene(scene, true);
            }

            SetTmpDefaultFont();
            AssetDatabase.SaveAssets();
            Debug.Log($"Font replacement complete: {tmpChanged} TMP, {legacyChanged} legacy, {assetChanged} assets saved.");
            EditorUtility.DisplayDialog("Replace Fonts",
                $"Hoàn tất!\nTMP: {tmpChanged}\nLegacy: {legacyChanged}\nPrefab/Scene đã lưu: {assetChanged}", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Replace Fonts", "Có lỗi xảy ra. Xem Console để biết chi tiết.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private void SetTmpDefaultFont()
    {
        if (!replaceTmpDefault || tmpFont == null || TMP_Settings.instance == null) return;
        SerializedObject settings = new SerializedObject(TMP_Settings.instance);
        SerializedProperty defaultFont = settings.FindProperty("m_defaultFontAsset");
        if (defaultFont == null || defaultFont.objectReferenceValue == tmpFont) return;
        defaultFont.objectReferenceValue = tmpFont;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(TMP_Settings.instance);
    }

    private bool ReplaceInHierarchy(GameObject root, ref int tmpChanged, ref int legacyChanged)
    {
        bool changed = false;
        if (tmpFont != null)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == tmpFont) continue;
                text.font = tmpFont;
                EditorUtility.SetDirty(text);
                tmpChanged++;
                changed = true;
            }
        }

        if (legacyFont == null) return changed;
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.font == legacyFont) continue;
            text.font = legacyFont;
            EditorUtility.SetDirty(text);
            legacyChanged++;
            changed = true;
        }
        foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true))
        {
            if (text.font == legacyFont) continue;
            text.font = legacyFont;
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = legacyFont.material;
            EditorUtility.SetDirty(text);
            legacyChanged++;
            changed = true;
        }
        return changed;
    }
}
