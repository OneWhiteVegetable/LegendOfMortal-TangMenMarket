using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Charting
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed partial class WeeklyChartGraphic : MaskableGraphic,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        private readonly List<ChartSeriesData> _series = new List<ChartSeriesData>();
        private readonly List<int> _timeline = new List<int>();
        private readonly List<Text> _axisLabels = new List<Text>();
        private readonly List<ChartTag> _dataTags = new List<ChartTag>();
        private readonly List<Text> _hoverRows = new List<Text>();
        private List<WeeklyPoint> _points = new List<WeeklyPoint>();
        private ReadOnlyCollection<WeeklyChartSegment> _segments = WeeklyChartSegmentBuilder.Empty;
        private WeeklyChartMode _mode = WeeklyChartMode.Card;
        private string _styleSeed = string.Empty;
        private int _presetRange = WeeklyChartData.DetailPoints;
        private bool _hasCustomRange;
        private Color32 _risingColor = WeeklyChartSegmentBuilder.DefaultRisingColor;
        private Color32 _fallingColor = WeeklyChartSegmentBuilder.DefaultFallingColor;
        private Color32 _flatColor = WeeklyChartSegmentBuilder.DefaultFlatColor;
        private Color32 _gridColor = new Color32(117, 107, 89, 87);
        private Color32 _crosshairColor = new Color32(98, 89, 75, 158);
        private Color32 _labelColor = new Color32(67, 59, 48, 255);
        private Vector4 _plotPadding = new Vector4(12f, 28f, 12f, 12f);
        private float _lineWidth = 1.85f;
        private float _gridLineWidth = 0.8f;
        private int _horizontalGridLines;
        private int _verticalGridLines;
        private int _labelFontSize = 10;
        private Font _axisLabelFont;
        private bool _interactionEnabled;
        private bool _showLatestLabel;
        private bool _showRangeLabels;
        private Font _labelFont;
        private Texture2D _dryBrushTexture;
        private RectTransform _plotClip;
        private WeeklyChartMeshGraphic _plotGraphic;
        private WeeklyChartMeshGraphic _overlayGraphic;
        private ChartTag _hoverTag;
        private float _viewStart;
        private int _viewCount = 1;
        private float _yZoomFactor = 1f;
        private float _yZoomCenterRatio = 0.5f;
        private bool _pointerInside;
        private Camera _pointerCamera;
        private int _selectedIndex = -1;
        private Vector2 _lastPointerLocal;
        private bool _hasPointerLocal;
        private DragState _drag;
        private bool _hasVisibleRange;
        private double _visibleMinimum;
        private double _visibleMaximum;
        private int _visibleLowPointIndex = -1;
        private int _visibleHighPointIndex = -1;
        private Color _lastGraphicColor;
        private bool _hasGraphicColor;
        private bool _isDestroying;

        public static WeeklyChartGraphic Create(
            Transform parent,
            WeeklyChartMode mode = WeeklyChartMode.Detail,
            Font labelFont = null)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            var gameObject = new GameObject("XingShangMod.WeeklyChart", typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(480f, 240f);
            rect.localScale = Vector3.one;

            EnsureCanvasRenderer(gameObject);
            WeeklyChartGraphic chart = gameObject.AddComponent<WeeklyChartGraphic>();
            chart._labelFont = labelFont;
            chart._mode = mode;
            chart.ApplyModeDefaults();
            chart.UpdateVisuals();
            return chart;
        }

        public static WeeklyChartGraphic Mount(
            RectTransform host,
            WeeklyChartMode mode,
            Font labelFont,
            Texture2D dryBrushTexture,
            Font axisLabelFont = null)
        {
            if (host == null)
                throw new ArgumentNullException("host");
            if (host.GetComponent<WeeklyChartGraphic>() != null)
                throw new InvalidOperationException("The chart host is already mounted.");
            EnsureCanvasRenderer(host.gameObject);
            WeeklyChartGraphic chart = host.gameObject.AddComponent<WeeklyChartGraphic>();
            chart._labelFont = labelFont;
            chart._axisLabelFont = axisLabelFont;
            chart._dryBrushTexture = dryBrushTexture;
            chart._mode = mode;
            chart.ApplyModeDefaults();
            if (chart._plotGraphic != null)
            {
                chart._plotGraphic.DryBrush = dryBrushTexture;
                chart._plotGraphic.SetMaterialDirty();
            }
            chart.UpdateVisuals();
            return chart;
        }

        public WeeklyChartMode Mode
        {
            get { return _mode; }
            set
            {
                if (_mode == value)
                    return;
                _mode = value;
                ApplyModeDefaults();
                ApplySeriesStyleKeys();
                CancelInteraction();
                ResetView();
            }
        }

        public event Action<WeeklyChartGraphic> ViewChanged;

        public string StyleSeed
        {
            get { return _styleSeed; }
            set
            {
                string next = value ?? string.Empty;
                if (string.Equals(_styleSeed, next, StringComparison.Ordinal))
                    return;
                _styleSeed = next;
                ApplySeriesStyleKeys();
                UpdateVisuals();
            }
        }

        public IReadOnlyList<WeeklyChartSegment> Segments
        {
            get { return _segments; }
        }

        public int? ActivePresetRange
        {
            get { return _hasCustomRange ? (int?)null : _presetRange; }
        }

        public bool HasCustomRange
        {
            get { return _hasCustomRange; }
        }

        public bool HasCustomPriceZoom
        {
            get { return WeeklyChartData.HasCustomPriceZoom(_yZoomFactor, _yZoomCenterRatio); }
        }

        public bool IsCustomView
        {
            get { return _hasCustomRange || HasCustomPriceZoom; }
        }

        public int VisiblePointCount
        {
            get
            {
                if (_series.Count == 0 || _timeline.Count == 0)
                    return 0;
                int first;
                int last;
                VisibleBounds(false, out first, out last);
                int count = 0;
                IList<WeeklyPoint> points = _series[0].AlignedPoints;
                for (int i = first; i <= last; i++)
                {
                    if (points[i] != null)
                        count++;
                }
                return count;
            }
        }

        public int TotalPointCount
        {
            get { return _timeline.Count; }
        }

        public int VisibleRange
        {
            get { return _timeline.Count == 0 ? 0 : _viewCount; }
        }

        public float VisibleStart
        {
            get { return _viewStart; }
        }

        public float YZoomFactor
        {
            get { return _yZoomFactor; }
        }

        public double VisibleMinimum
        {
            get { return _hasVisibleRange ? _visibleMinimum : double.NaN; }
        }

        public double VisibleMaximum
        {
            get { return _hasVisibleRange ? _visibleMaximum : double.NaN; }
        }

        public int VisibleLowPointIndex
        {
            get { return _visibleLowPointIndex; }
        }

        public int VisibleHighPointIndex
        {
            get { return _visibleHighPointIndex; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
        }

        public float LineWidth
        {
            get { return _lineWidth; }
            set
            {
                value = Mathf.Max(0f, value);
                if (_lineWidth == value)
                    return;
                _lineWidth = value;
                UpdateVisuals();
            }
        }

        internal float MainStrokeOpacity
        {
            get
            {
                return _mode == WeeklyChartMode.Card
                    ? 1f
                    : _mode == WeeklyChartMode.Spark ? 0.88f : _mode == WeeklyChartMode.Detail ? 1f : 0.94f;
            }
        }

        internal float UnderStrokeOpacity
        {
            get { return _mode == WeeklyChartMode.Card ? 0.07f : 0.13f; }
        }

        internal float UnderStrokeAdditionalWidth
        {
            get { return _mode == WeeklyChartMode.Spark ? 0.75f : 1.35f; }
        }

        public float GridLineWidth
        {
            get { return _gridLineWidth; }
            set
            {
                value = Mathf.Max(0f, value);
                if (_gridLineWidth == value)
                    return;
                _gridLineWidth = value;
                UpdateVisuals();
            }
        }

        public int HorizontalGridLines
        {
            get { return _horizontalGridLines; }
            set
            {
                value = Math.Max(0, value);
                if (_horizontalGridLines == value)
                    return;
                _horizontalGridLines = value;
                UpdateVisuals();
            }
        }

        public int VerticalGridLines
        {
            get { return _verticalGridLines; }
            set
            {
                value = Math.Max(0, value);
                if (_verticalGridLines == value)
                    return;
                _verticalGridLines = value;
                UpdateVisuals();
            }
        }

        public Vector4 PlotPadding
        {
            get { return _plotPadding; }
            set
            {
                value = new Vector4(
                    Mathf.Max(0f, value.x),
                    Mathf.Max(0f, value.y),
                    Mathf.Max(0f, value.z),
                    Mathf.Max(0f, value.w));
                if (_plotPadding == value)
                    return;
                _plotPadding = value;
                UpdateVisuals();
            }
        }

        public Color32 GridColor
        {
            get { return _gridColor; }
            set
            {
                if (_gridColor.Equals(value))
                    return;
                _gridColor = value;
                UpdateVisuals();
            }
        }

        public Color32 CrosshairColor
        {
            get { return _crosshairColor; }
            set
            {
                if (_crosshairColor.Equals(value))
                    return;
                _crosshairColor = value;
                UpdateVisuals();
            }
        }

        public Color32 LabelColor
        {
            get { return _labelColor; }
            set
            {
                if (_labelColor.Equals(value))
                    return;
                _labelColor = value;
                UpdateVisuals();
            }
        }

        public Color32 RisingColor
        {
            get { return _risingColor; }
            set
            {
                if (_risingColor.Equals(value))
                    return;
                _risingColor = value;
                RebuildSegments();
                UpdateVisuals();
            }
        }

        public Color32 FallingColor
        {
            get { return _fallingColor; }
            set
            {
                if (_fallingColor.Equals(value))
                    return;
                _fallingColor = value;
                RebuildSegments();
                UpdateVisuals();
            }
        }

        public Color32 FlatColor
        {
            get { return _flatColor; }
            set
            {
                if (_flatColor.Equals(value))
                    return;
                _flatColor = value;
                RebuildSegments();
                UpdateVisuals();
            }
        }

        public Font LabelFont
        {
            get { return _labelFont; }
            set
            {
                if (_labelFont == value)
                    return;
                _labelFont = value;
                ApplyLabelFont();
                UpdateVisuals();
            }
        }

        public Font AxisLabelFont
        {
            get { return _axisLabelFont; }
            set
            {
                if (_axisLabelFont == value)
                    return;
                _axisLabelFont = value;
                ApplyLabelFont();
                UpdateVisuals();
            }
        }

        public int LabelFontSize
        {
            get { return _labelFontSize; }
            set
            {
                value = Math.Max(1, value);
                if (_labelFontSize == value)
                    return;
                _labelFontSize = value;
                ApplyLabelFontSize();
                UpdateVisuals();
            }
        }

        public bool InteractionEnabled
        {
            get { return _interactionEnabled; }
            set
            {
                if (_interactionEnabled == value)
                    return;
                _interactionEnabled = value;
                raycastTarget = value;
                if (!value)
                {
                    CancelInteraction();
                    DisposeCursorResources();
                }
                UpdateVisuals();
            }
        }

        public bool ShowLatestLabel
        {
            get { return _showLatestLabel; }
            set
            {
                if (_showLatestLabel == value)
                    return;
                _showLatestLabel = value;
                UpdateVisuals();
            }
        }

        public bool ShowRangeLabels
        {
            get { return _showRangeLabels; }
            set
            {
                if (_showRangeLabels == value)
                    return;
                _showRangeLabels = value;
                UpdateVisuals();
            }
        }

        public void SetPoints(List<WeeklyPoint> visiblePoints)
        {
            if (visiblePoints == null)
                throw new ArgumentNullException("visiblePoints");
            string id = "series-1";
            for (int i = 0; i < visiblePoints.Count; i++)
            {
                if (visiblePoints[i] != null && !string.IsNullOrWhiteSpace(visiblePoints[i].ItemId))
                {
                    id = visiblePoints[i].ItemId;
                    break;
                }
            }
            SetSeries(new List<WeeklyChartSeries>
            {
                new WeeklyChartSeries(id, string.Empty, visiblePoints)
            });
        }

        public void SetPoints(List<WeeklyPoint> visiblePoints, int currentWeek)
        {
            SetPoints(WeeklyChartData.VisibleThrough(visiblePoints, currentWeek));
        }

        public void SetSeries(IList<WeeklyChartSeries> visibleSeries, int currentWeek)
        {
            if (visibleSeries == null)
                throw new ArgumentNullException("visibleSeries");
            var filtered = new List<WeeklyChartSeries>(visibleSeries.Count);
            for (int i = 0; i < visibleSeries.Count; i++)
            {
                WeeklyChartSeries source = visibleSeries[i];
                if (source == null)
                    throw new ArgumentException("Visible series " + i + " is null.", "visibleSeries");
                filtered.Add(new WeeklyChartSeries(
                    source.Id,
                    source.Label,
                    WeeklyChartData.VisibleThrough(source.VisiblePoints, currentWeek),
                    source.Color));
            }
            SetSeries(filtered);
        }

        public void SetSeries(IList<WeeklyChartSeries> visibleSeries)
        {
            if (visibleSeries == null)
                throw new ArgumentNullException("visibleSeries");
            var next = new List<ChartSeriesData>(visibleSeries.Count);
            var weeks = new HashSet<int>();
            for (int i = 0; i < visibleSeries.Count; i++)
            {
                WeeklyChartSeries source = visibleSeries[i];
                if (source == null)
                    throw new ArgumentException("Visible series " + i + " is null.", "visibleSeries");
                ChartSeriesData item = NormalizeSeries(source, i, weeks);
                next.Add(item);
            }
            LoadSeries(next, weeks);
        }

        public void SetAssetPoints(List<WeeklyPoint> cashPoints, List<WeeklyPoint> goodsPoints)
        {
            if (Mode != WeeklyChartMode.Assets)
                Mode = WeeklyChartMode.Assets;
            SetSeries(new List<WeeklyChartSeries>
            {
                new WeeklyChartSeries("cash", "金钱", cashPoints, new Color32(180, 147, 88, 255)),
                new WeeklyChartSeries("goods", "货物总产值", goodsPoints, new Color32(63, 107, 88, 255))
            });
        }

        internal bool IsProfitChart { get { return _mode == WeeklyChartMode.Assets && _series.Count > 0 && _series[0].Id == "profit"; } }

        internal void SetProfitPoints(List<WeeklyPoint> profit, List<WeeklyPoint> rate)
        {
            if (Mode != WeeklyChartMode.Assets) Mode = WeeklyChartMode.Assets;
            SetSeries(new List<WeeklyChartSeries>
            {
                new WeeklyChartSeries("profit", "总盈亏", profit, new Color32(180, 147, 88, 255)),
                new WeeklyChartSeries("returnRate", "总收益率", rate, new Color32(63, 107, 88, 255))
            });
        }

        private string SeriesValue(int index, double value)
        {
            if (_mode != WeeklyChartMode.Assets) return FormatValue(value);
            return FormatValue(value) + (IsProfitChart && index == 1 ? "%" : " 文");
        }

        public void ClearPoints()
        {
            _series.Clear();
            _timeline.Clear();
            _points = new List<WeeklyPoint>();
            _segments = WeeklyChartSegmentBuilder.Empty;
            _viewStart = 0f;
            _viewCount = 1;
            _yZoomFactor = 1f;
            _yZoomCenterRatio = 0.5f;
            _presetRange = GetDefaultRange();
            _hasCustomRange = false;
            CancelInteraction();
            UpdateVisuals();
            NotifyViewChanged();
        }

        public void SetRange(int points)
        {
            if (points < 1)
                return;
            _presetRange = points;
            _hasCustomRange = false;
            ChangeRange(points, null);
        }

        public void ResetView()
        {
            WeeklyChartViewport viewport = WeeklyChartData.ResetViewport(_timeline.Count, GetDefaultRange());
            _viewCount = viewport.Count;
            _viewStart = viewport.Start;
            _yZoomFactor = 1f;
            _yZoomCenterRatio = 0.5f;
            _presetRange = GetDefaultRange();
            _hasCustomRange = false;
            SetHoverIndex(-1);
            UpdateVisuals();
            NotifyViewChanged();
        }

        public void SetSelectedIndex(int index)
        {
            if (index < 0 || index >= _timeline.Count)
                throw new ArgumentOutOfRangeException("index");
            _hasPointerLocal = false;
            SetHoverIndex(index);
        }

        public void ClearSelection()
        {
            SetHoverIndex(-1);
        }

        protected override void Awake()
        {
            base.Awake();
            color = Color.white;
            EnsureMeshChildren();
            raycastTarget = _interactionEnabled;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ScheduleLayoutRefresh();
        }

        protected override void OnDisable()
        {
            CancelLayoutRefresh();
            CancelInteraction();
            DisposeCursorResources();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            _isDestroying = true;
            CancelLayoutRefresh();
            CancelInteraction();
            DisposeCursorResources();
            if (_plotGraphic != null)
                _plotGraphic.Owner = null;
            if (_overlayGraphic != null)
                _overlayGraphic.Owner = null;
            _series.Clear();
            _timeline.Clear();
            _points.Clear();
            _segments = WeeklyChartSegmentBuilder.Empty;
            _axisLabels.Clear();
            _dataTags.Clear();
            _hoverRows.Clear();
            _hoverTag = null;
            ViewChanged = null;
            base.OnDestroy();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            ScheduleLayoutRefresh();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
        }

        private void ApplyModeDefaults()
        {
            switch (_mode)
            {
                case WeeklyChartMode.Spark:
                    _lineWidth = 1.45f;
                    _gridLineWidth = 0.8f;
                    _plotPadding = new Vector4(2f, 3f, 2f, 3f);
                    _gridColor = new Color32(105, 93, 75, 168);
                    _labelFontSize = 10;
                    _interactionEnabled = false;
                    _showLatestLabel = false;
                    _showRangeLabels = false;
                    break;
                case WeeklyChartMode.Detail:
                    _lineWidth = 4.2f;
                    _gridLineWidth = 0.8f;
                    _plotPadding = new Vector4(64f, 34f, 24f, 26f);
                    _gridColor = new Color32(105, 93, 75, 168);
                    _labelFontSize = 12;
                    _interactionEnabled = true;
                    _showLatestLabel = true;
                    _showRangeLabels = true;
                    break;
                case WeeklyChartMode.Assets:
                    _lineWidth = 3.1f;
                    _gridLineWidth = 0.8f;
                    _plotPadding = new Vector4(64f, 34f, 24f, 34f);
                    _gridColor = new Color32(105, 93, 75, 168);
                    _labelFontSize = 11;
                    _interactionEnabled = true;
                    _showLatestLabel = true;
                    _showRangeLabels = true;
                    break;
                default:
                    _lineWidth = 1.85f;
                    _gridLineWidth = 0.8f;
                    _plotPadding = new Vector4(12f, 28f, 12f, 12f);
                    _gridColor = new Color32(117, 107, 89, 87);
                    _risingColor = new Color32(182, 64, 54, 255);
                    _fallingColor = new Color32(63, 107, 88, 255);
                    _labelFontSize = 10;
                    _interactionEnabled = false;
                    _showLatestLabel = false;
                    _showRangeLabels = false;
                    break;
            }
            _horizontalGridLines = 0;
            _verticalGridLines = 0;
            raycastTarget = _interactionEnabled;
        }

        private ChartSeriesData NormalizeSeries(WeeklyChartSeries source, int seriesIndex, HashSet<int> weeks)
        {
            var byWeek = new Dictionary<int, WeeklyPoint>();
            for (int i = 0; i < source.VisiblePoints.Count; i++)
            {
                WeeklyPoint copy = WeeklyChartSegmentBuilder.CopyVisiblePoint(source.VisiblePoints[i], i);
                byWeek[copy.MarketWeekIndex] = copy;
                weeks.Add(copy.MarketWeekIndex);
            }
            var ordered = new List<int>(byWeek.Keys);
            ordered.Sort();
            var points = new List<WeeklyPoint>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                points.Add(byWeek[ordered[i]]);
            return new ChartSeriesData
            {
                Id = string.IsNullOrWhiteSpace(source.Id) ? "series-" + (seriesIndex + 1) : source.Id,
                Label = source.Label ?? string.Empty,
                AssetColor = source.Color,
                SourcePoints = points,
                ByWeek = byWeek,
                AlignedPoints = new WeeklyPoint[0]
            };
        }

        private void LoadSeries(List<ChartSeriesData> next, HashSet<int> weekSet)
        {
            int oldCount = _viewCount;
            float oldStart = _viewStart;
            int oldLength = _timeline.Count;
            bool sameIdentity = next.Count == _series.Count && oldLength > 0;
            if (sameIdentity)
            {
                for (int i = 0; i < next.Count; i++)
                {
                    if (!string.Equals(next[i].Id, _series[i].Id, StringComparison.Ordinal))
                    {
                        sameIdentity = false;
                        break;
                    }
                }
            }

            _series.Clear();
            _series.AddRange(next);
            _timeline.Clear();
            _timeline.AddRange(weekSet);
            _timeline.Sort();
            for (int i = 0; i < _series.Count; i++)
            {
                ChartSeriesData item = _series[i];
                item.StyleKey = WeeklyChartData.StyleKey(_styleSeed, _mode, item.Id);
                item.AlignedPoints = new WeeklyPoint[_timeline.Count];
                for (int j = 0; j < _timeline.Count; j++)
                {
                    WeeklyPoint point;
                    if (item.ByWeek.TryGetValue(_timeline[j], out point))
                        item.AlignedPoints[j] = point;
                }
            }
            _points = _series.Count > 0
                ? new List<WeeklyPoint>(_series[0].SourcePoints)
                : new List<WeeklyPoint>();
            RebuildSegments();

            bool preserve = sameIdentity && _timeline.Count > 0;
            if (!preserve)
            {
                _yZoomFactor = 1f;
                _yZoomCenterRatio = 0.5f;
                _presetRange = GetDefaultRange();
                _hasCustomRange = false;
            }
            int length = _timeline.Count;
            WeeklyChartViewport viewport = WeeklyChartData.LoadViewport(
                oldCount,
                oldStart,
                oldLength,
                length,
                preserve,
                GetDefaultRange(),
                _presetRange,
                _hasCustomRange);
            _viewCount = viewport.Count;
            _viewStart = viewport.Start;
            if (length == 0 || (_drag != null && !preserve))
                CancelInteraction();
            SetHoverIndex(-1);
            UpdateVisuals();
            NotifyViewChanged();
        }

        private void ApplySeriesStyleKeys()
        {
            for (int i = 0; i < _series.Count; i++)
                _series[i].StyleKey = WeeklyChartData.StyleKey(_styleSeed, _mode, _series[i].Id);
        }

        private void RebuildSegments()
        {
            _segments = WeeklyChartSegmentBuilder.Build(
                _points,
                _risingColor,
                _fallingColor,
                _flatColor);
        }

        private void ChangeRange(int nextCount, float? anchorRatio)
        {
            int length = _timeline.Count;
            if (length == 0)
            {
                NotifyViewChanged();
                return;
            }
            WeeklyChartViewport viewport = WeeklyChartData.ChangeRange(
                _viewStart,
                _viewCount,
                length,
                nextCount,
                anchorRatio);
            _viewCount = viewport.Count;
            _viewStart = viewport.Start;
            SetHoverIndex(-1);
            UpdateVisuals();
            NotifyViewChanged();
        }

        private int GetDefaultRange()
        {
            return WeeklyChartData.DefaultRange(_mode, _timeline.Count);
        }

        private void NotifyViewChanged()
        {
            if (ViewChanged != null)
                ViewChanged(this);
        }

        private Rect GetPlotRect()
        {
            Rect rect = rectTransform.rect;
            float left = rect.xMin + _plotPadding.x;
            float bottom = rect.yMin + _plotPadding.y;
            float right = rect.xMax - _plotPadding.z;
            float top = rect.yMax - _plotPadding.w;
            if (right < left)
                left = right = (left + right) * 0.5f;
            if (top < bottom)
                bottom = top = (bottom + top) * 0.5f;
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        private void VisibleBounds(bool includeEdges, out int first, out int last)
        {
            int maximum = Math.Max(0, _timeline.Count - 1);
            float end = _viewStart + _viewCount - 1;
            first = Mathf.Clamp(
                includeEdges ? Mathf.FloorToInt(_viewStart) : Mathf.CeilToInt(_viewStart - 0.0000001f),
                0,
                maximum);
            last = Mathf.Clamp(
                includeEdges ? Mathf.CeilToInt(end) : Mathf.FloorToInt(end + 0.0000001f),
                0,
                maximum);
            if (_timeline.Count == 0)
                first = last = -1;
        }

        private void ComputeVisibleRange()
        {
            _hasVisibleRange = false;
            _visibleLowPointIndex = -1;
            _visibleHighPointIndex = -1;
            if (_series.Count == 0 || _timeline.Count == 0)
                return;
            int first;
            int last;
            VisibleBounds(false, out first, out last);
            IList<WeeklyPoint> points = _series[0].AlignedPoints;
            for (int i = first; i <= last; i++)
            {
                WeeklyPoint point = points[i];
                if (point == null)
                    continue;
                if (!_hasVisibleRange || point.Price < _visibleMinimum)
                {
                    _visibleMinimum = point.Price;
                    _visibleLowPointIndex = i;
                }
                if (!_hasVisibleRange || point.Price > _visibleMaximum)
                {
                    _visibleMaximum = point.Price;
                    _visibleHighPointIndex = i;
                }
                _hasVisibleRange = true;
            }
        }

        private void EnsureMeshChildren()
        {
            if (_plotGraphic != null)
                return;
            var clipObject = new GameObject("PlotClip", typeof(RectTransform));
            clipObject.layer = gameObject.layer;
            _plotClip = (RectTransform)clipObject.transform;
            _plotClip.SetParent(transform, false);
            _plotClip.anchorMin = Vector2.zero;
            _plotClip.anchorMax = Vector2.zero;
            _plotClip.pivot = Vector2.zero;
            _plotClip.localScale = Vector3.one;
            clipObject.AddComponent<RectMask2D>();

            var plotObject = new GameObject("DryBrushPlot", typeof(RectTransform));
            plotObject.layer = gameObject.layer;
            var plotRect = (RectTransform)plotObject.transform;
            plotRect.SetParent(_plotClip, false);
            StretchRect(plotRect, Vector2.zero);
            EnsureCanvasRenderer(plotObject);
            _plotGraphic = plotObject.AddComponent<WeeklyChartMeshGraphic>();
            _plotGraphic.Owner = this;
            _plotGraphic.Layer = WeeklyChartMeshLayer.Plot;
            _plotGraphic.DryBrush = _dryBrushTexture;

            var overlayObject = new GameObject("AxesAndRuler", typeof(RectTransform));
            overlayObject.layer = gameObject.layer;
            var overlayRect = (RectTransform)overlayObject.transform;
            overlayRect.SetParent(transform, false);
            StretchRect(overlayRect, rectTransform.pivot);
            EnsureCanvasRenderer(overlayObject);
            _overlayGraphic = overlayObject.AddComponent<WeeklyChartMeshGraphic>();
            _overlayGraphic.Owner = this;
            _overlayGraphic.Layer = WeeklyChartMeshLayer.Overlay;
        }

        private static void EnsureCanvasRenderer(GameObject target)
        {
            if (target.GetComponent<CanvasRenderer>() == null)
                target.AddComponent<CanvasRenderer>();
        }

        private static void StretchRect(RectTransform rect, Vector2 pivot)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private void UpdateVisuals()
        {
            if (_isDestroying || rectTransform == null)
                return;
            EnsureMeshChildren();
            ComputeVisibleRange();
            UpdateClipRect();
            UpdateCardLineWidth();
            SetVerticesDirty();
            _plotGraphic.SetVerticesDirty();
            _plotGraphic.SetMaterialDirty();
            _overlayGraphic.SetVerticesDirty();
            RefreshLabels();
            _lastGraphicColor = color;
            _hasGraphicColor = true;
        }

        private void UpdateCardLineWidth()
        {
            if (_mode != WeeklyChartMode.Card || rectTransform.rect.width <= 0.0001f)
                return;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            canvas = canvas.rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera == null ? Camera.main : canvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            float pixelsPerLocalUnit = (maximum.x - minimum.x) / rectTransform.rect.width;
            if (pixelsPerLocalUnit <= 0.0001f)
                return;
            float nativePixelsPerCssPixel = Screen.width >= 2304 && Screen.height >= 1296 ? 1.5f : 1f;
            _lineWidth = 1.85f * nativePixelsPerCssPixel / pixelsPerLocalUnit;
        }

        private void UpdateClipRect()
        {
            Rect parentRect = rectTransform.rect;
            Rect plot = GetPlotRect();
            Rect clip = Rect.MinMaxRect(
                plot.xMin - 4f,
                plot.yMin - 4f,
                plot.xMax + 4f,
                plot.yMax + 4f);
            _plotClip.pivot = Vector2.zero;
            _plotClip.anchorMin = Vector2.zero;
            _plotClip.anchorMax = Vector2.zero;
            _plotClip.sizeDelta = clip.size;
            _plotClip.anchoredPosition = clip.min - parentRect.min;
            _overlayGraphic.rectTransform.pivot = rectTransform.pivot;
        }

        private void SetHoverIndex(int index)
        {
            if (index >= _timeline.Count)
                index = _timeline.Count - 1;
            if (_selectedIndex == index)
                return;
            _selectedIndex = index;
            if (_overlayGraphic != null)
                _overlayGraphic.SetVerticesDirty();
            RefreshHoverLabel();
        }

        private Color32 Multiply(Color32 value)
        {
            return (Color32)((Color)value * color);
        }

        private Color32 Paint(Color32 value, float opacity)
        {
            return Multiply(new Color32(value.r, value.g, value.b, (byte)Mathf.Clamp(Mathf.RoundToInt(opacity * 255f), 0, 255)));
        }

        private Font ResolveLabelFont()
        {
            return _labelFont != null ? _labelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Font ResolveAxisLabelFont()
        {
            return _axisLabelFont != null ? _axisLabelFont : ResolveLabelFont();
        }

        internal static string FormatValue(double value)
        {
            return value.ToString("#,0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatDate(WeeklyPoint point)
        {
            return MarketQuery.FormatMarketTime(point);
        }

        private string FormatAxisDate(WeeklyPoint point)
        {
            if (_mode != WeeklyChartMode.Card || point == null)
                return FormatDate(point);
            return MarketQuery.FormatMarketWeek(point.MarketWeekIndex);
        }

        private sealed class ChartSeriesData
        {
            internal string Id;
            internal string Label;
            internal string StyleKey;
            internal Color32? AssetColor;
            internal List<WeeklyPoint> SourcePoints;
            internal Dictionary<int, WeeklyPoint> ByWeek;
            internal WeeklyPoint[] AlignedPoints;
        }

        private sealed class RenderContext
        {
            internal Rect Plot;
            internal int FirstEdge;
            internal int LastEdge;
            internal int First;
            internal int Last;
            internal double Minimum;
            internal double Maximum;
            internal int ReferenceIndex = -1;
            internal ChartSeriesData Reference;
            internal double ReferenceMinimum;
            internal double ReferenceMaximum;
        }

        private sealed class ChartTag
        {
            internal RectTransform Root;
            internal WeeklyChartPaperGraphic Paper;
            internal Text Text;
        }

        private enum DragKind
        {
            None,
            Time,
            Price
        }

        private sealed class DragState
        {
            internal DragKind Kind;
            internal int PointerId;
            internal Vector2 StartLocal;
            internal float StartView;
            internal float StartYFactor;
            internal float StartYCenter;
            internal float AnchorRatio;
            internal int Count;
            internal bool Moved;
        }
    }
}
