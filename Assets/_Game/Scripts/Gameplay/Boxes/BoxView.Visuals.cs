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
        /// <summary>
        /// Keep the Box4 artwork at one consistent rendered size across the board.
        /// </summary>
        private void NormalizeArtworkDimensions()
        {
            // The source PNG has broad transparent padding. Scale its canvas so
            // the visible rim, not the canvas, is exactly as wide as both doors.
            // Portrait presentation like the reference: the carton reads as an
            // upright box, not a wide tray leaning toward the camera.
            const float artworkCanvasWidth = 1.48f;
            const float artworkCanvasHeight = 1.82f;

            SetRenderedSize(openArtwork, artworkCanvasWidth, artworkCanvasHeight);
            SetRenderedSize(closedArtwork, 1.22f, 1.05f);
            openArtworkScale = Vector3.zero;
            closedArtworkScale = Vector3.zero;

            if (frontLipArtwork != null && openArtwork != null)
                frontLipArtwork.transform.localScale = openArtwork.transform.localScale;
        }

        private static void SetRenderedSize(SpriteRenderer renderer, float width, float height)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f) return;
            Vector3 scale = renderer.transform.localScale;
            renderer.transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, scale.z);
        }
        /// <summary>
        /// Builds the reference-style hybrid: layered 2D body, fake shadows and
        /// only two genuinely 3D thin doors. This also upgrades older sprite-door
        /// Box4 instances at runtime without changing gameplay transforms.
        /// </summary>
        private void Cache25DReferences()
        {
            Transform visualRoot = transform.Find("Visual Root (2.5D)");
            if (visualRoot == null) return;
            contactShadowArtwork = FindSprite(visualRoot, "Contact Shadow");
            depthArtwork = FindSprite(visualRoot, "Depth Back");
            leftDoorShadow = FindSprite(visualRoot, "Left Door Shadow");
            rightDoorShadow = FindSprite(visualRoot, "Right Door Shadow");
            EnsureBodySilhouetteMask(visualRoot);
            SoftenExistingLid(leftLid);
            SoftenExistingLid(rightLid);
            EnsureClosedDepthLayers(visualRoot);
            EnsureRimLighting(visualRoot);
            LayoutFruitSlots(visualRoot);
        }

        private void EnsureBodySilhouetteMask(Transform visualRoot)
        {
            if (openArtwork == null) return;
            Transform existing = visualRoot.Find("Body Silhouette Mask");
            GameObject maskObject = existing != null
                ? existing.gameObject
                : new GameObject("Body Silhouette Mask");
            if (existing == null) maskObject.transform.SetParent(visualRoot, false);
            Sprite maskSprite = GetSoftRoundedSprite();
            Vector2 bounds = maskSprite.bounds.size;
            maskObject.transform.localScale = new Vector3(1.00f / bounds.x, 1.12f / bounds.y, 1f);
            SpriteMask mask = maskObject.GetComponent<SpriteMask>();
            if (mask == null) mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = maskSprite;
            mask.alphaCutoff = 0.05f;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = openArtwork.sortingLayerID;
            mask.backSortingLayerID = openArtwork.sortingLayerID;
            // Include docked fruit in the body mask so tall artwork (especially
            // stems and leaves) can never protrude past the carton silhouette.
            mask.frontSortingOrder = FruitOrder + 1;
            mask.backSortingOrder = CavityOrder - 1;
            openArtwork.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        private static void SoftenExistingLid(Transform lid)
        {
            if (lid == null) return;
            MeshFilter[] filters = lid.GetComponentsInChildren<MeshFilter>(true);
            for (int index = 0; index < filters.Length; index++)
                ApplyRoundedDoorMesh(filters[index].transform);
        }

        private static void ApplyRoundedDoorMesh(Transform part)
        {
            if (part == null) return;
            MeshFilter filter = part.GetComponent<MeshFilter>();
            if (filter == null) return;
            if (roundedDoorMesh == null) roundedDoorMesh = BuildRoundedDoorMesh();
            filter.sharedMesh = roundedDoorMesh;
        }

        private static Mesh BuildRoundedDoorMesh()
        {
            const int cornerSegments = 5;
            const float radius = 0.15f;
            const float halfDepth = 0.5f;
            int perimeterCount = cornerSegments * 4;
            Vector3[] vertices = new Vector3[perimeterCount * 2 + 2];
            int frontCenter = perimeterCount * 2;
            int backCenter = frontCenter + 1;
            vertices[frontCenter] = new Vector3(0f, 0f, -halfDepth);
            vertices[backCenter] = new Vector3(0f, 0f, halfDepth);

            int vertex = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                float startAngle = -135f + corner * 90f;
                Vector2 center = corner switch
                {
                    0 => new Vector2(-0.5f + radius, -0.5f + radius),
                    1 => new Vector2(0.5f - radius, -0.5f + radius),
                    2 => new Vector2(0.5f - radius, 0.5f - radius),
                    _ => new Vector2(-0.5f + radius, 0.5f - radius)
                };
                for (int segment = 0; segment < cornerSegments; segment++)
                {
                    float angle = (startAngle + segment * 90f / (cornerSegments - 1)) * Mathf.Deg2Rad;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    vertices[vertex] = new Vector3(point.x, point.y, -halfDepth);
                    vertices[vertex + perimeterCount] = new Vector3(point.x, point.y, halfDepth);
                    vertex++;
                }
            }

            int[] triangles = new int[perimeterCount * 12];
            int triangle = 0;
            for (int index = 0; index < perimeterCount; index++)
            {
                int next = (index + 1) % perimeterCount;
                triangles[triangle++] = frontCenter;
                triangles[triangle++] = next;
                triangles[triangle++] = index;
                triangles[triangle++] = backCenter;
                triangles[triangle++] = index + perimeterCount;
                triangles[triangle++] = next + perimeterCount;
                triangles[triangle++] = index;
                triangles[triangle++] = next;
                triangles[triangle++] = next + perimeterCount;
                triangles[triangle++] = index;
                triangles[triangle++] = next + perimeterCount;
                triangles[triangle++] = index + perimeterCount;
            }

            Mesh mesh = new Mesh { name = "Runtime Rounded Box Door" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return mesh;
        }

        private static Color ColorFor(FruitType type) => type switch
        {
            FruitType.Apple => new Color(0f, 0.46f, 1f),
            FruitType.Orange => new Color(1f, 0.52f, 0.08f),
            FruitType.Grape => new Color(0.58f, 0.24f, 0.88f),
            FruitType.Lemon => new Color(1f, 0.84f, 0.12f),
            FruitType.Strawberry => new Color(1f, 0.34f, 0.52f),
            _ => Color.white
        };
    }
}
