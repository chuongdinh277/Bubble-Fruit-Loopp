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
        public void AutoFindPrefabs()
        {
#if UNITY_EDITOR
            if (bubblePrefab == null) bubblePrefab = AssetDatabase.LoadAssetAtPath<BubbleActor>("Assets/_Game/Resources/Prefabs/BubbleAsset2D.prefab");
            if (fruitPrefab == null) fruitPrefab = AssetDatabase.LoadAssetAtPath<Fruit>("Assets/_Game/Resources/Prefabs/FruitAsset2D.prefab");
            if (box4Prefab == null) box4Prefab = AssetDatabase.LoadAssetAtPath<BoxView>("Assets/_Game/Resources/Box4.prefab");

            int fruitTypeCount = System.Enum.GetValues(typeof(FruitType)).Length;
            if (fruitMaterials == null || fruitMaterials.Length != fruitTypeCount)
                fruitMaterials = new Material[fruitTypeCount];
            fruitMaterials[(int)FruitType.Apple] = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Apple.mat");
            fruitMaterials[(int)FruitType.Orange] = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Orange.mat");
            fruitMaterials[(int)FruitType.Grape] = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Grape.mat");
            fruitMaterials[(int)FruitType.Lemon] = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Lemon.mat");
            fruitMaterials[(int)FruitType.Strawberry] = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Strawberry.mat");

            if (fruitSprites == null || fruitSprites.Length == 0)
            {
                fruitSprites = new Sprite[5];
                fruitSprites[(int)FruitType.Apple] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Texture/Gameplay/quablue-removebg-preview.png"); // placeholder
                fruitSprites[(int)FruitType.Orange] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Texture/Gameplay/quacam-removebg-preview.png");
                fruitSprites[(int)FruitType.Grape] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Texture/Gameplay/quapurple-removebg-preview.png");
                fruitSprites[(int)FruitType.Lemon] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Texture/Gameplay/quachanh-removebg-preview.png");
                fruitSprites[(int)FruitType.Strawberry] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Texture/Gameplay/quadau-removebg-preview.png");
            }
            EnsureSeparateContainers();
            EditorUtility.SetDirty(this);
#endif
        }

        [Button("Refresh Box Colors From Palette", ButtonSizes.Large)]
        [GUIColor(0.35f, 0.9f, 1f)]
        public void RefreshPaletteColors()
        {
#if UNITY_EDITOR
            for (int index = 0; index < boxes.Count; index++)
            {
                BoxConfig box = boxes[index];
                if (box?.instance == null) continue;
                box.instance.Configure(box.fruitType, box.capacity, GetColorFor(box.fruitType));
                EditorUtility.SetDirty(box.instance);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        [HorizontalGroup("Actions")]
        [Button("Load From LevelAsset", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        public void LoadFromAsset()
        {
            if (currentLevel == null) return;

#if UNITY_EDITOR
            if (!EnsureSeparateContainers()) return;
            ClearScene();

            foreach (var bData in currentLevel.bubbles)
            {
                var cfg = AddBubble();
                if (cfg != null)
                {
                    cfg.instance.transform.position = bData.position;
                    cfg.fruits = new List<FruitType>(bData.fruits);
                    cfg.bubbleScale = bData.bubbleScale > 0.1f
                        ? bData.bubbleScale
                        : CalculateBubbleScale(cfg.fruits.Count);
                    cfg.fruitScales = bData.fruitScales != null
                        ? new List<Vector3>(bData.fruitScales)
                        : new List<Vector3>();
                    UpdateBubbleVisuals(cfg);
                }
            }

            foreach (var boxData in currentLevel.boxes)
            {
                var cfg = AddBoxWithCapacity(4);
                if (cfg != null)
                {
                    cfg.instance.transform.position = boxData.position;
                    cfg.fruitType = boxData.fruitType;
                    cfg.capacity = 4;
                    cfg.column = currentLevel.boxLayoutVersion > 0
                        ? Mathf.Clamp(boxData.column, 0, 2)
                        : ClosestBoxColumn(boxData.position.x);
                    cfg.queueOrder = currentLevel.boxLayoutVersion > 0
                        ? Mathf.Max(0, boxData.queueOrder)
                        : 0;
                    UpdateBoxVisuals(cfg);
                }
            }
#endif
        }

        [HorizontalGroup("Actions")]
        [Button("Save To LevelAsset", ButtonSizes.Large)]
        [GUIColor(0.2f, 1f, 0.2f)]
        public void SaveToAsset()
        {
            if (currentLevel == null)
            {
#if UNITY_EDITOR
                EditorUtility.DisplayDialog("Error", "Please assign a LevelData asset to save to!", "OK");
#endif
                return;
            }

            // Validate
            Dictionary<FruitType, int> spawned = new Dictionary<FruitType, int>();
            Dictionary<FruitType, int> capacities = new Dictionary<FruitType, int>();

            foreach (var b in bubbles)
            {
                foreach (var f in b.fruits)
                {
                    if (!spawned.ContainsKey(f)) spawned[f] = 0;
                    spawned[f]++;
                }
            }
            foreach (var b in boxes)
            {
                if (!capacities.ContainsKey(b.fruitType)) capacities[b.fruitType] = 0;
                capacities[b.fruitType] += 4;
            }

            bool valid = true;
            string log = "";
            foreach (FruitType type in System.Enum.GetValues(typeof(FruitType)))
            {
                int s = spawned.ContainsKey(type) ? spawned[type] : 0;
                int c = capacities.ContainsKey(type) ? capacities[type] : 0;
                if (s == 0 && c == 0) continue;
                if (s != c)
                {
                    valid = false;
                    log += $"[ERROR] {type}: {s} fruits != {c} box slots\n";
                }
            }

            if (!valid)
            {
#if UNITY_EDITOR
                EditorUtility.DisplayDialog("Validation Failed", "Cannot save level because fruits do not match box capacities:\n\n" + log, "OK");
#endif
                return;
            }

            // Save data
            UpdateBoxQueueMetadata();
            currentLevel.bubbles.Clear();
            foreach (var b in bubbles)
            {
                if (b.instance == null) continue;
                List<Vector3> scales = new List<Vector3>();
                Transform fruitRoot = b.instance.transform.Find("FruitRoot");
                if (fruitRoot != null)
                {
                    for (int index = 0; index < fruitRoot.childCount; index++)
                    {
                        Fruit fruit = fruitRoot.GetChild(index).GetComponent<Fruit>();
                        if (fruit != null) scales.Add(fruit.transform.localScale);
                    }
                }
                currentLevel.bubbles.Add(new LevelData.BubbleSpawnData {
                    position = b.instance.transform.position,
                    bubbleScale = b.bubbleScale,
                    fruits = new List<FruitType>(b.fruits),
                    fruitScales = scales
                });
            }

            currentLevel.boxes.Clear();
            foreach (var b in boxes)
            {
                if (b.instance == null) continue;
                currentLevel.boxes.Add(new LevelData.BoxSpawnData {
                    position = b.instance.transform.position,
                    fruitType = b.fruitType,
                    capacity = 4,
                    column = b.column,
                    queueOrder = b.queueOrder
                });
            }
            currentLevel.boxLayoutVersion = 1;

#if UNITY_EDITOR
            EditorUtility.SetDirty(currentLevel);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Level saved successfully to " + currentLevel.name, "OK");
#endif
        }

        [Button("Arrange 3 Columns + Save Order", ButtonSizes.Large)]
        [GUIColor(0.25f, 0.85f, 1f)]
        public void ArrangeAndSaveBoxQueue()
        {
#if UNITY_EDITOR
            if (!EnsureSeparateContainers() || currentLevel == null) return;
            SyncScene();

            List<BoxConfig>[] columnBoxes =
            {
                new List<BoxConfig>(), new List<BoxConfig>(), new List<BoxConfig>()
            };
            for (int index = 0; index < boxes.Count; index++)
            {
                BoxConfig box = boxes[index];
                if (box?.instance == null) continue;
                box.column = ClosestBoxColumn(box.instance.transform.position.x);
                columnBoxes[box.column].Add(box);
            }

            for (int column = 0; column < columnBoxes.Length; column++)
            {
                columnBoxes[column].Sort((a, b) =>
                    b.instance.transform.position.y.CompareTo(a.instance.transform.position.y));
                for (int row = 0; row < columnBoxes[column].Count; row++)
                {
                    BoxConfig box = columnBoxes[column][row];
                    box.queueOrder = row;
                    Undo.RecordObject(box.instance.transform, "Arrange Box Queue");
                    box.instance.transform.position = new Vector3(
                        (1 - column) * boxColumnSpacing,
                        boxFirstRowY - row * boxRowSpacing,
                        box.instance.transform.position.z);
                    box.instance.transform.localScale = box4Prefab != null
                        ? box4Prefab.transform.localScale * boxDisplayScale
                        : Vector3.one * boxDisplayScale;
                    box.instance.SetEditorClosed(row > 0);
                }
            }

            EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            SaveToAsset();
#endif
        }

    }
}
