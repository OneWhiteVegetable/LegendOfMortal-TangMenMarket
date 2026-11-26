using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiPrefabInstance : IDisposable
    {
        private readonly Dictionary<string, Component> _nodes =
            new Dictionary<string, Component>(StringComparer.Ordinal);
        private readonly bool _ownsInstance;
        private bool _disposed;

        internal UiPrefabInstance(GameObject root, UiPrefabMap map, bool ownsInstance)
        {
            if (root == null)
                throw new ArgumentNullException("root");
            if (map == null)
                throw new ArgumentNullException("map");
            Root = root.GetComponent<RectTransform>();
            if (Root == null)
                throw new InvalidOperationException("Prefab root is not a RectTransform: " + map.name);
            if (ownsInstance)
                foreach (Selectable control in root.GetComponentsInChildren<Selectable>(true))
                    UiFocusOrigin.Attach(control);
            _ownsInstance = ownsInstance;
            Name = map.name;

            for (int i = 0; i < map.nodes.Length; i++)
            {
                UiNodeMap node = map.nodes[i];
                if (node == null || string.IsNullOrEmpty(node.key) || _nodes.ContainsKey(node.key))
                    throw new InvalidOperationException("Invalid or duplicate UI binding in " + map.name + ".");
                Transform target = string.IsNullOrEmpty(node.path) ? root.transform : root.transform.Find(node.path);
                if (target == null)
                    throw new InvalidOperationException("Broken UI binding " + map.name + ":" + node.key);
                Component component = Resolve(target, node.type);
                if (component == null)
                    throw new InvalidOperationException(
                        "UI binding type mismatch " + map.name + ":" + node.key + " (" + node.type + ").");
                _nodes.Add(node.key, component);
            }
        }

        internal string Name { get; private set; }
        internal RectTransform Root { get; private set; }
        internal GameObject GameObject { get { return Root == null ? null : Root.gameObject; } }
        internal bool IsAlive { get { return !_disposed && Root != null; } }

        internal T Get<T>(string key) where T : Component
        {
            Component component;
            if (!_nodes.TryGetValue(key, out component) || component == null)
                throw new InvalidOperationException("UI binding is unavailable: " + Name + ":" + key);
            T typed = component as T;
            if (typed == null)
                throw new InvalidOperationException("UI binding has the wrong component: " + Name + ":" + key);
            return typed;
        }

        internal Text Text(string key) { return Get<Text>(key); }

        internal T ReplaceTextComponent<T>(string key, Text expected) where T : Text
        {
            if (!ReferenceEquals(Text(key), expected))
                throw new InvalidOperationException("UI text binding changed: " + Name + ":" + key);
            GameObject target = expected.gameObject;
            List<string> aliases = null;
            foreach (KeyValuePair<string, Component> entry in _nodes)
            {
                if (entry.Key != key && ReferenceEquals(entry.Value, expected))
                {
                    if (aliases == null)
                        aliases = new List<string>();
                    aliases.Add(entry.Key);
                }
            }
            UnityEngine.Object.DestroyImmediate(expected);
            T replacement = target.AddComponent<T>();
            _nodes[key] = replacement;
            if (aliases != null)
                foreach (string alias in aliases)
                    _nodes[alias] = replacement;
            return replacement;
        }

        internal Image Image(string key) { return Get<Image>(key); }
        internal Button Button(string key) { return Get<Button>(key); }
        internal RectTransform Rect(string key) { return Get<RectTransform>(key); }
        internal ScrollRect Scroll(string key) { return Get<ScrollRect>(key); }

        internal Transform Find(string path)
        {
            return Root == null ? null : Root.Find(path);
        }

        internal void SetActive(bool active)
        {
            if (Root != null && Root.gameObject.activeSelf != active)
                Root.gameObject.SetActive(active);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _nodes.Clear();
            if (_ownsInstance && Root != null)
            {
                Root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(Root.gameObject);
            }
            Root = null;
        }

        private static Component Resolve(Transform target, string type)
        {
            switch (type)
            {
                case "RectTransform": return target as RectTransform;
                case "Text": return target.GetComponent<Text>();
                case "Image": return target.GetComponent<Image>();
                case "Button": return target.GetComponent<Button>();
                case "ScrollRect": return target.GetComponent<ScrollRect>();
                case "Slider": return target.GetComponent<Slider>();
                case "CanvasGroup": return target.GetComponent<CanvasGroup>();
                case "HorizontalLayoutGroup": return target.GetComponent<HorizontalLayoutGroup>();
                case "VerticalLayoutGroup": return target.GetComponent<VerticalLayoutGroup>();
                case "GridLayoutGroup": return target.GetComponent<GridLayoutGroup>();
                case "LayoutElement": return target.GetComponent<LayoutElement>();
                default: throw new InvalidOperationException("Unsupported UI binding type: " + type);
            }
        }
    }
}
