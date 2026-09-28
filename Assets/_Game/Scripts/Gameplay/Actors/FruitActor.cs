using System.Collections.Generic;
using BubbleFruitLoop.Pooling;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class FruitActor : MonoBehaviour, IPoolable
    {
        private Transform cachedTransform;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private FruitType type;
        [SerializeField] private FruitState state;
        private Sprite configuredSprite;
        private float pathDistance;
        private float transientRemaining;

        public Transform CachedTransform => cachedTransform != null ? cachedTransform : cachedTransform = transform;
        public FruitType Type => type;
        public FruitState State => state;
        public SpriteRenderer VisualSpriteRenderer 
        {
            get
            {
                if (visualRenderer is SpriteRenderer sr) return sr;
                return GetComponentInChildren<SpriteRenderer>(true);
            }
        }
        public Sprite ConfiguredSprite
        {
            get
            {
                // Always return the per-instance sprite, not the static catalog.
                // The catalog maps type→sprite globally, so it returns the LAST sprite
                // assigned to any fruit of that type — wrong when pool reuses actors.
                if (configuredSprite != null) return configuredSprite;
                SpriteRenderer sr = visualRenderer as SpriteRenderer;
                if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);
                return sr != null ? sr.sprite : null;
            }
        }
        public Collider2D BodyCollider => bodyCollider;
        public Vector2 LinearVelocity => body != null ? body.linearVelocity : Vector2.zero;
        public float PathDistance { get => pathDistance; set => pathDistance = value; }
        public float CurrentSpeed { get; set; }
        public float LaneOffset { get; set; }
        public float TransientRemaining { get => transientRemaining; set => transientRemaining = value; }

        private void Awake()
        {
            cachedTransform = transform;
            if (visualRenderer is SpriteRenderer spriteRenderer)
                configuredSprite = spriteRenderer.sprite;
        }
        private void Start() => SetState(state);

        public void Initialize(Rigidbody2D physicsBody, Collider2D physicsCollider, Renderer renderer)
        {
            cachedTransform = transform;
            body = physicsBody;
            bodyCollider = physicsCollider;
            visualRenderer = renderer;
        }

        public void Configure(FruitType fruitType, Color color)
        {
            type = fruitType;
        }

        public void SetSharedMaterial(Material material)
        {
            if (visualRenderer == null || material == null) return;
            
            // Fruit materials belong to the box palette, not to the fruit artwork.
            // Never apply them to an actor: pooled instances must retain their exact
            // per-instance sprite, tint, and material from loop through docking.
        }
        
        public void SetSprite(Sprite sprite)
        {
            if (sprite == null) return;
            configuredSprite = sprite;
            SpriteRenderer sr = visualRenderer as SpriteRenderer;
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) sr.sprite = configuredSprite;
        }

        public void SetState(FruitState nextState)
        {
            state = nextState;
            ConfigureStatePhysics(nextState);
            ConfigureStateCollider(nextState);
        }

        // Select whether the actor participates in the physics simulation.
        private void ConfigureStatePhysics(FruitState nextState)
        {
            bool usesPhysics = nextState is FruitState.Released or FruitState.Jammed or FruitState.IntakeWaiting
                or FruitState.EnteringLoop or FruitState.Transient or FruitState.StableOnLoop
                or FruitState.EntryCongestion or FruitState.Admitted or FruitState.MergingToLane
                or FruitState.OnLoop or FruitState.WaitingFull;
            body.simulated = usesPhysics;
            if (nextState == FruitState.InsideBubble)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.None;
            }
            else if (nextState == FruitState.StableOnLoop)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        // Keep pooled fruit colliders disabled while retaining other state behavior.
        private void ConfigureStateCollider(FruitState nextState)
        {
            bodyCollider.enabled = nextState != FruitState.Pooled;
        }

        public void PrepareForBoxSlot(float targetDiameter, int sortingOrder)
        {
            DisablePhysics();
            DisableSlotCollider();
            ConfigureSlotRenderer(sortingOrder);
            ResizeForSlot(targetDiameter);
        }

        // Hide collision while the fruit is displayed inside a box.
        private void DisableSlotCollider()
        {
            if (bodyCollider != null) bodyCollider.enabled = false;
        }

        // Preserve the actor artwork and place it above the box body.
        private void ConfigureSlotRenderer(int sortingOrder)
        {
            if (visualRenderer == null) return;
            visualRenderer.enabled = true;
            visualRenderer.sortingOrder = sortingOrder;
        }

        // Fit the fruit artwork to the authored box slot diameter.
        private void ResizeForSlot(float targetDiameter)
        {
            if (targetDiameter > 0f && visualRenderer is SpriteRenderer spriteRenderer && spriteRenderer.sprite != null)
            {
                Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
                float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
                if (largestSide > 0.0001f)
                {
                    float scale = targetDiameter / largestSide;
                    CachedTransform.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        public void BeginIntakeWaiting()
        {
            state = FruitState.IntakeWaiting;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void MoveInIntakeQueue(Vector2 target, float moveSpeed)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Kinematic) return;
            body.MovePosition(Vector2.MoveTowards(body.position, target, moveSpeed * Time.fixedDeltaTime));
        }

        public void BeginIntakeDrop()
        {
            state = FruitState.Transient;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0.65f;
            body.linearDamping = 0.45f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void MoveStable(Vector3 position, Quaternion rotation)
        {
            if (body.simulated)
            {
                body.MovePosition(position);
                body.MoveRotation(rotation.eulerAngles.z);
            }
            else
            {
                CachedTransform.position = position;
                CachedTransform.rotation = rotation;
            }
        }

        public void PullToward(Vector2 target, float strength, float maxSpeed)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic) return;
            Vector2 desiredVelocity = Vector2.ClampMagnitude(target - body.position, 1f) * maxSpeed;
            Vector2 steering = (desiredVelocity - body.linearVelocity) * strength;
            body.AddForce(steering, ForceMode2D.Force);
        }

        public void FlowTowardEntry(Vector2 target, float response, float targetSpeed,
            float lateralSteering)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic) return;

            Vector2 flowDirection = GetEntryFlowDirection(target, lateralSteering);
            ApplyEntryFlowVelocity(flowDirection, response, targetSpeed);
        }

        // Bias horizontal steering while preserving a mostly vertical chute flow.
        private Vector2 GetEntryFlowDirection(Vector2 target, float lateralSteering)
        {
            Vector2 offset = target - body.position;
            Vector2 flowDirection = new(offset.x * lateralSteering, offset.y);
            if (flowDirection.sqrMagnitude < 0.0001f) flowDirection = Vector2.down;
            flowDirection.Normalize();
            return flowDirection;
        }

        // Smoothly converge toward the requested entry speed.
        private void ApplyEntryFlowVelocity(Vector2 flowDirection, float response, float targetSpeed)
        {
            // Direct velocity convergence makes all fruit descend at almost the same
            // pace, while MoveTowards still leaves enough softness for wall contacts.
            Vector2 desiredVelocity = flowDirection * targetSpeed;
            float velocityStep = Mathf.Max(1f, response) * Time.fixedDeltaTime;
            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity, desiredVelocity, velocityStep);
            body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, targetSpeed * 1.08f);
        }

        public void FollowPhysicalPath(Vector2 target, Vector2 tangent, float targetSpeed,
            float centeringStrength, float maxSpeed)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic) return;
            Vector2 pathDirection = tangent.normalized;
            Vector2 pathNormal = new(-pathDirection.y, pathDirection.x);

            // A damped spring keeps one lane without overwriting collision
            // velocity. Unity's contact impulse remains intact and the fruit then
            // settles smoothly back to the centre whenever room opens up.
            float forwardSpeed = Vector2.Dot(body.linearVelocity, pathDirection);
            float normalSpeed = Vector2.Dot(body.linearVelocity, pathNormal);
            float normalError = Vector2.Dot(target - body.position, pathNormal);
            const float driveResponse = 6.5f;
            const float lateralDamping = 5.2f;
            Vector2 driveAcceleration = pathDirection
                * ((targetSpeed - forwardSpeed) * driveResponse);
            Vector2 laneAcceleration = pathNormal
                * (normalError * centeringStrength - normalSpeed * lateralDamping);
            Vector2 acceleration = Vector2.ClampMagnitude(
                driveAcceleration + laneAcceleration, 20f);
            body.AddForce(acceleration * body.mass, ForceMode2D.Force);

            float currentSpeed = body.linearVelocity.magnitude;
            if (currentSpeed > maxSpeed)
            {
                Vector2 excessVelocity = body.linearVelocity.normalized
                    * (currentSpeed - maxSpeed);
                body.AddForce(-excessVelocity * body.mass * 8f, ForceMode2D.Force);
            }
        }

        // Keep the fruit moving forward while retaining limited sideways contact motion.
        private void MaintainPathForwardVelocity(Vector2 pathDirection, float targetSpeed)
        {
            float currentForwardSpeed = Vector2.Dot(body.linearVelocity, pathDirection);
            float forwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetSpeed, 7f * Time.fixedDeltaTime);

            // Preserve a limited amount of sideways collision movement while driving the fruit
            // forward at the Inspector's requested speed.
            Vector2 sidewaysVelocity = body.linearVelocity - pathDirection * currentForwardSpeed;
            sidewaysVelocity = Vector2.ClampMagnitude(sidewaysVelocity, 0.78f);
            body.linearVelocity = pathDirection * forwardSpeed + sidewaysVelocity;
        }

        // Pull the actor toward the authored path center.
        private void ApplyPathCentering(Vector2 target, float centeringStrength)
        {
            Vector2 centeringForce = Vector2.ClampMagnitude(target - body.position, 0.38f) * centeringStrength;
            body.AddForce(Vector2.ClampMagnitude(centeringForce, 11f), ForceMode2D.Force);
        }

        // Prevent collision response from exceeding the movement speed limit.
        private void ClampPathVelocity(float targetSpeed, float maxSpeed)
        {
            float effectiveSpeedLimit = Mathf.Max(maxSpeed, targetSpeed * 1.35f);
            body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, effectiveSpeedLimit);
        }

        public void OnSpawned() => SetState(FruitState.InsideBubble);

        public void OnDespawned()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            SetState(FruitState.Pooled);
        }
    }
}

