using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mortal.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Shared
{
    public static class SystemMenuEntry
    {
        public const string EntryName = "XingShangMod.MenuEntry";

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
            if (existing != null)
            {
                entry = existing.GetComponent<Button>();
            }
            else
            {
                entry = Create(source, openReadonlyInventory);
                ScheduleReposition(panel);
            }
            if (entry == null)
            {
                return null;
            }
            Text[] entryTexts = entry.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < entryTexts.Length; i++)
            {
                entryTexts[i].text = "唐门行市";
            }
            TextMeshProUGUI[] entryTmpTexts = entry.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < entryTmpTexts.Length; i++)
            {
                entryTmpTexts[i].text = "唐门行市";
            }
            entry.onClick = new Button.ButtonClickedEvent();
            if (openReadonlyInventory != null)
            {
                entry.onClick.AddListener(openReadonlyInventory.Invoke);
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

        private static Button Create(Button source, Action openReadonlyInventory)
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
                texts[i].text = "唐门行市";
            }
            TextMeshProUGUI[] tmpTexts = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                tmpTexts[i].text = "唐门行市";
            }

            Button button = clone.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            button.onClick = new Button.ButtonClickedEvent();
            if (openReadonlyInventory != null)
            {
                button.onClick.AddListener(openReadonlyInventory.Invoke);
            }

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

            RectTransform uncap = source.transform.parent.Find("UncapSixStatsEntry") as RectTransform;
            if (uncap != null)
            {
                float distance = Mathf.Max(entryRect.rect.height * entryRect.lossyScale.y, 36f) + 6f;
                entryRect.position = uncap.position + new Vector3(0f, distance, 0f);
                yield break;
            }

            float width = sourceRect.rect.width * sourceRect.lossyScale.x;
            MenuToggleButton toggle = source.GetComponent<MenuToggleButton>();
            GameObject hover = toggle == null ? null : GetField<GameObject>(toggle, "_hover");
            RectTransform hoverRect = hover == null ? null : hover.transform as RectTransform;
            if (hoverRect != null && hoverRect.rect.width > 0f)
            {
                width = hoverRect.rect.width * hoverRect.lossyScale.x;
            }
            entryRect.position = sourceRect.position + new Vector3(width * 0.96f, 0f, 0f);
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
