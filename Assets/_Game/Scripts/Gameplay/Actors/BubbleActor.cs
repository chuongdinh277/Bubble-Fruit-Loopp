using System.Collections;
using System.Collections.Generic;
using BubbleFruitLoop.Pooling;
using UnityEngine;
using UnityEngine.InputSystem;


namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BubbleActor : MonoBehaviour, IPoolable
    {
        [SerializeField] private List<FruitActor> fruits = new(8);
        [SerializeField] private Rigidbody2D physicsBody;
        [SerializeField] private Collider2D outerCollider;
        [SerializeField] private Collider2D innerBoundary;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer[] visualRenderers;
        [SerializeField] private BubbleFruitMotion fruitMotion;
        private BubbleDeformMesh[] deformMeshes;
        
        [Header("Jiggle Physics")]
        [SerializeField, Min(0f)] private float springStiffness = 30f;
        [SerializeField, Min(0f)] private float damping = 9f;
        [SerializeField, Min(0f)] private float impactMultiplier = 0.025f;
        [SerializeField, Min(0f)] private float bubbleContactKick = 0.1f;
        [SerializeField, Range(0f, 0.2f)] private float maxDeformation = 0.08f;

        [Header("Idle Motion")]
        [SerializeField, Min(0f)] private float idleHorizontalForce = 0.14f;
        [SerializeField, Min(0f)] private float idleVerticalForce = 0.04f;
        [SerializeField, Min(0f)] private float idleFrequency = 0.9f;
        [SerializeField, Min(0f)] private float idleTorque = 0.008f;
        [SerializeField, Range(0f, 1f)] private float idleBuoyancy = 0.72f;
        [SerializeField, Range(0f, 0.6f)] private float idleBuoyancyPulse = 0.38f;

        [Header("Pop Effect")]
        [SerializeField, Min(0.01f)] private float popAnticipationDuration = 0.075f;
        [SerializeField, Range(0.6f, 1f)] private float popAnticipationScale = 0.84f;
        [SerializeField, Min(0.01f)] private float popBurstDuration = 0.045f;
        [SerializeField, Range(1f, 1.2f)] private float popBurstScale = 1.06f;
        [SerializeField, Range(4, 20)] private int popBubbleCount = 11;
        [SerializeField, Min(0f)] private float fruitBurstForceMin = 1.05f;
        [SerializeField, Min(0f)] private float fruitBurstForceMax = 1.75f;
        [SerializeField, Min(0f)] private float fruitBurstLift = 0.55f;
        
        private Vector3 originalVisualScale = Vector3.one;
        private float currentSquash; 
        private float squashVelocity;
        private float idlePhase;
        
        private bool popped;

        public bool IsPopped => popped;
        public Collider2D ObstacleCollider => outerCollider != null ? outerCollider : innerBoundary;

        private void IsolateFromOtherFruits()
        {
            FruitActor[] allFruits = FindObjectsByType<FruitActor>(FindObjectsSortMode.None);
            for (int i = 0; i < allFruits.Length; i++)
            {
                FruitActor f = allFruits[i];
                if (f != null && f.BodyCollider != null)
                {
                    if (outerCollider != null) 
                    {
                        Physics2D.IgnoreCollision(outerCollider, f.BodyCollider, true);
                    }
                    
                    if (innerBoundary != null && !fruits.Contains(f))
                    {
                        Physics2D.IgnoreCollision(innerBoundary, f.BodyCollider, true);
                    }
                }
            }
        }

        public void Initialize(Collider2D obstacle, Renderer renderer)
        {
            innerBoundary = obstacle;
            visualRenderers = new[] { renderer };
            visualRoot = renderer.transform;
        }

        public void Initialize(Collider2D boundary, Renderer[] renderers, BubbleFruitMotion motion)
        {
            innerBoundary = boundary;
            visualRenderers = renderers;
            fruitMotion = motion;
            if (renderers.Length > 0 && renderers[0] != null)
                visualRoot = renderers[0].transform.parent != transform ? renderers[0].transform.parent : renderers[0].transform;
        }

        public void ConfigurePhysics(Rigidbody2D body, Collider2D outside, Collider2D inside)
        {
            physicsBody = body;
            outerCollider = outside;
            innerBoundary = inside;
        }

        public void AddFruit(FruitActor fruit)
        {
            if (fruit != null)
            {
                fruits.Add(fruit);
                if (outerCollider != null && fruit.BodyCollider != null)
                {
                    Physics2D.IgnoreCollision(outerCollider, fruit.BodyCollider, true);
                }
            }
        }

        public void ReplaceFruits(IEnumerable<FruitActor> replacements)
        {
            fruits.Clear();
            if (replacements == null) return;
            foreach (FruitActor fruit in replacements) AddFruit(fruit);
        }

        public void EnableCollisionWithReleasedFruit(Collider2D fruitCollider)
        {
            if (outerCollider == null || fruitCollider == null) return;
            Physics2D.IgnoreCollision(outerCollider, fruitCollider, false);
        }

        public void Pop()
        {
            if (popped) return;
            popped = true;
            if (outerCollider != null) outerCollider.enabled = false;
            if (innerBoundary != null) innerBoundary.enabled = false;
            if (fruitMotion != null) fruitMotion.StopMotion();
            if (physicsBody != null) physicsBody.simulated = false;
            StartCoroutine(PlayPopEffect());
        }

        public void OnSpawned()
        {
            popped = false;
            ResetBottomSupport();
            currentSquash = 0f;
            squashVelocity = 0f;
            if (visualRoot != null && visualRoot != transform)
            {
                visualRoot.localScale = originalVisualScale;
            }
            if (outerCollider != null) outerCollider.enabled = true;
            if (innerBoundary != null) innerBoundary.enabled = true;
            if (physicsBody != null) physicsBody.simulated = true;
            SetVisualsEnabled(true);
            if (fruitMotion != null) fruitMotion.StartMotion();
            IsolateFromOtherFruits();
        }

        public void OnDespawned()
        {
            ResetBottomSupport();
            fruits.Clear();
            popped = true;
        }

        private void Update()
        {
            UpdateJiggle();
            
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                if (Camera.main == null) return;
                Vector3 screenPos = Pointer.current.position.ReadValue();
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
                if (ObstacleCollider != null && ObstacleCollider.OverlapPoint(worldPos))
                {
                    Pop();
                }
            }
        }

        private void FixedUpdate()
        {
            if (popped || physicsBody == null || !physicsBody.simulated) return;

            float time = Time.fixedTime * Mathf.Max(0f, idleFrequency) + idlePhase;
            float gravityForce = -Physics2D.gravity.y * physicsBody.mass * physicsBody.gravityScale;
            float buoyancyWave = Mathf.Sin(time * 0.61f + idlePhase * 0.47f);
            float buoyancyFactor = Mathf.Max(0f, idleBuoyancy + buoyancyWave * idleBuoyancyPulse);
            Vector2 idleForce = new(
                Mathf.Sin(time) * idleHorizontalForce,
                buoyancyWave * idleVerticalForce + gravityForce * buoyancyFactor);

            physicsBody.AddForce(idleForce, ForceMode2D.Force);
            physicsBody.AddTorque(Mathf.Sin(time * 0.73f + 1.1f) * idleTorque, ForceMode2D.Force);
            SlowFallNearSupport();
            lastDownwardSpeed = Mathf.Max(0f, -physicsBody.linearVelocity.y);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (popped) return;
            float impact = collision.relativeVelocity.magnitude;
            bool hitBubble = collision.collider.GetComponentInParent<BubbleActor>() != null;

            // Bubble-to-bubble contact always gets a tiny readable response, even when
            // the bodies are only slowly pressing against one another.
            float softImpactMultiplier = Mathf.Clamp(impactMultiplier, 0f, 0.015f);
            float contactResponse = impact * softImpactMultiplier;
            if (hitBubble) contactResponse += Mathf.Clamp(bubbleContactKick, 0f, 0.035f);
            squashVelocity -= contactResponse;
            HandleBubbleImpact(collision);

        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            UpdateBottomSupport(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            RemoveBottomSupport(collision);
        }

        private void UpdateJiggle()
        {
            if (popped || visualRoot == null) return;

            // Clamp legacy Inspector values: older scenes stored a stiffness of 150,
            // which produces a rubber-ball snap instead of a soft bubble response.
            float softStiffness = Mathf.Clamp(springStiffness, 8f, 14f);
            float softDamping = Mathf.Clamp(damping, 12f, 18f);
            UpdateSquashMotion(softStiffness, softDamping);
            ApplySquashScale();
        }

        // Integrate the damped spring and constrain legacy tuning values.
        private void UpdateSquashMotion(float softStiffness, float softDamping)
        {
            float springForce = -softStiffness * currentSquash;
            float dampingForce = -softDamping * squashVelocity;
            squashVelocity += (springForce + dampingForce) * Time.deltaTime;
            currentSquash += squashVelocity * Time.deltaTime;
            // Old scene/prefab data may still contain the former 0.45 value. Keep the
            // runtime cap conservative so existing assets cannot become heavily oval.
            float safeMaxDeformation = Mathf.Clamp(maxDeformation, 0f, 0.035f);
            currentSquash = Mathf.Clamp(currentSquash, -safeMaxDeformation, safeMaxDeformation);

        }

        // Apply the current squash to the visual child without scaling colliders.
        private void ApplySquashScale()
        {
            float scaleY = 1f + currentSquash;
            float scaleX = 1f - currentSquash * 0.85f; 
            
            if (visualRoot != transform) // Only scale if it's a child to avoid scaling colliders!
            {
                visualRoot.localScale = new Vector3(
                    originalVisualScale.x * scaleX, 
                    originalVisualScale.y * scaleY, 
                    originalVisualScale.z
                );
            }
        }

        private void SetVisualsEnabled(bool isEnabled)
        {
            for (int index = 0; index < visualRenderers.Length; index++)
                visualRenderers[index].enabled = isEnabled;
        }
    }
}

