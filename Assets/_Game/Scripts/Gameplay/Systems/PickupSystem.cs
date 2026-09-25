using System.Collections.Generic;
using BubbleFruitLoop.Managers;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class PickupSystem
    {
        private readonly Dictionary<FruitActor, float> previousDistances = new(40);
        private readonly FruitLoopManager loop;
        private readonly MatchResolver resolver;
        private readonly float[] pickupDistances;

        public PickupSystem(FruitLoopManager loop, MatchResolver resolver, BoxBoardManager board)
        {
            this.loop = loop;
            this.resolver = resolver;
            pickupDistances = new float[board.ColumnCount];
            for (int index = 0; index < pickupDistances.Length; index++)
                pickupDistances[index] = loop.Path.FindClosestDistance(board.GetColumn(index).PickupPosition);
        }

        public void Tick()
        {
            var fruits = loop.Fruits;
            for (int fruitIndex = fruits.Count - 1; fruitIndex >= 0; fruitIndex--)
            {
                FruitActor fruit = fruits[fruitIndex];
                if (fruit.State != FruitState.OnLoop) continue;
                float previous = previousDistances.TryGetValue(fruit, out float value) ? value : fruit.PathDistance;
                float current = fruit.PathDistance;

                for (int columnIndex = 0; columnIndex < pickupDistances.Length; columnIndex++)
                {
                    if (!CrossedPickup(previous, current, pickupDistances[columnIndex])) continue;
                    if (resolver.TryMatchAtPickup(fruit, columnIndex))
                    {
                        previousDistances.Remove(fruit);
                        break;
                    }
                }

                if (fruit.State == FruitState.OnLoop) previousDistances[fruit] = current;
            }
        }

        private bool CrossedPickup(float previous, float current, float pickup)
        {
            previous = UnityEngine.Mathf.Repeat(previous, loop.PathLength);
            current = UnityEngine.Mathf.Repeat(current, loop.PathLength);
            pickup = UnityEngine.Mathf.Repeat(pickup, loop.PathLength);
            return current >= previous
                ? pickup > previous && pickup <= current
                : pickup > previous || pickup <= current;
        }
    }
}

