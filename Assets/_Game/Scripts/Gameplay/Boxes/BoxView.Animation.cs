using System;
using System.Collections;
using System.Collections.Generic;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Pooling;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxView
    {
        public void PlayPromote(Vector3 targetPosition)
        {
            StopMotion();
            EnsureLiftGroundShadow(targetPosition);
            SetLidProgress(1f);
            float lidProgress = 1f;
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.Join(transform.DOMove(targetPosition, slideDuration).SetEase(Ease.OutCubic));
            motionSequence.Join(DOTween.To(() => lidProgress, value =>
            {
                lidProgress = value;
                SetLidProgress(value);
            }, 0f, lidDuration).SetEase(Ease.OutCubic));
            motionSequence.OnComplete(() =>
            {
                transform.position = targetPosition;
                SetLidProgress(0f);
                motionSequence = null;
            });
        }

        public void PlayCompleteAndExit(Action completed)
        {
            StopMotion();
            if (liftGroundShadow != null)
            {
                // The carton is about to leave the table. Keep the projection
                // printed on the ground while it follows horizontal motion.
                liftShadowGroundLocked = true;
                liftShadowGroundY = liftGroundShadow.transform.position.y;
            }
            // Keep the actual flying/docked actors visible while an active box
            // opens or settles. They are hidden only during this box's final
            // completed exit animation.
            hidePackedFruitsOnClose = true;
            if (liftGroundShadow != null)
            {
                // While the carton rises, its projection remains printed on the
                // background plane. It starts following only during the exit move.
            }
            float lidProgress = 0f;
            Vector3 baseSize = transform.localScale;
            float exitDirection = Runtime != null && Runtime.ColumnIndex == 2 ? -1f : 1f;
            Vector3 lifted = transform.position + Vector3.up * 0.46f;
            Vector3 anticipation = lifted + Vector3.right * (-exitDirection * 0.20f);
            Vector3 exitTarget = GetOffscreenExitTarget(anticipation, exitDirection);
            GameObject exitTrails = null;
            GameObject fruitLabel = null;
            Tween exitTween = null;
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.AppendInterval(0.10f);
            motionSequence.Append(transform.DOMove(lifted, 0.20f).SetEase(Ease.OutCubic));
            if (liftGroundShadow != null)
            {
                Vector3 shadowScale = liftGroundShadow.transform.localScale;
                motionSequence.Join(liftGroundShadow.transform.DOScale(
                    new Vector3(shadowScale.x * 1.12f, shadowScale.y * 1.08f, 1f), 0.20f)
                    .SetEase(Ease.OutCubic));
                motionSequence.Join(liftGroundShadow.DOFade(liftShadowRestingAlpha * 0.75f, 0.20f));
            }
            motionSequence.Append(transform.DOMove(anticipation, 0.14f).SetEase(Ease.InOutSine));
            motionSequence.Append(DOTween.To(() => lidProgress, value =>
            {
                lidProgress = value;
                SetLidProgress(value);
            }, 1f, Mathf.Max(0.26f, closeLidDuration)).SetEase(Ease.InOutCubic));
            motionSequence.AppendCallback(() =>
            {
                PlayImpactBurst(transform.position + Vector3.up * 0.05f,
                    configuredColor, 11, 0.36f, 0.30f);
                if (fullVfx != null) fullVfx.Play();
            });
            motionSequence.Append(transform.DOScale(
                new Vector3(baseSize.x * 1.06f, baseSize.y * 0.94f, baseSize.z), 0.07f));
            motionSequence.Append(transform.DOScale(baseSize, 0.08f).SetEase(Ease.OutBack));
            motionSequence.AppendCallback(() => fruitLabel = CreateFruitLabel());
            motionSequence.Append(DOTween.To(() => 0f, value =>
            {
                if (fruitLabel == null) return;
                float eased = Mathf.SmoothStep(0f, 1f, value);
                fruitLabel.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 0.90f, eased)
                    * (1f + Mathf.Sin(value * Mathf.PI) * 0.12f);
                fruitLabel.transform.localPosition = new Vector3(0f,
                    Mathf.Lerp(0.24f, 0.035f, eased), -0.22f);
                fruitLabel.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Lerp(-13f, 0f, eased));
            }, 1f, 0.24f).SetEase(Ease.OutCubic));
            motionSequence.AppendCallback(() =>
            {
                if (fruitLabel != null) PlayLabelFirework(fruitLabel.transform.position);
            });
            motionSequence.AppendInterval(0.06f);
            motionSequence.Append(transform.DOPunchRotation(
                new Vector3(0f, 0f, -exitDirection * 7f), 0.30f, 5, 0.42f)
                .SetEase(Ease.InOutSine));
            motionSequence.AppendInterval(0.05f);
            motionSequence.AppendCallback(() =>
            {
                exitTrails = CreateExitTrails(exitDirection);
            });
            exitTween = transform.DOMove(exitTarget, Mathf.Max(0.46f, exitDuration))
                .SetEase(Ease.InCubic)
                .OnUpdate(() => UpdateExitTrailWaves(exitTrails,
                    exitTween != null ? exitTween.ElapsedPercentage() : 0f, exitDirection));
            motionSequence.Append(exitTween);
            if (liftGroundShadow != null)
                motionSequence.Join(liftGroundShadow.DOFade(0f, Mathf.Max(0.46f, exitDuration))
                    .SetEase(Ease.InQuad));
            motionSequence.OnComplete(() =>
            {
                ReleaseExitTrails(exitTrails);
                ReleaseLiftGroundShadow();
                motionSequence = null;
                completed?.Invoke();
            });
        }

        private void EnsureLiftGroundShadow(Vector3 groundPosition)
        {
            if (liftGroundShadow == null)
            {
                GameObject shadowObject = new($"{name} Ground Shadow");
                liftGroundShadow = shadowObject.AddComponent<SpriteRenderer>();
                liftGroundShadow.sprite = GetParallelogramShadowSprite();
                bool usesAuthoredShadow = liftGroundShadow.sprite != null &&
                    liftGroundShadow.sprite.name == "Box Ground Shadow (PNG)";
                liftShadowRestingAlpha = usesAuthoredShadow ? 0.82f : 0.56f;
                liftGroundShadow.color = usesAuthoredShadow
                    ? new Color(1f, 1f, 1f, liftShadowRestingAlpha)
                    : new Color(0.07f, 0.045f, 0.04f, liftShadowRestingAlpha);
                liftGroundShadow.sortingLayerID = openArtwork != null ? openArtwork.sortingLayerID : 0;
                // The projection is printed on the board, immediately beneath
                // the carton artwork. Its exposed left edge remains visible while
                // the overlapping portion correctly stays behind the box.
                liftGroundShadow.sortingOrder = openArtwork != null
                    ? openArtwork.sortingOrder - 1
                    : CavityOrder - 1;
            }

            liftGroundShadow.gameObject.SetActive(true);
            liftShadowGroundLocked = false;
            liftGroundShadow.transform.position = groundPosition + LiftShadowOffset;
            // The sprite itself is a rounded parallelogram. Keep the transform
            // unrotated so both side edges share the same authored down-slope.
            liftGroundShadow.transform.rotation = Quaternion.identity;
            // Upper-right key light: the projection keeps the carton's silhouette
            // and is displaced mostly to the left, with only a tiny downward bias.
            // Let the projection extend slightly past the 1.12-unit carton body
            // and keep it twice as wide so the cast shadow remains visible.
            float width = Mathf.Max(0.52f, Mathf.Abs(transform.lossyScale.x) * 0.96f);
            float height = Mathf.Max(1.20f, Mathf.Abs(transform.lossyScale.y) * 1.20f);
            Vector2 shadowBounds = liftGroundShadow.sprite != null
                ? liftGroundShadow.sprite.bounds.size
                : Vector2.one;
            liftGroundShadow.transform.localScale = new Vector3(
                width / Mathf.Max(0.001f, shadowBounds.x),
                height / Mathf.Max(0.001f, shadowBounds.y),
                1f);
            Color color = liftGroundShadow.color;
            color.a = liftShadowRestingAlpha;
            liftGroundShadow.color = color;
        }

        private void UpdateLiftGroundShadowTransform()
        {
            if (liftGroundShadow == null || liftGroundShadow.sprite == null) return;

            Vector3 boxScale = transform.lossyScale;
            Vector3 shadowPosition = transform.position + new Vector3(
                LiftShadowOffset.x * Mathf.Abs(boxScale.x),
                LiftShadowOffset.y * Mathf.Abs(boxScale.y),
                LiftShadowOffset.z);
            if (liftShadowGroundLocked) shadowPosition.y = liftShadowGroundY;
            liftGroundShadow.transform.position = shadowPosition;
            liftGroundShadow.transform.rotation = liftShadowGroundLocked
                ? Quaternion.identity
                : transform.rotation;

            float width = Mathf.Max(0.52f, Mathf.Abs(boxScale.x) * 0.96f);
            float height = Mathf.Max(1.20f, Mathf.Abs(boxScale.y) * 1.20f);
            Vector2 shadowBounds = liftGroundShadow.sprite.bounds.size;
            liftGroundShadow.transform.localScale = new Vector3(
                width / Mathf.Max(0.001f, shadowBounds.x),
                height / Mathf.Max(0.001f, shadowBounds.y),
                1f);
        }

        private void ReleaseLiftGroundShadow()
        {
            if (liftGroundShadow == null) return;
            Destroy(liftGroundShadow.gameObject);
            liftGroundShadow = null;
        }

        private GameObject CreateFruitLabel()
        {
            // Derive the carton label from this box's own first packed visual.
            // No sprite, tint, or material is shared through static/global state.
            SpriteRenderer labelSource = null;
            for (int index = 0; index < dockedVisuals.Count; index++)
            {
                if (dockedVisuals[index] == null || dockedVisuals[index].slotIndex != 0) continue;
                labelSource = dockedVisuals[index].fruit != null
                    ? dockedVisuals[index].fruit.VisualSpriteRenderer
                    : null;
                break;
            }
            if (labelSource == null && dockedVisuals.Count > 0 && dockedVisuals[0]?.fruit != null)
                labelSource = dockedVisuals[0].fruit.VisualSpriteRenderer;
            if (labelSource == null || labelSource.sprite == null) return null;
            Sprite labelSprite = labelSource.sprite;
            Color labelColor = labelSource.color;
            GameObject root = new GameObject($"{name} Fruit Label");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.24f, -0.22f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, -13f);
            root.transform.localScale = Vector3.zero;

            int sortingLayer = openArtwork != null ? openArtwork.sortingLayerID : 0;
            if (spriteSilhouetteMaterial == null)
            {
                Shader silhouetteShader = Shader.Find("BubbleFruit/SpriteSilhouette");
                if (silhouetteShader != null)
                {
                    spriteSilhouetteMaterial = new Material(silhouetteShader)
                    {
                        name = "Runtime Fruit Label Outline",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            GameObject shadowObject = new GameObject("Fruit Label Shadow");
            shadowObject.transform.SetParent(root.transform, false);
            shadowObject.transform.localPosition = new Vector3(0.035f, -0.045f, 0.02f);
            SpriteRenderer shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = labelSprite;
            shadow.color = new Color(0.12f, 0.05f, 0.04f, 0.28f);
            shadow.sharedMaterial = spriteSilhouetteMaterial;
            shadow.sortingLayerID = sortingLayer;
            shadow.sortingOrder = DoorOrder + 8;

            GameObject outlineObject = new GameObject("White Fruit Outline");
            outlineObject.transform.SetParent(root.transform, false);
            outlineObject.transform.localScale = Vector3.one * 1.18f;
            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sprite = labelSprite;
            outline.color = Color.white;
            outline.sharedMaterial = spriteSilhouetteMaterial;
            outline.sortingLayerID = sortingLayer;
            outline.sortingOrder = DoorOrder + 9;

            GameObject iconObject = new GameObject("Original Fruit Artwork");
            iconObject.transform.SetParent(root.transform, false);
            iconObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            SpriteRenderer icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = labelSprite;
            icon.color = labelColor;
            icon.sortingLayerID = sortingLayer;
            icon.sortingOrder = DoorOrder + 10;
            Vector2 spriteSize = labelSprite.bounds.size;
            float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
            if (largestSide > 0.0001f)
            {
                // Make the fruit sticker a strong, readable mark on the closed
                // carton instead of a small icon lost between the two doors.
                float fittedScale = 1.52f / largestSide;
                iconObject.transform.localScale = Vector3.one * fittedScale;
                outlineObject.transform.localScale = Vector3.one * (fittedScale * 1.18f);
                shadowObject.transform.localScale = Vector3.one * (fittedScale * 1.18f);
            }
            return root;
        }

        private void PlayLabelFirework(Vector3 worldPosition)
        {
            if (labelFireworkPrefab == null) return;
            GameObject effect = Instantiate(labelFireworkPrefab, worldPosition, Quaternion.identity);
            effect.name = "Cute Label Firework";
            effect.transform.localScale = Vector3.one * 0.20f;
            ParticleSystemRenderer[] renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
            int sortingLayer = openArtwork != null ? openArtwork.sortingLayerID : 0;
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].sortingLayerID = sortingLayer;
                renderers[index].sortingOrder = 135 + index;
            }
            Destroy(effect, 2.4f);
        }

        private Vector3 GetOffscreenExitTarget(Vector3 start, float direction)
        {
            Camera camera = Camera.main;
            if (camera == null) return start + Vector3.right * direction * 5f;
            float depth = Mathf.Abs(camera.transform.position.z - transform.position.z);
            Vector3 edge = camera.ViewportToWorldPoint(new Vector3(direction > 0f ? 1.12f : -0.12f,
                0.5f, depth));
            return new Vector3(edge.x, start.y + 0.20f, start.z);
        }

        private void PlayImpactBurst(Vector3 worldPosition, Color color, int count,
            float radius, float duration)
        {
            GameObject root = new GameObject($"{name} Color Burst");
            root.transform.position = worldPosition;
            Sequence burst = DOTween.Sequence().SetLink(root);
            Color bright = Color.Lerp(MakeBrightColor(color), Color.white, 0.18f);

            for (int index = 0; index < count; index++)
            {
                float angle = (index + 0.35f) / count * Mathf.PI * 2f;
                Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                GameObject dot = new GameObject("Burst Dot");
                dot.transform.SetParent(root.transform, false);
                dot.transform.localScale = Vector3.one * Mathf.Lerp(0.055f, 0.09f,
                    (index % 3) / 2f);
                SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSoftRoundedSprite();
                renderer.color = bright;
                renderer.sortingOrder = 120;
                burst.Join(dot.transform.DOLocalMove(direction * radius *
                    Mathf.Lerp(0.72f, 1.12f, (index % 4) / 3f), duration).SetEase(Ease.OutQuad));
                burst.Join(dot.transform.DOScale(0f, duration).SetEase(Ease.InQuad));
                burst.Join(renderer.DOFade(0f, duration).SetEase(Ease.InQuad));
            }

            burst.OnComplete(() => Destroy(root));
        }

        private GameObject CreateExitTrails(float direction)
        {
            GameObject root = new GameObject($"{name} Wavy Exit Trails");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            if (runtimeTrailMaterial == null)
            {
                runtimeTrailMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Runtime Box Exit Trail",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            for (int index = 0; index < 3; index++)
            {
                GameObject line = new GameObject($"Color Trail {index + 1}");
                line.transform.SetParent(root.transform, false);
                line.transform.localPosition = new Vector3(-direction * 0.48f, (index - 1) * 0.22f, 0f);
                TrailRenderer trail = line.AddComponent<TrailRenderer>();
                trail.sharedMaterial = runtimeTrailMaterial;
                trail.time = 0.42f;
                trail.minVertexDistance = 0.015f;
                trail.widthMultiplier = 1f;
                trail.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(0.18f, 0.055f), new Keyframe(1f, 0.025f));
                Color lineColor = index == 1
                    ? Color.Lerp(configuredColor, Color.white, 0.35f)
                    : Color.Lerp(configuredColor, Color.black, index == 0 ? 0.08f : 0.18f);
                trail.startColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.95f);
                trail.endColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0f);
                trail.sortingOrder = 110 + index;
                trail.emitting = true;
            }
            return root;
        }

        private static void UpdateExitTrailWaves(GameObject root, float progress, float direction)
        {
            if (root == null) return;
            for (int index = 0; index < root.transform.childCount; index++)
            {
                Transform line = root.transform.GetChild(index);
                float phase = progress * Mathf.PI * (5.2f + index * 0.7f) + index * 2.1f;
                line.localPosition = new Vector3(-direction * 0.48f + Mathf.Cos(phase * 0.55f) * 0.035f,
                    (index - 1) * 0.22f + Mathf.Sin(phase) * (0.075f + index * 0.012f), 0f);
            }
        }

        private void ReleaseExitTrails(GameObject root)
        {
            if (root == null) return;
            root.transform.SetParent(null, true);
            TrailRenderer[] trails = root.GetComponentsInChildren<TrailRenderer>();
            float lifetime = 0f;
            for (int index = 0; index < trails.Length; index++)
            {
                trails[index].emitting = false;
                lifetime = Mathf.Max(lifetime, trails[index].time);
            }
            Destroy(root, lifetime + 0.08f);
        }
        public void PlayShiftTo(Vector3 targetPosition)
        {
            StopMotion();
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.Append(transform.DOMove(targetPosition, slideDuration).SetEase(Ease.InOutCubic));
            motionSequence.OnComplete(() => motionSequence = null);
        }

        private void PlayFillAnimation(int slotIndex)
        {
            // The completed box owns a larger, timed punch after its doors close.
            // Avoid two scale animations fighting over the fourth fruit.
            if (Runtime != null && Runtime.CurrentCount >= Runtime.Capacity) return;
            StartCoroutine(PunchBox());
        }

        // Completion VFX is intentionally fired by PlayCompleteAndExit after the
        // doors meet, so the burst reads as the lid locking rather than fruit impact.
        private void PlayCloseAnimation() { }

        private IEnumerator PromoteRoutine(Vector3 target)
        {
            Vector3 start = transform.position;
            SetLidProgress(1f);
            float elapsed = 0f;
            float duration = Mathf.Max(slideDuration, lidDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float positionT = Smooth(Mathf.Clamp01(elapsed / slideDuration));
                float lidT = Smooth(Mathf.Clamp01(elapsed / lidDuration));
                transform.position = Vector3.LerpUnclamped(start, target, positionT);
                SetLidProgress(1f - lidT);
                yield return null;
            }
            transform.position = target;
            SetLidProgress(0f);
            motionRoutine = null;
        }

        private IEnumerator ShiftRoutine(Vector3 target)
        {
            Vector3 start = transform.position;
            yield return TweenPosition(start, target, slideDuration);
            motionRoutine = null;
        }

        private IEnumerator CompleteRoutine(Action completed)
        {
            // Hold briefly so the fourth fruit is readable, then visibly fold
            // both side doors inward before the completed box moves away.
            yield return new WaitForSeconds(0.10f);
            yield return TweenLids(0f, 1f, closeLidDuration);
            yield return new WaitForSeconds(0.08f);
            Vector3 start = transform.position;
            Vector3 lifted = start + Vector3.up * 0.3f;
            yield return TweenPosition(start, lifted, 0.16f);
            yield return PunchBox();
            yield return TweenPosition(lifted, lifted + new Vector3(2.8f, 0.15f, 0f), exitDuration);
            completed?.Invoke();
            motionRoutine = null;
        }
        private IEnumerator TweenPosition(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(from, to, Smooth(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            transform.position = to;
        }

        private IEnumerator TweenLids(float from, float to, float duration)
        {
            float elapsed = 0f;
            float totalDuration = duration + rightDoorDelay;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float leftT = Mathf.Clamp01(elapsed / duration);
                float rightT = Mathf.Clamp01((elapsed - rightDoorDelay) / duration);
                float leftEase = to < from ? EaseOutCubic(leftT) : Smooth(leftT);
                float rightEase = to < from ? EaseOutCubic(rightT) : Smooth(rightT);
                SetLidProgress(Mathf.Lerp(from, to, leftEase), Mathf.Lerp(from, to, rightEase));
                yield return null;
            }
            SetLidProgress(to);
        }

        private IEnumerator PunchBox()
        {
            baseScale = transform.localScale;
            Vector3 punch = new(baseScale.x * 1.06f, baseScale.y * 0.94f, baseScale.z);
            float elapsed = 0f;
            while (elapsed < 0.14f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.14f);
                transform.localScale = t < 0.45f ? Vector3.Lerp(baseScale, punch, t / 0.45f) : Vector3.Lerp(punch, baseScale, (t - 0.45f) / 0.55f);
                yield return null;
            }
            transform.localScale = baseScale;
        }

        private static IEnumerator PunchSlot(Transform slot)
        {
            if (slot == null) yield break;
            Vector3 start = slot.localScale;
            float elapsed = 0f;
            while (elapsed < 0.16f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.16f);
                slot.localScale = start * (1f + Mathf.Sin(t * Mathf.PI) * 0.16f);
                yield return null;
            }
            slot.localScale = start;
        }

        private void SetLidProgress(float closed)
        {
            SetLidProgress(closed, closed);
        }

        private void SetLidProgress(float leftClosed, float rightClosed)
        {
            float closed = (Mathf.Clamp01(leftClosed) + Mathf.Clamp01(rightClosed)) * 0.5f;
            // Once the doors have met, no part of the packed fruit should remain
            // visible above the closed carton (tall stems can exceed the lid art).
            bool showPackedFruit = !hidePackedFruitsOnClose || closed < 0.98f;
            for (int index = 0; index < dockedVisuals.Count; index++)
            {
                GameObject packedFruit = dockedVisuals[index]?.fruit?.gameObject;
                if (packedFruit != null && packedFruit.activeSelf != showPackedFruit)
                    packedFruit.SetActive(showPackedFruit);
            }
            if (leftLid != null && rightLid != null)
            {
                if (leftLidOpenPosition == Vector3.zero && rightLidOpenPosition == Vector3.zero)
                {
                    leftLidOpenPosition = leftLid.localPosition;
                    rightLidOpenPosition = rightLid.localPosition;
                }

                // Real hinge motion. The pivots remain attached to the outer
                // walls; edge-on panels sit flush beside the open tray and both
                // half-width panels rotate flat to meet exactly at the centre.
                leftLid.localPosition = leftLidOpenPosition;
                rightLid.localPosition = rightLidOpenPosition;
                // Rotate around the two fixed outer-edge pivots. No positional
                // interpolation is used: the doors physically fold out/in.
                leftLid.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-doorOpenAngle, 0f, leftClosed), 0f);
                rightLid.localRotation = Quaternion.Euler(0f, Mathf.Lerp(doorOpenAngle, 0f, rightClosed), 0f);
                UpdateDoorShadow(leftDoorShadow, true, leftClosed);
                UpdateDoorShadow(rightDoorShadow, false, rightClosed);

                if (openArtwork != null)
                {
                    Color trayColor = openArtwork.color;
                    float openness = 1f - (leftClosed + rightClosed) * 0.5f;
                    trayColor.a = Mathf.SmoothStep(0f, 1f, openness);
                    openArtwork.color = trayColor;
                }
                if (closedArtwork != null)
                {
                    Color hidden = closedArtwork.color;
                    hidden.a = 0f;
                    closedArtwork.color = hidden;
                }
                if (frontLipArtwork != null)
                {
                    Color lip = frontLipArtwork.color;
                    lip.a = 1f - closed;
                    frontLipArtwork.color = lip;
                }
                UpdateClosedDepth(closed);
                return;
            }

            if (openArtwork != null && closedArtwork != null)
            {
                EnsureFrontLip();
                if (openArtworkScale == Vector3.zero)
                {
                    openArtworkScale = openArtwork.transform.localScale;
                    closedArtworkScale = closedArtwork.transform.localScale;
                    openArtworkPosition = openArtwork.transform.localPosition;
                    closedArtworkPosition = closedArtwork.transform.localPosition;
                }
                Color openColor = openArtwork.color;
                Color closedColor = closedArtwork.color;
                // A staged close reads as an actual lid movement instead of one
                // sprite suddenly replacing another.
                float lidReveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.78f, closed));
                float trayFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, closed));
                openColor.a = trayFade;
                closedColor.a = lidReveal;
                openArtwork.color = openColor;
                closedArtwork.color = closedColor;
                if (frontLipArtwork != null)
                {
                    Color lipColor = openColor;
                    lipColor.a = 1f - closed;
                    frontLipArtwork.color = lipColor;
                }
                openArtwork.transform.localScale = new Vector3(
                    openArtworkScale.x * Mathf.Lerp(1f, 0.98f, closed),
                    openArtworkScale.y * Mathf.Lerp(1f, 0.82f, closed),
                    openArtworkScale.z);
                closedArtwork.transform.localScale = new Vector3(
                    closedArtworkScale.x * Mathf.Lerp(1.05f, 1f, lidReveal),
                    closedArtworkScale.y * Mathf.Lerp(0.58f, 1f, lidReveal),
                    closedArtworkScale.z);
                openArtwork.transform.localPosition = openArtworkPosition + Vector3.down * (0.05f * closed);
                closedArtwork.transform.localPosition = closedArtworkPosition + Vector3.up * (0.24f * (1f - lidReveal));
                if (frontLipArtwork != null)
                    frontLipArtwork.transform.localScale = openArtwork.transform.localScale;
                UpdateClosedDepth(closed);
                return;
            }
            UpdateClosedDepth(closed);
        }

        public void SetEditorClosed(bool closed) => SetLidProgress(closed ? 1f : 0f);

        private static void UpdateDoorShadow(SpriteRenderer shadow, bool left, float closed)
        {
            if (shadow == null) return;
            float openness = 1f - Mathf.Clamp01(closed);
            Transform shadowTransform = shadow.transform;
            shadowTransform.localPosition = new Vector3(
                (left ? -1f : 1f) * Mathf.Lerp(0.255f, 0.51f, openness),
                Mathf.Lerp(-0.02f, -0.05f, openness), 0.02f);
            Vector2 spriteSize = shadow.sprite != null ? shadow.sprite.bounds.size : Vector2.one;
            float targetWidth = Mathf.Lerp(0.44f, 0.16f, openness);
            float targetHeight = Mathf.Lerp(1.00f, 1.06f, openness);
            shadowTransform.localScale = new Vector3(targetWidth / Mathf.Max(0.0001f, spriteSize.x),
                targetHeight / Mathf.Max(0.0001f, spriteSize.y), 1f);
            Color shadowColor = shadow.color;
            shadowColor.a = Mathf.Lerp(0.05f, 0.16f, openness);
            shadow.color = shadowColor;
        }
        private static float Smooth(float value) => value * value * (3f - 2f * value);
        private static float EaseOutCubic(float value) => 1f - Mathf.Pow(1f - value, 3f);
        private void StopMotion()
        {
            if (motionRoutine != null) StopCoroutine(motionRoutine);
            motionRoutine = null;
            if (motionSequence != null && motionSequence.IsActive()) motionSequence.Kill();
            motionSequence = null;
        }
    }
}
