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
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Inventory
{
    internal sealed class AssetsPage : IDisposable
    {
        private const int MetricCount = 5;
        private const string BaseDisclaimer =
            "金钱读左轴，持货市值读右轴，均以文计。曲线记录市场周或成交时的金额，上方金钱为当前钱包实数。";
        private const string ProfitCaption = "金线读左轴（文），收益率读右轴（%）。正收益率用朱红，负收益率用青绿，零值用灰。总盈亏为已实现与持仓浮盈之和，收益率以累计买入成本与借货实收之和为分母。";

        private static readonly string[] MetricKeys = { "CashText", "GoodsText", "EquityText", "ProfitText", "ReturnText" };
        private static readonly string[] CaptionKeys = { "CashCaptionText", "GoodsCaptionText", "EquityCaptionText", "ProfitCaptionText", "ReturnCaptionText" };
        private static readonly string[] AccentKeys = { "CashAccent", "GoodsAccent", "EquityAccent", "ProfitAccent", "ReturnAccent" };
        private static readonly Color AssetGold = new Color32(180, 147, 88, 255);

        private readonly ModApplication _application;
        private readonly RunState _state;
        private readonly UiPrefabInstance _view;
        private readonly AssetChartGraphic _chart;
        private readonly AssetSecondaryAxis _goodsAxis;
        private readonly AssetsPageLayoutDriver _layoutDriver;
        private readonly HorizontalLayoutGroup _cardsLayout;
        private readonly RectTransform _chartPanel;
        private readonly RectTransform _chartHeader;
        private readonly RectTransform _chartHost;
        private readonly RectTransform _resetRect;
        private readonly Text _chartTitle;
        private readonly Text _disclaimer;
        private readonly Text _leftLegend, _rightLegend, _emptyChart;
        private readonly Image _ledgerStroke;
        private readonly RectTransform _rateColors;
        private readonly Button _switchButton;
        private bool _showProfit = true;
        private readonly Text[] _metricValues = new Text[MetricCount];
        private readonly Text[] _metricCaptions = new Text[MetricCount];
        private readonly Image[] _metricAccents = new Image[MetricCount];
        private readonly LayoutElement[] _cardLayouts = new LayoutElement[MetricCount];
        private AssetSnapshot _snapshot;
        private bool _metricsDirty;
        private bool _compact;

        internal AssetsPage(UiFrontendServices services, Transform parent, ModApplication application)
        {
            _application = application;
            _state = application.State;
            _view = services.Assets.Create("AssetsPage", parent);
            UiView.Stretch(_view.Root);
            _cardsLayout = _view.Get<HorizontalLayoutGroup>("AssetCardsLayout");
            _chartPanel = _view.Rect("ChartPanel");
            _chartHeader = _view.Rect("ChartHeader");
            _chartHost = _view.Rect("AssetChartHost");
            _chartTitle = _view.Text("ChartTitleText");
            _resetRect = _view.Button("ResetChartButton").GetComponent<RectTransform>();
            _disclaimer = _view.Text("DisclaimerText");
            _leftLegend = _chartHeader.Find("Legend/Cash/Label").GetComponent<Text>();
            _rightLegend = _chartHeader.Find("Legend/Goods/Label").GetComponent<Text>();
            _ledgerStroke = _chartHeader.Find("Legend/Goods/Stroke").GetComponent<Image>();
            _rateColors = RankingLayout.Rect("RateColors", _ledgerStroke.transform);
            RankingLayout.Stretch(_rateColors);
            InventoryPaper.Bar("BelowZero", _rateColors, 0, 0, .43f, 1, 0, 0, 0, 0, UiFormatting.Green);
            InventoryPaper.Bar("Zero", _rateColors, .43f, 0, .57f, 1, 0, 0, 0, 0, UiFormatting.Muted);
            InventoryPaper.Bar("AboveZero", _rateColors, .57f, 0, 1, 1, 0, 0, 0, 0, UiFormatting.Red);
            _switchButton = AssetChartSwitch.Create(_chartHeader, services);
            _switchButton.onClick.AddListener(SwitchChart);
            services.Overlays.AttachTooltip(_switchButton.gameObject,
                () => _showProfit ? "查看资产留痕" : "查看盈亏走势",
                () => _showProfit ? "查看各市场周记录的金钱和持货市值。" : "查看各市场周的总盈亏和总收益率。",
                () => string.Empty, false);
            _emptyChart = RankingLayout.Text("EmptyChart",_chartHost,"",15,UiFormatting.Muted,
                services.Assets.RegularFont,TextAnchor.MiddleCenter);
            RankingLayout.Stretch(_emptyChart.rectTransform);
            _emptyChart.raycastTarget = false;

            for (int i = 0; i < MetricCount; i++)
            {
                _metricValues[i] = _view.Text(MetricKeys[i]);
                _metricCaptions[i] = _view.Text(CaptionKeys[i]);
                _metricAccents[i] = _view.Image(AccentKeys[i]);
                _cardLayouts[i] = _metricValues[i].transform.parent.GetComponent<LayoutElement>();
                var card = _cardLayouts[i].GetComponent<Image>();
                InventoryPaper.Face(card,new Color32(247,242,229,185));
                InventoryPaper.Frame(card.transform,InventoryPaper.Border,1,InventoryPaper.Border);
                _metricAccents[i].transform.SetAsLastSibling();
                if (services.Assets.RegularFont != null)
                    _metricCaptions[i].font = services.Assets.RegularFont;
                if (services.Assets.BoldFont != null)
                    _metricValues[i].font = services.Assets.BoldFont;
            }

            Font labelFont = services.Assets.RegularFont != null
                ? services.Assets.RegularFont
                : _metricCaptions[0].font;
            _chart = AssetChartGraphic.Mount(_chartHost, labelFont, services.Assets.DryBrushTexture);
            _chart.Chart.PlotPadding = new Vector4(64f, 34f, 76f, 34f);
            _goodsAxis = AssetSecondaryAxis.Mount(_view.Rect("GoodsAxisHost"), _chart.Chart, labelFont);
            InventoryPaper.Face(_chartPanel.GetComponent<Image>(),InventoryPaper.Section);
            InventoryPaper.Frame(_chartPanel,InventoryPaper.Border,1,InventoryPaper.Border);
            _view.Button("ResetChartButton").onClick.AddListener(ResetView);
            AttachMetricTooltips(services);
            _layoutDriver = _view.GameObject.AddComponent<AssetsPageLayoutDriver>();
            _layoutDriver.Owner = this;
            SetActive(false);
        }

        internal GameObject GameObject { get { return _view.GameObject; } }
        internal bool IsActive { get { return _view.IsAlive && GameObject.activeSelf; } }
        internal Vector2 LayoutSize { get { return _view.Root.rect.size; } }

        internal void SetActive(bool active)
        {
            if (active && !_view.GameObject.activeSelf) _showProfit = true;
            _view.SetActive(active);
        }

        internal void ResetView()
        {
            _chart.ResetView();
            _goodsAxis.RefreshNow();
        }

        internal void Refresh(AssetSnapshot snapshot)
        {
            if (!IsActive)
                return;
            _snapshot = snapshot;
            if (snapshot == null)
            {
                for (int i = 0; i < MetricCount; i++)
                    SetMetric(i, "—", UiFormatting.Muted, UiFormatting.Muted);
            }
            else
            {
                SetMetric(0, MoneyWithUnit(snapshot.Cash), UiFormatting.Ink, AssetGold);
                SetMetric(1, MoneyWithUnit(snapshot.LongValue), UiFormatting.Green, UiFormatting.Green);
                SetMetric(2, MoneyWithUnit(snapshot.NetAssets), UiFormatting.Ink, AssetGold);
                bool complete = snapshot.HasCompletePrices && snapshot.IsRealizedProfitComplete;
                SetMetric(3, complete ? UiFormatting.SignedMoney(snapshot.TotalProfit) + "<size=12> 文</size>" : "—", UiFormatting.Profit(snapshot.TotalProfit), UiFormatting.Profit(snapshot.TotalProfit));
                SetMetric(4, complete && snapshot.CapitalBasis > 0 ? UiFormatting.Percent(snapshot.ReturnRate) : "—", UiFormatting.Profit(snapshot.ReturnRate), UiFormatting.Profit(snapshot.ReturnRate));
            }

            int currentWeek = MarketQuery.CurrentWeek(_state);
            IReadOnlyList<AssetPoint> history = _application.GetAssetHistory();
            IReadOnlyList<AssetPoint> points = _showProfit && _application.Assets != null
                ? _application.Assets.ProfitHistory(history,snapshot) : history;
            int count = _chart.SetPoints(points, currentWeek, _showProfit);
            _chartTitle.text = _showProfit ? "总盈亏走势" : "行商资产留痕";
            _ledgerStroke.enabled = !_showProfit;
            _rateColors.gameObject.SetActive(_showProfit);
            _leftLegend.text = _showProfit ? "总盈亏 · 文" : "金钱 · 左轴";
            _rightLegend.text = _showProfit ? "总收益率 · %" : "持货 · 右轴";
            PositionChartSwitch();
            _goodsAxis.RefreshNow();
            _emptyChart.text = _showProfit ? "暂无可核对的盈亏记录" : "尚无资产留痕";
            _emptyChart.gameObject.SetActive(count == 0);
            if (count == 0) _emptyChart.transform.SetAsLastSibling();
            string notice = _showProfit ? ProfitCaption : BaseDisclaimer;
            if (count == 1) notice = "仅一周有记录。 " + notice;
            if (_showProfit && snapshot != null && (!snapshot.HasCompletePrices || !snapshot.IsRealizedProfitComplete))
                notice += " 本周估值或成交记录不全。";
            if (_showProfit && HasUnknownWeeks(history,points))
                notice += " 无法核对的旧周留空。";
            _disclaimer.text = notice;
        }

        private static bool HasUnknownWeeks(IReadOnlyList<AssetPoint> history, IReadOnlyList<AssetPoint> plotted)
        {
            var weeks = new HashSet<int>();
            for (int i = 0; i < plotted.Count; i++) weeks.Add(plotted[i].MarketWeekIndex);
            for (int i = 0; i < history.Count; i++)
                if (history[i] != null && !weeks.Contains(history[i].MarketWeekIndex)) return true;
            return false;
        }

        private void SwitchChart()
        {
            _showProfit = !_showProfit;
            var tooltip = _switchButton.GetComponent<UiTooltipTrigger>();
            if (tooltip != null) tooltip.InvalidateBinding();
            Refresh(_snapshot);
        }

        private void PositionChartSwitch()
        {
            float x = 18f + Mathf.Ceil(_chartTitle.preferredWidth) + 12f;
            float headerHeight = _compact ? 34f : 38f;
            InventoryPaper.Box(_chartTitle.rectTransform,18f,0,Mathf.Ceil(_chartTitle.preferredWidth)+1f,headerHeight);
            InventoryPaper.Box(_switchButton.image.rectTransform,x,(headerHeight-30f)*.5f,30f,30f);
        }

        internal void ApplyLayoutProfile(bool compact)
        {
            _compact = compact;
            float gap = compact ? 8f : 12f;
            float cardHeight = compact ? 57f : 66f;
            float disclaimerHeight = compact ? 30f : 34f;
            float headerHeight = compact ? 34f : 38f;
            _cardsLayout.spacing = compact ? 7f : 10f;
            SetTopStretch(_cardsLayout.GetComponent<RectTransform>(), 0f, cardHeight);
            SetTopStretch(_chartHeader, 0f, headerHeight);
            _chartPanel.offsetMin = new Vector2(_chartPanel.offsetMin.x, disclaimerHeight + gap);
            _chartPanel.offsetMax = new Vector2(_chartPanel.offsetMax.x, -cardHeight - gap);
            _chartHost.offsetMin = new Vector2(14f, compact ? 8f : 12f);
            _chartHost.offsetMax = new Vector2(-14f, -headerHeight);
            _disclaimer.rectTransform.offsetMin = new Vector2(4f, 0f);
            _disclaimer.rectTransform.offsetMax = new Vector2(-4f, disclaimerHeight);
            _chartTitle.fontSize = compact ? 13 : 14;
            PositionChartSwitch();
            _resetRect.sizeDelta = new Vector2(48f, 25f);
            _resetRect.anchoredPosition = new Vector2(_resetRect.anchoredPosition.x,-(headerHeight-25f)*.5f);

            for (int i = 0; i < MetricCount; i++)
            {
                _cardLayouts[i].preferredHeight = cardHeight;
                _metricCaptions[i].fontSize = 10;
                _metricValues[i].fontSize = 26;
                SetTopStretch(_metricCaptions[i].rectTransform, compact ? 4f : 6f, 16f);
                SetTopStretch(_metricValues[i].rectTransform, compact ? 20f : 24f, compact ? 32f : 36f);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_view.Root);
            _metricsDirty = true;
        }

        internal void FitMetricsIfNeeded()
        {
            if (!_metricsDirty)
                return;
            bool waitingForLayout = false;
            int minimumSize = _compact ? 14 : 15;
            for (int i = 0; i < MetricCount; i++)
            {
                Text text = _metricValues[i];
                float width = text.rectTransform.rect.width;
                if (width <= 1f)
                {
                    waitingForLayout = true;
                    continue;
                }
                text.fontSize = 26;
                float preferredWidth = text.preferredWidth;
                if (preferredWidth > width && preferredWidth > 0f)
                    text.fontSize = Math.Max(minimumSize, Mathf.FloorToInt(26f * width / preferredWidth));
            }
            _metricsDirty = waitingForLayout;
        }

        private static void SetTopStretch(RectTransform rect, float top, float height)
        {
            rect.offsetMin = new Vector2(rect.offsetMin.x, -top - height);
            rect.offsetMax = new Vector2(rect.offsetMax.x, -top);
        }

        private static string MoneyWithUnit(double value)
        {
            return UiFormatting.Money(value) + "<size=12> 文</size>";
        }

        private void SetMetric(int index, string value, Color color, Color accentColor)
        {
            Text text = _metricValues[index];
            text.text = value;
            text.color = color;
            _metricAccents[index].color = accentColor;
            _metricsDirty = true;
        }

        private void AttachMetricTooltips(UiFrontendServices services)
        {
            AttachMetricTooltip(services, "CashText", "随身金钱", delegate
            {
                return _snapshot == null
                    ? "原版银两余额暂不可读；本页不会存取现金。"
                    : "原版银两 " + UiFormatting.Money(_snapshot.Cash) + " 文；Mod 欠款 " +
                      UiFormatting.Money(_snapshot.Debt) + " 文。";
            });
            AttachMetricTooltip(services, "GoodsText", "货物总产值", delegate
            {
                return _snapshot == null
                    ? "持仓货值暂不可读。"
                    : "多头货值 " + UiFormatting.Money(_snapshot.LongValue) + " 文；多头成本 " +
                      UiFormatting.Money(_snapshot.LongCost) + " 文；空头买回负债 " +
                      UiFormatting.Money(_snapshot.ShortLiability) + " 文。";
            });
            AttachMetricTooltip(services, "EquityText", "净总资产", delegate
            {
                return _snapshot == null
                    ? "资产暂不可读。"
                    : "金钱 + 持货市值 − 待还借货市值 − 欠款 = " +
                      UiFormatting.Money(_snapshot.NetAssets) + " 文。";
            });
            AttachMetricTooltip(services, "ProfitText", "累计盈亏", delegate
            {
                return _snapshot == null
                    ? "交易账目暂不可读。"
                    : "已实现 " + UiFormatting.SignedMoney(_snapshot.RealizedProfit) +
                      " + 浮动 " + UiFormatting.SignedMoney(_snapshot.FloatingProfit) +
                      " = " + UiFormatting.SignedMoney(_snapshot.TotalProfit) + " 文。";
            });
            AttachMetricTooltip(services, "ReturnText", "收益率", delegate
            {
                return _snapshot == null
                    ? "投入本金暂不可读。"
                    : "累计盈亏 ÷（累计买入成本 + 累计借货实收）。尚无投入时显示破折号。";
            });
        }

        private void AttachMetricTooltip(
            UiFrontendServices services,
            string key,
            string title,
            Func<string> body)
        {
            Transform card = _view.Text(key).transform.parent;
            services.Overlays.AttachTooltip(card.gameObject, () => title, body, () => "只读资产分析");
        }

        public void Dispose()
        {
            if (_layoutDriver != null)
            {
                _layoutDriver.Owner = null;
                UnityEngine.Object.Destroy(_layoutDriver);
            }
            if (_goodsAxis != null)
            {
                _goodsAxis.Dispose();
                UnityEngine.Object.Destroy(_goodsAxis);
            }
            if (_view.IsAlive)
                _view.Button("ResetChartButton").onClick.RemoveListener(ResetView);
            if (_switchButton != null) _switchButton.onClick.RemoveListener(SwitchChart);
            _snapshot = null;
            _view.Dispose();
        }
    }

    internal sealed class AssetsPageLayoutDriver : MonoBehaviour
    {
        internal AssetsPage Owner;
        private bool _initialized;
        private bool _compact;
        private int _screenWidth;
        private int _screenHeight;
        private Vector2 _layoutSize;

        private void OnEnable()
        {
            _initialized = false;
        }

        private void LateUpdate()
        {
            AssetsPage owner = Owner;
            if (owner == null)
                return;
            Vector2 size = owner.LayoutSize;
            bool compact = Screen.width > 0 && Screen.height > 0
                ? Screen.width <= 1450 || Screen.height <= 790
                : size.x <= 1500f || size.y <= 650f;
            bool changed = !_initialized || compact != _compact ||
                           Screen.width != _screenWidth || Screen.height != _screenHeight ||
                           (size - _layoutSize).sqrMagnitude > 0.01f;
            if (changed)
            {
                owner.ApplyLayoutProfile(compact);
                _initialized = true;
                _compact = compact;
                _screenWidth = Screen.width;
                _screenHeight = Screen.height;
                _layoutSize = size;
            }
            owner.FitMetricsIfNeeded();
        }
    }

    [DefaultExecutionOrder(100)]
    internal sealed class AssetSecondaryAxis : MonoBehaviour
    {
        private const int MaxTickCount = 6;
        private static readonly Color32 GoodsColor = new Color32(63, 107, 88, 232);
        private static readonly Color32 CashColor = new Color32(180, 147, 88, 255);

        private WeeklyChartGraphic _chart;
        private RectTransform _host;
        private AssetAxisGraphic _graphic;
        private Text[] _labels;
        private Text _title;
        private bool _force = true;
        private float _lastStart;
        private float _lastZoom;
        private float _lastCenter;
        private int _lastRange = -1;
        private int _lastCount = -1;
        private int _lastSelected = -2;
        private Rect _lastRect;
        private Vector4 _lastPadding;
        private bool _disposed;

        internal static AssetSecondaryAxis Mount(RectTransform host, WeeklyChartGraphic chart, Font font)
        {
            EnsureCanvasRenderer(host.gameObject);
            AssetAxisGraphic graphic = host.gameObject.AddComponent<AssetAxisGraphic>();
            graphic.raycastTarget = false;
            graphic.color = Color.white;
            graphic.raycastTarget = false;
            AssetSecondaryAxis axis = host.gameObject.AddComponent<AssetSecondaryAxis>();
            axis._host = host;
            axis._chart = chart;
            axis._graphic = graphic;
            axis._labels = new Text[MaxTickCount];
            for (int i = 0; i < axis._labels.Length; i++)
                axis._labels[i] = CreateText("GoodsAxisTick" + i, host, font);
            axis._title = CreateText("GoodsAxisTitle", host, font);
            axis._title.text = "货物右轴";
            host.SetAsLastSibling();
            axis.SetEmpty();
            return axis;
        }

        internal void RefreshNow()
        {
            if (_disposed)
                return;
            _force = true;
            if (isActiveAndEnabled)
                RefreshAxis();
        }

        private void OnEnable()
        {
            _force = true;
        }

        private void LateUpdate()
        {
            if (_disposed || _chart == null)
                return;
            _chart.StyleAssetPrimaryAxis(CashColor);
            Rect rect = _chart.rectTransform.rect;
            Vector4 padding = _chart.PlotPadding;
            if (!_force && _lastRange == _chart.VisibleRange && _lastCount == _chart.TotalPointCount &&
                _lastSelected == _chart.SelectedIndex &&
                Mathf.Approximately(_lastStart, _chart.VisibleStart) &&
                Mathf.Approximately(_lastZoom, _chart.YZoomFactor) &&
                Mathf.Approximately(_lastCenter, _chart.AssetYZoomCenterRatio) &&
                _lastRect == rect && _lastPadding == padding)
                return;
            RefreshAxis();
        }

        private void RefreshAxis()
        {
            if (_disposed)
                return;
            _force = false;
            if (_chart == null)
            {
                SetEmpty();
                return;
            }
            _lastStart = _chart.VisibleStart;
            _lastZoom = _chart.YZoomFactor;
            _lastCenter = _chart.AssetYZoomCenterRatio;
            _lastRange = _chart.VisibleRange;
            _lastCount = _chart.TotalPointCount;
            _lastSelected = _chart.SelectedIndex;
            _lastRect = _chart.rectTransform.rect;
            _lastPadding = _chart.PlotPadding;
            _chart.StyleAssetPrimaryAxis(CashColor);

            Rect plot;
            double minimum;
            double maximum;
            int tickCount;
            if (!_chart.TryGetAssetAxisScale(1, out plot, out minimum, out maximum, out tickCount))
            {
                SetEmpty();
                return;
            }

            _graphic.SetData(plot, minimum, maximum, tickCount, _chart.IsProfitChart);
            double span = maximum - minimum;
            for (int i = 0; i < _labels.Length; i++)
            {
                Text label = _labels[i];
                if (i >= tickCount)
                {
                    label.gameObject.SetActive(false);
                    continue;
                }
                double value = WeeklyChartGraphic.AxisTick(minimum,maximum,i,tickCount,_chart.IsProfitChart);
                float normalized = span == 0.0 ? 0.5f : (float)((maximum - value) / span);
                float y = plot.yMax - normalized * plot.height;
                label.text = WeeklyChartGraphic.FormatValue(value) + (_chart.IsProfitChart ? "%" : "");
                label.fontSize = 11;
                label.color = _chart.IsProfitChart ? UiFormatting.Muted : (Color)GoodsColor;
                float width = Math.Max(1f, label.preferredWidth) + 2f;
                PlaceText(label, new Vector2(plot.xMax + 8f, y), new Vector2(0f, 0.5f), new Vector2(width, 16f));
            }
            _title.text = _chart.IsProfitChart ? "收益率 · 右轴" : "货物 · 右轴";
            _title.color = _chart.IsProfitChart ? UiFormatting.Muted : (Color)GoodsColor;
            PlaceText(
                _title,
                new Vector2(_host.rect.xMax - 4f, _host.rect.yMax - 8f),
                new Vector2(1f, 1f),
                new Vector2(Math.Max(1f, _title.preferredWidth) + 2f, 18f));
        }

        internal void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            SetEmpty();
            ReleaseReferences();
        }

        private void OnDestroy()
        {
            _disposed = true;
            ReleaseReferences();
        }

        private void ReleaseReferences()
        {
            _chart = null;
            _host = null;
            _graphic = null;
            _labels = null;
            _title = null;
        }

        private void SetEmpty()
        {
            if (_graphic != null)
                _graphic.ClearData();
            if (_labels != null)
            {
                for (int i = 0; i < _labels.Length; i++)
                {
                    if (_labels[i] != null)
                        _labels[i].gameObject.SetActive(false);
                }
            }
            if (_title != null)
                _title.gameObject.SetActive(false);
        }

        private static void EnsureCanvasRenderer(GameObject target)
        {
            if (target.GetComponent<CanvasRenderer>() == null)
                target.AddComponent<CanvasRenderer>();
        }

        private static Text CreateText(string name, Transform parent, Font font)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.localScale = Vector3.one;
            EnsureCanvasRenderer(gameObject);
            Text text = gameObject.AddComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 11;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void PlaceText(Text label, Vector2 localPosition, Vector2 pivot, Vector2 size)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = localPosition - _host.rect.min;
            label.gameObject.SetActive(true);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class AssetAxisGraphic : MaskableGraphic
    {
        private Rect _plot;
        private double _minimum;
        private double _maximum;
        private int _tickCount;
        private bool _hasData, _markZero;

        internal void SetData(Rect plot, double minimum, double maximum, int tickCount, bool markZero)
        {
            _plot = plot;
            _minimum = minimum;
            _maximum = maximum;
            _tickCount = tickCount;
            _markZero = markZero;
            _hasData = true;
            SetVerticesDirty();
        }

        internal void ClearData()
        {
            _hasData = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!_hasData || _plot.height <= 0f || _tickCount < 2)
                return;
            Color32 color = _markZero ? new Color32(103,106,101,178) : new Color32(63, 107, 88, 178);
            WeeklyChartMesh.AddLine(
                vertexHelper,
                new Vector2(_plot.xMax, _plot.yMin),
                new Vector2(_plot.xMax, _plot.yMax),
                0.8f,
                color,
                color);
            double span = _maximum - _minimum;
            for (int i = 0; i < _tickCount; i++)
            {
                double value = WeeklyChartGraphic.AxisTick(_minimum,_maximum,i,_tickCount,_markZero);
                float normalized = span == 0.0 ? 0.5f : (float)((_maximum - value) / span);
                float y = _plot.yMax - normalized * _plot.height;
                WeeklyChartMesh.AddLine(
                    vertexHelper,
                    new Vector2(_plot.xMax, y),
                    new Vector2(_plot.xMax + 4f, y),
                    0.75f,
                    color,
                    color);
            }
        }
    }
}

namespace XingShangMod.UI.Charting
{
    public sealed partial class WeeklyChartGraphic
    {
        internal float AssetYZoomCenterRatio
        {
            get { return _yZoomCenterRatio; }
        }

        internal bool TryGetAssetAxisScale(
            int seriesIndex,
            out Rect plot,
            out double minimum,
            out double maximum,
            out int tickCount)
        {
            plot = GetPlotRect();
            minimum = 0.0;
            maximum = 1.0;
            tickCount = 0;
            if (_mode != WeeklyChartMode.Assets || seriesIndex < 0 || seriesIndex >= _series.Count || _timeline.Count == 0)
                return false;

            int first;
            int last;
            VisibleBounds(true, out first, out last);
            IList<WeeklyPoint> points = _series[seriesIndex].AlignedPoints;
            bool hasValues = false;
            for (int i = first; i <= last; i++)
            {
                if (i < 0 || i >= points.Count)
                    continue;
                WeeklyPoint point = points[i];
                if (point == null || double.IsNaN(point.Price) || double.IsInfinity(point.Price))
                    continue;
                hasValues = true;
                break;
            }
            if (!hasValues)
                return false;

            GetSeriesScale(_series[seriesIndex], plot, out minimum, out maximum);
            if (double.IsNaN(minimum) || double.IsInfinity(minimum) ||
                double.IsNaN(maximum) || double.IsInfinity(maximum) || maximum <= minimum)
                return false;
            tickCount = _horizontalGridLines > 0
                ? Math.Max(2, _horizontalGridLines)
                : Mathf.Clamp(Mathf.FloorToInt(plot.height / 42f) + 1, 3, MaxAssetAxisTickCount);
            return true;
        }

        internal void StyleAssetPrimaryAxis(Color32 axisColor)
        {
            if (_mode != WeeklyChartMode.Assets)
                return;
            if (_timeline.Count == 0)
            {
                HideAssetAxisLabels();
                return;
            }

            RenderContext context = CreateRenderContext();
            if (context.Reference == null)
            {
                HideAssetAxisLabels();
                return;
            }

            int labelIndex = 0;
            List<int> timeTicks = GetTimeTicks(context.Plot);
            for (int i = 0; i < timeTicks.Count; i++)
            {
                if (FindPointAt(timeTicks[i]) == null)
                    continue;
                float x = TimeTickPosition(timeTicks[i], context.Plot);
                if (x >= context.Plot.xMin - 1f && x <= context.Plot.xMax + 1f)
                    labelIndex++;
            }

            double[] prices = GetPriceTicks(context.ReferenceMinimum, context.ReferenceMaximum, context.Plot);
            Color priceColor = Paint(axisColor, 0.92f);
            for (int i = 0; i < prices.Length && labelIndex < _axisLabels.Count; i++)
            {
                Text label = _axisLabels[labelIndex++];
                if (label.color != priceColor)
                    label.color = priceColor;
            }
            if (labelIndex < _axisLabels.Count)
            {
                Text note = _axisLabels[labelIndex];
                string axis = IsProfitChart ? "总盈亏 · 文 · 左轴" : "金钱 · 文 · 左轴";
                string text = _timeline.Count == 1 ? "仅一周 · " + axis : axis;
                if (note.text != text)
                    note.text = text;
                Color noteColor = Paint(axisColor, 0.9f);
                if (note.color != noteColor)
                    note.color = noteColor;
            }
        }

        private const int MaxAssetAxisTickCount = 6;

        private void HideAssetAxisLabels()
        {
            for (int i = 0; i < _axisLabels.Count; i++)
                _axisLabels[i].gameObject.SetActive(false);
        }
    }
}
