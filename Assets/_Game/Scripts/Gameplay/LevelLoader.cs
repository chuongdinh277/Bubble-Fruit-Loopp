using UnityEngine;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Managers;

namespace BubbleFruitLoop.Gameplay
{
    [DefaultExecutionOrder(-1000)]
    public class LevelLoader : MonoBehaviour
    {
        private const float InnerBoundaryInset = 0.90f;
        private const float FruitRimClearance = 0.52f;
        private const float InitialPackingRatio = 0.76f;
        private const float BoxColumnSpacing = 1.65f;
        private const float BoxFirstRowY = -2.45f;
        private const float BoxRowSpacing = 1.72f;
        private const float BoxDisplayScale = 1.12f;

        public bool loadOnAwake = true;
        public LevelData currentLevel;
        public BubbleActor bubblePrefab;
        public FruitActor fruitPrefab;
        public BoxView box4Prefab;
        public Material[] fruitMaterials;
        public Sprite[] fruitSprites;
        public Transform bubbleContainer;
        public Transform boxContainer;

        private void Awake()
        {
            ResolveReferences();
            SelectCurrentTestLevel();
            if (loadOnAwake) LoadLevel(currentLevel);
        }

        private void SelectCurrentTestLevel()
        {
            int levelNumber = DataManager.Instance != null ? DataManager.Instance.GetLevel() : 1;
            levelNumber = levelNumber == 2 ? 2 : 1;
            LevelData selected = Resources.Load<LevelData>($"Levels/Level_{levelNumber:00}");
            if (selected != null) currentLevel = selected;
        }

        private void ResolveReferences()
        {
            if (currentLevel == null) currentLevel = Resources.Load<LevelData>("Levels/Level_01");
            if (box4Prefab == null) box4Prefab = Resources.Load<BoxView>("Box4");

            if (bubbleContainer == null)
                bubbleContainer = GameObject.Find("BubbleContainer")?.transform;
            if (boxContainer == null)
                boxContainer = GameObject.Find("BoxContainer")?.transform;

            if (boxContainer == null)
            {
                GameObject container = new("BoxContainer");
                boxContainer = container.transform;
                boxContainer.SetParent(transform, false);
                boxContainer.gameObject.AddComponent<BoxAssignmentTable>();
            }
        }

        public void LoadLevel(LevelData data)
        {
            if (data == null) return;
            currentLevel = data;
            ResolveReferences();
            if (bubbleContainer == null || boxContainer == null)
            {
                Debug.LogError("LevelLoader could not create the Level 1 scene containers.", this);
                return;
            }

            // Clear
            foreach (Transform t in bubbleContainer)
            {
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
            ClearRuntimeChildren(boxContainer);

            // Spawn Bubbles
            foreach (var b in data.bubbles)
            {
                BubbleActor bubble = Instantiate(bubblePrefab, bubbleContainer);
                bubble.transform.position = b.position;
                float bubbleScale = b.bubbleScale > 0.1f ? b.bubbleScale : 0.4f;
                // Resize only the shell. Scaling the BubbleActor root would also
                // resize FruitRoot and make identical fruits differ per bubble.
                Transform bubbleVisual = bubble.transform.Find("Visual");
                float rootScale = Mathf.Max(0.0001f, Mathf.Abs(bubble.transform.localScale.x));
                float shellMultiplier = bubbleScale / rootScale;
                if (bubbleVisual != null)
                {
                    Transform prefabVisual = bubblePrefab.transform.Find("Visual");
                    bubbleVisual.localScale = (prefabVisual != null ? prefabVisual.localScale : Vector3.one) * shellMultiplier;
                }
                Transform boundary = bubble.transform.Find("InnerBoundary");
                Transform prefabBoundary = bubblePrefab.transform.Find("InnerBoundary");
                if (boundary != null)
                    boundary.localScale = (prefabBoundary != null ? prefabBoundary.localScale : Vector3.one)
                        * (shellMultiplier * InnerBoundaryInset);

                if (bubble.ObstacleCollider is CircleCollider2D circle &&
                    bubblePrefab.ObstacleCollider is CircleCollider2D prefabCircle)
                    circle.radius = prefabCircle.radius * shellMultiplier;
                Transform fruitRoot = bubble.transform.Find("FruitRoot");
                if (fruitRoot == null)
                {
                    fruitRoot = new GameObject("FruitRoot").transform;
                    fruitRoot.SetParent(bubble.transform, false);
                }
                foreach (Transform oldFruit in fruitRoot)
                {
                    oldFruit.gameObject.SetActive(false);
                    Destroy(oldFruit.gameObject);
                }

                float packingRadius = bubble.ObstacleCollider is CircleCollider2D packingCircle
                    ? Mathf.Max(0.35f, packingCircle.radius - FruitRimClearance) * InitialPackingRatio
                    : 1f;
                System.Collections.Generic.List<FruitActor> created = new(b.fruits.Count);
                for (int i = 0; i < b.fruits.Count; i++)
                {
                    FruitType type = b.fruits[i];
                    FruitActor fruit = Instantiate(fruitPrefab, fruitRoot);
                    fruit.name = $"Fruit_{type}_{i + 1}";
                    fruit.Configure(type, GetColorFor(type));
                    
                    if (fruitSprites != null && fruitSprites.Length > (int)type && fruitSprites[(int)type] != null)
                        fruit.SetSprite(fruitSprites[(int)type]);
                    
                    float angle = i * 2.399963f;
                    float radius = Mathf.Sqrt((i + 0.5f) / Mathf.Max(1f, b.fruits.Count)) * packingRadius;
                    fruit.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.5f);
                    if (b.fruitScales != null && i < b.fruitScales.Count && b.fruitScales[i] != Vector3.zero)
                        fruit.transform.localScale = b.fruitScales[i];
                    created.Add(fruit);
                }
                bubble.ReplaceFruits(created);
                BubbleFruitMotion motion = bubble.GetComponent<BubbleFruitMotion>();
                if (motion != null) motion.Configure(created.ToArray(), bubble.GetInstanceID() % 2 == 0 ? 1 : -1,
                    Mathf.Abs(bubble.GetInstanceID()) * 0.0137f,
                    bubble.ObstacleCollider is CircleCollider2D bubbleCircle
                        ? Mathf.Max(0.35f, bubbleCircle.radius - FruitRimClearance)
                        : 1.2f);
            }

            // Every level uses the same runtime BoxContainer. The old authored
            // layout is legacy-only and must never participate in gameplay.
            GameObject authoredLayout = GameObject.Find("BOX MODELS (EDIT LAYOUT)");
            if (authoredLayout != null) authoredLayout.SetActive(false);
            boxContainer.gameObject.SetActive(true);
            System.Collections.Generic.List<BoxView> spawnedBoxes = new(data.boxes.Count);
            System.Collections.Generic.List<FruitType> spawnedTypes = new(data.boxes.Count);
            System.Collections.Generic.List<int> columns = new(data.boxes.Count);
            System.Collections.Generic.List<int> queueOrders = new(data.boxes.Count);
            int[] nextQueueOrder = new int[3];
            BoxAssignmentTable assignmentTable = boxContainer.GetComponent<BoxAssignmentTable>();
            if (assignmentTable == null)
                assignmentTable = boxContainer.gameObject.AddComponent<BoxAssignmentTable>();
            if (assignmentTable != null) assignmentTable.ConfigurePalette(fruitMaterials);
            Transform[] pickupPoints = new Transform[3];
            for (int column = 0; column < pickupPoints.Length; column++)
            {
                pickupPoints[column] = assignmentTable.GetPickupPoint(column);
                if (pickupPoints[column] == null)
                    pickupPoints[column] = GameObject.Find($"P{column + 1}")?.transform;
            }

            foreach (var b in data.boxes)
            {
                BoxView box = Instantiate(box4Prefab, boxContainer);
                int column = data.boxLayoutVersion > 0
                    ? Mathf.Clamp(b.column, 0, 2)
                    : ClosestColumn(b.position.x, pickupPoints);
                int queueOrder = data.boxLayoutVersion > 0
                    ? Mathf.Max(0, b.queueOrder)
                    : nextQueueOrder[column];
                box.transform.position = new Vector3(
                    (1 - column) * BoxColumnSpacing,
                    BoxFirstRowY - queueOrder * BoxRowSpacing,
                    0f);
                box.transform.localScale = box4Prefab.transform.localScale * BoxDisplayScale;
                box.Configure(b.fruitType, 4, GetColorFor(b.fruitType));
                box.SetupRuntime(); // Initialize BoxRuntime logic
                spawnedBoxes.Add(box);
                spawnedTypes.Add(b.fruitType);
                columns.Add(column);
                queueOrders.Add(queueOrder);
                nextQueueOrder[column] = Mathf.Max(nextQueueOrder[column], queueOrder + 1);
            }

            if (assignmentTable != null)
                assignmentTable.Configure(spawnedBoxes.ToArray(), spawnedTypes.ToArray(), columns.ToArray(),
                    queueOrders.ToArray(), pickupPoints);

            EditableBoxBoardController board = FindFirstObjectByType<EditableBoxBoardController>();
            if (board != null)
            {
                board.ConfigureAssignmentTable(assignmentTable);
                board.ConfigureSceneLayout(spawnedBoxes.ToArray(), pickupPoints);
            }
        }

        private static void ClearRuntimeChildren(Transform container)
        {
            if (container == null) return;
            // Detach first so newly spawned objects can never share a hierarchy
            // with objects pending Unity's end-of-frame Destroy.
            for (int index = container.childCount - 1; index >= 0; index--)
            {
                Transform child = container.GetChild(index);
                child.gameObject.SetActive(false);
                child.SetParent(null, true);
                Destroy(child.gameObject);
            }
        }

        private static int ClosestColumn(float x, Transform[] pickupPoints)
        {
            int best = 0;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < 3; index++)
            {
                float columnX = pickupPoints != null && index < pickupPoints.Length && pickupPoints[index] != null
                    ? pickupPoints[index].position.x
                    : -1.5f + index * 1.5f;
                float distance = Mathf.Abs(x - columnX);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            return best;
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
