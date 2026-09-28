using System.Collections;
using UnityEngine;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxManager
    {
        private void CreateCelebrationBurst(Vector3 position, int seed)
        {
            bool usedImportedFx = false;
            if (winBurstPrefab != null)
            {
                SpawnImportedWinFx(winBurstPrefab, position, Quaternion.identity, winFxScale, $"Win Confetti Burst {seed}", 5f);
                usedImportedFx = true;
            }

            if (winFlashPrefab != null)
            {
                SpawnImportedWinFx(winFlashPrefab, position + Vector3.back * 0.02f, Quaternion.identity, winFxScale * 0.8f, $"Win Flash {seed}", 3f);
                usedImportedFx = true;
            }

            if (usedImportedFx)
                return;
            GameObject effect = new($"Win Firework Burst {seed}");
            effect.transform.position = position;
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.15f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 8.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.23f);
            ConfigureBurstParticles(seed, particles, ref main);
            ConfigureBurstRotation(position, seed, effect, particles);
        }

        private static void CreateRocketSparks(GameObject rocket, Color color)
        {
            ParticleSystem sparks = rocket.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 34f;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.055f;
            ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 305;
            renderer.sharedMaterial = celebrationTrailMaterial;
            sparks.Play();
        }

        private static void CreateBurstFlash(Vector3 position, Color color)
        {
            GameObject flashObject = new("Win Firework Flash");
            flashObject.transform.position = position;
            ParticleSystem flash = flashObject.AddComponent<ParticleSystem>();
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = flash.main;
            main.loop = false;
            main.duration = 0.08f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startColor = Color.Lerp(color, Color.white, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = flash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });
            ParticleSystem.ShapeModule shape = flash.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            ParticleSystemRenderer renderer = flash.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 310;
            renderer.sharedMaterial = celebrationTrailMaterial;
            flash.Play();
            Destroy(flashObject, 0.8f);
        }

        private void CreateConfettiCannon(Camera camera, bool left)
        {
            float depth = Mathf.Abs(camera.transform.position.z);
            Vector3 position = camera.ViewportToWorldPoint(new Vector3(left ? 0.035f : 0.965f, 0.04f, depth));
            position.z = -0.45f;
            if (winDirectionalConfettiPrefab != null)
            {
                SpawnImportedConfetti(left, position);
                return;
            }

            GameObject cannon = new(left ? "Left Win Confetti Cannon" : "Right Win Confetti Cannon");
            cannon.transform.position = position;
            ParticleSystem particles = cannon.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ConfigureConfettiParticles(left, particles);
            ConfigureConfettiAppearance(cannon, particles);
        }

        private static GameObject SpawnImportedWinFx(GameObject prefab, Vector3 position, Quaternion rotation, float scaleMultiplier, string instanceName, float lifetime)
        {
            GameObject effect = Instantiate(prefab, position, rotation);
            effect.name = instanceName;
            effect.transform.localScale *= scaleMultiplier;
            ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystemRenderer renderer = particleSystems[index].GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 290);
                particleSystems[index].Play(true);
            }

            Destroy(effect, lifetime);
            return effect;
        }

        private static Color CelebrationColor(int index) => (Mathf.Abs(index) % 6) switch
        {
            0 => new Color(1f, 0.18f, 0.28f),
            1 => new Color(1f, 0.82f, 0.05f),
            2 => new Color(0.12f, 0.92f, 0.48f),
            3 => new Color(0.05f, 0.82f, 1f),
            4 => new Color(0.62f, 0.22f, 1f),
            _ => new Color(1f, 0.24f, 0.72f)};
        private void ConfigureBurstParticles(int seed, ParticleSystem particles, ref ParticleSystem.MainModule main)
        {
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)220) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;
            shape.radiusThickness = 1f;
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(new[] { new GradientColorKey(CelebrationColor(seed), 0f), new GradientColorKey(CelebrationColor(seed + 2), 0.35f), new GradientColorKey(CelebrationColor(seed + 4), 0.7f), new GradientColorKey(CelebrationColor(seed + 6), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private void ConfigureBurstRotation(Vector3 position, int seed, GameObject effect, ParticleSystem particles)
        {
            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 290;
            renderer.sharedMaterial = celebrationTrailMaterial;
            CreateBurstFlash(position, CelebrationColor(seed + 1));
            particles.Play();
            Destroy(effect, 3.2f);
        }

        private void ConfigureConfettiParticles(bool left, ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.3f, 4.0f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)210) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(left ? 2.2f : -5.8f, left ? 5.8f : -2.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(5.5f, 9.2f);
            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        }

        private void ConfigureConfettiAppearance(GameObject cannon, ParticleSystem particles)
        {
            ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
            colors.enabled = true;
            Gradient rainbow = new();
            rainbow.SetKeys(new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 0.2f), new GradientColorKey(Color.green, 0.4f), new GradientColorKey(Color.cyan, 0.6f), new GradientColorKey(new Color(0.55f, 0.2f, 1f), 0.8f), new GradientColorKey(Color.magenta, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.78f), new GradientAlphaKey(0f, 1f) });
            colors.color = new ParticleSystem.MinMaxGradient(rainbow);
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f;
            renderer.velocityScale = 0.22f;
            renderer.sortingOrder = 295;
            renderer.sharedMaterial = celebrationTrailMaterial;
            particles.Play();
            Destroy(cannon, 4.5f);
        }

        private void SpawnImportedConfetti(bool left, Vector3 position)
        {
            GameObject imported = SpawnImportedWinFx(winDirectionalConfettiPrefab, position, Quaternion.identity, winFxScale * 1.15f, left ? "Left Imported Confetti Cannon" : "Right Imported Confetti Cannon", 6f);
            if (!left)
            {
                Vector3 scale = imported.transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                imported.transform.localScale = scale;
            }
        }
    }
}
