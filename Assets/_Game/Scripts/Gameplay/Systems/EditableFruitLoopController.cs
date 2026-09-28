using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController : MonoBehaviour
    {
        private const int MaxLoopCapacity = 30;
        private sealed class LoopFruit
        {
            public FruitActor Fruit;
            public Vector2 MergeStartPosition;
            public Quaternion MergeStartRotation;
            public Vector2 MergeStartVelocity;
            public float MergeElapsed;
            public float MergeTargetDistance;
            public float MergeDuration;
        }

        private sealed class IntakeFlight
        {
            public Vector2 StartPosition;
            public Vector2 StartVelocity;
            public Vector2 LandingPosition;
            public float Elapsed;
            public float Duration;
            public Vector2 ArrivalVelocity;
        }

        [Header("Scene References")]
        [SerializeField] private LoopPathAuthoring pathAuthoring;
        [SerializeField] private Transform loopStart;
        [SerializeField] private Transform waterfallStartPoint;

        [Header("Entry Congestion")]
        [SerializeField, Min(0.1f)] private float entryZoneWidth = 1.45f;
        [SerializeField, Min(0.1f)] private float entryZoneHeight = 1.35f;
        [SerializeField, Min(0.05f)] private float admissionRadius = 0.42f;

        [Header("Waterfall Intake")]
        [SerializeField, Range(2f, 12f)] private float intakeFlightSpeed = 7.2f;
        [SerializeField, Min(0.04f)] private float intakeLaunchInterval = 0.04f;

        [Header("Full Loop Gate")]
        [SerializeField, Min(0.2f)] private float fullGateWidth = 1.25f;
        [SerializeField, Min(0.05f)] private float fullGateHeight = 0.18f;
        [SerializeField] private float fullGateYOffset = 0.22f;

        [Header("Stable Loop")]
        [SerializeField, Min(0.1f)] private float speed = 6.5f;
        [SerializeField, Min(0.1f)] private float maxComfortableLoopSpeed = 6.5f;
        [SerializeField, Range(2f, 30f)] private float laneCenteringStrength = 22f;
        [SerializeField, Range(1f, 8f)] private float physicalLoopMaxSpeed = 5.8f;

        [Header("Fruit Spacing")]
        [SerializeField, Min(0.01f)] private float minimumFruitGap = 0.06f;

        [Header("P00 Lane Merge")]
        [SerializeField, Range(0.08f, 0.4f)] private float laneMergeDuration = 0.09f;
        [SerializeField, Range(0.1f, 1f)] private float laneMergeAdvance = 0.46f;

        private readonly List<FruitActor> congestion = new(24);
        private readonly List<LoopFruit> active = new(35);
        private readonly List<LoopFruit> stableFruits = new(35);
        private readonly HashSet<FruitActor> owned = new();
        private readonly Dictionary<FruitActor, IntakeFlight> intakeFlights = new();
        private LoopPathCache path;
        private BoxCollider2D fullGateCollider;
        private float entryDistance;
        private float intakeLaunchTimer;
        private readonly List<FruitActor> arrivedFruits = new(24);

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
            speed = Mathf.Max(6.5f, speed);
            maxComfortableLoopSpeed = Mathf.Max(6.5f, maxComfortableLoopSpeed);
            laneCenteringStrength = Mathf.Clamp(laneCenteringStrength, 18f, 26f);
            physicalLoopMaxSpeed = Mathf.Clamp(physicalLoopMaxSpeed, 5.2f, 6.4f);
            // This zone tracks fruit at the chute outlet. Fruit above it keep
            // their own physics until they reach the end of the chute.
            entryZoneWidth = Mathf.Clamp(entryZoneWidth, 1.3f, 1.5f);
            entryZoneHeight = Mathf.Clamp(entryZoneHeight, 0.8f, 1.35f);
            intakeFlightSpeed = Mathf.Clamp(intakeFlightSpeed, 7.2f, 10f);
            intakeLaunchInterval = Mathf.Clamp(intakeLaunchInterval, 0.04f, 0.05f);
            laneMergeDuration = Mathf.Clamp(laneMergeDuration, 0.08f, 0.10f);
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
                LoopFruit item = active[index];
                FruitActor fruit = item.Fruit;
                if (fruit == null || fruit.State != FruitState.MergingToLane) continue;

                float elapsed = item.MergeElapsed + Time.deltaTime;
                float overflow = Mathf.Max(0f, elapsed - item.MergeDuration);
                item.MergeElapsed = Mathf.Min(elapsed, item.MergeDuration);
                float progress = Mathf.Clamp01(item.MergeElapsed / item.MergeDuration);
                path.Evaluate(item.MergeTargetDistance,
                    out Vector3 targetPosition, out Quaternion targetRotation);
                Vector2 endTangent = ((Vector2)(targetRotation * Vector3.up)).normalized;
                Vector2 startTangent = item.MergeStartVelocity * item.MergeDuration;
                if (startTangent.sqrMagnitude < 0.0025f)
                    startTangent = endTangent * (laneSpeed * item.MergeDuration * 0.2f);
                Vector2 finishTangent = endTangent * laneSpeed * item.MergeDuration;
                fruit.CachedTransform.position = HermitePosition(
                    item.MergeStartPosition, targetPosition,
                    startTangent, finishTangent, progress);
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
                fruit.CachedTransform.rotation = Quaternion.Slerp(
                    item.MergeStartRotation, targetRotation, smoothProgress);

                if (progress < 1f) continue;
                float completedDistance = Mathf.Repeat(
                    item.MergeTargetDistance + laneSpeed * overflow, path.Length);
                path.Evaluate(completedDistance, out Vector3 completedPosition,
                    out Quaternion completedRotation);
                fruit.CachedTransform.position = completedPosition;
                Vector2 tangent = completedRotation * Vector3.up;
                fruit.CurrentSpeed = laneSpeed;
                fruit.SetBodyColliderEnabled(true);
                SetLoopFruitContactsIgnored(fruit, true);
                fruit.CompleteLaneCapture(
                    completedDistance, tangent.normalized * laneSpeed);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
            }
        }

        private static Vector2 HermitePosition(Vector2 start, Vector2 end,
            Vector2 startTangent, Vector2 endTangent, float time)
        {
            float time2 = time * time;
            float time3 = time2 * time;
            return (2f * time3 - 3f * time2 + 1f) * start
                + (time3 - 2f * time2 + time) * startTangent
                + (-2f * time3 + 3f * time2) * end
                + (time3 - time2) * endTangent;
        }

        private void FixedUpdate()
        {
            if (path == null) return;
            UpdateFullGate();
        }

        private void UpdateIntakeFlights()
        {
            arrivedFruits.Clear();
            foreach (KeyValuePair<FruitActor, IntakeFlight> pair in intakeFlights)
            {
                FruitActor fruit = pair.Key;
                IntakeFlight flight = pair.Value;
                if (fruit == null) { arrivedFruits.Add(fruit); continue; }
                flight.Elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(flight.Elapsed / flight.Duration);
                fruit.CachedTransform.position = HermitePosition(
                    flight.StartPosition, flight.LandingPosition,
                    flight.StartVelocity * flight.Duration,
                    flight.ArrivalVelocity * flight.Duration, progress);
                if (flight.Elapsed >= flight.Duration) arrivedFruits.Add(fruit);
            }
            // Every reserved arrival immediately joins the lane at P00.
            for (int index = 0; index < arrivedFruits.Count; index++)
            {
                FruitActor fruit = arrivedFruits[index];
                IntakeFlight flight = intakeFlights[fruit];
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
                FruitActor fruit = congestion[index];
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

            FruitActor[] fruits = FindObjectsByType<FruitActor>(FindObjectsSortMode.None);
            for (int index = 0; index < fruits.Length; index++)
            {
                FruitActor fruit = fruits[index];
                if (fruit == null || owned.Contains(fruit) || congestion.Contains(fruit)) continue;
                if (fruit.State != FruitState.Released && fruit.State != FruitState.EntryCongestion
                    && fruit.State != FruitState.WaitingFull && fruit.State != FruitState.IntakeWaiting
                    && fruit.State != FruitState.EnteringLoop && fruit.State != FruitState.Transient) continue;
                if (!IsInsideEntryZone(fruit.CachedTransform.position)) continue;

                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
                congestion.Add(fruit);

            }

            if (active.Count + intakeFlights.Count >= Capacity)
            {
                for (int index = 0; index < congestion.Count; index++)
                {
                    FruitActor fruit = congestion[index];
                    if (fruit == null || intakeFlights.ContainsKey(fruit)) continue;
                    if (fruit.State != FruitState.WaitingFull)
                        fruit.EnterCongestion(true);
                    pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
                }
                return;
            }

            // Unselected fruit keep chute physics; selected fruit follow the
            // continuous waterfall trajectory through the authored Startpoint.
            for (int index = 0; index < congestion.Count; index++)
            {
                FruitActor fruit = congestion[index];
                if (fruit == null || intakeFlights.ContainsKey(fruit)) continue;
                if (fruit.State == FruitState.WaitingFull)
                    fruit.EnterCongestion(false);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
            }

            int freeSlots = Capacity - active.Count - intakeFlights.Count;
            if (freeSlots <= 0 || intakeLaunchTimer > 0f) return;

            int nextIndex = FindNearestUnlaunchedCandidate();
            if (nextIndex < 0) return;
            FruitActor nextFruit = congestion[nextIndex];
            intakeFlights[nextFruit] = CreateIntakeFlight(nextFruit);
            pathAuthoring.SetOuterBoundaryIgnored(nextFruit.BodyCollider, true);
            nextFruit.SetState(FruitState.EnteringLoop);
            nextFruit.DisablePhysics();
        }

        private int FindNearestUnlaunchedCandidate()
        {
            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;
            Vector2 entry = GetWaterfallStartPosition();
            for (int index = 0; index < congestion.Count; index++)
            {
                FruitActor fruit = congestion[index];
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

        private IntakeFlight CreateIntakeFlight(FruitActor fruit)
        {
            Vector2 start = fruit.CachedTransform.position;
            Vector2 landing = GetWaterfallStartPosition();
            float travelTime = Mathf.Max(0.08f,
                Vector2.Distance(start, landing) / Mathf.Max(0.1f, intakeFlightSpeed));
            // Do not extend flights to wait for earlier arrivals or a lane gap.
            intakeLaunchTimer = intakeLaunchInterval;
            return new IntakeFlight
            {
                StartPosition = start,
                LandingPosition = landing,
                StartVelocity = fruit.LinearVelocity,
                Elapsed = -Time.deltaTime,
                ArrivalVelocity = Vector2.down * intakeFlightSpeed,
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

        public void NotifyFruitCollected(FruitActor fruit)
        {
            if (fruit == null || !owned.Remove(fruit)) return;
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index].Fruit == fruit) active.RemoveAt(index);
        }

        public void CopyLoopFruits(List<FruitActor> destination)
        {
            destination.Clear();
            for (int index = 0; index < active.Count; index++)
            {
                FruitActor fruit = active[index].Fruit;
                if (fruit != null && fruit.State == FruitState.OnLoop) destination.Add(fruit);
            }
        }

        public float FindClosestPathDistance(Vector3 worldPosition) =>
            path != null ? path.FindClosestDistance(worldPosition) : 0f;

        public bool ReserveFruitForBox(FruitActor fruit)
        {
            if (fruit == null || fruit.State != FruitState.OnLoop) return false;
            for (int index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].Fruit != fruit) continue;
                active.RemoveAt(index);
                owned.Remove(fruit);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
                fruit.SetState(FruitState.Collecting);
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

