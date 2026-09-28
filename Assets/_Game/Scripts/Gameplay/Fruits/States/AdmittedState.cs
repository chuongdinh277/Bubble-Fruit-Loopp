using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public static class AdmittedState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExit(Fruit fruit) { }
        public static void OnExecute(Fruit fruit)
        {

        }

    }
}
