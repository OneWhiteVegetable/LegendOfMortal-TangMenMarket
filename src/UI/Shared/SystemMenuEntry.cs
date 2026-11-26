using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mortal.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XingShangMod.UI.Shared
{
    public static class SystemMenuEntry
    {
        public const string EntryName = "XingShangMod.MenuEntry";
        public const string EntryLabel = "唐门行市";

        public static Button EnsureCreated(
            MenuPanel panel,
            Action openReadonlyInventory,
            Action closeAll,
            Action<Font> fontReceived)
        {
            if (panel == null)
            {
                throw new ArgumentNullException("panel");
            }
            Button source = GetField<Button>(panel, "_systemSettingButton");
            if (source == null || source.transform.parent == null)
            {
                return null;
            }

            Transform existing = source.transform.parent.Find(EntryName);
            Button entry;
            bool created = existing == null;
            if (existing != null)
            {
                entry = existing.GetComponent<Button>();
            }
            else
            {
                entry = Create(source);
                ScheduleReposition(panel);
            }
            if (entry == null)
                return null;
            Text[] entryTexts = entry.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < entryTexts.Length; i++)
            {
                entryTexts[i].text = EntryLabel;
            }
            TextMeshProUGUI[] entryTmpTexts = entry.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < entryTmpTexts.Length; i++)
            {
                entryTmpTexts[i].text = EntryLabel;
            }

            SystemMenuEntryBinding binding = entry.GetComponent<SystemMenuEntryBinding>();
            if (binding == null)
            {
                entry.onClick = new Button.ButtonClickedEvent();
                binding = entry.gameObject.AddComponent<SystemMenuEntryBinding>();
            }
            binding.Configure(entry, openReadonlyInventory);
            MenuToggleButton entryToggle = entry.GetComponent<MenuToggleButton>();
            if (entryToggle != null)
            {
                entryToggle.SetActive(true);
                if (created)
                    entryToggle.Init();
                NormalizeHoverFont(entryToggle);
            }
            SystemMenuLinkage linkage = panel.GetComponent<SystemMenuLinkage>();
            if (linkage == null)
            {
                linkage = panel.gameObject.AddComponent<SystemMenuLinkage>();
            }
            linkage.Configure(panel, entry.gameObject, closeAll);

            Text sourceText = source.GetComponentInChildren<Text>(true);
            if (sourceText != null && sourceText.font != null && fontReceived != null)
            {
                fontReceived(sourceText.font);
            }
            return entry;
        }

        private static void NormalizeHoverFont(MenuToggleButton toggle)
        {
            GameObject normal = GetField<GameObject>(toggle, "_normal");
            GameObject hover = GetField<GameObject>(toggle, "_hover");
            if (normal == null || hover == null)
                return;

            Text normalText = null;
            Text[] normalTexts = normal.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < normalTexts.Length; i++)
            {
                if (normalTexts[i].font != null)
                {
                    normalText = normalTexts[i];
                    break;
                }
            }
            if (normalText != null)
            {
                Text[] hoverTexts = hover.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < hoverTexts.Length; i++)
                {
                    hoverTexts[i].font = normalText.font;
                    hoverTexts[i].material = normalText.material;
                }
            }

            TextMeshProUGUI normalTmp = null;
            TextMeshProUGUI[] normalTmpTexts = normal.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < normalTmpTexts.Length; i++)
            {
                if (normalTmpTexts[i].font != null)
                {
                    normalTmp = normalTmpTexts[i];
                    break;
                }
            }
            if (normalTmp != null)
            {
                TextMeshProUGUI[] hoverTmpTexts = hover.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < hoverTmpTexts.Length; i++)
                {
                    hoverTmpTexts[i].font = normalTmp.font;
                    hoverTmpTexts[i].fontSharedMaterial = normalTmp.fontSharedMaterial;
                }
            }
        }

        public static void ScheduleReposition(MenuPanel panel)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy)
            {
                return;
            }
            try
            {
                panel.StartCoroutine(RepositionLater(panel));
            }
            catch (Exception)
            {
            }
        }

        private static Button Create(Button source)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
            clone.name = EntryName;
            Component[] components = clone.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform)
                {
                    continue;
                }
                string componentNamespace = component.GetType().Namespace ?? string.Empty;
                if (componentNamespace.StartsWith("Lean", StringComparison.Ordinal))
                {
                    UnityEngine.Object.Destroy(component);
                }
            }

            Text[] texts = clone.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].text = EntryLabel;
            }
            TextMeshProUGUI[] tmpTexts = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                tmpTexts[i].text = EntryLabel;
            }

            Button button = clone.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            button.onClick = new Button.ButtonClickedEvent();

            LayoutElement layout = clone.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = clone.AddComponent<LayoutElement>();
            }
            layout.ignoreLayout = true;
            RectTransform sourceRect = source.transform as RectTransform;
            RectTransform cloneRect = clone.transform as RectTransform;
            if (sourceRect != null && cloneRect != null)
            {
                cloneRect.anchorMin = sourceRect.anchorMin;
                cloneRect.anchorMax = sourceRect.anchorMax;
                cloneRect.pivot = sourceRect.pivot;
                cloneRect.sizeDelta = sourceRect.sizeDelta;
                cloneRect.localScale = sourceRect.localScale;
                cloneRect.anchoredPosition = sourceRect.anchoredPosition;
                cloneRect.position = sourceRect.position;
            }
            return button;
        }

        private static IEnumerator RepositionLater(MenuPanel panel)
        {
            yield return null;
            yield return null;
            if (panel == null)
            {
                yield break;
            }
            Button source = GetField<Button>(panel, "_systemSettingButton");
            if (source == null || source.transform.parent == null)
            {
                yield break;
            }
            Transform entry = source.transform.parent.Find(EntryName);
            RectTransform entryRect = entry as RectTransform;
            RectTransform sourceRect = source.transform as RectTransform;
            if (entryRect == null || sourceRect == null)
            {
                yield break;
            }

            entryRect.localScale = sourceRect.localScale;
            RectTransform loadRect = GetField<Button>(panel, "_loadButton")?.transform as RectTransform;
            RectTransform uncap = source.transform.parent.Find("UncapSixStatsEntry") as RectTransform;
            float x = sourceRect.position.x;
            float y = loadRect == null ? sourceRect.position.y : loadRect.position.y;
            if (uncap != null)
            {
                x = uncap.position.x;
            }
            else
            {
                float width = sourceRect.rect.width * sourceRect.lossyScale.x;
                MenuToggleButton toggle = source.GetComponent<MenuToggleButton>();
                GameObject hover = toggle == null ? null : GetField<GameObject>(toggle, "_hover");
                RectTransform hoverRect = hover == null ? null : hover.transform as RectTransform;
                if (hoverRect != null && hoverRect.rect.width > 0f)
                {
                    width = hoverRect.rect.width * hoverRect.lossyScale.x;
                }
                x += width;
            }
            entryRect.position = new Vector3(x, y, entryRect.position.z);
        }

        internal static T GetField<T>(object target, string name) where T : class
        {
            if (target == null)
            {
                return null;
            }
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target) as T;
        }
    }

    internal sealed class SystemMenuEntryBinding : MonoBehaviour
    {
        private Button _button;
        private Action _callback;
        private UnityAction _handler;
        private bool _registered;
        private int _lastClickFrame = -1;

        internal void Configure(Button button, Action callback)
        {
            if (_handler == null)
                _handler = HandleClick;
            if (!ReferenceEquals(_button, button))
            {
                RemoveListener();
                _button = button;
            }
            if (_button != null)
            {
                _button.enabled = true;
                _button.interactable = true;
                if (!_registered)
                {
                    _button.onClick.AddListener(_handler);
                    _registered = true;
                }
            }
            _callback = callback;
        }

        private void HandleClick()
        {
            if (_callback == null || _lastClickFrame == Time.frameCount)
                return;
            _lastClickFrame = Time.frameCount;
            _callback();
        }

        private void RemoveListener()
        {
            if (_registered && _button != null)
                _button.onClick.RemoveListener(_handler);
            _registered = false;
        }

        private void OnDestroy()
        {
            RemoveListener();
            _button = null;
            _callback = null;
            _handler = null;
        }
    }

    internal sealed class SystemMenuLinkage : MonoBehaviour
    {
        private static readonly string[] InnerPanelFields = { "_systemSettingsPanel", "_savePanel", "_loadPanel" };
        private readonly List<Component> _innerPanels = new List<Component>();
        private MenuPanel _panel;
        private CanvasGroup _group;
        private GameObject _entry;
        private Action _closeAll;
        private bool _configured;
        private bool _wasOpen;
        private bool _innerOpen;

        internal void Configure(MenuPanel panel, GameObject entry, Action closeAll)
        {
            _panel = panel;
            _entry = entry;
            _closeAll = closeAll;
            _group = panel.GetComponent<CanvasGroup>();
            if (!_configured)
            {
                _innerPanels.Clear();
                for (int i = 0; i < InnerPanelFields.Length; i++)
                {
                    Component inner = SystemMenuEntry.GetField<Component>(panel, InnerPanelFields[i]);
                    if (inner != null)
                    {
                        _innerPanels.Add(inner);
                    }
                }
                _configured = true;
            }
        }

        private void Update()
        {
            if (_panel == null || _entry == null)
            {
                return;
            }
            bool menuOpen = _panel.gameObject.activeInHierarchy && (_group == null || _group.alpha > 0.01f);
            if (menuOpen && !_wasOpen)
            {
                MenuToggleButton toggle = _entry.GetComponent<MenuToggleButton>();
                if (toggle != null)
                {
                    toggle.SetActive(true);
                    toggle.Init();
                }
                SystemMenuEntry.ScheduleReposition(_panel);
            }
            bool innerOpen = false;
            for (int i = 0; i < _innerPanels.Count; i++)
            {
                Component panel = _innerPanels[i];
                if (panel != null && panel.gameObject.activeSelf)
                {
                    innerOpen = true;
                    break;
                }
            }
            if (_entry.activeSelf == innerOpen)
            {
                _entry.SetActive(!innerOpen);
            }
            if ((_wasOpen && !menuOpen) || (!_innerOpen && innerOpen))
            {
                Action callback = _closeAll;
                if (callback != null)
                {
                    callback();
                }
            }
            _wasOpen = menuOpen;
            _innerOpen = innerOpen;
        }

        private void OnDisable()
        {
            if (_wasOpen)
            {
                Action callback = _closeAll;
                if (callback != null)
                {
                    callback();
                }
            }
            _wasOpen = false;
            _innerOpen = false;
        }

        private void OnDestroy()
        {
            _closeAll = null;
            _entry = null;
            _panel = null;
            _innerPanels.Clear();
        }
    }
}
