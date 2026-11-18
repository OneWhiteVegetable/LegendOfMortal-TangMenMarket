using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Inventory
{
    internal sealed class AssetsPage
    {
        private readonly RunState _state;
        private readonly WeeklyChartGraphic _chart;
        private readonly Text _chartStatus;
        private readonly Text _empty;
        private readonly Text _footnote;
        private readonly Text _cash;
        private readonly Text _longValue;
        private readonly Text _shortLiability;
        private readonly Text _debt;
        private readonly Text _netAssets;
        private readonly Text _realized;
        private readonly Text _floating;
        private readonly Text _total;
        private readonly Text _returnRate;

        internal AssetsPage(UiFactory ui, Transform parent, RunState state, UiTooltip tooltip)
        {
            _state = state;
            Root = UiFactory.CreateRect("AssetsPage", parent);
            UiFactory.Stretch(Root);

            RectTransform metrics = UiFactory.CreateRect("AssetMetrics", Root);
            UiFactory.SetRect(
                metrics,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 172f));
            GridLayoutGroup grid = metrics.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.spacing = new Vector2(8f, 8f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            InventoryGridLayout layout = metrics.gameObject.AddComponent<InventoryGridLayout>();
            layout.NarrowColumns = 5;
            layout.WideColumns = 5;
            layout.RowHeight = 78f;
            layout.HorizontalSpacing = 8f;

            _cash = CreateMetric(ui, metrics, tooltip, "现金", "原版银两余额；本页不会存取现金。");
            _longValue = CreateMetric(ui, metrics, tooltip, "货值", "全部多头持仓按当前周或更早的最近可见价格估值。");
            _shortLiability = CreateMetric(ui, metrics, tooltip, "空头负债", "全部未还借货按当前可见价格计算的买回负债。");
            _debt = CreateMetric(ui, metrics, tooltip, "欠款", "到期自动平仓时现金不足形成的 Mod 欠款；后续入账优先偿还。");
            _netAssets = CreateMetric(ui, metrics, tooltip, "净资产", "现金 + 货值 − 空头负债 − 欠款。");
            _realized = CreateMetric(ui, metrics, tooltip, "已实现盈亏", "由已发生交易记录回放得到的已实现盈亏。");
            _floating = CreateMetric(ui, metrics, tooltip, "浮动盈亏", "多头货值减持仓成本，加上空头实收减当前空头负债。");
            _total = CreateMetric(ui, metrics, tooltip, "总盈亏", "已实现盈亏 + 浮动盈亏。");
            _returnRate = CreateMetric(ui, metrics, tooltip, "收益率", "总盈亏 ÷ 交易回放所得投入本金；无投入本金时为 0%。");

            Image chartPanel = ui.CreatePanel(
                Root,
                "AssetChartPanel",
                "panel_detail_chart_9s");
            UiFactory.Stretch(chartPanel.rectTransform, new Vector2(0f, 52f), new Vector2(0f, -184f));

            Text chartTitle = ui.CreateText(
                chartPanel.transform,
                "Title",
                "行商净资产留痕",
                22,
                InventoryStyle.Ink,
                TextAnchor.MiddleLeft,
                new Vector2(420f, 38f));
            UiFactory.SetTopLeft(chartTitle.rectTransform, new Vector2(18f, -8f), new Vector2(420f, 38f));
            _chartStatus = ui.CreateText(
                chartPanel.transform,
                "VisibleRange",
                string.Empty,
                15,
                InventoryStyle.Muted,
                TextAnchor.MiddleRight,
                new Vector2(480f, 34f));
            UiFactory.SetRect(
                _chartStatus.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-18f, -10f),
                new Vector2(480f, 34f));

            _chart = WeeklyChartGraphic.Create(chartPanel.transform, WeeklyChartMode.Assets, ui.GameFont);
            UiFactory.Stretch(_chart.rectTransform, new Vector2(12f, 12f), new Vector2(-12f, -50f));
            _chart.InteractionEnabled = true;
            _chart.ShowLatestLabel = true;
            _chart.ShowRangeLabels = true;

            _empty = ui.CreateText(
                chartPanel.transform,
                "Empty",
                "尚无已发生的资产快照",
                24,
                InventoryStyle.Muted,
                TextAnchor.MiddleCenter);
            UiFactory.Stretch(_empty.rectTransform, new Vector2(40f, 40f), new Vector2(-40f, -60f));

            _footnote = ui.CreateText(
                Root,
                "Footnote",
                string.Empty,
                15,
                InventoryStyle.Muted,
                TextAnchor.MiddleLeft);
            _footnote.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetRect(
                _footnote.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 46f));
        }

        internal RectTransform Root { get; private set; }

        internal void SetActive(bool active)
        {
            if (Root.gameObject.activeSelf != active)
                Root.gameObject.SetActive(active);
        }

        internal void Refresh(AssetSnapshot snapshot, string notice)
        {
            if (snapshot == null)
            {
                ClearMetrics();
            }
            else
            {
                SetMetric(_cash, InventoryStyle.Amount(snapshot.Cash) + " 文", InventoryStyle.Ink);
                SetMetric(_longValue, InventoryStyle.Amount(snapshot.LongValue) + " 文", InventoryStyle.Ink);
                SetMetric(_shortLiability, InventoryStyle.Amount(snapshot.ShortLiability) + " 文", InventoryStyle.Green);
                SetMetric(_debt, InventoryStyle.Amount(snapshot.Debt) + " 文", snapshot.Debt > 0 ? InventoryStyle.Red : InventoryStyle.Ink);
                SetMetric(_netAssets, InventoryStyle.Amount(snapshot.NetAssets) + " 文", InventoryStyle.Ink);
                SetMetric(_realized, InventoryStyle.SignedAmount(snapshot.RealizedProfit) + " 文", InventoryStyle.Profit(snapshot.RealizedProfit));
                SetMetric(_floating, InventoryStyle.SignedAmount(snapshot.FloatingProfit) + " 文", InventoryStyle.Profit(snapshot.FloatingProfit));
                SetMetric(_total, InventoryStyle.SignedAmount(snapshot.TotalProfit) + " 文", InventoryStyle.Profit(snapshot.TotalProfit));
                SetMetric(_returnRate, InventoryStyle.Percent(snapshot.ReturnRate), InventoryStyle.Profit(snapshot.ReturnRate));
            }

            List<WeeklyPoint> visible = GetVisibleHistory();
            if (visible.Count == 0)
            {
                _chart.ClearPoints();
                _empty.gameObject.SetActive(true);
            }
            else
            {
                _chart.SetPoints(visible);
                _empty.gameObject.SetActive(false);
            }
            int currentWeek = _state.Clock == null ? -1 : _state.Clock.MarketWeekIndex;
            _chartStatus.text = currentWeek < 0
                ? "市场周未就绪"
                : "仅已发生快照 · " + visible.Count + " 点 · 截至第 " + currentWeek + " 周";

            if (!string.IsNullOrEmpty(notice))
                _footnote.text = notice;
            else if (snapshot == null)
                _footnote.text = "资产暂不可读；本页保持只读，不写入快照。";
            else
            {
                string text = "净资产 = 现金 + 货值 − 空头负债 − 欠款；曲线仅使用当前周及更早的 AssetHistory。";
                if (!snapshot.HasCompletePrices)
                    text += " 部分持仓缺少可见价格，相关估值与浮盈并不完整。";
                if (!snapshot.IsRealizedProfitComplete)
                    text += " 交易记录不完整，已实现与总盈亏仅作部分列示。";
                _footnote.text = text;
            }
        }

        private List<WeeklyPoint> GetVisibleHistory()
        {
            List<WeeklyPoint> points = new List<WeeklyPoint>();
            if (_state.Clock == null || _state.AssetHistory == null)
                return points;

            int currentWeek = _state.Clock.MarketWeekIndex;
            for (int i = 0; i < _state.AssetHistory.Count; i++)
            {
                AssetPoint asset = _state.AssetHistory[i];
                if (asset == null || asset.MarketWeekIndex < 0 || asset.MarketWeekIndex > currentWeek ||
                    !InventoryStyle.IsFinite(asset.NetAssets))
                    continue;

                WeeklyPoint point = new WeeklyPoint
                {
                    ItemId = "Assets",
                    MarketWeekIndex = asset.MarketWeekIndex,
                    GameYear = asset.GameYear,
                    GameMonth = asset.GameMonth,
                    GameRound = asset.GameRound,
                    SourceRecordIndex = -1,
                    Price = asset.NetAssets,
                    Observed = true
                };
                InsertVisiblePoint(points, point);
            }
            return points;
        }

        private static void InsertVisiblePoint(List<WeeklyPoint> points, WeeklyPoint point)
        {
            int index = points.Count;
            while (index > 0 && points[index - 1].MarketWeekIndex > point.MarketWeekIndex)
                index--;
            points.Insert(index, point);
        }

        private void ClearMetrics()
        {
            SetMetric(_cash, "--", InventoryStyle.Muted);
            SetMetric(_longValue, "--", InventoryStyle.Muted);
            SetMetric(_shortLiability, "--", InventoryStyle.Muted);
            SetMetric(_debt, "--", InventoryStyle.Muted);
            SetMetric(_netAssets, "--", InventoryStyle.Muted);
            SetMetric(_realized, "--", InventoryStyle.Muted);
            SetMetric(_floating, "--", InventoryStyle.Muted);
            SetMetric(_total, "--", InventoryStyle.Muted);
            SetMetric(_returnRate, "--", InventoryStyle.Muted);
        }

        private static void SetMetric(Text text, string value, Color color)
        {
            text.text = value;
            text.color = color;
        }

        private static Text CreateMetric(
            UiFactory ui,
            Transform parent,
            UiTooltip tooltip,
            string caption,
            string explanation)
        {
            Image panel = ui.CreatePanel(parent, caption, "card_asset_9s");
            Text label = ui.CreateText(
                panel.transform,
                "Caption",
                caption,
                14,
                InventoryStyle.Muted,
                TextAnchor.MiddleLeft);
            UiFactory.SetRect(
                label.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -7f),
                new Vector2(-26f, 24f));
            Text value = ui.CreateText(
                panel.transform,
                "Value",
                "--",
                23,
                InventoryStyle.Ink,
                TextAnchor.MiddleLeft);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(
                value.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 8f),
                new Vector2(-26f, 36f));
            tooltip.Attach(panel.gameObject, explanation);
            return value;
        }
    }
}
