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
        private void ClearScene()
        {
            if (bubbleContainer != null && bubbleContainer == boxContainer)
            {
#if UNITY_EDITOR
                Debug.LogError("LevelDesignManager: Bubble Container và Box Container phải là hai object riêng. Không xoá scene để tránh mất bố cục.", this);
#endif
                return;
            }

            if (bubbleContainer != null)
            {
                for (int i = bubbleContainer.childCount - 1; i >= 0; i--)
                    DestroyImmediate(bubbleContainer.GetChild(i).gameObject);
            }

            if (boxContainer != null)
            {
                for (int i = boxContainer.childCount - 1; i >= 0; i--)
                    DestroyImmediate(boxContainer.GetChild(i).gameObject);
            }

            bubbles.Clear();
            boxes.Clear();
        }

#if UNITY_EDITOR
        private bool EnsureSeparateContainers()
        {
            if (bubbleContainer == null)
            {
                GameObject found = GameObject.Find("BubbleContainer");
                if (found != null)
                    bubbleContainer = found.transform;
            }

            ResolveBoxContainer();
            bool valid = bubbleContainer != null && boxContainer != null && bubbleContainer != boxContainer;
            if (!valid)
                Debug.LogError("LevelDesignManager cần BubbleContainer và BoxContainer riêng biệt.", this);
            return valid;
        }

        private BubbleConfig AddBubble()
        {
            if (!EnsureSeparateContainers() || bubblePrefab == null)
                return null;
            BubbleActor instance = (BubbleActor)PrefabUtility.InstantiatePrefab(bubblePrefab, bubbleContainer);
            instance.transform.position = Vector3.zero;
            instance.gameObject.name = $"Bubble_{bubbles.Count + 1}";
            var cfg = new BubbleConfig
            {
                instance = instance,
                manager = this
            };
            cfg.bubbleScale = CalculateBubbleScale(cfg.fruits.Count);
            UpdateBubbleVisuals(cfg);
            return cfg;
        }

        private void RemoveBubble(BubbleConfig cfg)
        {
            if (cfg.instance != null)
                DestroyImmediate(cfg.instance.gameObject);
            bubbles.Remove(cfg);
        }

        private BoxConfig AddBox() => AddBoxWithCapacity(4);
        private BoxConfig AddBoxWithCapacity(int cap)
        {
            if (!EnsureSeparateContainers() || box4Prefab == null)
                return null;
            cap = 4;
            BoxView instance = (BoxView)PrefabUtility.InstantiatePrefab(box4Prefab, boxContainer);
            instance.transform.position = Vector3.zero;
            instance.gameObject.name = $"Box_{boxes.Count + 1}";
            var cfg = new BoxConfig
            {
                instance = instance,
                manager = this,
                capacity = cap
            };
            UpdateBoxVisuals(cfg);
            return cfg;
        }

        private void RemoveBox(BoxConfig cfg)
        {
            if (cfg.instance != null)
                DestroyImmediate(cfg.instance.gameObject);
            boxes.Remove(cfg);
        }

        public void UpdateBubbleVisuals(BubbleConfig cfg)
        {
            if (cfg.instance == null || fruitPrefab == null)
                return;
            // Instances created by the broken version have Visual/InnerBoundary
            // recorded as removed prefab children. Replace only that bubble while
            // retaining its authored transform and fruit configuration.
            RestoreBubbleVisual(cfg);
            // The bubble prefab also owns Visual and InnerBoundary. Clearing the
            // whole instance removes its shell, which was why only tiny fruits and
            // collider gizmos remained in the Scene view.
            Transform fruitRoot = cfg.instance.transform.Find("FruitRoot");
            PrepareAuthoredFruitRoot(cfg, ref fruitRoot);
            float packingRadius = GetFruitContainmentRadius(cfg.instance) * InitialPackingRatio;
            List<Fruit> createdFruits = new List<Fruit>(cfg.fruits.Count);
            SpawnConfiguredBubbleFruits(cfg, fruitRoot, packingRadius, createdFruits);
            EditorUtility.SetDirty(cfg.instance);
        }

        private static float GetFruitContainmentRadius(BubbleActor bubble)
        {
            // BubbleManager constrains fruit centres, so reserve at least one
            // visible fruit radius plus a small highlight margin at the rim.
            if (bubble != null && bubble.ObstacleCollider is CircleCollider2D circle)
                return Mathf.Max(0.35f, circle.radius - FruitRimClearance);
            return 1.2f;
        }

        public void UpdateBoxVisuals(BoxConfig cfg)
        {
            if (cfg.instance == null || box4Prefab == null)
                return;
            Vector3 pos = cfg.instance.transform.position;
            Quaternion rotation = cfg.instance.transform.rotation;
            Vector3 scale = cfg.instance.transform.localScale;
            int siblingIndex = cfg.instance.transform.GetSiblingIndex();
            string objName = cfg.instance.gameObject.name;
            Undo.DestroyObjectImmediate(cfg.instance.gameObject);
            cfg.capacity = 4;
            cfg.instance = (BoxView)PrefabUtility.InstantiatePrefab(box4Prefab, boxContainer);
            cfg.instance.transform.position = pos;
            cfg.instance.transform.rotation = rotation;
            cfg.instance.transform.localScale = scale;
            cfg.instance.transform.SetSiblingIndex(Mathf.Min(siblingIndex, boxContainer.childCount - 1));
            cfg.instance.gameObject.name = objName;
            cfg.instance.Configure(cfg.fruitType, cfg.capacity, GetColorFor(cfg.fruitType));
        }

#endif
#if UNITY_EDITOR
        private void ResolveBoxContainer()
        {
            if (boxContainer == null || boxContainer == bubbleContainer)
            {
                Transform mixedContainer = bubbleContainer;
                GameObject found = GameObject.Find("BoxContainer");
                if (found == null)
                {
                    Transform parent = bubbleContainer != null ? bubbleContainer.parent : transform;
                    found = new GameObject("BoxContainer");
                    Undo.RegisterCreatedObjectUndo(found, "Create Box Container");
                    found.transform.SetParent(parent, false);
                }

                boxContainer = found.transform;
                // Older versions placed both kinds under BubbleContainer. Move only
                // direct BoxView children and retain their exact world transforms.
                if (mixedContainer != null && boxContainer != mixedContainer)
                {
                    for (int index = mixedContainer.childCount - 1; index >= 0; index--)
                    {
                        Transform child = mixedContainer.GetChild(index);
                        if (child.GetComponent<BoxView>() != null)
                            Undo.SetTransformParent(child, boxContainer, "Separate Box Container");
                    }
                }
            }
        }

#endif
#if UNITY_EDITOR
        private void SpawnConfiguredBubbleFruits(BubbleConfig cfg, Transform fruitRoot, float packingRadius, List<Fruit> createdFruits)
        {
            for (int i = 0; i < cfg.fruits.Count; i++)
            {
                FruitType type = cfg.fruits[i];
                Fruit fruit = (Fruit)PrefabUtility.InstantiatePrefab(fruitPrefab, fruitRoot);
                fruit.gameObject.name = $"Fruit_{type}_{i + 1}";
                fruit.Configure(type, GetColorFor(type));
                if (fruitSprites != null && fruitSprites.Length > (int)type && fruitSprites[(int)type] != null)
                    fruit.SetSprite(fruitSprites[(int)type]);
                // Golden-angle packing remains tidy for any editable fruit count.
                float angle = i * 2.399963f;
                float radius = Mathf.Sqrt((i + 0.5f) / Mathf.Max(1f, cfg.fruits.Count)) * packingRadius;
                fruit.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.5f);
                if (cfg.fruitScales != null && i < cfg.fruitScales.Count && cfg.fruitScales[i] != Vector3.zero)
                    fruit.transform.localScale = cfg.fruitScales[i];
                createdFruits.Add(fruit);
            }

            cfg.instance.ReplaceFruits(createdFruits);
            BubbleManager motion = cfg.instance.GetComponent<BubbleManager>();
            if (motion != null)
            {
                float containmentRadius = GetFruitContainmentRadius(cfg.instance);
                motion.Configure(createdFruits.ToArray(), cfg.instance.GetInstanceID() % 2 == 0 ? 1 : -1, Mathf.Abs(cfg.instance.GetInstanceID()) * 0.0137f, containmentRadius);
            }
        }

#endif
#if UNITY_EDITOR
        private void RestoreBubbleVisual(BubbleConfig cfg)
        {
            if (cfg.instance.transform.Find("Visual") == null && bubblePrefab != null)
            {
                BubbleActor damaged = cfg.instance;
                Transform parent = damaged.transform.parent;
                Vector3 position = damaged.transform.position;
                Quaternion rotation = damaged.transform.rotation;
                Vector3 scale = damaged.transform.localScale;
                int sibling = damaged.transform.GetSiblingIndex();
                string objectName = damaged.name;
                cfg.instance = (BubbleActor)PrefabUtility.InstantiatePrefab(bubblePrefab, parent);
                cfg.instance.transform.SetPositionAndRotation(position, rotation);
                cfg.instance.transform.localScale = scale;
                cfg.instance.transform.SetSiblingIndex(sibling);
                cfg.instance.name = objectName;
                Undo.DestroyObjectImmediate(damaged.gameObject);
            }
        }

#endif
#if UNITY_EDITOR
        private void PrepareAuthoredFruitRoot(BubbleConfig cfg, ref Transform fruitRoot)
        {
            if (fruitRoot == null)
            {
                GameObject rootObject = new GameObject("FruitRoot");
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Fruit Root");
                fruitRoot = rootObject.transform;
                fruitRoot.SetParent(cfg.instance.transform, false);
            }

            while (fruitRoot.childCount > 0)
                Undo.DestroyObjectImmediate(fruitRoot.GetChild(fruitRoot.childCount - 1).gameObject);
            ApplyBubbleScale(cfg);
        }
#endif
    }
}
