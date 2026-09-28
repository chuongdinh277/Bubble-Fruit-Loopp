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
            public float MergeElapsed;
            public float MergeTargetDistance;
        }

        private sealed class IntakeFlight
        {
            public Vector2 StartPosition;
            public Vector2 ApexPosition;
            public float Elapsed;
            public float RiseDuration;
            public float FallDuration;
            public float Duration;
        }

        [Header("Scene References")]
        [SerializeField] private LoopPathAuthoring pathAuthoring;
        [SerializeField] private Transform loopStart;

        [Header("Entry Congestion")]
        [SerializeField, Min(0.1f)] private float entryZoneWidth = 1.45f;
        [SerializeField, Min(0.1f)] private float entryZoneHeight = 1.35f;
        [SerializeField, Min(0.02f)] private float admissionCheckInterval = 0.07f;
        [SerializeField, Min(0.05f)] private float admissionRadius = 0.42f;
        [SerializeField, Min(0.01f)] private float fastAdmissionInterval = 0.022f;

        [Header("Intake Flight")]
        [SerializeField, Range(2f, 12f)] private float intakeFlightSpeed = 3.8f;
        [SerializeField, Range(0.02f, 0.35f)] private float intakeHopHeight = 0.24f;

        [Header("Full Loop Gate")]
        [SerializeField, Min(0.2f)] private float fullGateWidth = 1.25f;
        [SerializeField, Min(0.05f)] private float fullGateHeight = 0.18f;
        [SerializeField] private float fullGateYOffset = 0.22f;

        [Header("Stable Loop")]
        [SerializeField, Min(0.1f)] private float speed = 4.6f;
        [SerializeField, Min(0.1f)] private float maxComfortableLoopSpeed = 4.45f;
        [SerializeField, Range(2f, 30f)] private float laneCenteringStrength = 22f;
        [SerializeField, Range(1f, 8f)] private float physicalLoopMaxSpeed = 5.8f;

        [Header("P00 Lane Merge")]
        [SerializeField, Range(0.08f, 0.4f)] private float laneMergeDuration = 0.18f;
        [SerializeField, Range(0.1f, 1f)] private float laneMergeAdvance = 0.46f;

        private readonly List<FruitActor> congestion = new(24);
        private readonly List<LoopFruit> active = new(35);
        private readonly List<LoopFruit> stableFruits = new(35);
        private readonly HashSet<FruitActor> owned = new();
        private readonly Dictionary<FruitActor, IntakeFlight> intakeFlights = new();
        private LoopPathCache path;
        private BoxCollider2D fullGateCollider;
        private float entryDistance;
        private float admissionTimer;

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
            // Keep the loop readable: fruit should visibly travel from the chute
            // into the lane rather than being carried away immediately.
            speed = Mathf.Clamp(speed, 4.2f, 4.8f);
            maxComfortableLoopSpeed = Mathf.Clamp(maxComfortableLoopSpeed, 4.1f, 4.6f);
            laneCenteringStrength = Mathf.Clamp(laneCenteringStrength, 18f, 26f);
            physicalLoopMaxSpeed = Mathf.Clamp(physicalLoopMaxSpeed, 5.2f, 6.4f);
            fastAdmissionInterval = Mathf.Clamp(fastAdmissionInterval, 0.018f, 0.035f);
            // This zone belongs only to the takeoff point at the end of the chute.
            // Fruit above it must remain completely independent chute physics.
            // Let both chute sides roll almost to the shared centre before takeoff.
            // A narrow symmetric band makes left/right fruit begin the same jump
            // from comparable positions instead of launching out on the slopes.
            entryZoneWidth = Mathf.Clamp(entryZoneWidth, 1.3f, 1.5f);
            entryZoneHeight = Mathf.Clamp(entryZoneHeight, 0.8f, 1.35f);
            intakeFlightSpeed = Mathf.Clamp(intakeFlightSpeed, 3.4f, 4.3f);
            intakeHopHeight = Mathf.Clamp(intakeHopHeight, 0.2f, 0.3f);
            laneMergeDuration = Mathf.Clamp(laneMergeDuration, 0.14f, 0.24f);
            laneMergeAdvance = Mathf.Clamp(laneMergeAdvance, 0.36f, 0.58f);
        }

        private void Update()
        {
            if (!Application.isPlaying || path == null) return;
            admissionTimer -= Time.deltaTime;
            
            RefreshCongestionCandidates();
            // Transform-authored intake motion belongs to the rendered frame, not
            // the physics tick. This keeps Lerp/SmoothStep visually continuous.
            UpdateIntakeFlights();
            UpdateLaneMerges();
            
            if (admissionTimer <= 0f)
            {
                admissionTimer = Mathf.Min(admissionCheckInterval, fastAdmissionInterval);
                TryAdmitOne();
            }
        }

        private void UpdateLaneMerges()
        {
            float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                FruitActor fruit = item.Fruit;
                if (fruit == null || fruit.State != FruitState.MergingToLane) continue;

                item.MergeElapsed = Mathf.Min(
                    item.MergeElapsed + Time.deltaTime, laneMergeDuration);
                float progress = Mathf.Clamp01(item.MergeElapsed / laneMergeDuration);
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
                path.Evaluate(item.MergeTargetDistance,
                    out Vector3 targetPosition, out Quaternion targetRotation);
                fruit.CachedTransform.position = Vector3.Lerp(
                    item.MergeStartPosition, targetPosition, smoothProgress);
                fruit.CachedTransform.rotation = Quaternion.Slerp(
                    fruit.CachedTransform.rotation, targetRotation, smoothProgress);

                if (progress < 1f) continue;
                Vector2 tangent = targetRotation * Vector3.up;
                fruit.CurrentSpeed = laneSpeed;
                fruit.CompleteLaneCapture(
                    item.MergeTargetDistance, tangent.normalized * laneSpeed);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
            }
        }

        private void FixedUpdate()
        {
            if (path == null) return;
            UpdateFullGate();
            UpdateStableLoop();
        }

        private void UpdateIntakeFlights()
        {
            if (intakeFlights.Count == 0) return;
            Vector2 landing = GetEntryPosition();
            foreach (KeyValuePair<FruitActor, IntakeFlight> pair in intakeFlights)
            {
                FruitActor fruit = pair.Key;
                IntakeFlight flight = pair.Value;
                if (fruit == null || owned.Contains(fruit)) continue;

                flight.Elapsed = Mathf.Min(
                    flight.Elapsed + Time.deltaTime, flight.Duration);
                Vector2 position;
                if (flight.Elapsed < flight.RiseDuration)
                {
                    float rise = Mathf.Clamp01(flight.Elapsed / flight.RiseDuration);
                    position = Vector2.Lerp(flight.StartPosition, flight.ApexPosition, rise);
                }
                else
                {
                    float fall = Mathf.Clamp01(
                        (flight.Elapsed - flight.RiseDuration) / flight.FallDuration);
                    position = Vector2.Lerp(flight.ApexPosition, landing, fall);
                }
                fruit.CachedTransform.position = position;
            }
        }

        private void RefreshCongestionCandidates()
        {
            for (int index = congestion.Count - 1; index >= 0; index--)
            {
                FruitActor fruit = congestion[index];
                if (fruit == null || owned.Contains(fruit))
                {
                    if (fruit != null) intakeFlights.Remove(fruit);
                    congestion.RemoveAt(index);
                    continue;
                }
                // Once a fruit takes off, it owns its complete trajectory to P00.
                // Never cancel the flight merely because it left the takeoff zone.
                if (intakeFlights.ContainsKey(fruit)) continue;
                if (IsInsideEntryZone(fruit.CachedTransform.position)) continue;

                // A fruit that missed T0 must collide with the track normally again.
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
                intakeFlights.Remove(fruit);
                congestion.RemoveAt(index);
            }

            FruitActor[] fruits = FindObjectsByType<FruitActor>(FindObjectsSortMode.None);
            for (int index = 0; index < fruits.Length; index++)
            {
                FruitActor fruit = fruits[index];
                if (fruit == null || owned.Contains(fruit) || congestion.Contains(fruit)) continue;
                // Legacy intake states are accepted too, so a script reload during Play Mode
                // cannot leave fruit permanently stranded at the gate.
                if (fruit.State != FruitState.Released && fruit.State != FruitState.EntryCongestion
                    && fruit.State != FruitState.WaitingFull && fruit.State != FruitState.IntakeWaiting
                    && fruit.State != FruitState.EnteringLoop && fruit.State != FruitState.Transient) continue;
                if (!IsInsideEntryZone(fruit.CachedTransform.position)) continue;
                fruit.EnterCongestion(IsFull);
                // The authored outer loop wall is closed. Let intake fruit cross
                // that wall at P00; the separate full gate still stops them when
                // the loop has reached capacity.
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
                congestion.Add(fruit);
                if (!IsFull)
                {
                    intakeFlights[fruit] = CreateIntakeFlight(fruit);
                    fruit.DisablePhysics();
                }
            }

            // Fruit held by the full-loop gate begin the same authored flight as
            // soon as a slot becomes available again.
            if (!IsFull)
            {
                for (int index = 0; index < congestion.Count; index++)
                {
                    FruitActor fruit = congestion[index];
                    if (fruit == null || intakeFlights.ContainsKey(fruit)) continue;
                    intakeFlights[fruit] = CreateIntakeFlight(fruit);
                    pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
                    fruit.DisablePhysics();
                }
            }
        }

        private IntakeFlight CreateIntakeFlight(FruitActor fruit)
        {
            Vector2 start = fruit.CachedTransform.position;
            Vector2 landing = GetEntryPosition();
            float horizontalDirection = Mathf.Sign(landing.x - start.x);
            if (Mathf.Abs(horizontalDirection) < 0.01f) horizontalDirection = 1f;
            Vector2 apex = start + new Vector2(
                horizontalDirection * 0.16f, intakeHopHeight);
            float speed = Mathf.Max(0.1f, intakeFlightSpeed);
            float riseDuration = Mathf.Max(0.04f, Vector2.Distance(start, apex) / speed);
            float fallDuration = Mathf.Max(0.04f, Vector2.Distance(apex, landing) / speed);
            return new IntakeFlight
            {
                StartPosition = start,
                ApexPosition = apex,
                Elapsed = 0f,
                RiseDuration = riseDuration,
                FallDuration = fallDuration,
                Duration = riseDuration + fallDuration
            };
        }

        private bool IsInsideEntryZone(Vector3 position)
        {
            Vector2 center = loopStart != null ? loopStart.position : GetEntryPosition();
            Vector2 offset = (Vector2)position - center;
            return Mathf.Abs(offset.x) <= entryZoneWidth * 0.5f
                && offset.y <= entryZoneHeight * 0.65f && offset.y >= -entryZoneHeight;
        }

        public void NotifyFruitCollected(FruitActor fruit)
        {
            if (fruit == null || !owned.Remove(fruit)) return;
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index].Fruit == fruit) active.RemoveAt(index);
            admissionTimer = 0f;
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
                admissionTimer = 0f;
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
            Vector3 center = loopStart != null ? loopStart.position
                : path != null ? GetEntryPosition() : transform.position;
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.35f);
            Gizmos.DrawCube(center + Vector3.down * entryZoneHeight * 0.175f,
                new Vector3(entryZoneWidth, entryZoneHeight * 1.65f, 0.05f));
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.75f);
            Gizmos.DrawWireSphere(center, admissionRadius);
        }
    }
}

