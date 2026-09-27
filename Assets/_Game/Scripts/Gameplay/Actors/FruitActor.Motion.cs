using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class FruitActor
    {        public void Release(Vector2 inheritedVelocity)
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
            RestoreReleasedFruitCollisions();
        }

        private bool clearRecoveryArmed;
        private bool touchingSupport;
        private float jammedDuration;
        private float recoveryCooldown;
        private int recoveryAttempts;
        private float lastContactTime;

        // Only arm this after the last bubble bursts. This leaves normal resting
        // fruit alone while the level still has active bubbles.
        public void ArmClearRecovery()
        {
            if (state != FruitState.Released && state != FruitState.Jammed) return;
            clearRecoveryArmed = true;
            jammedDuration = 0f;
            recoveryCooldown = 0f;
            recoveryAttempts = 0;
            touchingSupport = false;
        }

        private void FixedUpdate()
        {
            if (!clearRecoveryArmed || body == null || !body.simulated
                || (state != FruitState.Released && state != FruitState.Jammed)) return;

            recoveryCooldown = Mathf.Max(0f, recoveryCooldown - Time.fixedDeltaTime);
            bool hasRecentSupport = touchingSupport && Time.time - lastContactTime <= 0.12f;
            touchingSupport = false;
            if (!hasRecentSupport || body.linearVelocity.sqrMagnitude > 0.055f)
            {
                jammedDuration = 0f;
                return;
            }

            jammedDuration += Time.fixedDeltaTime;
            if (jammedDuration < 0.48f || recoveryCooldown > 0f || recoveryAttempts >= 3) return;

            float direction = ((GetInstanceID() + recoveryAttempts) & 1) == 0 ? -1f : 1f;
            body.linearVelocity = new Vector2(direction * 0.72f, Mathf.Min(body.linearVelocity.y, -0.12f));
            body.AddTorque(direction * 0.018f, ForceMode2D.Impulse);
            recoveryAttempts++;
            recoveryCooldown = 0.48f;
            jammedDuration = 0f;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!clearRecoveryArmed) return;
            touchingSupport = true;
            lastContactTime = Time.time;
        }

        private void OnDisable()
        {
            clearRecoveryArmed = false;
            touchingSupport = false;
            jammedDuration = 0f;
            recoveryAttempts = 0;
        }

        // Restore contacts with every bubble after a fruit leaves its container.
        private void RestoreReleasedFruitCollisions()
        {
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


    }
}
