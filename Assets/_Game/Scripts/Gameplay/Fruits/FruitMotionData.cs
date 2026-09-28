using UnityEngine;
namespace BubbleFruitLoop.Gameplay
{
    internal sealed class FruitLoopData
    {
            public Fruit Fruit;
            public Vector2 MergeStartPosition;
            public Quaternion MergeStartRotation;
            public Vector2 MergeStartVelocity;
            public float MergeElapsed;
            public float MergeTargetDistance;
            public float MergeDuration;
            public float EntryLift;
            public float VerticalOffset;
            public float VerticalVelocity;
            public float SpacingPushSpeed;
            public float SpacingPushAcceleration;
            public Vector2 WallOffset;
            public Vector2 WallOffsetVelocity;
            public float RollAngle;
            public float RollSpeed;
            public float RollSpeedFactor;

    }
    internal sealed class FruitIntakeData
    {
            public Vector2 StartPosition;
            public Vector2 StartVelocity;
            public Vector2 LandingPosition;
            public float Elapsed;
            public float Duration;
            public Vector2 ArrivalVelocity;

    }
}
