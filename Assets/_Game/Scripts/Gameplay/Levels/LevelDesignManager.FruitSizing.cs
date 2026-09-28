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

        [Button("FIX FULL LEVEL FRUIT SIZE", ButtonSizes.Large), PropertyOrder(1003)]
        [GUIColor(0.35f, 1f, 0.55f)]
        public void FixFullLevelFruitSize()
        {
            float target = Mathf.Max(0.01f, targetFruitScale);
            Undo.SetCurrentGroupName("Fix Full Level Fruit Size");
            int undoGroup = Undo.GetCurrentGroup();

            FruitActor[] sceneFruits = FindObjectsByType<FruitActor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;
            for (int index = 0; index < sceneFruits.Length; index++)
            {
                FruitActor fruit = sceneFruits[index];
                if (fruit == null || EditorUtility.IsPersistent(fruit)) continue;
                SetFruitLocalScale(fruit, target, true);
                changed++;
            }

            SyncFruitScalesToConfigsAndAsset();

            if (updateFruitPrefabSize && fruitPrefab != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(fruitPrefab);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        FruitActor prefabFruit = prefabRoot.GetComponentInChildren<FruitActor>(true);
                        if (prefabFruit != null)
                        {
                            SetFruitLocalScale(prefabFruit, target, false);
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
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Fruit Size Tool] Wrote local scale {target:0.###} directly to {changed} fruits.", this);
        }

        private static void SetFruitLocalScale(FruitActor fruit, float target, bool recordUndo)
        {
            if (recordUndo) Undo.RecordObject(fruit.transform, "Resize Fruit");
            Vector3 scale = fruit.transform.localScale;
            fruit.transform.localScale = new Vector3(target, target, scale.z);
            EditorUtility.SetDirty(fruit.transform);
        }

        private void SyncFruitScalesToConfigsAndAsset()
        {
            for (int bubbleIndex = 0; bubbleIndex < bubbles.Count; bubbleIndex++)
            {
                BubbleConfig config = bubbles[bubbleIndex];
                if (config?.instance == null) continue;

                FruitActor[] fruits = config.instance.GetComponentsInChildren<FruitActor>(true);
                config.fruitScales = new List<Vector3>(fruits.Length);
                for (int fruitIndex = 0; fruitIndex < fruits.Length; fruitIndex++)
                    config.fruitScales.Add(fruits[fruitIndex].transform.localScale);

                if (currentLevel == null || bubbleIndex >= currentLevel.bubbles.Count) continue;
                currentLevel.bubbles[bubbleIndex].fruitScales =
                    new List<Vector3>(config.fruitScales);
            }

            if (currentLevel != null) EditorUtility.SetDirty(currentLevel);
        }
    }
}
#endif
