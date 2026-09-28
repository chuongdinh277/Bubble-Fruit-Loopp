using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Runtime;

namespace BubbleFruitLoop.Editor
{
    public partial class LevelBuilderWindow
    {
        private void DrawBubbleSummary(Dictionary<FruitType, int> spawnedFruits, Dictionary<FruitType, int> boxCapacities)
        {
            foreach (var b in bubbles)
            {
                foreach (var f in b.fruits)
                {
                    if (!spawnedFruits.ContainsKey(f))
                        spawnedFruits[f] = 0;
                    spawnedFruits[f]++;
                }
            }

            foreach (var b in boxes)
            {
                if (!boxCapacities.ContainsKey(b.fruitType))
                    boxCapacities[b.fruitType] = 0;
                boxCapacities[b.fruitType] += b.capacity;
            }
        }

        private void DrawBubbleAddControls(int i)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Bubble {i + 1}", EditorStyles.boldLabel, GUILayout.Width(80));
            if (GUILayout.Button("+ Fruit", GUILayout.Width(60)) && bubbles[i].fruits.Count < 4)
                bubbles[i].fruits.Add(FruitType.Apple);
            if (GUILayout.Button("- Fruit", GUILayout.Width(60)) && bubbles[i].fruits.Count > 1)
                bubbles[i].fruits.RemoveAt(bubbles[i].fruits.Count - 1);
        }

        private void FinishBubbleControls(int i)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int j = 0; j < bubbles[i].fruits.Count; j++)
            {
                bubbles[i].fruits[j] = (FruitType)EditorGUILayout.EnumPopup(bubbles[i].fruits[j], GUILayout.Width(80));
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void ConfigureColumnObjects(BoxView box4Prefab, List<BoxColumnAuthoring> spawnedColumns, int i, int colIndex, Transform columnRoot)
        {
            Transform active = new GameObject("Active Point").transform;
            active.SetParent(columnRoot);
            active.position = new Vector3(-1.5f + colIndex * 1.5f, -6.2f);
            Transform pickup = new GameObject("Pickup Point").transform;
            pickup.SetParent(columnRoot);
            pickup.position = new Vector3(-1.5f + colIndex * 1.5f, -3.55f);
            List<BoxView> columnBoxes = new List<BoxView>();
            for (int r = 0; r < 2; r++)
            {
                if (i + r >= boxes.Count)
                    break;
                BoxConfig cfg = boxes[i + r];
                cfg.capacity = 4;
                BoxView box = PrefabUtility.InstantiatePrefab(box4Prefab, columnRoot) as BoxView;
                box.name = $"Box_{cfg.fruitType}_{cfg.capacity}";
                Color c = Color.white;
                if (cfg.fruitType == FruitType.Apple)
                    c = new Color(0f, 0.46f, 1f);
                else if (cfg.fruitType == FruitType.Orange)
                    c = new Color(1f, 0.52f, 0.08f);
                else if (cfg.fruitType == FruitType.Grape)
                    c = new Color(0.58f, 0.24f, 0.88f);
                else if (cfg.fruitType == FruitType.Lemon)
                    c = new Color(1f, 0.84f, 0.12f);
                else if (cfg.fruitType == FruitType.Strawberry)
                    c = new Color(1f, 0.34f, 0.52f);
                box.Configure(cfg.fruitType, cfg.capacity, c);
                box.transform.position = active.position + Vector3.down * r * 0.9f;
                columnBoxes.Add(box);
            }

            BoxColumnAuthoring columnAuth = columnRoot.gameObject.AddComponent<BoxColumnAuthoring>();
            columnAuth.Configure(active, pickup, columnBoxes.ToArray());
            spawnedColumns.Add(columnAuth);
        }

        private void ConfigureAuthoredBubble(BubbleActor bubblePrefab, Fruit fruitPrefab, Material[] materials, Transform bubbleRoot, List<BubbleActor> spawnedBubbles, List<Fruit> spawnedFruits, int i)
        {
            BubbleActor bubble = PrefabUtility.InstantiatePrefab(bubblePrefab, bubbleRoot) as BubbleActor;
            bubble.name = $"Bubble_{i + 1:00}";
            bubble.transform.position = new Vector3(-3f + i % 3 * 3f, 6.4f - i / 3 * 2.15f);
            spawnedBubbles.Add(bubble);
            for (int f = 0; f < bubbles[i].fruits.Count; f++)
            {
                FruitType type = bubbles[i].fruits[f];
                Fruit fruit = PrefabUtility.InstantiatePrefab(fruitPrefab, bubble.transform) as Fruit;
                fruit.name = $"Fruit_{type}_{f + 1}";
                Color c = Color.white; // Fetch color based on type
                if (type == FruitType.Apple)
                    c = new Color(0f, 0.46f, 1f);
                else if (type == FruitType.Orange)
                    c = new Color(1f, 0.52f, 0.08f);
                else if (type == FruitType.Grape)
                    c = new Color(0.58f, 0.24f, 0.88f);
                else if (type == FruitType.Lemon)
                    c = new Color(1f, 0.84f, 0.12f);
                else if (type == FruitType.Strawberry)
                    c = new Color(1f, 0.34f, 0.52f);
                fruit.Configure(type, c);
                if (materials[(int)type] != null)
                    fruit.SetSharedMaterial(materials[(int)type]);
                fruit.transform.localPosition = new Vector3((f % 2 - 0.5f) * 0.32f, (f / 2 - 0.5f) * 0.42f, -0.5f);
                fruit.transform.SetParent(bubbleRoot, true);
                bubble.AddFruit(fruit);
                spawnedFruits.Add(fruit);
            }
        }

        private void ConfigurePrototypeBootstrap(PrototypeBootstrap bootstrap, List<BubbleActor> spawnedBubbles, List<Fruit> spawnedFruits, List<BoxColumnAuthoring> spawnedColumns)
        {
            SerializedObject serialized = new SerializedObject(bootstrap);
            SerializedProperty bubblesProp = serialized.FindProperty("bubbles");
            bubblesProp.arraySize = spawnedBubbles.Count;
            for (int i = 0; i < spawnedBubbles.Count; i++)
                bubblesProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedBubbles[i];
            SerializedProperty fruitsProp = serialized.FindProperty("fruits");
            fruitsProp.arraySize = spawnedFruits.Count;
            for (int i = 0; i < spawnedFruits.Count; i++)
                fruitsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedFruits[i];
            SerializedProperty columnsProp = serialized.FindProperty("columns");
            columnsProp.arraySize = spawnedColumns.Count;
            for (int i = 0; i < spawnedColumns.Count; i++)
                columnsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedColumns[i];
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Level successfully built to scene!");
        }
    }
}
