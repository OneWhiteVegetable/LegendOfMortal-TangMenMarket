using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XingShang.UI.Authoring
{
    [Serializable]
    internal sealed class UiMapData
    {
        public int schemaVersion = 1;
        public string bundleName = "xingshang_ui.bundle";
        public string unityVersion = "2020.3.49f1c1";
        public int referenceWidth = 1920;
        public int referenceHeight = 1080;
        public float matchWidthOrHeight = 0.5f;
        public string pathConvention = "Slash-separated relative to the instantiated prefab root; empty is the root.";
        public UiPrefabMap[] prefabs;
        public UiAssetMap[] sprites;
        public UiAssetMap[] fonts;
    }

    [Serializable]
    internal sealed class UiPrefabMap
    {
        public string name;
        public string asset;
        public UiNodeMap[] nodes;
    }

    [Serializable]
    internal sealed class UiNodeMap
    {
        public string key;
        public string path;
        public string type;
    }

    [Serializable]
    internal sealed class UiAssetMap
    {
        public string key;
        public string asset;

        public UiAssetMap(string key, string asset)
        {
            this.key = key;
            this.asset = asset;
        }
    }

    internal sealed class UiMapBuilder
    {
        private readonly List<UiNodeMap> nodes = new List<UiNodeMap>();

        public string PrefabName { get; private set; }

        public UiMapBuilder(string prefabName)
        {
            PrefabName = prefabName;
        }

        public void Add(string key, RectTransform root, RectTransform target, string type)
        {
            if (nodes.Any(node => node.key == key))
            {
                throw new InvalidOperationException("Duplicate binding key " + key + " in " + PrefabName);
            }

            nodes.Add(new UiNodeMap
            {
                key = key,
                path = target == root ? string.Empty : RelativePath(root, target),
                type = type
            });
        }

        public UiPrefabMap Build()
        {
            return new UiPrefabMap
            {
                name = PrefabName,
                asset = PrefabName,
                nodes = nodes.OrderBy(node => node.key, StringComparer.Ordinal).ToArray()
            };
        }

        private static string RelativePath(Transform root, Transform target)
        {
            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            if (current != root)
            {
                throw new InvalidOperationException(target.name + " is not beneath " + root.name);
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }
}
