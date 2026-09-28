using System.Collections.Generic;
using BubbleFruitLoop.Pooling;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class Fruit
    {
        private void ApplyLaneAcceleration(float maxSpeed, Vector2 driveAcceleration, Vector2 laneAcceleration)
        {
            Vector2 acceleration = Vector2.ClampMagnitude(driveAcceleration + laneAcceleration, 28f);
            // Normalize force accumulation to a 50 Hz reference while updating
            // every rendered frame; no loop movement depends on FixedUpdate.
            float frameForceScale = Time.deltaTime * 50f;
            body.AddForce(acceleration * body.mass * frameForceScale, ForceMode2D.Force);
            float currentSpeed = body.linearVelocity.magnitude;
            if (currentSpeed > maxSpeed)
            {
                Vector2 excessVelocity = body.linearVelocity.normalized * (currentSpeed - maxSpeed);
                body.AddForce(-excessVelocity * body.mass * 8f * frameForceScale, ForceMode2D.Force);
            }
        }

        private void MaintainMinimumForwardSpeed(float targetSpeed, Vector2 pathDirection, float forwardSpeed, ref Vector2 driveAcceleration)
        {
            float minimumForwardSpeed = targetSpeed * 0.58f;
            if (forwardSpeed < minimumForwardSpeed)
            {
                // Each fruit owns its conveyor drive. Contact can slow it, but
                // another fruit is never required to make it start moving.
                driveAcceleration += pathDirection * ((minimumForwardSpeed - forwardSpeed) * 16f);
            }
        }
    }
}
