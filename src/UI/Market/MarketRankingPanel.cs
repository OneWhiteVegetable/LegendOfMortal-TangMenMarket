using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketRankingPanel : IDisposable
    {
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.10f, 1f);
        private static readonly Color Muted = new Color(0.40f, 0.38f, 0.34f, 1f);

        private readonly UiFactory _ui;
        private readonly RunState _state;
        private readonly ItemCatalog _items;
        private readonly RectTransform _content;
        private readonly Text _emptyText;
        private readonly Button _backButton;
        private readonly Button _sortButton;
        private readonly List<RankingRow> _rows = new List<RankingRow>();
        private bool _sortAscending;

        internal event Action BackRequested;
        internal event Action<string> DetailRequested;

        internal MarketRankingPanel(Transform parent, UiFactory ui, RunState state, ItemCatalog items)
        {
            _ui = ui;
            _state = state;
            _items = items;
            Image root = ui.CreatePanel(
                parent,
                "RankingPanel",
                "panel_merchant_window_9s",
                null,
                Color.white);
            Root = root.rectTransform;

            _backButton = ui.CreateButton(
                Root,
                "Back",
                "‹",
                Back,
                34,
                new Vector2(50f, 48f),
                "button_brush_arrow_normal",
                "button_brush_arrow_hover",
                "button_brush_arrow_pressed",
                "button_brush_arrow_disabled");
            UiFactory.SetTopLeft(_backButton.GetComponent<RectTransform>(), new Vector2(10f, -8f), new Vector2(50f, 48f));
            Text title = ui.CreateText(
                Root,
                "Title",
                "货物涨跌榜",
                28,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(280f, 48f));
            UiFactory.SetTopLeft(title.rectTransform, new Vector2(74f, -8f), new Vector2(280f, 48f));
            Text period = ui.CreateText(
                Root,
                "Period",
                "本周涨跌 · 仅列已出现商品",
                15,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(320f, 32f));
            UiFactory.SetTopLeft(period.rectTransform, new Vector2(360f, -16f), new Vector2(320f, 32f));
            _sortButton = ui.CreateButton(
                Root,
                "Sort",
                "涨幅优先",
                ToggleSort,
                18,
                new Vector2(174f, 44f),
                "button_sort_normal_9s",
                "button_sort_hover_9s",
                "button_sort_pressed_9s");
            SetTopRight(_sortButton, new Vector2(-16f, -10f), new Vector2(174f, 44f));

            ScrollRect scroll = ui.CreateScrollRect(Root, "RankingScroll", Vector2.zero, out _content);
            UiFactory.Stretch(scroll.GetComponent<RectTransform>(), new Vector2(8f, 8f), new Vector2(-8f, -68f));
            _emptyText = ui.CreateText(
                Root,
                "Empty",
                "尚无已出现商品。",
                23,
                Muted,
                TextAnchor.MiddleCenter,
                new Vector2(480f, 80f));
            _emptyText.gameObject.SetActive(false);
        }

        internal RectTransform Root { get; private set; }

        internal void Refresh()
        {
            ClearRows();
            List<RankingEntry> entries = new List<RankingEntry>();
            List<ItemDefinition> appeared = MarketView.GetAppearedItems(_state, _items);
            for (int i = 0; i < appeared.Count; i++)
            {
                ItemDefinition item = appeared[i];
                List<WeeklyPoint> history = MarketView.GetVisibleHistory(_state, item.ItemId);
                entries.Add(new RankingEntry
                {
                    Item = item,
                    History = history,
                    Current = history.Count == 0 ? null : history[history.Count - 1],
                    Delisted = MarketView.IsDelisted(_state, item.ItemId)
                });
            }
            entries.Sort(CompareEntries);
            _emptyText.gameObject.SetActive(entries.Count == 0);
            _content.sizeDelta = new Vector2(0f, Math.Max(80f, entries.Count * 90f + 8f));
            _content.anchoredPosition = Vector2.zero;

            for (int i = 0; i < entries.Count; i++)
                CreateRow(entries[i], i);
            SetButtonLabel(_sortButton, _sortAscending ? "跌幅优先" : "涨幅优先");
        }

        public void Dispose()
        {
            _backButton.onClick.RemoveListener(Back);
            _sortButton.onClick.RemoveListener(ToggleSort);
            ClearRows();
            BackRequested = null;
            DetailRequested = null;
        }

        private int CompareEntries(RankingEntry left, RankingEntry right)
        {
            double leftReturn = left.Current == null ? double.NegativeInfinity : left.Current.Return;
            double rightReturn = right.Current == null ? double.NegativeInfinity : right.Current.Return;
            if (double.IsNaN(leftReturn))
                leftReturn = double.NegativeInfinity;
            if (double.IsNaN(rightReturn))
                rightReturn = double.NegativeInfinity;
            int comparison = leftReturn.CompareTo(rightReturn);
            if (!_sortAscending)
                comparison = -comparison;
            return comparison != 0
                ? comparison
                : string.Compare(left.Item.ItemId, right.Item.ItemId, StringComparison.Ordinal);
        }

        private void CreateRow(RankingEntry entry, int index)
        {
            Button button = _ui.CreateButton(
                _content,
                "Rank." + entry.Item.ItemId,
                string.Empty,
                null,
                18,
                null,
                "row_ranking_normal_9s",
                "row_ranking_hover_9s");
            RectTransform rect = button.GetComponent<RectTransform>();
            UiFactory.SetRect(
                rect,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -index * 90f - 4f),
                new Vector2(0f, 82f));

            Text rank = _ui.CreateText(
                rect,
                "Rank",
                (index + 1).ToString("00", CultureInfo.InvariantCulture),
                25,
                Ink,
                TextAnchor.MiddleCenter,
                new Vector2(58f, 60f));
            UiFactory.SetTopLeft(rank.rectTransform, new Vector2(12f, -10f), new Vector2(58f, 60f));
            Image icon = _ui.CreateImage(
                rect,
                "Icon",
                MarketView.GetIconSpriteId(entry.Item),
                new Vector2(62f, 62f),
                true);
            UiFactory.SetTopLeft(icon.rectTransform, new Vector2(78f, -10f), new Vector2(62f, 62f));
            Text name = _ui.CreateText(
                rect,
                "Name",
                entry.Item.Name,
                21,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(300f, 36f));
            UiFactory.SetTopLeft(name.rectTransform, new Vector2(152f, -8f), new Vector2(300f, 36f));
            Text itemId = _ui.CreateText(
                rect,
                "ItemId",
                "行货 · " + entry.Item.ItemId,
                13,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(300f, 28f));
            UiFactory.SetTopLeft(itemId.rectTransform, new Vector2(152f, -42f), new Vector2(300f, 28f));
            Text price = _ui.CreateText(
                rect,
                "Price",
                entry.Current == null ? "—" : MarketView.FormatNumber(entry.Current.Price) + " 文",
                22,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(200f, 56f));
            SetMiddleRight(price, new Vector2(-470f, 0f), new Vector2(200f, 56f));
            double weeklyReturn = entry.Current == null ? 0.0 : entry.Current.Return;
            Text change = _ui.CreateText(
                rect,
                "Change",
                entry.Current == null ? "—" : MarketView.FormatReturn(weeklyReturn),
                21,
                MarketView.GetTrendColor(weeklyReturn),
                TextAnchor.MiddleRight,
                new Vector2(170f, 56f));
            SetMiddleRight(change, new Vector2(-282f, 0f), new Vector2(170f, 56f));

            WeeklyChartGraphic chart = WeeklyChartGraphic.Create(rect, WeeklyChartMode.Card, _ui.GameFont);
            chart.LineWidth = 1.8f;
            chart.HorizontalGridLines = 0;
            chart.SetPoints(entry.History);
            SetMiddleRight(chart, new Vector2(-24f, 0f), new Vector2(220f, 64f));

            Image wasted = _ui.CreateImage(
                rect,
                "DelistedWordmark",
                "wasted_delist_wordmark",
                new Vector2(140f, 72f),
                true);
            wasted.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            wasted.gameObject.SetActive(entry.Delisted);
            if (entry.Delisted)
            {
                button.image.color = new Color(0.65f, 0.67f, 0.66f, 1f);
                icon.color = new Color(0.45f, 0.47f, 0.45f, 0.65f);
                name.color = Muted;
                price.color = Muted;
                change.color = Muted;
                chart.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                button.interactable = false;
            }

            RankingRow row = new RankingRow
            {
                Button = button,
                Chart = chart,
                ItemId = entry.Item.ItemId
            };
            row.ClickAction = delegate { OpenDetail(row.ItemId); };
            button.onClick.AddListener(row.ClickAction);
            _rows.Add(row);
        }

        private void OpenDetail(string itemId)
        {
            if (DetailRequested != null)
                DetailRequested(itemId);
        }

        private void ToggleSort()
        {
            _sortAscending = !_sortAscending;
            Refresh();
        }

        private void Back()
        {
            if (BackRequested != null)
                BackRequested();
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                RankingRow row = _rows[i];
                if (row.Button != null)
                {
                    row.Button.onClick.RemoveListener(row.ClickAction);
                    row.Button.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(row.Button.gameObject);
                }
                if (row.Chart != null)
                    row.Chart.ClearPoints();
            }
            _rows.Clear();
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

        private static void SetButtonLabel(Button button, string value)
        {
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = value;
        }

        private sealed class RankingEntry
        {
            internal ItemDefinition Item;
            internal List<WeeklyPoint> History;
            internal WeeklyPoint Current;
            internal bool Delisted;
        }

        private sealed class RankingRow
        {
            internal Button Button;
            internal WeeklyChartGraphic Chart;
            internal string ItemId;
            internal UnityAction ClickAction;
        }
    }
}
