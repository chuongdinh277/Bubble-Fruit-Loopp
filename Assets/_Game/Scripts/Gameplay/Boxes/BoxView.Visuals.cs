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
        private void EnsureRuntime25DModel()
        {
            Transform visualRoot = transform.Find("Visual Root (2.5D)");
            if (visualRoot != null)
            {
                Cache25DReferences();
                return;
            }

            // Version 24 briefly generated a fully meshed tray. Keep it disabled
            // while the prefab builder upgrades the asset to the intended hybrid.
            Transform obsoleteFull3D = transform.Find("Model Root (3D Tilt)");
            // Never make a stale scene instance invisible: if its new layered
            // sprite reference is not present yet, the editor rebuild will fix
            // the prefab on the next domain reload and the old visual stays as a
            // safe fallback until then.
            if (openArtwork == null)
            {
#if UNITY_EDITOR
                // Scene instances made from the short-lived full-3D prefab must
                // also look correct immediately in Play Mode, before the asset
                // itself has a chance to rebuild.
                Sprite fallbackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Game/Art/Box/Box4_Open_3D.png");
                Material fallbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Game/Art/Box/BoxColorize.mat");
                if (fallbackSprite != null)
                {
                    GameObject cavityObject = new GameObject("Cavity + BackBody");
                    cavityObject.transform.SetParent(transform, false);
                    openArtwork = cavityObject.AddComponent<SpriteRenderer>();
                    openArtwork.sprite = fallbackSprite;
                    openArtwork.sharedMaterial = fallbackMaterial;
                    openArtwork.sortingOrder = CavityOrder;
                    SetRenderedSize(openArtwork, 1.48f, 1.82f);
                }
#endif
                if (openArtwork == null) return;
            }
            if (obsoleteFull3D != null) obsoleteFull3D.gameObject.SetActive(false);

            Shader shader = Shader.Find("BubbleFruit/BoxModel3D");
            if (shader == null) return;
            if (runtime3DMaterial == null)
            {
                runtime3DMaterial = new Material(shader)
                {
                    name = "Runtime Box 3D Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            visualRoot = new GameObject("Visual Root (2.5D)").transform;
            visualRoot.SetParent(transform, false);

            openArtwork.transform.SetParent(visualRoot, true);
            openArtwork.name = "Cavity + BackBody";
            openArtwork.sortingOrder = CavityOrder;
            EnsureBodySilhouetteMask(visualRoot);
            if (closedArtwork != null) closedArtwork.gameObject.SetActive(false);

            contactShadowArtwork = CreateRuntimeSprite("Contact Shadow", visualRoot,
                GetSoftRoundedSprite(), new Vector3(0f, -0.075f, 0.08f), new Vector2(0.84f, 1.04f),
                new Color(0f, 0f, 0f, 0.10f), ContactShadowOrder);
            depthArtwork = CreateRuntimeSprite("Depth Back", visualRoot,
                GetSoftRoundedSprite(), new Vector3(0f, -0.035f, 0.04f), new Vector2(0.88f, 1.08f),
                Color.white, DepthOrder);

            CreateDividerPair(visualRoot, true);
            CreateDividerPair(visualRoot, false);
            EnsureRimLighting(visualRoot);

            if (leftLid != null) leftLid.gameObject.SetActive(false);
            if (rightLid != null) rightLid.gameObject.SetActive(false);
            leftLid = CreateRuntimeLid("Left Door", visualRoot, true, out leftDoorShadow);
            rightLid = CreateRuntimeLid("Right Door", visualRoot, false, out rightDoorShadow);
            EnsureClosedDepthLayers(visualRoot);
            LayoutFruitSlots(visualRoot);

            boxRenderer = openArtwork;
            innerTray = openArtwork.transform;
            backShadow = depthArtwork.transform;
        }

        private static Renderer CreateRuntimePart(string partName, Transform parent, Vector3 size, Vector3 position)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = runtime3DMaterial;
            return renderer;
        }

        private static Transform CreateRuntimeLid(string lidName, Transform parent, bool left,
            out SpriteRenderer shadow)
        {
            Transform hinge = new GameObject(lidName + " Hinge").transform;
            hinge.SetParent(parent, false);
            // Each half-door is attached to its outside vertical edge. Open =
            // folded to the two sides; closed = both rotate inward and meet.
            hinge.localPosition = new Vector3(left ? -0.51f : 0.51f, 0f, -0.12f);
            hinge.localRotation = Quaternion.Euler(0f, left ? -78f : 78f, 0f);
            Renderer panel = CreateRuntimePart(lidName + " Panel", hinge,
                new Vector3(0.49f, 1.12f, 0.045f), new Vector3(left ? 0.245f : -0.245f, 0f, 0f));
            ApplyRoundedDoorMesh(panel.transform);
            panel.transform.localRotation = Quaternion.identity;
            panel.sortingOrder = DoorOrder;
            Renderer innerFace = CreateRuntimePart(lidName + " Inner Face", panel.transform,
                new Vector3(0.97f, 0.97f, 0.035f), new Vector3(0f, 0f, 0.515f));
            ApplyRoundedDoorMesh(innerFace.transform);
            innerFace.sortingOrder = DoorOrder;
            Renderer edge = CreateRuntimePart(lidName + " Edge", panel.transform,
                new Vector3(0.025f, 0.97f, 1.06f), new Vector3(left ? 0.49f : -0.49f, 0f, 0f));
            ApplyRoundedDoorMesh(edge.transform);
            edge.sortingOrder = DoorHighlightOrder;
            Renderer highlight = CreateRuntimePart(lidName + " Highlight", panel.transform,
                new Vector3(0.015f, 0.94f, 1.04f), new Vector3(left ? -0.47f : 0.47f, 0f, -0.02f));
            ApplyRoundedDoorMesh(highlight.transform);
            highlight.sortingOrder = DoorHighlightOrder;

            shadow = CreateRuntimeSprite(lidName + " Shadow", parent, GetSoftRoundedSprite(),
                new Vector3(left ? -0.30f : 0.30f, -0.04f, 0.02f), new Vector2(0.44f, 1.00f),
                new Color(0f, 0f, 0f, 0.08f), DoorShadowOrder);
            return hinge;
        }

        private static SpriteRenderer CreateRuntimeSprite(string objectName, Transform parent, Sprite sprite,
            Vector3 position, Vector2 size, Color color, int sortingOrder)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = position;
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            Vector2 bounds = sprite.bounds.size;
            layer.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
            return renderer;
        }

        private static void CreateDividerPair(Transform parent, bool vertical)
        {
            Vector2 size = vertical ? new Vector2(0.018f, 0.78f) : new Vector2(0.74f, 0.018f);
            Vector3 offset = vertical ? new Vector3(-0.007f, 0.015f, 0f) : new Vector3(0f, 0.003f, 0f);
            CreateRuntimeSprite(vertical ? "Vertical Divider Shadow" : "Horizontal Divider Shadow", parent,
                GetSoftRoundedSprite(), offset + new Vector3(0.008f, -0.008f, 0f), size,
                new Color(0f, 0f, 0f, 0.16f), DividerOrder);
            CreateRuntimeSprite(vertical ? "Vertical Divider Highlight" : "Horizontal Divider Highlight", parent,
                GetSoftRoundedSprite(), offset + new Vector3(-0.006f, 0.006f, 0f), size,
                new Color(1f, 1f, 1f, 0.24f), DividerOrder + 1);
        }

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

        private static void EnsureRimLighting(Transform visualRoot)
        {
            Sprite soft = GetSoftRoundedSprite();
            CreateLightingLayerIfMissing("Top Rim Highlight", visualRoot, soft,
                new Vector3(0f, 0.515f, -0.01f), new Vector2(0.86f, 0.052f),
                new Color(1f, 1f, 1f, 0.56f));
            CreateLightingLayerIfMissing("Left Rim Highlight", visualRoot, soft,
                new Vector3(-0.465f, 0.015f, -0.01f), new Vector2(0.032f, 0.96f),
                new Color(1f, 1f, 1f, 0.34f));
            CreateLightingLayerIfMissing("Right Rim Shade", visualRoot, soft,
                new Vector3(0.465f, -0.01f, -0.01f), new Vector2(0.04f, 0.98f),
                new Color(0f, 0f, 0f, 0.30f));
            CreateLightingLayerIfMissing("Bottom Rim Shade", visualRoot, soft,
                new Vector3(0f, -0.515f, -0.01f), new Vector2(0.86f, 0.07f),
                new Color(0f, 0f, 0f, 0.38f));
            CreateLightingLayerIfMissing("Bright Corner", visualRoot, soft,
                new Vector3(-0.43f, 0.48f, -0.02f), new Vector2(0.12f, 0.12f),
                new Color(1f, 1f, 1f, 0.42f));
            CreateLightingLayerIfMissing("Dark Corner", visualRoot, soft,
                new Vector3(0.43f, -0.48f, -0.02f), new Vector2(0.13f, 0.13f),
                new Color(0f, 0f, 0f, 0.25f));
        }

        private static void CreateLightingLayerIfMissing(string objectName, Transform parent,
            Sprite sprite, Vector3 position, Vector2 size, Color color)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.color = color;
                return;
            }
            CreateRuntimeSprite(objectName, parent, sprite, position, size, color, DividerOrder + 2);
        }

        private void EnsureClosedDepthLayers(Transform visualRoot)
        {
            if (visualRoot == null) return;
            closedDepthRoot = visualRoot.Find("Closed Carton Depth");
            if (closedDepthRoot == null)
            {
                closedDepthRoot = new GameObject("Closed Carton Depth").transform;
                closedDepthRoot.SetParent(visualRoot, false);
                closedDepthBack = CreateRuntimeSprite("Back Extrusion", closedDepthRoot,
                    GetSoftRoundedSprite(), new Vector3(0.055f, -0.075f, 0.06f),
                    new Vector2(1.03f, 1.14f), Color.white, DoorOrder - 3);
                closedBottomBevel = CreateRuntimeSprite("Bottom Bevel", closedDepthRoot,
                    GetSoftRoundedSprite(), new Vector3(0.01f, -0.555f, 0.03f),
                    new Vector2(0.94f, 0.14f), Color.white, DoorOrder + 2);
                closedRightBevel = CreateRuntimeSprite("Right Bevel", closedDepthRoot,
                    GetSoftRoundedSprite(), new Vector3(0.49f, -0.025f, 0.03f),
                    new Vector2(0.11f, 0.98f), Color.white, DoorOrder + 1);
            }
            else
            {
                closedDepthBack = FindSprite(closedDepthRoot, "Back Extrusion");
                closedBottomBevel = FindSprite(closedDepthRoot, "Bottom Bevel");
                closedRightBevel = FindSprite(closedDepthRoot, "Right Bevel");
            }
            ApplyClosedDepthColor(configuredColor);
            UpdateClosedDepth(0f);
        }

        private void ApplyClosedDepthColor(Color color)
        {
            Color bright = MakeBrightColor(color);
            SetSpriteColor(closedDepthBack, Color.Lerp(bright, Color.black, 0.34f));
            SetSpriteColor(closedBottomBevel, Color.Lerp(bright, Color.black, 0.48f));
            SetSpriteColor(closedRightBevel, Color.Lerp(bright, Color.black, 0.42f));
        }

        private static void SetSpriteColor(SpriteRenderer renderer, Color color)
        {
            if (renderer == null) return;
            color.a = renderer.color.a;
            renderer.color = color;
        }

        private void UpdateClosedDepth(float closed)
        {
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.92f, closed));
            SetSpriteAlpha(closedDepthBack, alpha);
            SetSpriteAlpha(closedBottomBevel, alpha * 0.95f);
            SetSpriteAlpha(closedRightBevel, alpha * 0.9f);

            // Open-tray highlights used to remain visible behind the shut doors,
            // producing little tabs and spikes outside the closed silhouette.
            // Fade every tray-only detail as the doors meet.
            Transform visualRoot = closedDepthRoot != null ? closedDepthRoot.parent : null;
            float openAlpha = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.30f, 0.88f, closed));
            SetNamedLayerAlpha(visualRoot, "Top Rim Highlight", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Left Rim Highlight", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Right Rim Shade", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Bottom Rim Shade", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Bright Corner", openAlpha);
            SetNamedLayerAlpha(visualRoot, "Dark Corner", openAlpha);
        }

        private static void SetNamedLayerAlpha(Transform root, string childName, float alpha)
        {
            if (root == null) return;
            Transform child = root.Find(childName);
            if (child == null) return;
            SetSpriteAlpha(child.GetComponent<SpriteRenderer>(), alpha);
        }

        private static void SetSpriteAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }

        private void LayoutFruitSlots(Transform visualRoot)
        {
            if (fruitSlots == null) return;
            for (int index = 0; index < fruitSlots.Length && index < 4; index++)
            {
                Transform slot = fruitSlots[index];
                if (slot == null) continue;
                slot.SetParent(visualRoot, false);
                int row = index / 2;
                int column = index % 2;
                slot.localPosition = new Vector3(column == 0 ? -0.235f : 0.235f,
                    row == 0 ? 0.31f : -0.31f, -0.08f);
            }
        }

        private static SpriteRenderer FindSprite(Transform root, string objectName)
        {
            Transform child = root.Find(objectName);
            return child != null ? child.GetComponent<SpriteRenderer>() : null;
        }
        private void EnsureFrontLip()
        {
            if (frontLipArtwork != null || openArtwork == null) return;

            GameObject maskObject = new GameObject("Front Lip Mask");
            maskObject.transform.SetParent(transform, false);
            // Occlude only the lowest slice of the bottom-row fruit. The old
            // 0.40-high mask swallowed slot 3 almost completely.
            maskObject.transform.localPosition = new Vector3(0f, editorCapacity <= 4 ? -0.49f : -0.50f, -0.15f);
            maskObject.transform.localScale = new Vector3(1.08f, editorCapacity <= 4 ? 0.20f : 0.24f, 1f);
            frontLipMask = maskObject.AddComponent<SpriteMask>();
            frontLipMask.sprite = GetLipMaskSprite();
            frontLipMask.alphaCutoff = 0.01f;
            frontLipMask.isCustomRangeActive = true;
            frontLipMask.frontSortingLayerID = openArtwork.sortingLayerID;
            frontLipMask.backSortingLayerID = openArtwork.sortingLayerID;
            frontLipMask.frontSortingOrder = FrontWallOrder + 1;
            frontLipMask.backSortingOrder = FrontWallOrder - 1;

            GameObject lipObject = new GameObject("Front Lip Artwork");
            lipObject.transform.SetParent(transform, false);
            lipObject.transform.localPosition = openArtwork.transform.localPosition;
            lipObject.transform.localRotation = openArtwork.transform.localRotation;
            lipObject.transform.localScale = openArtwork.transform.localScale;
            frontLipArtwork = lipObject.AddComponent<SpriteRenderer>();
            frontLipArtwork.sprite = openArtwork.sprite;
            frontLipArtwork.sharedMaterial = openArtwork.sharedMaterial;
            frontLipArtwork.color = openArtwork.color;
            frontLipArtwork.sortingLayerID = openArtwork.sortingLayerID;
            frontLipArtwork.sortingOrder = FrontWallOrder;
            frontLipArtwork.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        private static Sprite GetLipMaskSprite()
        {
            if (lipMaskSprite != null) return lipMaskSprite;
            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "Runtime Box Front Lip Mask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[64];
            for (int index = 0; index < pixels.Length; index++) pixels[index] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            lipMaskSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            lipMaskSprite.name = "Runtime Box Front Lip Mask";
            lipMaskSprite.hideFlags = HideFlags.HideAndDontSave;
            return lipMaskSprite;
        }

        private static Sprite GetSoftRoundedSprite()
        {
            if (softRoundedSprite != null) return softRoundedSprite;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Soft Rounded Rectangle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            // Softer toy-like corners for the carton silhouette and its shadows.
            const float radius = 0.29f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                Vector2 delta = new Vector2(Mathf.Abs(point.x - 0.5f), Mathf.Abs(point.y - 0.5f));
                Vector2 corner = new Vector2(Mathf.Max(delta.x - (0.5f - radius), 0f),
                    Mathf.Max(delta.y - (0.5f - radius), 0f));
                float distance = corner.magnitude - radius;
                float alpha = 1f - Mathf.SmoothStep(-0.025f, 0.035f, distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            softRoundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            softRoundedSprite.name = "Runtime Soft Rounded Rectangle";
            softRoundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return softRoundedSprite;
        }

        private static Sprite GetParallelogramShadowSprite()
        {
            if (parallelogramShadowSprite != null) return parallelogramShadowSprite;
            Texture2D authoredTexture = Resources.Load<Texture2D>("Shadows/BoxGroundShadow");
            if (authoredTexture != null)
            {
                // Use the PNG's own transparent, feathered silhouette. Setting
                // PPU to its width gives stable 1:2 world-space sprite bounds.
                parallelogramShadowSprite = Sprite.Create(authoredTexture,
                    new Rect(0f, 0f, authoredTexture.width, authoredTexture.height),
                    new Vector2(0.5f, 0.5f), authoredTexture.width);
                parallelogramShadowSprite.name = "Box Ground Shadow (PNG)";
                parallelogramShadowSprite.hideFlags = HideFlags.HideAndDontSave;
                return parallelogramShadowSprite;
            }

            const int width = 64;
            const int height = 128;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime Rounded Parallelogram Shadow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) / width - 0.5f;
                float py = (y + 0.5f) / height - 0.5f;
                // A hanging cast-shadow silhouette: the right edge stays almost
                // vertical while the soft left edge and sloped crown droop down.
                float leftEdge = -0.32f + (py + 0.5f) * 0.075f;
                const float rightEdge = 0.31f;
                float across = Mathf.InverseLerp(leftEdge, rightEdge, px);
                float topEdge = Mathf.Lerp(0.27f, 0.43f, Mathf.SmoothStep(0f, 1f, across));
                const float bottomEdge = -0.49f;

                float distanceInside = Mathf.Min(
                    Mathf.Min(px - leftEdge, rightEdge - px),
                    Mathf.Min(topEdge - py, py - bottomEdge));
                // Feather the exposed left side more heavily like a real projected
                // shadow; the other edges remain softly rounded but readable.
                float leftBlend = Mathf.SmoothStep(-0.085f, 0.045f, px - leftEdge);
                float bodyBlend = Mathf.SmoothStep(-0.018f, 0.028f, distanceInside);
                float alpha = leftBlend * bodyBlend;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            parallelogramShadowSprite = Sprite.Create(texture,
                new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 64f);
            parallelogramShadowSprite.name = "Runtime Rounded Parallelogram Shadow";
            parallelogramShadowSprite.hideFlags = HideFlags.HideAndDontSave;
            return parallelogramShadowSprite;
        }
        private void ApplyColor(Color color)
        {
            color = MakeBrightColor(color);
            ApplyClosedDepthColor(color);
            if (openArtwork != null)
            {
                Color openColor = color;
                openColor.a = openArtwork.color.a;
                openArtwork.color = openColor;
                if (closedArtwork != null)
                {
                    Color closedColor = color;
                    closedColor.a = closedArtwork.color.a;
                    closedArtwork.color = closedColor;
                }
                if (frontLipArtwork != null) frontLipArtwork.color = openColor;
                if (depthArtwork != null)
                {
                    Color depth = Color.Lerp(color, Color.black, 0.30f);
                    depth.a = 1f;
                    depthArtwork.color = depth;
                }
                if (contactShadowArtwork != null)
                    contactShadowArtwork.color = new Color(0f, 0f, 0f, 0.12f);
                SetLidColor(leftLid, color);
                SetLidColor(rightLid, color);
                return;
            }
            Color rim = Color.Lerp(color, Color.black, 0.24f);
            Color shadow = Color.Lerp(color, Color.black, 0.52f);
            Color tray = Color.Lerp(color, Color.black, 0.14f);
            Color slot = Color.Lerp(color, Color.white, 0.12f);
            Color highlight = Color.Lerp(color, Color.white, 0.34f);
            Color sideShade = Color.Lerp(color, Color.black, 0.18f);
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
            for (int index = 0; index < sprites.Length; index++)
            {
                string objectName = sprites[index].gameObject.name;
                if (objectName == "Box Shadow") sprites[index].color = shadow;
                else if (objectName == "Inner Tray") sprites[index].color = tray;
                else if (objectName == "Slot Plate") sprites[index].color = slot;
                else if (objectName.Contains("Top Wall") || objectName.Contains("Left Wall")) sprites[index].color = highlight;
                else if (objectName.Contains("Bottom Wall") || objectName.Contains("Right Wall")) sprites[index].color = sideShade;
                else if (objectName.Contains("Lid")) sprites[index].color = color;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] is SpriteRenderer) continue;
                string objectName = renderers[index].gameObject.name;
                SetRendererColor(renderers[index], objectName == "Box Shadow" ? shadow
                    : objectName.Contains("Inner Tray") ? tray
                    : objectName.Contains("Slot Plate") ? slot
                    : objectName.Contains("Top Wall") || objectName.Contains("Left Wall") ? highlight
                    : objectName.Contains("Bottom Wall") || objectName.Contains("Right Wall") ? sideShade
                    : objectName.Contains("Divider") ? rim
                    : objectName.Contains("Lid") ? highlight
                    : color);
            }
        }

        private static void SetLidColor(Transform lid, Color color)
        {
            if (lid == null) return;
            Renderer[] renderers = lid.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                string objectName = renderers[index].gameObject.name;
                Color partColor = objectName.Contains("Highlight")
                    ? Color.Lerp(color, Color.white, 0.36f)
                    : objectName.Contains("Inner") || objectName.Contains("Edge")
                        ? Color.Lerp(color, Color.black, 0.22f)
                        : color;
                SetRendererColor(renderers[index], partColor);
            }
        }

        private static Color MakeBrightColor(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            // Keep distinct fruit colours but give every box the bright toy-like
            // finish used by the reference UI.
            saturation = Mathf.Clamp(saturation * 1.08f, 0.68f, 0.94f);
            value = Mathf.Clamp(Mathf.Max(value, 0.96f), 0f, 1f);
            Color bright = Color.HSVToRGB(hue, saturation, value);
            bright.a = color.a;
            return bright;
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            if (renderer is SpriteRenderer sprite) sprite.color = color;
            else
            {
                colorPropertyBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(colorPropertyBlock);
                colorPropertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(colorPropertyBlock);
                colorPropertyBlock.Clear();
            }
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
