using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using XingShangMod.Data;

namespace XingShangMod.UI.Infrastructure
{
    public sealed class SpriteAssetLibrary : IDisposable
    {
        public const string RuntimeRelativePath = "Runtime/ui/v4";

        private static readonly string[] KnownProperties =
        {
            "assets", "id", "file", "width", "height", "spriteBorder", "pivot",
            "pixelsPerUnit", "filterMode", "wrapMode"
        };

        private readonly Dictionary<string, AssetDto> _assets =
            new Dictionary<string, AssetDto>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CacheEntry> _cache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CacheEntry _fallback;
        private bool _manifestLoaded;
        private bool _disposed;

        private SpriteAssetLibrary()
        {
        }

        public SpriteAssetLibrary(string pluginRoot) : this()
        {
            if (string.IsNullOrWhiteSpace(pluginRoot))
            {
                throw new ArgumentException("A plugin root is required.", "pluginRoot");
            }
            AssetDirectory = Path.GetFullPath(Path.Combine(pluginRoot, RuntimeRelativePath));
        }

        public static SpriteAssetLibrary FromAssetDirectory(string assetDirectory)
        {
            if (string.IsNullOrWhiteSpace(assetDirectory))
            {
                throw new ArgumentException("An asset directory is required.", "assetDirectory");
            }
            SpriteAssetLibrary library = new SpriteAssetLibrary();
            library.AssetDirectory = Path.GetFullPath(assetDirectory);
            return library;
        }

        public string AssetDirectory { get; private set; }
        public string FallbackSpriteId { get; set; }
        public Action<string> LogWarning { get; set; }

        public int AssetCount
        {
            get
            {
                EnsureManifest();
                return _assets.Count;
            }
        }

        public int LoadedSpriteCount
        {
            get
            {
                ThrowIfDisposed();
                return _cache.Count;
            }
        }

        public Sprite FallbackSprite
        {
            get
            {
                ThrowIfDisposed();
                return GetFallbackSprite();
            }
        }

        public bool Contains(string id)
        {
            EnsureManifest();
            return !string.IsNullOrWhiteSpace(id) && _assets.ContainsKey(id);
        }

        public Sprite GetSprite(string id)
        {
            ThrowIfDisposed();
            Sprite sprite;
            if (TryGetSprite(id, out sprite))
            {
                return sprite;
            }
            if (!string.IsNullOrWhiteSpace(FallbackSpriteId) &&
                !string.Equals(id, FallbackSpriteId, StringComparison.OrdinalIgnoreCase) &&
                TryGetSprite(FallbackSpriteId, out sprite))
            {
                return sprite;
            }
            return GetFallbackSprite();
        }

        public bool TryGetSprite(string id, out Sprite sprite)
        {
            ThrowIfDisposed();
            sprite = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }
            EnsureManifest();
            CacheEntry cached;
            if (_cache.TryGetValue(id, out cached))
            {
                sprite = cached.Sprite;
                return sprite != null;
            }
            if (_failedIds.Contains(id))
            {
                return false;
            }
            CacheEntry loaded = LoadSprite(id);
            if (loaded == null)
            {
                _failedIds.Add(id);
                return false;
            }
            _cache.Add(id, loaded);
            sprite = loaded.Sprite;
            return true;
        }

        public void Preload(IEnumerable<string> ids)
        {
            ThrowIfDisposed();
            if (ids == null)
            {
                return;
            }
            foreach (string id in ids)
            {
                Sprite ignored;
                TryGetSprite(id, out ignored);
            }
        }

        public bool Invalidate(string id)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }
            _failedIds.Remove(id);
            CacheEntry entry;
            if (!_cache.TryGetValue(id, out entry))
            {
                return false;
            }
            _cache.Remove(id);
            DestroyEntry(entry);
            return true;
        }

        public void Clear()
        {
            ThrowIfDisposed();
            foreach (CacheEntry entry in _cache.Values)
            {
                DestroyEntry(entry);
            }
            _cache.Clear();
            _failedIds.Clear();
            DestroyEntry(_fallback);
            _fallback = null;
        }

        public void Reload()
        {
            Clear();
            _assets.Clear();
            _manifestLoaded = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            Clear();
            _assets.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void EnsureManifest()
        {
            ThrowIfDisposed();
            if (_manifestLoaded)
            {
                return;
            }
            _manifestLoaded = true;
            string manifestPath = Path.Combine(AssetDirectory, "manifest.json");
            try
            {
                if (!File.Exists(manifestPath))
                {
                    ReportWarning("Sprite manifest not found: " + manifestPath);
                    return;
                }
                string json = NormalizePropertyNames(File.ReadAllText(manifestPath));
                if (json.TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    json = "{\"assets\":" + json + "}";
                }
                ManifestDto manifest = JsonCodec.Deserialize<ManifestDto>(json);
                if (manifest == null || manifest.assets == null)
                {
                    ReportWarning("Sprite manifest has no assets: " + manifestPath);
                    return;
                }
                foreach (AssetDto asset in manifest.assets)
                {
                    if (asset == null || string.IsNullOrWhiteSpace(asset.id))
                    {
                        continue;
                    }
                    if (_assets.ContainsKey(asset.id))
                    {
                        ReportWarning("Duplicate sprite id ignored: " + asset.id);
                        continue;
                    }
                    _assets.Add(asset.id, asset);
                }
            }
            catch (Exception exception)
            {
                ReportWarning("Unable to read sprite manifest: " + exception.Message);
            }
        }

        private CacheEntry LoadSprite(string id)
        {
            AssetDto asset;
            if (!_assets.TryGetValue(id, out asset))
            {
                ReportWarning("Unknown sprite id: " + id);
                return null;
            }

            Texture2D texture = null;
            try
            {
                string pngPath = ResolveAssetPath(AssetDirectory, asset.file);
                string sidecarPath = Path.ChangeExtension(pngPath, ".json");
                AssetDto sidecar = null;
                if (File.Exists(sidecarPath))
                {
                    string sidecarJson = NormalizePropertyNames(File.ReadAllText(sidecarPath));
                    sidecar = JsonCodec.Deserialize<AssetDto>(sidecarJson);
                    if (sidecar != null && !string.IsNullOrWhiteSpace(sidecar.file))
                    {
                        pngPath = ResolveAssetPath(Path.GetDirectoryName(pngPath), sidecar.file);
                    }
                }
                if (!File.Exists(pngPath))
                {
                    ReportWarning("Sprite PNG not found for '" + id + "': " + pngPath);
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(pngPath);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                texture.name = id + ".Texture";
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    ReportWarning("Invalid PNG for sprite: " + id);
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                string filterValue = sidecar != null && !string.IsNullOrWhiteSpace(sidecar.filterMode)
                    ? sidecar.filterMode
                    : asset.filterMode;
                string wrapValue = sidecar != null && !string.IsNullOrWhiteSpace(sidecar.wrapMode)
                    ? sidecar.wrapMode
                    : asset.wrapMode;
                texture.filterMode = ParseFilterMode(filterValue);
                texture.wrapMode = ParseWrapMode(wrapValue);
                texture.Apply(false, false);

                float[] pivotData = sidecar != null && sidecar.pivot != null ? sidecar.pivot : asset.pivot;
                float[] borderData = sidecar != null && sidecar.spriteBorder != null
                    ? sidecar.spriteBorder
                    : asset.spriteBorder;
                float pixelsPerUnit = sidecar != null && IsPositiveFinite(sidecar.pixelsPerUnit)
                    ? sidecar.pixelsPerUnit
                    : asset.pixelsPerUnit;
                if (!IsPositiveFinite(pixelsPerUnit))
                {
                    pixelsPerUnit = 100f;
                }

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    ReadPivot(pivotData),
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect,
                    ReadBorder(borderData));
                sprite.name = id;
                return new CacheEntry(sprite, texture);
            }
            catch (Exception exception)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                ReportWarning("Unable to load sprite '" + id + "': " + exception.Message);
                return null;
            }
        }

        private Sprite GetFallbackSprite()
        {
            if (_fallback != null && _fallback.Sprite != null)
            {
                return _fallback.Sprite;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            texture.name = "XingShangMod.MissingSprite.Texture";
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "XingShangMod.MissingSprite";
            _fallback = new CacheEntry(sprite, texture);
            return sprite;
        }

        private string ResolveAssetPath(string baseDirectory, string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new InvalidDataException("Sprite file is empty.");
            }
            string root = Path.GetFullPath(AssetDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string combined = Path.IsPathRooted(file) ? file : Path.Combine(baseDirectory, file);
            string fullPath = Path.GetFullPath(combined);
            string rootWithSeparator = root + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Sprite file is outside the asset directory: " + file);
            }
            return fullPath;
        }

        private static string NormalizePropertyNames(string json)
        {
            string normalized = json;
            foreach (string property in KnownProperties)
            {
                normalized = Regex.Replace(
                    normalized,
                    "\"" + property + "\"\\s*:",
                    "\"" + property + "\":",
                    RegexOptions.IgnoreCase);
            }
            return normalized;
        }

        private static Vector2 ReadPivot(float[] values)
        {
            if (values == null || values.Length < 2 ||
                !IsFinite(values[0]) || !IsFinite(values[1]))
            {
                return new Vector2(0.5f, 0.5f);
            }
            return new Vector2(values[0], values[1]);
        }

        private static Vector4 ReadBorder(float[] values)
        {
            if (values == null || values.Length < 4)
            {
                return Vector4.zero;
            }
            for (int i = 0; i < 4; i++)
            {
                if (!IsFinite(values[i]) || values[i] < 0f)
                {
                    return Vector4.zero;
                }
            }
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

        private static FilterMode ParseFilterMode(string value)
        {
            FilterMode mode;
            if (Enum.TryParse(value, true, out mode) && Enum.IsDefined(typeof(FilterMode), mode))
            {
                return mode;
            }
            return FilterMode.Bilinear;
        }

        private static TextureWrapMode ParseWrapMode(string value)
        {
            TextureWrapMode mode;
            if (Enum.TryParse(value, true, out mode) && Enum.IsDefined(typeof(TextureWrapMode), mode))
            {
                return mode;
            }
            return TextureWrapMode.Clamp;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static void DestroyEntry(CacheEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            if (entry.Sprite != null)
            {
                UnityEngine.Object.Destroy(entry.Sprite);
            }
            if (entry.Texture != null)
            {
                UnityEngine.Object.Destroy(entry.Texture);
            }
        }

        private void ReportWarning(string message)
        {
            string formatted = "[XingShangMod.UI] " + message;
            Action<string> logger = LogWarning;
            if (logger != null)
            {
                try
                {
                    logger(formatted);
                    return;
                }
                catch
                {
                }
            }
            Debug.LogWarning(formatted);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

#pragma warning disable 0649
        [Serializable]
        public sealed class ManifestDto
        {
            public AssetDto[] assets;
        }

        [Serializable]
        public sealed class AssetDto
        {
            public string id;
            public string file;
            public int width;
            public int height;
            public float[] spriteBorder;
            public float[] pivot;
            public float pixelsPerUnit;
            public string filterMode;
            public string wrapMode;
        }
#pragma warning restore 0649

        public sealed class CacheEntry
        {
            public CacheEntry(Sprite sprite, Texture2D texture)
            {
                Sprite = sprite;
                Texture = texture;
            }

            public Sprite Sprite { get; private set; }
            public Texture2D Texture { get; private set; }
        }
    }
}
