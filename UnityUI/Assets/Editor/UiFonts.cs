using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal enum UiFontRole
    {
        Body,
        Bold,
        Serif,
        Display
    }

    internal sealed class UiFontDefinition
    {
        internal UiFontDefinition(
            string key, string assetName, string path, string licensePath,
            string familyName, string sha256, bool displayOnly, UiFontDefinition fallback)
        {
            Key = key;
            AssetName = assetName;
            Path = path;
            LicensePath = licensePath;
            LicenseAssetName = System.IO.Path.GetFileNameWithoutExtension(licensePath);
            FamilyName = familyName;
            Sha256 = sha256;
            DisplayOnly = displayOnly;
            Fallback = fallback;
        }

        internal readonly string Key;
        internal readonly string AssetName;
        internal readonly string Path;
        internal readonly string LicensePath;
        internal readonly string LicenseAssetName;
        internal readonly string FamilyName;
        internal readonly string Sha256;
        internal readonly bool DisplayOnly;
        internal readonly UiFontDefinition Fallback;
    }

    internal static class UiFonts
    {
        private const string HanProbe = "唐门行市货囊契匣崆峒辟邪玉铁拳暖食快报印花涨跌做空多读档旬负重资产";
        private const string SymbolProbe = "，。：；！？（）【】《》—…·“”‘’、−×▲▼✓";
        private const string CompatibilityProbe = "綠六茶";

        internal static readonly UiFontDefinition Regular = new UiFontDefinition(
            "regular", "NotoSansCJKsc-Regular", UiAssets.RegularFontPath, UiAssets.SharedLicensePath,
            "Noto Sans CJK SC", "2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b", false, null);

        internal static readonly UiFontDefinition Bold = new UiFontDefinition(
            "bold", "NotoSansCJKsc-Bold", UiAssets.BoldFontPath, UiAssets.SharedLicensePath,
            "Noto Sans CJK SC", "b5f0d1a190a7f9b43c310a8850630af12553df32c4c050543f9059732d9b4c0a", false, null);

        internal static readonly UiFontDefinition Serif = new UiFontDefinition(
            "serif", "NotoSerifSC-Regular", UiAssets.SerifFontPath, UiAssets.SerifLicensePath,
            "Noto Serif SC", "a513b97fa1503422d78ee1f40415db736926e2d0d8d99701bdcede8cf6f6058e", false, Regular);

        internal static readonly UiFontDefinition Display = new UiFontDefinition(
            "display", "BarlowCondensed-SemiBoldItalic", UiAssets.DisplayFontPath, UiAssets.DisplayLicensePath,
            "Barlow Condensed SemiBold", "958bd2a5bcb371d40fbb2480efda568a9bf5b3b1fdb8dc1fbdcadab959293bff", true, Regular);

        internal static readonly UiFontDefinition[] All = { Regular, Bold, Serif, Display };

        internal static Font Load(UiFontDefinition definition)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(definition.Path);
            if (font == null)
                throw new InvalidOperationException("Font import failed: " + definition.Path);
            return font;
        }

        internal static Font ForRole(UiFontRole role, string value)
        {
            switch (role)
            {
                case UiFontRole.Body:
                    return Load(Regular);
                case UiFontRole.Bold:
                    return Load(Bold);
                case UiFontRole.Serif:
                    return Load(Serif);
                case UiFontRole.Display:
                    return Load(IsDisplayText(value) ? Display : Bold);
                default:
                    throw new ArgumentOutOfRangeException("role", role, null);
            }
        }

        internal static bool IsDisplayText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\r' || character == '\n' || character == '\t')
                    continue;
                if (character < 32 || character > 126)
                    return false;
            }
            return true;
        }

        internal static void ValidateImportedFonts()
        {
            for (int i = 0; i < All.Length; i++)
            {
                UiFontDefinition definition = All[i];
                Font font = Load(definition);
                ValidateGlyphCoverage(definition, font);
                if (!font.dynamic)
                    throw new InvalidOperationException("Font must use Dynamic rendering: " + definition.Path);

                if (definition.Fallback == null)
                    continue;
                TrueTypeFontImporter importer = AssetImporter.GetAtPath(definition.Path) as TrueTypeFontImporter;
                if (importer == null || importer.fontNames == null ||
                    !importer.fontNames.Contains(definition.FamilyName) ||
                    !importer.fontNames.Contains(definition.Fallback.FamilyName))
                    throw new InvalidOperationException("Font fallback names are incomplete: " + definition.Path);
                Font fallback = Load(definition.Fallback);
                if (importer.fontReferences == null || !importer.fontReferences.Contains(fallback))
                    throw new InvalidOperationException("Bundled font fallback reference is missing: " + definition.Path);
            }
        }

        internal static void ValidatePrefabs(IEnumerable<string> prefabPaths)
        {
            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new InvalidOperationException("Cannot validate fonts in missing prefab: " + path);
                Text[] texts = prefab.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                    ValidateText(prefab.name, texts[i]);
            }
        }

        internal static void ValidateBundle(AssetBundle bundle, UiMapData map)
        {
            if (map.fonts == null || map.fonts.Length != All.Length)
                throw new InvalidOperationException("ui_map must contain regular, bold, serif and display fonts.");
            if (map.fonts.GroupBy(entry => entry.key).Count() != map.fonts.Length ||
                map.fonts.GroupBy(entry => entry.asset).Count() != map.fonts.Length)
                throw new InvalidOperationException("ui_map font keys and asset names must be unique.");

            for (int i = 0; i < All.Length; i++)
            {
                UiFontDefinition definition = All[i];
                UiAssetMap entry = map.fonts.SingleOrDefault(item => item.key == definition.Key);
                if (entry == null || entry.asset != definition.AssetName)
                    throw new InvalidOperationException("ui_map font compatibility entry is missing: " + definition.Key);
                Font font = bundle.LoadAsset<Font>(entry.asset);
                if (font == null)
                    throw new InvalidOperationException("Bundle font missing: " + entry.asset);
                ValidateGlyphCoverage(definition, font);
                TextAsset license = bundle.LoadAsset<TextAsset>(definition.LicenseAssetName);
                if (license == null)
                    throw new InvalidOperationException("Bundle font license missing: " + definition.LicenseAssetName);
                ValidateLicenseText(definition.LicensePath, license.text);
            }
        }

        internal static void ValidateLicenseText(string path, string text)
        {
            if (string.IsNullOrEmpty(text) ||
                text.IndexOf("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase) < 0 ||
                (text.IndexOf("Copyright", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("©", StringComparison.Ordinal) < 0))
                throw new InvalidOperationException("Font license must include its copyright notice and OFL text: " + path);
        }

        private static void ValidateText(string prefabName, Text text)
        {
            if (text.font == null)
                throw new InvalidOperationException("Missing font on " + prefabName + ":" + text.name);
            UiFontDefinition definition = Find(text.font);
            if (definition == null)
                throw new InvalidOperationException("Unregistered font on " + prefabName + ":" + text.name + " -> " + text.font.name);
            string value = text.text ?? string.Empty;
            if (definition.DisplayOnly && !IsDisplayText(value))
                throw new InvalidOperationException("Display font requires an ASCII-only numeric node; split units and arrows into CJK text: " + prefabName + ":" + text.name);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsControl(character) || char.IsSurrogate(character))
                    continue;
                if (!HasGlyph(definition, text.font, character))
                    throw new InvalidOperationException(string.Format(
                        "Missing glyph U+{0:X4} ({1}) on {2}:{3} using {4}.",
                        (int)character, character, prefabName, text.name, definition.AssetName));
            }
        }

        private static UiFontDefinition Find(Font font)
        {
            string path = AssetDatabase.GetAssetPath(font);
            for (int i = 0; i < All.Length; i++)
            {
                if (path == All[i].Path || font.name == All[i].AssetName)
                    return All[i];
            }
            return null;
        }

        private static void ValidateGlyphCoverage(UiFontDefinition definition, Font font)
        {
            if (definition.DisplayOnly)
            {
                for (int code = 32; code <= 126; code++)
                {
                    if (!font.HasCharacter((char)code))
                        throw new InvalidOperationException(string.Format(
                            "Display font {0} is missing ASCII U+{1:X4}.", definition.AssetName, code));
                }
            }
            else
            {
                for (int i = 0; i < HanProbe.Length; i++)
                {
                    if (!font.HasCharacter(HanProbe[i]))
                        throw new InvalidOperationException("CJK font probe failed: " + definition.AssetName + " U+" + ((int)HanProbe[i]).ToString("X4"));
                }
            }

            string fallbackText = SymbolProbe + CompatibilityProbe;
            for (int i = 0; i < fallbackText.Length; i++)
            {
                if (!HasGlyph(definition, font, fallbackText[i]))
                    throw new InvalidOperationException("CJK symbol/fallback probe failed: " + definition.AssetName + " U+" + ((int)fallbackText[i]).ToString("X4"));
            }
            if (definition.Fallback != null)
            {
                for (int i = 0; i < HanProbe.Length; i++)
                {
                    if (!HasGlyph(definition, font, HanProbe[i]))
                        throw new InvalidOperationException("CJK fallback probe failed: " + definition.AssetName + " U+" + ((int)HanProbe[i]).ToString("X4"));
                }
            }
        }

        private static bool HasGlyph(UiFontDefinition definition, Font font, char character)
        {
            if (font.HasCharacter(character))
                return true;
            return definition.Fallback != null && Load(definition.Fallback).HasCharacter(character);
        }
    }
}
