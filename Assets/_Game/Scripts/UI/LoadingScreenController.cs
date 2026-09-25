using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubbleFruitLoop.UI
{
    [DefaultExecutionOrder(-10000)]
    public sealed class LoadingScreenController : MonoBehaviour
    {
        private sealed class Fragment
        {
            public RectTransform Rect;
            public Graphic Graphic;
            public Vector2 Direction;
            public float Delay;
            public float Rotation;
        }

        private const int Columns = 12;
        private const int Rows = 20;
        private const float HoldDuration = 1.25f;
        private const float BreakDuration = 1.05f;
        private static LoadingScreenController instance;
        private Coroutine transition;
        private float gameplayTimeScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            GameObject host = new("Loading Screen Controller");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<LoadingScreenController>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Play();
        }

        public static void Play()
        {
            if (instance == null) Install();
            if (instance == null) return;
            if (instance.transition == null)
                instance.gameplayTimeScale = Mathf.Approximately(Time.timeScale, 0f)
                    ? 1f
                    : Time.timeScale;
            // Freeze gameplay before the first Update/FixedUpdate of the scene.
            Time.timeScale = 0f;
            if (instance.transition != null) instance.StopCoroutine(instance.transition);
            instance.transition = instance.StartCoroutine(instance.PlayLoadingReveal());
        }

        private IEnumerator PlayLoadingReveal()
        {
            // Create/show the cover synchronously before yielding so the very first
            // visible frame is loading artwork rather than live gameplay.
            GameObject loading = FindSceneLoadingCanvas();
            if (loading == null)
            {
                GameObject prefab = Resources.Load<GameObject>("UI/UICanvasLoading");
                if (prefab == null)
                {
                    FinishTransition(null);
                    yield break;
                }
                loading = Instantiate(prefab);
            }

            UICanvasLoading loadingView = loading.GetComponent<UICanvasLoading>();
            if (loadingView == null) loadingView = loading.AddComponent<UICanvasLoading>();
            loadingView.Setup();
            loadingView.PrepareForReveal();

            Image source = loadingView.Artwork;
            if (source == null || source.sprite == null)
            {
                yield return new WaitForSecondsRealtime(HoldDuration);
                loadingView.FinishReveal();
                FinishTransition(loadingView);
                yield break;
            }

            source.gameObject.SetActive(true);
            source.color = new Color(source.color.r, source.color.g, source.color.b, 1f);
            // Keep the intact artwork visible for a real loading beat. Building and
            // swapping to fragments before this wait caused a one-frame flash on
            // some GPUs because the RawImages had not rendered yet.
            yield return new WaitForSecondsRealtime(HoldDuration);

            List<Fragment> fragments = BuildFragments(source);
            // Give the newly-created fragment graphics one full render frame while
            // the intact image is still covering them, then begin the break.
            yield return new WaitForEndOfFrame();
            source.enabled = false;

            float elapsed = 0f;
            while (elapsed < BreakDuration + 0.24f)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int index = 0; index < fragments.Count; index++)
                {
                    Fragment fragment = fragments[index];
                    if (fragment?.Rect == null) continue;
                    float progress = Mathf.Clamp01((elapsed - fragment.Delay) / BreakDuration);
                    float eased = 1f - Mathf.Pow(1f - progress, 3f);
                    fragment.Rect.anchoredPosition = fragment.Direction * (105f * eased)
                        + Vector2.down * (45f * eased * eased);
                    fragment.Rect.localRotation = Quaternion.Euler(0f, 0f,
                        fragment.Rotation * eased);
                    fragment.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, eased);
                    Color color = fragment.Graphic.color;
                    color.a = 1f - Mathf.SmoothStep(0.16f, 1f, progress);
                    fragment.Graphic.color = color;
                }
                yield return null;
            }

            for (int index = 0; index < fragments.Count; index++)
                if (fragments[index]?.Rect != null) Destroy(fragments[index].Rect.gameObject);
            source.enabled = true;
            loadingView.FinishReveal();
            FinishTransition(loadingView);
        }

        private void FinishTransition(UICanvasLoading loadingView)
        {
            if (loadingView != null && loadingView.gameObject.activeSelf)
                loadingView.FinishReveal();
            Time.timeScale = gameplayTimeScale;
            transition = null;
        }

        private static List<Fragment> BuildFragments(Image source)
        {
            List<Fragment> fragments = new(Columns * Rows);
            Rect spriteRect = source.sprite.textureRect;
            Texture texture = source.sprite.texture;
            RectTransform parent = source.rectTransform;

            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                GameObject piece = new($"Loading Fragment {column}-{row}",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform rect = (RectTransform)piece.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(column / (float)Columns, row / (float)Rows);
                rect.anchorMax = new Vector2((column + 1f) / Columns, (row + 1f) / Rows);
                rect.offsetMin = new Vector2(-0.6f, -0.6f);
                rect.offsetMax = new Vector2(0.6f, 0.6f);

                RawImage image = piece.GetComponent<RawImage>();
                image.texture = texture;
                image.material = source.material;
                image.color = source.color;
                image.raycastTarget = false;
                image.uvRect = new Rect(
                    (spriteRect.x + spriteRect.width * column / Columns) / texture.width,
                    (spriteRect.y + spriteRect.height * row / Rows) / texture.height,
                    spriteRect.width / Columns / texture.width,
                    spriteRect.height / Rows / texture.height);

                Vector2 normalized = new(
                    (column + 0.5f) / Columns * 2f - 1f,
                    (row + 0.5f) / Rows * 2f - 1f);
                float randomX = Hash01(column, row, 17) * 2f - 1f;
                float randomY = Hash01(column, row, 31) * 2f - 1f;
                Vector2 direction = (normalized * 0.72f
                    + new Vector2(randomX, randomY) * 0.38f).normalized;
                float edgeWave = Mathf.Clamp01(normalized.magnitude / 1.25f);
                fragments.Add(new Fragment
                {
                    Rect = rect,
                    Graphic = image,
                    Direction = direction,
                    Delay = Mathf.Lerp(0.20f, 0f, edgeWave) + Hash01(column, row, 47) * 0.08f,
                    Rotation = Mathf.Lerp(-18f, 18f, Hash01(column, row, 73))
                });
            }
            return fragments;
        }

        private static float Hash01(int x, int y, int salt)
        {
            uint value = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791));
            value ^= value >> 13;
            value *= 1274126177u;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static GameObject FindSceneLoadingCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
                if (canvases[index].name == "UICanvasLoading") return canvases[index].gameObject;
            return null;
        }
    }
}
