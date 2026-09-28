#if UNITY_EDITOR
using System.Collections.Generic;
using BubbleFruitLoop.Gameplay;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    public partial class LevelDesignManager
    {
        [Title("Full Level Fruit Size")]
        [MinValue(0.01f), LabelText("Fruit Transform Scale"), PropertyOrder(1001)]
        public float targetFruitScale = 0.18f;
        [LabelText("Also Update Fruit Prefab"), PropertyOrder(1002)]
        public bool updateFruitPrefabSize;
        [Range(0f, 0.15f), LabelText("Collider Extra Padding"), PropertyOrder(1002)]
        public float fruitColliderPadding = 0.01f;
        [Button("FIX FULL LEVEL FRUIT SIZE", ButtonSizes.Large), PropertyOrder(1003)]
        [GUIColor(0.35f, 1f, 0.55f)]
        public void FixFullLevelFruitSize()
        {
            float target = Mathf.Max(0.01f, targetFruitScale);
            Undo.SetCurrentGroupName("Fix Full Level Fruit Size");
            int undoGroup = Undo.GetCurrentGroup();
            Fruit[] sceneFruits = FindObjectsByType<Fruit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;
            ResizeSceneFruits(target, sceneFruits, ref changed);
            SaveFruitSizingChanges(target, undoGroup);
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Fruit Size Tool] Resized {changed} fruits to {target:0.###} and fitted every collider around its complete sprite.", this);
        }

        private static void SetFruitLocalScale(Fruit fruit, float target, bool recordUndo)
        {
            if (recordUndo)
                Undo.RecordObject(fruit.transform, "Resize Fruit");
            Vector3 scale = fruit.transform.localScale;
            fruit.transform.localScale = new Vector3(target, target, scale.z);
            EditorUtility.SetDirty(fruit.transform);
        }

        private static void FitColliderAroundFruit(Fruit fruit, float paddingRatio, bool recordUndo)
        {
            SpriteRenderer spriteRenderer = fruit.VisualSpriteRenderer;
            Collider2D collider = fruit.BodyCollider;
            if (collider == null)
                collider = fruit.GetComponentInChildren<Collider2D>(true);
            if (spriteRenderer == null || spriteRenderer.sprite == null || collider == null)
                return;
            if (recordUndo)
                Undo.RecordObject(collider, "Fit Fruit Collider");
            Sprite sprite = spriteRenderer.sprite;
            List<Vector2> outline = new();
            BuildFruitColliderShape(sprite, outline);
            if (outline.Count == 0)
                return;
            Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
            List<Vector2> colliderPoints = new(outline.Count);
            TransformFruitOutline(spriteRenderer, collider, outline, ref minimum, ref maximum, colliderPoints);
            Vector2 center = (minimum + maximum) * 0.5f;
            Vector2 size = maximum - minimum;
            float padding = 1f + Mathf.Max(0f, paddingRatio);
            if (collider is CircleCollider2D circle)
            {
                circle.offset = center;
                float tightRadius = 0f;
                for (int index = 0; index < colliderPoints.Count; index++)
                    tightRadius = Mathf.Max(tightRadius, Vector2.Distance(center, colliderPoints[index]));
                circle.radius = tightRadius * padding;
            }
            else if (collider is CapsuleCollider2D capsule)
            {
                capsule.offset = center;
                capsule.size = size * padding;
                capsule.direction = size.x >= size.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
            }
            else if (collider is BoxCollider2D box)
            {
                box.offset = center;
                box.size = size * padding;
            }
            else if (collider is PolygonCollider2D polygon)
            {
                FitFallbackCollider(center, size, padding, polygon);
            }

            EditorUtility.SetDirty(collider);
        }

        private void SyncFruitScalesToConfigsAndAsset()
        {
            for (int bubbleIndex = 0; bubbleIndex < bubbles.Count; bubbleIndex++)
            {
                BubbleConfig config = bubbles[bubbleIndex];
                if (config?.instance == null)
                    continue;
                Fruit[] fruits = config.instance.GetComponentsInChildren<Fruit>(true);
                config.fruitScales = new List<Vector3>(fruits.Length);
                for (int fruitIndex = 0; fruitIndex < fruits.Length; fruitIndex++)
                    config.fruitScales.Add(fruits[fruitIndex].transform.localScale);
                if (currentLevel == null || bubbleIndex >= currentLevel.bubbles.Count)
                    continue;
                currentLevel.bubbles[bubbleIndex].fruitScales = new List<Vector3>(config.fruitScales);
            }

            if (currentLevel != null)
                EditorUtility.SetDirty(currentLevel);
        }

#if UNITY_EDITOR
        private void SaveFruitSizingChanges(float target, int undoGroup)
        {
            SyncFruitScalesToConfigsAndAsset();
            if (updateFruitPrefabSize && fruitPrefab != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(fruitPrefab);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        Fruit prefabFruit = prefabRoot.GetComponentInChildren<Fruit>(true);
                        if (prefabFruit != null)
                        {
                            SetFruitLocalScale(prefabFruit, target, false);
                            FitColliderAroundFruit(prefabFruit, fruitColliderPadding, false);
                            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

#endif
#if UNITY_EDITOR
        private void ResizeSceneFruits(float target, Fruit[] sceneFruits, ref int changed)
        {
            for (int index = 0; index < sceneFruits.Length; index++)
            {
                Fruit fruit = sceneFruits[index];
                if (fruit == null || EditorUtility.IsPersistent(fruit))
                    continue;
                SetFruitLocalScale(fruit, target, true);
                FitColliderAroundFruit(fruit, fruitColliderPadding, true);
                changed++;
            }
        }

#endif
#if UNITY_EDITOR
        private static void BuildFruitColliderShape(Sprite sprite, List<Vector2> outline)
        {
            List<Vector2> shape = new();
            int shapeCount = sprite.GetPhysicsShapeCount();
            for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
            {
                shape.Clear();
                sprite.GetPhysicsShape(shapeIndex, shape);
                outline.AddRange(shape);
            }

            // Tight-mesh vertices also follow the visible alpha instead of the
            // often much larger transparent source canvas.
            if (outline.Count == 0 && sprite.vertices != null)
                outline.AddRange(sprite.vertices);
        }

#endif
#if UNITY_EDITOR
        private static void TransformFruitOutline(SpriteRenderer spriteRenderer, Collider2D collider, List<Vector2> outline, ref Vector2 minimum, ref Vector2 maximum, List<Vector2> colliderPoints)
        {
            for (int index = 0; index < outline.Count; index++)
            {
                Vector2 point = outline[index];
                if (spriteRenderer.flipX)
                    point.x = -point.x;
                if (spriteRenderer.flipY)
                    point.y = -point.y;
                Vector3 world = spriteRenderer.transform.TransformPoint(point);
                Vector3 local = collider.transform.InverseTransformPoint(world);
                colliderPoints.Add(local);
                minimum = Vector2.Min(minimum, local);
                maximum = Vector2.Max(maximum, local);
            }
        }

#endif
#if UNITY_EDITOR
        private static void FitFallbackCollider(Vector2 center, Vector2 size, float padding, PolygonCollider2D polygon)
        {
            Vector2 half = size * 0.5f * padding;
            polygon.pathCount = 1;
            polygon.SetPath(0, new[] { center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y), center + new Vector2(half.x, half.y), center + new Vector2(half.x, -half.y) });
        }
#endif
    }
}
#endif
