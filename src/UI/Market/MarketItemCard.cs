using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Trading;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketItemCard : IDisposable
    {
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.10f, 1f);
        private static readonly Color Muted = new Color(0.39f, 0.37f, 0.33f, 1f);

        private readonly UiFactory _ui;
        private readonly Image _background;
        private readonly Image _icon;
        private readonly Image _wasted;
        private readonly Text _name;
        private readonly Text _category;
        private readonly Text _ghostNumber;
        private readonly Text _price;
        private readonly Text _delta;
        private readonly Text _change;
        private readonly Text _positions;
        private readonly Text _quantityText;
        private readonly WeeklyChartGraphic _chart;
        private readonly Button _minusButton;
        private readonly Button _plusButton;
        private readonly Button _detailButton;
        private readonly Button _buyButton;
        private readonly Button _borrowButton;
        private ItemDefinition _item;
        private int _quantity = 1;
        private bool _delisted;

        internal event Action<string> DetailRequested;
        internal event Action<string, TradeAction, int> AddRequested;

        internal MarketItemCard(Transform parent, UiFactory ui)
        {
            _ui = ui;
            _background = ui.CreatePanel(parent, "GoodsCard", "card_goods_normal", null, Color.white);
            Root = _background.rectTransform;

            _chart = WeeklyChartGraphic.Create(Root, WeeklyChartMode.Card, ui.GameFont);
            _chart.color = new Color(1f, 1f, 1f, 0.29f);
            _chart.LineWidth = 2.2f;
            _chart.GridColor = new Color32(55, 46, 36, 22);
            UiFactory.Stretch(_chart.rectTransform, new Vector2(8f, 58f), new Vector2(-8f, -48f));

            _ghostNumber = ui.CreateText(
                Root,
                "GhostNumber",
                string.Empty,
                90,
                new Color(0.25f, 0.23f, 0.19f, 0.10f),
                TextAnchor.UpperRight,
                new Vector2(150f, 100f));
            SetTopRight(_ghostNumber, new Vector2(-12f, -34f), new Vector2(150f, 100f));

            _name = ui.CreateText(
                Root,
                "Name",
                string.Empty,
                25,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(320f, 40f));
            UiFactory.SetTopLeft(_name.rectTransform, new Vector2(18f, -12f), new Vector2(320f, 40f));
            _category = ui.CreateText(
                Root,
                "Category",
                string.Empty,
                14,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(108f, 28f));
            SetTopRight(_category, new Vector2(-14f, -14f), new Vector2(108f, 28f));

            _icon = ui.CreateImage(
                Root,
                "ItemIcon",
                (Sprite)null,
                new Vector2(176f, 176f),
                true);
            UiFactory.SetRect(
                _icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 10f),
                new Vector2(176f, 176f));

            _price = ui.CreateText(
                Root,
                "Price",
                string.Empty,
                43,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(240f, 62f));
            SetMiddleRight(_price, new Vector2(-22f, 50f), new Vector2(240f, 62f));
            _delta = ui.CreateText(
                Root,
                "Delta",
                string.Empty,
                16,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(220f, 26f));
            SetMiddleRight(_delta, new Vector2(-24f, 10f), new Vector2(220f, 26f));
            _change = ui.CreateText(
                Root,
                "Change",
                string.Empty,
                20,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(220f, 30f));
            SetMiddleRight(_change, new Vector2(-24f, -18f), new Vector2(220f, 30f));

            _positions = ui.CreateText(
                Root,
                "Positions",
                string.Empty,
                15,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(320f, 28f));
            UiFactory.SetRect(
                _positions.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(20f, 66f),
                new Vector2(320f, 28f));

            _minusButton = ui.CreateButton(
                Root,
                "Minus",
                "−",
                Decrease,
                25,
                new Vector2(40f, 42f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetBottomLeft(_minusButton, new Vector2(16f, 14f), new Vector2(40f, 42f));
            _quantityText = ui.CreateText(
                Root,
                "Quantity",
                "1",
                19,
                Ink,
                TextAnchor.MiddleCenter,
                new Vector2(48f, 42f));
            SetBottomLeft(_quantityText, new Vector2(58f, 14f), new Vector2(48f, 42f));
            _plusButton = ui.CreateButton(
                Root,
                "Plus",
                "+",
                Increase,
                25,
                new Vector2(40f, 42f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetBottomLeft(_plusButton, new Vector2(108f, 14f), new Vector2(40f, 42f));
            _detailButton = ui.CreateButton(
                Root,
                "Detail",
                "详情",
                OpenDetail,
                17,
                new Vector2(68f, 42f),
                "button_text_action_normal_9s",
                "button_text_action_hover_9s",
                null,
                "button_text_action_disabled_9s");
            SetBottomLeft(_detailButton, new Vector2(156f, 14f), new Vector2(68f, 42f));
            _buyButton = ui.CreateButton(
                Root,
                "Buy",
                "买货",
                AddBuy,
                18,
                new Vector2(74f, 44f),
                "button_trade_red_normal",
                "button_trade_red_hover",
                "button_trade_red_pressed",
                "button_trade_red_disabled");
            SetBottomRight(_buyButton, new Vector2(-172f, 14f), new Vector2(74f, 44f));
            _borrowButton = ui.CreateButton(
                Root,
                "Borrow",
                "借货",
                AddBorrow,
                18,
                new Vector2(74f, 44f),
                "button_trade_green_normal",
                "button_trade_green_hover",
                "button_trade_green_pressed",
                "button_trade_green_disabled");
            SetBottomRight(_borrowButton, new Vector2(-88f, 14f), new Vector2(74f, 44f));

            _wasted = ui.CreateImage(
                Root,
                "DelistedWordmark",
                "wasted_delist_wordmark",
                new Vector2(320f, 164f),
                true);
            _wasted.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            _wasted.gameObject.SetActive(false);
        }

        internal RectTransform Root { get; private set; }

        internal void Bind(ItemDefinition item, Persistence.RunState state)
        {
            if (item == null)
            {
                Root.gameObject.SetActive(false);
                return;
            }
            if (_item == null || !string.Equals(_item.ItemId, item.ItemId, StringComparison.Ordinal))
                _quantity = 1;
            _item = item;

            List<WeeklyPoint> history = MarketView.GetVisibleHistory(state, item.ItemId);
            Holding holding = state.FindHolding(item.ItemId);
            CurrentHolding = holding == null ? 0 : holding.Quantity;
            CurrentShort = MarketView.GetShortQuantity(state, item.ItemId);
            CurrentDelisted = MarketView.IsDelisted(state, item.ItemId);
            _chart.SetPoints(history);
            BindValues(item, history);
        }

        internal void BindValues(ItemDefinition item, List<WeeklyPoint> history)
        {
            _name.text = item.Name;
            _category.text = "行货 · " + item.ItemId;
            _ghostNumber.text = item.ItemId;
            _icon.sprite = _ui.Sprites == null ? null : _ui.Sprites.GetSprite(MarketView.GetIconSpriteId(item));
            WeeklyPoint current = history.Count == 0 ? null : history[history.Count - 1];
            double weeklyReturn = current == null ? 0.0 : current.Return;
            if (current != null && (double.IsNaN(weeklyReturn) || double.IsInfinity(weeklyReturn)) && history.Count > 1)
            {
                WeeklyPoint previous = history[history.Count - 2];
                weeklyReturn = previous.Price == 0.0 ? 0.0 : current.Price / previous.Price - 1.0;
            }
            Color trend = MarketView.GetTrendColor(weeklyReturn);
            _price.text = current == null ? "暂无报价" : MarketView.FormatNumber(current.Price) + " 文";
            _price.fontSize = current == null ? 28 : 43;
            _price.color = trend;
            _delta.text = current == null || history.Count < 2
                ? "本周新开行市"
                : FormatDelta(current.Price - history[history.Count - 2].Price);
            _delta.color = trend;
            _change.text = current == null ? "—" : MarketView.FormatReturn(weeklyReturn);
            _change.color = trend;
            _positions.text = "持货 " + CurrentHolding.ToString(CultureInfo.InvariantCulture) +
                              " · 待还 " + CurrentShort.ToString(CultureInfo.InvariantCulture);
            SetDelisted(CurrentDelisted);
            UpdateQuantity();
        }

        internal int CurrentHolding { get; set; }
        internal long CurrentShort { get; set; }
        internal bool CurrentDelisted { get; set; }

        public void Dispose()
        {
            _minusButton.onClick.RemoveListener(Decrease);
            _plusButton.onClick.RemoveListener(Increase);
            _detailButton.onClick.RemoveListener(OpenDetail);
            _buyButton.onClick.RemoveListener(AddBuy);
            _borrowButton.onClick.RemoveListener(AddBorrow);
            DetailRequested = null;
            AddRequested = null;
            _chart.ClearPoints();
            _item = null;
        }

        private void SetDelisted(bool delisted)
        {
            _delisted = delisted;
            _background.sprite = _ui.Sprites == null
                ? null
                : _ui.Sprites.GetSprite(delisted ? "card_goods_delisted" : "card_goods_normal");
            _background.color = delisted ? new Color(0.72f, 0.74f, 0.74f, 1f) : Color.white;
            _icon.color = delisted ? new Color(0.45f, 0.47f, 0.45f, 0.72f) : Color.white;
            _chart.color = delisted ? new Color(0.45f, 0.46f, 0.44f, 0.18f) : new Color(1f, 1f, 1f, 0.29f);
            _name.color = delisted ? Muted : Ink;
            _category.color = delisted ? Muted : Ink;
            _positions.color = Muted;
            if (delisted)
            {
                _price.color = Muted;
                _delta.color = Muted;
                _change.color = Muted;
            }
            _wasted.gameObject.SetActive(delisted);
            _minusButton.interactable = !delisted && _quantity > 1;
            _plusButton.interactable = !delisted;
            _detailButton.interactable = !delisted;
            _buyButton.interactable = !delisted;
            _borrowButton.interactable = !delisted;
        }

        private void Decrease()
        {
            if (_delisted)
                return;
            if (_quantity > 1)
                _quantity--;
            UpdateQuantity();
        }

        private void Increase()
        {
            if (_delisted)
                return;
            if (_quantity < int.MaxValue)
                _quantity++;
            UpdateQuantity();
        }

        private void UpdateQuantity()
        {
            _quantityText.text = _quantity.ToString(CultureInfo.InvariantCulture);
            _minusButton.interactable = !_delisted && _quantity > 1;
            _plusButton.interactable = !_delisted;
        }

        private void OpenDetail()
        {
            if (_item != null && DetailRequested != null)
                DetailRequested(_item.ItemId);
        }

        private void AddBuy()
        {
            Add(TradeAction.Buy);
        }

        private void AddBorrow()
        {
            Add(TradeAction.Borrow);
        }

        private void Add(TradeAction action)
        {
            if (_item != null && !_delisted && AddRequested != null)
                AddRequested(_item.ItemId, action, _quantity);
        }

        private static string FormatDelta(double value)
        {
            string sign = value > 0.0 ? "+" : string.Empty;
            return sign + MarketView.FormatNumber(value) + " 文";
        }

        private static void SetTopRight(Component component, Vector2 position, Vector2 size)
        {
            UiFactory.SetRect(
                (RectTransform)component.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                position,
                size);
        }

        private static void SetMiddleRight(Component component, Vector2 position, Vector2 size)
        {
            UiFactory.SetRect(
                (RectTransform)component.transform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                position,
                size);
        }

        private static void SetBottomLeft(Component component, Vector2 position, Vector2 size)
        {
            UiFactory.SetRect(
                (RectTransform)component.transform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                position,
                size);
        }

        private static void SetBottomRight(Component component, Vector2 position, Vector2 size)
        {
            UiFactory.SetRect(
                (RectTransform)component.transform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                position,
                size);
        }

    }
}
