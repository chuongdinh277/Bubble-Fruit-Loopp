#if UNITY_EDITOR
using System.Collections.Generic;
using BubbleFruitLoop.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubbleFruitLoop.Editor
{
    [InitializeOnLoad]
    public static class LoopPathSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string InstallKey = "BubbleFruitLoop.EditablePathVersion";
        private const int Version = 1;

        static LoopPathSceneInstaller()
        {
            if (EditorPrefs.GetInt(InstallKey, 0) < Version)
                EditorApplication.delayCall += InstallInOpenScene;
        }

        [MenuItem("Tools/Bubble Fruit Loop/Create Editable Fruit Loop")]
        public static void InstallInOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || Object.FindFirstObjectByType<LoopPathAuthoring>() != null) return;

            bool wasDirty = scene.isDirty;
            GameObject root = new("Fruit Loop Path (EDIT POINTS)");
            LoopPathAuthoring authoring = root.AddComponent<LoopPathAuthoring>();
            EditableFruitLoopController controller = root.AddComponent<EditableFruitLoopController>();

            Vector2[] positions =
            {
                new(0f, -0.5f), new(1.25f, -0.5f), new(2.5f, -0.5f), new(3.45f, -0.75f),
                new(4.15f, -1.35f), new(4.15f, -2.15f), new(4.15f, -2.95f), new(3.45f, -3.55f),
                new(2.5f, -3.8f), new(1.25f, -3.8f), new(0f, -3.8f), new(-1.25f, -3.8f),
                new(-2.5f, -3.8f), new(-3.45f, -3.55f), new(-4.15f, -2.95f), new(-4.15f, -2.15f),
                new(-4.15f, -1.35f), new(-3.45f, -0.75f), new(-2.5f, -0.5f), new(-1.25f, -0.5f)
            };

            List<Transform> points = new(positions.Length);
            for (int index = 0; index < positions.Length; index++)
            {
                string pointName = index == 0 ? "P00_INTAKE" : $"P{index:00}";
                Transform point = new GameObject(pointName).transform;
                point.SetParent(root.transform, false);
                point.localPosition = positions[index];
                points.Add(point);
            }

            authoring.SetPoints(points);
            controller.Configure(authoring);
            EditorUtility.SetDirty(authoring);
            EditorUtility.SetDirty(controller);
            EditorPrefs.SetInt(InstallKey, Version);
            Selection.activeGameObject = root;

            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            else EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
