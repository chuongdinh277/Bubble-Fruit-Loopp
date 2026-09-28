using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class TransientState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {

        }


        internal static void BeginIntakeDrop(Fruit fruit)
        {
            fruit.ChangeFruitStateTo(FruitStates.For(FruitStatus.Transient), false);
            fruit.body.simulated = true;
            fruit.body.bodyType = RigidbodyType2D.Dynamic;
            fruit.body.gravityScale = 0.65f;
            fruit.body.linearDamping = 0.45f;
            fruit.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            fruit.body.interpolation = RigidbodyInterpolation2D.Interpolate;

        }
    }
}
