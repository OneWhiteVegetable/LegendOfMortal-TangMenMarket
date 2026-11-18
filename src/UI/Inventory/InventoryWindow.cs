using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Inventory
{
    public sealed class InventoryWindow
    {
        private readonly UiFactory _ui;
        private readonly RunState _state;
        private readonly CapacityService _capacity;
        private readonly Dictionary<string, ItemDefinition> _definitions;
        private readonly InventoryPage _inventoryPage;
        private readonly AssetsPage _assetsPage;
        private readonly UiTooltip _tooltip;
        private readonly Button _inventoryTab;
        private readonly Button _assetsTab;
        private AssetService _assets;
        private GameObject _previousSelected;
        private bool _showAssets;

        public InventoryWindow(
            Transform parent,
            UiFactory ui,
            RunState state,
            ItemCatalog items,
            ShortService shorts,
            AssetService assets,
            CapacityService capacity)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            if (ui == null)
                throw new ArgumentNullException("ui");
            if (state == null)
                throw new ArgumentNullException("state");
            if (items == null)
                throw new ArgumentNullException("items");
            if (shorts == null)
                throw new ArgumentNullException("shorts");
            if (assets == null)
                throw new ArgumentNullException("assets");
            if (capacity == null)
                throw new ArgumentNullException("capacity");

            _ui = ui;
            _state = state;
            _assets = assets;
            _capacity = capacity;
            _definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < items.Items.Count; i++)
            {
                ItemDefinition item = items.Items[i];
                if (item != null && !_definitions.ContainsKey(item.ItemId))
                    _definitions.Add(item.ItemId, item);
            }

            Image overlay = ui.CreatePanel(
                parent,
                "XingShangMod.InventoryWindow",
                null,
                null,
                new Color(0.025f, 0.022f, 0.018f, 0.88f));
            UiFactory.Stretch(overlay.rectTransform);
            Root = overlay.rectTransform;
            CanvasGroup group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;

            Image window = ui.CreatePanel(
                overlay.transform,
                "PaperWindow",
                "panel_merchant_window_9s");
            UiFactory.Stretch(window.rectTransform, new Vector2(24f, 24f), new Vector2(-24f, -24f));
            window.raycastTarget = true;

            Image header = ui.CreatePanel(
                window.transform,
                "Header",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            UiFactory.SetRect(
                header.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 100f));
            header.raycastTarget = false;

            Image emblem = ui.CreatePanel(
                header.transform,
                "Emblem",
                "panel_brand_block",
                new Vector2(64f, 64f),
                InventoryStyle.Ink);
            UiFactory.SetTopLeft(emblem.rectTransform, new Vector2(30f, -18f), new Vector2(64f, 64f));
            emblem.raycastTarget = false;
            Image bag = ui.CreateImage(
                emblem.transform,
                "Bag",
                "icon_bag",
                new Vector2(38f, 38f),
                true);
            bag.color = InventoryStyle.Paper;
            UiFactory.SetTopLeft(bag.rectTransform, new Vector2(13f, -13f), new Vector2(38f, 38f));

            Text title = ui.CreateText(
                header.transform,
                "Title",
                "行商货囊",
                32,
                InventoryStyle.Ink,
                TextAnchor.MiddleLeft,
                new Vector2(310f, 44f));
            UiFactory.SetTopLeft(title.rectTransform, new Vector2(112f, -14f), new Vector2(310f, 44f));
            Text subtitle = ui.CreateText(
                header.transform,
                "Subtitle",
                "只读账目 · 多空单货共格",
                16,
                InventoryStyle.Muted,
                TextAnchor.MiddleLeft,
                new Vector2(310f, 28f));
            UiFactory.SetTopLeft(subtitle.rectTransform, new Vector2(114f, -57f), new Vector2(310f, 28f));

            _assetsTab = ui.CreateButton(
                header.transform,
                "AssetsTab",
                "资产分析",
                OpenAssets,
                21,
                new Vector2(156f, 58f),
                "tab_range_normal_9s",
                "tab_range_hover_9s",
                "tab_range_active_9s");
            UiFactory.SetRect(
                _assetsTab.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-210f, -22f),
                new Vector2(156f, 58f));

            _inventoryTab = ui.CreateButton(
                header.transform,
                "InventoryTab",
                "货囊",
                OpenInventory,
                21,
                new Vector2(142f, 58f),
                "tab_range_active_9s",
                "tab_range_hover_9s",
                "tab_range_active_9s");
            UiFactory.SetRect(
                _inventoryTab.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-370f, -22f),
                new Vector2(142f, 58f));

            Button close = ui.CreateButton(
                header.transform,
                "CloseButton",
                "关闭",
                Hide,
                21,
                new Vector2(104f, 58f),
                "button_modal_neutral_normal_9s",
                "button_modal_neutral_hover_9s",
                "button_modal_neutral_pressed_9s");
            UiFactory.SetRect(
                close.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-82f, -22f),
                new Vector2(104f, 58f));

            RectTransform content = UiFactory.CreateRect("Content", window.transform);
            UiFactory.Stretch(content, new Vector2(30f, 24f), new Vector2(-30f, -108f));
            _tooltip = UiTooltip.Create(ui, overlay.transform);
            _tooltip.MaxWidth = 480f;
            _inventoryPage = new InventoryPage(ui, content, state, items, shorts, _tooltip);
            _assetsPage = new AssetsPage(ui, content, state, _tooltip);

            InventoryWindowInput input = overlay.gameObject.AddComponent<InventoryWindowInput>();
            input.CloseRequested = Hide;
            SelectPage(false);
            overlay.gameObject.SetActive(false);
        }

        public RectTransform Root { get; private set; }

        public bool IsOpen
        {
            get { return Root != null && Root.gameObject.activeSelf; }
        }

        public void Show()
        {
            if (Root == null)
                return;
            if (!IsOpen && EventSystem.current != null)
                _previousSelected = EventSystem.current.currentSelectedGameObject;

            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Refresh();
            SelectCurrentTab();
        }

        public void Hide()
        {
            if (Root == null)
                return;
            bool wasOpen = IsOpen;
            _tooltip.Hide();
            Root.gameObject.SetActive(false);
            if (wasOpen && EventSystem.current != null && _previousSelected != null &&
                _previousSelected.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(_previousSelected);
            }
            _previousSelected = null;
        }

        public void Refresh()
        {
            AssetSnapshot assetSnapshot = null;
            CapacitySnapshot capacitySnapshot = null;
            string notice = string.Empty;

            if (_assets == null)
            {
                try
                {
                    _assets = new AssetService(_state, new MortalWallet(), _definitions);
                }
                catch (InvalidOperationException)
                {
                    notice = "原版银两数据尚未就绪，资产与现价暂不可读。";
                }
            }
            if (_assets != null)
                assetSnapshot = _assets.GetSnapshot();

            try
            {
                capacitySnapshot = _capacity.Evaluate(_state, _definitions);
            }
            catch (InvalidOperationException)
            {
                notice = string.IsNullOrEmpty(notice)
                    ? "原版角色属性尚未就绪，货囊容量暂不可读。"
                    : notice + " 角色属性亦未就绪。";
            }

            _inventoryPage.BindAssets(_assets);
            _inventoryPage.Refresh(capacitySnapshot, assetSnapshot, notice);
            _assetsPage.Refresh(assetSnapshot, notice);
            if (IsOpen && EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy)
                    SelectCurrentTab();
            }
        }

        private void OpenInventory()
        {
            SelectPage(false);
            Refresh();
        }

        private void OpenAssets()
        {
            SelectPage(true);
            Refresh();
        }

        private void SelectPage(bool assets)
        {
            _showAssets = assets;
            _inventoryPage.SetActive(!assets);
            _assetsPage.SetActive(assets);
            _tooltip.Hide();
            SetTabSprite(_inventoryTab, !assets);
            SetTabSprite(_assetsTab, assets);
        }

        private void SetTabSprite(Button button, bool active)
        {
            _ui.SetButtonSprites(
                button,
                active ? "tab_range_active_9s" : "tab_range_normal_9s",
                "tab_range_hover_9s",
                active ? "tab_range_active_9s" : "tab_range_normal_9s");
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.color = active ? InventoryStyle.Ink : InventoryStyle.Muted;
        }

        private void SelectCurrentTab()
        {
            Button tab = _showAssets ? _assetsTab : _inventoryTab;
            if (EventSystem.current != null && tab.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(tab.gameObject);
        }
    }

    internal sealed class InventoryWindowInput : MonoBehaviour
    {
        public Action CloseRequested;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel"))
            {
                Action callback = CloseRequested;
                if (callback != null)
                    callback();
            }
        }
    }

    internal static class InventoryStyle
    {
        internal static readonly Color Ink = new Color(0.13f, 0.12f, 0.10f, 1f);
        internal static readonly Color Muted = new Color(0.37f, 0.36f, 0.31f, 1f);
        internal static readonly Color Paper = new Color(0.94f, 0.90f, 0.81f, 1f);
        internal static readonly Color Red = new Color(0.67f, 0.20f, 0.16f, 1f);
        internal static readonly Color Green = new Color(0.18f, 0.38f, 0.29f, 1f);
        internal static readonly Color Gold = new Color(0.66f, 0.49f, 0.22f, 1f);

        internal static Color Profit(double value)
        {
            return value > 0.0 ? Red : value < 0.0 ? Green : Ink;
        }

        internal static string Amount(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        internal static string Amount(double value)
        {
            return IsFinite(value) ? value.ToString("N0", CultureInfo.InvariantCulture) : "--";
        }

        internal static string SignedAmount(double value)
        {
            if (!IsFinite(value))
                return "--";
            return (value > 0.0 ? "+" : string.Empty) + Amount(value);
        }

        internal static string Percent(double value)
        {
            if (!IsFinite(value))
                return "--";
            return (value > 0.0 ? "+" : string.Empty) + value.ToString("0.00%", CultureInfo.InvariantCulture);
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
