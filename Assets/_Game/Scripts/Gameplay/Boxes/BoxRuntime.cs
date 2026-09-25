using System.Collections.Generic;
using UnityEngine;
using System;

namespace BubbleFruitLoop.Gameplay
{
    [Serializable]
    public class BoxRuntime
    {
        public FruitType FruitType { get; private set; }
        public int Capacity { get; private set; }
        public int CurrentCount { get; private set; }
        public int ReservedCount { get; private set; }
        public BoxState State { get; private set; }
        public int ColumnIndex { get; set; }
        public int QueueIndex { get; set; }
        public bool[] FilledSlots { get; private set; }
        private readonly HashSet<int> reservedSlots = new();
        
        public Action<int> OnFruitFilled; // passes slot index
        public Action OnBoxFull;

        public int Missing => Mathf.Max(0, Capacity - CurrentCount - ReservedCount);
        // Consecutive matching fruit must be captured continuously. Every
        // in-flight fruit reserves its own slot, so none of the fruit directly
        // behind it can slip past the same pickup point while capacity remains.
        public bool CanReserve => (State == BoxState.Active || State == BoxState.Receiving)
            && Missing > 0;

        public void Configure(FruitType type, int capacity)
        {
            FruitType = type;
            Capacity = 4;
            CurrentCount = 0;
            ReservedCount = 0;
            FilledSlots = new bool[Capacity];
            reservedSlots.Clear();
            State = BoxState.Waiting;
        }

        public bool TryReserve(FruitType type)
        {
            return TryReserve(type, out _);
        }

        public bool TryReserve(FruitType type, out int slotIndex)
        {
            slotIndex = -1;
            if (type != FruitType || !CanReserve) return false;
            for (int index = 0; index < Capacity; index++)
            {
                if (FilledSlots[index] || reservedSlots.Contains(index)) continue;
                slotIndex = index;
                reservedSlots.Add(index);
                ReservedCount++;
                State = BoxState.Receiving;
                return true;
            }
            return false;
        }

        public bool CommitReservedFruit()
        {
            if (!TryGetNextReservedSlot(out int slotIndex)) return false;
            return CommitReservedFruit(slotIndex);
        }

        public bool CommitReservedFruit(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Capacity || !reservedSlots.Remove(slotIndex)) return false;
            ReservedCount--;
            CurrentCount++;
            FilledSlots[slotIndex] = true;
            OnFruitFilled?.Invoke(slotIndex);

            State = CurrentCount >= Capacity ? BoxState.Full : BoxState.Active;
            
            if (State == BoxState.Full)
            {
                OnBoxFull?.Invoke();
            }
            
            return State == BoxState.Full;
        }

        public void CancelReservation()
        {
            if (!TryGetNextReservedSlot(out int slotIndex)) return;
            CancelReservation(slotIndex);
        }

        public void CancelReservation(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Capacity || !reservedSlots.Remove(slotIndex)) return;
            ReservedCount--;
            if (State == BoxState.Receiving && ReservedCount == 0)
                State = CurrentCount > 0 ? BoxState.Receiving : BoxState.Active;
        }

        private bool TryGetNextReservedSlot(out int slotIndex)
        {
            slotIndex = -1;
            foreach (int reserved in reservedSlots)
                if (slotIndex < 0 || reserved < slotIndex) slotIndex = reserved;
            return slotIndex >= 0;
        }

        public void Activate() => State = BoxState.Active;
        public void Complete() => State = BoxState.Completed;
    }
}

