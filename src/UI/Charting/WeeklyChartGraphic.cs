using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    public sealed class WeeklyChartGraphic : MaskableGraphic,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IDragHandler
    {
        private List<WeeklyPoint> _points = new List<WeeklyPoint>();
        private ReadOnlyCollection<WeeklyChartSegment> _segments = WeeklyChartSegmentBuilder.Empty;
        private WeeklyChartMode _mode = WeeklyChartMode.Card;
        private Color32 _risingColor = WeeklyChartSegmentBuilder.DefaultRisingColor;
        private Color32 _fallingColor = WeeklyChartSegmentBuilder.DefaultFallingColor;
        private Color32 _flatColor = WeeklyChartSegmentBuilder.DefaultFlatColor;
        private Color32 _gridColor = new Color32(55, 46, 36, 36);
        private Color32 _crosshairColor = new Color32(48, 40, 32, 160);
        private Color32 _labelColor = new Color32(48, 40, 32, 255);
        private Vector4 _plotPadding = new Vector4(8f, 8f, 8f, 8f);
        private float _lineWidth = 2f;
        private float _gridLineWidth = 1f;
        private int _horizontalGridLines = 3;
        private int _verticalGridLines;
        private int _labelFontSize = 14;
        private bool _interactionEnabled;
        private bool _showLatestLabel;
        private bool _showRangeLabels;
        private Font _labelFont;
        private Text _highLabel;
        private Text _lowLabel;
        private Text _latestLabel;
        private Text _hoverLabel;
        private bool _pointerInside;
        private Camera _pointerCamera;
        private int _selectedIndex = -1;
        private bool _hasVisibleRange;
        private double _visibleMinimum;
        private double _visibleMaximum;
        private double _plotMinimum;
        private double _plotMaximum;
        private int _visibleLowPointIndex = -1;
        private int _visibleHighPointIndex = -1;

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

            WeeklyChartGraphic chart = gameObject.AddComponent<WeeklyChartGraphic>();
            chart._labelFont = labelFont;
            chart.Mode = mode;
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
                _pointerInside = false;
                _selectedIndex = -1;
                raycastTarget = _interactionEnabled;
                SetVerticesDirty();
                RefreshLabels();
            }
        }

        public IReadOnlyList<WeeklyChartSegment> Segments
        {
            get { return _segments; }
        }

        public int VisiblePointCount
        {
            get { return _points.Count; }
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
                SetVerticesDirty();
                RefreshLabelLayout();
            }
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
                SetVerticesDirty();
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
                SetVerticesDirty();
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
                SetVerticesDirty();
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
                SetVerticesDirty();
                RefreshLabelLayout();
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
                SetVerticesDirty();
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
                SetVerticesDirty();
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
                ApplyLabelColor(_highLabel);
                ApplyLabelColor(_lowLabel);
                ApplyLabelColor(_latestLabel);
                ApplyLabelColor(_hoverLabel);
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
                ApplyLabelFont(_highLabel);
                ApplyLabelFont(_lowLabel);
                ApplyLabelFont(_latestLabel);
                ApplyLabelFont(_hoverLabel);
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
                ApplyLabelFontSize(_highLabel);
                ApplyLabelFontSize(_lowLabel);
                ApplyLabelFontSize(_latestLabel);
                ApplyLabelFontSize(_hoverLabel);
                RefreshLabelLayout();
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
                    _pointerInside = false;
                    _selectedIndex = -1;
                }
                SetVerticesDirty();
                RefreshLabels();
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
                SetVerticesDirty();
                RefreshLabels();
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
                RefreshLabels();
            }
        }

        public void SetPoints(List<WeeklyPoint> visiblePoints)
        {
            if (visiblePoints == null)
                throw new ArgumentNullException("visiblePoints");

            var copy = new List<WeeklyPoint>(visiblePoints.Count);
            for (int i = 0; i < visiblePoints.Count; i++)
                copy.Add(WeeklyChartSegmentBuilder.CopyVisiblePoint(visiblePoints[i], i));
            ReadOnlyCollection<WeeklyChartSegment> segments = WeeklyChartSegmentBuilder.Build(
                copy,
                _risingColor,
                _fallingColor,
                _flatColor);

            _points = copy;
            _segments = segments;
            _selectedIndex = -1;
            ComputeVisibleRange();
            SetVerticesDirty();
            RefreshLabels();
        }

        public void ClearPoints()
        {
            _points.Clear();
            _segments = WeeklyChartSegmentBuilder.Empty;
            _selectedIndex = -1;
            ComputeVisibleRange();
            SetVerticesDirty();
            RefreshLabels();
        }

        public void SetSelectedIndex(int index)
        {
            if (index < 0 || index >= _points.Count)
                throw new ArgumentOutOfRangeException("index");
            SetHoverIndex(index);
        }

        public void ClearSelection()
        {
            SetHoverIndex(-1);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactionEnabled)
                return;
            _pointerInside = true;
            _pointerCamera = eventData != null ? eventData.pressEventCamera : null;
            if (eventData != null)
                UpdateSelection(eventData.position, _pointerCamera);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            _pointerCamera = null;
            SetHoverIndex(-1);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactionEnabled || eventData == null)
                return;
            _pointerInside = true;
            _pointerCamera = eventData.pressEventCamera;
            UpdateSelection(eventData.position, _pointerCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_interactionEnabled || eventData == null)
                return;
            _pointerCamera = eventData.pressEventCamera;
            UpdateSelection(eventData.position, _pointerCamera);
        }

        protected override void Awake()
        {
            base.Awake();
            color = Color.white;
            raycastTarget = _interactionEnabled;
        }

        protected override void OnDisable()
        {
            _pointerInside = false;
            _pointerCamera = null;
            _selectedIndex = -1;
            RefreshLabels();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            DestroyLabel(ref _highLabel);
            DestroyLabel(ref _lowLabel);
            DestroyLabel(ref _latestLabel);
            DestroyLabel(ref _hoverLabel);
            _points.Clear();
            _segments = WeeklyChartSegmentBuilder.Empty;
            base.OnDestroy();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
            RefreshLabelLayout();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect plot = GetPlotRect();
            if (plot.width <= 0f || plot.height <= 0f)
                return;

            DrawGrid(vertexHelper, plot);
            if (_points.Count == 0)
                return;

            for (int i = 0; i < _segments.Count; i++)
            {
                WeeklyChartSegment segment = _segments[i];
                Vector2 start = GetPointPosition(segment.StartPointIndex, plot);
                Vector2 end = GetPointPosition(segment.EndPointIndex, plot);
                AddLine(
                    vertexHelper,
                    start,
                    end,
                    _lineWidth,
                    segment.StartColor,
                    segment.EndColor);
            }

            if (_points.Count == 1 || _showLatestLabel || _interactionEnabled)
            {
                int latestIndex = _points.Count - 1;
                AddQuad(
                    vertexHelper,
                    GetPointPosition(latestIndex, plot),
                    Mathf.Max(3f, _lineWidth * 1.8f),
                    GetPointColor(latestIndex));
            }

            if (_selectedIndex >= 0 && _selectedIndex < _points.Count)
                DrawCrosshair(vertexHelper, plot, _selectedIndex);
        }

        private void Update()
        {
            if (!_interactionEnabled || !_pointerInside || _points.Count == 0)
                return;
            UpdateSelection(Input.mousePosition, _pointerCamera);
        }

        private void ApplyModeDefaults()
        {
            switch (_mode)
            {
                case WeeklyChartMode.Detail:
                    _lineWidth = 3f;
                    _gridLineWidth = 1f;
                    _horizontalGridLines = 5;
                    _verticalGridLines = 7;
                    _plotPadding = new Vector4(56f, 30f, 20f, 26f);
                    _labelFontSize = 18;
                    _interactionEnabled = true;
                    _showLatestLabel = true;
                    _showRangeLabels = true;
                    break;
                case WeeklyChartMode.Assets:
                    _lineWidth = 2.5f;
                    _gridLineWidth = 1f;
                    _horizontalGridLines = 5;
                    _verticalGridLines = 5;
                    _plotPadding = new Vector4(58f, 30f, 20f, 26f);
                    _labelFontSize = 18;
                    _interactionEnabled = true;
                    _showLatestLabel = true;
                    _showRangeLabels = true;
                    break;
                default:
                    _lineWidth = 2f;
                    _gridLineWidth = 1f;
                    _horizontalGridLines = 3;
                    _verticalGridLines = 0;
                    _plotPadding = new Vector4(8f, 8f, 8f, 8f);
                    _labelFontSize = 14;
                    _interactionEnabled = false;
                    _showLatestLabel = false;
                    _showRangeLabels = false;
                    break;
            }
        }

        private void RebuildSegments()
        {
            _segments = WeeklyChartSegmentBuilder.Build(
                _points,
                _risingColor,
                _fallingColor,
                _flatColor);
            SetVerticesDirty();
        }

        private void ComputeVisibleRange()
        {
            _hasVisibleRange = _points.Count > 0;
            _visibleLowPointIndex = _hasVisibleRange ? 0 : -1;
            _visibleHighPointIndex = _hasVisibleRange ? 0 : -1;
            if (!_hasVisibleRange)
            {
                _visibleMinimum = 0.0;
                _visibleMaximum = 0.0;
                _plotMinimum = 0.0;
                _plotMaximum = 0.0;
                return;
            }

            _visibleMinimum = _points[0].Price;
            _visibleMaximum = _points[0].Price;
            for (int i = 1; i < _points.Count; i++)
            {
                double price = _points[i].Price;
                if (price < _visibleMinimum)
                {
                    _visibleMinimum = price;
                    _visibleLowPointIndex = i;
                }
                if (price > _visibleMaximum)
                {
                    _visibleMaximum = price;
                    _visibleHighPointIndex = i;
                }
            }

            double range = _visibleMaximum - _visibleMinimum;
            if (double.IsInfinity(range))
            {
                _plotMinimum = _visibleMinimum;
                _plotMaximum = _visibleMaximum;
                return;
            }

            double margin = range > 0.0
                ? range * 0.08
                : Math.Max(1.0, Math.Abs(_visibleMinimum) * 0.05);
            double lower = _visibleMinimum - margin;
            double upper = _visibleMaximum + margin;
            if (double.IsNaN(lower) || double.IsInfinity(lower) ||
                double.IsNaN(upper) || double.IsInfinity(upper))
            {
                lower = _visibleMinimum;
                upper = _visibleMaximum;
            }
            _plotMinimum = lower;
            _plotMaximum = upper;
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

        private Vector2 GetPointPosition(int index, Rect plot)
        {
            float x = _points.Count == 1
                ? plot.center.x
                : plot.xMin + plot.width * index / (_points.Count - 1);
            double range = _plotMaximum - _plotMinimum;
            double normalized;
            if (range == 0.0)
            {
                normalized = 0.5;
            }
            else if (double.IsInfinity(range))
            {
                normalized = ((_points[index].Price * 0.5) - (_plotMinimum * 0.5)) /
                             ((_plotMaximum * 0.5) - (_plotMinimum * 0.5));
            }
            else
            {
                normalized = (_points[index].Price - _plotMinimum) / range;
            }
            float y = plot.yMin + plot.height * Mathf.Clamp01((float)normalized);
            return new Vector2(x, y);
        }

        private Color32 GetPointColor(int index)
        {
            return index > 0 && index - 1 < _segments.Count
                ? _segments[index - 1].EndColor
                : _flatColor;
        }

        private void DrawGrid(VertexHelper vertexHelper, Rect plot)
        {
            if (_horizontalGridLines > 1)
            {
                for (int i = 0; i < _horizontalGridLines; i++)
                {
                    float y = plot.yMin + plot.height * i / (_horizontalGridLines - 1);
                    AddLine(
                        vertexHelper,
                        new Vector2(plot.xMin, y),
                        new Vector2(plot.xMax, y),
                        _gridLineWidth,
                        _gridColor,
                        _gridColor);
                }
            }

            if (_verticalGridLines > 1)
            {
                for (int i = 0; i < _verticalGridLines; i++)
                {
                    float x = plot.xMin + plot.width * i / (_verticalGridLines - 1);
                    AddLine(
                        vertexHelper,
                        new Vector2(x, plot.yMin),
                        new Vector2(x, plot.yMax),
                        _gridLineWidth,
                        _gridColor,
                        _gridColor);
                }
            }
        }

        private void DrawCrosshair(VertexHelper vertexHelper, Rect plot, int index)
        {
            Vector2 point = GetPointPosition(index, plot);
            float width = Mathf.Max(1f, _gridLineWidth);
            AddLine(
                vertexHelper,
                new Vector2(point.x, plot.yMin),
                new Vector2(point.x, plot.yMax),
                width,
                _crosshairColor,
                _crosshairColor);
            AddLine(
                vertexHelper,
                new Vector2(plot.xMin, point.y),
                new Vector2(plot.xMax, point.y),
                width,
                _crosshairColor,
                _crosshairColor);
            AddQuad(
                vertexHelper,
                point,
                Mathf.Max(5f, _lineWidth * 2f),
                GetPointColor(index));
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color32 startColor,
            Color32 endColor)
        {
            if (width <= 0f)
                return;
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < 0.000001f)
                return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int index = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = startColor;
            vertex.position = start - normal;
            vertexHelper.AddVert(vertex);
            vertex.position = start + normal;
            vertexHelper.AddVert(vertex);
            vertex.color = endColor;
            vertex.position = end + normal;
            vertexHelper.AddVert(vertex);
            vertex.position = end - normal;
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index + 2, index + 3, index);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 center,
            float size,
            Color32 color)
        {
            float half = size * 0.5f;
            int index = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center + new Vector2(-half, -half);
            vertexHelper.AddVert(vertex);
            vertex.position = center + new Vector2(-half, half);
            vertexHelper.AddVert(vertex);
            vertex.position = center + new Vector2(half, half);
            vertexHelper.AddVert(vertex);
            vertex.position = center + new Vector2(half, -half);
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index + 2, index + 3, index);
        }

        private void UpdateSelection(Vector2 screenPoint, Camera eventCamera)
        {
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out localPoint))
            {
                return;
            }

            Rect plot = GetPlotRect();
            int index = 0;
            if (_points.Count > 1 && plot.width > 0f)
            {
                float normalized = Mathf.Clamp01((localPoint.x - plot.xMin) / plot.width);
                index = Mathf.RoundToInt(normalized * (_points.Count - 1));
            }
            SetHoverIndex(index);
        }

        private void SetHoverIndex(int index)
        {
            if (index >= _points.Count)
                index = _points.Count - 1;
            if (_selectedIndex == index)
                return;
            _selectedIndex = index;
            SetVerticesDirty();
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            bool hasPoints = _points.Count > 0;
            bool showRange = hasPoints && _showRangeLabels;
            bool showLatest = hasPoints && _showLatestLabel && _selectedIndex < 0;
            bool showHover = hasPoints && _selectedIndex >= 0;

            if (showRange)
            {
                _highLabel = EnsureLabel(_highLabel, "HighLabel");
                _lowLabel = EnsureLabel(_lowLabel, "LowLabel");
                _highLabel.text = "高 " + FormatValue(_visibleMaximum);
                _lowLabel.text = "低 " + FormatValue(_visibleMinimum);
            }
            if (showLatest)
            {
                _latestLabel = EnsureLabel(_latestLabel, "LatestLabel");
                _latestLabel.text = "最新 " + FormatValue(_points[_points.Count - 1].Price);
            }
            if (showHover)
            {
                _hoverLabel = EnsureLabel(_hoverLabel, "HoverLabel");
                WeeklyPoint point = _points[_selectedIndex];
                _hoverLabel.text = "周 " + point.MarketWeekIndex.ToString(CultureInfo.InvariantCulture)
                                   + "  " + FormatValue(point.Price);
            }

            SetLabelActive(_highLabel, showRange);
            SetLabelActive(_lowLabel, showRange);
            SetLabelActive(_latestLabel, showLatest);
            SetLabelActive(_hoverLabel, showHover);
            RefreshLabelLayout();
        }

        private void RefreshLabelLayout()
        {
            if (rectTransform == null)
                return;
            Rect plot = GetPlotRect();
            if (plot.width <= 0f || plot.height <= 0f)
                return;

            if (_highLabel != null && _highLabel.gameObject.activeSelf)
            {
                _highLabel.alignment = TextAnchor.UpperLeft;
                PlaceLabel(
                    _highLabel,
                    new Vector2(plot.xMin + 4f, plot.yMax - 2f),
                    new Vector2(0f, 1f),
                    Mathf.Max(60f, plot.width * 0.45f));
            }
            if (_lowLabel != null && _lowLabel.gameObject.activeSelf)
            {
                _lowLabel.alignment = TextAnchor.LowerLeft;
                PlaceLabel(
                    _lowLabel,
                    new Vector2(plot.xMin + 4f, plot.yMin + 2f),
                    new Vector2(0f, 0f),
                    Mathf.Max(60f, plot.width * 0.45f));
            }
            if (_latestLabel != null && _latestLabel.gameObject.activeSelf && _points.Count > 0)
            {
                Vector2 point = GetPointPosition(_points.Count - 1, plot);
                float height = _labelFontSize + 8f;
                float y = Mathf.Clamp(
                    point.y + height,
                    rectTransform.rect.yMin + height * 0.5f,
                    rectTransform.rect.yMax - height * 0.5f);
                _latestLabel.alignment = TextAnchor.MiddleRight;
                PlaceLabel(
                    _latestLabel,
                    new Vector2(plot.xMax - 4f, y),
                    new Vector2(1f, 0.5f),
                    Mathf.Min(220f, Mathf.Max(100f, plot.width * 0.45f)));
            }
            if (_hoverLabel != null && _hoverLabel.gameObject.activeSelf && _selectedIndex >= 0)
            {
                Vector2 point = GetPointPosition(_selectedIndex, plot);
                bool alignRight = point.x > plot.center.x;
                float height = _labelFontSize + 8f;
                float y = Mathf.Clamp(
                    point.y + 12f,
                    rectTransform.rect.yMin,
                    rectTransform.rect.yMax - height);
                _hoverLabel.alignment = alignRight ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
                PlaceLabel(
                    _hoverLabel,
                    new Vector2(point.x + (alignRight ? -8f : 8f), y),
                    new Vector2(alignRight ? 1f : 0f, 0f),
                    Mathf.Min(260f, Mathf.Max(120f, plot.width * 0.55f)));
            }
        }

        private Text EnsureLabel(Text label, string name)
        {
            if (label != null)
                return label;

            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = this.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.localScale = Vector3.one;
            Text created = gameObject.AddComponent<Text>();
            created.font = ResolveLabelFont();
            created.fontSize = _labelFontSize;
            created.color = _labelColor;
            created.supportRichText = false;
            created.horizontalOverflow = HorizontalWrapMode.Overflow;
            created.verticalOverflow = VerticalWrapMode.Overflow;
            created.raycastTarget = false;
            return created;
        }

        private Font ResolveLabelFont()
        {
            return _labelFont != null
                ? _labelFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void PlaceLabel(Text label, Vector2 localPosition, Vector2 pivot, float width)
        {
            RectTransform rect = label.rectTransform;
            Rect parentRect = rectTransform.rect;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(width, _labelFontSize + 8f);
            rect.anchoredPosition = new Vector2(
                localPosition.x - parentRect.xMin,
                localPosition.y - parentRect.yMin);
        }

        private static void SetLabelActive(Text label, bool active)
        {
            if (label != null && label.gameObject.activeSelf != active)
                label.gameObject.SetActive(active);
        }

        private static string FormatValue(double value)
        {
            return value.ToString(
                Math.Abs(value) >= 10000.0 ? "0" : "0.##",
                CultureInfo.InvariantCulture);
        }

        private void ApplyLabelColor(Text label)
        {
            if (label != null)
                label.color = _labelColor;
        }

        private void ApplyLabelFont(Text label)
        {
            if (label != null)
                label.font = ResolveLabelFont();
        }

        private void ApplyLabelFontSize(Text label)
        {
            if (label != null)
                label.fontSize = _labelFontSize;
        }

        private static void DestroyLabel(ref Text label)
        {
            if (label != null)
                Destroy(label.gameObject);
            label = null;
        }
    }
}
