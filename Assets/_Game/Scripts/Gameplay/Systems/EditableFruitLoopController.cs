using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController : MonoBehaviour
    {
        private const int MaxLoopCapacity = 30;
        private const float LaneCarryDistance = 0.18f;
        private sealed class LoopFruit
        {
            public FruitActor Fruit;
            public float MergeElapsed;
            public float TargetDistance;
            public float DesiredSpeed;
            public float SpeedSmoothVelocity;
            public float SpeedVariation;
            public Vector2 MergeStartPosition;
            public Vector2 MergeStartVelocity;
        }

        [Header("Scene References")]
        [SerializeField] private LoopPathAuthoring pathAuthoring;
        [SerializeField] private Transform loopStart;

        [Header("Entry Congestion")]
        [SerializeField, Min(0.1f)] private float entryZoneWidth = 1.45f;
        [SerializeField, Min(0.1f)] private float entryZoneHeight = 1.35f;
        [SerializeField, Min(0.02f)] private float admissionCheckInterval = 0.07f;
        [SerializeField, Min(0.05f)] private float admissionRadius = 0.42f;
        [SerializeField, Min(1f)] private float entryFallSpeed = 10.25f;
        [SerializeField, Min(1f)] private float entryPullStrength = 28f;
        [SerializeField, Range(0.05f, 1f)] private float upperEntrySteering = 0.2f;
        [SerializeField, Range(1f, 2f)] private float gateEntrySteering = 1.15f;
        [SerializeField, Min(0.01f)] private float fastAdmissionInterval = 0.035f;
        [SerializeField, Min(0.05f)] private float fastMergeDuration = 0.15f;

        [Header("Full Loop Gate")]
        [SerializeField, Min(0.2f)] private float fullGateWidth = 1.25f;
        [SerializeField, Min(0.05f)] private float fullGateHeight = 0.18f;
        [SerializeField] private float fullGateYOffset = 0.22f;

        [Header("Lane Capture")]
        [SerializeField, Min(0.1f)] private float mergeDuration = 0.42f;
        [SerializeField, Min(0.5f)] private float maxMergeSpeed = 6f;
        [SerializeField, Min(0.05f)] private float jumpHeight = 0.52f;
        [SerializeField, Min(0.05f)] private float jumpLandingAdvance = 0.68f;
        [SerializeField, Min(0.05f)] private float entryContactDuration = 0.36f;

        [Header("Stable Loop")]
        [SerializeField, Min(0.1f)] private float speed = 4.35f;
        [SerializeField, Min(0.1f)] private float maxComfortableLoopSpeed = 4.25f;
        [SerializeField, Min(0.1f)] private float minimumSpacing = 0.62f;
        [SerializeField, Range(0.05f, 1f)] private float spacingCorrection = 0.55f;
        [SerializeField, Range(0f, 0.08f)] private float speedVariation = 0.025f;
        [SerializeField, Range(0.5f, 12f)] private float softContactStrength = 5f;
        [SerializeField, Range(0f, 1f)] private float contactVelocitySharing = 0.35f;
        [SerializeField, Min(0f)] private float spacingSettleDelay = 0.12f;
        [SerializeField, Range(0.25f, 0.4f)] private float spacingSettleLapFraction = 0.38f;
        [SerializeField, Range(0.005f, 0.08f)] private float maxSpacingShiftPerStep = 0.028f;

        private readonly List<FruitActor> congestion = new(24);
        private readonly List<LoopFruit> active = new(35);
        private readonly List<LoopFruit> stableFruits = new(35);
        private readonly HashSet<FruitActor> owned = new();
        private LoopPathCache path;
        private BoxCollider2D fullGateCollider;
        private float entryDistance;
        private float admissionTimer;
        private float spacingDelayRemaining;
        private float spacingRelaxRemaining;
        private float spacingRelaxDuration;

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
            speed = Mathf.Max(speed, 4.55f);
            maxComfortableLoopSpeed = Mathf.Max(maxComfortableLoopSpeed, 4.5f);
            entryFallSpeed = Mathf.Max(entryFallSpeed, 11.25f);
            entryPullStrength = Mathf.Max(entryPullStrength, 30f);
            spacingSettleDelay = Mathf.Min(spacingSettleDelay, 0.12f);
            spacingSettleLapFraction = Mathf.Clamp(spacingSettleLapFraction, 0.25f, 0.4f);
            // Keep one continuous, readable arc from takeoff through lane capture.
            mergeDuration = Mathf.Clamp(mergeDuration, 0.88f, 0.96f);
            fastMergeDuration = Mathf.Clamp(fastMergeDuration, 0.88f, 0.96f);
            // The chute-to-loop motion is a small forward hop, not a high jump.
            // Clamp stale scene values left by the previous tall arc tuning.
            jumpHeight = Mathf.Clamp(jumpHeight, 0.16f, 0.24f);
            // Keep the landing close to the mouth of the loop. Larger authored
            // values send the fruit too far around the outside before it joins.
            jumpLandingAdvance = Mathf.Clamp(jumpLandingAdvance, 0.10f, 0.26f);
            entryContactDuration = Mathf.Max(entryContactDuration, 0.36f);
        }

        private void Update()
        {
            if (!Application.isPlaying || path == null) return;
            admissionTimer -= Time.deltaTime;
            
            RefreshCongestionCandidates();
            
            if (admissionTimer <= 0f)
            {
                admissionTimer = Mathf.Min(admissionCheckInterval, fastAdmissionInterval);
                TryAdmitOne();
            }
        }

        private void FixedUpdate()
        {
            if (path == null) return;
            UpdateFullGate();
            UpdateEntryFlow();
            UpdateMergingFruit();
            UpdateStableLoop();
        }

        private void UpdateEntryFlow()
        {
            if (IsFull || congestion.Count == 0) return;
            Vector2 entry = loopStart != null ? loopStart.position : GetEntryPosition();
            for (int index = 0; index < congestion.Count; index++)
            {
                FruitActor fruit = congestion[index];
                if (fruit == null) continue;

                // Keep the upper part of the chute mostly vertical, then bend the
                // stream progressively toward the gate. This produces the soft,
                // curved slide seen in the reference instead of aiming every fruit
                // straight at one point from the moment it enters the zone.
                Vector2 position = fruit.CachedTransform.position;
                float heightAboveGate = Mathf.Max(0f, position.y - entry.y);
                float gateProximity = 1f - Mathf.Clamp01(
                    heightAboveGate / Mathf.Max(0.1f, entryZoneHeight * 0.65f));
                float curvedProximity = Mathf.SmoothStep(0f, 1f, gateProximity);
                float lateralSteering = Mathf.Lerp(
                    upperEntrySteering, gateEntrySteering, curvedProximity);
                fruit.FlowTowardEntry(entry, entryPullStrength, entryFallSpeed,
                    lateralSteering);
            }
        }

        private void RefreshCongestionCandidates()
        {
            for (int index = congestion.Count - 1; index >= 0; index--)
            {
                FruitActor fruit = congestion[index];
                if (fruit == null || owned.Contains(fruit) || !IsInsideEntryZone(fruit.CachedTransform.position))
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
                congestion.Add(fruit);
            }
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
                // Rebalance immediately and smoothly while this fruit is flying to
                // its box; the remaining stream must never pause its spacing pass.
                BeginSpacingRelaxation();
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

        private static float SignedVariation(int seed)
        {
            uint value = unchecked((uint)seed * 747796405u + 2891336453u);
            return value / (float)uint.MaxValue * 2f - 1f;
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
            if (path != null)
                entryDistance = path.FindClosestDistance(loopStart != null ? loopStart.position : GetEntryPosition());
        }

        private Vector3 GetEntryPosition()
        {
            path.Evaluate(entryDistance, out Vector3 position, out _);
            return position;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = loopStart != null ? loopStart.position : transform.position;
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.35f);
            Gizmos.DrawCube(center + Vector3.down * entryZoneHeight * 0.175f,
                new Vector3(entryZoneWidth, entryZoneHeight * 1.65f, 0.05f));
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.75f);
            Gizmos.DrawWireSphere(center, admissionRadius);
        }
    }
}

