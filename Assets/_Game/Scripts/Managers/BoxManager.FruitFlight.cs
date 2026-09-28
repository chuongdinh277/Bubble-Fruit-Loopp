using System.Collections;
namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxManager
    {
        internal IEnumerator FlyFruitToBox(Fruit fruit, BoxView box, int slotIndex, Column column) =>
            CollectingState.FlyToBox(this, fruit, box, slotIndex, column);
    }
}
