using System.IO;
using UnityEditor;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    public static class Faux3DBoxGenerator
    {
        private const string ArtDir = "Assets/_Game/Art/Box";

        [MenuItem("BubbleFruit/Generate Faux3D Textures")]
        public static void GenerateTextures()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Art")) AssetDatabase.CreateFolder("Assets/_Game", "Art");
            if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Box");

            GenerateRimTexture();
            GenerateSlotTexture();
            GenerateLidTexture();
            AssetDatabase.Refresh();
            Debug.Log("Faux 3D Textures Generated!");
        }

        private static void GenerateRimTexture()
        {
            int size = 128;
            float outerRadius = 32f;
            float innerRadius = 16f;
            float rimThickness = 24f; // Distance from outer edge to inner edge
            
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            Vector2 lightDir = new Vector2(-1f, 1f).normalized;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    
                    // Outer SDF (Box Size: size x size)
                    Vector2 outerHalfSize = new Vector2(size/2f, size/2f);
                    float dOuter = RoundedRectSDF(p, outerHalfSize, outerRadius);
                    
                    // Inner SDF (Hole Size)
                    Vector2 innerHalfSize = new Vector2(size/2f - rimThickness, size/2f - rimThickness);
                    float dInner = RoundedRectSDF(p, innerHalfSize, innerRadius);

                    float alpha = 1f;
                    
                    // Anti-aliasing outer edge
                    if (dOuter > 0) alpha = Mathf.Clamp01(1f - dOuter);
                    // Anti-aliasing inner edge
                    if (dInner < 0) alpha = Mathf.Min(alpha, Mathf.Clamp01(1f + dInner));

                    if (alpha <= 0.01f)
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }

                    // Calculate Bevel Profile
                    // Map distance from inner to outer boundary to [0, 1]
                    float edgeDist = 0f;
                    float normalZ = 1f;
                    Vector2 normalXY = Vector2.zero;

                    // Compute gradient (normal)
                    float eps = 1f;
                    float dOuterX = RoundedRectSDF(p + new Vector2(eps, 0), outerHalfSize, outerRadius) - dOuter;
                    float dOuterY = RoundedRectSDF(p + new Vector2(0, eps), outerHalfSize, outerRadius) - dOuter;
                    
                    float dInnerX = RoundedRectSDF(p + new Vector2(eps, 0), innerHalfSize, innerRadius) - dInner;
                    float dInnerY = RoundedRectSDF(p + new Vector2(0, eps), innerHalfSize, innerRadius) - dInner;

                    if (dOuter > -rimThickness * 0.5f) 
                    {
                        // Outer bevel
                        normalXY = new Vector2(dOuterX, dOuterY).normalized;
                        edgeDist = Mathf.Abs(dOuter) / (rimThickness * 0.5f);
                    }
                    else 
                    {
                        // Inner bevel (facing opposite way because it's a hole)
                        normalXY = new Vector2(-dInnerX, -dInnerY).normalized;
                        edgeDist = Mathf.Abs(dInner) / (rimThickness * 0.5f);
                    }

                    // Smooth bevel shape (cosine curve)
                    normalZ = Mathf.Sin(Mathf.Clamp01(edgeDist) * Mathf.PI * 0.5f);
                    Vector3 normal = new Vector3(normalXY.x, normalXY.y, normalZ + 0.5f).normalized;

                    // Diffuse lighting
                    Vector3 light3D = new Vector3(lightDir.x, lightDir.y, 1.2f).normalized;
                    float diffuse = Mathf.Max(0f, Vector3.Dot(normal, light3D));
                    
                    // Remap to base color
                    float baseGray = 0.6f;
                    float finalColor = baseGray + (diffuse - 0.5f) * 0.6f;
                    finalColor = Mathf.Clamp01(finalColor);

                    tex.SetPixel(x, y, new Color(finalColor, finalColor, finalColor, alpha));
                }
            }
            SaveTexture(tex, "FauxRim.png", 32);
        }

        private static void GenerateSlotTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            Vector2 lightDir = new Vector2(-1f, 1f).normalized;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float dOuter = RoundedRectSDF(p, new Vector2(size/2f, size/2f), 12f);
                    float alpha = Mathf.Clamp01(1f - dOuter);
                    
                    if (alpha <= 0.01f)
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }

                    // Subtle inner bevel (debossed)
                    float eps = 1f;
                    float dx = RoundedRectSDF(p + new Vector2(eps, 0), new Vector2(size/2f, size/2f), 12f) - dOuter;
                    float dy = RoundedRectSDF(p + new Vector2(0, eps), new Vector2(size/2f, size/2f), 12f) - dOuter;
                    
                    Vector3 normal = new Vector3(-dx, -dy, 1.5f).normalized; // Inverted for deboss
                    Vector3 light3D = new Vector3(lightDir.x, lightDir.y, 1f).normalized;
                    
                    float diffuse = Mathf.Max(0f, Vector3.Dot(normal, light3D));
                    float finalColor = 0.45f + (diffuse - 0.5f) * 0.2f; // Darker tray
                    tex.SetPixel(x, y, new Color(finalColor, finalColor, finalColor, alpha));
                }
            }
            SaveTexture(tex, "FauxSlot.png", 12);
        }

        private static void GenerateLidTexture()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            Vector2 lightDir = new Vector2(-1f, 1f).normalized;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float dOuter = RoundedRectSDF(p, new Vector2(size/2f, size/2f), 16f);
                    float alpha = Mathf.Clamp01(1f - dOuter);
                    
                    if (alpha <= 0.01f)
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }

                    // Convex bevel
                    float eps = 1f;
                    float dx = RoundedRectSDF(p + new Vector2(eps, 0), new Vector2(size/2f, size/2f), 16f) - dOuter;
                    float dy = RoundedRectSDF(p + new Vector2(0, eps), new Vector2(size/2f, size/2f), 16f) - dOuter;
                    
                    Vector3 normal = new Vector3(dx, dy, 2f).normalized;
                    Vector3 light3D = new Vector3(lightDir.x, lightDir.y, 1f).normalized;
                    
                    float diffuse = Mathf.Max(0f, Vector3.Dot(normal, light3D));
                    float finalColor = 0.7f + (diffuse - 0.5f) * 0.5f; 
                    tex.SetPixel(x, y, new Color(finalColor, finalColor, finalColor, alpha));
                }
            }
            SaveTexture(tex, "FauxLid.png", 16);
        }

        private static float RoundedRectSDF(Vector2 p, Vector2 halfSize, float radius)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + new Vector2(radius, radius);
            return Mathf.Min(Mathf.Max(d.x, d.y), 0.0f) + Vector2.Max(d, Vector2.zero).magnitude - radius;
        }

        private static void SaveTexture(Texture2D texture, string fileName, int border)
        {
            string path = $"{ArtDir}/{fileName}";
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = new Vector4(border, border, border, border); // 9-slicing!
            importer.SaveAndReimport();
        }
    }
}

