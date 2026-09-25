#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using BubbleFruitLoop.Gameplay;
using BubbleFruitLoop.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BubbleFruitLoop.Editor
{
    [InitializeOnLoad]
    public static class PrototypeSceneBuilder
    {
        private const string Root = "Assets/_Game";
        private const string ResourcePath = Root + "/Resources/Prefabs";
        private const string MaterialPath = Root + "/Materials";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string BuildVersionKey = "BubbleFruitLoop.PrototypeBuildVersion";
        private const int BuildVersion = 4;
        private static readonly Color[] FruitColors =
        {
            new(0.95f, 0.12f, 0.12f), new(1f, 0.45f, 0.05f),
            new(0.55f, 0.18f, 0.85f), new(0.95f, 0.88f, 0.08f)
        };
        static PrototypeSceneBuilder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if ((!File.Exists(ScenePath) || EditorPrefs.GetInt(BuildVersionKey, 0) < BuildVersion) && !EditorApplication.isPlaying)
                EditorApplication.delayCall += BuildPrototype;
        }
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.GetInt(BuildVersionKey, 0) < BuildVersion)
                EditorApplication.delayCall += BuildPrototype;
        }
        [MenuItem("Tools/Bubble Fruit Loop/Rebuild Prototype Scene %#r")]
        public static void BuildPrototype()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
            CreateFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material[] fruitMaterials = CreateFruitMaterials();
            Material bubbleMaterial = CreateMaterial("Bubble", new Color(0.2f, 0.75f, 1f, 0.32f), true);
            Material boxMaterial = CreateMaterial("Box", Color.white, false);
            Material environmentMaterial = CreateMaterial("Environment", new Color(0.12f, 0.18f, 0.28f), false);
            CreatePrefabs(fruitMaterials[0], bubbleMaterial, boxMaterial, out FruitActor fruitPrefab,
                out BubbleActor bubblePrefab, out BoxView boxPrefab);
            CreateScene(scene, fruitPrefab, bubblePrefab, boxPrefab, fruitMaterials, environmentMaterial);
        }
        private static void CreateFolders()
        {
            Directory.CreateDirectory(ResourcePath);
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
            MaterialAssetMigration.MoveLegacyMaterials(MaterialPath);
        }
        private static Material[] CreateFruitMaterials()
        {
            Material[] materials = new Material[FruitColors.Length];
            for (int index = 0; index < materials.Length; index++)
                materials[index] = CreateMaterial($"Fruit_{(FruitType)index}", FruitColors[index], false);
            return materials;
        }

        private static Material CreateMaterial(string name, Color color, bool transparent)
        {
            string path = $"{MaterialPath}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            Material material = new(shader) { color = color, name = name };
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreatePrefabs(Material fruitMaterial, Material bubbleMaterial, Material boxMaterial,
            out FruitActor fruitPrefab, out BubbleActor bubblePrefab, out BoxView boxPrefab)
        {
            fruitPrefab = CreateFruitPrefab(fruitMaterial);
            bubblePrefab = CreateBubblePrefab(bubbleMaterial);
            boxPrefab = CreateBoxPrefab(boxMaterial);
        }

        private static FruitActor CreateFruitPrefab(Material material)
        {
            GameObject root = CreateModel("Fruit", PrimitiveType.Sphere, material, out MeshRenderer renderer);
            root.transform.localScale = Vector3.one * 0.42f;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            FruitActor actor = root.AddComponent<FruitActor>();
            actor.Initialize(body, collider, renderer);
            return SavePrefab(root, actor, "Fruit");
        }

        private static BubbleActor CreateBubblePrefab(Material material)
        {
            GameObject root = CreateModel("Bubble", PrimitiveType.Sphere, material, out MeshRenderer renderer);
            root.transform.localScale = new Vector3(2.35f, 1.55f, 0.35f);
            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.isTrigger = true;
            BubbleActor actor = root.AddComponent<BubbleActor>();
            actor.Initialize(collider, renderer);
            return SavePrefab(root, actor, "Bubble");
        }

        private static BoxView CreateBoxPrefab(Material material)
        {
            GameObject root = CreateModel("Box", PrimitiveType.Cube, material, out MeshRenderer renderer);
            root.transform.localScale = new Vector3(1.15f, 0.75f, 0.45f);
            BoxView actor = root.AddComponent<BoxView>();
            actor.Initialize(renderer);
            return SavePrefab(root, actor, "Box");
        }

        private static GameObject CreateModel(string name, PrimitiveType primitive, Material material, out MeshRenderer renderer)
        {
            GameObject root = new(name);
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>(primitive == PrimitiveType.Sphere ? "New-Sphere.fbx" : "Cube.fbx");
            renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return root;
        }

        private static T SavePrefab<T>(GameObject root, T component, string name) where T : Component
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, $"{ResourcePath}/{name}.prefab", InteractionMode.AutomatedAction);
            return component;
        }

        private static void CreateScene(Scene scene, FruitActor fruitPrefab, BubbleActor bubblePrefab, BoxView boxPrefab,
            Material[] fruitMaterials, Material environmentMaterial)
        {
            GameObject gameplay = new("--- GAMEPLAY ROOT ---");
            Camera camera = CreateCamera(gameplay.transform);
            SceneLightingFactory.Create(gameplay.transform);
            Transform poolRoot = CreateRoot("Pools", gameplay.transform);
            CreateEnvironment(gameplay.transform, environmentMaterial);
            CreateLayoutGuides(gameplay.transform);
            List<FruitActor> fruits = new(30);
            BubbleActor[] bubbles = CreateBubbles(gameplay.transform, bubblePrefab, fruitPrefab, fruitMaterials, fruits);
            BoxColumnAuthoring[] columns = CreateBoxes(gameplay.transform, boxPrefab, fruitMaterials);
            PrepareTemplate(fruitPrefab, poolRoot);
            PrepareTemplate(bubblePrefab, poolRoot);
            PrepareTemplate(boxPrefab, poolRoot);
            PrototypeBootstrap bootstrap = gameplay.AddComponent<PrototypeBootstrap>();
            bootstrap.ConfigureScene(camera, poolRoot, fruitPrefab, bubblePrefab, boxPrefab, bubbles, fruits.ToArray(), columns);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeObject = gameplay;
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(BuildVersionKey, BuildVersion);
        }

        private static void PrepareTemplate(Component template, Transform poolRoot)
        {
            template.transform.SetParent(poolRoot, false);
            template.gameObject.name += " Template";
            template.gameObject.SetActive(false);
        }

        private static Camera CreateCamera(Transform parent)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 0f, -14f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateEnvironment(Transform parent, Material material)
        {
            Transform environment = CreateRoot("Environment", parent);
            CreateBlock("Left Wall", new Vector3(-4.75f, 3.7f), new Vector3(0.18f, 8.4f, 0.5f), 0f, environment, material);
            CreateBlock("Right Wall", new Vector3(4.75f, 3.7f), new Vector3(0.18f, 8.4f, 0.5f), 0f, environment, material);
            CreateBlock("Funnel Left", new Vector3(-2.25f, -0.15f), new Vector3(4.8f, 0.16f, 0.5f), -18f, environment, material);
            CreateBlock("Funnel Right", new Vector3(2.25f, -0.15f), new Vector3(4.8f, 0.16f, 0.5f), 18f, environment, material);
            CreateLoopMarkers(environment, material);
        }

        private static BubbleActor[] CreateBubbles(Transform parent, BubbleActor bubblePrefab, FruitActor fruitPrefab,
            Material[] materials, List<FruitActor> allFruits)
        {
            Transform root = CreateRoot("Bubble Board", parent);
            BubbleActor[] bubbles = new BubbleActor[9];
            for (int bubbleIndex = 0; bubbleIndex < bubbles.Length; bubbleIndex++)
            {
                BubbleActor bubble = Object.Instantiate(bubblePrefab, root);
                bubble.name = $"Bubble_{bubbleIndex + 1:00}";
                bubble.transform.position = new Vector3(-3f + bubbleIndex % 3 * 3f, 6.4f - bubbleIndex / 3 * 2.15f);
                bubbles[bubbleIndex] = bubble;
                int fruitCount = bubbleIndex < 3 ? 4 : 3;
                for (int fruitIndex = 0; fruitIndex < fruitCount; fruitIndex++)
                {
                    FruitActor fruit = Object.Instantiate(fruitPrefab, bubble.transform);
                    int typeIndex = (bubbleIndex + fruitIndex) % FruitColors.Length;
                    fruit.name = $"Fruit_{(FruitType)typeIndex}_{fruitIndex + 1}";
                    fruit.Configure((FruitType)typeIndex, FruitColors[typeIndex]);
                    fruit.SetSharedMaterial(materials[typeIndex]);
                    fruit.transform.localPosition = new Vector3((fruitIndex % 2 - 0.5f) * 0.32f, (fruitIndex / 2 - 0.5f) * 0.42f, -0.5f);
                    fruit.transform.SetParent(root, true);
                    bubble.AddFruit(fruit);
                    allFruits.Add(fruit);
                }
            }
            return bubbles;
        }

        private static BoxColumnAuthoring[] CreateBoxes(Transform parent, BoxView boxPrefab, Material[] materials)
        {
            Transform root = CreateRoot("Box Board", parent);
            BoxColumnAuthoring[] columns = new BoxColumnAuthoring[3];
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                Transform columnRoot = CreateRoot($"Column_{columnIndex + 1}", root);
                Transform active = CreatePoint("Active Point", new Vector3(-1.5f + columnIndex * 1.5f, -6.2f), columnRoot);
                Transform pickup = CreatePoint("Pickup Point", new Vector3(-1.5f + columnIndex * 1.5f, -3.55f), columnRoot);
                BoxView[] boxes = new BoxView[2];
                for (int row = 0; row < boxes.Length; row++)
                {
                    boxes[row] = Object.Instantiate(boxPrefab, columnRoot);
                    int typeIndex = (columnIndex + row) % FruitColors.Length;
                    boxes[row].name = $"Box_{(FruitType)typeIndex}_4";
                    boxes[row].Configure((FruitType)typeIndex, 4, FruitColors[typeIndex]);
                    boxes[row].SetSharedMaterial(materials[typeIndex]);
                    boxes[row].transform.position = active.position + Vector3.down * row * 0.9f;
                }
                columns[columnIndex] = columnRoot.gameObject.AddComponent<BoxColumnAuthoring>();
                columns[columnIndex].Configure(active, pickup, boxes);
            }
            return columns;
        }

        private static void CreateLoopMarkers(Transform parent, Material material)
        {
            Transform root = CreateRoot("Loop Path Preview", parent);
            for (int index = 0; index < 32; index++)
            {
                float angle = index / 32f * Mathf.PI * 2f;
                Vector3 position = new(Mathf.Cos(angle) * 4.15f, -2.15f + Mathf.Sin(angle) * 1.65f, 0.3f);
                CreateBlock($"LoopMarker_{index:00}", position, Vector3.one * 0.09f, 0f, root, material, false);
            }
        }

        private static void CreateLayoutGuides(Transform parent)
        {
            Transform root = CreateRoot("Layout Guides - 1080x1920", parent);
            CreatePoint("UI Top Safe Area [Y 8.0 to 10.0]", new Vector3(0f, 9f), root);
            CreatePoint("Bubble Gameplay Area [Y 0.5 to 7.8]", new Vector3(0f, 4.15f), root);
            CreatePoint("Loop Counter UI Anchor", new Vector3(0f, -2.15f), root);
            CreatePoint("Box Gameplay Area [Y -8.8 to -5.2]", new Vector3(0f, -7f), root);
            CreatePoint("UI Bottom Safe Area [Y -10.0 to -9.0]", new Vector3(0f, -9.5f), root);
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, float angle,
            Transform parent, Material material, bool addCollider = true)
        {
            GameObject block = new(name);
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            block.transform.localScale = scale;
            MeshFilter filter = block.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            MeshRenderer renderer = block.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (addCollider) block.AddComponent<BoxCollider2D>();
            return block;
        }

        private static Transform CreateRoot(string name, Transform parent)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static Transform CreatePoint(string name, Vector3 position, Transform parent)
        {
            Transform point = CreateRoot(name, parent);
            point.position = position;
            return point;
        }
    }
}
#endif
