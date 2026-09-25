#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Gameplay;
using TMPro;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Editor
{
    [InitializeOnLoad]
    public static class GameplayPresentationInstaller
    {
        private const string TextureRoot = "Assets/_Game/Texture/Gameplay/";

        static GameplayPresentationInstaller() => EditorApplication.delayCall += InstallInOpenScene;

        [MenuItem("BubbleFruit/Repair Complete Sample Scene")]
        public static void RepairCompleteSampleScene()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            if (SceneManager.GetActiveScene().path != scenePath)
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            BoxSceneLayoutBuilder.BuildLayout();
            InstallInOpenScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("SampleScene repaired: presentation, progress bar, editable boxes and gameplay UI restored.");
        }

        [MenuItem("BubbleFruit/Repair Gameplay Background + Fill Bar")]
        public static void InstallInOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            EnsureSettingPrefabController();
            GameObject board = GameObject.Find("--- BUBBLE BOARD ---") ?? GameObject.Find("Bubble Board");
            Camera camera = Camera.main;
            if (board == null || camera == null) return;
            RepairFruitPaletteMapping();

            bool changed = false;
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "bg.png");
            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "bgfill-removebg-preview.png");
            Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "fill-removebg-preview.png");
            if (backgroundSprite == null || frameSprite == null || fillSprite == null) return;

            Transform background = board.transform.Find("Gameplay Background");
            if (background == null)
            {
                background = new GameObject("Gameplay Background").transform;
                background.SetParent(board.transform, false);
                changed = true;
            }
            SpriteRenderer backgroundRenderer = GetOrAddRenderer(background.gameObject, ref changed);
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingOrder = -100;
            background.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 2f);
            background.localScale = Vector3.one * (camera.orthographicSize * 2f / backgroundSprite.bounds.size.y);

            Transform progress = board.transform.Find("Progress Bar (Inside Board)");
            if (progress == null)
            {
                progress = new GameObject("Progress Bar (Inside Board)").transform;
                progress.SetParent(board.transform, false);
                changed = true;
            }
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(progress.gameObject) > 0)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(progress.gameObject);
                changed = true;
            }
            SpriteRenderer boardRenderer = board.transform.Find("BoardVisual")?.GetComponent<SpriteRenderer>();
            if (boardRenderer == null) return;
            Bounds boardBounds = boardRenderer.bounds;
            progress.position = new Vector3(boardBounds.center.x,
                boardBounds.min.y + boardBounds.size.y * 0.125f, 0f);

            SpriteRenderer frameRenderer = ConfigureLayer(progress, "Fill Frame", frameSprite,
                boardBounds.size.x * 0.62f,
                -8, Vector3.zero, ref changed);
            SpriteRenderer fillRenderer = ConfigureLayer(progress, "Fill", fillSprite,
                boardBounds.size.x * 0.55f, -7, Vector3.zero, ref changed);

            Transform labelTransform = progress.Find("Fruit Count");
            if (labelTransform == null)
            {
                labelTransform = new GameObject("Fruit Count").transform;
                labelTransform.SetParent(progress, false);
                changed = true;
            }
            labelTransform.localPosition = new Vector3(0f, 0f, -0.05f);
            TextMeshProUGUI label = labelTransform.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                TextMesh oldLabel = labelTransform.GetComponent<TextMesh>();
                if (oldLabel != null) Object.DestroyImmediate(oldLabel);
                label = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();
                changed = true;
            }
            label.text = "0/30";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 64f;
            label.color = new Color(0.12f, 0.12f, 0.16f, 1f);

            LoopProgressDisplay display = progress.GetComponent<LoopProgressDisplay>();
            if (display == null)
            {
                display = progress.gameObject.AddComponent<LoopProgressDisplay>();
                changed = true;
            }
            display.Configure(frameRenderer, fillRenderer, label,
                Object.FindFirstObjectByType<EditableFruitLoopController>());
            display.AlignInsideLoopBoundary();

            LowerBoxLayout(boardBounds);
            RemoveRuntimeUiFromScene();

            // Existing objects may only need their coordinates repaired.
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = progress.gameObject;
        }

        private static void RepairFruitPaletteMapping()
        {
            LevelLoader loader = Object.FindFirstObjectByType<LevelLoader>();
            if (loader == null) return;

            // Arrays must follow FruitType's numeric order:
            // Apple, Orange, Grape, Lemon, Strawberry.
            loader.fruitMaterials = new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Apple.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Orange.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Grape.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Lemon.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Fruit_Strawberry.mat")
            };
            loader.fruitSprites = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "quablue-removebg-preview.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "quacam-removebg-preview.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "quapurple-removebg-preview.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "quachanh-removebg-preview.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "quadau-removebg-preview.png")
            };
            EditorUtility.SetDirty(loader);
        }

        private static void EnsureSettingPrefabController()
        {
            const string path = "Assets/_Game/Resources/UI/UICanvasGameSetting.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null) return;
            try
            {
                if (contents.GetComponent<UICanvasGameSetting>() != null) return;
                contents.AddComponent<UICanvasGameSetting>();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void RemoveRuntimeUiFromScene()
        {
            string[] runtimeCanvasNames = { "UICanvasGameplay", "UICanvasGameSetting" };
            for (int index = 0; index < runtimeCanvasNames.Length; index++)
            {
                GameObject existing = FindSceneObject(runtimeCanvasNames[index]);
                if (existing != null) Object.DestroyImmediate(existing);
            }
        }

        private static GameObject FindSceneObject(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int index = 0; index < objects.Length; index++)
                if (objects[index].name == objectName && objects[index].scene == SceneManager.GetActiveScene())
                    return objects[index];
            return null;
        }

        private static SpriteRenderer ConfigureLayer(Transform parent, string name, Sprite sprite, float width,
            int sortingOrder, Vector3 localPosition, ref bool changed)
        {
            Transform layer = parent.Find(name);
            if (layer == null)
            {
                layer = new GameObject(name).transform;
                layer.SetParent(parent, false);
                changed = true;
            }
            SpriteRenderer renderer = GetOrAddRenderer(layer.gameObject, ref changed);
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            layer.localPosition = localPosition;
            layer.localScale = Vector3.one * (width / sprite.bounds.size.x);
            return renderer;
        }

        private static SpriteRenderer GetOrAddRenderer(GameObject target, ref bool changed)
        {
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null) return renderer;
            changed = true;
            return target.AddComponent<SpriteRenderer>();
        }

        private static void LowerBoxLayout(Bounds boardBounds)
        {
            GameObject boxRoot = GameObject.Find("BOX MODELS (EDIT LAYOUT)")
                ?? GameObject.Find("BoxContainer")
                ?? GameObject.Find("Box Board");
            if (boxRoot == null) return;
            BoxView[] boxes = boxRoot.GetComponentsInChildren<BoxView>(true);
            if (boxes.Length == 0) return;

            float currentTop = float.NegativeInfinity;
            for (int index = 0; index < boxes.Length; index++)
            {
                Renderer[] renderers = boxes[index].GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    currentTop = Mathf.Max(currentTop, renderers[rendererIndex].bounds.max.y);
            }
            if (float.IsNegativeInfinity(currentTop)) return;

            float desiredTop = boardBounds.min.y - 0.42f;
            float offset = desiredTop - currentTop;
            if (Mathf.Abs(offset) < 0.01f) return;
            for (int index = 0; index < boxes.Length; index++)
                boxes[index].transform.position += Vector3.up * offset;
        }
    }
}
#endif
