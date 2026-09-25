#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    public static class MaterialAssetMigration
    {
        private const string LegacyPath = "Assets/_Game/Art/Materials";

        public static void MoveLegacyMaterials(string destinationFolder)
        {
            string[] names =
            {
                "Fruit_Apple", "Fruit_Orange", "Fruit_Grape", "Fruit_Lemon",
                "Bubble", "Box", "Environment"
            };

            for (int index = 0; index < names.Length; index++)
            {
                string source = $"{LegacyPath}/{names[index]}.mat";
                string destination = $"{destinationFolder}/{names[index]}.mat";
                bool destinationMissing = AssetDatabase.LoadAssetAtPath<Material>(destination) == null;
                bool sourceExists = AssetDatabase.LoadAssetAtPath<Material>(source) != null;
                if (destinationMissing && sourceExists) AssetDatabase.MoveAsset(source, destination);
            }
        }
    }
}
#endif
