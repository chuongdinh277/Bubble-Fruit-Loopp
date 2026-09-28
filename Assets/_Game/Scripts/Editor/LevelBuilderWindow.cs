using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Runtime;

namespace BubbleFruitLoop.Editor
{
    public partial class LevelBuilderWindow : EditorWindow
    {
        [System.Serializable]
        public class BubbleConfig
        {
            public List<FruitType> fruits = new List<FruitType>()
            {
                FruitType.Apple,
                FruitType.Apple,
                FruitType.Apple
            };
        }

        [System.Serializable]
        public class BoxConfig
        {
            public FruitType fruitType = FruitType.Apple;
            public int capacity = 4;
        }

        public List<BubbleConfig> bubbles = new List<BubbleConfig>();
        public List<BoxConfig> boxes = new List<BoxConfig>();
        private Vector2 scrollPos;
        [MenuItem("BubbleFruit/Level Builder (Odin-like)")]
        public static void ShowWindow()
        {
            GetWindow<LevelBuilderWindow>("Level Builder").Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("BUBBLE FRUIT LOOP - LEVEL BUILDER", EditorStyles.boldLabel);
            // VALIDATION
            Dictionary<FruitType, int> spawnedFruits = new Dictionary<FruitType, int>();
            Dictionary<FruitType, int> boxCapacities = new Dictionary<FruitType, int>();
            DrawBubbleSummary(spawnedFruits, boxCapacities);
            bool isValid = true;
            GUILayout.Space(10);
            GUILayout.Label("Validation Check:", EditorStyles.boldLabel);
            foreach (FruitType type in System.Enum.GetValues(typeof(FruitType)))
            {
                int spawned = spawnedFruits.ContainsKey(type) ? spawnedFruits[type] : 0;
                int capacity = boxCapacities.ContainsKey(type) ? boxCapacities[type] : 0;
                if (spawned == 0 && capacity == 0)
                    continue;
                if (spawned == capacity)
                {
                    GUI.color = Color.green;
                    GUILayout.Label($"[OK] {type}: {spawned} fruits match {capacity} box capacity.");
                }
                else
                {
                    isValid = false;
                    GUI.color = Color.red;
                    GUILayout.Label($"[ERROR] {type}: {spawned} fruits != {capacity} box capacity!");
                }

                GUI.color = Color.white;
            }

            ValidateLevelCounts(ref isValid);
            for (int i = 0; i < bubbles.Count; i++)
            {
                DrawBubbleAddControls(i);
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    bubbles.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    continue;
                }

                FinishBubbleControls(i);
            }

            // BOXES
            DrawBoxQueueControls();
            GUILayout.EndScrollView();
        }

        private void BuildScene()
        {
            // We find PrototypeBootstrap and replace the scene bubbles and fruits
            PrototypeBootstrap bootstrap = FindAnyObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("Error", "Cannot find PrototypeBootstrap in scene!", "OK");
                return;
            }

            // Delete old Bubble Board and Box Board if they exist
            GameObject oldBubbleBoard = GameObject.Find("Bubble Board");
            if (oldBubbleBoard != null)
                DestroyImmediate(oldBubbleBoard);
            GameObject oldBoxBoard = GameObject.Find("Box Board");
            if (oldBoxBoard != null)
                DestroyImmediate(oldBoxBoard);
            // Load templates
            BubbleActor bubblePrefab = AssetDatabase.LoadAssetAtPath<BubbleActor>("Assets/_Game/Resources/Prefabs/BubbleTemplate.prefab") ?? FindAnyObjectByType<BubbleActor>(FindObjectsInactive.Include);
            Fruit fruitPrefab = AssetDatabase.LoadAssetAtPath<Fruit>("Assets/_Game/Resources/Prefabs/FruitTemplate.prefab") ?? FindAnyObjectByType<Fruit>(FindObjectsInactive.Include);
            BoxView box4Prefab = AssetDatabase.LoadAssetAtPath<BoxView>("Assets/_Game/Resources/Box4.prefab");
            if (bubblePrefab == null || fruitPrefab == null || box4Prefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Missing prefabs in Resources. Ensure they exist.", "OK");
                return;
            }

            Material[] materials = new Material[]
            {
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/AppleMaterial.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/OrangeMaterial.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/GrapeMaterial.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/LemonMaterial.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/StrawberryMaterial.mat")
            };
            // Spawn Bubbles
            Transform bubbleRoot = new GameObject("Bubble Board").transform;
            List<BubbleActor> spawnedBubbles = new List<BubbleActor>();
            List<Fruit> spawnedFruits = new List<Fruit>();
            for (int i = 0; i < bubbles.Count; i++)
            {
                ConfigureAuthoredBubble(bubblePrefab, fruitPrefab, materials, bubbleRoot, spawnedBubbles, spawnedFruits, i);
            }

            // Spawn Boxes
            Transform boxRoot = new GameObject("Box Board").transform;
            List<BoxColumnAuthoring> spawnedColumns = new List<BoxColumnAuthoring>();
            // We just place boxes in columns of 2 for simplicity
            for (int i = 0; i < boxes.Count; i += 2)
            {
                int colIndex = i / 2;
                Transform columnRoot = new GameObject($"Column_{colIndex + 1}").transform;
                columnRoot.SetParent(boxRoot, false);
                ConfigureColumnObjects(box4Prefab, spawnedColumns, i, colIndex, columnRoot);
            }

            // Bind to Bootstrap
            ConfigurePrototypeBootstrap(bootstrap, spawnedBubbles, spawnedFruits, spawnedColumns);
        }

        private void DrawBoxQueueControls()
        {
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Boxes ({boxes.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Box", GUILayout.Width(100)))
                boxes.Add(new BoxConfig());
            GUILayout.EndHorizontal();
            for (int i = 0; i < boxes.Count; i++)
            {
                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Box {i + 1}", EditorStyles.boldLabel, GUILayout.Width(50));
                boxes[i].fruitType = (FruitType)EditorGUILayout.EnumPopup(boxes[i].fruitType, GUILayout.Width(100));
                boxes[i].capacity = 4;
                EditorGUILayout.LabelField("4 Slots", GUILayout.Width(80));
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    boxes.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    continue;
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        private void ValidateLevelCounts(ref bool isValid)
        {
            if (bubbles.Count == 0 || boxes.Count == 0)
                isValid = false;
            GUILayout.Space(10);
            if (GUILayout.Button("BUILD LEVEL TO SCENE", GUILayout.Height(40)))
            {
                if (isValid)
                    BuildScene();
                else
                    EditorUtility.DisplayDialog("Error", "Validation failed! Fruit count must exactly match box capacity.", "OK");
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            // BUBBLES
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Bubbles ({bubbles.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Bubble", GUILayout.Width(100)))
                bubbles.Add(new BubbleConfig());
            GUILayout.EndHorizontal();
        }
    }
}
