using System.Collections;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BubbleActor
    {        private IEnumerator PlayPopEffect()
        {
            Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            yield return AnimatePopAnticipation(startScale);
            yield return AnimatePopBurst(startScale);
            SpawnPopBubbles();
            SetVisualsEnabled(false);
            yield return ReleaseContentsInSequence();
            ArmFruitRecoveryIfLastBubble();
        }

        private void ArmFruitRecoveryIfLastBubble()
        {
            BubbleActor[] remainingBubbles = FindObjectsByType<BubbleActor>(FindObjectsSortMode.None);
            for (int index = 0; index < remainingBubbles.Length; index++)
            {
                if (remainingBubbles[index] != null && !remainingBubbles[index].IsPopped) return;
            }

            FruitActor[] releasedFruit = FindObjectsByType<FruitActor>(FindObjectsSortMode.None);
            for (int index = 0; index < releasedFruit.Length; index++)
                if (releasedFruit[index] != null) releasedFruit[index].ArmClearRecovery();
        }

        // Compress the bubble shell before its burst.
        private IEnumerator AnimatePopAnticipation(Vector3 startScale)
        {
            yield return AnimateVisualScale(startScale, startScale * popAnticipationScale, popAnticipationDuration);
        }

        // Expand the shell to its short burst scale.
        private IEnumerator AnimatePopBurst(Vector3 startScale)
        {
            yield return AnimateVisualScale(
                visualRoot != null ? visualRoot.localScale : startScale,
                startScale * popBurstScale,
                popBurstDuration);
        }

        // Release each contained fruit with its outward burst impulse.
        private IEnumerator ReleaseContentsInSequence()
        {
            Vector2 inheritedVelocity = physicsBody != null ? physicsBody.linearVelocity : Vector2.zero;
            Vector2 burstCenter = transform.position;
            // Release on a short cadence so the fruit forms a visible stream
            // instead of becoming one simultaneous pile below the bubble.
            const float releaseInterval = 0.012f;
            for (int index = 0; index < fruits.Count; index++)
            {
                FruitActor fruit = fruits[index];
                if (fruit == null)
                {
                    if (index < fruits.Count - 1) yield return new WaitForSeconds(releaseInterval);
                    continue;
                }
                Vector2 radial = (Vector2)fruit.CachedTransform.position - burstCenter;
                if (radial.sqrMagnitude < 0.0025f)
                {
                    float fallbackAngle = (index + 0.5f) / Mathf.Max(1, fruits.Count) * Mathf.PI * 2f;
                    radial = new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle));
                }
                radial.Normalize();

                // A short outward puff separates the fruit silhouettes before
                // gravity takes over. A small lift keeps it readable as a burst,
                // while the capped force prevents fruit escaping the chute.
                float force = Random.Range(
                    Mathf.Min(fruitBurstForceMin, fruitBurstForceMax),
                    Mathf.Max(fruitBurstForceMin, fruitBurstForceMax));
                Vector2 sidewaysVariation = new(-radial.y, radial.x);
                Vector2 burstVelocity = radial * force
                    + Vector2.up * fruitBurstLift
                    + sidewaysVariation * Random.Range(-0.18f, 0.18f);
                fruit.Release(inheritedVelocity + burstVelocity);
                if (index < fruits.Count - 1) yield return new WaitForSeconds(releaseInterval);
            }
            fruits.Clear();
        }

        private IEnumerator AnimateVisualScale(Vector3 from, Vector3 to, float duration)
        {
            if (visualRoot == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                progress = progress * progress * (3f - 2f * progress);
                visualRoot.localScale = Vector3.LerpUnclamped(from, to, progress);
                yield return null;
            }
            visualRoot.localScale = to;
        }

        private void SpawnPopBubbles()
        {
            GameObject effect = new($"{name}_PopBubbles");
            effect.transform.position = transform.position;
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ConfigurePopParticleMain(particles);
            ConfigurePopParticleShape(particles);
            ConfigurePopParticleAppearance(particles);
            ConfigurePopParticleRenderer(particles);
            particles.Play();
            Destroy(effect, 1.5f);
        }

        // Set timing, motion, and particle capacity for the burst.
        private void ConfigurePopParticleMain(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(20, popBubbleCount);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)popBubbleCount)
            });

        }

        // Spread particles evenly around the bubble's pop point.
        private void ConfigurePopParticleShape(ParticleSystem particles)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.65f;
            shape.radiusThickness = 1f;

        }

        // Fade particle size and color over their lifetime.
        private void ConfigurePopParticleAppearance(ParticleSystem particles)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(0.72f, 0.8f),
                    new Keyframe(1f, 0f)));

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.92f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.65f, 0.65f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

        }

        // Match particle rendering to the bubble's artwork layer.
        private void ConfigurePopParticleRenderer(ParticleSystem particles)
        {
            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            if (visualRenderers != null && visualRenderers.Length > 0 && visualRenderers[^1] != null)
                particleRenderer.sharedMaterial = visualRenderers[^1].sharedMaterial;
            particleRenderer.sortingOrder = 25;
        }


    }
}
