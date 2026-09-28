using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class IntakeWaitingState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {

        }


        internal static void BeginIntakeWaiting(Fruit fruit)
        {
            fruit.ChangeFruitStateTo(FruitStates.For(FruitStatus.IntakeWaiting), false);
            fruit.body.simulated = true;
            fruit.body.bodyType = RigidbodyType2D.Kinematic;
            fruit.body.gravityScale = 0f;
            fruit.body.linearVelocity = Vector2.zero;
            fruit.body.angularVelocity = 0f;
            fruit.body.interpolation = RigidbodyInterpolation2D.Interpolate;

        }
    }
}
