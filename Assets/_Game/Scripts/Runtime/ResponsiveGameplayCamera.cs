using BubbleFruitLoop.Gameplay;
using UnityEngine;

namespace BubbleFruitLoop.Runtime
{
    [ExecuteAlways, RequireComponent(typeof(Camera)), DefaultExecutionOrder(-500)]
    public sealed class ResponsiveGameplayCamera : MonoBehaviour
    {
        public static readonly Vector2 DesignResolution = new Vector2(1080f, 1920f);
        [SerializeField, Min(0f)] private float referenceOrthographicSize;
        [SerializeField] private Vector3 referenceCameraPosition;
        [SerializeField] private SpriteRenderer background;
        private Camera targetCamera;
        private Sprite extensionSource;
        private Sprite topEdgeSprite;
        private Sprite bottomEdgeSprite;
        private SpriteRenderer topExtension;
        private SpriteRenderer bottomExtension;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LockMobileOrientation()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<FruitLoopManager>() == null) return;
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic
                && camera.GetComponent<ResponsiveGameplayCamera>() == null)
                camera.gameObject.AddComponent<ResponsiveGameplayCamera>();
        }

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            if (referenceOrthographicSize <= 0f)
            {
                referenceOrthographicSize = targetCamera.orthographicSize;
                referenceCameraPosition = targetCamera.transform.position;
            }
            ApplyLayout();
        }

        private void LateUpdate() => ApplyLayout();

        public static Rect GetSafeScreenRect()
        {
            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            Rect safe = Application.isPlaying ? Screen.safeArea : full;
            return safe.width > 0f && safe.height > 0f ? safe : full;
        }

        public static float CalculateDesignScale(Rect safe)
        {
            return Mathf.Max(0.0001f, Mathf.Min(safe.width / DesignResolution.x,
                safe.height / DesignResolution.y));
        }

        public void ApplyLayout()
        {
            if (targetCamera == null || !targetCamera.orthographic
                || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = GetSafeScreenRect();
            float pixelsPerWorldUnit = CalculateDesignScale(safe)
                * DesignResolution.y / (referenceOrthographicSize * 2f);
            targetCamera.orthographicSize = Screen.height / (2f * pixelsPerWorldUnit);
            Vector2 screenOffset = safe.center - new Vector2(Screen.width, Screen.height) * 0.5f;
            targetCamera.transform.position = referenceCameraPosition
                - new Vector3(screenOffset.x, screenOffset.y, 0f) / pixelsPerWorldUnit;

            if (background == null)
            {
                GameObject found = GameObject.Find("Gameplay Background");
                if (found != null) background = found.GetComponent<SpriteRenderer>();
            }
            if (background == null || background.sprite == null) return;
            Vector2 spriteSize = background.sprite.bounds.size;
            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            // The moulding line is part of the gameplay layout, not a free
            // background decoration. Its world Y must not change with aspect.
            float designHeight = referenceOrthographicSize * 2f;
            float verticalScale = designHeight / Mathf.Max(0.001f, spriteSize.y);
            float horizontalScale = Mathf.Max(verticalScale,
                width / Mathf.Max(0.001f, spriteSize.x));
            Vector3 parentScale = background.transform.parent != null
                ? background.transform.parent.lossyScale : Vector3.one;
            background.transform.localScale = new Vector3(
                horizontalScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                verticalScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            Vector3 position = background.transform.position;
            position.x = targetCamera.transform.position.x;
            position.y = referenceCameraPosition.y;
            background.transform.position = position;
            ExtendBackgroundEdges(height, width, designHeight);
        }

        private void ExtendBackgroundEdges(float viewHeight, float viewWidth, float designHeight)
        {
            if (extensionSource != background.sprite || topExtension == null || bottomExtension == null)
            {
                ClearExtensions();
                extensionSource = background.sprite;
                Rect rect = extensionSource.textureRect;
                float edgeHeight = Mathf.Min(2f, rect.height);
                topEdgeSprite = Sprite.Create(extensionSource.texture,
                    new Rect(rect.x, rect.yMax - edgeHeight, rect.width, edgeHeight),
                    new Vector2(0.5f, 0.5f), extensionSource.pixelsPerUnit,
                    0, SpriteMeshType.FullRect);
                bottomEdgeSprite = Sprite.Create(extensionSource.texture,
                    new Rect(rect.x, rect.y, rect.width, edgeHeight),
                    new Vector2(0.5f, 0.5f), extensionSource.pixelsPerUnit,
                    0, SpriteMeshType.FullRect);
                topEdgeSprite.hideFlags = bottomEdgeSprite.hideFlags = HideFlags.DontSave;
                topExtension = CreateExtension("Background Top Extension", topEdgeSprite);
                bottomExtension = CreateExtension("Background Bottom Extension", bottomEdgeSprite);
            }
            float backgroundTop = referenceCameraPosition.y + designHeight * 0.5f;
            float backgroundBottom = referenceCameraPosition.y - designHeight * 0.5f;
            float viewTop = targetCamera.transform.position.y + viewHeight * 0.5f;
            float viewBottom = targetCamera.transform.position.y - viewHeight * 0.5f;
            SetExtensionGeometry(topExtension, viewWidth, Mathf.Max(0f, viewTop - backgroundTop),
                (viewTop + backgroundTop) * 0.5f);
            SetExtensionGeometry(bottomExtension, viewWidth, Mathf.Max(0f, backgroundBottom - viewBottom),
                (viewBottom + backgroundBottom) * 0.5f);
        }

        private SpriteRenderer CreateExtension(string objectName, Sprite sprite)
        {
            GameObject child = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(background.transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = background.sharedMaterial;
            return renderer;
        }

        private void SetExtensionGeometry(SpriteRenderer renderer, float width, float height, float centerY)
        {
            renderer.enabled = height > 0.0001f;
            if (!renderer.enabled) return;
            renderer.sortingLayerID = background.sortingLayerID;
            renderer.sortingOrder = background.sortingOrder;
            renderer.color = background.color;
            renderer.transform.position = new Vector3(targetCamera.transform.position.x,
                centerY, background.transform.position.z);
            Vector3 parentScale = background.transform.lossyScale;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.001f, spriteSize.x * Mathf.Abs(parentScale.x)),
                (height + 0.002f) / Mathf.Max(0.001f, spriteSize.y * Mathf.Abs(parentScale.y)), 1f);
        }

        private void ClearExtensions()
        {
            DestroyGenerated(topExtension != null ? topExtension.gameObject : null);
            DestroyGenerated(bottomExtension != null ? bottomExtension.gameObject : null);
            DestroyGenerated(topEdgeSprite);
            DestroyGenerated(bottomEdgeSprite);
            topExtension = bottomExtension = null;
            topEdgeSprite = bottomEdgeSprite = extensionSource = null;
        }

        private static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }

        private void OnDisable()
        {
            ClearExtensions();
            if (targetCamera == null || referenceOrthographicSize <= 0f) return;
            targetCamera.orthographicSize = referenceOrthographicSize;
            targetCamera.transform.position = referenceCameraPosition;
        }
    }
}
