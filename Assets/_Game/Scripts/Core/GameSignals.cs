using System;
using BubbleFruitLoop.Gameplay;

namespace BubbleFruitLoop.Core
{
    public sealed class GameSignals
    {
        public event Action<FruitActor> FruitReachedIntake;
        public event Action<FruitActor> LoopSlotReleased;
        public event Action<BoxRuntime> BoxCompleted;
        public event Action<GameResult> GameResolved;
        public event Action<FruitActor, BoxRuntime> FruitCollectedToBox;

        public void RaiseFruitReachedIntake(FruitActor fruit) => FruitReachedIntake?.Invoke(fruit);
        public void RaiseLoopSlotReleased(FruitActor fruit) => LoopSlotReleased?.Invoke(fruit);
        public void RaiseBoxCompleted(BoxRuntime box) => BoxCompleted?.Invoke(box);
        public void RaiseFruitCollectedToBox(FruitActor fruit, BoxRuntime box) => FruitCollectedToBox?.Invoke(fruit, box);
        public void RaiseGameResolved(GameResult result) => GameResolved?.Invoke(result);
    }
}
