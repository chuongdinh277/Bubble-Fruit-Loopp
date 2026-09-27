using UnityEditor;
using UnityEngine;
using BubbleFruitLoop.Gameplay;

namespace BubbleFruitLoop.Editor
{
    public static partial class BoxSystemPrefabBuilder
    {
        private static Transform CreateLidModel(string name, Transform parent, Material material,
            float height, bool left)
        {
            GameObject hinge = new(name + " Hinge");
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = new Vector3(left ? -0.51f : 0.51f, 0f, -0.12f);
            hinge.transform.localRotation = Quaternion.Euler(0f, left ? -78f : 78f, 0f);
            Renderer panel = CreatePart(name + " Panel", hinge.transform,
                new Vector3(0.49f, height, 0.045f),
                new Vector3(left ? 0.245f : -0.245f, 0f, 0f), material, 20);
            panel.transform.localRotation = Quaternion.identity;
            panel.sortingOrder = 40;
            Renderer innerFace = CreatePart(name + " Inner Face", panel.transform,
                new Vector3(0.97f, 0.97f, 0.035f), new Vector3(0f, 0f, 0.515f), material, 40);
            innerFace.transform.localRotation = Quaternion.identity;
            Renderer edge = CreatePart(name + " Edge", panel.transform,
                new Vector3(0.025f, 0.97f, 1.06f),
                new Vector3(left ? 0.49f : -0.49f, 0f, 0f), material, 41);
            edge.transform.localRotation = Quaternion.identity;
            Renderer highlight = CreatePart(name + " Highlight", panel.transform,
                new Vector3(0.015f, 0.94f, 1.04f),
                new Vector3(left ? -0.47f : 0.47f, 0f, -0.02f), material, 41);
            highlight.transform.localRotation = Quaternion.identity;
            return hinge.transform;
        }

        private static SpriteRenderer CreateSpriteLayer(string objectName, Transform parent, Sprite sprite,
            Vector3 position, Vector2 size, Color color, int sortingOrder, Material material)
        {
            GameObject layer = new(objectName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = position;
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (material != null) renderer.sharedMaterial = material;
            Vector2 spriteSize = sprite.bounds.size;
            layer.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
            return renderer;
        }

        private static void CreateDividerPair(Transform parent, Sprite sprite, bool vertical)
        {
            Vector2 size = vertical ? new Vector2(0.018f, 0.78f) : new Vector2(0.74f, 0.018f);
            Vector3 offset = vertical ? new Vector3(-0.007f, 0.015f, 0f) : new Vector3(0f, 0.003f, 0f);
            CreateSpriteLayer(vertical ? "Vertical Divider Shadow" : "Horizontal Divider Shadow", parent,
                sprite, offset + new Vector3(0.008f, -0.008f, 0f), size,
                new Color(0f, 0f, 0f, 0.16f), 22, null);
            CreateSpriteLayer(vertical ? "Vertical Divider Highlight" : "Horizontal Divider Highlight", parent,
                sprite, offset + new Vector3(-0.006f, 0.006f, 0f), size,
                new Color(1f, 1f, 1f, 0.24f), 23, null);
        }

        private static void CreateBodySilhouetteMask(Transform parent, Sprite sprite, SpriteRenderer cavity)
        {
            GameObject maskObject = new("Body Silhouette Mask");
            maskObject.transform.SetParent(parent, false);
            Vector2 size = sprite.bounds.size;
            maskObject.transform.localScale = new Vector3(1.00f / size.x, 1.12f / size.y, 1f);
            SpriteMask mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = sprite;
            mask.alphaCutoff = 0.05f;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = cavity.sortingLayerID;
            mask.backSortingLayerID = cavity.sortingLayerID;
            mask.frontSortingOrder = 21;
            mask.backSortingOrder = 14;
            cavity.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        private static void CreateRimLighting(Transform parent, Sprite sprite)
        {
            CreateSpriteLayer("Top Rim Highlight", parent, sprite,
                new Vector3(0f, 0.515f, -0.01f), new Vector2(0.86f, 0.045f),
                new Color(1f, 1f, 1f, 0.38f), 24, null);
            CreateSpriteLayer("Left Rim Highlight", parent, sprite,
                new Vector3(-0.465f, 0.015f, -0.01f), new Vector2(0.025f, 0.96f),
                new Color(1f, 1f, 1f, 0.20f), 24, null);
            CreateSpriteLayer("Right Rim Shade", parent, sprite,
                new Vector3(0.465f, -0.01f, -0.01f), new Vector2(0.03f, 0.98f),
                new Color(0f, 0f, 0f, 0.20f), 24, null);
            CreateSpriteLayer("Bottom Rim Shade", parent, sprite,
                new Vector3(0f, -0.515f, -0.01f), new Vector2(0.86f, 0.06f),
                new Color(0f, 0f, 0f, 0.26f), 24, null);
        }

        private static Renderer CreatePart(string name, Transform parent, Vector3 size,
            Vector3 position, Material material, int sortingOrder)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Transform[] CreateSlots(Transform root, int capacity)
        {
            Transform[] slots = new Transform[capacity];
            int rows = capacity == 4 ? 2 : 3;
            const float x = 0.235f;
            float y = capacity == 4 ? 0.31f : 0.31f;
            for (int index = 0; index < capacity; index++)
            {
                Transform slot = new GameObject($"Fruit Slot {index + 1}").transform;
                slot.SetParent(root, false);
                int row = index / 2;
                int column = index % 2;
                slot.localPosition = new Vector3(column == 0 ? -x : x,
                    (rows - 1 - row * 2) * y, -0.08f);
                slots[index] = slot;
            }
            return slots;
        }

        private static SpriteRenderer CreateArtwork(string name, Transform parent, Sprite sprite,
            int capacity, bool isOpen, int sortingOrder, float alpha)
        {
            GameObject artworkObject = new(name);
            artworkObject.transform.SetParent(parent, false);
            // Normalize using the opaque artwork bounds, not the transparent 1254px canvas.
            // This gives every box the same visible width while preserving its aspect ratio.
            Vector2 size = sprite.bounds.size;
            artworkObject.transform.localScale = new Vector3(1.48f / size.x, 1.82f / size.y, 1f);
            SpriteRenderer renderer = artworkObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = new Color(1f, 1f, 1f, alpha);
            return renderer;
        }

        private static Sprite ImportArtwork(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool needsImport = importer.textureType != TextureImporterType.Sprite ||
                               !Mathf.Approximately(importer.spritePixelsPerUnit, 1024f) ||
                               !importer.alphaIsTransparency || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (needsImport) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Material EnsureColorizeMaterial()
        {
            const string materialPath = ArtFolder + "/BoxColorize.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = Shader.Find("BubbleFruit/BoxColorize");
            if (shader == null)
            {
                Debug.LogError("BubbleFruit/BoxColorize shader has not finished importing.");
                return null;
            }
            if (material == null)
            {
                material = new Material(shader) { name = "BoxColorize" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Material EnsureModelMaterial()
        {
            const string materialPath = ArtFolder + "/BoxModel3D.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = Shader.Find("BubbleFruit/BoxModel3D");
            if (shader == null)
            {
                Debug.LogError("BubbleFruit/BoxModel3D shader has not finished importing.");
                return null;
            }
            if (material == null)
            {
                material = new Material(shader) { name = "BoxModel3D" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Art")) AssetDatabase.CreateFolder("Assets/_Game", "Art");
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Box");
            if (!AssetDatabase.IsValidFolder(ResourceFolder)) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        }
    }
}
