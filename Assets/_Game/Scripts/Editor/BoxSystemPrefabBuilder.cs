#if UNITY_EDITOR
using BubbleFruitLoop.Gameplay;
using UnityEditor;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    public static partial class BoxSystemPrefabBuilder
    {
        private const string ResourceFolder = "Assets/_Game/Resources";
        private const string ArtFolder = "Assets/_Game/Art/Box";
        private const string VersionKey = "BubbleFruitLoop.BoxPrefabVersion";
        private const int Version = 30;

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticRebuild()
        {
            if (EditorPrefs.GetInt(VersionKey, 0) >= Version && PrefabUsesCurrent25DLayout()) return;
            EditorApplication.delayCall += RebuildWhenReady;
        }

        private static bool PrefabUsesCurrent25DLayout()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ResourceFolder}/Box4.prefab");
            return prefab != null && prefab.transform.Find("Visual Root (2.5D)") != null;
        }

        private static void RebuildWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RebuildWhenReady;
                return;
            }
            GeneratePrefabs();
            EditorPrefs.SetInt(VersionKey, Version);
        }

        [MenuItem("BubbleFruit/Rebuild Box 4 Prefab")]
        public static void GeneratePrefabs()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!CreateBoxPrefab("Box4", 4)) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt Box4 as layered 2.5D art with thin hinged 3D doors.");
        }

        private static bool CreateBoxPrefab(string prefabName, int capacity)
        {
            const string openArtworkPath = ArtFolder + "/Box4_Open_3D.png";
            const string roundedPanelPath = ArtFolder + "/RoundedBoxPanel.png";
            Sprite openSprite = ImportArtwork(openArtworkPath);
            // This file used to be imported as Multiple without any slices, so
            // LoadAllAssets returned no Sprite and silently left the old 3D
            // prefab in place. Force it to one normal sprite.
            Sprite roundedSprite = ImportArtwork(roundedPanelPath);
            Material colorizeMaterial = EnsureColorizeMaterial();
            Material boxMaterial = EnsureModelMaterial();
            if (openSprite == null || roundedSprite == null || colorizeMaterial == null || boxMaterial == null)
            {
                Debug.LogError($"Missing 2.5D Box4 art or material for {prefabName}.");
                return false;
            }

            GameObject root = new(prefabName);
            BoxView view = root.AddComponent<BoxView>();

            Transform visualRoot = new GameObject("Visual Root (2.5D)").transform;
            visualRoot.SetParent(root.transform, false);

            CreateSpriteLayer("Contact Shadow", visualRoot, roundedSprite,
                new Vector3(0f, -0.075f, 0.08f), new Vector2(0.84f, 1.04f),
                new Color(0f, 0f, 0f, 0.10f), 0, null);
            SpriteRenderer depthBack = CreateSpriteLayer("Depth Back", visualRoot, roundedSprite,
                new Vector3(0f, -0.035f, 0.04f), new Vector2(0.88f, 1.08f),
                Color.white, 5, colorizeMaterial);
            SpriteRenderer cavity = CreateArtwork("Cavity + BackBody", visualRoot, openSprite,
                capacity, true, 15, 1f);
            cavity.sharedMaterial = colorizeMaterial;
            CreateBodySilhouetteMask(visualRoot, roundedSprite, cavity);

            CreateDividerPair(visualRoot, roundedSprite, true);
            CreateDividerPair(visualRoot, roundedSprite, false);
            CreateRimLighting(visualRoot, roundedSprite);
            CreateSpriteLayer("Left Door Shadow", visualRoot, roundedSprite,
                new Vector3(-0.30f, -0.04f, 0.02f), new Vector2(0.44f, 1.00f),
                new Color(0f, 0f, 0f, 0.08f), 35, null);
            CreateSpriteLayer("Right Door Shadow", visualRoot, roundedSprite,
                new Vector3(0.30f, -0.04f, 0.02f), new Vector2(0.44f, 1.00f),
                new Color(0f, 0f, 0f, 0.08f), 35, null);

            Transform leftLid = CreateLidModel("Left Door", visualRoot, boxMaterial, 1.12f, true);
            Transform rightLid = CreateLidModel("Right Door", visualRoot, boxMaterial, 1.12f, false);
            Transform[] slots = CreateSlots(visualRoot, capacity);

            SerializedObject serialized = new(view);
            serialized.FindProperty("boxRenderer").objectReferenceValue = cavity;
            serialized.FindProperty("openArtwork").objectReferenceValue = cavity;
            serialized.FindProperty("closedArtwork").objectReferenceValue = null;
            serialized.FindProperty("leftLid").objectReferenceValue = leftLid;
            serialized.FindProperty("rightLid").objectReferenceValue = rightLid;
            serialized.FindProperty("innerTray").objectReferenceValue = cavity.transform;
            serialized.FindProperty("backShadow").objectReferenceValue = depthBack.transform;
            serialized.FindProperty("editorCapacity").intValue = capacity;
            serialized.FindProperty("doorOpenAngle").floatValue = 78f;
            serialized.FindProperty("lidDuration").floatValue = 0.23f;
            serialized.FindProperty("closeLidDuration").floatValue = 0.20f;
            serialized.FindProperty("rightDoorDelay").floatValue = 0.03f;
            SerializedProperty slotProperty = serialized.FindProperty("fruitSlots");
            slotProperty.arraySize = capacity;
            for (int index = 0; index < capacity; index++)
                slotProperty.GetArrayElementAtIndex(index).objectReferenceValue = slots[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, $"{ResourceFolder}/{prefabName}.prefab");
            ValidateMeasurements(root);
            Object.DestroyImmediate(root);
            return true;
        }

        [MenuItem("BubbleFruit/Validate Box 4 (2.5D)")]
        private static void ValidateSavedPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ResourceFolder}/Box4.prefab");
            if (prefab == null)
            {
                Debug.LogError("Box4 prefab is missing.");
                return;
            }
            ValidateMeasurements(prefab);
        }

        private static void ValidateMeasurements(GameObject root)
        {
            Transform visual = root.transform.Find("Visual Root (2.5D)");
            Transform leftHinge = visual != null ? visual.Find("Left Door Hinge") : null;
            Transform rightHinge = visual != null ? visual.Find("Right Door Hinge") : null;
            Transform leftPanel = leftHinge != null ? leftHinge.Find("Left Door Panel") : null;
            BoxView view = root.GetComponent<BoxView>();
            SerializedObject serialized = view != null ? new SerializedObject(view) : null;
            int slotCount = serialized?.FindProperty("fruitSlots")?.arraySize ?? 0;

            bool valid = visual != null && leftHinge != null && rightHinge != null && leftPanel != null
                && slotCount == 4
                && Mathf.Abs(Mathf.Abs(leftHinge.localPosition.x) - 0.51f) < 0.001f
                && Mathf.Abs(Mathf.Abs(rightHinge.localPosition.x) - 0.51f) < 0.001f
                && Mathf.Abs(leftHinge.localPosition.y) < 0.001f
                && Mathf.Abs(rightHinge.localPosition.y) < 0.001f
                && Mathf.Abs(leftPanel.localScale.x - 0.49f) < 0.001f
                && Mathf.Abs(leftPanel.localScale.y - 1.12f) < 0.001f
                && Mathf.Abs(leftPanel.localScale.z - 0.045f) < 0.001f;

            if (valid)
                Debug.Log("Box4 2.5D validated: 4 slots and two 0.045-thick side-hinged doors.");
            else
                Debug.LogError("Box4 2.5D validation failed. Rebuild it from BubbleFruit/Rebuild Box 4 Prefab.");
        }

    }
}
#endif
