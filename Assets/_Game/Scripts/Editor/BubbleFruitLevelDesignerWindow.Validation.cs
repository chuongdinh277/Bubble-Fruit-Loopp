using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;

namespace BubbleFruitLoop.Editor
{
    public sealed partial class BubbleFruitLevelDesignerWindow
    {
        private string BuildValidationMessage()
        {
            if (level == null) return "Chưa chọn Level Definition.";
            if (level.boxes.Count == 0) return "Level chưa có box.";
            if (!AreTypesSupported())
                return "CHƯA HỢP LỆ: scene hiện mới có sprite Orange và Strawberry. Không dùng Apple/Grape/Lemon cho tới khi thêm sprite tương ứng.";
            Dictionary<FruitType, int> differences = BuildTypeCounts(true);
            if (FruitTotal != BoxCapacityTotal)
                return $"CHƯA HỢP LỆ: fruit = {FruitTotal}, sức chứa box = {BoxCapacityTotal}.";
            if (differences.Count > 0)
            {
                string message = "CHƯA HỢP LỆ THEO LOẠI: ";
                foreach (KeyValuePair<FruitType, int> item in differences)
                    message += $"{item.Key} {(item.Value > 0 ? "+" : string.Empty)}{item.Value}; ";
                return message + "(số dương = thừa fruit, số âm = thiếu fruit)";
            }
            return $"HỢP LỆ: {FruitTotal} fruit khớp chính xác {BoxCapacityTotal} ô box theo từng loại.";
        }

        private Dictionary<FruitType, int> BuildTypeCounts(bool removeBalanced)
        {
            Dictionary<FruitType, int> counts = new();
            if (level == null) return counts;
            for (int bubble = 0; bubble < level.bubbles.Count; bubble++)
            for (int fruit = 0; fruit < level.bubbles[bubble].fruits.Count; fruit++)
            {
                FruitType type = level.bubbles[bubble].fruits[fruit];
                counts[type] = counts.TryGetValue(type, out int value) ? value + 1 : 1;
            }
            for (int index = 0; index < level.boxes.Count; index++)
            {
                BubbleFruitLevelDefinition.BoxSetup box = level.boxes[index];
                int capacity = (int)box.capacity;
                counts[box.fruitType] = counts.TryGetValue(box.fruitType, out int value) ? value - capacity : -capacity;
            }
            if (removeBalanced)
            {
                List<FruitType> balanced = new();
                foreach (KeyValuePair<FruitType, int> item in counts)
                    if (item.Value == 0) balanced.Add(item.Key);
                for (int index = 0; index < balanced.Count; index++) counts.Remove(balanced[index]);
            }
            return counts;
        }

        private void RebuildBoxes()
        {
            BoxSystemPrefabBuilder.GeneratePrefabs();
            GameObject old = GameObject.Find(BoxLayoutName);
            if (old != null) DestroyImmediate(old);
            GameObject root = new(BoxLayoutName);
            BoxAssignmentTable table = root.AddComponent<BoxAssignmentTable>();
            List<BoxView> views = new();
            List<FruitType> types = new();
            List<int> columns = new();
            List<int> orders = new();
            int[] nextOrder = new int[3];
            Camera camera = Camera.main;

            for (int index = 0; index < level.boxes.Count; index++)
            {
                BubbleFruitLevelDefinition.BoxSetup setup = level.boxes[index];
                const int capacity = 4;
                int column = Mathf.Clamp(setup.column, 0, 2);
                int order = nextOrder[column]++;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/Box4.prefab");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = $"C{column + 1} Q{order + 1} - {setup.fruitType} Box{capacity}";
                Vector3 position = camera.ViewportToWorldPoint(new Vector3(
                    0.5f, 0.065f - order * 0.080f, -camera.transform.position.z));
                position.x = camera.transform.position.x + (1 - column) * 1.65f;
                position.z = 0f;
                instance.transform.position = position;
                instance.transform.localScale = Vector3.one * 1.05f;
                BoxView view = instance.GetComponent<BoxView>();
                view.Configure(setup.fruitType, capacity, BoxColor(setup.fruitType));
                view.SetEditorClosed(order > 0);
                views.Add(view);
                types.Add(setup.fruitType);
                columns.Add(column);
                orders.Add(order);
            }

            Transform[] points = { Find("P1"), Find("P2"), Find("P3") };
            table.Configure(views.ToArray(), types.ToArray(), columns.ToArray(), orders.ToArray(), points);
            FruitLoopManager loop = Object.FindFirstObjectByType<FruitLoopManager>();
            BoxManager board = loop.GetComponent<BoxManager>();
            if (board == null) board = loop.gameObject.AddComponent<BoxManager>();
            board.ConfigureAssignmentTable(table);
            board.ConfigureSceneLayout(views.ToArray(), points);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(board);
            Selection.activeGameObject = root;
        }

    }
}
