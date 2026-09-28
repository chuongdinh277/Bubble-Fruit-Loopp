using System.Collections.Generic;
using BubbleFruitLoop.Pooling;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BubbleFruitLoop.Gameplay", "Assembly-CSharp", "FruitActor")]
    public sealed partial class Fruit : MonoBehaviour, IPoolable
    {
        private Transform cachedTransform;
        [SerializeField]
        internal Rigidbody2D body;
        [SerializeField]
        private Collider2D bodyCollider;
        [SerializeField]
        private Renderer visualRenderer;
        [SerializeField]
        private FruitType type;
        [SerializeField]
        internal FruitStatus state;
        private Sprite configuredSprite;
        internal float pathDistance;
        private float transientRemaining;
        private float loopRotationVelocity;
        private PhysicsMaterial2D originalPhysicsMaterial;
        private static PhysicsMaterial2D loopFrictionlessMaterial;
        public Transform CachedTransform => cachedTransform != null ? cachedTransform : cachedTransform = transform;
        public FruitType Type => type;
        public FruitStatus State => state;

        public SpriteRenderer VisualSpriteRenderer
        {
            get
            {
                if (visualRenderer is SpriteRenderer sr)
                    return sr;
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
                if (configuredSprite != null)
                    return configuredSprite;
                SpriteRenderer sr = visualRenderer as SpriteRenderer;
                if (sr == null)
                    sr = GetComponentInChildren<SpriteRenderer>(true);
                return sr != null ? sr.sprite : null;
            }
        }

        public Collider2D BodyCollider => bodyCollider;
        public bool IsBodyColliderEnabled => bodyCollider != null && bodyCollider.enabled;
        public Vector2 LinearVelocity => body != null ? body.linearVelocity : Vector2.zero;
        public float PathDistance { get => pathDistance; set => pathDistance = value; }
        public float CurrentSpeed { get; set; }
        public float LaneOffset { get; set; }
        public float TransientRemaining { get => transientRemaining; set => transientRemaining = value; }

        private void Awake()
        {
            cachedTransform = transform;
            if (bodyCollider != null)
                originalPhysicsMaterial = bodyCollider.sharedMaterial;
            if (visualRenderer is SpriteRenderer spriteRenderer)
                configuredSprite = spriteRenderer.sprite;
        }

        internal void Start() => SetState(state);
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
            if (visualRenderer == null || material == null)
                return;
        // Fruit materials belong to the box palette, not to the fruit artwork.
        // Never apply them to an actor: pooled instances must retain their exact
        // per-instance sprite, tint, and material from loop through docking.
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            configuredSprite = sprite;
            SpriteRenderer sr = visualRenderer as SpriteRenderer;
            if (sr == null)
                sr = GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
                sr.sprite = configuredSprite;
        }

        public void SetState(FruitStatus nextState)
        {
            ChangeFruitStateTo(FruitStates.For(nextState));
        }

        // Select whether the actor participates in the physics simulation.
        private void ConfigureStatePhysics(FruitStatus nextState)
        {
            bool usesPhysics = nextState is FruitStatus.Released or FruitStatus.Jammed or FruitStatus.IntakeWaiting or FruitStatus.EnteringLoop or FruitStatus.Transient or FruitStatus.StableOnLoop or FruitStatus.EntryCongestion or FruitStatus.Admitted or FruitStatus.MergingToLane or FruitStatus.OnLoop or FruitStatus.WaitingFull;
            body.simulated = usesPhysics;
            if (nextState == FruitStatus.InsideBubble)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.None;
            }
            else if (nextState == FruitStatus.StableOnLoop)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        // Keep pooled fruit colliders disabled while retaining other state behavior.
        private void ConfigureStateCollider(FruitStatus nextState)
        {
            bodyCollider.enabled = nextState != FruitStatus.Pooled;
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
            if (bodyCollider != null)
                bodyCollider.enabled = false;
        }

        // Preserve the actor artwork and place it above the box body.
        private void ConfigureSlotRenderer(int sortingOrder)
        {
            if (visualRenderer == null)
                return;
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
            IntakeWaitingState.BeginIntakeWaiting(this);
        }

        public void MoveInIntakeQueue(Vector2 target, float moveSpeed)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Kinematic)
                return;
            body.MovePosition(Vector2.MoveTowards(body.position, target, moveSpeed * Time.deltaTime));
        }

        public void BeginIntakeDrop()
        {
            TransientState.BeginIntakeDrop(this);
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
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return;
            Vector2 desiredVelocity = Vector2.ClampMagnitude(target - body.position, 1f) * maxSpeed;
            Vector2 steering = (desiredVelocity - body.linearVelocity) * strength;
            body.AddForce(steering, ForceMode2D.Force);
        }
    // Bias horizontal steering while preserving a mostly vertical chute flow.
    // Smoothly converge toward the requested entry speed.
    // Keep the fruit moving forward while retaining limited sideways contact motion.
    // Pull the actor toward the authored path center.
    // Prevent collision response from exceeding the movement speed limit.
    }
}
