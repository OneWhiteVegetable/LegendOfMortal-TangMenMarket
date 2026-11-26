using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using UnityEngine;
using XingShangMod.Data;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiAssetBundle : IDisposable
    {
        internal const string BundleName = "xingshang_ui.bundle";
        internal const string RelativeBundlePath = "Runtime/bundles/xingshang_ui.bundle";

        private static readonly string[] RequiredPrefabNames =
        {
            "GoodsCard", "CartEntry", "InventorySlot", "RankingRow", "ChartHost", "NewsTicker",
            "Toast", "Tooltip", "Modal", "ItemDetail", "AssetsPage", "InventoryWindow", "MarketWindow"
        };

        private readonly Dictionary<string, UiPrefabMap> _prefabs =
            new Dictionary<string, UiPrefabMap>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _sprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> _prefabAssets =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private AssetBundle _bundle;
        private bool _disposed;

        internal UiAssetBundle()
        {
            string path = GetRuntimePath();
            if (!File.Exists(path))
                throw new FileNotFoundException("UI AssetBundle is missing.", path);
            _bundle = AssetBundle.LoadFromFile(path);
            if (_bundle == null)
                throw new InvalidOperationException("Cannot load UI AssetBundle: " + path);

            try
            {
                TextAsset mapAsset = _bundle.LoadAsset<TextAsset>("ui_map");
                if (mapAsset == null)
                    throw new InvalidOperationException("UI binding map is missing from the AssetBundle.");
                string raw = mapAsset.text ?? string.Empty;
                MapSha256 = ComputeSha256(Encoding.UTF8.GetBytes(raw));
                UiMapData map = JsonCodec.Deserialize<UiMapData>(raw);
                if (map == null || map.schemaVersion != 1 || map.prefabs == null ||
                    !string.Equals(map.bundleName, BundleName, StringComparison.Ordinal))
                    throw new InvalidOperationException("UI binding map has an unsupported schema.");
                for (int i = 0; i < map.prefabs.Length; i++)
                {
                    UiPrefabMap prefab = map.prefabs[i];
                    if (prefab == null || string.IsNullOrEmpty(prefab.name) ||
                        string.IsNullOrEmpty(prefab.asset) || prefab.nodes == null)
                        throw new InvalidOperationException("UI binding map contains an invalid prefab entry at index " + i + ".");
                    _prefabs.Add(prefab.name, prefab);
                }

                for (int i = 0; i < RequiredPrefabNames.Length; i++)
                {
                    string name = RequiredPrefabNames[i];
                    UiPrefabMap prefabMap = GetPrefabMap(name);
                    GameObject prefabAsset = LoadRequired<GameObject>(prefabMap.asset);
                    _prefabAssets[name] = prefabAsset;
                }
                LoadFonts(map);
                Sprite dryBrush = GetSprite("mask_chart_drybrush_tile");
                DryBrushTexture = dryBrush == null ? null : dryBrush.texture;
            }
            catch
            {
                _bundle.Unload(true);
                _bundle = null;
                throw;
            }
        }

        internal bool IsAlive { get { return !_disposed && _bundle != null; } }
        internal string MapSha256 { get; private set; }
        internal Font RegularFont { get; private set; }
        internal Font BoldFont { get; private set; }
        internal Font SerifFont { get; private set; }
        internal Font DisplayFont { get; private set; }
        internal Texture2D DryBrushTexture { get; private set; }

        internal static string GetRuntimePath()
        {
            if (string.IsNullOrWhiteSpace(Paths.PluginPath))
                throw new InvalidOperationException("BepInEx plugin path is unavailable.");
            return Path.Combine(
                Paths.PluginPath,
                "XingShangMod",
                "Runtime",
                "bundles",
                BundleName);
        }

        internal UiPrefabInstance Create(string prefabName, Transform parent = null)
        {
            ThrowIfDisposed();
            GameObject asset = GetPrefabAsset(prefabName);
            GameObject instance = parent == null
                ? UnityEngine.Object.Instantiate(asset)
                : UnityEngine.Object.Instantiate(asset, parent, false);
            instance.name = "XingShangMod." + prefabName;
            instance.SetActive(false);
            instance.transform.localScale = Vector3.one;
            instance.transform.localRotation = Quaternion.identity;
            try
            {
                return new UiPrefabInstance(instance, GetPrefabMap(prefabName), true);
            }
            catch
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                throw;
            }
        }

        internal UiPrefabInstance Adopt(string prefabName, GameObject instance)
        {
            ThrowIfDisposed();
            if (instance == null)
                throw new ArgumentNullException("instance");
            return new UiPrefabInstance(instance, GetPrefabMap(prefabName), false);
        }

        internal Sprite GetSprite(string key)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(key))
                return null;
            Sprite sprite;
            if (_sprites.TryGetValue(key, out sprite))
                return sprite;
            sprite = _bundle.LoadAsset<Sprite>(key);
            _sprites[key] = sprite;
            return sprite;
        }

        private void LoadFonts(UiMapData map)
        {
            if (map.fonts == null)
            {
                RegularFont = LoadRequired<Font>("NotoSansCJKsc-Regular");
                BoldFont = LoadRequired<Font>("NotoSansCJKsc-Bold");
                SerifFont = RegularFont;
                DisplayFont = RegularFont;
                return;
            }

            RegularFont = LoadMappedFont(map.fonts, "regular");
            BoldFont = LoadMappedFont(map.fonts, "bold");
            SerifFont = LoadMappedFont(map.fonts, "serif");
            DisplayFont = LoadMappedFont(map.fonts, "display");
        }

        private Font LoadMappedFont(UiAssetMap[] fonts, string key)
        {
            UiAssetMap match = null;
            for (int i = 0; i < fonts.Length; i++)
            {
                UiAssetMap entry = fonts[i];
                if (entry == null || !string.Equals(entry.key, key, StringComparison.Ordinal))
                    continue;
                if (match != null)
                    throw new InvalidOperationException("UI binding map contains a duplicate font key: " + key);
                match = entry;
            }
            if (match == null)
                throw new InvalidOperationException("UI binding map font entry is missing: " + key);
            if (string.IsNullOrEmpty(match.asset))
                throw new InvalidOperationException("UI binding map font asset is empty: " + key);
            return LoadRequired<Font>(match.asset);
        }

        private GameObject GetPrefabAsset(string name)
        {
            GameObject prefab;
            if (_prefabAssets.TryGetValue(name, out prefab) && prefab != null)
                return prefab;
            UiPrefabMap map = GetPrefabMap(name);
            prefab = LoadRequired<GameObject>(map.asset);
            _prefabAssets[name] = prefab;
            return prefab;
        }

        private UiPrefabMap GetPrefabMap(string name)
        {
            UiPrefabMap map;
            if (!_prefabs.TryGetValue(name, out map))
            {
                throw new InvalidOperationException("Prefab is absent from ui_map: " + name +
                    ". Current keys[" + _prefabs.Count + "]: " + AvailableKeys());
            }
            return map;
        }

        private string AvailableKeys()
        {
            if (_prefabs.Count == 0)
                return "<empty>";
            List<string> keys = new List<string>(_prefabs.Keys);
            keys.Sort(StringComparer.Ordinal);
            return string.Join(",", keys.ToArray());
        }

        private T LoadRequired<T>(string assetName) where T : UnityEngine.Object
        {
            T asset = _bundle.LoadAsset<T>(assetName);
            if (asset == null)
                throw new InvalidOperationException("UI AssetBundle asset is missing: " + assetName);
            return asset;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2"));
                return result.ToString();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_bundle != null)
                _bundle.Unload(true);
            _bundle = null;
            _prefabs.Clear();
            _prefabAssets.Clear();
            _sprites.Clear();
            MapSha256 = null;
            RegularFont = null;
            BoldFont = null;
            SerifFont = null;
            DisplayFont = null;
            DryBrushTexture = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _bundle == null)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }

    [Serializable]
    public sealed class UiMapData
    {
        public int schemaVersion;
        public string bundleName;
        public UiPrefabMap[] prefabs;
        public UiAssetMap[] fonts;
    }

    [Serializable]
    public sealed class UiAssetMap
    {
        public string key;
        public string asset;
    }

    [Serializable]
    public sealed class UiPrefabMap
    {
        public string name;
        public string asset;
        public UiNodeMap[] nodes;
    }

    [Serializable]
    public sealed class UiNodeMap
    {
        public string key;
        public string path;
        public string type;
    }
}
