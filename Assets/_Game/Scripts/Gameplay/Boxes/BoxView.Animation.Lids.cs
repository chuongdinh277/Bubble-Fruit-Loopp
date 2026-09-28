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
        private static void UpdateDoorShadow(SpriteRenderer shadow, bool left, float closed)
        {
            if (shadow == null)
                return;
            float openness = 1f - Mathf.Clamp01(closed);
            Transform shadowTransform = shadow.transform;
            shadowTransform.localPosition = new Vector3((left ? -1f : 1f) * Mathf.Lerp(0.255f, 0.51f, openness), Mathf.Lerp(-0.02f, -0.05f, openness), 0.02f);
            Vector2 spriteSize = shadow.sprite != null ? shadow.sprite.bounds.size : Vector2.one;
            float targetWidth = Mathf.Lerp(0.44f, 0.16f, openness);
            float targetHeight = Mathf.Lerp(1.00f, 1.06f, openness);
            shadowTransform.localScale = new Vector3(targetWidth / Mathf.Max(0.0001f, spriteSize.x), targetHeight / Mathf.Max(0.0001f, spriteSize.y), 1f);
            Color shadowColor = shadow.color;
            shadowColor.a = Mathf.Lerp(0.05f, 0.16f, openness);
            shadow.color = shadowColor;
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);
        private static float EaseOutCubic(float value) => 1f - Mathf.Pow(1f - value, 3f);
        private void StopMotion()
        {
            if (motionRoutine != null)
                StopCoroutine(motionRoutine);
            motionRoutine = null;
            if (motionSequence != null && motionSequence.IsActive())
                motionSequence.Kill();
            motionSequence = null;
        }

        private void ApplyHingedLidArtwork(float leftClosed, float rightClosed, float closed)
        {
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
        }

        private void ApplySpriteLidArtwork(float closed, ref Color openColor)
        {
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

            openArtwork.transform.localScale = new Vector3(openArtworkScale.x * Mathf.Lerp(1f, 0.98f, closed), openArtworkScale.y * Mathf.Lerp(1f, 0.82f, closed), openArtworkScale.z);
            closedArtwork.transform.localScale = new Vector3(closedArtworkScale.x * Mathf.Lerp(1.05f, 1f, lidReveal), closedArtworkScale.y * Mathf.Lerp(0.58f, 1f, lidReveal), closedArtworkScale.z);
            openArtwork.transform.localPosition = openArtworkPosition + Vector3.down * (0.05f * closed);
            closedArtwork.transform.localPosition = closedArtworkPosition + Vector3.up * (0.24f * (1f - lidReveal));
            if (frontLipArtwork != null)
                frontLipArtwork.transform.localScale = openArtwork.transform.localScale;
        }

        private void RememberLidOpenPositions()
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
        }

        private void RememberArtworkTransforms()
        {
            EnsureFrontLip();
            if (openArtworkScale == Vector3.zero)
            {
                openArtworkScale = openArtwork.transform.localScale;
                closedArtworkScale = closedArtwork.transform.localScale;
                openArtworkPosition = openArtwork.transform.localPosition;
                closedArtworkPosition = closedArtwork.transform.localPosition;
            }
        }
    }
}
