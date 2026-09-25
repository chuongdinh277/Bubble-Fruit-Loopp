using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    [CreateAssetMenu(menuName = "Bubble Fruit/Level Definition", fileName = "Level_001")]
    public sealed class BubbleFruitLevelDefinition : ScriptableObject
    {
        public enum BoxCapacity { Box4 = 4 }

        [Serializable]
        public sealed class BubbleSetup
        {
            public Vector2 position;
            public List<FruitType> fruits = new();
        }

        [Serializable]
        public sealed class BoxSetup
        {
            public FruitType fruitType;
            public BoxCapacity capacity = BoxCapacity.Box4;
            [Range(0, 2)] public int column;
        }

        public List<BubbleSetup> bubbles = new();
        public List<BoxSetup> boxes = new();
    }
}

