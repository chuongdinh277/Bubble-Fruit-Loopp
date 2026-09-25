using System;
using UnityEngine;

namespace BubbleFruitLoop.Data
{
    [Serializable]
    public class PlayerData
    {
        public int level = 1;
        public int coin = 0;
        public bool isSoundOn = true;
        public bool isMusicOn = true;
        public bool isVibrationOn = true;

        public void Validate()
        {
            if (level < 1) level = 1;
            if (coin < 0) coin = 0;
        }
    }
}
