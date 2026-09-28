#if UNITY_EDITOR
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    [InitializeOnLoad]
    public static class ResponsiveGameplayLayoutInstaller
    {
        private static double nextCheck;
        static ResponsiveGameplayLayoutInstaller() => EditorApplication.update += CheckOpenScene;

        private static void CheckOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1d;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            InstallInOpenScene();
        }

        [MenuItem("BubbleFruit/Adapt Gameplay To Phone And Tablet")]
        public static void InstallInOpenScene()
        {
            if (Object.FindFirstObjectByType<EditableFruitLoopController>() == null) return;
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic) return;
            if (camera.GetComponent<ResponsiveGameplayCamera>() == null)
            {
                Undo.AddComponent<ResponsiveGameplayCamera>(camera.gameObject);
                EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            }
            camera.GetComponent<ResponsiveGameplayCamera>().ApplyLayout();
        }
    }
}
#endif
