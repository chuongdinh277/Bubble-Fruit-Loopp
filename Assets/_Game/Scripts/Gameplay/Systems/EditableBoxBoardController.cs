using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Managers;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Gameplay
{
    [DefaultExecutionOrder(200)]
    public sealed partial class EditableBoxBoardController : MonoBehaviour
    {
        private sealed class Column
        {
            public readonly Queue<BoxView> Queue = new();
            public readonly List<Vector3> RowPositions = new();
            public BoxView Active;
            public BoxView Prepared;
            public Vector3 ActivePosition;
            public float PickupDistance;
        }

        private readonly List<FruitActor> loopFruits = new(40);
        private readonly Dictionary<FruitActor, float> previousDistances = new(40);
        private readonly Dictionary<BoxRuntime, BoxView> views = new();
        private static Material fruitFlightTrailMaterial;
        private const float PickupCatchDistance = 0.24f;
        private const int FlightFruitOrder = 80;
        private const float AuthoredColumnSpacing = 1.65f;
        private const float AuthoredFirstRowY = -2.45f;
        private const float AuthoredRowSpacing = 1.72f;
        private static Material celebrationTrailMaterial;
        private EditableFruitLoopController loop;
        private Column[] columns;
        private bool winCelebrated;
        private bool gameEnded;
        private int boxesExiting;
        private float lossCheckElapsed;

        [Header("Editable scene layout")]
        [SerializeField] private BoxAssignmentTable assignmentTable;
        [SerializeField] private BoxView[] layoutBoxes;
        [SerializeField] private Transform[] columnPickupPoints;
        [SerializeField, Min(0f)] private float lossDisplayDelay = 2f;

        [Header("Win celebration assets")]
        [SerializeField] private GameObject winBurstPrefab;
        [SerializeField] private GameObject winDirectionalConfettiPrefab;
        [SerializeField] private GameObject winFlashPrefab;
        [SerializeField, Min(0.1f)] private float winFxScale = 1.35f;

        public void ConfigureSceneLayout(BoxView[] boxes) => layoutBoxes = boxes;
        public void ConfigureSceneLayout(BoxView[] boxes, Transform[] pickupPoints)
        {
            layoutBoxes = boxes;
            columnPickupPoints = pickupPoints;
        }
        public void ConfigureAssignmentTable(BoxAssignmentTable table) => assignmentTable = table;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForEditableLoop()
        {
            EditableFruitLoopController editable = FindFirstObjectByType<EditableFruitLoopController>();
            if (editable == null || editable.GetComponent<EditableBoxBoardController>() != null) return;
            editable.gameObject.AddComponent<EditableBoxBoardController>();
        }

        private void Awake() => loop = GetComponent<EditableFruitLoopController>();

        private IEnumerator Start()
        {
            // Allow the editable path to finish rebuilding before pickup distances are sampled.
            yield return null;
            if (loop == null || loop.PathLength <= 0f) yield break;
            BuildBoard();
        }

        private void Update()
        {
            if (columns == null || loop == null || gameEnded) return;
            loop.CopyLoopFruits(loopFruits);
            for (int fruitIndex = 0; fruitIndex < loopFruits.Count; fruitIndex++)
            {
                FruitActor fruit = loopFruits[fruitIndex];
                float current = fruit.PathDistance;
                float previous = previousDistances.TryGetValue(fruit, out float value) ? value : current;

                // Resolve exactly one destination. Scanning from right to left
                // makes the rightmost matching active box authoritative; a full
                // or closing right box also blocks matching boxes to its left
                // until its replacement has been promoted.
                int targetColumnIndex = FindPriorityColumn(fruit.Type);
                if (targetColumnIndex >= 0)
                {
                    Column targetColumn = columns[targetColumnIndex];
                    if (ReachedPickup(previous, current, targetColumn.PickupDistance))
                        TryCollect(fruit, targetColumn, targetColumnIndex);
                }

                if (fruit.State == FruitState.OnLoop) previousDistances[fruit] = current;
                else previousDistances.Remove(fruit);
            }

            UpdateLossCondition();
        }

        private void UpdateLossCondition()
        {
            if (!loop.IsFull || loopFruits.Count != loop.Count)
            {
                lossCheckElapsed = 0f;
                return;
            }

            for (int index = 0; index < loopFruits.Count; index++)
                if (FindPriorityColumn(loopFruits[index].Type) >= 0)
                {
                    lossCheckElapsed = 0f;
                    return;
                }

            lossCheckElapsed += Time.deltaTime;
            if (lossCheckElapsed < lossDisplayDelay) return;
            gameEnded = true;
            UICanvasLose.Show();
        }

        private bool TryCollect(FruitActor fruit, Column column, int columnIndex)
        {
            BoxView box = column.Active;
            if (box == null || !box.Runtime.TryReserve(fruit.Type, out int slotIndex)) return false;
            
            if (!IsHighestPriorityBox(fruit.Type, columnIndex))
            {
                box.Runtime.CancelReservation(slotIndex);
                return false;
            }

            if (!loop.ReserveFruitForBox(fruit))
            {
                box.Runtime.CancelReservation(slotIndex);
                return false;
            }

            SoundManager.TryPlayFX(FxID.FruitCollect);
            StartCoroutine(FlyFruitToBox(fruit, box, slotIndex, column));
            return true;
        }

        private bool Crossed(float previous, float current, float pickup)
        {
            float length = loop.PathLength;
            if (length <= 0f) return false;
            
            float delta = Mathf.Repeat(current - previous + length * 0.5f, length) - length * 0.5f;
            if (delta <= 0f) return false; // Ignore backward movement from collision resolution

            float distanceToPickup = Mathf.Repeat(pickup - previous, length);
            return distanceToPickup > 0f && distanceToPickup <= delta;
        }

        private static void DeactivateCollectedFruit(FruitActor fruit)
        {
            if (fruit != null) fruit.gameObject.SetActive(false);
        }

        private static Color ColorFor(FruitType type) => type switch
        {
            FruitType.Apple => new Color(0f, 0.46160913f, 1f),
            FruitType.Orange => new Color(1f, 0.45f, 0.05f),
            FruitType.Grape => new Color(0.55f, 0.18f, 0.85f),
            FruitType.Lemon => new Color(0.95f, 0.88f, 0.08f),
            FruitType.Strawberry => new Color(1f, 0.4292453f, 0.4292453f),
            _ => Color.white
        };
    }
}

