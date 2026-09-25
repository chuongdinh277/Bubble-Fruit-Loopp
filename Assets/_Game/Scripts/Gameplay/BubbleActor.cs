using System.Collections;
using System.Collections.Generic;
using BubbleFruitLoop.Pooling;
using UnityEngine;
using UnityEngine.InputSystem;


namespace BubbleFruitLoop.Gameplay
{
    public sealed class BubbleActor : MonoBehaviour, IPoolable
    {
        [SerializeField] private List<FruitActor> fruits = new(8);
        [SerializeField] private Rigidbody2D physicsBody;
        [SerializeField] private Collider2D outerCollider;
        [SerializeField] private Collider2D innerBoundary;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer[] visualRenderers;
        [SerializeField] private BubbleFruitMotion fruitMotion;
        
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

        private void Start()
        {
            // Stable per-bubble offset keeps the board moving organically instead of
            // making every bubble sway in exactly the same direction at once.
            idlePhase = Mathf.Repeat(transform.position.x * 0.83f + transform.position.y * 1.37f, Mathf.PI * 2f);

            // The bubbles should drift through a viscous medium, not rebound like
            // rubber balls. Apply a safe floor here so older serialized scenes also
            // receive the softer motion without needing to be rebuilt.
            if (physicsBody != null)
            {
                physicsBody.linearDamping = Mathf.Max(physicsBody.linearDamping, 0.85f);
                physicsBody.angularDamping = Mathf.Max(physicsBody.angularDamping, 1.2f);
            }

            if (visualRoot == null && visualRenderers != null && visualRenderers.Length > 0 && visualRenderers[0] != null)
            {
                // Fallback: If no visual root assigned, but we have multiple renderers under a parent, use the parent
                if (visualRenderers.Length > 1 && visualRenderers[0].transform.parent != transform)
                    visualRoot = visualRenderers[0].transform.parent;
                else
                    visualRoot = visualRenderers[0].transform;
            }

            if (visualRoot != null)
            {
                originalVisualScale = visualRoot.localScale;
            }
            
            popped = false;
            if (fruits.Count == 0)
            {
                fruits.AddRange(GetComponentsInChildren<FruitActor>(true));
            }

            if (!popped && fruitMotion != null) fruitMotion.StartMotion();
            
            if (innerBoundary is EdgeCollider2D edge && edge.edgeRadius < 0.05f)
            {
                edge.edgeRadius = 0.1f;
            }

            IsolateFromOtherFruits();
        }

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

        private IEnumerator PlayPopEffect()
        {
            Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            yield return AnimateVisualScale(startScale, startScale * popAnticipationScale, popAnticipationDuration);
            yield return AnimateVisualScale(
                visualRoot != null ? visualRoot.localScale : startScale,
                startScale * popBurstScale,
                popBurstDuration);

            SpawnPopBubbles();
            SetVisualsEnabled(false);

            Vector2 inheritedVelocity = physicsBody != null ? physicsBody.linearVelocity : Vector2.zero;
            Vector2 burstCenter = transform.position;
            for (int index = 0; index < fruits.Count; index++)
            {
                FruitActor fruit = fruits[index];
                if (fruit == null) continue;
                Vector2 radial = (Vector2)fruit.CachedTransform.position - burstCenter;
                if (radial.sqrMagnitude < 0.0025f)
                {
                    float fallbackAngle = (index + 0.5f) / Mathf.Max(1, fruits.Count) * Mathf.PI * 2f;
                    radial = new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle));
                }
                radial.Normalize();

                // A short outward puff separates the fruit silhouettes before
                // gravity takes over. A small lift keeps it readable as a burst,
                // while the capped force prevents fruit escaping the chute.
                float force = Random.Range(
                    Mathf.Min(fruitBurstForceMin, fruitBurstForceMax),
                    Mathf.Max(fruitBurstForceMin, fruitBurstForceMax));
                Vector2 sidewaysVariation = new(-radial.y, radial.x);
                Vector2 burstVelocity = radial * force
                    + Vector2.up * fruitBurstLift
                    + sidewaysVariation * Random.Range(-0.18f, 0.18f);
                fruit.Release(inheritedVelocity + burstVelocity);
            }
            fruits.Clear();
        }

        private IEnumerator AnimateVisualScale(Vector3 from, Vector3 to, float duration)
        {
            if (visualRoot == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                progress = progress * progress * (3f - 2f * progress);
                visualRoot.localScale = Vector3.LerpUnclamped(from, to, progress);
                yield return null;
            }
            visualRoot.localScale = to;
        }

        private void SpawnPopBubbles()
        {
            GameObject effect = new($"{name}_PopBubbles");
            effect.transform.position = transform.position;

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(20, popBubbleCount);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)popBubbleCount)
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.65f;
            shape.radiusThickness = 1f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(0.72f, 0.8f),
                    new Keyframe(1f, 0f)));

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.92f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.65f, 0.65f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            if (visualRenderers != null && visualRenderers.Length > 0 && visualRenderers[^1] != null)
                particleRenderer.sharedMaterial = visualRenderers[^1].sharedMaterial;
            particleRenderer.sortingOrder = 25;

            particles.Play();
            Destroy(effect, 1.5f);
        }

        public void OnSpawned()
        {
            popped = false;
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
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (popped) return;
            float impact = collision.relativeVelocity.magnitude;
            bool hitBubble = collision.collider.GetComponentInParent<BubbleActor>() != null;

            // Bubble-to-bubble contact always gets a tiny readable response, even when
            // the bodies are only slowly pressing against one another.
            float softImpactMultiplier = Mathf.Clamp(impactMultiplier, 0f, 0.04f);
            float contactResponse = impact * softImpactMultiplier;
            if (hitBubble) contactResponse += Mathf.Clamp(bubbleContactKick, 0f, 0.11f);
            squashVelocity -= contactResponse;

        }

        private void UpdateJiggle()
        {
            if (popped || visualRoot == null) return;

            // Clamp legacy Inspector values: older scenes stored a stiffness of 150,
            // which produces a rubber-ball snap instead of a soft bubble response.
            float softStiffness = Mathf.Clamp(springStiffness, 18f, 35f);
            float softDamping = Mathf.Clamp(damping, 8f, 12f);
            float springForce = -softStiffness * currentSquash;
            float dampingForce = -softDamping * squashVelocity;
            squashVelocity += (springForce + dampingForce) * Time.deltaTime;
            currentSquash += squashVelocity * Time.deltaTime;
            // Old scene/prefab data may still contain the former 0.45 value. Keep the
            // runtime cap conservative so existing assets cannot become heavily oval.
            float safeMaxDeformation = Mathf.Clamp(maxDeformation, 0f, 0.08f);
            currentSquash = Mathf.Clamp(currentSquash, -safeMaxDeformation, safeMaxDeformation);

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
