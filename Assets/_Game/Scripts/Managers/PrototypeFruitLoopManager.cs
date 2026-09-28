using System;
using System.Collections.Generic;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Gameplay;
using UnityEngine;

namespace BubbleFruitLoop.Managers
{
    public sealed class PrototypeFruitLoopManager
    {
        private readonly List<Fruit> fruits = new(40);
        private readonly LoopPathCache path;
        private readonly CapacityController capacity;
        private readonly GameSignals signals;
        private readonly float targetSpeed;
        private const float PreferredSpacing = 0.62f;

        public IReadOnlyList<Fruit> Fruits => fruits;
        public CapacityController Capacity => capacity;
        public float PathLength => path.Length;
        public LoopPathCache Path => path;

        public PrototypeFruitLoopManager(LoopPathCache path, CapacityController capacity, GameSignals signals, float targetSpeed)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
            this.signals = signals ?? throw new ArgumentNullException(nameof(signals));
            this.targetSpeed = targetSpeed;
        }

        public bool TryEnter(Fruit fruit)
        {
            if (fruit == null || !capacity.TryOccupy()) return false;

            // Phase 3: Immediate entry
            fruit.PathDistance = path.EntryDistance;
            fruit.CurrentSpeed = targetSpeed;
            fruit.SetState(FruitStatus.OnLoop);

            // Phase 16: Assign a tiny random lane offset
            fruit.LaneOffset = UnityEngine.Random.Range(-0.1f, 0.1f);

            fruits.Add(fruit);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (fruits.Count == 0) return;

            // Sort fruits by PathDistance
            fruits.Sort((a, b) => a.PathDistance.CompareTo(b.PathDistance));

            float optimalSpacing = path.Length / fruits.Count;
            float springStrength = 2.0f; // Tuning parameter

            for (int i = 0; i < fruits.Count; i++)
            {
                Fruit fruit = fruits[i];
                if (fruit.State != FruitStatus.OnLoop) continue;

                // Find next fruit
                int nextIndex = (i + 1) % fruits.Count;
                Fruit nextFruit = fruits[nextIndex];

                // Calculate distance to next fruit
                float distanceToNext = nextFruit.PathDistance - fruit.PathDistance;
                if (distanceToNext < 0) distanceToNext += path.Length;

                // Find previous fruit
                int prevIndex = (i - 1 + fruits.Count) % fruits.Count;
                Fruit prevFruit = fruits[prevIndex];

                // Calculate distance from previous fruit
                float distanceFromPrev = fruit.PathDistance - prevFruit.PathDistance;
                if (distanceFromPrev < 0) distanceFromPrev += path.Length;

                // Ideal behavior: we want distanceToNext and distanceFromPrev to equal optimalSpacing.
                // We add a correction velocity if they are not.
                float correction = 0f;
                if (fruits.Count > 1)
                {
                    float pushFromPrev = (optimalSpacing - distanceFromPrev); // positive if too close to prev
                    float pushFromNext = (optimalSpacing - distanceToNext);   // positive if too close to next

                    // Net force: if pushFromPrev is positive, we want to move forward (+).
                    // If pushFromNext is positive, we want to move backward (-).
                    correction = (pushFromPrev - pushFromNext) * springStrength;
                }

                float desiredSpeed = targetSpeed + correction;

                // Limit the max and min speed
                desiredSpeed = Mathf.Clamp(desiredSpeed, targetSpeed * 0.2f, targetSpeed * 1.8f);

                fruit.CurrentSpeed = Mathf.Lerp(fruit.CurrentSpeed, desiredSpeed, 10f * deltaTime);

                // Move
                fruit.PathDistance += fruit.CurrentSpeed * deltaTime;
                if (fruit.PathDistance >= path.Length) fruit.PathDistance -= path.Length;
                if (fruit.PathDistance < 0) fruit.PathDistance += path.Length;

                // Evaluate path
                path.EvaluateDistance(fruit.PathDistance, out Vector2 position, out Vector2 tangent, out Vector2 normal);

                // Make the visual purely follow tangent without wobble or random lane offset to keep it clean.
                Quaternion rotation = Quaternion.LookRotation(Vector3.forward, tangent);

                fruit.MoveStable(position, rotation);
            }
        }

        public bool Reserve(Fruit fruit)
        {
            int index = fruits.IndexOf(fruit);
            if (index < 0 || fruit.State != FruitStatus.OnLoop) return false;
            fruits.RemoveAt(index);
            fruit.SetState(FruitStatus.Reserved);
            capacity.Release();
            signals.RaiseLoopSlotReleased(fruit);
            return true;
        }
    }
}
