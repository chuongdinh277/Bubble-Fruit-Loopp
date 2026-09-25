using System.Collections.Generic;
using BubbleFruitLoop.Gameplay;
using UnityEngine;

namespace BubbleFruitLoop.Managers
{
    public sealed class FunnelIntakeManager
    {
        private readonly List<FruitActor> waiting = new();
        private readonly FruitLoopManager loop;
        private FruitActor currentlyFalling;
        private float fallProgress;
        private Vector2 fallStartPosition;

        public int WaitingCount => waiting.Count;

        public FunnelIntakeManager(FruitLoopManager loop)
        {
            this.loop = loop;
        }

        public void Submit(FruitActor fruit)
        {
            if (fruit == null) return;
            fruit.DisablePhysics(); // No physics for waiting fruits
            fruit.SetState(FruitState.IntakeWaiting);
            waiting.Add(fruit);
        }

        public void Tick(float deltaTime)
        {
            loop.Path.EvaluateDistance(loop.Path.EntryDistance, out Vector2 entryPos, out _, out _);
            Vector2 groovePos = entryPos + new Vector2(0, 1.5f);

            if (currentlyFalling != null)
            {
                // Rapidly fall to entry point
                currentlyFalling.CachedTransform.position = Vector3.MoveTowards(
                    currentlyFalling.CachedTransform.position, 
                    entryPos, 
                    deltaTime * 12f);
                    
                if (Vector2.Distance(currentlyFalling.CachedTransform.position, entryPos) < 0.01f)
                {
                    loop.TryEnter(currentlyFalling);
                    currentlyFalling = null;
                }
            }
            else if (waiting.Count > 0 && loop.Capacity.HasSlot)
            {
                // Find lowest fruit to fall next
                int lowestIndex = 0;
                float lowestY = float.MaxValue;
                for (int i = 0; i < waiting.Count; i++)
                {
                    if (waiting[i].CachedTransform.position.y < lowestY)
                    {
                        lowestY = waiting[i].CachedTransform.position.y;
                        lowestIndex = i;
                    }
                }

                currentlyFalling = waiting[lowestIndex];
                waiting.RemoveAt(lowestIndex);
            }

            // Move waiting fruits towards the groove neatly
            // Sort them by Y to assign vertical stack positions
            waiting.Sort((a, b) => a.CachedTransform.position.y.CompareTo(b.CachedTransform.position.y));

            for (int i = 0; i < waiting.Count; i++)
            {
                FruitActor w = waiting[i];
                Vector3 current = w.CachedTransform.position;
                
                // Target is the groove, plus a vertical offset so they form a neat line
                Vector3 target = groovePos + new Vector2(0, i * 0.45f);
                
                // Horizontal funneling (move towards center X quickly, drop Y steadily)
                float moveX = Mathf.MoveTowards(current.x, target.x, deltaTime * 8f);
                float moveY = Mathf.MoveTowards(current.y, target.y, deltaTime * 8f);
                
                w.CachedTransform.position = new Vector3(moveX, moveY, current.z);
            }
        }
    }
}
