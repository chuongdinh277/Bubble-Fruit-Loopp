#if UNITY_EDITOR
using System.IO;
using BubbleFruitLoop.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubbleFruitLoop.Editor
{
    [InitializeOnLoad]
    public static class BubbleShowcaseSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TexturePath = "Assets/_Game/Texture/Gameplay";
        private const string PrefabPath = "Assets/_Game/Resources/Prefabs";
        private const string BuildKey = "BubbleFruitLoop.BoardPhysicsBuild";
        private const int BuildVersion = 1;

        static BubbleShowcaseSceneBuilder()
        {
            if (EditorPrefs.GetInt(BuildKey, 0) < BuildVersion && !EditorApplication.isPlaying)
                EditorApplication.delayCall += Build;
        }

        [MenuItem("Tools/Bubble Fruit Loop/Build Physics Board %#b")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            BubbleBoardAssets assets = LoadAssets();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera();
            BubbleBoardFactory factory = new(assets);
            factory.CreateBoard(camera);
            GameObject prefabSource = factory.CreateBubble(new Vector2(0f, 12f), 0);
            CreatePrefabs(prefabSource, factory.FruitSample);
            factory.CreateRemainingBubbles();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(BuildKey, BuildVersion);
            Selection.activeObject = factory.BoardRoot;
        }

        private static BubbleBoardAssets LoadAssets()
        {
            Sprite bubble = LoadSprite("bubble-removebg-preview.png");
            return new BubbleBoardAssets(
                bubble,
                LoadSprite("bubbleBoard-removebg-preview.png"),
                LoadSprite("bubblechot-removebg-preview.png"),
                LoadSprite("bg.png"),
                LoadSprite("bgfill-removebg-preview.png"),
                LoadSprite("fill-removebg-preview.png"),
                LoadSprite("quacam-removebg-preview.png"),
                LoadSprite("quadau-removebg-preview.png"),
                CreateBubbleMaterial(bubble, "BubbleMeshBack", 0.28f),
                CreateBubbleMaterial(bubble, "BubbleMeshFront", 0.92f));
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.14f, 0.17f, 0.29f);
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static Sprite LoadSprite(string fileName)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturePath}/{fileName}");
            if (sprite == null) throw new MissingReferenceException($"Sprite not found: {fileName}");
            return sprite;
        }

        private static Material CreateBubbleMaterial(Sprite sprite, string name, float alpha)
        {
            string path = $"Assets/_Game/Materials/{name}.mat";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Unlit-Default.shader");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_MainTex", sprite.texture);
            material.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreatePrefabs(GameObject bubbleRoot, FruitActor fruitSample)
        {
            Directory.CreateDirectory(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(bubbleRoot, PrefabPath + "/BubbleAsset2D.prefab");
            PrefabUtility.SaveAsPrefabAsset(fruitSample.gameObject, PrefabPath + "/FruitAsset2D.prefab");
        }
    }

    public readonly struct BubbleBoardAssets
    {
        public BubbleBoardAssets(Sprite bubble, Sprite board, Sprite peg, Sprite background,
            Sprite fillFrame, Sprite fill, Sprite orange, Sprite strawberry,
            Material back, Material front)
        {
            Bubble = bubble;
            Board = board;
            Peg = peg;
            Background = background;
            FillFrame = fillFrame;
            Fill = fill;
            Orange = orange;
            Strawberry = strawberry;
            Back = back;
            Front = front;
        }

        public Sprite Bubble { get; }
        public Sprite Board { get; }
        public Sprite Peg { get; }
        public Sprite Background { get; }
        public Sprite FillFrame { get; }
        public Sprite Fill { get; }
        public Sprite Orange { get; }
        public Sprite Strawberry { get; }
        public Material Back { get; }
        public Material Front { get; }
    }
}
#endif
