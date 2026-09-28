using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class Fruit
    {        public void Release(Vector2 inheritedVelocity)
        {
            ReleasedState.Release(this, inheritedVelocity);
        }

        internal bool clearRecoveryArmed;
        internal bool touchingSupport;
        internal float jammedDuration;
        internal float recoveryCooldown;
        internal int recoveryAttempts;
        internal float lastContactTime;

        // Only arm this after the last bubble bursts. This leaves normal resting
        // fruit alone while the level still has active bubbles.
        public void ArmClearRecovery()
        {
            if (state != FruitStatus.Released && state != FruitStatus.Jammed) return;
            clearRecoveryArmed = true;
            jammedDuration = 0f;
            recoveryCooldown = 0f;
            recoveryAttempts = 0;
            touchingSupport = false;
        }



        private void OnCollisionStay2D(Collision2D collision)
        {
            ApplyControlledLoopPush(collision, 1f);
            if (!clearRecoveryArmed) return;
            touchingSupport = true;
            lastContactTime = Time.time;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            ApplyControlledLoopPush(collision, 0.65f);
        }

        private void ApplyControlledLoopPush(Collision2D collision, float sustainedWeight)
        {
            if (state != FruitStatus.OnLoop || body == null) return;
            Fruit other = collision.collider.GetComponentInParent<Fruit>();
            if (other == null || other == this || other.State != FruitStatus.OnLoop) return;
            if (collision.contactCount == 0) return;
            if (body.linearVelocity.sqrMagnitude <= other.body.linearVelocity.sqrMagnitude + 0.0025f)
                return;

            Vector2 awayFromThis = other.body.position - body.position;
            if (awayFromThis.sqrMagnitude < 0.0001f)
                awayFromThis = -collision.GetContact(0).normal;
            awayFromThis.Normalize();
            Vector2 pushDirection = (awayFromThis + Vector2.up * 0.22f).normalized;
            float closingSpeed = Mathf.Max(0f,
                Vector2.Dot(body.linearVelocity - other.body.linearVelocity, awayFromThis));
            float pushForce = Mathf.Lerp(2.2f, 5.2f,
                Mathf.InverseLerp(0f, 3.5f, closingSpeed)) * sustainedWeight;

            // Contact is real; only its extra game-feel force is authored. Applying
            // Force over contact frames makes the rear fruit steadily press the
            // front fruit forward/up instead of producing a one-frame impulse.
            other.body.AddForce(pushDirection * pushForce, ForceMode2D.Force);
        }

        private void OnDisable()
        {
            clearRecoveryArmed = false;
            touchingSupport = false;
            jammedDuration = 0f;
            recoveryAttempts = 0;
        }

        // Restore contacts with every bubble after a fruit leaves its container.
        internal void RestoreReleasedFruitCollisions()
        {
            BubbleActor[] bubbles = FindObjectsByType<BubbleActor>(FindObjectsSortMode.None);
            for (int index = 0; index < bubbles.Length; index++)
                bubbles[index].EnableCollisionWithReleasedFruit(bodyCollider);
        }

        public void BeginStableMotion(float distance)
        {
            StableOnLoopState.BeginStableMotion(this, distance);
        }

        public void BeginPhysicalLoopMotion(float distance)
        {
            StableOnLoopState.BeginPhysicalLoopMotion(this, distance);
        }

        public void EnterCongestion(bool loopIsFull)
        {
            EntryCongestionState.EnterCongestion(this, loopIsFull);
        }

        public void BeginLaneCapture(float distance)
        {
            MergingToLaneState.BeginLaneCapture(this, distance);
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
            OnLoopState.CompleteLaneCapture(this, distance, entryVelocity);
        }

        public void DisablePhysics()
        {
            body.simulated = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }


        private void Update() => FruitManager.ExecuteRecovery(this);
    }
}
