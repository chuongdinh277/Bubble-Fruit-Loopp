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
    public partial class LevelDesignManager : SerializedMonoBehaviour
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

