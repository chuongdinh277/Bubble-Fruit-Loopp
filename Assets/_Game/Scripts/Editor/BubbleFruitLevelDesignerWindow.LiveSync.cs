using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;

namespace BubbleFruitLoop.Editor
{
    public sealed partial class BubbleFruitLevelDesignerWindow
    {
        private void PreviewUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextPreviewRepaint) return;
            nextPreviewRepaint = EditorApplication.timeSinceStartup + 0.1d;
            WatchForLiveChanges();
            Repaint();
        }

        private void WatchForLiveChanges()
        {
            if (!livePreview || level == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            int bubbleHash = ComputeBubbleHash();
            int boxHash = ComputeBoxHash();
            if (bubbleHash == lastBubbleHash && boxHash == lastBoxHash) return;

            bool bubblesChanged = bubbleHash != lastBubbleHash;
            lastBubbleHash = bubbleHash;
            lastBoxHash = boxHash;
            if (bubblesChanged)
            {
                InitializeNewBubbles();
                if (autoCreateBoxesFromFruits && !TrySyncBoxesFromFruits()) return;
            }
            QueueLiveApply();
        }

        private void QueueLiveApply()
        {
            if (liveApplyQueued) return;
            liveApplyQueued = true;
            EditorApplication.delayCall += () =>
            {
                liveApplyQueued = false;
                if (this == null || level == null) return;
                SaveLevel();
                if (IsValid) ApplyToScene();
                Repaint();
            };
        }

        private void InitializeNewBubbles()
        {
            if (level.bubbles.Count > knownBubbleCount)
            {
                for (int index = knownBubbleCount; index < level.bubbles.Count; index++)
                {
                    BubbleFruitLevelDefinition.BubbleSetup bubble = level.bubbles[index];
                    bubble.position = DefaultBubblePosition(index);
                    if (bubble.fruits.Count == 0)
                    {
                        for (int fruit = 0; fruit < 8; fruit++)
                            bubble.fruits.Add(fruit % 2 == 0 ? FruitType.Orange : FruitType.Strawberry);
                    }
                }
            }
            knownBubbleCount = level.bubbles.Count;
        }

        private bool TrySyncBoxesFromFruits()
        {
            Dictionary<FruitType, int> counts = new();
            for (int bubble = 0; bubble < level.bubbles.Count; bubble++)
            for (int fruit = 0; fruit < level.bubbles[bubble].fruits.Count; fruit++)
            {
                FruitType type = level.bubbles[bubble].fruits[fruit];
                counts[type] = counts.TryGetValue(type, out int value) ? value + 1 : 1;
            }

            List<BubbleFruitLevelDefinition.BoxSetup> generated = new();
            int nextColumn = 0;
            foreach (KeyValuePair<FruitType, int> item in counts)
            {
                if (item.Key is not (FruitType.Orange or FruitType.Strawberry)) return false;
                int remaining = item.Value;
                if (remaining % 4 != 0) return false;
                while (remaining > 0)
                {
                    const int capacity = 4;
                    generated.Add(new BubbleFruitLevelDefinition.BoxSetup
                    {
                        fruitType = item.Key,
                        capacity = (BubbleFruitLevelDefinition.BoxCapacity)capacity,
                        column = nextColumn++ % 3
                    });
                    remaining -= capacity;
                }
            }
            level.boxes = generated;
            lastBoxHash = ComputeBoxHash();
            return true;
        }

        private int ComputeBubbleHash()
        {
            if (level == null) return 0;
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < level.bubbles.Count; index++)
                {
                    BubbleFruitLevelDefinition.BubbleSetup bubble = level.bubbles[index];
                    hash = hash * 31 + bubble.position.GetHashCode();
                    hash = hash * 31 + bubble.fruits.Count;
                    for (int fruit = 0; fruit < bubble.fruits.Count; fruit++) hash = hash * 31 + (int)bubble.fruits[fruit];
                }
                return hash;
            }
        }

        private int ComputeBoxHash()
        {
            if (level == null) return 0;
            unchecked
            {
                int hash = 23;
                for (int index = 0; index < level.boxes.Count; index++)
                {
                    BubbleFruitLevelDefinition.BoxSetup box = level.boxes[index];
                    hash = hash * 31 + (int)box.fruitType;
                    hash = hash * 31 + (int)box.capacity;
                    hash = hash * 31 + box.column;
                }
                return hash;
            }
        }
    }
}
