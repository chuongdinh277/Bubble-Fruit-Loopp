using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Core;

namespace BubbleFruitLoop.Editor
{
    public sealed partial class BubbleFruitLevelDesignerWindow
    {
        private void RebuildBubbles()
        {
            GameObject containerObject = GameObject.Find("BubbleContainer");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
            if (containerObject == null || prefab == null)
                throw new MissingReferenceException("BubbleContainer hoặc BubbleAsset2D.prefab không tồn tại.");

            ClearChildren(containerObject.transform);
            for (int index = 0; index < level.bubbles.Count; index++)
            {
                BubbleFruitLevelDefinition.BubbleSetup setup = level.bubbles[index];
                GameObject bubble = (GameObject)PrefabUtility.InstantiatePrefab(prefab, containerObject.transform);
                bubble.name = $"Bubble_Level_{index + 1:00}";
                bubble.transform.position = setup.position;
                ConfigureBubbleFruits(bubble, setup.fruits, index);
            }
        }

        private static void ConfigureBubbleFruits(GameObject bubble, List<FruitType> types, int seed)
        {
            Transform fruitRoot = bubble.transform.Find("FruitRoot");
            BubbleActor actor = bubble.GetComponent<BubbleActor>();
            BubbleFruitMotion motion = bubble.GetComponent<BubbleFruitMotion>();
            if (fruitRoot == null || actor == null || motion == null) return;
            ClearChildren(fruitRoot);

            List<FruitActor> fruits = new();
            for (int index = 0; index < types.Count; index++)
                fruits.Add(CreateFruit(fruitRoot, types[index], index, types.Count));
            actor.ReplaceFruits(fruits);
            motion.Configure(fruits.ToArray(), seed % 2 == 0 ? 1 : -1, 13.7f + seed);
        }

        private static FruitActor CreateFruit(Transform parent, FruitType type, int index, int count)
        {
            Sprite sprite = SpriteFor(type);
            GameObject fruitObject = new($"{type}_{index + 1:00}");
            fruitObject.transform.SetParent(parent, false);
            float angle = index * 2.399963f;
            float radius = Mathf.Sqrt((index + 0.5f) / Mathf.Max(1f, count)) * 1.35f;
            fruitObject.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            SpriteRenderer renderer = fruitObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 11;
            fruitObject.transform.localScale = Vector3.one * (0.9f / sprite.bounds.size.x);
            Rigidbody2D body = fruitObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 1.2f;
            body.angularDamping = 0.7f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = fruitObject.AddComponent<CircleCollider2D>();
            collider.radius = sprite.bounds.extents.x * 0.76f;
            FruitActor actor = fruitObject.AddComponent<FruitActor>();
            actor.Initialize(body, collider, renderer);
            actor.Configure(type, Color.white);
            actor.OnSpawned();
            return actor;
        }

    }
}
