using System;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class CapacityController
    {
        private readonly int baseCapacity;
        private readonly int boostedCapacity;

        public int Count { get; private set; }
        public int Capacity { get; private set; }
        public bool HasSlot => Count < Capacity;
        public bool IsFull => Count >= Capacity;

        public CapacityController(int normalCapacity, int extraCapacity)
        {
            if (normalCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(normalCapacity));
            baseCapacity = normalCapacity;
            boostedCapacity = normalCapacity + Math.Max(0, extraCapacity);
            Capacity = baseCapacity;
        }

        public bool TryOccupy()
        {
            if (!HasSlot) return false;
            Count++;
            return true;
        }

        public void Release()
        {
            if (Count <= 0) throw new InvalidOperationException("Loop occupancy underflow.");
            Count--;
        }

        public bool TryBoost(GameResult result)
        {
            if (result != GameResult.Playing || Capacity == boostedCapacity) return false;
            Capacity = boostedCapacity;
            return true;
        }

        public void Reset()
        {
            Count = 0;
            Capacity = baseCapacity;
        }
    }
}

