using System.Collections.Generic;
using BubbleFruitLoop.Pooling;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class FruitActor : MonoBehaviour, IPoolable
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
                // The catalog maps typeâ†’sprite globally, so it returns the LAST sprite
                // assigned to any fruit of that type â€” wrong when pool reuses actors.
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
            bool usesPhysics = nextState is FruitState.Released or FruitState.Jammed or FruitState.IntakeWaiting
                or FruitState.EnteringLoop or FruitState.Transient or FruitState.StableOnLoop
                or FruitState.EntryCongestion or FruitState.Admitted or FruitState.MergingToLane
                or FruitState.OnLoop or FruitState.WaitingFull;
            body.simulated = usesPhysics;
            bodyCollider.enabled = nextState != FruitState.Pooled;
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

        public void Release(Vector2 inheritedVelocity)
        {
            CachedTransform.SetParent(null, true);
            SetState(FruitState.Released);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = false;
            body.gravityScale = 1f;
            body.linearDamping = 1.2f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = inheritedVelocity;

            // Fruits inside bubbles ignore every outer shell while contained. Restore
            // those contacts on release so falling fruit can land on and press the
            // remaining bubbles instead of passing straight through them.
            BubbleActor[] bubbles = FindObjectsByType<BubbleActor>(FindObjectsSortMode.None);
            for (int index = 0; index < bubbles.Length; index++)
                bubbles[index].EnableCollisionWithReleasedFruit(bodyCollider);
        }

        public void BeginStableMotion(float distance)
        {
            pathDistance = distance;
            SetState(FruitState.StableOnLoop);
        }

        public void BeginPhysicalLoopMotion(float distance)
        {
            pathDistance = distance;
            state = FruitState.StableOnLoop;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0.4f;
            body.angularDamping = 2.5f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void EnterCongestion(bool loopIsFull)
        {
            state = loopIsFull ? FruitState.WaitingFull : FruitState.EntryCongestion;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            // Entry motion is velocity-controlled below. A little gravity keeps
            // contact with the sloped chute without making each collision produce a
            // visibly different falling speed.
            body.gravityScale = loopIsFull ? 1f : 0.22f;
            body.linearDamping = loopIsFull ? 1.2f : 0.35f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void BeginLaneCapture(float distance)
        {
            pathDistance = distance;
            state = FruitState.MergingToLane;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
            body.linearDamping = 0.7f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void BlendIntoLane(Vector2 target, Vector2 tangent, float progress, float loopSpeed,
            float attraction, float tangentSteering, float maxSpeed)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic) return;
            float pathWeight = Mathf.SmoothStep(0f, 1f, progress);
            body.gravityScale = Mathf.Lerp(1f, 0f, pathWeight);
            Vector2 attractionForce = (target - body.position) * (attraction * pathWeight);
            Vector2 tangentVelocity = tangent.normalized * loopSpeed;
            Vector2 steeringForce = (tangentVelocity - body.linearVelocity) * (tangentSteering * pathWeight);
            body.AddForce(Vector2.ClampMagnitude(attractionForce + steeringForce, 18f), ForceMode2D.Force);
            body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, maxSpeed);
        }

        public void CompleteLaneCapture(float distance, Vector2 entryVelocity)
        {
            pathDistance = distance;
            state = FruitState.OnLoop;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearDamping = 1f;
            body.angularDamping = 2f;
            body.freezeRotation = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void DisablePhysics()
        {
            body.simulated = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public void PrepareForBoxSlot(float targetDiameter, int sortingOrder)
        {
            DisablePhysics();
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (visualRenderer != null)
            {
                visualRenderer.enabled = true;
                visualRenderer.sortingOrder = sortingOrder;
            }

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

            Vector2 offset = target - body.position;
            Vector2 flowDirection = new(offset.x * lateralSteering, offset.y);
            if (flowDirection.sqrMagnitude < 0.0001f) flowDirection = Vector2.down;
            flowDirection.Normalize();

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
            float currentForwardSpeed = Vector2.Dot(body.linearVelocity, pathDirection);
            float forwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetSpeed, 12f * Time.fixedDeltaTime);

            // Preserve a limited amount of sideways collision movement while driving the fruit
            // forward at the Inspector's requested speed.
            Vector2 sidewaysVelocity = body.linearVelocity - pathDirection * currentForwardSpeed;
            sidewaysVelocity = Vector2.ClampMagnitude(sidewaysVelocity, 0.45f);
            body.linearVelocity = pathDirection * forwardSpeed + sidewaysVelocity;

            Vector2 centeringForce = Vector2.ClampMagnitude(target - body.position, 0.3f) * centeringStrength;
            body.AddForce(Vector2.ClampMagnitude(centeringForce, 18f), ForceMode2D.Force);

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

