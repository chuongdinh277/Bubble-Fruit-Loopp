using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;
#if UNITY_EDITOR
#if UNITY_EDITOR
using UnityEditor;
#endif
#endif

namespace BubbleFruitLoop.Editor
{
    public partial class LevelDesignManager
    {
        public void CheckBubbles()
        {
#if UNITY_EDITOR
            // Rebuild the editor lists from the actual scene first. This removes
            // stale/null duplicated entries left by previous generation passes.
            SyncScene();
#endif
            fruitCountSummary.Clear();
            Dictionary<FruitType, int> counts = CountBubbleFruits();
            foreach (FruitType type in System.Enum.GetValues(typeof(FruitType)))
            {
                if (!counts.TryGetValue(type, out int count) || count <= 0) continue;
                fruitCountSummary.Add(new FruitCountInfo
                {
                    fruitType = type,
                    count = count,
                    canBuildBoxes = HasBoxCombination(count)
                });
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (fruitCountSummary.Count == 0)
                Debug.LogWarning("Không tìm thấy fruit nào trong danh sách bubble.", this);
#endif
        }

        [HorizontalGroup("BoxGeneration")]
        [Button("2. Random Box Từ Kết Quả Check", ButtonSizes.Large)]
        [GUIColor(0.35f, 1f, 0.45f)]
        public void GenerateRandomBoxesFromBubbles()
        {
#if UNITY_EDITOR
            if (!EnsureSeparateContainers()) return;
            CheckBubbles();
            EnsureFruitLoopPath();

            List<string> invalid = new List<string>();
            for (int index = 0; index < fruitCountSummary.Count; index++)
                if (!fruitCountSummary[index].canBuildBoxes)
                    invalid.Add($"{fruitCountSummary[index].fruitType}: {fruitCountSummary[index].count}");

            if (invalid.Count > 0)
            {
                EditorUtility.DisplayDialog("Không thể tạo box",
                    "Các số lượng sau không thể chia chính xác thành Box4:\n\n" +
                    string.Join("\n", invalid) +
                    "\n\nHãy sửa số fruit trong bubble trước.", "OK");
                return;
            }

            // Only replace boxes. Bubble instances, board and funnel stay untouched.
            for (int index = boxContainer.childCount - 1; index >= 0; index--)
                if (boxContainer.GetChild(index).GetComponent<BoxView>() != null)
                    Undo.DestroyObjectImmediate(boxContainer.GetChild(index).gameObject);
            boxes.Clear();

            System.Random random = new System.Random(System.Environment.TickCount);
            List<(FruitType type, int capacity)> generated = new List<(FruitType, int)>();
            for (int index = 0; index < fruitCountSummary.Count; index++)
            {
                FruitCountInfo info = fruitCountSummary[index];
                List<int> capacities = CreateBox4Capacities(info.count);
                for (int capacityIndex = 0; capacityIndex < capacities.Count; capacityIndex++)
                    generated.Add((info.fruitType, capacities[capacityIndex]));
            }

            // Randomize appearance order across all fruit types.
            for (int index = generated.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                (generated[index], generated[other]) = (generated[other], generated[index]);
            }

            int[] rowsPerColumn = new int[3];
            for (int index = 0; index < generated.Count; index++)
            {
                int column = index % 3;
                int row = rowsPerColumn[column]++;
                BoxConfig cfg = AddBoxWithCapacity(generated[index].capacity);
                if (cfg == null) continue;
                cfg.fruitType = generated[index].type;
                cfg.capacity = generated[index].capacity;
                cfg.column = column;
                cfg.queueOrder = row;
                cfg.instance.transform.position = new Vector3(
                    (1 - column) * boxColumnSpacing,
                    boxFirstRowY - row * boxRowSpacing,
                    0f);
                cfg.instance.transform.localScale = box4Prefab.transform.localScale * boxDisplayScale;
                UpdateBoxVisuals(cfg);
                cfg.instance.SetEditorClosed(row > 0);
                boxes.Add(cfg);
            }

            // Make the serialized list exactly match the BoxView instances that
            // now exist under the one shared container.
            SyncScene();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            EditorUtility.SetDirty(this);
#endif
        }

        [Button("Create / Repair Fruit Loop Path", ButtonSizes.Large)]
        [GUIColor(0.2f, 1f, 0.7f)]
        public void EnsureFruitLoopPath()
        {
#if UNITY_EDITOR
            LoopPathAuthoring authoring = FindFirstObjectByType<LoopPathAuthoring>();
            FruitLoopManager controller;

            if (authoring == null)
            {
                GameObject pathObject = new GameObject("Fruit Loop Path (EDIT POINTS)");
                Undo.RegisterCreatedObjectUndo(pathObject, "Create Fruit Loop Path");
                pathObject.transform.SetParent(transform, false);
                authoring = Undo.AddComponent<LoopPathAuthoring>(pathObject);
                controller = Undo.AddComponent<FruitLoopManager>(pathObject);

                // Matches the oval lane inside the fixed lower funnel. These remain
                // ordinary child transforms so the designer can fine-tune them.
                Vector2[] positions =
                {
                    new(-0.04f, 1.10f), new(0.73f, 1.06f), new(1.57f, 1.14f), new(2.36f, 0.98f),
                    new(2.66f, 0.34f), new(2.26f, -0.17f), new(1.56f, -0.31f), new(0.71f, -0.32f),
                    new(-0.21f, -0.35f), new(-1.06f, -0.47f), new(-1.94f, -0.41f), new(-2.64f, -0.07f),
                    new(-2.76f, 0.55f), new(-2.45f, 1.13f), new(-1.81f, 1.33f), new(-0.89f, 1.29f)
                };

                List<Transform> points = new List<Transform>(positions.Length);
                for (int index = 0; index < positions.Length; index++)
                {
                    GameObject pointObject = new GameObject(index == 0 ? "P00_INTAKE" : $"P{index:00}");
                    Undo.RegisterCreatedObjectUndo(pointObject, "Create Loop Point");
                    pointObject.transform.SetParent(pathObject.transform, false);
                    pointObject.transform.localPosition = positions[index];
                    points.Add(pointObject.transform);
                }
                authoring.SetPoints(points);
            }
            else
            {
                controller = authoring.GetComponent<FruitLoopManager>();
                if (controller == null)
                    controller = Undo.AddComponent<FruitLoopManager>(authoring.gameObject);
            }

            Transform loopStart = transform.Find("LoopStart");
            if (loopStart == null)
            {
                GameObject startObject = new GameObject("LoopStart");
                Undo.RegisterCreatedObjectUndo(startObject, "Create Loop Start");
                loopStart = startObject.transform;
                loopStart.SetParent(transform, false);
                loopStart.localPosition = new Vector3(0f, 2.21f, 0f);
            }

            int totalFruit = 0;
            Dictionary<FruitType, int> counts = CountBubbleFruits();
            foreach (KeyValuePair<FruitType, int> pair in counts) totalFruit += pair.Value;
            controller.Configure(authoring, loopStart, Mathf.Max(30, totalFruit));
            EditorUtility.SetDirty(authoring);
            EditorUtility.SetDirty(controller);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        [HorizontalGroup("TrackBoundary")]
        [Button("Boundary: Auto Theo Points", ButtonSizes.Medium)]
        [GUIColor(0.35f, 0.85f, 1f)]
        private void EnableAutomaticTrackBoundary()
        {
#if UNITY_EDITOR
            LoopPathAuthoring authoring = FindFirstObjectByType<LoopPathAuthoring>();
            if (authoring == null)
            {
                EnsureFruitLoopPath();
                authoring = FindFirstObjectByType<LoopPathAuthoring>();
            }
            if (authoring == null) return;
            Undo.RecordObject(authoring, "Enable Automatic Track Boundary");
            authoring.SetBoundaryEditingMode(true);
            EditorUtility.SetDirty(authoring);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        [HorizontalGroup("TrackBoundary")]
        [Button("Boundary: Chỉnh Tay", ButtonSizes.Medium)]
        [GUIColor(1f, 0.72f, 0.3f)]
        private void EnableManualTrackBoundary()
        {
#if UNITY_EDITOR
            LoopPathAuthoring authoring = FindFirstObjectByType<LoopPathAuthoring>();
            if (authoring == null)
            {
                EnsureFruitLoopPath();
                authoring = FindFirstObjectByType<LoopPathAuthoring>();
            }
            if (authoring == null) return;
            Undo.RecordObject(authoring, "Enable Manual Track Boundary Editing");
            // First simplify/bake the collider to the authored control points, then
            // stop auto-fit so every manual edit remains untouched.
            authoring.RebuildTrackBoundaries();
            authoring.SetBoundaryEditingMode(false);
            EditorUtility.SetDirty(authoring);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Selection.activeGameObject = authoring.transform.Find("Loop Track Colliders/Outer Boundary (Closed)")?.gameObject;
#endif
        }

    }
}
