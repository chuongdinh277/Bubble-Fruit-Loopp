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
    public sealed partial class BubbleFruitLevelDesignerWindow : OdinEditorWindow
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
