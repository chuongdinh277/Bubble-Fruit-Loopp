#if UNITY_EDITOR
using BubbleFruitLoop.Gameplay;
using TMPro;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    public sealed class BubbleBoardFactory
    {
        private readonly BubbleBoardAssets assets;
        private readonly Transform bubbleContainer;
        private int bubbleIndex;

        public BubbleBoardFactory(BubbleBoardAssets boardAssets)
        {
            assets = boardAssets;
            BoardRoot = new GameObject("--- BUBBLE BOARD ---");
            bubbleContainer = CreateChild("BubbleContainer", BoardRoot.transform);
        }

        public GameObject BoardRoot { get; }
        public FruitActor FruitSample { get; private set; }

        public void CreateBoard(Camera camera)
        {
            CreateBackground(camera);
            SpriteRenderer boardVisual = CreateBoardVisual();
            CreateProgressBar(boardVisual);
            Transform physicsRoot = CreateChild("BoardPhysics", BoardRoot.transform);
            CreateEdge("LeftWall", physicsRoot, new Vector2(-4.22f, -2.1f), new Vector2(-4.22f, 8.65f));
            CreateEdge("RightWall", physicsRoot, new Vector2(4.22f, 8.65f), new Vector2(4.22f, -2.1f));
            CreateEdge("BottomLeftSlope", physicsRoot, new Vector2(-4.22f, -2.1f), new Vector2(0f, -3.45f));
            CreateEdge("BottomRightSlope", physicsRoot, new Vector2(0f, -3.45f), new Vector2(4.22f, -2.1f));
            CreateSupportPegs(physicsRoot);
        }

        public GameObject CreateBubble(Vector2 position, int layoutIndex)
        {
            bubbleIndex++;
            GameObject root = new($"Bubble_{bubbleIndex:00}");
            root.transform.SetParent(bubbleContainer, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * 0.55f;
            Rigidbody2D body = CreateBubbleBody(root);
            CircleCollider2D outer = root.AddComponent<CircleCollider2D>();
            outer.radius = 2.32f;
            BubbleFruitMotion motion = root.AddComponent<BubbleFruitMotion>();
            BubbleActor actor = root.AddComponent<BubbleActor>();
            Transform visual = CreateChild("Visual", root.transform);
            MeshRenderer back = CreateBubbleMesh("BubbleBack", visual, assets.Back, 10, layoutIndex * 0.7f);
            MeshRenderer front = CreateBubbleMesh("BubbleFront", visual, assets.Front, 12, 2.4f + layoutIndex);
            Transform boundaryRoot = CreateChild("InnerBoundary", root.transform);
            EdgeCollider2D boundary = boundaryRoot.gameObject.AddComponent<EdgeCollider2D>();
            boundary.points = CreateCirclePoints(2.03f, 36);
            boundary.edgeRadius = 0.1f;
            Transform fruitRoot = CreateChild("FruitRoot", root.transform);
            FruitActor[] fruits = CreateFruits(fruitRoot, layoutIndex);
            for (int index = 0; index < fruits.Length; index++) actor.AddFruit(fruits[index]);
            motion.Configure(fruits, layoutIndex % 2 == 0 ? 1 : -1, 13.7f + layoutIndex);
            actor.Initialize(boundary, new Renderer[] { back, front }, motion);
            actor.ConfigurePhysics(body, outer, boundary);
            if (FruitSample == null) FruitSample = fruits[0];
            return root;
        }

        public void CreateRemainingBubbles()
        {
            Vector2[] positions =
            {
                new(-2.6f, 9.2f), new(0f, 10.2f), new(2.6f, 9.2f),
                new(-1.35f, 13f), new(1.35f, 13.8f), new(0f, 16.4f)
            };
            for (int index = 0; index < positions.Length; index++) CreateBubble(positions[index], index + 1);
        }

        private SpriteRenderer CreateBoardVisual()
        {
            GameObject visual = new("BoardVisual");
            visual.transform.SetParent(BoardRoot.transform, false);
            visual.transform.position = new Vector3(0f, 1.85f, 0f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = assets.Board;
            renderer.sortingOrder = -10;
            SetWorldWidth(visual.transform, assets.Board, 9.45f);
            return renderer;
        }

        private void CreateBackground(Camera camera)
        {
            GameObject background = new("Gameplay Background");
            background.transform.SetParent(BoardRoot.transform, false);
            background.transform.position = new Vector3(camera.transform.position.x,
                camera.transform.position.y, 2f);
            SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = assets.Background;
            renderer.sortingOrder = -100;
            float cameraHeight = camera.orthographicSize * 2f;
            background.transform.localScale = Vector3.one * (cameraHeight / assets.Background.bounds.size.y);
        }

        private void CreateProgressBar(SpriteRenderer boardVisual)
        {
            Transform root = CreateChild("Progress Bar (Inside Board)", BoardRoot.transform);
            Bounds boardBounds = boardVisual.bounds;
            // The lower rounded chamber occupies roughly the bottom 24% of the
            // authored board. Deriving this from rendered bounds keeps the bar
            // aligned after the board is moved or non-uniformly scaled.
            root.position = new Vector3(boardBounds.center.x,
                boardBounds.min.y + boardBounds.size.y * 0.125f, 0f);

            GameObject frameObject = new("Fill Frame");
            frameObject.transform.SetParent(root, false);
            SpriteRenderer frame = frameObject.AddComponent<SpriteRenderer>();
            frame.sprite = assets.FillFrame;
            frame.sortingOrder = -8;
            SetWorldWidth(frameObject.transform, assets.FillFrame, boardBounds.size.x * 0.62f);

            GameObject fillObject = new("Fill");
            fillObject.transform.SetParent(root, false);
            fillObject.transform.localPosition = Vector3.zero;
            SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
            fill.sprite = assets.Fill;
            fill.sortingOrder = -7;
            SetWorldWidth(fillObject.transform, assets.Fill, boardBounds.size.x * 0.55f);

            GameObject labelObject = new("Fruit Count");
            labelObject.transform.SetParent(root, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "0/30";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 64f;
            label.color = new Color(0.12f, 0.12f, 0.16f, 1f);

            LoopProgressDisplay display = root.gameObject.AddComponent<LoopProgressDisplay>();
            display.Configure(frame, fill, label);
        }

        private void CreateSupportPegs(Transform parent)
        {
            float[] positions = { -3.45f, -2.05f, -0.7f, 0.7f, 2.05f, 3.45f };
            for (int index = 0; index < positions.Length; index++)
            {
                GameObject peg = new($"SupportPeg_{index + 1:00}");
                peg.transform.SetParent(parent, false);
                peg.transform.position = new Vector3(positions[index], -1.85f, 0f);
                SpriteRenderer renderer = peg.AddComponent<SpriteRenderer>();
                renderer.sprite = assets.Peg;
                renderer.sortingOrder = 20;
                SetWorldWidth(peg.transform, assets.Peg, 0.48f);
                CircleCollider2D collider = peg.AddComponent<CircleCollider2D>();
                collider.radius = assets.Peg.bounds.extents.x * 0.78f;
            }
        }

        private FruitActor[] CreateFruits(Transform parent, int seed)
        {
            Vector2[] positions =
            {
                new(-0.95f, 0.75f), new(0f, 1.05f), new(0.95f, 0.7f), new(-1.05f, -0.15f),
                new(0f, 0.1f), new(1.05f, -0.2f), new(-0.55f, -0.95f), new(0.55f, -0.9f)
            };
            FruitActor[] fruits = new FruitActor[positions.Length];
            for (int index = 0; index < fruits.Length; index++)
            {
                bool orange = (index + seed) % 2 == 0;
                Sprite sprite = orange ? assets.Orange : assets.Strawberry;
                GameObject fruitObject = new(orange ? $"Orange_{index + 1:00}" : $"Strawberry_{index + 1:00}");
                fruitObject.transform.SetParent(parent, false);
                fruitObject.transform.localPosition = positions[index];
                SpriteRenderer renderer = fruitObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 11;
                SetWorldWidth(fruitObject.transform, sprite, 0.9f);
                Rigidbody2D body = fruitObject.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.linearDamping = 1.2f;
                body.angularDamping = 0.7f;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                CircleCollider2D collider = fruitObject.AddComponent<CircleCollider2D>();
                ConfigureTightCircle(collider, sprite);
                FruitActor fruit = fruitObject.AddComponent<FruitActor>();
                fruit.Initialize(body, collider, renderer);
                fruit.Configure(orange ? FruitType.Orange : FruitType.Strawberry, Color.white);
                fruit.OnSpawned();
                fruits[index] = fruit;
            }
            return fruits;
        }

        private static Rigidbody2D CreateBubbleBody(GameObject root)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0.22f;
            body.mass = 1.4f;
            body.linearDamping = 0.12f;
            body.angularDamping = 0.35f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            return body;
        }

        private static MeshRenderer CreateBubbleMesh(string name, Transform parent, Material material,
            int order, float phase)
        {
            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent, false);
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            BubbleDeformMesh deform = gameObject.AddComponent<BubbleDeformMesh>();
            deform.Initialize(filter, 40, 2.55f, 0.09f, 1.05f, phase);
            return renderer;
        }

        private static void CreateEdge(string name, Transform parent, Vector2 start, Vector2 end)
        {
            GameObject edge = new(name);
            edge.transform.SetParent(parent, false);
            EdgeCollider2D collider = edge.AddComponent<EdgeCollider2D>();
            collider.points = new[] { start, end };
            collider.edgeRadius = 0.08f;
        }

        private static Vector2[] CreateCirclePoints(float radius, int segments)
        {
            Vector2[] points = new Vector2[segments + 1];
            for (int index = 0; index <= segments; index++)
            {
                float angle = index / (float)segments * Mathf.PI * 2f;
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static void ConfigureTightCircle(CircleCollider2D collider, Sprite sprite)
        {
            Vector2 minimum = sprite.vertices[0];
            Vector2 maximum = minimum;
            for (int index = 1; index < sprite.vertices.Length; index++)
            {
                minimum = Vector2.Min(minimum, sprite.vertices[index]);
                maximum = Vector2.Max(maximum, sprite.vertices[index]);
            }
            collider.offset = (minimum + maximum) * 0.5f;
            collider.radius = Mathf.Min(maximum.x - minimum.x, maximum.y - minimum.y) * 0.46f;
        }

        private static void SetWorldWidth(Transform target, Sprite sprite, float width)
        {
            target.localScale = Vector3.one * (width / sprite.bounds.size.x);
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
#endif
