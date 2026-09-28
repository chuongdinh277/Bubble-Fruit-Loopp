using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Managers;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxManager
    {
        internal void TryPlayWinCelebration()
        {
            if (winCelebrated || columns == null || boxesExiting > 0)
                return;
            for (int index = 0; index < columns.Length; index++)
                if (columns[index].Active != null || columns[index].Prepared != null || columns[index].Queue.Count > 0)
                    return;
            winCelebrated = true;
            gameEnded = true;
            StartCoroutine(PlayWinCelebration());
        }

        private IEnumerator PlayWinCelebration()
        {
            Camera camera = Camera.main;
            if (camera == null)
                yield break;
            CreateConfettiCannon(camera, true);
            CreateConfettiCannon(camera, false);
            for (int wave = 0; wave < 4; wave++)
            {
                float height = 0.38f + wave * 0.12f;
                StartCoroutine(LaunchCelebrationRocket(camera, new Vector2(0.04f, 0.06f), new Vector2(0.22f + wave * 0.035f, height), wave));
                StartCoroutine(LaunchCelebrationRocket(camera, new Vector2(0.96f, 0.06f), new Vector2(0.78f - wave * 0.035f, height), wave + 7));
                yield return new WaitForSecondsRealtime(0.24f);
            }

            // Let the imported fireworks finish, then wait for the player to press
            // Next. Level switching is owned by UICanvasWin.
            yield return new WaitForSecondsRealtime(2.7f);
            UICanvasWin.Show();
        }

        private IEnumerator LaunchCelebrationRocket(Camera camera, Vector2 startViewport, Vector2 targetViewport, int seed)
        {
            float depth = Mathf.Abs(camera.transform.position.z);
            Vector3 start = camera.ViewportToWorldPoint(new Vector3(startViewport.x, startViewport.y, depth));
            Vector3 target = camera.ViewportToWorldPoint(new Vector3(targetViewport.x, targetViewport.y, depth));
            start.z = target.z = -0.5f;
            GameObject rocket = new($"Win Rocket {seed}");
            rocket.transform.position = start;
            TrailRenderer trail = rocket.AddComponent<TrailRenderer>();
            ConfigureRocketTrail(seed, rocket, trail);
            float elapsed = 0f;
            const float duration = 0.42f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                Vector3 control = (start + target) * 0.5f + Vector3.up * (0.55f + seed % 3 * 0.12f);
                float inverse = 1f - eased;
                rocket.transform.position = inverse * inverse * start + 2f * inverse * eased * control + eased * eased * target;
                yield return null;
            }

            trail.emitting = false;
            CreateCelebrationBurst(target, seed);
            Destroy(rocket, trail.time + 0.08f);
        }

        private void ConfigureRocketTrail(int seed, GameObject rocket, TrailRenderer trail)
        {
            if (celebrationTrailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    celebrationTrailMaterial = new Material(shader)
                    {
                        name = "Runtime Win Rocket Trail",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            trail.sharedMaterial = celebrationTrailMaterial;
            trail.time = 0.32f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.13f), new Keyframe(1f, 0f));
            Color rocketColor = CelebrationColor(seed);
            trail.startColor = rocketColor;
            trail.endColor = new Color(rocketColor.r, rocketColor.g, rocketColor.b, 0f);
            trail.sortingOrder = 300;
            CreateRocketSparks(rocket, rocketColor);
        }
    }
}
