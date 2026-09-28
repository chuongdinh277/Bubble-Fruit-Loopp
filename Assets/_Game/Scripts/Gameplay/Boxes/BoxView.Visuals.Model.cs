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
                Sprite fallbackSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Box/Box4_Open_3D.png");
                AssignEditorBoxMaterial(fallbackSprite);
#endif
                if (openArtwork == null)
                    return;
            }

            if (obsoleteFull3D != null)
                obsoleteFull3D.gameObject.SetActive(false);
            Shader shader = Shader.Find("BubbleFruit/BoxModel3D");
            if (shader == null)
                return;
            CreateRuntimeBoxModel(out visualRoot, shader);
            ConfigureRuntimeBoxLighting(visualRoot);
        }

        private static Renderer CreateRuntimePart(string partName, Transform parent, Vector3 size, Vector3 position)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = runtime3DMaterial;
            return renderer;
        }

        private static Transform CreateRuntimeLid(string lidName, Transform parent, bool left, out SpriteRenderer shadow)
        {
            Transform hinge = new GameObject(lidName + " Hinge").transform;
            hinge.SetParent(parent, false);
            // Each half-door is attached to its outside vertical edge. Open =
            // folded to the two sides; closed = both rotate inward and meet.
            hinge.localPosition = new Vector3(left ? -0.51f : 0.51f, 0f, -0.12f);
            hinge.localRotation = Quaternion.Euler(0f, left ? -78f : 78f, 0f);
            Renderer panel = CreateRuntimePart(lidName + " Panel", hinge, new Vector3(0.49f, 1.12f, 0.045f), new Vector3(left ? 0.245f : -0.245f, 0f, 0f));
            ApplyRoundedDoorMesh(panel.transform);
            panel.transform.localRotation = Quaternion.identity;
            panel.sortingOrder = DoorOrder;
            Renderer innerFace = CreateRuntimePart(lidName + " Inner Face", panel.transform, new Vector3(0.97f, 0.97f, 0.035f), new Vector3(0f, 0f, 0.515f));
            ApplyRoundedDoorMesh(innerFace.transform);
            innerFace.sortingOrder = DoorOrder;
            Renderer edge = CreateRuntimePart(lidName + " Edge", panel.transform, new Vector3(0.025f, 0.97f, 1.06f), new Vector3(left ? 0.49f : -0.49f, 0f, 0f));
            ApplyRoundedDoorMesh(edge.transform);
            edge.sortingOrder = DoorHighlightOrder;
            Renderer highlight = CreateRuntimePart(lidName + " Highlight", panel.transform, new Vector3(0.015f, 0.94f, 1.04f), new Vector3(left ? -0.47f : 0.47f, 0f, -0.02f));
            ApplyRoundedDoorMesh(highlight.transform);
            highlight.sortingOrder = DoorHighlightOrder;
            shadow = CreateRuntimeSprite(lidName + " Shadow", parent, GetSoftRoundedSprite(), new Vector3(left ? -0.30f : 0.30f, -0.04f, 0.02f), new Vector2(0.44f, 1.00f), new Color(0f, 0f, 0f, 0.08f), DoorShadowOrder);
            return hinge;
        }

        private static SpriteRenderer CreateRuntimeSprite(string objectName, Transform parent, Sprite sprite, Vector3 position, Vector2 size, Color color, int sortingOrder)
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
            CreateRuntimeSprite(vertical ? "Vertical Divider Shadow" : "Horizontal Divider Shadow", parent, GetSoftRoundedSprite(), offset + new Vector3(0.008f, -0.008f, 0f), size, new Color(0f, 0f, 0f, 0.16f), DividerOrder);
            CreateRuntimeSprite(vertical ? "Vertical Divider Highlight" : "Horizontal Divider Highlight", parent, GetSoftRoundedSprite(), offset + new Vector3(-0.006f, 0.006f, 0f), size, new Color(1f, 1f, 1f, 0.24f), DividerOrder + 1);
        }

        private void CreateRuntimeBoxModel(out Transform visualRoot, Shader shader)
        {
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
            if (closedArtwork != null)
                closedArtwork.gameObject.SetActive(false);
            contactShadowArtwork = CreateRuntimeSprite("Contact Shadow", visualRoot, GetSoftRoundedSprite(), new Vector3(0f, -0.075f, 0.08f), new Vector2(0.84f, 1.04f), new Color(0f, 0f, 0f, 0.10f), ContactShadowOrder);
            depthArtwork = CreateRuntimeSprite("Depth Back", visualRoot, GetSoftRoundedSprite(), new Vector3(0f, -0.035f, 0.04f), new Vector2(0.88f, 1.08f), Color.white, DepthOrder);
            CreateDividerPair(visualRoot, true);
            CreateDividerPair(visualRoot, false);
        }

#if UNITY_EDITOR
        private void AssignEditorBoxMaterial(Sprite fallbackSprite)
        {
            Material fallbackMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Box/BoxColorize.mat");
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
        }

#endif
        private void ConfigureRuntimeBoxLighting(Transform visualRoot)
        {
            EnsureRimLighting(visualRoot);
            if (leftLid != null)
                leftLid.gameObject.SetActive(false);
            if (rightLid != null)
                rightLid.gameObject.SetActive(false);
            leftLid = CreateRuntimeLid("Left Door", visualRoot, true, out leftDoorShadow);
            rightLid = CreateRuntimeLid("Right Door", visualRoot, false, out rightDoorShadow);
            EnsureClosedDepthLayers(visualRoot);
            LayoutFruitSlots(visualRoot);
            boxRenderer = openArtwork;
            innerTray = openArtwork.transform;
            backShadow = depthArtwork.transform;
        }
    }
}
