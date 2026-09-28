using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class EntryCongestionState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {

        }


        internal static void EnterCongestion(Fruit fruit, bool loopIsFull)
        {
            fruit.ChangeFruitStateTo(FruitStates.For(loopIsFull ? FruitStatus.WaitingFull : FruitStatus.EntryCongestion), false);
            fruit.body.simulated = true;
            fruit.body.bodyType = RigidbodyType2D.Dynamic;
            // Fruit stays under real chute physics until it actually reaches T0.
            // No steering force pulls it from the funnel into the loop lane.
            fruit.body.gravityScale = loopIsFull ? 1f : 2.1f;
            fruit.body.linearDamping = loopIsFull ? 1.2f : 0.10f;
            fruit.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            fruit.body.interpolation = RigidbodyInterpolation2D.Interpolate;

        }
    }
}
