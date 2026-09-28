using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class FruitLoopManager
    {
        internal void SetLoopFruitContactsIgnored(Fruit fruit, bool ignored)
        {
            // Loop movement is path driven; fruit contacts are intentionally
            // disabled so physics cannot pull them away from the authored route.
            if (fruit == null || fruit.BodyCollider == null || !ignored) return;
            for (int index = 0; index < active.Count; index++)
            {
                Fruit other = active[index].Fruit;
                if (other == null || other == fruit || other.BodyCollider == null) continue;
                Physics2D.IgnoreCollision(fruit.BodyCollider, other.BodyCollider, true);
            }
        }
    }
}
