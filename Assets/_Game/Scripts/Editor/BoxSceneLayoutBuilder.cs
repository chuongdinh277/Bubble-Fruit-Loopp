#if UNITY_EDITOR
using BubbleFruitLoop.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubbleFruitLoop.Editor
{
    public static class BoxSceneLayoutBuilder
    {
        private const string LayoutName = "BOX MODELS (EDIT LAYOUT)";
        private const string VersionKey = "BubbleFruitLoop.BoxSceneLayoutVersion";
        // 29 repairs SampleScene after the authored presentation/layout objects were deleted.
        private const int Version = 33;
        private const float ColumnSpacing = 1.65f;
        private const float BoxScale = 1.05f;
        private const float FirstRowWorldY = -2.45f;
        private const float RowWorldSpacing = 1.35f;

        [InitializeOnLoadMethod]
        private static void ScheduleBuild()
        {
            if (EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            EditorApplication.delayCall += ApplySpacingWhenReady;
        }

        private static void ApplySpacingWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += ApplySpacingWhenReady;
                return;
            }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                SceneManager.GetActiveScene().name != "SampleScene")
            {
                EditorApplication.delayCall += ApplySpacingWhenReady;
                return;
            }

            BoxAssignmentTable table = Object.FindFirstObjectByType<BoxAssignmentTable>();
            if (table == null || table.Entries.Count == 0)
            {
                BuildWhenReady();
                return;
            }

            foreach (BoxAssignmentTable.Entry entry in table.Entries)
            {
                if (entry?.box == null) continue;
                Vector3 position = entry.box.transform.position;
                position.x = (1 - entry.column) * ColumnSpacing;
                position.y = FirstRowWorldY - entry.queueOrder * RowWorldSpacing;
                entry.box.transform.position = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(entry.box.transform);
            }
            EditorUtility.SetDirty(table);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetInt(VersionKey, Version);
        }

        private static void BuildWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += BuildWhenReady;
                return;
            }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null || SceneManager.GetActiveScene().name != "SampleScene")
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                EditorApplication.delayCall += BuildWhenReady;
                return;
            }
            BuildLayout();
            EditorPrefs.SetInt(VersionKey, Version);
        }

        [MenuItem("BubbleFruit/Rebuild Editable Box Scene Layout %#l")]
        public static void BuildLayout()
        {
            BoxSystemPrefabBuilder.GeneratePrefabs();
            EditableFruitLoopController loop = Object.FindFirstObjectByType<EditableFruitLoopController>();
            Camera camera = Camera.main;
            if (loop == null || camera == null)
            {
                Debug.LogError("SampleScene needs an EditableFruitLoopController and Main Camera.");
                return;
            }

            Transform oldLayout = GameObject.Find(LayoutName)?.transform;
            if (oldLayout != null) Object.DestroyImmediate(oldLayout.gameObject);

            GameObject layoutRoot = new(LayoutName);
            Undo.RegisterCreatedObjectUndo(layoutRoot, "Create editable box layout");
            BoxAssignmentTable assignmentTable = layoutRoot.AddComponent<BoxAssignmentTable>();
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(
                "Assets/_Game/Resources/Levels/Level_01.asset");
            if (level == null || level.boxes == null || level.boxes.Count == 0)
            {
                Debug.LogError("Level_01 has no saved boxes to build.");
                Object.DestroyImmediate(layoutRoot);
                return;
            }
            BoxView[] boxes = new BoxView[level.boxes.Count];
            FruitType[] types = new FruitType[level.boxes.Count];
            int[] columns = new int[level.boxes.Count];
            int[] queueOrders = new int[level.boxes.Count];

            for (int index = 0; index < level.boxes.Count; index++)
            {
                LevelData.BoxSpawnData savedBox = level.boxes[index];
                int row = index / 3;
                int column = index % 3;
                int capacity = Mathf.Max(1, savedBox.capacity);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Resources/Box4.prefab");
                if (prefab == null) continue;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layoutRoot.transform);
                instance.name = $"C{column + 1} Q{row + 1} - {savedBox.fruitType} Box{capacity}";
                instance.transform.position = new Vector3((1 - column) * ColumnSpacing,
                    FirstRowWorldY - row * RowWorldSpacing, 0f);
                instance.transform.localScale = Vector3.one * BoxScale;
                BoxView view = instance.GetComponent<BoxView>();
                view.Configure(savedBox.fruitType, capacity, ColorFor(savedBox.fruitType));
                view.SetEditorClosed(row > 0);
                boxes[index] = view;
                types[index] = savedBox.fruitType;
                columns[index] = column;
                queueOrders[index] = row;
                EditorUtility.SetDirty(view);
            }

            EditableBoxBoardController board = loop.GetComponent<EditableBoxBoardController>();
            if (board == null) board = Undo.AddComponent<EditableBoxBoardController>(loop.gameObject);
            Transform[] pickupPoints =
            {
                GameObject.Find("P1")?.transform,
                GameObject.Find("P2")?.transform,
                GameObject.Find("P3")?.transform
            };
            NormalizePickupPositions(pickupPoints);
            assignmentTable.Configure(boxes, types, columns, queueOrders, pickupPoints);
            board.ConfigureSceneLayout(boxes, pickupPoints);
            board.ConfigureAssignmentTable(assignmentTable);
            EditorUtility.SetDirty(assignmentTable);
            EditorUtility.SetDirty(board);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = layoutRoot;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("Created 3x4 editable box layout directly in SampleScene.");
        }

        private static void NormalizePickupPositions(Transform[] points)
        {
            if (points == null || points.Length < 3 || points[0] == null || points[2] == null) return;
            if (points[0].position.x >= points[2].position.x) return;
            Vector3 right = points[2].position;
            Vector3 left = points[0].position;
            points[0].position = right;
            points[2].position = left;
            EditorUtility.SetDirty(points[0]);
            EditorUtility.SetDirty(points[2]);
        }

        private static Color ColorFor(FruitType type) => type switch
        {
            FruitType.Apple => new Color(0f, 0.46f, 1f),
            FruitType.Orange => new Color(1f, 0.52f, 0.08f),
            FruitType.Grape => new Color(0.58f, 0.24f, 0.88f),
            FruitType.Lemon => new Color(1f, 0.84f, 0.12f),
            FruitType.Strawberry => new Color(1f, 0.34f, 0.52f),
            _ => Color.white
        };
    }
}
#endif
