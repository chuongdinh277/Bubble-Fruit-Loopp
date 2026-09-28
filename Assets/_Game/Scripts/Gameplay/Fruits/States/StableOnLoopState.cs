using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class StableOnLoopState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {

        }


        internal static void BeginStableMotion(Fruit fruit, float distance)
        {
            fruit.pathDistance = distance;
            fruit.SetState(FruitStatus.StableOnLoop);

        }

        internal static void BeginPhysicalLoopMotion(Fruit fruit, float distance)
        {
            fruit.pathDistance = distance;
            fruit.ChangeFruitStateTo(FruitStates.For(FruitStatus.StableOnLoop), false);
            fruit.body.simulated = true;
            fruit.body.bodyType = RigidbodyType2D.Dynamic;
            fruit.body.gravityScale = 0f;
            fruit.body.linearDamping = 0.4f;
            fruit.body.angularDamping = 2.5f;
            fruit.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            fruit.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            fruit.UseLoopFrictionlessMaterial();

        }
    }
}
