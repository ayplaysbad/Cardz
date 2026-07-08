#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LastFreeCity.UI.Editor
{
    [InitializeOnLoad]
    public static class SpriteImportSettingsHelper
    {
        static SpriteImportSettingsHelper()
        {
            // Run automatically when the editor compiles or loads
            ConfigureSprites();
        }

        [MenuItem("Last Free City/Configure Sprite Settings")]
        public static void ConfigureSprites()
        {
            string spritesFolder = "Assets/UI/Sprites";
            string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { spritesFolder });

            bool assetsChanged = false;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    bool shouldBeSprite = importer.textureType != TextureImporterType.Sprite;
                    bool shouldBeSingleSprite = ShouldForceSingleSprite(path)
                        && importer.spriteImportMode != SpriteImportMode.Single;
                    bool shouldDisableMipmaps = importer.mipmapEnabled;
                    bool shouldUseBilinear = importer.filterMode != FilterMode.Bilinear;

                    if (shouldBeSprite || shouldBeSingleSprite || shouldDisableMipmaps || shouldUseBilinear)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.mipmapEnabled = false;
                        importer.filterMode = FilterMode.Bilinear;

                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                        assetsChanged = true;
                        Debug.Log($"Successfully converted texture at '{path}' to Sprite (2D and UI) type.");
                    }
                }
            }

            if (assetsChanged)
            {
                AssetDatabase.Refresh();
            }
        }

        private static bool ShouldForceSingleSprite(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string normalizedPath = assetPath.Replace('\\', '/');
            return normalizedPath.StartsWith("Assets/UI/Sprites/character/", System.StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith("Assets/UI/Sprites/infrastructure/", System.StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith("Assets/UI/Sprites/abilities/", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
