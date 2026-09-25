using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class EditableFruitLoopController : MonoBehaviour
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
            public TrailRenderer MergeTrail;
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
        [SerializeField, Min(0.1f)] private float spacingSettleDuration = 20f;
        [SerializeField, Range(0.005f, 0.08f)] private float maxSpacingShiftPerStep = 0.028f;

        private readonly List<FruitActor> congestion = new(24);
        private readonly List<LoopFruit> active = new(35);
        private readonly List<LoopFruit> stableFruits = new(35);
        private readonly HashSet<FruitActor> owned = new();
        private static Material mergeTrailMaterial;
        private LoopPathCache path;
        private BoxCollider2D fullGateCollider;
        private float entryDistance;
        private float admissionTimer;
        private float spacingDelayRemaining;
        private float spacingRelaxRemaining;

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
            speed = Mathf.Max(speed, 4.35f);
            maxComfortableLoopSpeed = Mathf.Max(maxComfortableLoopSpeed, 3.65f);
            entryFallSpeed = Mathf.Max(entryFallSpeed, 11.25f);
            entryPullStrength = Mathf.Max(entryPullStrength, 30f);
            spacingSettleDelay = Mathf.Min(spacingSettleDelay, 0.12f);
            spacingSettleDuration = Mathf.Max(spacingSettleDuration, 20f);
            // Entry should flow straight into the moving lane. Clamp stale scene
            // values so admission never hangs on a long, separate landing step.
            fastMergeDuration = Mathf.Clamp(fastMergeDuration, 0.32f, 0.46f);
            // The chute-to-loop motion is a small forward hop, not a high jump.
            // Clamp stale scene values left by the previous tall arc tuning.
            jumpHeight = Mathf.Clamp(jumpHeight, 0.08f, 0.16f);
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

        private void EnsureFullGate()
        {
            if (!Application.isPlaying || fullGateCollider != null) return;
            Transform existing = transform.Find("Full Loop Intake Gate");
            GameObject gate = existing != null
                ? existing.gameObject
                : new GameObject("Full Loop Intake Gate");
            if (existing == null) gate.transform.SetParent(transform, true);
            fullGateCollider = gate.GetComponent<BoxCollider2D>();
            if (fullGateCollider == null) fullGateCollider = gate.AddComponent<BoxCollider2D>();
            fullGateCollider.isTrigger = false;
            fullGateCollider.enabled = false;
            UpdateFullGateTransform();
        }

        private void UpdateFullGate()
        {
            EnsureFullGate();
            if (fullGateCollider == null) return;
            UpdateFullGateTransform();
            fullGateCollider.enabled = IsFull;
        }

        private void UpdateFullGateTransform()
        {
            if (fullGateCollider == null) return;
            Vector3 entry = loopStart != null ? loopStart.position : GetEntryPosition();
            fullGateCollider.transform.position = entry + Vector3.up * fullGateYOffset;
            fullGateCollider.transform.rotation = Quaternion.identity;
            fullGateCollider.transform.localScale = Vector3.one;
            fullGateCollider.size = new Vector2(fullGateWidth, fullGateHeight);
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

        private void TryAdmitOne()
        {
            if (congestion.Count == 0) return;
            if (IsFull)
            {
                for (int index = 0; index < congestion.Count; index++)
                    if (congestion[index] != null) congestion[index].EnterCongestion(true);
                return;
            }

            int candidateIndex = FindBestCandidateIndex();
            if (candidateIndex < 0) return;
            FruitActor candidate = congestion[candidateIndex];
            congestion.RemoveAt(candidateIndex);
            owned.Add(candidate);
            pathAuthoring.SetOuterBoundaryIgnored(candidate.BodyCollider, true);

            // Capture the real falling motion, then animate the short merge ourselves.
            // This avoids collision impulses at the narrow gate while preserving a
            // continuous trajectory into the loop.
            Vector2 mergeStartPosition = candidate.CachedTransform.position;
            Vector2 mergeStartVelocity = candidate.LinearVelocity;
            candidate.SetState(FruitState.MergingToLane);
            candidate.DisablePhysics();
            // Give the actor lane speed at takeoff. The visual merge below then
            // carries that same motion into the path instead of accelerating only
            // after the landing frame.
            candidate.CurrentSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            
            active.Add(new LoopFruit
            {
                Fruit = candidate,
                TargetDistance = entryDistance,
                SpeedVariation = 1f + SignedVariation(candidate.GetInstanceID()) * speedVariation,
                MergeElapsed = 0f,
                MergeStartPosition = mergeStartPosition,
                MergeStartVelocity = Vector2.ClampMagnitude(mergeStartVelocity, maxMergeSpeed),
                MergeTrail = CreateMergeTrail(candidate)
            });
        }

        private int FindBestCandidateIndex()
        {
            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;
            Vector2 gate = loopStart != null ? loopStart.position : GetEntryPosition();
            for (int index = 0; index < congestion.Count; index++)
            {
                FruitActor fruit = congestion[index];
                if (fruit == null) continue;
                Vector2 offset = (Vector2)fruit.CachedTransform.position - gate;
                if (offset.sqrMagnitude > admissionRadius * admissionRadius) continue;
                float score = offset.sqrMagnitude + Mathf.Max(0f, -offset.y) * 0.25f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestIndex = index;
            }
            return bestIndex;
        }

        private void UpdateMergingFruit()
        {
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                if (item.Fruit == null || item.Fruit.State != FruitState.MergingToLane) continue;

                item.MergeElapsed += Time.fixedDeltaTime;
                float effectiveMergeDuration = Mathf.Max(0.05f, Mathf.Min(mergeDuration, fastMergeDuration));
                float progress = Mathf.Clamp01(item.MergeElapsed / effectiveMergeDuration);

                float duration = effectiveMergeDuration;
                float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
                float mergeDistance = Mathf.Clamp(
                    jumpLandingAdvance + laneSpeed * duration * 0.25f + LaneCarryDistance,
                    0.16f, 0.86f);
                item.TargetDistance = Mathf.Repeat(entryDistance + mergeDistance, path.Length);

                // Follow a point already on the loop while descending. The chute
                // offset fades with zero slope at both ends, so there is no snap
                // when the fruit becomes part of the lane.
                float pathProgress = progress;
                // Keep the jump mostly on the chute line. Only the final part
                // of the descent bends the actor into the loop mouth.
                float entryTurn = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.42f, 1f, progress));
                float offsetFade = entryTurn;
                float distance = Mathf.Repeat(entryDistance + mergeDistance * pathProgress, path.Length);
                path.Evaluate(entryDistance, out Vector3 entryPoint, out _);
                path.Evaluate(distance, out Vector3 lanePoint, out Quaternion rotation);
                Vector2 offset = (Vector2)item.MergeStartPosition - (Vector2)entryPoint;
                Vector2 position = (Vector2)lanePoint + offset * (1f - offsetFade);

                // Give the takeoff a small, soft nudge toward the centre of the
                // loop gate. This keeps the jump readable without sending it
                // high or far past the entry before the final lane turn.
                Vector2 gateCenter = loopStart != null ? (Vector2)loopStart.position : (Vector2)entryPoint;
                Vector2 towardGate = (gateCenter - item.MergeStartPosition).normalized;
                float gateBump = 0.14f * 4f * progress * (1f - progress);
                position += towardGate * gateBump;

                // Add a small lateral belly to the descent. Its direction is
                // chosen from the chute-to-entry vector and the loop tangent,
                // so the fruit bends inward instead of drifting outward.
                Vector2 chuteDirection = ((Vector2)entryPoint - item.MergeStartPosition).normalized;
                Vector2 lateral = new Vector2(-chuteDirection.y, chuteDirection.x);
                Vector2 entryTangent = (rotation * Vector3.up).normalized;
                if (Vector2.Dot(lateral, entryTangent) < 0f) lateral = -lateral;
                float lateralBend = 0.18f * entryTurn * (1f - progress);
                position += lateral * lateralBend;

                // A bump with zero derivative at takeoff and landing keeps the
                // vertical jump continuous with the lane motion.
                float arc = 16f * jumpHeight * progress * progress
                    * (1f - progress) * (1f - progress);
                position += Vector2.up * arc;
                item.Fruit.CachedTransform.position = position;
                if (item.MergeTrail != null)
                    item.MergeTrail.transform.position = item.Fruit.CachedTransform.position;

                if (progress < 1f) continue;

                float mergeSpeed = mergeDistance / duration;
                item.Fruit.CurrentSpeed = mergeSpeed;
                item.DesiredSpeed = laneSpeed;
                Vector2 landingTangent = rotation * Vector3.up;
                item.Fruit.CompleteLaneCapture(item.TargetDistance, landingTangent.normalized * mergeSpeed);
                ReleaseMergeTrail(item.MergeTrail);
                item.MergeTrail = null;
                // The fruit is already travelling at lane speed on contact, so
                // there is no artificial settle/pause before spacing takes over.
                spacingDelayRemaining = 0f;
                spacingRelaxRemaining = Mathf.Max(spacingRelaxRemaining, spacingSettleDuration);
                pathAuthoring.SetOuterBoundaryIgnored(item.Fruit.BodyCollider, false);
            }
        }

        private static Vector2 Hermite(Vector2 start, Vector2 end, Vector2 startTangent,
            Vector2 endTangent, float time)
        {
            float t2 = time * time;
            float t3 = t2 * time;
            return (2f * t3 - 3f * t2 + 1f) * start
                + (t3 - 2f * t2 + time) * startTangent
                + (-2f * t3 + 3f * t2) * end
                + (t3 - t2) * endTangent;
        }

        private static TrailRenderer CreateMergeTrail(FruitActor fruit)
        {
            if (fruit == null) return null;
            if (mergeTrailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return null;
                mergeTrailMaterial = new Material(shader)
                {
                    name = "Runtime Loop Entry Trail",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            Color color = ColorFor(fruit.Type);
            GameObject trailObject = new($"{fruit.name} Loop Entry Trail");
            trailObject.transform.position = fruit.CachedTransform.position;
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mergeTrailMaterial;
            trail.time = 0.32f;
            trail.minVertexDistance = 0.018f;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.03f),
                new Keyframe(0.18f, 0.19f),
                new Keyframe(1f, 0f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.32f), 0f),
                    new GradientColorKey(color, 0.42f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.68f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
            trail.sortingOrder = 18;
            trail.emitting = true;
            return trail;
        }

        private static void ReleaseMergeTrail(TrailRenderer trail)
        {
            if (trail == null) return;
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.08f);
        }

        private static Color ColorFor(FruitType type) => type switch
        {
            FruitType.Apple => new Color(0f, 0.46f, 1f),
            FruitType.Orange => new Color(1f, 0.48f, 0.05f),
            FruitType.Grape => new Color(0.58f, 0.2f, 0.92f),
            FruitType.Lemon => new Color(1f, 0.86f, 0.08f),
            FruitType.Strawberry => new Color(1f, 0.3f, 0.48f),
            _ => Color.white
        };

        private void UpdateStableLoop()
        {
            if (active.Count == 0) return;

            float loopSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);

            stableFruits.Clear();
            for (int index = 0; index < active.Count; index++)
            {
                LoopFruit item = active[index];
                if (item.Fruit != null && item.Fruit.State == FruitState.OnLoop)
                    stableFruits.Add(item);
            }
            if (stableFruits.Count == 0) return;

            stableFruits.Sort((left, right) =>
                left.Fruit.PathDistance.CompareTo(right.Fruit.PathDistance));
            float idealSpacing = path.Length / stableFruits.Count;

            // Every fruit shares one fixed cruising speed. Removing a fruit for a
            // box creates only a visual gap and never changes the pace of the
            // remaining stream.
            for (int index = 0; index < stableFruits.Count; index++)
            {
                LoopFruit item = stableFruits[index];
                item.DesiredSpeed = loopSpeed;
            }

            // Equalise gaps by smoothly varying velocity, never by rewriting path
            // positions. This prevents the visible hitch from the old snap-based
            // spacing pass while preserving continuous forward motion.
            // First let newly-landed fruit press apart like soft physical pieces;
            // the delayed spacing solver then takes over and evens out the stream.
            if (spacingDelayRemaining > 0f)
                ResolveSoftLaneContacts(idealSpacing);
            UpdateEvenSpacing(idealSpacing);

            for (int index = 0; index < stableFruits.Count; index++)
            {
                LoopFruit item = stableFruits[index];
                
                float phase = Time.time * 1.15f + item.Fruit.GetInstanceID() * 0.017f;
                // Keep one shared cruising speed after the entry overlap has been
                // resolved. Per-fruit pace variation made tidy gaps drift over time.
                float adjustedSpeed = Mathf.SmoothDamp(
                    item.Fruit.CurrentSpeed,
                    item.DesiredSpeed,
                    ref item.SpeedSmoothVelocity,
                    0.14f,
                    loopSpeed,
                    Time.fixedDeltaTime);

                // No random speed variation, pure even rotation
                item.Fruit.CurrentSpeed = adjustedSpeed;
                item.Fruit.PathDistance += item.Fruit.CurrentSpeed * Time.fixedDeltaTime;
                
                if (item.Fruit.PathDistance >= path.Length) item.Fruit.PathDistance -= path.Length;
                if (item.Fruit.PathDistance < 0) item.Fruit.PathDistance += path.Length;

                path.Evaluate(item.Fruit.PathDistance, out Vector3 position, out Quaternion rotation);
                Vector2 tangent = rotation * Vector3.up;
                Vector2 normal = new(-tangent.y, tangent.x);

                // A controlled micro-sway makes the lane feel alive without letting
                // physics contacts turn the fruit stream into a fight.
                float laneWobble = Mathf.Sin(phase) * 0.042f;
                float forwardWobble = Mathf.Sin(phase * 0.63f + 1.1f) * 0.018f;
                float angleWobble = Mathf.Sin(phase * 0.83f + 0.7f) * 2.8f;
                Vector3 displayPosition = position
                    + (Vector3)(normal * laneWobble)
                    + (Vector3)(tangent.normalized * forwardWobble);
                Quaternion displayRotation = rotation * Quaternion.Euler(0f, 0f, angleWobble);
                item.Fruit.MoveStable(displayPosition, displayRotation);
            }
        }

        private void UpdateEvenSpacing(float idealSpacing)
        {
            if (stableFruits.Count < 2 || spacingRelaxRemaining <= 0f) return;
            if (spacingDelayRemaining > 0f)
            {
                spacingDelayRemaining -= Time.fixedDeltaTime;
                return;
            }

            spacingRelaxRemaining -= Time.fixedDeltaTime;
            float maximumSpeedOffset = Mathf.Max(0.35f, maxSpacingShiftPerStep * 45f);
            for (int index = 0; index < stableFruits.Count; index++)
            {
                LoopFruit previous = stableFruits[(index - 1 + stableFruits.Count) % stableFruits.Count];
                LoopFruit current = stableFruits[index];
                LoopFruit next = stableFruits[(index + 1) % stableFruits.Count];
                float gapBehind = Mathf.Repeat(
                    current.Fruit.PathDistance - previous.Fruit.PathDistance, path.Length);
                float gapAhead = Mathf.Repeat(
                    next.Fruit.PathDistance - current.Fruit.PathDistance, path.Length);

                // More room ahead than behind => accelerate gently; the reverse
                // slows down gently. All fruit remain moving forward at all times.
                float balance = gapAhead - gapBehind;
                float speedOffset = Mathf.Clamp(
                    balance * spacingCorrection * 1.55f,
                    -maximumSpeedOffset,
                    maximumSpeedOffset);
                current.DesiredSpeed = Mathf.Max(speed * 0.62f,
                    current.DesiredSpeed + speedOffset);
            }
        }

        private void ResolveSoftLaneContacts(float idealSpacing)
        {
            // This is only the initial overlap breaker. The neighbour-speed solver
            // above owns the final even spacing, including dense high-count loops.
            float requiredSpacing = Mathf.Max(0.1f,
                Mathf.Min(minimumSpacing, idealSpacing * 0.72f));
            float response = Mathf.Max(0.5f, softContactStrength) * Time.fixedDeltaTime;

            for (int firstIndex = 0; firstIndex < active.Count; firstIndex++)
            {
                LoopFruit first = active[firstIndex];
                if (first.Fruit == null || first.Fruit.State != FruitState.OnLoop) continue;

                for (int secondIndex = firstIndex + 1; secondIndex < active.Count; secondIndex++)
                {
                    LoopFruit second = active[secondIndex];
                    if (second.Fruit == null || second.Fruit.State != FruitState.OnLoop) continue;

                    float forwardGap = Mathf.Repeat(
                        second.Fruit.PathDistance - first.Fruit.PathDistance,
                        path.Length);
                    bool secondIsAhead = forwardGap <= path.Length * 0.5f;
                    float separation = secondIsAhead ? forwardGap : path.Length - forwardGap;
                    if (separation >= requiredSpacing) continue;

                    float overlap = requiredSpacing - separation;
                    float correction = Mathf.Min(overlap * response * spacingCorrection, 0.045f);
                    float firstDirection = secondIsAhead ? -1f : 1f;
                    first.Fruit.PathDistance = Mathf.Repeat(
                        first.Fruit.PathDistance + firstDirection * correction * 0.5f,
                        path.Length);
                    second.Fruit.PathDistance = Mathf.Repeat(
                        second.Fruit.PathDistance - firstDirection * correction * 0.5f,
                        path.Length);

                    // Share speed gradually at contact instead of exchanging velocity in
                    // one rigid-body impulse. This reads as a soft push along the lane.
                    float sharedSpeed = (first.Fruit.CurrentSpeed + second.Fruit.CurrentSpeed) * 0.5f;
                    float share = contactVelocitySharing * Time.fixedDeltaTime * 10f;
                    first.Fruit.CurrentSpeed = Mathf.Lerp(first.Fruit.CurrentSpeed, sharedSpeed, share);
                    second.Fruit.CurrentSpeed = Mathf.Lerp(second.Fruit.CurrentSpeed, sharedSpeed, share);
                }
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
                spacingDelayRemaining = 0f;
                spacingRelaxRemaining = Mathf.Max(spacingRelaxRemaining, spacingSettleDuration);
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
