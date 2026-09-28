using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class FruitLoopManager
    {
        public void NotifyFruitCollected(Fruit fruit)
        {
            if (fruit == null || !owned.Remove(fruit))
                return;
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index].Fruit == fruit)
                    active.RemoveAt(index);
        }

        public void CopyLoopFruits(List<Fruit> destination)
        {
            destination.Clear();
            for (int index = 0; index < active.Count; index++)
            {
                Fruit fruit = active[index].Fruit;
                if (fruit != null && fruit.State == FruitStatus.OnLoop)
                    destination.Add(fruit);
            }
        }

        public float FindClosestPathDistance(Vector3 worldPosition) => path != null ? path.FindClosestDistance(worldPosition) : 0f;
        public bool ReserveFruitForBox(Fruit fruit)
        {
            if (fruit == null || fruit.State != FruitStatus.OnLoop)
                return false;
            for (int index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].Fruit != fruit)
                    continue;
                active.RemoveAt(index);
                owned.Remove(fruit);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
                fruit.SetState(FruitStatus.Collecting);
                fruit.DisablePhysics();
                return true;
            }

            return false;
        }
    }
}
