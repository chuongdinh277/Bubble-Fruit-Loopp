#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    public sealed class BubbleFruitLevelPreviewWindow : EditorWindow
    {
        private const string BubbleRootName = "BubbleContainer";
        private const string BoxRootName = "BOX MODELS (EDIT LAYOUT)";
        private RenderTexture previewTexture;
        private GameObject cameraObject;
        private Camera previewCamera;
        private Vector2 center;
        private float orthographicSize = 10f;
        private double nextRepaint;

        [MenuItem("BubbleFruit/Level Preview")]
        public static void OpenWindow()
        {
            BubbleFruitLevelPreviewWindow window = GetWindow<BubbleFruitLevelPreviewWindow>("Bubble Fruit Level Preview");
            window.minSize = new Vector2(420f, 600f);
            window.Show();
            window.FocusAll();
        }

        private void OnEnable()
        {
            CreatePreviewCamera();
            EditorApplication.update += PreviewUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PreviewUpdate;
            ReleaseResources();
        }

        private void OnGUI()
        {
            DrawToolbar();
            Rect previewRect = GUILayoutUtility.GetRect(1f, 100000f, 1f, 100000f,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint) RenderPreview(previewRect);
            HandleNavigation(previewRect);
            DrawOverlay(previewRect);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Fit All", EditorStyles.toolbarButton)) FocusAll();
                if (GUILayout.Button("Bubbles", EditorStyles.toolbarButton)) FocusObject(BubbleRootName);
                if (GUILayout.Button("Boxes", EditorStyles.toolbarButton)) FocusObject(BoxRootName);
                GUILayout.Space(10f);
                GUILayout.Label($"Zoom {orthographicSize:0.0}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Level Designer", EditorStyles.toolbarButton))
                    GetWindow<BubbleFruitLevelDesignerWindow>("Bubble Fruit Level Designer").Show();
            }
        }

        private void RenderPreview(Rect rect)
        {
            Camera source = Camera.main;
            if (source == null)
            {
                EditorGUI.HelpBox(rect, "SampleScene chưa có Main Camera.", MessageType.Warning);
                return;
            }
            EnsureTexture(Mathf.Max(1, Mathf.RoundToInt(rect.width)), Mathf.Max(1, Mathf.RoundToInt(rect.height)));
            if (previewCamera == null) CreatePreviewCamera();
            previewCamera.CopyFrom(source);
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = orthographicSize;
            previewCamera.aspect = rect.width / Mathf.Max(1f, rect.height);
            previewCamera.transform.position = new Vector3(center.x, center.y, source.transform.position.z);
            previewCamera.transform.rotation = source.transform.rotation;
            previewCamera.targetTexture = previewTexture;
            previewCamera.Render();
            previewCamera.targetTexture = null;
            EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.StretchToFill);
        }

        private void HandleNavigation(Rect rect)
        {
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition)) return;
            if (current.type == EventType.ScrollWheel)
            {
                orthographicSize = Mathf.Clamp(orthographicSize * (1f + current.delta.y * 0.06f), 1.5f, 40f);
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseDrag && (current.button == 0 || current.button == 2))
            {
                float worldHeight = orthographicSize * 2f;
                float worldWidth = worldHeight * rect.width / Mathf.Max(1f, rect.height);
                center.x -= current.delta.x / rect.width * worldWidth;
                center.y += current.delta.y / rect.height * worldHeight;
                current.Use();
                Repaint();
            }
        }

        private static void DrawOverlay(Rect rect)
        {
            Rect label = new(rect.x + 8f, rect.y + 8f, 310f, 38f);
            EditorGUI.HelpBox(label, "Kéo chuột trái/giữa để di chuyển • Lăn chuột để zoom", MessageType.None);
        }

        private void FocusAll()
        {
            if (TryGetCombinedBounds(out Bounds bounds)) FocusBounds(bounds, 1.12f);
            else
            {
                Camera main = Camera.main;
                center = main != null ? (Vector2)main.transform.position : Vector2.zero;
                orthographicSize = main != null ? main.orthographicSize : 10f;
            }
            Repaint();
        }

        private void FocusObject(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null && TryGetBounds(target, out Bounds bounds)) FocusBounds(bounds, 1.18f);
            Repaint();
        }

        private void FocusBounds(Bounds bounds, float padding)
        {
            center = bounds.center;
            float aspect = Mathf.Max(0.2f, position.width / Mathf.Max(1f, position.height - 20f));
            orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * padding;
            orthographicSize = Mathf.Clamp(orthographicSize, 1.5f, 40f);
        }

        private static bool TryGetCombinedBounds(out Bounds bounds)
        {
            bool found = false;
            bounds = default;
            string[] names = { "--- BUBBLE BOARD ---", BoxRootName };
            for (int index = 0; index < names.Length; index++)
            {
                GameObject target = GameObject.Find(names[index]);
                if (target == null || !TryGetBounds(target, out Bounds item)) continue;
                if (!found) bounds = item;
                else bounds.Encapsulate(item);
                found = true;
            }
            return found;
        }

        private static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled) continue;
                if (!found) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                found = true;
            }
            return found;
        }

        private void CreatePreviewCamera()
        {
            if (cameraObject != null) return;
            cameraObject = new GameObject("BubbleFruit Level Preview Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
        }

        private void EnsureTexture(int width, int height)
        {
            if (previewTexture != null && previewTexture.width == width && previewTexture.height == height) return;
            if (previewTexture != null)
            {
                previewTexture.Release();
                DestroyImmediate(previewTexture);
            }
            previewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "BubbleFruit_LevelPreview",
                antiAliasing = 2,
                hideFlags = HideFlags.HideAndDontSave
            };
            previewTexture.Create();
        }

        private void PreviewUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextRepaint) return;
            nextRepaint = EditorApplication.timeSinceStartup + 0.1d;
            Repaint();
        }

        private void ReleaseResources()
        {
            if (previewTexture != null)
            {
                previewTexture.Release();
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }
            if (cameraObject != null)
            {
                DestroyImmediate(cameraObject);
                cameraObject = null;
                previewCamera = null;
            }
        }
    }
}
#endif
