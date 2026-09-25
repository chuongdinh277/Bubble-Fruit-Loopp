using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class BoxColumnRuntime
    {
        private readonly Queue<BoxRuntime> queue = new();
        public int ColumnIndex { get; private set; }
        
        public BoxRuntime ActiveBox { get; private set; }
        public Vector3 ActivePosition { get; }
        public Vector3 PickupPosition { get; }
        public int RemainingCount => queue.Count + (ActiveBox != null ? 1 : 0);
        
        public Action<BoxRuntime, Vector3> OnBoxActivated;
        public Action<BoxRuntime> OnBoxCompleted;

        public BoxColumnRuntime(int columnIndex, Vector3 activePosition, Vector3 pickupPosition)
        {
            ColumnIndex = columnIndex;
            ActivePosition = activePosition;
            PickupPosition = pickupPosition;
        }

        public void Enqueue(BoxRuntime box)
        {
            box.ColumnIndex = ColumnIndex;
            box.QueueIndex = queue.Count + (ActiveBox != null ? 1 : 0);
            queue.Enqueue(box);
            if (ActiveBox == null) ActivateNext();
        }

        public void CompleteActiveBox()
        {
            if (ActiveBox == null) return;
            
            ActiveBox.Complete();
            OnBoxCompleted?.Invoke(ActiveBox);
            ActiveBox = null;
            
            ActivateNext();
        }

        private void ActivateNext()
        {
            if (queue.Count == 0) return;
            
            ActiveBox = queue.Dequeue();
            ActiveBox.Activate();
            
            OnBoxActivated?.Invoke(ActiveBox, ActivePosition);
        }
    }
}

