using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BubbleFruitLoop.Gameplay", "Assembly-CSharp", "EditableFruitLoopController")]
    public sealed partial class FruitLoopManager : MonoBehaviour
    {
        private const int MaxLoopCapacity = 30;

        [Header("Scene References")]
        [SerializeField] internal LoopPathAuthoring pathAuthoring;
        [SerializeField] private Transform loopStart;
        [SerializeField] private Transform waterfallStartPoint;

        [Header("Entry Congestion")]
        [SerializeField, Min(0.1f)] private float entryZoneWidth = 1.45f;
        [SerializeField, Min(0.1f)] private float entryZoneHeight = 1.35f;
        [SerializeField, Min(0.05f)] private float admissionRadius = 0.42f;

        [Header("Waterfall Intake")]
        [SerializeField, Range(2f, 12f)] private float intakeFlightSpeed = 7.2f;
        [SerializeField, Min(0.04f)] private float intakeLaunchInterval = 0.04f;
        [SerializeField, Range(0.1f, 0.3f)] private float intakeFallSmoothDuration = 0.20f;

        [Header("Full Loop Gate")]
        [SerializeField, Min(0.2f)] private float fullGateWidth = 1.25f;
        [SerializeField, Min(0.05f)] private float fullGateHeight = 0.18f;
        [SerializeField] private float fullGateYOffset = 0.22f;

        [Header("Stable Loop")]
        [SerializeField, Min(0.1f)] internal float speed = 5.0f;
        [SerializeField, Min(0.1f)] internal float maxComfortableLoopSpeed = 5.0f;
        [SerializeField, Range(2f, 30f)] private float laneCenteringStrength = 22f;
        [SerializeField, Range(1f, 8f)] private float physicalLoopMaxSpeed = 5.8f;

        [Header("Fruit Spacing")]
        [SerializeField, Min(0.01f)] internal float minimumFruitGap = 0.06f;
        [SerializeField, Range(0.04f, 0.3f)] private float spacingSmoothTime = 0.065f;
        [SerializeField, Range(0.1f, 0.5f)] private float maxSpacingPushSpeedRatio = 0.48f;

        [Header("Gentle Fruit Rolling")]
        [SerializeField, Range(0f, 45f)] internal float fruitRollDegreesPerSecond = 20f;

        [Header("P00 Lane Merge")]
        [SerializeField, Range(0.08f, 0.4f)] private float laneMergeDuration = 0.20f;
        [SerializeField, Range(0.1f, 1f)] private float laneMergeAdvance = 0.46f;
        [SerializeField, Range(0.05f, 0.25f)] internal float entryLiftHeight = 0.16f;
        [SerializeField, Range(0.02f, 0.15f)] internal float entryPressDepth = 0.09f;
        [SerializeField, Range(0.05f, 0.3f)] internal float entryPressureSmoothTime = 0.10f;

        private readonly List<Fruit> congestion = new(24);
        private readonly List<FruitLoopData> active = new(35);
        private readonly List<FruitLoopData> stableFruits = new(35);
        private readonly List<FruitLoopData> spacingFruits = new(35);
        private readonly HashSet<Fruit> owned = new();
        private readonly Dictionary<Fruit, FruitIntakeData> intakeFlights = new();
        internal LoopPathCache path;
        private BoxCollider2D fullGateCollider;
        private float entryDistance;
        private float intakeLaunchTimer;
        private readonly List<Fruit> arrivedFruits = new(24);

        public int Count => active.Count;
        public int Capacity => MaxLoopCapacity;
        public bool IsFull => Count >= Capacity;
        public float PathLength => path != null ? path.Length : 0f;

        public void Configure(LoopPathAuthoring authoring) => pathAuthoring = authoring;

        public void Configure(LoopPathAuthoring authoring, Transform intakePoint, int loopCapacity)
        {
            pathAuthoring = authoring;
            loopStart = intakePoint;
            RebuildPath();
        }

        private void Awake()
        {
            ApplyRuntimeTuning();
            RebuildPath();
            EnsureFullGate();
        }
        private void OnEnable()
        {
            ApplyRuntimeTuning();
            RebuildPath();
            EnsureFullGate();
        }

        private void ApplyRuntimeTuning()
        {
            // Keep current scene files compatible with the latest movement tuning.
            // This avoids having to rewrite an open .unity file just to update speed.
            // Upgrade the slower values serialized in existing scenes.
            speed = Mathf.Max(5.0f, speed);
            maxComfortableLoopSpeed = Mathf.Max(5.0f, maxComfortableLoopSpeed);
            // Apply faster, still eased separation to existing serialized scenes.
            spacingSmoothTime = Mathf.Clamp(spacingSmoothTime, 0.04f, 0.065f);
            maxSpacingPushSpeedRatio = Mathf.Clamp(maxSpacingPushSpeedRatio, 0.48f, 0.5f);
            laneCenteringStrength = Mathf.Clamp(laneCenteringStrength, 18f, 26f);
            physicalLoopMaxSpeed = Mathf.Clamp(physicalLoopMaxSpeed, 5.2f, 6.4f);
            // This zone tracks fruit at the chute outlet. Fruit above it keep
            // their own physics until they reach the end of the chute.
            entryZoneWidth = Mathf.Clamp(entryZoneWidth, 1.3f, 1.5f);
            entryZoneHeight = Mathf.Clamp(entryZoneHeight, 0.8f, 1.35f);
            intakeFlightSpeed = Mathf.Clamp(intakeFlightSpeed, 7.2f, 10f);
            intakeLaunchInterval = Mathf.Clamp(intakeLaunchInterval, 0.04f, 0.05f);
            intakeFallSmoothDuration = Mathf.Clamp(intakeFallSmoothDuration, 0.20f, 0.3f);
            laneMergeDuration = Mathf.Clamp(laneMergeDuration, 0.18f, 0.24f);
            laneMergeAdvance = Mathf.Clamp(laneMergeAdvance, 0.36f, 0.58f);
        }

        private void Update()
        {
            if (!Application.isPlaying || path == null) return;
            intakeLaunchTimer = Mathf.Max(0f, intakeLaunchTimer - Time.deltaTime);

            UpdateStableLoop();
            RefreshCongestionCandidates();
            UpdateIntakeFlights();
            UpdateLaneMerges();
            // Include fruit that completed its merge this frame before drawing the lane.
            UpdateLoopSpacingAndPoses();
        }

        private void UpdateLaneMerges()
        {
            float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            for (int index = 0; index < active.Count; index++)
            {
                FruitLoopData item = active[index];
                Fruit fruit = item.Fruit;
                if (fruit == null || fruit.State != FruitStatus.MergingToLane) continue;

                fruit.LoopData = item;
                FruitManager.ExecuteLoop(fruit, this, FruitExecutionPhase.Merge);

            }
        }



        private void FixedUpdate()
        {
            if (path == null) return;
            UpdateFullGate();
        }

        private void UpdateIntakeFlights()
        {
            arrivedFruits.Clear();
            foreach (KeyValuePair<Fruit, FruitIntakeData> pair in intakeFlights)
            {
                Fruit fruit = pair.Key;
                FruitIntakeData flight = pair.Value;
                if (fruit == null) { arrivedFruits.Add(fruit); continue; }
                fruit.IntakeData = flight;
                FruitManager.ExecuteLoop(fruit, this, FruitExecutionPhase.Intake);
                if (flight.Elapsed >= flight.Duration) arrivedFruits.Add(fruit);
            }
            // Arrivals keep moving into an upper layer while the lane yields below.
            for (int index = 0; index < arrivedFruits.Count; index++)
            {
                Fruit fruit = arrivedFruits[index];
                FruitIntakeData flight = intakeFlights[fruit];
                if (fruit != null)
                    BeginWaterfallMerge(fruit, flight,
                        Mathf.Max(0f, flight.Elapsed - flight.Duration));
                intakeFlights.Remove(fruit);
                congestion.Remove(fruit);
            }
        }

        private void RefreshCongestionCandidates()
        {
            for (int index = congestion.Count - 1; index >= 0; index--)
            {
                Fruit fruit = congestion[index];
                if (fruit == null || owned.Contains(fruit))
                {
                    intakeFlights.Remove(fruit);
                    congestion.RemoveAt(index);
                    continue;
                }
                if (intakeFlights.ContainsKey(fruit)) continue;
                if (IsInsideEntryZone(fruit.CachedTransform.position)) continue;

                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);

                congestion.RemoveAt(index);
            }

            Fruit[] fruits = FindObjectsByType<Fruit>(FindObjectsSortMode.None);
            for (int index = 0; index < fruits.Length; index++)
            {
                Fruit fruit = fruits[index];
                if (fruit == null || owned.Contains(fruit) || congestion.Contains(fruit)) continue;
                if (fruit.State != FruitStatus.Released && fruit.State != FruitStatus.EntryCongestion
                    && fruit.State != FruitStatus.WaitingFull && fruit.State != FruitStatus.IntakeWaiting
                    && fruit.State != FruitStatus.EnteringLoop && fruit.State != FruitStatus.Transient) continue;
                if (!IsInsideEntryZone(fruit.CachedTransform.position)) continue;

                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
                congestion.Add(fruit);

            }

            if (active.Count + intakeFlights.Count >= Capacity)
            {
                for (int index = 0; index < congestion.Count; index++)
                {
                    Fruit fruit = congestion[index];
                    if (fruit == null || intakeFlights.ContainsKey(fruit)) continue;
                    if (fruit.State != FruitStatus.WaitingFull)
                        fruit.EnterCongestion(true);
                    pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
                }
                return;
            }

            // Unselected fruit keep chute physics; selected fruit follow the
            // continuous waterfall trajectory through the authored Startpoint.
            for (int index = 0; index < congestion.Count; index++)
            {
                Fruit fruit = congestion[index];
                if (fruit == null || intakeFlights.ContainsKey(fruit)) continue;
                if (fruit.State == FruitStatus.WaitingFull)
                    fruit.EnterCongestion(false);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
            }

            int freeSlots = Capacity - active.Count - intakeFlights.Count;
            if (freeSlots <= 0 || intakeLaunchTimer > 0f) return;

            int nextIndex = FindNearestUnlaunchedCandidate();
            if (nextIndex < 0) return;
            Fruit nextFruit = congestion[nextIndex];
            intakeFlights[nextFruit] = nextFruit.IntakeData = CreateIntakeFlight(nextFruit);
            pathAuthoring.SetOuterBoundaryIgnored(nextFruit.BodyCollider, true);
            nextFruit.SetState(FruitStatus.EnteringLoop);
            nextFruit.DisablePhysics();
        }

        private int FindNearestUnlaunchedCandidate()
        {
            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;
            Vector2 entry = GetWaterfallStartPosition();
            for (int index = 0; index < congestion.Count; index++)
            {
                Fruit fruit = congestion[index];
                if (fruit == null || intakeFlights.ContainsKey(fruit)) continue;
                Vector2 offset = (Vector2)fruit.CachedTransform.position - entry;
                if (!IsInsideEntryZone(fruit.CachedTransform.position)) continue;
                if (Mathf.Abs(offset.x) > entryZoneWidth * 0.4f) continue;
                float score = offset.sqrMagnitude + Mathf.Max(0f, -offset.y) * 0.25f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestIndex = index;
            }
            return bestIndex;
        }

        private FruitIntakeData CreateIntakeFlight(Fruit fruit)
        {
            Vector2 start = fruit.CachedTransform.position;
            Vector2 landing = GetWaterfallStartPosition();
            float travelTime = Mathf.Max(intakeFallSmoothDuration,
                Vector2.Distance(start, landing) * 1.5f / Mathf.Max(0.1f, intakeFlightSpeed));
            // Do not extend flights to wait for earlier arrivals or a lane gap.
            intakeLaunchTimer = intakeLaunchInterval;
            return new FruitIntakeData
            {
                StartPosition = start,
                LandingPosition = landing,
                StartVelocity = fruit.LinearVelocity,
                ArrivalVelocity = EnteringLoopState.GetWaterfallArrivalVelocity(start, landing, travelTime,
                    Mathf.Min(speed, maxComfortableLoopSpeed)),
                Elapsed = -Time.deltaTime,
                Duration = travelTime
            };
        }


        private Vector3 GetWaterfallStartPosition() =>
            waterfallStartPoint != null ? waterfallStartPoint.position : GetEntryPosition();

        private bool IsInsideEntryZone(Vector3 position)
        {
            Vector2 center = GetWaterfallStartPosition();
            Vector2 offset = (Vector2)position - center;
            return Mathf.Abs(offset.x) <= entryZoneWidth * 0.5f
                && offset.y <= entryZoneHeight * 0.65f && offset.y >= -entryZoneHeight;
        }

        public void NotifyFruitCollected(Fruit fruit)
        {
            if (fruit == null || !owned.Remove(fruit)) return;
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index].Fruit == fruit) active.RemoveAt(index);
        }

        public void CopyLoopFruits(List<Fruit> destination)
        {
            destination.Clear();
            for (int index = 0; index < active.Count; index++)
            {
                Fruit fruit = active[index].Fruit;
                if (fruit != null && fruit.State == FruitStatus.OnLoop) destination.Add(fruit);
            }
        }

        public float FindClosestPathDistance(Vector3 worldPosition) =>
            path != null ? path.FindClosestDistance(worldPosition) : 0f;

        public bool ReserveFruitForBox(Fruit fruit)
        {
            if (fruit == null || fruit.State != FruitStatus.OnLoop) return false;
            for (int index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].Fruit != fruit) continue;
                active.RemoveAt(index);
                owned.Remove(fruit);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
                fruit.SetState(FruitStatus.Collecting);
                fruit.DisablePhysics();
                return true;
            }
            return false;
        }

        private float CircularSeparation(float first, float second)
        {
            return Mathf.Abs(Mathf.DeltaAngle(first / path.Length * 360f,
                second / path.Length * 360f)) / 360f * path.Length;
        }

        private void RebuildPath()
        {
            if (pathAuthoring == null) pathAuthoring = GetComponent<LoopPathAuthoring>();
            if (loopStart == null)
            {
                GameObject startObject = GameObject.Find("LoopStart");
                if (startObject != null) loopStart = startObject.transform;
            }
            if (waterfallStartPoint == null)
            {
                GameObject startObject = GameObject.Find("Startpoint");
                if (startObject != null) waterfallStartPoint = startObject.transform;
            }
            path = pathAuthoring != null ? pathAuthoring.BuildPath() : null;
            if (path != null) entryDistance = path.EntryDistance;
        }

        private Vector3 GetEntryPosition()
        {
            if (path == null)
                return loopStart != null ? loopStart.position : transform.position;
            path.Evaluate(entryDistance, out Vector3 position, out _);
            return position;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = GetWaterfallStartPosition();
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.35f);
            Gizmos.DrawCube(center + Vector3.down * entryZoneHeight * 0.175f,
                new Vector3(entryZoneWidth, entryZoneHeight * 1.65f, 0.05f));
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.75f);
            Gizmos.DrawWireSphere(GetWaterfallStartPosition(), admissionRadius);
        }
    }
}
