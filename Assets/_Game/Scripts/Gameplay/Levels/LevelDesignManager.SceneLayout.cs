using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;
#if UNITY_EDITOR
#if UNITY_EDITOR
using UnityEditor;
#endif
#endif

namespace BubbleFruitLoop.Editor
{
    public partial class LevelDesignManager
    {
        public void GenerateDefaultLayout()
        {
#if UNITY_EDITOR
            if (!EnsureSeparateContainers()) return;
            ClearScene();

            // Default 9 bubbles layout
            for (int i = 0; i < 9; i++)
            {
                var cfg = AddBubble();
                if (cfg != null)
                {
                    // 3x3 grid at top
                    cfg.instance.transform.position = new Vector3(-2f + (i % 3) * 2f, 3.5f - (i / 3) * 2.15f, 0f);
                    cfg.fruits = new List<FruitType>() { FruitType.Apple, FruitType.Apple, FruitType.Apple, FruitType.Apple };
                    cfg.bubbleScale = CalculateBubbleScale(cfg.fruits.Count);
                    UpdateBubbleVisuals(cfg);
                }
            }

            // Default 3x3 boxes layout (3 columns, 3 rows)
            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 3; row++)
                {
                    var cfg = AddBoxWithCapacity(4);
                    if (cfg != null)
                    {
                        float xPos = -1.5f + col * 1.5f;
                        float yPos = -6.2f - row * 0.9f;
                        cfg.instance.transform.position = new Vector3(xPos, yPos, 0f);
                        cfg.fruitType = (FruitType)col; // Just some default colors
                        UpdateBoxVisuals(cfg);
                    }
                }
            }
#endif
        }

        [Button("Sync Scene To Inspector", ButtonSizes.Medium)]
        private void SyncScene()
        {
#if UNITY_EDITOR
            if (!EnsureSeparateContainers()) return;
#endif
            if (bubbleContainer != null)
            {
                bubbles.Clear();
                foreach (Transform child in bubbleContainer)
                {
                    BubbleActor bubble = child.GetComponent<BubbleActor>();
                    if (bubble != null)
                    {
                        BubbleConfig cfg = new BubbleConfig { instance = bubble, manager = this };
                        Transform visual = bubble.transform.Find("Visual");
                        float rootScale = Mathf.Abs(bubble.transform.localScale.x);
                        float visualScale = visual != null ? Mathf.Abs(visual.localScale.x) : 1f;
                        cfg.bubbleScale = Mathf.Max(0.1f, rootScale * visualScale);
                        cfg.fruits.Clear();
                        cfg.fruitScales.Clear();
                        foreach (Fruit fruit in bubble.GetComponentsInChildren<Fruit>(true))
                        {
                            if (fruit != null)
                            {
                                cfg.fruits.Add(fruit.Type);
                                cfg.fruitScales.Add(fruit.transform.localScale);
                            }
                        }
                        if (cfg.fruits.Count == 0) cfg.fruits.AddRange(new[] { FruitType.Apple, FruitType.Apple, FruitType.Apple });
                        bubbles.Add(cfg);
                    }
                }
            }

            if (boxContainer != null)
            {
                boxes.Clear();
                foreach (Transform child in boxContainer)
                {
                    BoxView box = child.GetComponent<BoxView>();
                    if (box != null)
                    {
                        BoxConfig cfg = new BoxConfig { instance = box, manager = this };
                        cfg.capacity = 4;
                        cfg.fruitType = box.editorFruitType;
                        boxes.Add(cfg);
                    }
                }
                UpdateBoxQueueMetadata();
            }
        }

        private void UpdateBoxQueueMetadata()
        {
            List<BoxConfig>[] grouped =
            {
                new List<BoxConfig>(), new List<BoxConfig>(), new List<BoxConfig>()
            };
            for (int index = 0; index < boxes.Count; index++)
            {
                BoxConfig box = boxes[index];
                if (box?.instance == null) continue;
                box.column = ClosestBoxColumn(box.instance.transform.position.x);
                grouped[box.column].Add(box);
            }
            for (int column = 0; column < grouped.Length; column++)
            {
                grouped[column].Sort((a, b) =>
                    b.instance.transform.position.y.CompareTo(a.instance.transform.position.y));
                for (int row = 0; row < grouped[column].Count; row++)
                    grouped[column][row].queueOrder = row;
            }
        }

    }
}
