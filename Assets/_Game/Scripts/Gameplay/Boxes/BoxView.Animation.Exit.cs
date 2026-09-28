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
        public void PlayCompleteAndExit(Action completed)
        {
            PlayCompleteAndExit(null, null, completed);
        }

        public void PlayCompleteAndExit(Action promoteNext, Action completed)
        {
            PlayCompleteAndExit(null, promoteNext, completed);
        }

        public void PlayCompleteAndExit(Action prepareNext, Action promoteNext, Action completed)
        {
            PrepareCompletedBox();
            float lidProgress = 0f;
            Vector3 baseSize = transform.localScale;
            float exitDirection = Runtime != null && Runtime.ColumnIndex == 2 ? -1f : 1f;
            Vector3 lifted = transform.position + Vector3.up * 0.5f;
            Vector3 anticipation = lifted + Vector3.right * (-exitDirection * 0.20f);
            Vector3 exitTarget = GetOffscreenExitTarget(anticipation, exitDirection);
            GameObject exitTrails = null;
            GameObject fruitLabel = null;
            Tween exitTween = null;
            motionSequence = DOTween.Sequence().SetLink(gameObject);
            motionSequence.AppendInterval(0.10f);
            motionSequence.AppendCallback(() => prepareNext?.Invoke());
            AnimateCompletedBoxLift(lifted, anticipation);
            AppendCompletionLidTween(lidProgress);
            motionSequence.AppendCallback(() =>
            {
                PlayImpactBurst(transform.position + Vector3.up * 0.05f, configuredColor, 11, 0.36f, 0.30f);
                if (fullVfx != null)
                    fullVfx.Play();
            });
            motionSequence.Append(transform.DOScale(new Vector3(baseSize.x * 1.06f, baseSize.y * 0.94f, baseSize.z), 0.07f));
            motionSequence.Append(transform.DOScale(baseSize, 0.08f).SetEase(Ease.OutBack));
            AppendFruitLabelReveal(fruitLabel);
            motionSequence.AppendInterval(0.06f);
            motionSequence.Append(transform.DOPunchRotation(new Vector3(0f, 0f, -exitDirection * 7f), 0.30f, 5, 0.42f).SetEase(Ease.InOutSine));
            motionSequence.AppendInterval(0.05f);
            motionSequence.AppendCallback(() =>
            {
                exitTrails = CreateExitTrails(exitDirection);
            });
            // Begin the next box's promotion at the exact start of this box's
            // outward flight, so both boxes move at the same time.
            motionSequence.AppendCallback(() => promoteNext?.Invoke());
            exitTween = transform.DOMove(exitTarget, Mathf.Max(0.46f, exitDuration)).SetEase(Ease.InCubic).OnUpdate(() =>
            {
                float exitProgress = exitTween != null ? exitTween.ElapsedPercentage() : 0f;
                UpdateExitTrailWaves(exitTrails, exitProgress, exitDirection);
            });
            motionSequence.Append(exitTween);
            if (liftGroundShadow != null)
                motionSequence.Join(liftGroundShadow.DOFade(0f, Mathf.Max(0.46f, exitDuration)).SetEase(Ease.InQuad));
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
            CreateGroundShadow(groundPosition);
            // Upper-right key light: the projection keeps the carton's silhouette
            // and is displaced mostly to the left, with only a tiny downward bias.
            // Let the projection extend slightly past the 1.12-unit carton body
            // and keep it twice as wide so the cast shadow remains visible.
            ConfigureGroundShadow();
        }

        private void UpdateLiftGroundShadowTransform()
        {
            if (liftGroundShadow == null || liftGroundShadow.sprite == null)
                return;
            Vector3 boxScale = transform.lossyScale;
            Vector3 shadowPosition = transform.position + new Vector3(LiftShadowOffset.x * Mathf.Abs(boxScale.x), LiftShadowOffset.y * Mathf.Abs(boxScale.y), LiftShadowOffset.z);
            if (liftShadowGroundLocked)
                shadowPosition.y = liftShadowGroundY;
            liftGroundShadow.transform.position = shadowPosition;
            liftGroundShadow.transform.rotation = liftShadowGroundLocked ? Quaternion.identity : transform.rotation;
            float width = Mathf.Max(0.52f, Mathf.Abs(boxScale.x) * 0.96f);
            float height = Mathf.Max(1.20f, Mathf.Abs(boxScale.y) * 1.20f);
            Vector2 shadowBounds = liftGroundShadow.sprite.bounds.size;
            liftGroundShadow.transform.localScale = new Vector3(width / Mathf.Max(0.001f, shadowBounds.x), height / Mathf.Max(0.001f, shadowBounds.y), 1f);
        }

        private void ReleaseLiftGroundShadow()
        {
            if (liftGroundShadow == null)
                return;
            Destroy(liftGroundShadow.gameObject);
            liftGroundShadow = null;
        }

        private void PrepareCompletedBox()
        {
            StopMotion();
            // This box is completing and exiting the board, so its shadow stays
            // on the ground. Boxes that are merely promoted keep their shadow
            // attached to the carton through UpdateLiftGroundShadowTransform.
            if (liftGroundShadow != null)
            {
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
        }

        private void AnimateCompletedBoxLift(Vector3 lifted, Vector3 anticipation)
        {
            motionSequence.Append(transform.DOMove(lifted, 0.20f).SetEase(Ease.OutCubic));
            if (liftGroundShadow != null)
            {
                Vector3 shadowScale = liftGroundShadow.transform.localScale;
                motionSequence.Join(liftGroundShadow.transform.DOScale(new Vector3(shadowScale.x * 1.12f, shadowScale.y * 1.08f, 1f), 0.20f).SetEase(Ease.OutCubic));
                motionSequence.Join(liftGroundShadow.DOFade(liftShadowRestingAlpha * 0.75f, 0.20f));
            }

            motionSequence.Append(transform.DOMove(anticipation, 0.14f).SetEase(Ease.InOutSine));
        }

        private void CreateGroundShadow(Vector3 groundPosition)
        {
            if (liftGroundShadow == null)
            {
                GameObject shadowObject = new($"{name} Ground Shadow");
                liftGroundShadow = shadowObject.AddComponent<SpriteRenderer>();
                liftGroundShadow.sprite = GetParallelogramShadowSprite();
                bool usesAuthoredShadow = liftGroundShadow.sprite != null && liftGroundShadow.sprite.name == "Box Ground Shadow (PNG)";
                liftShadowRestingAlpha = usesAuthoredShadow ? 0.82f : 0.56f;
                liftGroundShadow.color = usesAuthoredShadow ? new Color(1f, 1f, 1f, liftShadowRestingAlpha) : new Color(0.07f, 0.045f, 0.04f, liftShadowRestingAlpha);
                liftGroundShadow.sortingLayerID = openArtwork != null ? openArtwork.sortingLayerID : 0;
                // The projection is printed on the board, immediately beneath
                // the carton artwork. Its exposed left edge remains visible while
                // the overlapping portion correctly stays behind the box.
                liftGroundShadow.sortingOrder = openArtwork != null ? openArtwork.sortingOrder - 1 : CavityOrder - 1;
            }

            liftGroundShadow.gameObject.SetActive(true);
            liftShadowGroundLocked = false;
            liftGroundShadow.transform.position = groundPosition + LiftShadowOffset;
            // The sprite itself is a rounded parallelogram. Keep the transform
            // unrotated so both side edges share the same authored down-slope.
            liftGroundShadow.transform.rotation = Quaternion.identity;
        }

        private void ConfigureGroundShadow()
        {
            float width = Mathf.Max(0.52f, Mathf.Abs(transform.lossyScale.x) * 0.96f);
            float height = Mathf.Max(1.20f, Mathf.Abs(transform.lossyScale.y) * 1.20f);
            Vector2 shadowBounds = liftGroundShadow.sprite != null ? liftGroundShadow.sprite.bounds.size : Vector2.one;
            liftGroundShadow.transform.localScale = new Vector3(width / Mathf.Max(0.001f, shadowBounds.x), height / Mathf.Max(0.001f, shadowBounds.y), 1f);
            Color color = liftGroundShadow.color;
            color.a = liftShadowRestingAlpha;
            liftGroundShadow.color = color;
        }

        private void AppendCompletionLidTween(float lidProgress)
        {
            motionSequence.Append(DOTween.To(() => lidProgress, value =>
            {
                lidProgress = value;
                SetLidProgress(value);
            }, 1f, Mathf.Max(0.26f, closeLidDuration)).SetEase(Ease.InOutCubic));
        }

        private void AppendFruitLabelReveal(GameObject fruitLabel)
        {
            motionSequence.AppendCallback(() =>
            {
                fruitLabel = CreateFruitLabel();
            });
            motionSequence.Append(DOTween.To(() => 0f, value =>
            {
                if (fruitLabel == null)
                    return;
                ApplyFruitLabelRevealPose(fruitLabel, value);
            }, 1f, 0.24f).SetEase(Ease.OutCubic));
            motionSequence.AppendCallback(() =>
            {
                if (fruitLabel == null)
                    return;
                Vector3 labelPosition = fruitLabel.transform.position;
                PlayImpactBurst(labelPosition, configuredColor, 6, 0.16f, 0.22f);
                PlayLabelFirework(labelPosition);
            });
        }

        private void ApplyFruitLabelRevealPose(GameObject fruitLabel, float value)
        {
            float eased = Mathf.SmoothStep(0f, 1f, value);
            fruitLabel.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 0.90f, eased) * (1f + Mathf.Sin(value * Mathf.PI) * 0.12f);
            fruitLabel.transform.localPosition = new Vector3(0f, Mathf.Lerp(0.24f, 0.035f, eased), -0.22f);
            fruitLabel.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-13f, 0f, eased));
        }
    }
}
