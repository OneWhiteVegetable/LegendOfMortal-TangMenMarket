using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketItemDetail : UiScreen
    {
        private const float ConstituentRowHeight = 36f;
        private const float ConstituentRowSpacing = 5f;

        private readonly ModApplication _application;
        private readonly RunState _state;
        private readonly Func<bool> _tradingVisit;
        private readonly UiWindowShell _shell;
        private readonly NewsTickerView _ticker;
        private readonly WeeklyChartView _indexChart;
        private readonly WeeklyChartView _chart;
        private readonly Button[] _rangeButtons = new Button[4];
        private readonly MarketDetailChartButton[] _rangeFaces = new MarketDetailChartButton[4];
        private readonly MarketDetailTypography _typography = new MarketDetailTypography();
        private readonly MarketDetailStickyFooter _stickyFooter;
        private readonly Button _constituentTemplate;
        private readonly RectTransform _constituentList;
        private readonly List<ConstituentRow> _constituentRows = new List<ConstituentRow>();
        private Button _trendButton;
        private Image _indexStageImage;
        private Image _indexHeadingIcon;
        private Image _deltaRule;
        private ScrollRect _descriptionScroll;
        private RectTransform _descriptionContent;
        private string _descriptionText;
        private Image _wasted;
        private string _itemId;
        private List<WeeklyPoint> _history = new List<WeeklyPoint>();
        private bool _indexMode;
        private bool _delisted;
        private readonly MarketCartPanel _cart;
        private int _quantity = 1;
        private Vector2 _trendRestPosition;
        private float _trendDirection = 1f;

        internal MarketItemDetail(UiFrontendServices services, ModApplication application, MarketCartPanel cart,
            Func<bool> tradingVisit)
            : base(services, "ItemDetail", UiSorting.Detail)
        {
            _application = application;
            _cart = cart;
            _state = application.State;
            _tradingVisit = tradingVisit;
            _shell = Root.GetComponent<UiWindowShell>();
            _ticker = new NewsTickerView(services, View.Rect("NewsTicker"), _application);
            _indexChart = new WeeklyChartView(
                View.Rect("IndexChartHost"),
                WeeklyChartMode.Spark,
                services.Assets.RegularFont,
                services.Assets.DryBrushTexture,
                services.Assets.SerifFont);
            _chart = new WeeklyChartView(
                View.Rect("ChartHost"),
                WeeklyChartMode.Detail,
                services.Assets.RegularFont,
                services.Assets.DryBrushTexture,
                services.Assets.SerifFont);
            _chart.Chart.ViewChanged += OnChartViewChanged;
            _typography.Apply(
                View.Text("ChartCaptionText"),
                View.Text("DescriptionText"),
                View.Text("NewsBodyText"));
            Transform legacySource = View.Rect("NewsSection").Find("NewsSourceText");
            if (legacySource != null)
                legacySource.gameObject.SetActive(false);
            View.Text("PriceText").fontStyle = FontStyle.BoldAndItalic;
            _typography.ApplyDisplay(
                services.Assets.DisplayFont,
                View.Text("GhostNumber"));
            _trendButton = EnsureTrendButton(View.Image("TrendIcon"));
            _indexStageImage = EnsureIndexStageImage(View.Image("ItemImage"), services.Assets.GetSprite("icon_chart"));
            ConfigureDetailStageImage(View.Image("ItemShadowImage").rectTransform, new Vector2(6f, -8f));
            ConfigureDetailStageImage(View.Image("ItemImage").rectTransform, Vector2.zero);
            Image stageSlash = View.Image("ItemImage").transform.parent.Find("StageSlash").GetComponent<Image>();
            UiPaperShape.Attach(stageSlash);
            stageSlash.color = new Color32(180, 147, 88, 204);
            stageSlash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 5f);
            stageSlash.rectTransform.anchorMin = new Vector2(.03f, .1f);
            stageSlash.rectTransform.anchorMax = new Vector2(.50f, .1f);
            stageSlash.rectTransform.pivot = new Vector2(0f, 0f);
            stageSlash.rectTransform.sizeDelta = new Vector2(0f, 8f);
            stageSlash.rectTransform.anchoredPosition = Vector2.zero;
            InitializeDescription();
            ConfigureHeadingLayout();
            _constituentTemplate = View.Button("ConstituentRowTemplate");
            _constituentTemplate.gameObject.SetActive(false);
            _constituentList = View.Rect("ConstituentList");
            _stickyFooter = View.Rect("TradeSection").gameObject.AddComponent<MarketDetailStickyFooter>();
            _stickyFooter.Initialize(
                View.Scroll("SidebarScroll"),
                View.Rect("TradeSection"),
                View.Rect("TradeSpacer"));
            _trendRestPosition = View.Image("TrendIcon").rectTransform.anchoredPosition;

            View.Button("BackButton").onClick.AddListener(Back);
            MarketBrushArrow.Attach(View.Button("BackButton"), services.Motion);
            View.Button("CloseButton").onClick.AddListener(Close);
            View.Button("IndexButton").onClick.AddListener(ShowIndex);
            View.Button("InventoryButton").onClick.AddListener(RequestInventory);
            View.Button("RankingButton").onClick.AddListener(RequestRanking);
            View.Button("MotionButton").onClick.AddListener(ToggleMotion);
            _trendButton.onClick.AddListener(PlayTrendMotion);
            View.Button("ResetChartButton").onClick.AddListener(ResetChart);
            MarketDetailChartButton.Attach(View.Button("ResetChartButton"), -1);
            Image captionMark = View.Text("ChartCaptionText").transform.parent.Find("CaptionMark").GetComponent<Image>();
            captionMark.rectTransform.localRotation = Quaternion.identity;
            UiPaperShape.Attach(captionMark, 2.3f);
            View.Button("QuantityDecrementButton").onClick.AddListener(Decrease);
            View.Button("QuantityIncrementButton").onClick.AddListener(Increment);
            MarketIconInteractionState.AttachStepper(
                View.Button("QuantityDecrementButton"), services.Motion);
            MarketIconInteractionState.AttachStepper(
                View.Button("QuantityIncrementButton"), services.Motion);
            MarketDetailActionFace.Attach(View.Button("BuyButton"), UiFormatting.Red, true, services.Motion);
            MarketDetailActionFace.Attach(View.Button("BorrowButton"), UiFormatting.Green, true, services.Motion);
            MarketDetailActionFace.Attach(View.Button("SellButton"), UiFormatting.Ink, false, services.Motion);
            MarketDetailActionFace.Attach(View.Button("CoverButton"), UiFormatting.Ink, false, services.Motion);
            View.Button("BuyButton").onClick.AddListener(AddBuy);
            View.Button("BorrowButton").onClick.AddListener(AddBorrow);
            services.Overlays.AttachTooltip(View.Button("BorrowButton").gameObject, () => string.Empty,
                _cart.BorrowHint, () => string.Empty, false);
            View.Button("SellButton").onClick.AddListener(AddSell);
            View.Button("CoverButton").onClick.AddListener(AddCover);
            _rangeButtons[0] = View.Button("RangeMonthButton");
            _rangeButtons[1] = View.Button("RangeHalfYearButton");
            _rangeButtons[2] = View.Button("RangeYearButton");
            _rangeButtons[3] = View.Button("RangeAllButton");
            for (int i = 0; i < _rangeButtons.Length; i++)
            {
                int captured = i;
                _rangeFaces[i] = MarketDetailChartButton.Attach(_rangeButtons[i], i);
                _rangeButtons[i].onClick.AddListener(delegate { SetRange(captured); });
            }

            services.Overlays.AttachTooltip(
                View.Button("ResetChartButton").gameObject,
                "复位图表",
                "恢复近半年范围、末端位置与价格缩放，不改变行情或仓位。",
                "只改变视图");
            services.Overlays.AttachTooltip(
                _trendButton.gameObject,
                "行情笔形",
                "播放本周涨跌的装饰笔形，不改变行情，也不代表下一周方向。",
                "仅视觉反馈");
            services.Overlays.AttachTooltip(
                View.Button("MotionButton").gameObject,
                "减少动效",
                "关闭界面淡入淡出，所有账目与交互保持不变。",
                "设置仅影响本机界面");
        }

        internal event Action BackRequested;
        internal event Action CloseRequested;
        internal event Action InventoryRequested;
        internal event Action RankingRequested;
        internal event Action<string, TradeAction, int, RectTransform> AddRequested;
        internal event Action<string> ConstituentRequested;

        internal string ItemId { get { return _itemId; } }
        internal bool IndexMode { get { return _indexMode; } }

        internal bool HasPosition(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_state.HasItemAppeared(itemId))
                return false;
            Holding holding = _state.FindHolding(itemId);
            return (holding != null && holding.Quantity > 0) || MarketQuery.ShortQuantity(_state, itemId) > 0;
        }

        internal void Show(string itemId)
        {
            if (_tradingVisit())
                ShowItem(itemId);
        }

        internal void ShowHolding(string itemId)
        {
            if (!_tradingVisit() && !IsOpen && HasPosition(itemId))
                ShowItem(itemId);
        }

        private void ShowItem(string itemId)
        {
            bool changed = _indexMode || !string.Equals(_itemId, itemId, StringComparison.Ordinal);
            _itemId = itemId;
            _indexMode = false;
            if (changed)
            {
                StopTrendMotion();
                _quantity = 1;
                View.Scroll("SidebarScroll").verticalNormalizedPosition = 1f;
            }
            Open();
        }

        internal void Resume()
        {
            if (_tradingVisit())
                Open();
        }

        internal void ShowIndex()
        {
            if (!_tradingVisit())
                return;
            bool changed = !_indexMode;
            _indexMode = true;
            _itemId = null;
            if (changed)
            {
                StopTrendMotion();
                View.Scroll("SidebarScroll").verticalNormalizedPosition = 1f;
            }
            Open();
        }

        protected override void OnOpened()
        {
            bool trading = _tradingVisit();
            View.Button("IndexButton").gameObject.SetActive(trading);
            View.Button("RankingButton").gameObject.SetActive(trading);
            View.Button("InventoryButton").gameObject.SetActive(trading);
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
            if (_indexMode)
                BindIndex();
            else
                BindItem();
        }

        public override bool CancelFromInput()
        {
            Back();
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
            _indexChart.SetPoints(history, MarketQuery.CurrentWeek(_state));
            SetWalletBalance(_application.Wallet == null ? (long?)null : _application.Wallet.Balance);
        }

        private void BindItem()
        {
            ItemDefinition item;
            if (string.IsNullOrEmpty(_itemId) || !_state.HasItemAppeared(_itemId) ||
                (!_tradingVisit() && !HasPosition(_itemId)) ||
                !_application.Items.TryGetItem(_itemId, out item))
            {
                Back();
                return;
            }
            _delisted = MarketQuery.IsDelisted(_application, _state, item.ItemId);
            _history = MarketQuery.ItemHistory(_application, item.ItemId);
            WeeklyPoint current = _history.Count == 0 ? null : _history[_history.Count - 1];
            double weekly = MarketQuery.WeeklyReturn(_history);
            double total = MarketQuery.TotalReturn(_history);
            Color trend = _delisted ? UiFormatting.Muted : UiFormatting.Trend(weekly);
            int number = MarketQuery.AppearedItems(_state, _application.Items).FindIndex(
                candidate => string.Equals(candidate.ItemId, item.ItemId, StringComparison.Ordinal)) + 1;

            View.Text("ItemKeyText").text = "ITEM / MOD:" + item.ItemId;
            View.Text("NameText").text = item.Name;
            View.Text("GhostNumber").text = Math.Max(1, number).ToString("00");
            View.Text("PriceText").text = current == null
                ? "—"
                : UiFormatting.Money(current.Price) + " <size=12><color=#6B726B>文</color></size>";
            View.Text("PriceText").color = trend;
            View.Text("DeltaText").text = current == null || _history.Count < 2
                ? "—"
                : UiFormatting.Delta(current.Price - _history[_history.Count - 2].Price, "文");
            View.Text("DeltaText").color = trend;
            _deltaRule.color = trend;
            View.Text("ChangeText").text = current == null ? "—" : UiFormatting.Percent(weekly);
            View.Text("ChangeText").color = trend;
            View.Text("ChartCaptionText").text = "逐周墨笔 · 红涨绿跌";
            View.Text("ChartHintText").text = "拖动看前期 · 滚轮缩放 · 尺线亦可操作";
            View.Text("DescriptionTitleText").text = "货物小传";
            bool hasTrend = current != null && Math.Abs(weekly) > 0.0000001;
            View.Image("TrendIcon").gameObject.SetActive(hasTrend);
            _indexHeadingIcon.gameObject.SetActive(false);
            _trendButton.interactable = hasTrend;
            View.Image("TrendIcon").sprite = Services.Assets.GetSprite(
                weekly >= 0.0 ? "trend_up_brush" : "trend_down_brush");
            _trendDirection = weekly >= 0.0 ? 1f : -1f;
            RestoreTrendPose();
            Sprite itemSprite = Services.Assets.GetSprite(MarketQuery.IconKey(item));
            View.Image("ItemImage").gameObject.SetActive(true);
            _indexStageImage.gameObject.SetActive(false);
            View.Image("ItemImage").sprite = itemSprite;
            View.Image("ItemImage").color = _delisted
                ? new Color(0.45f, 0.47f, 0.45f, 0.72f)
                : Color.white;
            View.Image("ItemImage").rectTransform.localRotation = Quaternion.identity;
            View.Image("ItemShadowImage").gameObject.SetActive(true);
            View.Image("ItemShadowImage").sprite = itemSprite;
            View.Image("ItemShadowImage").color = new Color(0f, 0f, 0f, _delisted ? 0.08f : 0.17f);
            SetWasted(_delisted);
            View.Text("DescriptionText").text = item.Description ?? string.Empty;
            ResizeDescription();
            BindNews(MarketQuery.LatestNews(_application, _state, item.ItemId));
            SetPositionSectionVisible(true);
            SetIndexSectionsVisible(false);
            SetTradeVisible(_tradingVisit());

            Holding holding = _state.FindHolding(item.ItemId);
            ShortPriceProjection shortPrice = MarketQuery.BuildShortPriceProjection(_state, item.ItemId);
            long borrowed = shortPrice.Quantity;
            double averageCost = holding == null || holding.Quantity <= 0
                ? 0.0
                : (double)holding.TotalCost / holding.Quantity;
            View.Text("LongPositionText").text = (holding == null ? 0 : holding.Quantity) +
                " 件 · " + UiFormatting.Money(averageCost);
            View.Text("ShortPositionText").text = borrowed + " 件\n" +
                MarketQuery.ShortPriceText(shortPrice);
            View.Text("WeeklyPositionText").text = current == null ? "—" : UiFormatting.Percent(weekly);
            View.Text("WeeklyPositionText").color = _delisted ? UiFormatting.DarkMuted : UiFormatting.Trend(weekly, true);
            View.Text("TotalPositionText").text = current == null ? "—" : UiFormatting.Percent(total);
            View.Text("TotalPositionText").transform.parent.Find("Caption").GetComponent<Text>().text =
                MarketQuery.ItemTotalReturnCaption(_history);
            View.Text("TotalPositionText").color = _delisted ? UiFormatting.DarkMuted : UiFormatting.Trend(total, true);
            UpdateQuantity();
            BindChart();
            BindTicker(item.ItemId);
            View.Text("StatusText").text = !_tradingVisit() ? "持仓行情 · 返回货囊可查看其他持仓" : _delisted
                ? "WASTED · 商品已退市，仓位冻结，所有交易与平仓按钮禁用"
                : "逐周真实行情 · 交易先写入契匣，确认后才成交";
        }

        private void BindIndex()
        {
            _delisted = false;
            _history = MarketQuery.IndexHistory(_application);
            WeeklyPoint current = _history.Count == 0 ? null : _history[_history.Count - 1];
            double weekly = MarketQuery.WeeklyReturn(_history);
            double total = MarketQuery.TotalReturn(_history);
            Color trend = UiFormatting.Trend(weekly);
            List<ItemDefinition> appeared = MarketQuery.AppearedItems(_state, _application.Items);
            View.Text("ItemKeyText").text = "TANGMEN COMPOSITE INDEX";
            View.Text("NameText").text = "唐门指数";
            View.Text("GhostNumber").text = "TMI";
            View.Text("PriceText").text = current == null
                ? "—"
                : UiFormatting.Number(current.Price, 2) + " <size=12><color=#6B726B>点</color></size>";
            View.Text("PriceText").color = trend;
            View.Text("DeltaText").text = current == null || _history.Count < 2
                ? "—"
                : UiFormatting.Delta(current.Price - _history[_history.Count - 2].Price, "点", 2);
            View.Text("DeltaText").color = trend;
            _deltaRule.color = trend;
            View.Text("ChangeText").text = current == null ? "—" : UiFormatting.Percent(weekly);
            View.Text("ChangeText").color = trend;
            View.Text("ChartCaptionText").text = appeared.Count + " 件已出现货物 · 大盘联动";
            View.Text("ChartHintText").text = "观察大盘 · 拖动看前期 · 滚轮缩放";
            View.Text("DescriptionTitleText").text = "指数小传";
            View.Image("TrendIcon").gameObject.SetActive(false);
            _indexHeadingIcon.gameObject.SetActive(true);
            View.Image("ItemImage").gameObject.SetActive(false);
            _indexStageImage.gameObject.SetActive(true);
            View.Image("ItemShadowImage").gameObject.SetActive(false);
            SetWasted(false);
            View.Text("DescriptionText").text =
                "唐门指数以本周目随机锁定的真实指数为源，从1000点起记录其随机历史区段的逐周变动，用于纵观行市冷暖，不对应单件物品成交价。\n\n" +
                "下列成分入口仅列本周目已经出现货物；图形、极值与刻度只读取当前周及以前。";
            ResizeDescription();
            View.Rect("NewsSection").gameObject.SetActive(false);
            SetPositionSectionVisible(false);
            View.Text("IndexWeeklyText").text = current == null ? "—" : UiFormatting.Percent(weekly);
            View.Text("IndexWeeklyText").color = current == null
                ? UiFormatting.DarkMuted
                : UiFormatting.Trend(weekly, true);
            View.Text("IndexTotalText").text = current == null ? "—" : UiFormatting.Percent(total);
            View.Text("IndexTotalText").color = current == null
                ? UiFormatting.DarkMuted
                : UiFormatting.Trend(total, true);
            BindConstituents(appeared);
            SetIndexSectionsVisible(true);
            SetTradeVisible(false);
            BindChart();
            BindTicker(null);
            View.Text("StatusText").text = "唐门指数 · 仅已发生周 · 指数不可交易";
        }

        private void BindTicker(string itemId)
        {
            GameObject ticker = View.Rect("NewsTicker").gameObject;
            bool visible = ticker.activeSelf;
            _ticker.Bind(_state, itemId);
            if (ticker.activeSelf != visible)
                _shell.ApplyNow();
        }

        private void BindNews(NewsRecord news)
        {
            View.Rect("NewsSection").gameObject.SetActive(news != null);
            if (news == null)
                return;
            NewsEntry entry = null;
            if (_application.NewsCatalog != null)
            {
                string[] parts = (news.PoolId ?? string.Empty).Split(':');
                int periodIndex;
                if (parts.Length >= 3 && int.TryParse(parts[1], out periodIndex) && periodIndex >= 0 &&
                    periodIndex < _application.NewsCatalog.Periods.Count)
                {
                    IReadOnlyList<NewsEntry> candidates = _application.NewsCatalog.GetCandidates(
                        news.ItemId,
                        periodIndex,
                        news.Direction >= 0 ? 1 : -1);
                    if (news.SelectionIndex >= 0 && news.SelectionIndex < candidates.Count)
                        entry = candidates[news.SelectionIndex];
                }
            }
            string delta = _history.Count < 2
                ? string.Empty
                : " · " + UiFormatting.Delta(
                    _history[_history.Count - 1].Price - _history[_history.Count - 2].Price,
                    "文");
            View.Text("NewsDirectionText").text = UiFormatting.Percent(news.Return) + delta;
            View.Text("NewsDirectionText").color = UiFormatting.Trend(news.Return, true);
            string title = entry == null || string.IsNullOrEmpty(entry.EventName) ? "江湖异动" : entry.EventName;
            View.Text("EventTitleText").text = title;
            View.Text("NewsBodyText").text = news.Text ?? string.Empty;

            float titleHeight = ResizeText(View.Text("EventTitleText"), 298f, 20f);
            float bodyY = 73f + titleHeight + 6f;
            SetTop(View.Text("NewsBodyText"), bodyY);
            float bodyHeight = ResizeText(View.Text("NewsBodyText"), 298f, 20f);
            SetSectionHeight(View.Rect("NewsSection"), bodyY + bodyHeight + 17f);
        }

        private void ConfigureHeadingLayout()
        {
            Text eyebrow = View.Text("ItemKeyText");
            ConfigureHeadingSpan(eyebrow.rectTransform, 0f, 0.62f, 72f, 3f, 8f, 23f);
            Text title = View.Text("NameText");
            RectTransform titleLine = RankingLayout.Rect("TitleLine", title.transform.parent);
            ConfigureHeadingSpan(titleLine, 0f, 0.68f, 72f, 27f, 0f, 62f);
            HorizontalLayoutGroup layout = titleLine.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            title.rectTransform.SetParent(titleLine, false);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = title.fontSize;
            RectTransform symbol = RankingLayout.Rect("TitleSymbol", titleLine);
            RankingLayout.Layout(symbol, 51f, 43f).minWidth = 51f;
            RectTransform trend = View.Image("TrendIcon").rectTransform;
            trend.SetParent(symbol, false);
            RankingLayout.Center(trend, 51f, 43f);
            _indexHeadingIcon = RankingLayout.Image("IndexSymbol", symbol, null, UiFormatting.Red);
            RankingLayout.Center(_indexHeadingIcon.rectTransform, 38f, 38f);
            _indexHeadingIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -5f);
            UiPaperShape.Attach(_indexHeadingIcon);
            Image indexGlyph = RankingLayout.Image("ChartIcon", _indexHeadingIcon.transform,
                Services.Assets.GetSprite("icon_chart"), UiFormatting.Paper);
            indexGlyph.preserveAspect = true;
            RankingLayout.Center(indexGlyph.rectTransform, 26f, 26f);

            Text price = View.Text("PriceText");
            ConfigureHeadingSpan(price.rectTransform, 0.68f, 1f, 16f, 0f, 0f, 65f);
            price.fontStyle = FontStyle.BoldAndItalic;
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            price.verticalOverflow = VerticalWrapMode.Overflow;
            price.lineSpacing = 0.95f;
            RectTransform changes = RankingLayout.Rect("QuoteChanges", price.transform.parent);
            ConfigureHeadingSpan(changes, 0.50f, 1f, 0f, 65f, 0f, 31f);
            HorizontalLayoutGroup quoteLayout = changes.gameObject.AddComponent<HorizontalLayoutGroup>();
            quoteLayout.childAlignment = TextAnchor.MiddleRight;
            quoteLayout.spacing = 13f;
            quoteLayout.childControlWidth = quoteLayout.childControlHeight = true;
            quoteLayout.childForceExpandWidth = false;
            quoteLayout.childForceExpandHeight = true;
            RectTransform delta = View.Text("DeltaText").rectTransform;
            delta.SetParent(changes, false);
            View.Text("ChangeText").rectTransform.SetParent(changes, false);
            _deltaRule = RankingLayout.Image("AmountUnderline", delta, null, UiFormatting.Muted);
            _deltaRule.rectTransform.anchorMin = new Vector2(0f, .5f);
            _deltaRule.rectTransform.anchorMax = new Vector2(1f, .5f);
            _deltaRule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            _deltaRule.rectTransform.anchoredPosition = new Vector2(0f, -9f);
            price.rectTransform.SetAsLastSibling();
            changes.SetAsLastSibling();
        }

        private static void ConfigureHeadingSpan(
            RectTransform rect,
            float leftAnchor,
            float rightAnchor,
            float left,
            float top,
            float right,
            float height)
        {
            rect.anchorMin = new Vector2(leftAnchor, 1f);
            rect.anchorMax = new Vector2(rightAnchor, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void ConfigureDetailStageImage(RectTransform rect, Vector2 shadowOffset)
        {
            rect.anchorMin = new Vector2(0.06f, 0.02f);
            rect.anchorMax = new Vector2(0.94f, 0.98f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(shadowOffset.x, 10f + shadowOffset.y);
        }

        private void InitializeDescription()
        {
            RectTransform section = View.Rect("DescriptionSection");
            Text text = View.Text("DescriptionText");
            Transform viewportTransform = section.Find("DescriptionScroll");
            if (viewportTransform == null)
            {
                var viewportObject = new GameObject("DescriptionScroll", typeof(RectTransform));
                viewportObject.layer = section.gameObject.layer;
                viewportTransform = viewportObject.transform;
                viewportTransform.SetParent(section, false);
            }
            RectTransform viewport = (RectTransform)viewportTransform;
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(0f, 1f);
            viewport.pivot = new Vector2(0f, 1f);
            viewport.anchoredPosition = new Vector2(20f, -36f);
            viewport.sizeDelta = new Vector2(320f, 118f);
            Image viewportImage = viewport.GetComponent<Image>();
            if (viewportImage == null)
                viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
            _descriptionScroll = viewport.GetComponent<ScrollRect>();
            if (_descriptionScroll == null)
                _descriptionScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _descriptionScroll.horizontal = false;
            _descriptionScroll.vertical = true;
            _descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
            _descriptionScroll.scrollSensitivity = 24f;
            _descriptionScroll.viewport = viewport;
            _descriptionContent = _descriptionScroll.content;
            if (_descriptionContent == null)
            {
                var contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.layer = section.gameObject.layer;
                _descriptionContent = (RectTransform)contentObject.transform;
                _descriptionContent.SetParent(viewport, false);
            }
            _descriptionContent.anchorMin = new Vector2(0f, 1f);
            _descriptionContent.anchorMax = new Vector2(1f, 1f);
            _descriptionContent.pivot = new Vector2(0.5f, 1f);
            _descriptionContent.offsetMin = Vector2.zero;
            _descriptionContent.offsetMax = Vector2.zero;
            _descriptionScroll.content = _descriptionContent;
            text.rectTransform.SetParent(_descriptionContent, false);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        private void ResizeDescription()
        {
            Text text = View.Text("DescriptionText");
            float maximum = Screen.height > 0 && Screen.height <= 790 ? 82f : 118f;
            text.rectTransform.sizeDelta = new Vector2(0f, 22f);
            float contentHeight = Mathf.Max(22f, Mathf.Ceil(text.preferredHeight));
            text.rectTransform.sizeDelta = new Vector2(0f, contentHeight);
            _descriptionContent.sizeDelta = new Vector2(0f, contentHeight);
            float visibleHeight = Mathf.Min(maximum, contentHeight);
            RectTransform viewport = _descriptionScroll.viewport;
            viewport.sizeDelta = new Vector2(320f, visibleHeight);
            _descriptionScroll.vertical = contentHeight > visibleHeight + 0.5f;
            if (!string.Equals(_descriptionText, text.text, StringComparison.Ordinal))
            {
                _descriptionText = text.text;
                _descriptionScroll.verticalNormalizedPosition = 1f;
            }
            SetSectionHeight(View.Rect("DescriptionSection"), 36f + visibleHeight + 12f);
        }

        private static float ResizeText(Text text, float width, float minimumHeight)
        {
            text.rectTransform.sizeDelta = new Vector2(width, minimumHeight);
            float height = Mathf.Max(minimumHeight, Mathf.Ceil(text.preferredHeight));
            text.rectTransform.sizeDelta = new Vector2(width, height);
            return height;
        }

        private static void SetTop(Text text, float top)
        {
            text.rectTransform.anchoredPosition = new Vector2(
                text.rectTransform.anchoredPosition.x,
                -top);
        }

        private static void SetSectionHeight(RectTransform section, float height)
        {
            LayoutElement layout = section.GetComponent<LayoutElement>();
            if (layout != null)
                layout.preferredHeight = Mathf.Ceil(height);
        }

        private void BindConstituents(List<ItemDefinition> items)
        {
            int count = items == null ? 0 : items.Count;
            View.Text("ConstituentEmptyText").gameObject.SetActive(count == 0);
            for (int i = 0; i < count; i++)
            {
                ConstituentRow row = GetConstituentRow(i);
                ItemDefinition item = items[i];
                List<WeeklyPoint> history = MarketQuery.ItemHistory(_application, item.ItemId);
                WeeklyPoint current = history.Count == 0 ? null : history[history.Count - 1];
                double weekly = MarketQuery.WeeklyReturn(history);
                row.ItemId = item.ItemId;
                row.Button.gameObject.SetActive(true);
                row.Button.interactable = true;
                row.Icon.sprite = Services.Assets.GetSprite(MarketQuery.IconKey(item));
                row.Name.text = item.Name;
                row.Change.text = current == null ? "—" : UiFormatting.Percent(weekly);
                row.Change.color = current == null
                    ? UiFormatting.DarkMuted
                    : UiFormatting.Trend(weekly, true);
            }
            for (int i = count; i < _constituentRows.Count; i++)
                _constituentRows[i].Button.gameObject.SetActive(false);
            float listHeight = count == 0
                ? 0f
                : count * ConstituentRowHeight + (count - 1) * ConstituentRowSpacing;
            _constituentList.sizeDelta = new Vector2(320f, listHeight);
            SetSectionHeight(
                View.Rect("ConstituentSection"),
                36f + (count == 0 ? 28f : listHeight) + 12f);
        }

        private ConstituentRow GetConstituentRow(int index)
        {
            if (index < _constituentRows.Count)
                return _constituentRows[index];
            GameObject clone = UnityEngine.Object.Instantiate(
                _constituentTemplate.gameObject,
                _constituentList,
                false);
            clone.name = "ConstituentRow_" + index.ToString("00");
            ConstituentRow row = new ConstituentRow
            {
                Button = clone.GetComponent<Button>(),
                Icon = clone.transform.Find("Icon").GetComponent<Image>(),
                Name = clone.transform.Find("Name").GetComponent<Text>(),
                Change = clone.transform.Find("Change").GetComponent<Text>()
            };
            ConstituentRow captured = row;
            row.Button.onClick.AddListener(delegate { OpenConstituent(captured.ItemId); });
            _constituentRows.Add(row);
            return row;
        }

        private void OpenConstituent(string itemId)
        {
            if (!_tradingVisit() || !_indexMode || string.IsNullOrEmpty(itemId) || !_state.HasItemAppeared(itemId))
                return;
            if (ConstituentRequested != null)
                ConstituentRequested(itemId);
            else
                Show(itemId);
        }

        private void BindChart()
        {
            _chart.SetPoints(_history, MarketQuery.CurrentWeek(_state));
            _chart.Chart.color = _delisted ? new Color(0.45f, 0.46f, 0.44f, 0.65f) : Color.white;
            UpdateRangeButtons();
        }

        private void SetRange(int selection)
        {
            _chart.Chart.SetRange(RangeForSelection(selection));
        }

        private void ResetChart()
        {
            _chart.Chart.ResetView();
        }

        private void OnChartViewChanged(WeeklyChartGraphic chart)
        {
            UpdateRangeButtons();
        }

        private void UpdateRangeButtons()
        {
            WeeklyChartGraphic chart = _chart.Chart;
            int? activePreset = chart.ActivePresetRange;
            bool customView = chart.IsCustomView;
            for (int i = 0; i < _rangeButtons.Length; i++)
            {
                bool active = !customView && activePreset.HasValue &&
                    activePreset.Value == RangeForSelection(i);
                _rangeFaces[i].SetSelected(active);
            }
        }

        private static int RangeForSelection(int selection)
        {
            return selection == 0 ? 12 : selection == 1 ? 26 : selection == 2 ? 52 : int.MaxValue;
        }

        private static Button EnsureTrendButton(Image image)
        {
            image.raycastTarget = true;
            Button button = image.GetComponent<Button>();
            if (button == null)
                button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            return button;
        }

        private static Image EnsureIndexStageImage(Image itemImage, Sprite sprite)
        {
            Transform parent = itemImage.transform.parent;
            Transform existing = parent.Find("IndexStageImage");
            Image image = existing == null ? null : existing.GetComponent<Image>();
            RectTransform rect;
            if (image == null)
            {
                var gameObject = new GameObject("IndexStageImage", typeof(RectTransform));
                gameObject.layer = itemImage.gameObject.layer;
                rect = (RectTransform)gameObject.transform;
                rect.SetParent(parent, false);
                image = gameObject.AddComponent<Image>();
            }
            else
            {
                rect = image.rectTransform;
            }
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 7f);
            rect.sizeDelta = new Vector2(110f, 110f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.Euler(0f, 0f, -5f);
            rect.SetSiblingIndex(itemImage.transform.GetSiblingIndex() + 1);
            image.sprite = sprite;
            image.color = UiFormatting.Ink;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        private void SetWasted(bool visible)
        {
            if (visible && _wasted == null)
            {
                Image source = View.Image("ItemImage");
                GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
                clone.name = "RuntimeWasted";
                _wasted = clone.GetComponent<Image>();
                _wasted.sprite = Services.Assets.GetSprite("wasted_delist_wordmark");
                _wasted.color = Color.white;
                _wasted.raycastTarget = false;
                _wasted.preserveAspect = true;
                _wasted.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                _wasted.transform.SetAsLastSibling();
            }
            if (_wasted != null)
                _wasted.gameObject.SetActive(visible);
        }

        private void SetTradeVisible(bool visible)
        {
            _stickyFooter.SetVisible(visible);
        }

        private void SetPositionSectionVisible(bool visible)
        {
            Transform section = View.Find("Window/DetailPage/Sidebar/SidebarScroll/Content/PositionSection");
            if (section != null)
                section.gameObject.SetActive(visible);
        }

        private void SetIndexSectionsVisible(bool visible)
        {
            View.Rect("IndexMetricsSection").gameObject.SetActive(visible);
            View.Rect("ConstituentSection").gameObject.SetActive(visible);
        }

        private void Decrease() { SetQuantity(_quantity - 1); }
        private void Increment() { if (_quantity < int.MaxValue) SetQuantity(_quantity + 1); }

        private void SetQuantity(int quantity)
        {
            if (!_tradingVisit() || _delisted || _indexMode)
                return;
            _quantity = Math.Max(1, quantity);
            UpdateQuantity();
        }

        private void UpdateQuantity()
        {
            MarketDraft draft = !_tradingVisit() || _delisted || _indexMode ? default(MarketDraft) : _cart.Draft(_itemId, ref _quantity, true);
            View.Button("BuyButton").interactable = draft.Buy;
            View.Button("BorrowButton").interactable = draft.Borrow;
            View.Button("SellButton").interactable = draft.Sell;
            View.Button("CoverButton").interactable = draft.Return;
            View.Text("QuantityText").text = _quantity.ToString();
            Button decrement = View.Button("QuantityDecrementButton");
            Button increment = View.Button("QuantityIncrementButton");
            decrement.interactable = _tradingVisit() && !_delisted && !_indexMode && _quantity > 1;
            increment.interactable = draft.CanIncrement;
            MarketIconInteractionState.Refresh(decrement);
            MarketIconInteractionState.Refresh(increment);
        }

        private void AddBuy() { Add(TradeAction.Buy, "BuyButton"); }
        private void AddBorrow() { Add(TradeAction.Borrow, "BorrowButton"); }
        private void AddSell() { Add(TradeAction.Sell, "SellButton"); }
        private void AddCover() { Add(TradeAction.Return, "CoverButton"); }

        private void Add(TradeAction action, string buttonKey)
        {
            if (_tradingVisit() && IsOpen && !_indexMode && !_delisted && !string.IsNullOrEmpty(_itemId) && AddRequested != null)
            {
                AddRequested(
                    _itemId,
                    action,
                    _quantity,
                    View.Button(buttonKey).GetComponent<RectTransform>());
                UpdateQuantity();
            }
        }

        private void Back()
        {
            if (BackRequested != null)
                BackRequested();
        }

        private void Close()
        {
            if (CloseRequested != null)
                CloseRequested();
        }

        private void RequestInventory()
        {
            if (_tradingVisit() && InventoryRequested != null)
                InventoryRequested();
        }

        private void RequestRanking()
        {
            if (_tradingVisit() && RankingRequested != null)
                RankingRequested();
        }

        private void ToggleMotion()
        {
            Services.Motion.Toggle();
            if (Services.Motion.ReducedMotion)
                StopTrendMotion();
            UiView.SetLabel(
                View.Button("MotionButton"),
                Services.Motion.ReducedMotion ? "动效已减弱" : "减少动效");
        }

        private void PlayTrendMotion()
        {
            if (_indexMode || !View.Image("TrendIcon").gameObject.activeSelf)
                return;
            StopTrendMotion();
            if (Services.Motion.ReducedMotion)
                return;
            RectTransform rect = View.Image("TrendIcon").rectTransform;
            rect.localScale = Vector3.one * 0.82f;
            rect.localRotation = Quaternion.identity;
            Sequence sequence = DOTween.Sequence();
            sequence.Append(rect.DOScale(1.16f, 0.167f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Join(rect.DOAnchorPos(
                _trendRestPosition + new Vector2(6f * _trendDirection, 0f),
                0.167f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Join(rect.DOLocalRotateQuaternion(
                Quaternion.Euler(0f, 0f, -8f * _trendDirection),
                0.167f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Append(rect.DOScale(1.03f, 0.132f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Join(rect.DOAnchorPos(
                _trendRestPosition - new Vector2(2f * _trendDirection, 0f),
                0.132f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Join(rect.DOLocalRotateQuaternion(
                Quaternion.Euler(0f, 0f, 3f * _trendDirection),
                0.132f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Append(rect.DOScale(1f, 0.141f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Join(rect.DOAnchorPos(_trendRestPosition, 0.141f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.Join(rect.DOLocalRotateQuaternion(
                Quaternion.Euler(0f, 0f, -4f * _trendDirection),
                0.141f).SetEase(UiMotionEasing.PaperOutEase));
            sequence.SetUpdate(true).SetId(rect.gameObject).OnComplete(RestoreTrendPose);
        }

        private void StopTrendMotion()
        {
            if (!View.IsAlive)
                return;
            Services.Motion.Kill(View.Image("TrendIcon").gameObject);
            RestoreTrendPose();
        }

        private void RestoreTrendPose()
        {
            if (!View.IsAlive)
                return;
            RectTransform rect = View.Image("TrendIcon").rectTransform;
            rect.anchoredPosition = _trendRestPosition;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.Euler(0f, 0f, -4f * _trendDirection);
        }

        public override void Dispose()
        {
            if (View.IsAlive)
            {
                View.Button("BackButton").onClick.RemoveListener(Back);
                View.Button("CloseButton").onClick.RemoveListener(Close);
                View.Button("IndexButton").onClick.RemoveListener(ShowIndex);
                View.Button("InventoryButton").onClick.RemoveListener(RequestInventory);
                View.Button("RankingButton").onClick.RemoveListener(RequestRanking);
                View.Button("MotionButton").onClick.RemoveListener(ToggleMotion);
                _trendButton.onClick.RemoveListener(PlayTrendMotion);
                View.Button("ResetChartButton").onClick.RemoveListener(ResetChart);
                View.Button("QuantityDecrementButton").onClick.RemoveListener(Decrease);
                View.Button("QuantityIncrementButton").onClick.RemoveListener(Increment);
                View.Button("BuyButton").onClick.RemoveListener(AddBuy);
                View.Button("BorrowButton").onClick.RemoveListener(AddBorrow);
                View.Button("SellButton").onClick.RemoveListener(AddSell);
                View.Button("CoverButton").onClick.RemoveListener(AddCover);
                for (int i = 0; i < _rangeButtons.Length; i++)
                    _rangeButtons[i].onClick.RemoveAllListeners();
                for (int i = 0; i < _constituentRows.Count; i++)
                    _constituentRows[i].Button.onClick.RemoveAllListeners();
            }
            StopTrendMotion();
            _stickyFooter.Shutdown();
            _constituentRows.Clear();
            if (_chart.Chart != null)
                _chart.Chart.ViewChanged -= OnChartViewChanged;
            _chart.Dispose();
            _indexChart.Dispose();
            BackRequested = null;
            CloseRequested = null;
            InventoryRequested = null;
            RankingRequested = null;
            AddRequested = null;
            ConstituentRequested = null;
            base.Dispose();
            _typography.Dispose();
        }

        private sealed class ConstituentRow
        {
            internal Button Button;
            internal Image Icon;
            internal Text Name;
            internal Text Change;
            internal string ItemId;
        }
    }
}
