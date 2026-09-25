using System.Collections.Generic;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Managers;
using BubbleFruitLoop.Pooling;
using UnityEngine;
using UnityEngine.InputSystem;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Runtime
{
    public sealed class PrototypeBootstrap : SceneSingleton<PrototypeBootstrap>
    {
        private const int FlightFruitOrder = 80;
        [Header("Scene References")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform poolRoot;
        [SerializeField] private FruitActor fruitPrefab;
        [SerializeField] private BubbleActor bubblePrefab;
        [SerializeField] private BoxView boxPrefab;
        [SerializeField] private BubbleActor[] bubbles;
        [SerializeField] private FruitActor[] fruits;
        [SerializeField] private BoxColumnAuthoring[] columns;

        private readonly Dictionary<Collider2D, BubbleActor> bubbleByCollider = new();
        private readonly List<FruitActor> trackedFruits = new(48);
        private ComponentPool<FruitActor> fruitPool;
        private ComponentPool<BubbleActor> bubblePool;
        private ComponentPool<BoxView> boxPool;
        private FruitLoopManager loop;
        private FunnelIntakeManager intake;
        private PickupSystem pickup;
        private GameStateResolver stateResolver;
        
        private readonly Dictionary<BoxRuntime, BoxView> boxViewMap = new();

        public void ConfigureScene(Camera cameraReference, Transform poolContainer, FruitActor fruitTemplate,
            BubbleActor bubbleTemplate, BoxView boxTemplate, BubbleActor[] sceneBubbles,
            FruitActor[] sceneFruits, BoxColumnAuthoring[] sceneColumns)
        {
            gameplayCamera = cameraReference;
            poolRoot = poolContainer;
            fruitPrefab = fruitTemplate;
            bubblePrefab = bubbleTemplate;
            boxPrefab = boxTemplate;
            bubbles = sceneBubbles;
            fruits = sceneFruits;
            columns = sceneColumns;
        }

        protected override void OnSingletonReady()
        {
            ValidateReferences();
            InitializePools();
            BuildSystems();
            RegisterAuthoredScene();
        }

        private void Update()
        {
            HandleTap();
            MoveReleasedFruitToIntake();
            loop.Tick(Time.deltaTime);
            pickup.Tick();
            intake.Tick(Time.deltaTime);
            stateResolver.Resolve();
        }

        private void InitializePools()
        {
            fruitPool = new ComponentPool<FruitActor>(() => Instantiate(fruitPrefab), CreatePoolRoot("FruitPool"), 16);
            bubblePool = new ComponentPool<BubbleActor>(() => Instantiate(bubblePrefab), CreatePoolRoot("BubblePool"), 4);
            boxPool = new ComponentPool<BoxView>(() => Instantiate(boxPrefab), CreatePoolRoot("BoxPool"), 6);
        }

        private void BuildSystems()
        {
            GameSignals signals = new();
            LoopPathCache path = new(new Vector3(0f, -2.15f), 4.15f, 1.65f, 96);
            loop = new FruitLoopManager(path, new CapacityController(30, 5), signals, 2.5f);
            intake = new FunnelIntakeManager(loop);
            BoxBoardManager board = BuildBoxBoard();
            MatchResolver matches = new(loop, board, signals);
            pickup = new PickupSystem(loop, matches, board);
            stateResolver = new GameStateResolver(loop, board, matches, signals);
            signals.LoopSlotReleased += OnLoopSlotReleased;
            signals.BoxCompleted += OnBoxCompleted;
            signals.FruitCollectedToBox += OnFruitCollectedToBox;
        }

        private void OnBoxCompleted(BoxRuntime boxRuntime)
        {
            // Add coins when a box completes via DataManager
            if (DataManager.Instance != null && UIManager.Instance != null && UIManager.Instance.IsLoaded<UICanvasGameplay>())
            {
                DataManager.Instance.AddCoin(10);
                UIManager.Instance.GetUI<UICanvasGameplay>().UpdateCoin(DataManager.Instance.GetCoin());
            }

            if (boxViewMap.TryGetValue(boxRuntime, out BoxView view))
            {
                boxViewMap.Remove(boxRuntime);
                view.PlayCompleteAndExit(() =>
                {
                    view.ReleaseCollectedFruits(fruitPool.Despawn);
                    boxPool.Despawn(view);
                });
            }
        }

        private void OnFruitCollectedToBox(FruitActor fruit, BoxRuntime boxRuntime)
        {
            SoundManager.TryPlayFX(FxID.FruitCollect);
            if (boxViewMap.TryGetValue(boxRuntime, out BoxView view))
            {
                int slotIndex = boxRuntime.CurrentCount - 1; // Since it was incremented before firing
                Transform slotTarget = view.GetSlotTransform(slotIndex);
                StartCoroutine(AnimateFruitToBox(fruit, view, slotTarget, slotIndex));
            }
            else
            {
                fruitPool.Despawn(fruit);
            }
        }

        private System.Collections.IEnumerator AnimateFruitToBox(FruitActor fruit, BoxView owner,
            Transform target, int slotIndex)
        {
            SpriteRenderer flightRenderer = fruit != null ? fruit.VisualSpriteRenderer : null;
            Sprite flightSprite = flightRenderer != null ? flightRenderer.sprite : null;
            int originalSortingOrder = flightRenderer != null ? flightRenderer.sortingOrder : 0;
            if (flightRenderer != null) flightRenderer.sortingOrder = FlightFruitOrder;
            fruit.DisablePhysics(); // make sure it's kinematic/disabled
            Vector3 startPos = fruit.CachedTransform.position;
            float verticalGap = Mathf.Abs(startPos.y - target.position.y);
            float arcHeight = Mathf.Max(1.15f, verticalGap * 0.35f + 0.75f);
            Vector3 control = (startPos + target.position) * 0.5f + Vector3.up * arcHeight;
            float elapsed = 0f;
            float duration = 0.34f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float inverse = 1f - eased;
                fruit.CachedTransform.position = inverse * inverse * startPos
                    + 2f * inverse * eased * control
                    + eased * eased * target.position;
                fruit.CachedTransform.Rotate(0f, 0f, 360f * Time.deltaTime);
                yield return null;
            }

            if (owner != null && owner.gameObject.activeInHierarchy)
            {
                if (!owner.DockFruit(fruit, flightRenderer, flightSprite, slotIndex)
                    && flightRenderer != null)
                    flightRenderer.sortingOrder = originalSortingOrder;
            }
            else
            {
                if (flightRenderer != null) flightRenderer.sortingOrder = originalSortingOrder;
                fruitPool.Despawn(fruit);
            }
        }

        private BoxBoardManager BuildBoxBoard()
        {
            BoxBoardManager board = new();
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                BoxColumnAuthoring source = columns[columnIndex];
                BoxColumnRuntime runtime = new(columnIndex, source.ActivePoint.position, source.PickupPoint.position);
                board.AddColumn(runtime);
                
                // Also subscribe to visual activations
                runtime.OnBoxActivated += (boxRt, pos) => {
                    if (boxViewMap.TryGetValue(boxRt, out BoxView view))
                    {
                        view.PlayPromote(pos);
                    }
                };
                
                for (int boxIndex = 0; boxIndex < source.Boxes.Length; boxIndex++)
                {
                    BoxView view = source.Boxes[boxIndex];
                    view.SetupRuntime();
                    boxViewMap[view.Runtime] = view;
                    boxPool.AdoptActive(view);
                    runtime.Enqueue(view.Runtime);
                }
            }
            return board;
        }

        private void RegisterAuthoredScene()
        {
            for (int index = 0; index < bubbles.Length; index++)
            {
                bubblePool.AdoptActive(bubbles[index]);
                bubbleByCollider[bubbles[index].ObstacleCollider] = bubbles[index];
            }

            for (int index = 0; index < fruits.Length; index++)
            {
                fruitPool.AdoptActive(fruits[index]);
                trackedFruits.Add(fruits[index]);
            }
        }

        private void HandleTap()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            Vector3 screenPosition = pointer.position.ReadValue();
            Vector3 worldPosition = gameplayCamera.ScreenToWorldPoint(screenPosition);
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
            for (int i = 0; i < hits.Length; i++)
            {
                if (bubbleByCollider.TryGetValue(hits[i], out BubbleActor bubble))
                {
                    bubble.Pop();
                    break;
                }
            }
        }
        private void MoveReleasedFruitToIntake()
        {
            for (int index = 0; index < trackedFruits.Count; index++)
            {
                FruitActor fruit = trackedFruits[index];
                if (fruit.State == FruitState.Released) intake.Submit(fruit);
            }
        }
        private Transform CreatePoolRoot(string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(poolRoot, false);
            return child;
        }

        private void OnLoopSlotReleased(FruitActor fruit) { }

        private void ValidateReferences()
        {
            if (gameplayCamera == null || poolRoot == null || fruitPrefab == null || bubblePrefab == null || boxPrefab == null)
                throw new MissingReferenceException("Prototype scene references are incomplete. Rebuild from Tools/Bubble Fruit Loop.");
        }
    }
}
