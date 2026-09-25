using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BubbleFruitLoop.Editor
{
    [ExecuteAlways]
    public class LevelDesignManager : SerializedMonoBehaviour
    {
        // Keep the visible guide and every fruit comfortably inside the glossy
        // outer shell. These values are also mirrored by LevelLoader at runtime.
        private const float InnerBoundaryInset = 0.90f;
        private const float FruitRimClearance = 0.52f;
        private const float InitialPackingRatio = 0.76f;

        [Title("Level Asset", "Create a LevelData asset and drag it here to load/save")]
        [Required]
        [InlineEditor]
        public LevelData currentLevel;

        [Title("Prefabs & Materials")]
        [Required] public BubbleActor bubblePrefab;
        [Required] public FruitActor fruitPrefab;
        [Required] public BoxView box4Prefab;
        
        [Title("Fruit Visuals (Optional)")]
        [LabelText("Box Color Palette")]
        public Material[] fruitMaterials;
        public Sprite[] fruitSprites;

        [Title("Scene Containers")]
        [Required] public Transform bubbleContainer;
        [Required] public Transform boxContainer;

        [Title("Box Queue Layout", "Xếp thành 3 cột; hàng dưới sẽ được đẩy dần lên khi chơi")]
        [MinValue(0.1f)] public float boxColumnSpacing = 1.65f;
        public float boxFirstRowY = -2.45f;
        [MinValue(0.1f)] public float boxRowSpacing = 1.72f;
        [MinValue(0.1f)] public float boxDisplayScale = 1.12f;

        [Title("Bubble Sizing", "Bubble nhiều fruit sẽ lớn hơn nhẹ, nhưng vẫn giữ kiểu nhỏ gọn")]
        [MinValue(0.1f), LabelText("Size nhỏ nhất")]
        public float minBubbleScale = 0.36f;

        [MinValue(0.1f), LabelText("Size lớn nhất")]
        public float maxBubbleScale = 0.48f;

        [MinValue(1), LabelText("Số fruit ở size nhỏ nhất")]
        public int minFruitForSizing = 3;

        [MinValue(1), LabelText("Số fruit ở size lớn nhất")]
        public int maxFruitForSizing = 8;

        [System.Serializable]
        public class BubbleConfig
        {
            [HorizontalGroup("Row", Width = 0.2f)]
            [PreviewField(40, ObjectFieldAlignment.Left), HideLabel]
            public BubbleActor instance;

            [HorizontalGroup("Row")]
            [OnValueChanged("OnFruitsChanged")]
            public List<FruitType> fruits = new List<FruitType>() { FruitType.Apple, FruitType.Apple, FruitType.Apple };

            [HorizontalGroup("Row", Width = 0.16f)]
            [LabelText("Size"), MinValue(0.1f)]
            [OnValueChanged("OnScaleChanged")]
            public float bubbleScale = 0.4f;

            [HideInInspector]
            public List<Vector3> fruitScales = new List<Vector3>();

            [HideInInspector] public LevelDesignManager manager;

            private void OnFruitsChanged()
            {
                if (manager != null)
                {
                    bubbleScale = manager.CalculateBubbleScale(fruits != null ? fruits.Count : 0);
                    manager.UpdateBubbleVisuals(this);
                }
            }

            private void OnScaleChanged()
            {
                if (manager != null) manager.ApplyBubbleScale(this);
            }
        }

        [System.Serializable]
        public class BoxConfig
        {
            [HorizontalGroup("Row", Width = 0.2f)]
            [PreviewField(40, ObjectFieldAlignment.Left), HideLabel]
            public BoxView instance;

            [HorizontalGroup("Row")]
            [OnValueChanged("OnBoxChanged")]
            public FruitType fruitType = FruitType.Apple;
            
            [HorizontalGroup("Row")]
            [OnValueChanged("OnBoxChanged"), ValueDropdown("GetCapacities")]
            public int capacity = 4;

            [HorizontalGroup("Queue"), ReadOnly, LabelText("Column")]
            public int column;

            [HorizontalGroup("Queue"), ReadOnly, LabelText("Order")]
            public int queueOrder;

            [HideInInspector] public LevelDesignManager manager;
            private int[] GetCapacities() => new int[] { 4 };

            private void OnBoxChanged()
            {
                if (manager != null) manager.UpdateBoxVisuals(this);
            }
        }

        [System.Serializable]
        public class FruitCountInfo
        {
            [ReadOnly, LabelText("Loại quả")]
            public FruitType fruitType;

            [ReadOnly, LabelText("Số lượng")]
            public int count;

            [ReadOnly, LabelText("Có thể chia Box4")]
            public bool canBuildBoxes;
        }

        [Title("Level Content (Edit in Scene!)")]
        [ListDrawerSettings(CustomAddFunction = "AddBubble", CustomRemoveElementFunction = "RemoveBubble")]
        public List<BubbleConfig> bubbles = new List<BubbleConfig>();

        [ListDrawerSettings(CustomAddFunction = "AddBox", CustomRemoveElementFunction = "RemoveBox")]
        public List<BoxConfig> boxes = new List<BoxConfig>();

        [Title("Bubble Check")]
        [TableList(AlwaysExpanded = true, HideToolbar = true)]
        [ReadOnly]
        public List<FruitCountInfo> fruitCountSummary = new List<FruitCountInfo>();

        private void OnValidate()
        {
            foreach (var b in bubbles)
            {
                if (b == null) continue;
                b.manager = this;
                if (b.bubbleScale <= 0.1f)
                    b.bubbleScale = CalculateBubbleScale(b.fruits != null ? b.fruits.Count : 0);
            }
            foreach (var b in boxes)
            {
                if (b == null) continue;
                b.manager = this;
                b.capacity = 4;
            }
        }

        [Button("Auto-Find Project Assets", ButtonSizes.Large)]
        [GUIColor(0.8f, 1f, 0.8f)]
        public void AutoFindPrefabs()
        {
#if UNITY_EDITOR
            if (bubblePrefab == null) bubblePrefab = AssetDatabase.LoadAssetAtPath<BubbleActor>("Assets/_Game/Resources/Prefabs/BubbleAsset2D.prefab");
            if (fruitPrefab == null) fruitPrefab = AssetDatabase.LoadAssetAtPath<FruitActor>("Assets/_Game/Resources/Prefabs/FruitAsset2D.prefab");
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
                        FruitActor fruit = fruitRoot.GetChild(index).GetComponent<FruitActor>();
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

        private int ClosestBoxColumn(float x)
        {
            int closest = 0;
            float best = float.PositiveInfinity;
            for (int column = 0; column < 3; column++)
            {
                float distance = Mathf.Abs(x - (1 - column) * boxColumnSpacing);
                if (distance >= best) continue;
                best = distance;
                closest = column;
            }
            return closest;
        }

        [HorizontalGroup("Actions2")]
        [Button("Generate Default Layout", ButtonSizes.Large)]
        [GUIColor(0.6f, 0.8f, 1f)]
        public void GenerateDefaultLayout()
        {
#if UNITY_EDITOR
            if (!EnsureSeparateContainers()) return;
            ClearScene();

            // Default 9 bubbles layout
            for (int i = 0; i < 9; i++)
            {
                var cfg = AddBubble();
                if (cfg != null)
                {
                    // 3x3 grid at top
                    cfg.instance.transform.position = new Vector3(-2f + (i % 3) * 2f, 3.5f - (i / 3) * 2.15f, 0f);
                    cfg.fruits = new List<FruitType>() { FruitType.Apple, FruitType.Apple, FruitType.Apple, FruitType.Apple };
                    cfg.bubbleScale = CalculateBubbleScale(cfg.fruits.Count);
                    UpdateBubbleVisuals(cfg);
                }
            }

            // Default 3x3 boxes layout (3 columns, 3 rows)
            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 3; row++)
                {
                    var cfg = AddBoxWithCapacity(4);
                    if (cfg != null)
                    {
                        float xPos = -1.5f + col * 1.5f;
                        float yPos = -6.2f - row * 0.9f;
                        cfg.instance.transform.position = new Vector3(xPos, yPos, 0f);
                        cfg.fruitType = (FruitType)col; // Just some default colors
                        UpdateBoxVisuals(cfg);
                    }
                }
            }
#endif
        }

        [Button("Sync Scene To Inspector", ButtonSizes.Medium)]
        private void SyncScene()
        {
#if UNITY_EDITOR
            if (!EnsureSeparateContainers()) return;
#endif
            if (bubbleContainer != null)
            {
                bubbles.Clear();
                foreach (Transform child in bubbleContainer)
                {
                    BubbleActor bubble = child.GetComponent<BubbleActor>();
                    if (bubble != null)
                    {
                        BubbleConfig cfg = new BubbleConfig { instance = bubble, manager = this };
                        Transform visual = bubble.transform.Find("Visual");
                        float rootScale = Mathf.Abs(bubble.transform.localScale.x);
                        float visualScale = visual != null ? Mathf.Abs(visual.localScale.x) : 1f;
                        cfg.bubbleScale = Mathf.Max(0.1f, rootScale * visualScale);
                        cfg.fruits.Clear();
                        cfg.fruitScales.Clear();
                        foreach (FruitActor fruit in bubble.GetComponentsInChildren<FruitActor>(true))
                        {
                            if (fruit != null)
                            {
                                cfg.fruits.Add(fruit.Type);
                                cfg.fruitScales.Add(fruit.transform.localScale);
                            }
                        }
                        if (cfg.fruits.Count == 0) cfg.fruits.AddRange(new[] { FruitType.Apple, FruitType.Apple, FruitType.Apple });
                        bubbles.Add(cfg);
                    }
                }
            }

            if (boxContainer != null)
            {
                boxes.Clear();
                foreach (Transform child in boxContainer)
                {
                    BoxView box = child.GetComponent<BoxView>();
                    if (box != null)
                    {
                        BoxConfig cfg = new BoxConfig { instance = box, manager = this };
                        cfg.capacity = 4;
                        cfg.fruitType = box.editorFruitType;
                        boxes.Add(cfg);
                    }
                }
                UpdateBoxQueueMetadata();
            }
        }

        private void UpdateBoxQueueMetadata()
        {
            List<BoxConfig>[] grouped =
            {
                new List<BoxConfig>(), new List<BoxConfig>(), new List<BoxConfig>()
            };
            for (int index = 0; index < boxes.Count; index++)
            {
                BoxConfig box = boxes[index];
                if (box?.instance == null) continue;
                box.column = ClosestBoxColumn(box.instance.transform.position.x);
                grouped[box.column].Add(box);
            }
            for (int column = 0; column < grouped.Length; column++)
            {
                grouped[column].Sort((a, b) =>
                    b.instance.transform.position.y.CompareTo(a.instance.transform.position.y));
                for (int row = 0; row < grouped[column].Count; row++)
                    grouped[column][row].queueOrder = row;
            }
        }

        [Button("Auto Size All Bubbles", ButtonSizes.Large)]
        [GUIColor(0.75f, 0.55f, 1f)]
        public void AutoSizeAllBubbles()
        {
            for (int index = 0; index < bubbles.Count; index++)
            {
                BubbleConfig cfg = bubbles[index];
                if (cfg == null) continue;
                cfg.bubbleScale = CalculateBubbleScale(cfg.fruits != null ? cfg.fruits.Count : 0);
                ApplyBubbleScale(cfg);
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        public float CalculateBubbleScale(int fruitCount)
        {
            float low = Mathf.Min(minBubbleScale, maxBubbleScale);
            float high = Mathf.Max(minBubbleScale, maxBubbleScale);
            int minCount = Mathf.Max(1, minFruitForSizing);
            int maxCount = Mathf.Max(minCount + 1, maxFruitForSizing);
            float t = Mathf.InverseLerp(minCount, maxCount, Mathf.Max(0, fruitCount));
            return Mathf.Lerp(low, high, t);
        }

        public void ApplyBubbleScale(BubbleConfig cfg)
        {
            if (cfg?.instance == null) return;
            // Never scale the BubbleActor root: FruitRoot is its sibling and must
            // keep the authored fruit size. Only the bubble shell visual changes.
            Vector3 prefabRootScale = bubblePrefab != null
                ? bubblePrefab.transform.localScale
                : Vector3.one * 0.55f;
            cfg.instance.transform.localScale = prefabRootScale;

            Transform visual = cfg.instance.transform.Find("Visual");
            float rootScale = Mathf.Max(0.0001f, Mathf.Abs(prefabRootScale.x));
            float visualMultiplier = Mathf.Max(0.1f, cfg.bubbleScale) / rootScale;
            if (visual != null)
            {
                Transform prefabVisual = bubblePrefab != null ? bubblePrefab.transform.Find("Visual") : null;
                Vector3 baseVisualScale = prefabVisual != null ? prefabVisual.localScale : Vector3.one;
                visual.localScale = baseVisualScale * visualMultiplier;
            }

            // Draw the guide slightly inside the glossy shell. It must not sit on
            // the outer edge, otherwise fruit sprites appear to pierce the bubble.
            Transform boundary = cfg.instance.transform.Find("InnerBoundary");
            Transform prefabBoundary = bubblePrefab != null ? bubblePrefab.transform.Find("InnerBoundary") : null;
            if (boundary != null)
                boundary.localScale = (prefabBoundary != null ? prefabBoundary.localScale : Vector3.one)
                    * (visualMultiplier * InnerBoundaryInset);

            if (cfg.instance.ObstacleCollider is CircleCollider2D circle)
            {
                CircleCollider2D prefabCircle = bubblePrefab != null
                    ? bubblePrefab.ObstacleCollider as CircleCollider2D
                    : null;
                if (prefabCircle != null) circle.radius = prefabCircle.radius * visualMultiplier;
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(cfg.instance);
#endif
        }

        [Button("Repair Containers + Sync Existing Layout", ButtonSizes.Large)]
        [GUIColor(0.35f, 0.85f, 1f)]
        private void RepairAndSyncScene()
        {
#if UNITY_EDITOR
            if (!EnsureSeparateContainers()) return;
            EnsureFruitLoopPath();
            SyncScene();
            for (int index = 0; index < bubbles.Count; index++) UpdateBubbleVisuals(bubbles[index]);
            EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        [HorizontalGroup("BoxGeneration")]
        [Button("1. Check Bubble", ButtonSizes.Large)]
        [GUIColor(0.45f, 0.8f, 1f)]
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
            EditableFruitLoopController controller;

            if (authoring == null)
            {
                GameObject pathObject = new GameObject("Fruit Loop Path (EDIT POINTS)");
                Undo.RegisterCreatedObjectUndo(pathObject, "Create Fruit Loop Path");
                pathObject.transform.SetParent(transform, false);
                authoring = Undo.AddComponent<LoopPathAuthoring>(pathObject);
                controller = Undo.AddComponent<EditableFruitLoopController>(pathObject);

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
                controller = authoring.GetComponent<EditableFruitLoopController>();
                if (controller == null)
                    controller = Undo.AddComponent<EditableFruitLoopController>(authoring.gameObject);
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

        private Dictionary<FruitType, int> CountBubbleFruits()
        {
            Dictionary<FruitType, int> counts = new Dictionary<FruitType, int>();
            for (int bubbleIndex = 0; bubbleIndex < bubbles.Count; bubbleIndex++)
            {
                BubbleConfig bubble = bubbles[bubbleIndex];
                if (bubble == null || bubble.fruits == null) continue;
                for (int fruitIndex = 0; fruitIndex < bubble.fruits.Count; fruitIndex++)
                {
                    FruitType type = bubble.fruits[fruitIndex];
                    counts[type] = counts.TryGetValue(type, out int value) ? value + 1 : 1;
                }
            }
            return counts;
        }

        private static bool HasBoxCombination(int fruitCount)
        {
            return fruitCount >= 0 && fruitCount % 4 == 0;
        }

        private static List<int> CreateBox4Capacities(int fruitCount)
        {
            List<int> result = new List<int>();
            if (!HasBoxCombination(fruitCount)) return result;
            for (int index = 0; index < fruitCount / 4; index++) result.Add(4);
            return result;
        }

        private void ClearScene()
        {
            if (bubbleContainer != null && bubbleContainer == boxContainer)
            {
#if UNITY_EDITOR
                Debug.LogError("LevelDesignManager: Bubble Container và Box Container phải là hai object riêng. Không xoá scene để tránh mất bố cục.", this);
#endif
                return;
            }
            if (bubbleContainer != null)
            {
                for (int i = bubbleContainer.childCount - 1; i >= 0; i--)
                    DestroyImmediate(bubbleContainer.GetChild(i).gameObject);
            }
            if (boxContainer != null)
            {
                for (int i = boxContainer.childCount - 1; i >= 0; i--)
                    DestroyImmediate(boxContainer.GetChild(i).gameObject);
            }
            bubbles.Clear();
            boxes.Clear();
        }

#if UNITY_EDITOR
        private bool EnsureSeparateContainers()
        {
            if (bubbleContainer == null)
            {
                GameObject found = GameObject.Find("BubbleContainer");
                if (found != null) bubbleContainer = found.transform;
            }

            if (boxContainer == null || boxContainer == bubbleContainer)
            {
                Transform mixedContainer = bubbleContainer;
                GameObject found = GameObject.Find("BoxContainer");
                if (found == null)
                {
                    Transform parent = bubbleContainer != null ? bubbleContainer.parent : transform;
                    found = new GameObject("BoxContainer");
                    Undo.RegisterCreatedObjectUndo(found, "Create Box Container");
                    found.transform.SetParent(parent, false);
                }
                boxContainer = found.transform;

                // Older versions placed both kinds under BubbleContainer. Move only
                // direct BoxView children and retain their exact world transforms.
                if (mixedContainer != null && boxContainer != mixedContainer)
                {
                    for (int index = mixedContainer.childCount - 1; index >= 0; index--)
                    {
                        Transform child = mixedContainer.GetChild(index);
                        if (child.GetComponent<BoxView>() != null)
                            Undo.SetTransformParent(child, boxContainer, "Separate Box Container");
                    }
                }
            }

            bool valid = bubbleContainer != null && boxContainer != null && bubbleContainer != boxContainer;
            if (!valid)
                Debug.LogError("LevelDesignManager cần BubbleContainer và BoxContainer riêng biệt.", this);
            return valid;
        }

        private BubbleConfig AddBubble()
        {
            if (!EnsureSeparateContainers() || bubblePrefab == null) return null;
            BubbleActor instance = (BubbleActor)PrefabUtility.InstantiatePrefab(bubblePrefab, bubbleContainer);
            instance.transform.position = Vector3.zero;
            instance.gameObject.name = $"Bubble_{bubbles.Count + 1}";
            var cfg = new BubbleConfig { instance = instance, manager = this };
            cfg.bubbleScale = CalculateBubbleScale(cfg.fruits.Count);
            UpdateBubbleVisuals(cfg);
            return cfg;
        }

        private void RemoveBubble(BubbleConfig cfg)
        {
            if (cfg.instance != null) DestroyImmediate(cfg.instance.gameObject);
            bubbles.Remove(cfg);
        }

        private BoxConfig AddBox() => AddBoxWithCapacity(4);
        
        private BoxConfig AddBoxWithCapacity(int cap)
        {
            if (!EnsureSeparateContainers() || box4Prefab == null) return null;
            cap = 4;
            BoxView instance = (BoxView)PrefabUtility.InstantiatePrefab(box4Prefab, boxContainer);
            instance.transform.position = Vector3.zero;
            instance.gameObject.name = $"Box_{boxes.Count + 1}";
            var cfg = new BoxConfig { instance = instance, manager = this, capacity = cap };
            UpdateBoxVisuals(cfg);
            return cfg;
        }

        private void RemoveBox(BoxConfig cfg)
        {
            if (cfg.instance != null) DestroyImmediate(cfg.instance.gameObject);
            boxes.Remove(cfg);
        }

        public void UpdateBubbleVisuals(BubbleConfig cfg)
        {
            if (cfg.instance == null || fruitPrefab == null) return;
            // Instances created by the broken version have Visual/InnerBoundary
            // recorded as removed prefab children. Replace only that bubble while
            // retaining its authored transform and fruit configuration.
            if (cfg.instance.transform.Find("Visual") == null && bubblePrefab != null)
            {
                BubbleActor damaged = cfg.instance;
                Transform parent = damaged.transform.parent;
                Vector3 position = damaged.transform.position;
                Quaternion rotation = damaged.transform.rotation;
                Vector3 scale = damaged.transform.localScale;
                int sibling = damaged.transform.GetSiblingIndex();
                string objectName = damaged.name;

                cfg.instance = (BubbleActor)PrefabUtility.InstantiatePrefab(bubblePrefab, parent);
                cfg.instance.transform.SetPositionAndRotation(position, rotation);
                cfg.instance.transform.localScale = scale;
                cfg.instance.transform.SetSiblingIndex(sibling);
                cfg.instance.name = objectName;
                Undo.DestroyObjectImmediate(damaged.gameObject);
            }

            // The bubble prefab also owns Visual and InnerBoundary. Clearing the
            // whole instance removes its shell, which was why only tiny fruits and
            // collider gizmos remained in the Scene view.
            Transform fruitRoot = cfg.instance.transform.Find("FruitRoot");
            if (fruitRoot == null)
            {
                GameObject rootObject = new GameObject("FruitRoot");
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Fruit Root");
                fruitRoot = rootObject.transform;
                fruitRoot.SetParent(cfg.instance.transform, false);
            }

            while (fruitRoot.childCount > 0)
                Undo.DestroyObjectImmediate(fruitRoot.GetChild(fruitRoot.childCount - 1).gameObject);

            ApplyBubbleScale(cfg);
            float packingRadius = GetFruitContainmentRadius(cfg.instance) * InitialPackingRatio;
            List<FruitActor> createdFruits = new List<FruitActor>(cfg.fruits.Count);
            for (int i = 0; i < cfg.fruits.Count; i++)
            {
                FruitType type = cfg.fruits[i];
                FruitActor fruit = (FruitActor)PrefabUtility.InstantiatePrefab(fruitPrefab, fruitRoot);
                fruit.gameObject.name = $"Fruit_{type}_{i+1}";
                fruit.Configure(type, GetColorFor(type));
                
                if (fruitSprites != null && fruitSprites.Length > (int)type && fruitSprites[(int)type] != null)
                    fruit.SetSprite(fruitSprites[(int)type]);

                // Golden-angle packing remains tidy for any editable fruit count.
                float angle = i * 2.399963f;
                float radius = Mathf.Sqrt((i + 0.5f) / Mathf.Max(1f, cfg.fruits.Count)) * packingRadius;
                fruit.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.5f);
                if (cfg.fruitScales != null && i < cfg.fruitScales.Count && cfg.fruitScales[i] != Vector3.zero)
                    fruit.transform.localScale = cfg.fruitScales[i];
                createdFruits.Add(fruit);
            }

            cfg.instance.ReplaceFruits(createdFruits);
            BubbleFruitMotion motion = cfg.instance.GetComponent<BubbleFruitMotion>();
            if (motion != null)
            {
                float containmentRadius = GetFruitContainmentRadius(cfg.instance);
                motion.Configure(createdFruits.ToArray(), cfg.instance.GetInstanceID() % 2 == 0 ? 1 : -1,
                    Mathf.Abs(cfg.instance.GetInstanceID()) * 0.0137f, containmentRadius);
            }
            EditorUtility.SetDirty(cfg.instance);
        }

        private static float GetFruitContainmentRadius(BubbleActor bubble)
        {
            // BubbleFruitMotion constrains fruit centres, so reserve at least one
            // visible fruit radius plus a small highlight margin at the rim.
            if (bubble != null && bubble.ObstacleCollider is CircleCollider2D circle)
                return Mathf.Max(0.35f, circle.radius - FruitRimClearance);
            return 1.2f;
        }

        public void UpdateBoxVisuals(BoxConfig cfg)
        {
            if (cfg.instance == null || box4Prefab == null) return;
            Vector3 pos = cfg.instance.transform.position;
            Quaternion rotation = cfg.instance.transform.rotation;
            Vector3 scale = cfg.instance.transform.localScale;
            int siblingIndex = cfg.instance.transform.GetSiblingIndex();
            string objName = cfg.instance.gameObject.name;
            Undo.DestroyObjectImmediate(cfg.instance.gameObject);
            
            cfg.capacity = 4;
            cfg.instance = (BoxView)PrefabUtility.InstantiatePrefab(box4Prefab, boxContainer);
            cfg.instance.transform.position = pos;
            cfg.instance.transform.rotation = rotation;
            cfg.instance.transform.localScale = scale;
            cfg.instance.transform.SetSiblingIndex(Mathf.Min(siblingIndex, boxContainer.childCount - 1));
            cfg.instance.gameObject.name = objName;
            cfg.instance.Configure(cfg.fruitType, cfg.capacity, GetColorFor(cfg.fruitType));
        }
#endif

        private Color GetColorFor(FruitType type)
        {
            int index = (int)type;
            if (fruitMaterials != null && index >= 0 && index < fruitMaterials.Length && fruitMaterials[index] != null)
            {
                Material palette = fruitMaterials[index];
                if (palette.HasProperty("_BaseColor")) return palette.GetColor("_BaseColor");
                if (palette.HasProperty("_Color")) return palette.GetColor("_Color");
            }
            switch (type)
            {
                case FruitType.Apple: return new Color(0f, 0.46f, 1f);
                case FruitType.Orange: return new Color(1f, 0.52f, 0.08f);
                case FruitType.Grape: return new Color(0.58f, 0.24f, 0.88f);
                case FruitType.Lemon: return new Color(1f, 0.84f, 0.12f);
                case FruitType.Strawberry: return new Color(1f, 0.34f, 0.52f);
                default: return Color.white;
            }
        }
    }
}
