using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Shared
{
    public enum NavigationTab
    {
        None,
        Market,
        Inventory,
        Assets
    }

    public sealed class ButtonNavigation : MonoBehaviour
    {
        private readonly List<Button> _buttons = new List<Button>();
        private UiFactory _factory;
        private NavigationTab _current;

        public RectTransform Root { get; private set; }
        public Button MarketButton { get; private set; }
        public Button InventoryButton { get; private set; }
        public Button AssetsButton { get; private set; }
        public Button CloseButton { get; private set; }
        public bool AllowsMarketNavigation { get { return MarketButton != null; } }

        public static ButtonNavigation CreateMarket(
            UiFactory factory,
            Transform parent,
            Action openMarket,
            Action openInventory,
            Action openAssets,
            Action close)
        {
            ButtonNavigation navigation = CreateRoot(factory, parent, true);
            navigation.MarketButton = navigation.AddButton("Market", "行市", "icon_trade", openMarket);
            navigation.InventoryButton = navigation.AddButton("Inventory", "貨囊", "icon_bag", openInventory);
            navigation.AssetsButton = navigation.AddButton("Assets", "資產", "icon_chart", openAssets);
            navigation.CloseButton = navigation.AddButton("Close", "離開", "icon_close", close);
            navigation.LayoutButtons();
            return navigation;
        }

        public static ButtonNavigation CreateReadonly(
            UiFactory factory,
            Transform parent,
            Action openInventory,
            Action openAssets,
            Action close)
        {
            ButtonNavigation navigation = CreateRoot(factory, parent, false);
            navigation.InventoryButton = navigation.AddButton("Inventory", "貨囊", "icon_bag", openInventory);
            navigation.AssetsButton = navigation.AddButton("Assets", "資產", "icon_chart", openAssets);
            navigation.CloseButton = navigation.AddButton("Close", "離開", "icon_close", close);
            navigation.LayoutButtons();
            return navigation;
        }

        public void SetCurrent(NavigationTab tab)
        {
            _current = tab;
            ApplyState(MarketButton, NavigationTab.Market);
            ApplyState(InventoryButton, NavigationTab.Inventory);
            ApplyState(AssetsButton, NavigationTab.Assets);
        }

        public void SetFont(Font font)
        {
            if (font == null || Root == null)
            {
                return;
            }
            Text[] texts = Root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }

        private static ButtonNavigation CreateRoot(UiFactory factory, Transform parent, bool market)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }
            Image image = factory.CreatePanel(
                parent,
                market ? "MarketNavigation" : "ReadonlyNavigation",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            image.raycastTarget = false;
            ButtonNavigation navigation = image.gameObject.AddComponent<ButtonNavigation>();
            navigation.Root = image.rectTransform;
            navigation._factory = factory;
            return navigation;
        }

        private Button AddButton(string name, string label, string icon, Action callback)
        {
            Button button = _factory.CreateButton(
                Root,
                name + "Button",
                label,
                callback == null ? null : new UnityEngine.Events.UnityAction(callback.Invoke),
                20,
                new Vector2(142f, 62f),
                "button_text_action_normal_9s",
                "button_text_action_hover_9s",
                null,
                "button_text_action_disabled_9s");
            Image image = _factory.CreateImage(
                button.transform,
                "Icon",
                icon,
                new Vector2(32f, 32f),
                true);
            UiFactory.SetRect(
                image.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(12f, 0f),
                new Vector2(32f, 32f));
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                UiFactory.Stretch(text.rectTransform, new Vector2(42f, 5f), new Vector2(-8f, -5f));
            }
            _buttons.Add(button);
            return button;
        }

        private void LayoutButtons()
        {
            float width = Mathf.Max(1f, _buttons.Count * 150f - 8f);
            Root.sizeDelta = new Vector2(width, 62f);
            UiFactory.SetRect(
                Root,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -116f),
                new Vector2(width, 62f));
            for (int i = 0; i < _buttons.Count; i++)
            {
                UiFactory.SetTopLeft(
                    _buttons[i].GetComponent<RectTransform>(),
                    new Vector2(i * 150f, 0f),
                    new Vector2(142f, 62f));
            }
        }

        private void ApplyState(Button button, NavigationTab tab)
        {
            if (button == null)
            {
                return;
            }
            bool selected = _current == tab;
            if (selected)
            {
                _factory.SetButtonSprites(
                    button,
                    "button_checkout_red_normal_9s",
                    "button_checkout_red_hover_9s",
                    "button_checkout_red_pressed_9s",
                    "button_checkout_red_disabled_9s");
            }
            else
            {
                _factory.SetButtonSprites(
                    button,
                    "button_text_action_normal_9s",
                    "button_text_action_hover_9s",
                    null,
                    "button_text_action_disabled_9s");
            }
        }
    }
}
