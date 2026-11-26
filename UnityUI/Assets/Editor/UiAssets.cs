using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace XingShang.UI.Authoring
{
    internal static class UiAssets
    {
        public const string ArtRoot = "Assets/Art/V4";
        public const string ManifestPath = ArtRoot + "/manifest.json";
        public const string RegularFontPath = "Assets/Fonts/NotoSansCJKsc-Regular.otf";
        public const string BoldFontPath = "Assets/Fonts/NotoSansCJKsc-Bold.otf";
        public const string SerifFontPath = "Assets/Fonts/NotoSerifSC-Regular.ttf";
        public const string DisplayFontPath = "Assets/Fonts/BarlowCondensed-SemiBoldItalic.ttf";
        public const string SharedLicensePath = "Assets/Fonts/OFL.txt";
        public const string SerifLicensePath = "Assets/Fonts/NotoSerifSC-OFL.txt";
        public const string DisplayLicensePath = "Assets/Fonts/BarlowCondensed-OFL.txt";

        private static SpriteManifest manifest;
        private static Dictionary<string, SpriteAsset> byId;

        public static Font RegularFont
        {
            get { return UiFonts.Load(UiFonts.Regular); }
        }

        public static Font BoldFont
        {
            get { return UiFonts.Load(UiFonts.Bold); }
        }

        public static Font SerifFont
        {
            get { return UiFonts.Load(UiFonts.Serif); }
        }

        public static Font DisplayFont
        {
            get { return UiFonts.Load(UiFonts.Display); }
        }

        public static IEnumerable<SpriteAsset> Entries
        {
            get
            {
                EnsureManifest();
                return manifest.assets;
            }
        }

        public static void ConfigureAndValidate()
        {
            EnsureManifest();
            if (manifest.totalSpriteCount != 165 || manifest.assets.Length != 165)
            {
                throw new InvalidOperationException("V4 manifest must contain 165 sprites including the loan countdown numerals.");
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (SpriteAsset asset in manifest.assets)
                {
                    string path = AssetPath(asset.file);
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("Missing V4 sprite", path);
                    }

                    VerifyHash(path, asset.sha256);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        throw new InvalidOperationException("Not a texture: " + path);
                    }

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.sRGBTexture = true;
                    // These free-standing marks are shown at 15–50 UI pixels from 160–512px originals.
                    // Blend coverage across mip levels when a seal, coin or action icon is tilted and reduced.
                    bool downsample = asset.id == "loan_maturity_atlas" ||
                        asset.file.StartsWith("icons/", StringComparison.Ordinal) ||
                        asset.file.StartsWith("brand/", StringComparison.Ordinal) ||
                        (asset.file.StartsWith("buttons/", StringComparison.Ordinal) &&
                            Array.TrueForAll(asset.spriteBorder, edge => edge == 0));
                    importer.mipmapEnabled = downsample;
                    importer.filterMode = downsample ? FilterMode.Trilinear : ParseFilter(asset.filterMode);
                    importer.wrapMode = ParseWrap(asset.wrapMode);
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 2048;
                    importer.npotScale = TextureImporterNPOTScale.None;

                    TextureImporterSettings settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteExtrude = 0;
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(asset.pivot[0], asset.pivot[1]);
                    settings.spriteBorder = new Vector4(asset.spriteBorder[0], asset.spriteBorder[1], asset.spriteBorder[2], asset.spriteBorder[3]);
                    settings.spritePixelsPerUnit = asset.pixelsPerUnit;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (SpriteAsset asset in manifest.assets)
            {
                string path = AssetPath(asset.file);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    throw new InvalidOperationException("Sprite import failed: " + path);
                }

                Vector4 border = new Vector4(asset.spriteBorder[0], asset.spriteBorder[1], asset.spriteBorder[2], asset.spriteBorder[3]);
                if (sprite.border != border || !Mathf.Approximately(sprite.pixelsPerUnit, asset.pixelsPerUnit))
                {
                    throw new InvalidOperationException("Sprite settings mismatch: " + asset.id);
                }

                if (!Mathf.Approximately(sprite.rect.width, asset.width) || !Mathf.Approximately(sprite.rect.height, asset.height))
                {
                    throw new InvalidOperationException("Sprite dimensions mismatch: " + asset.id);
                }
            }

            for (int i = 0; i < UiFonts.All.Length; i++)
                ConfigureFont(UiFonts.All[i]);
            UiFonts.ValidateImportedFonts();
            Debug.Log("XingShang UI: validated " + manifest.assets.Length + " sprites, four OFL fonts, CJK fallbacks and font licenses.");
        }

        public static bool Has(string id)
        {
            EnsureManifest();
            return byId.ContainsKey(id);
        }

        public static Sprite Sprite(string id)
        {
            EnsureManifest();
            SpriteAsset asset;
            if (!byId.TryGetValue(id, out asset))
            {
                throw new KeyNotFoundException("Unknown V4 sprite id: " + id);
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath(asset.file));
            if (sprite == null)
            {
                throw new InvalidOperationException("Sprite is not imported: " + id);
            }

            return sprite;
        }

        public static string SpritePath(string id)
        {
            EnsureManifest();
            return AssetPath(byId[id].file);
        }

        private static void EnsureManifest()
        {
            if (manifest != null)
            {
                return;
            }

            manifest = JsonUtility.FromJson<SpriteManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.assets == null)
            {
                throw new InvalidOperationException("Cannot parse " + ManifestPath);
            }

            byId = manifest.assets.GroupBy(asset => asset.id).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            if (byId.Count != manifest.assets.Length)
            {
                throw new InvalidOperationException("V4 sprite ids must be unique.");
            }
        }

        private static string AssetPath(string file)
        {
            return ArtRoot + "/" + file.Replace('\\', '/');
        }

        private static void VerifyHash(string path, string expected)
        {
            if (string.IsNullOrEmpty(expected))
            {
                return;
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
                string actual = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SHA-256 mismatch: " + path);
                }
            }
        }

        private static FilterMode ParseFilter(string value)
        {
            return string.Equals(value, "Point", StringComparison.OrdinalIgnoreCase) ? FilterMode.Point : FilterMode.Bilinear;
        }

        private static TextureWrapMode ParseWrap(string value)
        {
            return string.Equals(value, "Repeat", StringComparison.OrdinalIgnoreCase) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        }

        private static void ConfigureFont(UiFontDefinition definition)
        {
            if (!File.Exists(definition.Path))
                throw new FileNotFoundException("Bundled font is missing.", definition.Path);
            if (!File.Exists(definition.LicensePath))
                throw new FileNotFoundException("Bundled font license is missing.", definition.LicensePath);
            VerifyHash(definition.Path, definition.Sha256);
            UiFonts.ValidateLicenseText(definition.LicensePath, File.ReadAllText(definition.LicensePath));

            TrueTypeFontImporter importer = AssetImporter.GetAtPath(definition.Path) as TrueTypeFontImporter;
            if (importer == null)
                throw new InvalidOperationException("Font import failed: " + definition.Path);

            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.fontRenderingMode = FontRenderingMode.HintedSmooth;
            importer.includeFontData = true;
            if (definition.Fallback != null)
            {
                importer.fontNames = new[] { definition.FamilyName, definition.Fallback.FamilyName };
                importer.fontReferences = new[] { UiFonts.Load(definition.Fallback) };
            }
            importer.SaveAndReimport();
        }
    }

    [Serializable]
    internal sealed class SpriteManifest
    {
        public int totalSpriteCount;
        public SpriteAsset[] assets;
    }

    [Serializable]
    internal sealed class SpriteAsset
    {
        public string id;
        public string file;
        public int width;
        public int height;
        public int[] spriteBorder;
        public float[] pivot;
        public float pixelsPerUnit;
        public string filterMode;
        public string wrapMode;
        public string sha256;
    }
}
