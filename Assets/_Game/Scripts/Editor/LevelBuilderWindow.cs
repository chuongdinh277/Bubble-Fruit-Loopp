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
    public class LevelBuilderWindow : EditorWindow
    {
        [System.Serializable]
        public class BubbleConfig
        {
            public List<FruitType> fruits = new List<FruitType>() { FruitType.Apple, FruitType.Apple, FruitType.Apple };
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

            foreach (var b in bubbles)
            {
                foreach (var f in b.fruits)
                {
                    if (!spawnedFruits.ContainsKey(f)) spawnedFruits[f] = 0;
                    spawnedFruits[f]++;
                }
            }

            foreach (var b in boxes)
            {
                if (!boxCapacities.ContainsKey(b.fruitType)) boxCapacities[b.fruitType] = 0;
                boxCapacities[b.fruitType] += b.capacity;
            }

            bool isValid = true;
            GUILayout.Space(10);
            GUILayout.Label("Validation Check:", EditorStyles.boldLabel);
            
            foreach (FruitType type in System.Enum.GetValues(typeof(FruitType)))
            {
                int spawned = spawnedFruits.ContainsKey(type) ? spawnedFruits[type] : 0;
                int capacity = boxCapacities.ContainsKey(type) ? boxCapacities[type] : 0;
                
                if (spawned == 0 && capacity == 0) continue;
                
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

            if (bubbles.Count == 0 || boxes.Count == 0) isValid = false;

            GUILayout.Space(10);
            if (GUILayout.Button("BUILD LEVEL TO SCENE", GUILayout.Height(40)))
            {
                if (isValid) BuildScene();
                else EditorUtility.DisplayDialog("Error", "Validation failed! Fruit count must exactly match box capacity.", "OK");
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);

            // BUBBLES
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Bubbles ({bubbles.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Bubble", GUILayout.Width(100))) bubbles.Add(new BubbleConfig());
            GUILayout.EndHorizontal();

            for (int i = 0; i < bubbles.Count; i++)
            {
                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Bubble {i + 1}", EditorStyles.boldLabel, GUILayout.Width(80));
                
                if (GUILayout.Button("+ Fruit", GUILayout.Width(60)) && bubbles[i].fruits.Count < 4)
                    bubbles[i].fruits.Add(FruitType.Apple);
                
                if (GUILayout.Button("- Fruit", GUILayout.Width(60)) && bubbles[i].fruits.Count > 1)
                    bubbles[i].fruits.RemoveAt(bubbles[i].fruits.Count - 1);

                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    bubbles.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    continue;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                for (int j = 0; j < bubbles[i].fruits.Count; j++)
                {
                    bubbles[i].fruits[j] = (FruitType)EditorGUILayout.EnumPopup(bubbles[i].fruits[j], GUILayout.Width(80));
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            // BOXES
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Boxes ({boxes.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Box", GUILayout.Width(100))) boxes.Add(new BoxConfig());
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
            if (oldBubbleBoard != null) DestroyImmediate(oldBubbleBoard);
            
            GameObject oldBoxBoard = GameObject.Find("Box Board");
            if (oldBoxBoard != null) DestroyImmediate(oldBoxBoard);

            // Load templates
            BubbleActor bubblePrefab = AssetDatabase.LoadAssetAtPath<BubbleActor>("Assets/_Game/Resources/Prefabs/BubbleTemplate.prefab") ?? FindAnyObjectByType<BubbleActor>(FindObjectsInactive.Include);
            FruitActor fruitPrefab = AssetDatabase.LoadAssetAtPath<FruitActor>("Assets/_Game/Resources/Prefabs/FruitTemplate.prefab") ?? FindAnyObjectByType<FruitActor>(FindObjectsInactive.Include);
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
            List<FruitActor> spawnedFruits = new List<FruitActor>();

            for (int i = 0; i < bubbles.Count; i++)
            {
                BubbleActor bubble = PrefabUtility.InstantiatePrefab(bubblePrefab, bubbleRoot) as BubbleActor;
                bubble.name = $"Bubble_{i + 1:00}";
                bubble.transform.position = new Vector3(-3f + i % 3 * 3f, 6.4f - i / 3 * 2.15f);
                spawnedBubbles.Add(bubble);

                for (int f = 0; f < bubbles[i].fruits.Count; f++)
                {
                    FruitType type = bubbles[i].fruits[f];
                    FruitActor fruit = PrefabUtility.InstantiatePrefab(fruitPrefab, bubble.transform) as FruitActor;
                    fruit.name = $"Fruit_{type}_{f + 1}";
                    
                    Color c = Color.white; // Fetch color based on type
                    if (type == FruitType.Apple) c = new Color(0f, 0.46f, 1f);
                    else if (type == FruitType.Orange) c = new Color(1f, 0.52f, 0.08f);
                    else if (type == FruitType.Grape) c = new Color(0.58f, 0.24f, 0.88f);
                    else if (type == FruitType.Lemon) c = new Color(1f, 0.84f, 0.12f);
                    else if (type == FruitType.Strawberry) c = new Color(1f, 0.34f, 0.52f);
                    
                    fruit.Configure(type, c);
                    if (materials[(int)type] != null) fruit.SetSharedMaterial(materials[(int)type]);
                    
                    fruit.transform.localPosition = new Vector3((f % 2 - 0.5f) * 0.32f, (f / 2 - 0.5f) * 0.42f, -0.5f);
                    fruit.transform.SetParent(bubbleRoot, true);
                    bubble.AddFruit(fruit);
                    spawnedFruits.Add(fruit);
                }
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
                Transform active = new GameObject("Active Point").transform; active.SetParent(columnRoot); active.position = new Vector3(-1.5f + colIndex * 1.5f, -6.2f);
                Transform pickup = new GameObject("Pickup Point").transform; pickup.SetParent(columnRoot); pickup.position = new Vector3(-1.5f + colIndex * 1.5f, -3.55f);
                
                List<BoxView> columnBoxes = new List<BoxView>();
                for (int r = 0; r < 2; r++)
                {
                    if (i + r >= boxes.Count) break;
                    BoxConfig cfg = boxes[i + r];
                    cfg.capacity = 4;
                    BoxView box = PrefabUtility.InstantiatePrefab(box4Prefab, columnRoot) as BoxView;
                    box.name = $"Box_{cfg.fruitType}_{cfg.capacity}";
                    
                    Color c = Color.white;
                    if (cfg.fruitType == FruitType.Apple) c = new Color(0f, 0.46f, 1f);
                    else if (cfg.fruitType == FruitType.Orange) c = new Color(1f, 0.52f, 0.08f);
                    else if (cfg.fruitType == FruitType.Grape) c = new Color(0.58f, 0.24f, 0.88f);
                    else if (cfg.fruitType == FruitType.Lemon) c = new Color(1f, 0.84f, 0.12f);
                    else if (cfg.fruitType == FruitType.Strawberry) c = new Color(1f, 0.34f, 0.52f);
                    
                    box.Configure(cfg.fruitType, cfg.capacity, c);
                    box.transform.position = active.position + Vector3.down * r * 0.9f;
                    columnBoxes.Add(box);
                }
                
                BoxColumnAuthoring columnAuth = columnRoot.gameObject.AddComponent<BoxColumnAuthoring>();
                columnAuth.Configure(active, pickup, columnBoxes.ToArray());
                spawnedColumns.Add(columnAuth);
            }

            // Bind to Bootstrap
            SerializedObject serialized = new SerializedObject(bootstrap);
            
            SerializedProperty bubblesProp = serialized.FindProperty("bubbles");
            bubblesProp.arraySize = spawnedBubbles.Count;
            for(int i = 0; i < spawnedBubbles.Count; i++) bubblesProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedBubbles[i];
            
            SerializedProperty fruitsProp = serialized.FindProperty("fruits");
            fruitsProp.arraySize = spawnedFruits.Count;
            for(int i = 0; i < spawnedFruits.Count; i++) fruitsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedFruits[i];
            
            SerializedProperty columnsProp = serialized.FindProperty("columns");
            columnsProp.arraySize = spawnedColumns.Count;
            for(int i = 0; i < spawnedColumns.Count; i++) columnsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedColumns[i];
            
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Level successfully built to scene!");
        }
    }
}
