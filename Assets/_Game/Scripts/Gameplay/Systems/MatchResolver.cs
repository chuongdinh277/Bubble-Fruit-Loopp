using System;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Managers;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class MatchResolver
    {
        private readonly PrototypeFruitLoopManager loop;
        private readonly BoxBoardManager board;
        private readonly GameSignals signals;

        public MatchResolver(PrototypeFruitLoopManager loop, BoxBoardManager board, GameSignals signals)
        {
            this.loop = loop ?? throw new ArgumentNullException(nameof(loop));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.signals = signals ?? throw new ArgumentNullException(nameof(signals));
        }

        public bool TryMatchAtPickup(Fruit fruit, int columnIndex)
        {
            if (fruit == null || fruit.State != FruitStatus.OnLoop) return false;
            if (columnIndex < 0 || columnIndex >= board.ColumnCount) return false;
            BoxRuntime box = board.GetColumn(columnIndex).ActiveBox;
            if (box == null || !box.TryReserve(fruit.Type)) return false;
            // Occupancy is released at reservation time, before collect visuals finish.
            if (!loop.Reserve(fruit))
            {
                box.CancelReservation();
                return false;
            }
            Collect(fruit, box, board.GetColumn(columnIndex));
            return true;
        }

        public bool ExistsValidTransition()
        {
            var fruits = loop.Fruits;
            for (int index = 0; index < fruits.Count; index++)
            {
                if (fruits[index].State == FruitStatus.OnLoop && board.FindPriorityTarget(fruits[index].Type) != null) return true;
            }
            return false;
        }

        private void Collect(Fruit fruit, BoxRuntime box, BoxColumnRuntime owner)
        {
            fruit.SetState(FruitStatus.Collecting);
            bool full = box.CommitReservedFruit();
            signals.RaiseFruitCollectedToBox(fruit, box);

            if (!full) return;
            owner?.CompleteActiveBox();
            signals.RaiseBoxCompleted(box);
        }
    }
}

