#if UNITY_EDITOR
using System.Collections.Generic;
using BubbleFruitLoop.Gameplay;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubbleFruitLoop.Editor
{
    public sealed class BubbleFruitLevelDesignerWindow : OdinEditorWindow
    {
        private const string DefaultLevelPath = "Assets/_Game/Levels/Level_Test.asset";
        private const string BubblePrefabPath = "Assets/_Game/Resources/Prefabs/BubbleAsset2D.prefab";
        private const string BoxLayoutName = "BOX MODELS (EDIT LAYOUT)";

        [MenuItem("BubbleFruit/Level Designer")]
        private static void OpenWindow()
        {
            GetWindow<BubbleFruitLevelDesignerWindow>("Bubble Fruit Level Designer").Show();
        }

        [Title("Level Asset", "Một asset chứa toàn bộ bubble, fruit và box của level")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        [AssetsOnly]
        public BubbleFruitLevelDefinition level;

        [Title("Live Scene Preview")]
        [ToggleLeft, LabelText("Tự cập nhật Scene khi sửa bubble/fruit")]
        public bool livePreview = true;

        [ToggleLeft, LabelText("Tự tạo Box4 từ số fruit")]
        public bool autoCreateBoxesFromFruits = true;

        private int lastBubbleHash;
        private int lastBoxHash;
        private int knownBubbleCount;
        private bool liveApplyQueued;
        private double nextPreviewRepaint;

        [HorizontalGroup("Totals")]
        [ShowInInspector, ReadOnly, LabelText("Tổng fruit")]
        private int FruitTotal => CountFruits();

        [HorizontalGroup("Totals")]
        [ShowInInspector, ReadOnly, LabelText("Tổng ô box")]
        private int BoxCapacityTotal => CountBoxCapacity();

        [ShowInInspector, ReadOnly, MultiLineProperty(4)]
        [GUIColor(nameof(ValidationColor))]
        [LabelText("Kiểm tra level")]
        private string Validation => BuildValidationMessage();

        protected override void OnEnable()
        {
            base.OnEnable();
            if (level == null) level = AssetDatabase.LoadAssetAtPath<BubbleFruitLevelDefinition>(DefaultLevelPath);
            NormalizeAllBoxesToBox4();
            knownBubbleCount = level != null ? level.bubbles.Count : 0;
            lastBubbleHash = ComputeBubbleHash();
            lastBoxHash = ComputeBoxHash();
            EditorApplication.update += PreviewUpdate;
        }

        protected override void OnDestroy()
        {
            EditorApplication.update -= PreviewUpdate;
            base.OnDestroy();
        }

        [Button("MỞ CỬA SỔ LEVEL PREVIEW RIÊNG", ButtonSizes.Gigantic), PropertyOrder(-100)]
        [GUIColor(0.25f, 0.72f, 1f)]
        private void OpenSeparatePreview()
        {
            BubbleFruitLevelPreviewWindow.OpenWindow();
        }

        [Button("Tạo level test mặc định", ButtonSizes.Large), GUIColor(0.35f, 0.8f, 1f)]
        private void CreateDefaultLevel()
        {
            EnsureLevelAsset();
            level.boxes.Clear();
            AddBox(FruitType.Orange, 4, 0);
            AddBox(FruitType.Strawberry, 4, 1);
            AddBox(FruitType.Orange, 4, 2);
            AddBox(FruitType.Strawberry, 4, 0);
            AddBox(FruitType.Orange, 4, 1);
            AddBox(FruitType.Strawberry, 4, 2);
            BalanceFruitsToBoxes();
        }

        [Button("Tự tạo fruit đúng theo box", ButtonSizes.Large), GUIColor(0.45f, 1f, 0.55f)]
        private void BalanceFruitsToBoxes()
        {
            if (level == null) return;
            List<FruitType> required = new();
            for (int index = 0; index < level.boxes.Count; index++)
            {
                BubbleFruitLevelDefinition.BoxSetup box = level.boxes[index];
                int capacity = (int)box.capacity;
                for (int count = 0; count < capacity; count++) required.Add(box.fruitType);
            }

            level.bubbles.Clear();
            const int fruitsPerBubble = 8;
            for (int start = 0; start < required.Count; start += fruitsPerBubble)
            {
                int bubbleIndex = level.bubbles.Count;
                BubbleFruitLevelDefinition.BubbleSetup bubble = new()
                {
                    position = DefaultBubblePosition(bubbleIndex)
                };
                int end = Mathf.Min(start + fruitsPerBubble, required.Count);
                for (int index = start; index < end; index++) bubble.fruits.Add(required[index]);
                level.bubbles.Add(bubble);
            }
            SaveLevel();
            QueueLiveApply();
        }

        [Button("Tạo lại box từ fruit hiện tại", ButtonSizes.Large), GUIColor(1f, 0.72f, 0.2f)]
        private void RebuildBoxesFromFruitCounts()
        {
            if (level == null || !TrySyncBoxesFromFruits()) return;
            SaveLevel();
            QueueLiveApply();
        }

        [Button("VALIDATE + APPLY VÀO SAMPLE SCENE", ButtonSizes.Gigantic), GUIColor(0.2f, 1f, 0.35f)]
        [EnableIf(nameof(IsValid))]
        private void ApplyToScene()
        {
            if (!IsValid || level == null) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "SampleScene")
                scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

            RebuildBubbles();
            RebuildBoxes();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SaveLevel();
            Debug.Log($"Applied {level.name}: {FruitTotal} fruits, {level.boxes.Count} boxes.");
            lastBubbleHash = ComputeBubbleHash();
            lastBoxHash = ComputeBoxHash();
        }

        [Button("Chỉ lưu asset")]
        private void SaveLevel()
        {
            if (level == null) return;
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private bool IsValid => level != null && AreTypesSupported() && FruitTotal == BoxCapacityTotal &&
                                BuildTypeCounts(true).Count == 0;
        private Color ValidationColor => IsValid ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.42f, 0.35f);

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

        private void RebuildBubbles()
        {
            GameObject containerObject = GameObject.Find("BubbleContainer");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
            if (containerObject == null || prefab == null)
                throw new MissingReferenceException("BubbleContainer hoặc BubbleAsset2D.prefab không tồn tại.");

            ClearChildren(containerObject.transform);
            for (int index = 0; index < level.bubbles.Count; index++)
            {
                BubbleFruitLevelDefinition.BubbleSetup setup = level.bubbles[index];
                GameObject bubble = (GameObject)PrefabUtility.InstantiatePrefab(prefab, containerObject.transform);
                bubble.name = $"Bubble_Level_{index + 1:00}";
                bubble.transform.position = setup.position;
                ConfigureBubbleFruits(bubble, setup.fruits, index);
            }
        }

        private static void ConfigureBubbleFruits(GameObject bubble, List<FruitType> types, int seed)
        {
            Transform fruitRoot = bubble.transform.Find("FruitRoot");
            BubbleActor actor = bubble.GetComponent<BubbleActor>();
            BubbleFruitMotion motion = bubble.GetComponent<BubbleFruitMotion>();
            if (fruitRoot == null || actor == null || motion == null) return;
            ClearChildren(fruitRoot);

            List<FruitActor> fruits = new();
            for (int index = 0; index < types.Count; index++)
                fruits.Add(CreateFruit(fruitRoot, types[index], index, types.Count));
            actor.ReplaceFruits(fruits);
            motion.Configure(fruits.ToArray(), seed % 2 == 0 ? 1 : -1, 13.7f + seed);
        }

        private static FruitActor CreateFruit(Transform parent, FruitType type, int index, int count)
        {
            Sprite sprite = SpriteFor(type);
            GameObject fruitObject = new($"{type}_{index + 1:00}");
            fruitObject.transform.SetParent(parent, false);
            float angle = index * 2.399963f;
            float radius = Mathf.Sqrt((index + 0.5f) / Mathf.Max(1f, count)) * 1.35f;
            fruitObject.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            SpriteRenderer renderer = fruitObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 11;
            fruitObject.transform.localScale = Vector3.one * (0.9f / sprite.bounds.size.x);
            Rigidbody2D body = fruitObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 1.2f;
            body.angularDamping = 0.7f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = fruitObject.AddComponent<CircleCollider2D>();
            collider.radius = sprite.bounds.extents.x * 0.76f;
            FruitActor actor = fruitObject.AddComponent<FruitActor>();
            actor.Initialize(body, collider, renderer);
            actor.Configure(type, Color.white);
            actor.OnSpawned();
            return actor;
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
            EditableFruitLoopController loop = Object.FindFirstObjectByType<EditableFruitLoopController>();
            EditableBoxBoardController board = loop.GetComponent<EditableBoxBoardController>();
            if (board == null) board = loop.gameObject.AddComponent<EditableBoxBoardController>();
            board.ConfigureAssignmentTable(table);
            board.ConfigureSceneLayout(views.ToArray(), points);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(board);
            Selection.activeGameObject = root;
        }

        private void EnsureLevelAsset()
        {
            if (level != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Levels"))
                AssetDatabase.CreateFolder("Assets/_Game", "Levels");
            level = CreateInstance<BubbleFruitLevelDefinition>();
            AssetDatabase.CreateAsset(level, DefaultLevelPath);
        }

        private void NormalizeAllBoxesToBox4()
        {
            if (level == null) return;
            bool changed = false;
            for (int index = 0; index < level.boxes.Count; index++)
            {
                if (level.boxes[index].capacity == BubbleFruitLevelDefinition.BoxCapacity.Box4) continue;
                level.boxes[index].capacity = BubbleFruitLevelDefinition.BoxCapacity.Box4;
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(level);
        }

        private void AddBox(FruitType type, int capacity, int column) => level.boxes.Add(
            new BubbleFruitLevelDefinition.BoxSetup
            {
                fruitType = type,
                capacity = BubbleFruitLevelDefinition.BoxCapacity.Box4,
                column = column
            });
        private int CountFruits()
        {
            int total = 0;
            if (level != null) for (int index = 0; index < level.bubbles.Count; index++) total += level.bubbles[index].fruits.Count;
            return total;
        }
        private int CountBoxCapacity()
        {
            int total = 0;
            if (level != null) for (int index = 0; index < level.boxes.Count; index++) total += (int)level.boxes[index].capacity;
            return total;
        }
        private bool AreTypesSupported()
        {
            if (level == null) return false;
            for (int bubble = 0; bubble < level.bubbles.Count; bubble++)
            for (int fruit = 0; fruit < level.bubbles[bubble].fruits.Count; fruit++)
                if (level.bubbles[bubble].fruits[fruit] is not (FruitType.Orange or FruitType.Strawberry)) return false;
            for (int index = 0; index < level.boxes.Count; index++)
                if (level.boxes[index].fruitType is not (FruitType.Orange or FruitType.Strawberry)) return false;
            return true;
        }

        private void PreviewUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextPreviewRepaint) return;
            nextPreviewRepaint = EditorApplication.timeSinceStartup + 0.1d;
            WatchForLiveChanges();
            Repaint();
        }

        private void WatchForLiveChanges()
        {
            if (!livePreview || level == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            int bubbleHash = ComputeBubbleHash();
            int boxHash = ComputeBoxHash();
            if (bubbleHash == lastBubbleHash && boxHash == lastBoxHash) return;

            bool bubblesChanged = bubbleHash != lastBubbleHash;
            lastBubbleHash = bubbleHash;
            lastBoxHash = boxHash;
            if (bubblesChanged)
            {
                InitializeNewBubbles();
                if (autoCreateBoxesFromFruits && !TrySyncBoxesFromFruits()) return;
            }
            QueueLiveApply();
        }

        private void QueueLiveApply()
        {
            if (liveApplyQueued) return;
            liveApplyQueued = true;
            EditorApplication.delayCall += () =>
            {
                liveApplyQueued = false;
                if (this == null || level == null) return;
                SaveLevel();
                if (IsValid) ApplyToScene();
                Repaint();
            };
        }

        private void InitializeNewBubbles()
        {
            if (level.bubbles.Count > knownBubbleCount)
            {
                for (int index = knownBubbleCount; index < level.bubbles.Count; index++)
                {
                    BubbleFruitLevelDefinition.BubbleSetup bubble = level.bubbles[index];
                    bubble.position = DefaultBubblePosition(index);
                    if (bubble.fruits.Count == 0)
                    {
                        for (int fruit = 0; fruit < 8; fruit++)
                            bubble.fruits.Add(fruit % 2 == 0 ? FruitType.Orange : FruitType.Strawberry);
                    }
                }
            }
            knownBubbleCount = level.bubbles.Count;
        }

        private bool TrySyncBoxesFromFruits()
        {
            Dictionary<FruitType, int> counts = new();
            for (int bubble = 0; bubble < level.bubbles.Count; bubble++)
            for (int fruit = 0; fruit < level.bubbles[bubble].fruits.Count; fruit++)
            {
                FruitType type = level.bubbles[bubble].fruits[fruit];
                counts[type] = counts.TryGetValue(type, out int value) ? value + 1 : 1;
            }

            List<BubbleFruitLevelDefinition.BoxSetup> generated = new();
            int nextColumn = 0;
            foreach (KeyValuePair<FruitType, int> item in counts)
            {
                if (item.Key is not (FruitType.Orange or FruitType.Strawberry)) return false;
                int remaining = item.Value;
                if (remaining % 4 != 0) return false;
                while (remaining > 0)
                {
                    const int capacity = 4;
                    generated.Add(new BubbleFruitLevelDefinition.BoxSetup
                    {
                        fruitType = item.Key,
                        capacity = (BubbleFruitLevelDefinition.BoxCapacity)capacity,
                        column = nextColumn++ % 3
                    });
                    remaining -= capacity;
                }
            }
            level.boxes = generated;
            lastBoxHash = ComputeBoxHash();
            return true;
        }

        private int ComputeBubbleHash()
        {
            if (level == null) return 0;
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < level.bubbles.Count; index++)
                {
                    BubbleFruitLevelDefinition.BubbleSetup bubble = level.bubbles[index];
                    hash = hash * 31 + bubble.position.GetHashCode();
                    hash = hash * 31 + bubble.fruits.Count;
                    for (int fruit = 0; fruit < bubble.fruits.Count; fruit++) hash = hash * 31 + (int)bubble.fruits[fruit];
                }
                return hash;
            }
        }

        private int ComputeBoxHash()
        {
            if (level == null) return 0;
            unchecked
            {
                int hash = 23;
                for (int index = 0; index < level.boxes.Count; index++)
                {
                    BubbleFruitLevelDefinition.BoxSetup box = level.boxes[index];
                    hash = hash * 31 + (int)box.fruitType;
                    hash = hash * 31 + (int)box.capacity;
                    hash = hash * 31 + box.column;
                }
                return hash;
            }
        }
        private static Vector2 DefaultBubblePosition(int index) => new(
            -2.6f + index % 3 * 2.6f, 5.5f + index / 3 * 2.65f);
        private static Transform Find(string name) => GameObject.Find(name)?.transform;
        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--) DestroyImmediate(parent.GetChild(index).gameObject);
        }
        private static Sprite SpriteFor(FruitType type)
        {
            string file = type == FruitType.Orange ? "quacam-removebg-preview.png" : "quadau-removebg-preview.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Texture/Gameplay/{file}");
        }
        private static Color BoxColor(FruitType type) => type == FruitType.Orange
            ? new Color(1f, 0.52f, 0.08f) : new Color(1f, 0.34f, 0.52f);
    }
}
#endif
