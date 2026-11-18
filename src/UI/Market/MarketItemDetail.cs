using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketItemDetail : IDisposable
    {
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.10f, 1f);
        private static readonly Color Muted = new Color(0.38f, 0.35f, 0.30f, 1f);

        private readonly RunState _state;
        private readonly UiFactory _ui;
        private readonly Image _icon;
        private readonly Image _wasted;
        private readonly Text _title;
        private readonly Text _itemNumber;
        private readonly Text _price;
        private readonly Text _change;
        private readonly Text _historyCaption;
        private readonly Text _description;
        private readonly Text _news;
        private readonly Text _positions;
        private readonly Text _quantityText;
        private readonly WeeklyChartGraphic _chart;
        private readonly Button _backButton;
        private readonly Button _minusButton;
        private readonly Button _plusButton;
        private readonly Button _buyButton;
        private readonly Button _borrowButton;
        private readonly Button _sellButton;
        private readonly Button _returnButton;
        private ItemDefinition _item;
        private int _quantity = 1;
        private bool _delisted;

        internal event Action BackRequested;
        internal event Action<string, TradeAction, int> AddRequested;

        internal MarketItemDetail(Transform parent, UiFactory ui, RunState state)
        {
            _ui = ui;
            _state = state;
            Image root = ui.CreatePanel(
                parent,
                "ItemDetail",
                "panel_merchant_window_9s",
                null,
                Color.white);
            Root = root.rectTransform;

            Image workspace = ui.CreatePanel(
                Root,
                "ChartWorkspace",
                "panel_detail_chart_9s",
                null,
                Color.white);
            UiFactory.Stretch(workspace.rectTransform, Vector2.zero, new Vector2(-556f, 0f));

            _backButton = ui.CreateButton(
                workspace.transform,
                "Back",
                "‹",
                Back,
                36,
                new Vector2(52f, 52f),
                "button_brush_arrow_normal",
                "button_brush_arrow_hover",
                "button_brush_arrow_pressed",
                "button_brush_arrow_disabled");
            UiFactory.SetTopLeft(_backButton.GetComponent<RectTransform>(), new Vector2(18f, -16f), new Vector2(52f, 52f));
            _itemNumber = ui.CreateText(
                workspace.transform,
                "ItemNumber",
                string.Empty,
                13,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(360f, 22f));
            UiFactory.SetTopLeft(_itemNumber.rectTransform, new Vector2(86f, -16f), new Vector2(360f, 22f));
            _title = ui.CreateText(
                workspace.transform,
                "Title",
                string.Empty,
                35,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(660f, 52f));
            UiFactory.SetTopLeft(_title.rectTransform, new Vector2(84f, -38f), new Vector2(660f, 52f));
            _price = ui.CreateText(
                workspace.transform,
                "Price",
                string.Empty,
                45,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(300f, 60f));
            SetTopRight(_price, new Vector2(-24f, -12f), new Vector2(300f, 60f));
            _change = ui.CreateText(
                workspace.transform,
                "Change",
                string.Empty,
                19,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(300f, 28f));
            SetTopRight(_change, new Vector2(-24f, -70f), new Vector2(300f, 28f));

            _historyCaption = ui.CreateText(
                workspace.transform,
                "HistoryCaption",
                "逐周墨笔 · 红涨绿跌",
                16,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(520f, 32f));
            UiFactory.SetTopLeft(_historyCaption.rectTransform, new Vector2(28f, -98f), new Vector2(520f, 32f));
            _chart = WeeklyChartGraphic.Create(workspace.transform, WeeklyChartMode.Detail, ui.GameFont);
            _chart.LineWidth = 4.2f;
            UiFactory.Stretch(_chart.rectTransform, new Vector2(20f, 46f), new Vector2(-20f, -132f));
            Text chartLegend = ui.CreateText(
                workspace.transform,
                "ChartLegend",
                "红涨　绿跌 · 尺线仅读取当前周及以前",
                15,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(520f, 30f));
            SetBottomLeft(chartLegend, new Vector2(30f, 12f), new Vector2(520f, 30f));

            Image sidebar = ui.CreatePanel(
                Root,
                "Sidebar",
                "panel_cart_rail_9s",
                null,
                Color.white);
            UiFactory.SetRect(
                sidebar.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(544f, 0f));

            Image stage = ui.CreatePanel(
                sidebar.transform,
                "ItemStage",
                "detail_stage_paper",
                new Vector2(500f, 194f),
                Color.white);
            UiFactory.SetTopLeft(stage.rectTransform, new Vector2(20f, -14f), new Vector2(500f, 194f));
            Image slash = ui.CreateImage(
                stage.transform,
                "GoldSlash",
                "detail_stage_gold_slash",
                new Vector2(220f, 180f),
                true);
            SetTopRight(slash, new Vector2(-16f, -8f), new Vector2(220f, 180f));
            _icon = ui.CreateImage(
                stage.transform,
                "ItemIcon",
                (Sprite)null,
                new Vector2(168f, 168f),
                true);
            _icon.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _wasted = ui.CreateImage(
                stage.transform,
                "DelistedWordmark",
                "wasted_delist_wordmark",
                new Vector2(310f, 150f),
                true);
            _wasted.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            _wasted.gameObject.SetActive(false);

            Text descriptionTitle = ui.CreateText(
                sidebar.transform,
                "DescriptionTitle",
                "货物小传",
                20,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(480f, 32f));
            UiFactory.SetTopLeft(descriptionTitle.rectTransform, new Vector2(30f, -224f), new Vector2(480f, 32f));
            _description = ui.CreateText(
                sidebar.transform,
                "Description",
                string.Empty,
                19,
                Ink,
                TextAnchor.UpperLeft,
                new Vector2(480f, 126f));
            UiFactory.SetTopLeft(_description.rectTransform, new Vector2(30f, -258f), new Vector2(480f, 126f));

            Text newsTitle = ui.CreateText(
                sidebar.transform,
                "NewsTitle",
                "行情异动",
                20,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(480f, 32f));
            UiFactory.SetTopLeft(newsTitle.rectTransform, new Vector2(30f, -398f), new Vector2(480f, 32f));
            _news = ui.CreateText(
                sidebar.transform,
                "News",
                string.Empty,
                18,
                Ink,
                TextAnchor.UpperLeft,
                new Vector2(480f, 136f));
            UiFactory.SetTopLeft(_news.rectTransform, new Vector2(30f, -432f), new Vector2(480f, 136f));

            _positions = ui.CreateText(
                sidebar.transform,
                "Positions",
                string.Empty,
                19,
                Ink,
                TextAnchor.UpperLeft,
                new Vector2(480f, 94f));
            SetBottomLeft(_positions, new Vector2(30f, 208f), new Vector2(480f, 94f));

            Text quantityLabel = ui.CreateText(
                sidebar.transform,
                "QuantityLabel",
                "立契数量",
                18,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(150f, 42f));
            SetBottomLeft(quantityLabel, new Vector2(30f, 154f), new Vector2(150f, 42f));
            _minusButton = ui.CreateButton(
                sidebar.transform,
                "Minus",
                "−",
                Decrease,
                25,
                new Vector2(42f, 42f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetBottomRight(_minusButton, new Vector2(-158f, 154f), new Vector2(42f, 42f));
            _quantityText = ui.CreateText(
                sidebar.transform,
                "Quantity",
                "1",
                20,
                Ink,
                TextAnchor.MiddleCenter,
                new Vector2(58f, 42f));
            SetBottomRight(_quantityText, new Vector2(-114f, 154f), new Vector2(58f, 42f));
            _plusButton = ui.CreateButton(
                sidebar.transform,
                "Plus",
                "+",
                Increase,
                25,
                new Vector2(42f, 42f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetBottomRight(_plusButton, new Vector2(-30f, 154f), new Vector2(42f, 42f));

            _buyButton = ui.CreateButton(
                sidebar.transform,
                "Buy",
                "买货 · 做多",
                AddBuy,
                21,
                new Vector2(232f, 62f),
                "button_contract_red_normal_9s",
                "button_contract_red_hover_9s",
                "button_contract_red_pressed_9s",
                "button_contract_red_disabled_9s");
            SetBottomLeft(_buyButton, new Vector2(30f, 82f), new Vector2(232f, 62f));
            _borrowButton = ui.CreateButton(
                sidebar.transform,
                "Borrow",
                "借货 · 做空",
                AddBorrow,
                21,
                new Vector2(232f, 62f),
                "button_contract_green_normal_9s",
                "button_contract_green_hover_9s",
                "button_contract_green_pressed_9s",
                "button_contract_green_disabled_9s");
            SetBottomRight(_borrowButton, new Vector2(-30f, 82f), new Vector2(232f, 62f));
            _sellButton = ui.CreateButton(
                sidebar.transform,
                "Sell",
                "卖出持货",
                AddSell,
                19,
                new Vector2(232f, 46f),
                "button_text_action_normal_9s",
                "button_text_action_hover_9s",
                null,
                "button_text_action_disabled_9s");
            SetBottomLeft(_sellButton, new Vector2(30f, 24f), new Vector2(232f, 46f));
            _returnButton = ui.CreateButton(
                sidebar.transform,
                "Return",
                "归还借货",
                AddReturn,
                19,
                new Vector2(232f, 46f),
                "button_text_action_normal_9s",
                "button_text_action_hover_9s",
                null,
                "button_text_action_disabled_9s");
            SetBottomRight(_returnButton, new Vector2(-30f, 24f), new Vector2(232f, 46f));
        }

        internal RectTransform Root { get; private set; }

        internal void Bind(ItemDefinition item)
        {
            if (item == null)
                return;
            if (_item == null || !string.Equals(_item.ItemId, item.ItemId, StringComparison.Ordinal))
                _quantity = 1;
            _item = item;
            _delisted = MarketView.IsDelisted(_state, item.ItemId);

            List<WeeklyPoint> history = MarketView.GetVisibleHistory(_state, item.ItemId);
            WeeklyPoint current = history.Count == 0 ? null : history[history.Count - 1];
            double weeklyReturn = current == null ? 0.0 : current.Return;
            Color trend = MarketView.GetTrendColor(weeklyReturn);
            _title.text = item.Name;
            _itemNumber.text = "ITEM / " + item.ItemId + " · " + item.Category;
            _price.text = current == null ? "暂无报价" : MarketView.FormatNumber(current.Price) + " 文";
            _price.fontSize = current == null ? 30 : 45;
            _price.color = _delisted ? Muted : trend;
            _change.text = current == null ? "—" : MarketView.FormatReturn(weeklyReturn);
            _change.color = _delisted ? Muted : trend;
            _historyCaption.text = string.Format(
                CultureInfo.InvariantCulture,
                "逐周墨笔 · 可见历史 {0} 周 · 截至市场周 {1}",
                history.Count,
                MarketView.GetCurrentWeek(_state));
            _chart.SetPoints(history);
            _chart.color = _delisted ? new Color(0.55f, 0.56f, 0.54f, 0.65f) : Color.white;
            _icon.sprite = _ui.Sprites == null ? null : _ui.Sprites.GetSprite(MarketView.GetIconSpriteId(item));
            _icon.color = _delisted ? new Color(0.45f, 0.47f, 0.45f, 0.72f) : Color.white;
            _wasted.gameObject.SetActive(_delisted);
            _description.text = item.Description;

            NewsRecord news = MarketView.GetLatestNews(_state, item.ItemId);
            _news.text = news == null
                ? "暂无异动记录。"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "市场周 {0} · {1} {2}\n{3}",
                    news.MarketWeekIndex,
                    news.Direction >= 0 ? "利多" : "利空",
                    MarketView.FormatReturn(news.Return),
                    news.Text);

            Holding holding = _state.FindHolding(item.ItemId);
            int held = holding == null ? 0 : holding.Quantity;
            long borrowed = MarketView.GetShortQuantity(_state, item.ItemId);
            int dueWeek = MarketView.GetNearestDueWeek(_state, item.ItemId);
            string due = borrowed <= 0
                ? "无待还"
                : _delisted
                    ? "已退市，无法平仓"
                    : dueWeek < 0
                        ? "期待还"
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            "最近到期 {0} 周后",
                            Math.Max(0, dueWeek - MarketView.GetCurrentWeek(_state)));
            string averageCost = holding == null || holding.Quantity <= 0
                ? "—"
                : MarketView.FormatNumber((double)holding.TotalCost / holding.Quantity) + " 文";
            _positions.text = string.Format(
                CultureInfo.InvariantCulture,
                "持货：{0} 件 · 均价 {1}\n待还：{2} 件 · {3}",
                held,
                averageCost,
                borrowed,
                due);

            _buyButton.interactable = !_delisted;
            _borrowButton.interactable = !_delisted;
            _sellButton.interactable = !_delisted && held > 0;
            _returnButton.interactable = !_delisted && borrowed > 0;
            UpdateQuantity();
        }

        public void Dispose()
        {
            _backButton.onClick.RemoveListener(Back);
            _minusButton.onClick.RemoveListener(Decrease);
            _plusButton.onClick.RemoveListener(Increase);
            _buyButton.onClick.RemoveListener(AddBuy);
            _borrowButton.onClick.RemoveListener(AddBorrow);
            _sellButton.onClick.RemoveListener(AddSell);
            _returnButton.onClick.RemoveListener(AddReturn);
            BackRequested = null;
            AddRequested = null;
            _chart.ClearPoints();
            _item = null;
        }

        private void Decrease()
        {
            if (!_delisted && _quantity > 1)
                _quantity--;
            UpdateQuantity();
        }

        private void Increase()
        {
            if (!_delisted && _quantity < int.MaxValue)
                _quantity++;
            UpdateQuantity();
        }

        private void UpdateQuantity()
        {
            _quantityText.text = _quantity.ToString(CultureInfo.InvariantCulture);
            _minusButton.interactable = !_delisted && _quantity > 1;
            _plusButton.interactable = !_delisted;
        }

        private void Back()
        {
            if (BackRequested != null)
                BackRequested();
        }

        private void AddBuy()
        {
            Add(TradeAction.Buy);
        }

        private void AddBorrow()
        {
            Add(TradeAction.Borrow);
        }

        private void AddSell()
        {
            Add(TradeAction.Sell);
        }

        private void AddReturn()
        {
            Add(TradeAction.Return);
        }

        private void Add(TradeAction action)
        {
            if (_item != null && !_delisted && AddRequested != null)
                AddRequested(_item.ItemId, action, _quantity);
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
