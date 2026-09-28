using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class ReleasedState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {
            if (fruit.ExecutionPhase == FruitExecutionPhase.Recovery) ExecuteRecovery(fruit);
        }

        internal static void ExecuteRecovery(Fruit fruit)
        {
            if (!fruit.clearRecoveryArmed || fruit.body == null || !fruit.body.simulated
                || (fruit.state != FruitStatus.Released && fruit.state != FruitStatus.Jammed)) return;

            fruit.recoveryCooldown = Mathf.Max(0f, fruit.recoveryCooldown - Time.deltaTime);
            bool hasRecentSupport = fruit.touchingSupport && Time.time - fruit.lastContactTime <= 0.12f;
            fruit.touchingSupport = false;
            if (!hasRecentSupport || fruit.body.linearVelocity.sqrMagnitude > 0.055f)
            {
                fruit.jammedDuration = 0f;
                return;
            }

            fruit.jammedDuration += Time.deltaTime;
            if (fruit.jammedDuration < 0.48f || fruit.recoveryCooldown > 0f || fruit.recoveryAttempts >= 3) return;

            float direction = ((fruit.GetInstanceID() + fruit.recoveryAttempts) & 1) == 0 ? -1f : 1f;
            fruit.body.linearVelocity = new Vector2(direction * 0.72f, Mathf.Min(fruit.body.linearVelocity.y, -0.12f));
            fruit.body.AddTorque(direction * 0.018f, ForceMode2D.Impulse);
            fruit.recoveryAttempts++;
            fruit.recoveryCooldown = 0.48f;
            fruit.jammedDuration = 0f;

        }


        internal static void Release(Fruit fruit, Vector2 inheritedVelocity)
        {
            fruit.CachedTransform.SetParent(null, true);
            fruit.SetState(FruitStatus.Released);
            fruit.body.bodyType = RigidbodyType2D.Dynamic;
            fruit.body.freezeRotation = false;
            fruit.body.gravityScale = 2.1f;
            fruit.body.linearDamping = 0.10f;
            fruit.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            fruit.body.linearVelocity = inheritedVelocity;
            fruit.RestoreOriginalPhysicsMaterial();

            // Fruits inside bubbles ignore every outer shell while contained. Restore
            // those contacts on release so falling fruit can land on and press the
            // remaining bubbles instead of passing straight through them.
            fruit.RestoreReleasedFruitCollisions();

        }
    }
}
