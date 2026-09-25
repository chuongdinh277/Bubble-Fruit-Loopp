using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed class BoxAssignmentTable : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public BoxView box;
            public FruitType fruitType;
            [Range(0, 2)] public int column;
            [Min(0)] public int queueOrder;
        }

        [SerializeField] private Transform[] pickupPoints = new Transform[3];
        [SerializeField] private List<Entry> entries = new();
        [SerializeField] private Material[] fruitPalette;

        public int ColumnCount => pickupPoints != null ? pickupPoints.Length : 0;
        public IReadOnlyList<Entry> Entries => entries;

        public Transform GetPickupPoint(int column) => pickupPoints != null &&
            column >= 0 && column < pickupPoints.Length ? pickupPoints[column] : null;

        public void ConfigurePalette(Material[] palette) => fruitPalette = palette;

        public Color GetColor(FruitType type)
        {
            int index = (int)type;
            if (fruitPalette != null && index >= 0 && index < fruitPalette.Length && fruitPalette[index] != null)
            {
                Material material = fruitPalette[index];
                if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
                if (material.HasProperty("_Color")) return material.GetColor("_Color");
            }
            return ColorFor(type);
        }

        public void Configure(BoxView[] boxes, FruitType[,] types, Transform[] points)
        {
            pickupPoints = points;
            entries.Clear();
            int columns = types.GetLength(1);
            int rows = types.GetLength(0);
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                if (index >= boxes.Length || boxes[index] == null) continue;
                entries.Add(new Entry
                {
                    box = boxes[index],
                    fruitType = types[row, column],
                    column = column,
                    queueOrder = row
                });
            }
            ApplyAssignments();
        }

        public void Configure(BoxView[] boxes, FruitType[] types, int[] columns, int[] queueOrders,
            Transform[] points)
        {
            pickupPoints = points;
            entries.Clear();
            for (int index = 0; index < boxes.Length; index++)
            {
                if (boxes[index] == null) continue;
                entries.Add(new Entry
                {
                    box = boxes[index],
                    fruitType = types[index],
                    column = columns[index],
                    queueOrder = queueOrders[index]
                });
            }
            ApplyAssignments();
        }

        public void ApplyAssignments()
        {
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry?.box == null) continue;
                entry.box.Configure(entry.fruitType, entry.box.Capacity, GetColor(entry.fruitType));
            }
        }

        private void OnValidate() => ApplyAssignments();

        private static Color ColorFor(FruitType type) => type switch
        {
            FruitType.Apple => new Color(0f, 0.46160913f, 1f),
            FruitType.Orange => new Color(1f, 0.45f, 0.05f),
            FruitType.Grape => new Color(0.55f, 0.18f, 0.85f),
            FruitType.Lemon => new Color(0.95f, 0.88f, 0.08f),
            FruitType.Strawberry => new Color(1f, 0.4292453f, 0.4292453f),
            _ => Color.white
        };
    }
}

