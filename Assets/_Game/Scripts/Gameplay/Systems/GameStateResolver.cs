using System;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Managers;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class GameStateResolver
    {
        private readonly FruitLoopManager loop;
        private readonly BoxBoardManager boxes;
        private readonly MatchResolver matches;
        private readonly GameSignals signals;

        public GameResult Result { get; private set; } = GameResult.Playing;

        public GameStateResolver(FruitLoopManager loop, BoxBoardManager boxes, MatchResolver matches, GameSignals signals)
        {
            this.loop = loop ?? throw new ArgumentNullException(nameof(loop));
            this.boxes = boxes ?? throw new ArgumentNullException(nameof(boxes));
            this.matches = matches ?? throw new ArgumentNullException(nameof(matches));
            this.signals = signals ?? throw new ArgumentNullException(nameof(signals));
        }

        public void Resolve()
        {
            if (Result != GameResult.Playing) return;
            if (!boxes.HasAnyBox()) SetResult(GameResult.Won);
            // A full loop may continue whenever at least one legal match remains.
            else if (loop.Capacity.IsFull && !matches.ExistsValidTransition()) SetResult(GameResult.Lost);
        }

        public bool TryUseCapacityBooster() => loop.Capacity.TryBoost(Result);

        private void SetResult(GameResult result)
        {
            Result = result;
            signals.RaiseGameResolved(result);
        }
    }
}

