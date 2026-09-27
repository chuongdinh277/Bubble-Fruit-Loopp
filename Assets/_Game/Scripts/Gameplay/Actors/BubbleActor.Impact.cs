using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BubbleActor
    {
        private readonly Dictionary<Collider2D, Vector2> bottomSupportContacts = new();
        private readonly RaycastHit2D[] supportCastHits = new RaycastHit2D[12];
        private float lastDownwardSpeed;
        private float nextLandingBounceTime;

        // Cache the shell meshes once so collisions do not allocate component arrays.
        private void CacheDeformMeshes()
        {
            deformMeshes = visualRoot != null
                ? visualRoot.GetComponentsInChildren<BubbleDeformMesh>(true)
                : System.Array.Empty<BubbleDeformMesh>();
        }

        // Start compressing the lower membrane when it first touches a support.
        private void HandleBubbleImpact(Collision2D collision)
        {
            if (!TryGetBottomContactDirection(collision, out Vector2 contactDirection)) return;
            if (bottomSupportContacts.Count == 0) ApplySoftLandingBounce();
            bottomSupportContacts[collision.collider] = contactDirection;
            SetMembraneSupport(true, contactDirection);
        }

        // Keep a gentle squash while the bubble is still resting on its support.
        private void UpdateBottomSupport(Collision2D collision)
        {
            if (!TryGetBottomContactDirection(collision, out Vector2 contactDirection)) return;
            bottomSupportContacts[collision.collider] = contactDirection;
            SetMembraneSupport(true, contactDirection);
        }

        // Release the support squash so the spring can rebound after separation.
        private void RemoveBottomSupport(Collision2D collision)
        {
            if (!bottomSupportContacts.Remove(collision.collider)) return;
            if (bottomSupportContacts.Count == 0)
            {
                SetMembraneSupport(false, Vector2.down);
                return;
            }

            foreach (Vector2 contactDirection in bottomSupportContacts.Values)
            {
                SetMembraneSupport(true, contactDirection);
                break;
            }
        }

        // Use the lowest actual contact so each shell dents toward its support.
        private bool TryGetBottomContactDirection(Collision2D collision, out Vector2 direction)
        {
            direction = Vector2.down;
            float lowestContact = -0.35f;
            bool foundContact = false;
            for (int index = 0; index < collision.contactCount; index++)
            {
                Vector2 localContact = transform.InverseTransformPoint(collision.GetContact(index).point);
                if (localContact.y > lowestContact) continue;
                lowestContact = localContact.y;
                direction = localContact.sqrMagnitude > 0.001f
                    ? localContact.normalized
                    : Vector2.down;
                foundContact = true;
            }
            return foundContact;
        }

        // Apply support state to both front and back shell meshes.
        private void SetMembraneSupport(bool supported, Vector2 contactDirection)
        {
            if (deformMeshes == null) CacheDeformMeshes();
            for (int index = 0; index < deformMeshes.Length; index++)
            {
                if (deformMeshes[index] != null)
                    deformMeshes[index].SetBottomPressure(supported, contactDirection);
            }
        }

        private void ResetBottomSupport()
        {
            bottomSupportContacts.Clear();
            SetMembraneSupport(false, Vector2.down);
        }

        // Ease the final part of a fall when an upward-facing support is close.
        private void SlowFallNearSupport()
        {
            if (physicsBody == null || outerCollider == null) return;
            if (physicsBody.linearVelocity.y >= 0f)
            {
                SetMembraneApproach(0f);
                return;
            }

            const float brakingDistance = 1.75f;
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.SetLayerMask(Physics2D.AllLayers);
            int hitCount = outerCollider.Cast(Vector2.down, filter, supportCastHits, brakingDistance);
            float closestSupport = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit2D hit = supportCastHits[index];
                if (hit.collider == null || hit.collider == outerCollider || hit.collider == innerBoundary)
                    continue;
                if (hit.normal.y < 0.55f || hit.distance >= closestSupport) continue;
                closestSupport = hit.distance;
            }
            if (float.IsPositiveInfinity(closestSupport))
            {
                SetMembraneApproach(0f);
                return;
            }

            float approach = 1f - Mathf.Clamp01(closestSupport / brakingDistance);
            float slowdown = Mathf.SmoothStep(0f, 1f, approach);
            SetMembraneApproach(Mathf.SmoothStep(0.08f, 0.9f, approach));
            float targetDownSpeed = Mathf.Lerp(4.2f, 0.58f, slowdown);
            float currentDownSpeed = -physicsBody.linearVelocity.y;
            if (currentDownSpeed <= targetDownSpeed) return;

            currentDownSpeed = Mathf.MoveTowards(currentDownSpeed, targetDownSpeed,
                10f * Mathf.Max(0.12f, slowdown) * Time.fixedDeltaTime);
            physicsBody.linearVelocity = new Vector2(
                physicsBody.linearVelocity.x, -currentDownSpeed);
        }

        private void SetMembraneApproach(float pressure)
        {
            if (deformMeshes == null) CacheDeformMeshes();
            for (int index = 0; index < deformMeshes.Length; index++)
                if (deformMeshes[index] != null)
                    deformMeshes[index].SetApproachPressure(pressure, Vector2.down);
        }

        // Add one small, damped lift after a downward support contact.
        private void ApplySoftLandingBounce()
        {
            if (physicsBody == null || Time.time < nextLandingBounceTime
                || lastDownwardSpeed < 0.2f) return;

            Vector2 velocity = physicsBody.linearVelocity;
            float bounceSpeed = Mathf.Clamp(lastDownwardSpeed * 0.28f, 0.12f, 0.24f);
            velocity.y = Mathf.Max(velocity.y, bounceSpeed);
            physicsBody.linearVelocity = velocity;
            nextLandingBounceTime = Time.time + 0.65f;
        }
    }
}
