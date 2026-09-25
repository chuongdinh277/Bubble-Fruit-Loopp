using System.Collections.Generic;
using BubbleFruitLoop.Gameplay;

namespace BubbleFruitLoop.Managers
{
    public sealed class BoxBoardManager
    {
        private readonly List<BoxColumnRuntime> columns = new(8);

        public int ColumnCount => columns.Count;

        public void AddColumn(BoxColumnRuntime column) => columns.Add(column);
        public BoxColumnRuntime GetColumn(int index) => columns[index];

        public BoxRuntime FindPriorityTarget(FruitType type)
        {
            for (int index = columns.Count - 1; index >= 0; index--)
            {
                BoxRuntime box = columns[index].ActiveBox;
                if (box != null && box.FruitType == type && box.CanReserve) return box;
            }
            return null;
        }

        public BoxColumnRuntime FindOwner(BoxRuntime target)
        {
            for (int index = 0; index < columns.Count; index++)
            {
                if (columns[index].ActiveBox == target) return columns[index];
            }
            return null;
        }

        public bool HasAnyBox()
        {
            for (int index = 0; index < columns.Count; index++)
            {
                if (columns[index].RemainingCount > 0) return true;
            }
            return false;
        }
    }
}
