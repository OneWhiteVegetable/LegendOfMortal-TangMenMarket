using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Inventory
{
    public enum InventoryWindowPage
    {
        Inventory,
        Assets
    }

    internal sealed class InventoryWindow : UiScreen
    {
        private readonly ModApplication _application;
        private readonly RunState _state;
        private readonly Dictionary<string, ItemDefinition> _definitions;
        private readonly InventorySection _longSection;
        private readonly InventorySection _shortSection;
        private readonly AssetsPage _assetsPage;
        private readonly TradeConfirmation _confirmation;
        private readonly WeeklyChartView _indexChart;
        private readonly RectTransform _summary;
        private readonly RectTransform _sections;
        private readonly RectTransform[] _summaryCells;
        private readonly List<InventorySlotModel> _longModels = new List<InventorySlotModel>();
        private readonly List<InventorySlotModel> _shortModels = new List<InventorySlotModel>();
        private bool _interactive;
        private bool _showAssets;
        private bool _compact;
        private int _gridColumns;
        private bool _capacityProjectionExact = true;

        internal InventoryWindow(
            UiFrontendServices services,
            ModApplication application,
            TradeConfirmation confirmation)
            : base(services, "InventoryWindow", UiSorting.Inventory)
        {
            _application = application;
            _state = application.State;
            View.Text("CapacityText").text = "—";
            View.Text("WeightCaptionText").text = "单格负重能力";
            View.Text("WeightText").text = "—";
            _definitions = MarketQuery.BuildDefinitions(application.Items);
            _confirmation = confirmation;
            _summary = (RectTransform)View.Find("Window/ProfilePage/ProfileContent/Summary");
            _sections = (RectTransform)View.Find("Window/ProfilePage/ProfileContent/Sections");
            _summaryCells = new[]
            {
                View.Rect("SummaryGoodsCell"),
                View.Rect("SummaryTotalCell"),
                View.Rect("SummaryWeeklyCell"),
                View.Rect("SummaryLoansCell")
            };
            InventoryPaper.Header(View, services);
            _summaryCells[1].GetChild(0).GetComponent<Text>().text = "总盈亏";
            _summaryCells[2].GetChild(0).GetComponent<Text>().text = "本周盈亏";
            _longSection = new InventorySection(services, View, true);
            _shortSection = new InventorySection(services, View, false);
            _longSection.ViewRequested += OpenPositionDetail;
            _longSection.CloseRequested += ClosePosition;
            _shortSection.ViewRequested += OpenPositionDetail;
            _shortSection.CloseRequested += ClosePosition;
            _assetsPage = new AssetsPage(services, View.Rect("ProfileContent"), _application);
            _indexChart = new WeeklyChartView(
                View.Rect("IndexChartHost"),
                WeeklyChartMode.Spark,
                services.Assets.RegularFont,
                services.Assets.DryBrushTexture);

            View.Button("CloseButton").onClick.AddListener(Close);
            View.Button("ProfileCloseButton").onClick.AddListener(Close);
            View.Button("InventoryTabButton").onClick.AddListener(ShowInventory);
            View.Button("AssetsTabButton").onClick.AddListener(ShowAssets);
            View.Button("CloseAllLongButton").onClick.AddListener(CloseAllLong);
            View.Button("CloseAllShortButton").onClick.AddListener(CloseAllShort);
            View.Button("IndexButton").onClick.AddListener(RequestIndex);
            View.Button("RankingButton").onClick.AddListener(RequestRanking);
            View.Button("InventoryButton").onClick.AddListener(ShowInventory);
            View.Button("MotionButton").onClick.AddListener(ToggleMotion);
            services.Overlays.AttachTooltip(
                View.Button("CloseAllLongButton").gameObject,
                "全部平多头",
                "为所有未退市持货生成卖出指令，先统一预览，确认后才成交。",
                "退市仓位保持冻结");
            services.Overlays.AttachTooltip(
                View.Button("CloseAllShortButton").gameObject,
                "全部平空头",
                "为所有未退市借货生成还货指令，现金与格位在预览中重新校验。",
                "整批同成同败");
        }

        internal event Action CloseRequested;
        internal event Action<string> DetailRequested;
        internal event Action IndexRequested;
        internal event Action RankingRequested;
        internal bool IsInteractive { get { return _interactive; } }

        internal void Show(bool interactive)
        {
            _interactive = interactive;
            ApplyAccessMode();
            Open();
        }

        protected override void OnOpened()
        {
            ApplyAccessMode();
            ApplyResponsiveLayout();
            UiView.SetLabel(
                View.Button("MotionButton"),
                Services.Motion.ReducedMotion ? "动效已减弱" : "减少动效");
            Refresh();
        }

        internal void Refresh()
        {
            if (!IsOpen)
                return;
            RefreshMasthead();
            AssetSnapshot snapshot = null;
            CapacitySnapshot capacity = null;
            string notice = string.Empty;
            try
            {
                if (_application.Assets != null)
                    snapshot = _application.Assets.GetSnapshot();
            }
            catch (Exception)
            {
                notice = "原版银两数据尚未就绪，资产与现价暂不可读。";
            }
            try
            {
                if (_application.Capacity != null)
                    capacity = _application.Capacity.Evaluate(_state, _definitions);
            }
            catch (Exception)
            {
                notice = string.IsNullOrEmpty(notice)
                    ? "原版角色属性尚未就绪，货囊容量暂不可读。"
                    : notice + " 角色属性亦未就绪。";
            }

            if (_showAssets)
            {
                _assetsPage.Refresh(snapshot);
            }
            else
            {
                RefreshInventory(capacity);
            }
            View.Text("StatusText").text = _interactive
                ? "市场货囊 · 平仓需落印确认"
                : "系统货囊 · 可查看持仓行情";
            if (!_showAssets && !_capacityProjectionExact && capacity != null)
                View.Text("StatusText").text += " · 格位投影待核验";
            if (!string.IsNullOrEmpty(notice))
                View.Text("StatusText").text = notice + " " + View.Text("StatusText").text;
        }

        internal void Tick()
        {
            if (IsOpen)
                ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            float headerWidth = ((RectTransform)View.Rect("SummaryCapacityCell").parent).rect.width;
            if (!_showAssets && Mathf.Abs(headerWidth - _metricHeaderWidth) > 0.01f)
                _metricHeaderWidth = InventoryPaper.FitHeaderMetrics(View);
            _compact = Screen.width > 0 && Screen.width <= 1366;
            _gridColumns = _compact ? 4 : 6;
            ApplySummaryLayout();
            VerticalLayoutGroup sectionsLayout = _sections == null
                ? null
                : _sections.GetComponent<VerticalLayoutGroup>();
            if (sectionsLayout != null)
                sectionsLayout.spacing = InventorySectionLayout.Spacing(_compact);
            float availableHeight = _sections == null ? 0f : _sections.rect.height;
            _longSection.SetLayout(_gridColumns, _compact, availableHeight);
            _shortSection.SetLayout(_gridColumns, _compact, availableHeight);
        }

        private void ApplySummaryLayout()
        {
            if (_summary == null || _sections == null || _summary.rect.width < 1f)
                return;
            float height = InventorySummaryLayout.Height(_compact);
            _summary.offsetMin = new Vector2(_summary.offsetMin.x, -height);
            _summary.offsetMax = new Vector2(_summary.offsetMax.x, 0f);
            _sections.offsetMax = new Vector2(
                _sections.offsetMax.x,
                -height - InventorySummaryLayout.GapAfterSummary(_compact));
            float left = 14f;
            for (int i = 0; i < _summaryCells.Length; i++)
            {
                RectTransform cell = _summaryCells[i];
                Text caption = cell.GetChild(0).GetComponent<Text>();
                Text value = cell.GetChild(1).GetComponent<Text>();
                caption.fontSize = _compact ? 10 : 11;
                value.fontSize = i == 0 ? (_compact ? 22 : 24) : (_compact ? 17 : 19);
                caption.alignment = value.alignment = i == 3 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                caption.horizontalOverflow = value.horizontalOverflow = HorizontalWrapMode.Overflow;
                float textWidth = Mathf.Ceil(Mathf.Max(caption.preferredWidth, value.preferredWidth)) + 3f;
                float width = Mathf.Clamp(textWidth, i == 0 ? 82f : 112f, _summary.rect.width * 0.22f);
                if (i == 3)
                    InventoryPaper.Right(cell, 14, 0, width, height);
                else
                    InventoryPaper.Box(cell, left, 0, width, height);
                left += width + (_compact ? 17 : 26);
                InventoryPaper.Box(caption.rectTransform, 0, _compact ? 2 : 7, width, 15);
                InventoryPaper.Box(value.rectTransform, 0, _compact ? 18 : 24, width, _compact ? 28 : 32);
            }
        }

        public override bool CancelFromInput()
        {
            Close();
            return true;
        }

        private void RefreshMasthead()
        {
            List<WeeklyPoint> history = MarketQuery.IndexHistory(_application);
            WeeklyPoint current = history.Count == 0 ? null : history[history.Count - 1];
            double weekly = MarketQuery.WeeklyReturn(history);
            SetIndexQuote(current == null ? (double?)null : current.Price, weekly);
            if (history.Count > 14)
                history = history.GetRange(history.Count - 14, 14);
            _indexChart.SetPoints(history);
            SetWalletBalance(_application.Wallet == null ? (long?)null : _application.Wallet.Balance);
        }

        private float _metricHeaderWidth;

        private void RefreshInventory(CapacitySnapshot capacity)
        {
            bool capacityValid = capacity != null && capacity.Error == TradeError.None;
            View.Text("CapacityText").text = UiFormatting.CapacitySlots(capacity);
            View.Text("CapacityText").color = !capacityValid
                ? UiFormatting.Muted
                : capacity.IsOverCapacity ? UiFormatting.Red : UiFormatting.Ink;
            View.Text("CapacityCaptionText").text = capacity != null
                ? "货囊格位 · 体力 " + capacity.Stats.Life
                : "货囊容量";
            View.Text("WeightCaptionText").text = capacity != null
                ? "单格负重 · 轻功 " + capacity.Stats.Lightness + " / 内功 " + capacity.Stats.InternalStrength
                : "单格负重能力";
            View.Text("WeightText").text = capacity != null
                ? UiFormatting.Money((long)capacity.CarryingCapacity) + " / 格"
                : "—";
            View.Text("WeightText").color = capacity != null ? UiFormatting.Ink : UiFormatting.Muted;
            _metricHeaderWidth = InventoryPaper.FitHeaderMetrics(View);

            BuildPositionModels();
            InventoryGridProjection grid = InventoryGridProjection.Build(
                _longModels,
                _shortModels,
                capacityValid ? capacity : null);
            _capacityProjectionExact = grid.HasExactCapacity;
            _longSection.Refresh(grid.LongCells, _interactive);
            _shortSection.Refresh(grid.ShortCells, _interactive);
            PortfolioProjection profit = MarketQuery.BuildPortfolioProjection(_state);
            long longQuantity = Quantity(_longModels);
            long shortQuantity = Quantity(_shortModels);
            double longProfit = Floating(_longModels);
            double shortProfit = Floating(_shortModels);
            bool longComplete = PricesComplete(_longModels);
            bool shortComplete = PricesComplete(_shortModels);

            View.Text("GoodsValueText").text = profit.HasCompletePrices
                ? UiFormatting.Money(profit.LongValue) + "<size=11> 文</size>"
                : "—";
            View.Text("TotalProfitText").text = profit.HasCompletePrices
                ? UiFormatting.SignedMoney(profit.FloatingProfit) + "<size=10> 文 · " +
                    UiFormatting.Percent(profit.FloatingReturnRate) + "</size>"
                : "— · 部分价格不可见";
            View.Text("TotalProfitText").color = profit.HasCompletePrices
                ? UiFormatting.Profit(profit.FloatingProfit, true)
                : UiFormatting.DarkMuted;
            View.Text("WeeklyProfitText").text = profit.HasCompleteWeeklyPrices
                ? UiFormatting.SignedMoney(profit.WeeklyRevaluation) + "<size=10> 文 · " +
                    UiFormatting.Percent(profit.WeeklyReturnRate) + "</size>"
                : "— · 缺最近两周价格";
            View.Text("WeeklyProfitText").color = profit.HasCompleteWeeklyPrices
                ? UiFormatting.Profit(profit.WeeklyRevaluation, true)
                : UiFormatting.DarkMuted;
            View.Text("LoansValueText").text = profit.HasCompletePrices
                ? (profit.ShortLiability > 0 ? "− " : "") + UiFormatting.Money(profit.ShortLiability) + "<size=11> 文</size>"
                : "—";
            View.Text("LongSummaryText").text = "持货 " + longQuantity + " 件 / 成本 " +
                UiFormatting.Money(profit.LongCost) + " 文";
            View.Text("ShortSummaryText").text = "待还 " + shortQuantity + " 件 / 实收 " +
                UiFormatting.Money(profit.ShortProceeds) + " 文";
            View.Text("LongProfitText").text = "浮动 " + (longComplete ? UiFormatting.SignedMoney(longProfit) : "—");
            View.Text("LongProfitText").color = longComplete ? UiFormatting.Profit(longProfit) : UiFormatting.Muted;
            View.Text("ShortProfitText").text = "浮动 " + (shortComplete ? UiFormatting.SignedMoney(shortProfit) : "—");
            View.Text("ShortProfitText").color = shortComplete ? UiFormatting.Profit(shortProfit) : UiFormatting.Muted;
            InventoryPaper.Right(View.Button("CloseAllLongButton").image.rectTransform,
                Mathf.Ceil(View.Text("LongProfitText").preferredWidth) + 11, 0, 74, 26);
            InventoryPaper.Right(View.Button("CloseAllShortButton").image.rectTransform,
                Mathf.Ceil(View.Text("ShortProfitText").preferredWidth) + 11, 0, 74, 26);
            View.Button("CloseAllLongButton").interactable = _interactive && HasClosable(_longModels);
            View.Button("CloseAllShortButton").interactable = _interactive && HasClosable(_shortModels);
            View.Button("CloseAllLongButton").GetComponent<InventoryCloseAllStyle>().Paint();
            View.Button("CloseAllShortButton").GetComponent<InventoryCloseAllStyle>().Paint();
        }

        private void BuildPositionModels()
        {
            _longModels.Clear();
            _shortModels.Clear();
            if (_state.Holdings != null)
            {
                for (int i = 0; i < _state.Holdings.Count; i++)
                {
                    Holding holding = _state.Holdings[i];
                    if (holding == null || holding.Quantity <= 0)
                        continue;
                    _longModels.Add(CreateModel(holding.ItemId, true, holding.Quantity, holding.TotalCost, -1));
                }
            }
            Dictionary<string, InventorySlotModel> shorts =
                new Dictionary<string, InventorySlotModel>(StringComparer.Ordinal);
            if (_state.ShortBatches != null)
            {
                int currentWeek = MarketQuery.CurrentWeek(_state);
                for (int i = 0; i < _state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = _state.ShortBatches[i];
                    if (batch == null || batch.Quantity <= 0)
                        continue;
                    string itemId = batch.ItemId ?? string.Empty;
                    InventorySlotModel model;
                    if (!shorts.TryGetValue(itemId, out model))
                    {
                        model = CreateModel(itemId, false, 0, 0, -1);
                        shorts.Add(itemId, model);
                    }
                    InventoryBatchProjection.Accumulate(model, batch, currentWeek);
                }
            }
            _shortModels.AddRange(shorts.Values);
            SortModels(_longModels);
            SortModels(_shortModels);
        }

        private InventorySlotModel CreateModel(string itemId, bool isLong, long quantity, long basis, int dueWeek)
        {
            itemId = itemId ?? string.Empty;
            ItemDefinition item;
            if (!_definitions.TryGetValue(itemId, out item))
                item = new ItemDefinition { ItemId = itemId, Name = itemId };
            WeeklyPoint current = MarketQuery.CurrentPoint(_application, itemId);
            return new InventorySlotModel
            {
                Item = item,
                IsLong = isLong,
                Quantity = quantity,
                Basis = basis,
                CurrentPrice = current == null ? 0.0 : current.Price,
                PriceAvailable = current != null,
                Delisted = MarketQuery.IsDelisted(_application, _state, itemId),
                DueWeek = dueWeek
            };
        }

        private void ApplyAccessMode()
        {
            View.Button("CloseAllLongButton").gameObject.SetActive(_interactive);
            View.Button("CloseAllShortButton").gameObject.SetActive(_interactive);
            View.Button("IndexButton").gameObject.SetActive(_interactive);
            View.Button("RankingButton").gameObject.SetActive(_interactive);
            View.Button("InventoryButton").gameObject.SetActive(_interactive);
        }

        private void ShowInventory()
        {
            SelectPage(false);
        }

        private void ShowAssets()
        {
            SelectPage(true);
        }

        private void SelectPage(bool assets)
        {
            _showAssets = assets;
            if (_summary != null)
                _summary.gameObject.SetActive(!assets);
            if (_sections != null)
                _sections.gameObject.SetActive(!assets);
            View.Rect("SummaryCapacityCell").gameObject.SetActive(!assets);
            View.Rect("SummaryWeightCell").gameObject.SetActive(!assets);
            _assetsPage.SetActive(assets);
            View.Rect("InventoryTabActiveLine").gameObject.SetActive(!assets);
            View.Rect("AssetsTabActiveLine").gameObject.SetActive(assets);
            UiView.SetLabelColor(View.Button("InventoryTabButton"), assets ? UiFormatting.Muted : UiFormatting.Ink);
            UiView.SetLabelColor(View.Button("AssetsTabButton"), assets ? UiFormatting.Ink : UiFormatting.Muted);
            if (IsOpen)
                Refresh();
        }

        private void OpenPositionDetail(InventorySlotModel model)
        {
            if (model != null && DetailRequested != null)
                DetailRequested(model.Item.ItemId);
        }

        private void ClosePosition(InventorySlotModel model)
        {
            if (!_interactive || model == null || model.Delisted)
                return;
            _confirmation.Present(
                CreateOrders(model),
                model.IsLong ? "卖出平仓" : "还货平仓",
                null);
        }

        private void CloseAllLong()
        {
            CloseAll(_longModels, true);
        }

        private void CloseAllShort()
        {
            CloseAll(_shortModels, false);
        }

        private void CloseAll(List<InventorySlotModel> models, bool isLong)
        {
            if (!_interactive)
                return;
            List<TradeOrder> orders = new List<TradeOrder>();
            for (int i = 0; i < models.Count; i++)
            {
                if (!models[i].Delisted)
                    orders.AddRange(CreateOrders(models[i]));
            }
            if (orders.Count > 0)
                _confirmation.Present(orders, isLong ? "全部多头平仓" : "全部空头平仓", null);
        }

        private static List<TradeOrder> CreateOrders(InventorySlotModel model)
        {
            List<TradeOrder> result = new List<TradeOrder>();
            long remaining = model.Quantity;
            while (remaining > 0)
            {
                int quantity = remaining > int.MaxValue ? int.MaxValue : (int)remaining;
                result.Add(new TradeOrder(
                    model.IsLong ? TradeAction.Sell : TradeAction.Return,
                    model.Item.ItemId,
                    quantity));
                remaining -= quantity;
            }
            return result;
        }

        private void RequestIndex()
        {
            if (_interactive && IndexRequested != null)
                IndexRequested();
        }

        private void RequestRanking()
        {
            if (_interactive && RankingRequested != null)
                RankingRequested();
        }

        private void Close()
        {
            if (CloseRequested != null)
                CloseRequested();
        }

        private void ToggleMotion()
        {
            Services.Motion.Toggle();
            UiView.SetLabel(
                View.Button("MotionButton"),
                Services.Motion.ReducedMotion ? "动效已减弱" : "减少动效");
        }

        private static long Quantity(List<InventorySlotModel> models)
        {
            long result = 0;
            for (int i = 0; i < models.Count; i++)
                result += models[i].Quantity;
            return result;
        }

        private static double Floating(List<InventorySlotModel> models)
        {
            double result = 0.0;
            for (int i = 0; i < models.Count; i++)
            {
                if (models[i].PriceAvailable)
                    result += models[i].Floating;
            }
            return result;
        }

        private static bool PricesComplete(List<InventorySlotModel> models)
        {
            for (int i = 0; i < models.Count; i++)
            {
                if (!models[i].PriceAvailable)
                    return false;
            }
            return true;
        }

        private static bool HasClosable(List<InventorySlotModel> models)
        {
            for (int i = 0; i < models.Count; i++)
            {
                if (!models[i].Delisted && models[i].Quantity > 0)
                    return true;
            }
            return false;
        }

        private static void SortModels(List<InventorySlotModel> models)
        {
            models.Sort(delegate (InventorySlotModel left, InventorySlotModel right)
            {
                return string.Compare(left.Item.ItemId, right.Item.ItemId, StringComparison.Ordinal);
            });
        }

        public override void Dispose()
        {
            if (View.IsAlive)
            {
                View.Button("CloseButton").onClick.RemoveListener(Close);
                View.Button("ProfileCloseButton").onClick.RemoveListener(Close);
                View.Button("InventoryTabButton").onClick.RemoveListener(ShowInventory);
                View.Button("AssetsTabButton").onClick.RemoveListener(ShowAssets);
                View.Button("CloseAllLongButton").onClick.RemoveListener(CloseAllLong);
                View.Button("CloseAllShortButton").onClick.RemoveListener(CloseAllShort);
                View.Button("IndexButton").onClick.RemoveListener(RequestIndex);
                View.Button("RankingButton").onClick.RemoveListener(RequestRanking);
                View.Button("InventoryButton").onClick.RemoveListener(ShowInventory);
                View.Button("MotionButton").onClick.RemoveListener(ToggleMotion);
            }
            _longSection.Dispose();
            _shortSection.Dispose();
            _assetsPage.Dispose();
            _indexChart.Dispose();
            CloseRequested = null;
            DetailRequested = null;
            IndexRequested = null;
            RankingRequested = null;
            base.Dispose();
        }
    }
}
