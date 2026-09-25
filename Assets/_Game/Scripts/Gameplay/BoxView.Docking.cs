using System;
using System.Collections;
using System.Collections.Generic;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Pooling;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxView
    {
        private sealed class DockedVisual
        {
            public FruitActor fruit;
            public SpriteRenderer renderer;
            public Sprite sprite;
            public Color color;
            public Material material;
            public bool ownsMaterial;
            public Transform target;
            public int slotIndex;
            public GameObject shadowObject;
        }
        private readonly List<DockedVisual> dockedVisuals = new(6);
        public Transform GetSlotTransform(int index)
        {
            return index >= 0 && index < fruitSlots.Length ? fruitSlots[index] : transform;
        }

        public bool DockFruit(FruitActor fruit, SpriteRenderer source, Sprite flightSprite, int slotIndex)
        {
            if (fruit == null || source == null || flightSprite == null) return false;
            // The flight owns this renderer and sprite snapshot. Never search the
            // box hierarchy or use a different fruit/slot's visual at docking.
            if (source.sprite != flightSprite)
            {
                Debug.LogWarning($"Fruit sprite changed during flight; restoring its captured sprite. Fruit={fruit.name} (ID {fruit.GetInstanceID()}), expected={flightSprite.name}, found={(source.sprite != null ? source.sprite.name : "<null>")}", fruit);
                source.sprite = flightSprite;
            }
            Transform slot = GetSlotTransform(slotIndex);
            DockActorInSlot(fruit, source, slot, slotIndex);
            PlayImpactBurst(slot.position, configuredColor, 7, 0.22f, 0.24f);
            fruit.DisablePhysics();
            if (fruit.BodyCollider != null) fruit.BodyCollider.enabled = false;
            collectedFruits.Add(fruit);
            StartCoroutine(PunchSlot(slot));
            return true;
        }

        private void DockActorInSlot(FruitActor fruit, SpriteRenderer source, Transform slot, int slotIndex)
        {
            // Keep the actual actor and its renderer. A second SpriteRenderer
            // introduced a needless copy step where sprite/tint/material could
            // diverge from this fruit while several independent flights overlap.
            fruit.transform.SetParent(null, true);
            fruit.gameObject.SetActive(true);
            Material isolatedMaterial = source.sharedMaterial != null
                ? new Material(source.sharedMaterial)
                : null;
            if (isolatedMaterial != null)
            {
                isolatedMaterial.name = $"{fruit.name} Box Material";
                isolatedMaterial.hideFlags = HideFlags.HideAndDontSave;
                source.sharedMaterial = isolatedMaterial;
            }
            source.sortingOrder = FruitOrder;
            source.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            GameObject shadowObject = new GameObject("Fruit Shadow");
            shadowObject.transform.SetParent(fruit.transform, false);
            shadowObject.transform.localPosition = new Vector3(0.025f, -0.035f, 0.01f);
            shadowObject.transform.localScale = new Vector3(1.02f, 1.02f, 1f);
            SpriteRenderer fruitShadow = shadowObject.AddComponent<SpriteRenderer>();
            fruitShadow.sprite = source.sprite;
            fruitShadow.color = new Color(0f, 0f, 0f, 0.16f);
            fruitShadow.sortingLayerID = source.sortingLayerID;
            fruitShadow.sortingOrder = FruitShadowOrder;
            fruitShadow.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            fruit.transform.position = slot.position + Vector3.back * 0.1f;
            fruit.transform.rotation = slot.rotation;
            dockedVisuals.Add(new DockedVisual
            {
                fruit = fruit,
                renderer = source,
                sprite = source.sprite,
                color = source.color,
                material = isolatedMaterial,
                ownsMaterial = isolatedMaterial != null,
                target = slot,
                slotIndex = slotIndex,
                shadowObject = shadowObject
            });
        }

        private void LateUpdate()
        {
            if (liftGroundShadow != null)
            {
                // Keep the projection locked to the box. A delayed follow made
                // the shadow drift away whenever the box lifted, punched, or
                // changed scale during an animation.
                UpdateLiftGroundShadowTransform();
            }

            for (int index = dockedVisuals.Count - 1; index >= 0; index--)
            {
                DockedVisual visual = dockedVisuals[index];
                if (visual?.fruit == null || visual.target == null)
                {
                    if (visual?.ownsMaterial == true && visual.material != null)
                        Destroy(visual.material);
                    dockedVisuals.RemoveAt(index);
                    continue;
                }
                // A packed fruit owns its appearance independently from this box.
                // Restore only its own captured values if another lifecycle path
                // touched the renderer; never copy from another slot or box.
                if (visual.renderer != null)
                {
                    if (visual.renderer.sprite != visual.sprite) visual.renderer.sprite = visual.sprite;
                    if (visual.renderer.color != visual.color) visual.renderer.color = visual.color;
                    if (visual.renderer.sharedMaterial != visual.material)
                        visual.renderer.sharedMaterial = visual.material;
                }
                visual.fruit.transform.position = visual.target.position + Vector3.back * 0.1f;
            }
        }

        public void ReleaseCollectedFruits(Action<FruitActor> release)
        {
            for (int index = 0; index < collectedFruits.Count; index++)
            {
                FruitActor fruit = collectedFruits[index];
                if (fruit == null) continue;
                if (release != null) release.Invoke(fruit);
                else fruit.gameObject.SetActive(false);
            }
            collectedFruits.Clear();
            for (int index = 0; index < dockedVisuals.Count; index++)
            {
                DockedVisual visual = dockedVisuals[index];
                if (visual?.shadowObject != null) Destroy(visual.shadowObject);
                if (visual?.ownsMaterial == true && visual.material != null) Destroy(visual.material);
            }
            dockedVisuals.Clear();
        }
    }
}
