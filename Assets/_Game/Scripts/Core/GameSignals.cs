using System;
using BubbleFruitLoop.Gameplay;

namespace BubbleFruitLoop.Core
{
    public sealed class GameSignals
    {
        public event Action<Fruit> FruitReachedIntake;
        public event Action<Fruit> LoopSlotReleased;
        public event Action<BoxRuntime> BoxCompleted;
        public event Action<GameResult> GameResolved;
        public event Action<Fruit, BoxRuntime> FruitCollectedToBox;

        public void RaiseFruitReachedIntake(Fruit fruit) => FruitReachedIntake?.Invoke(fruit);
        public void RaiseLoopSlotReleased(Fruit fruit) => LoopSlotReleased?.Invoke(fruit);
        public void RaiseBoxCompleted(BoxRuntime box) => BoxCompleted?.Invoke(box);
        public void RaiseFruitCollectedToBox(Fruit fruit, BoxRuntime box) => FruitCollectedToBox?.Invoke(fruit, box);
        public void RaiseGameResolved(GameResult result) => GameResolved?.Invoke(result);
    }
}
