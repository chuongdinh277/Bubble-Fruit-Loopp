using UnityEngine;
using System.Collections.Generic;
using BubbleFruitLoop.Core;

namespace BubbleFruitLoop.Gameplay
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "BubbleFruit/Level Data")]
    public class LevelData : ScriptableObject
    {
        [System.Serializable]
        public class BubbleSpawnData
        {
            public Vector2 position;
            [Min(0.1f)] public float bubbleScale = 0.4f;
            public List<FruitType> fruits = new List<FruitType>();
            public List<Vector3> fruitScales = new List<Vector3>();
        }

        [System.Serializable]
        public class BoxSpawnData
        {
            public Vector2 position; // active point
            public FruitType fruitType;
            public int capacity;
            [Range(0, 2)] public int column;
            [Min(0)] public int queueOrder;
        }

        public List<BubbleSpawnData> bubbles = new List<BubbleSpawnData>();
        public List<BoxSpawnData> boxes = new List<BoxSpawnData>();
        [HideInInspector] public int boxLayoutVersion;
    }
}

