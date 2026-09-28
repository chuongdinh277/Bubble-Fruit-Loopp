using System.Collections.Generic;
using BubbleFruitLoop.Pooling;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class Fruit
    {
        public void FlowTowardEntry(Vector2 target, float response, float targetSpeed, float lateralSteering)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return;
            Vector2 flowDirection = GetEntryFlowDirection(target, lateralSteering);
            ApplyEntryFlowVelocity(flowDirection, response, targetSpeed);
        }

        // Bias horizontal steering while preserving a mostly vertical chute flow.
        private Vector2 GetEntryFlowDirection(Vector2 target, float lateralSteering)
        {
            Vector2 offset = target - body.position;
            Vector2 flowDirection = new(offset.x * lateralSteering, offset.y);
            if (flowDirection.sqrMagnitude < 0.0001f)
                flowDirection = Vector2.down;
            flowDirection.Normalize();
            return flowDirection;
        }

        // Smoothly converge toward the requested entry speed.
        private void ApplyEntryFlowVelocity(Vector2 flowDirection, float response, float targetSpeed)
        {
            // Direct velocity convergence makes all fruit descend at almost the same
            // pace, while MoveTowards still leaves enough softness for wall contacts.
            Vector2 desiredVelocity = flowDirection * targetSpeed;
            float velocityStep = Mathf.Max(1f, response) * Time.deltaTime;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desiredVelocity, velocityStep);
            body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, targetSpeed * 1.08f);
        }

        public void FollowPhysicalPath(Vector2 target, Vector2 tangent, float targetSpeed, float centeringStrength, float maxSpeed)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return;
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
            Vector2 driveAcceleration = pathDirection * ((targetSpeed - forwardSpeed) * driveResponse);
            MaintainMinimumForwardSpeed(targetSpeed, pathDirection, forwardSpeed, ref driveAcceleration);
            Vector2 laneAcceleration = pathNormal * (normalError * centeringStrength - normalSpeed * lateralDamping);
            ApplyLaneAcceleration(maxSpeed, driveAcceleration, laneAcceleration);
        }

        public void SmoothRotateOnLoop(float targetAngle, float smoothTime, float maxDegreesPerSecond)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return;
            float smoothedAngle = Mathf.SmoothDampAngle(body.rotation, targetAngle, ref loopRotationVelocity, Mathf.Max(0.01f, smoothTime), Mathf.Max(1f, maxDegreesPerSecond), Time.deltaTime);
            body.MoveRotation(smoothedAngle);
        }

        public void SetBodyColliderEnabled(bool enabled)
        {
            if (bodyCollider != null)
                bodyCollider.enabled = enabled;
        }

        public void ApplySoftLoopForce(Vector2 force)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return;
            body.AddForce(force, ForceMode2D.Force);
        }

        public void SoftMoveToLane(Vector2 target, float response)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, response) * Time.deltaTime);
            body.MovePosition(Vector2.Lerp(body.position, target, blend));
        }

        public float GetWorldCollisionRadius()
        {
            if (bodyCollider == null)
                return 0.2f;
            Vector3 scale = bodyCollider.transform.lossyScale;
            float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            if (bodyCollider is CircleCollider2D circle)
                return Mathf.Max(0.05f, circle.radius * largestScale);
            if (bodyCollider is CapsuleCollider2D capsule)
                return Mathf.Max(0.05f, Mathf.Max(capsule.size.x, capsule.size.y) * largestScale * 0.5f);
            if (bodyCollider is BoxCollider2D box)
                return Mathf.Max(0.05f, Mathf.Max(box.size.x, box.size.y) * largestScale * 0.5f);
            Bounds bounds = bodyCollider.bounds;
            return Mathf.Max(0.05f, Mathf.Max(bounds.extents.x, bounds.extents.y));
        }

        public void SmoothVisualRotation(Quaternion targetRotation, float response)
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, response) * Time.deltaTime);
            CachedTransform.rotation = Quaternion.Slerp(CachedTransform.rotation, targetRotation, blend);
        }

        public void SetSmoothLoopPose(Vector3 position, Quaternion rotation, float rotationResponse)
        {
            bool usePhysicsInterpolation = body != null && body.simulated && body.bodyType == RigidbodyType2D.Kinematic;
            Quaternion currentRotation = usePhysicsInterpolation ? Quaternion.Euler(0f, 0f, body.rotation) : CachedTransform.rotation;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, rotationResponse) * Time.deltaTime);
            Quaternion smoothedRotation = Quaternion.Slerp(currentRotation, rotation, blend);
            if (usePhysicsInterpolation)
            {
                // This method is called from FixedUpdate. MovePosition/Rotation
                // let Rigidbody2D interpolate the render pose between physics ticks
                // instead of Transform and Rigidbody repeatedly overwriting each other.
                body.MovePosition(position);
                body.MoveRotation(smoothedRotation.eulerAngles.z);
                return;
            }

            CachedTransform.position = position;
            CachedTransform.rotation = smoothedRotation;
        }

        internal void UseLoopFrictionlessMaterial()
        {
            if (bodyCollider == null)
                return;
            if (loopFrictionlessMaterial == null)
            {
                loopFrictionlessMaterial = new PhysicsMaterial2D("Runtime Loop Fruit - Frictionless")
                {
                    friction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine2D.Minimum,
                    bounceCombine = PhysicsMaterialCombine2D.Minimum,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            bodyCollider.sharedMaterial = loopFrictionlessMaterial;
        }

        internal void RestoreOriginalPhysicsMaterial()
        {
            if (bodyCollider != null)
                bodyCollider.sharedMaterial = originalPhysicsMaterial;
        }

        // Keep the fruit moving forward while retaining limited sideways contact motion.
        private void MaintainPathForwardVelocity(Vector2 pathDirection, float targetSpeed)
        {
            float currentForwardSpeed = Vector2.Dot(body.linearVelocity, pathDirection);
            float forwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetSpeed, 7f * Time.deltaTime);
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

        public void OnSpawned() => SetState(FruitStatus.InsideBubble);
        public void OnDespawned()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            loopRotationVelocity = 0f;
            SetState(FruitStatus.Pooled);
        }
    }
}
