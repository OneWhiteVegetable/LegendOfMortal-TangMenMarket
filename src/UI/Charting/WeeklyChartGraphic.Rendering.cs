using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    public sealed partial class WeeklyChartGraphic
    {
        private static readonly Color32 ReturnNeutral = new Color32(103, 106, 101, 255);
        private static readonly Color32[] AssetColors =
        {
            new Color32(180, 147, 88, 255),
            new Color32(63, 107, 88, 255),
            new Color32(182, 64, 54, 255),
            new Color32(101, 119, 107, 255),
            new Color32(138, 110, 82, 255)
        };

        internal void PopulatePlot(VertexHelper vertexHelper)
        {
            RenderContext context = CreateRenderContext();
            if (_timeline.Count == 0 || context.Plot.width <= 0f || context.Plot.height <= 0f)
                return;
            Vector2 clipMinimum = context.Plot.min - new Vector2(4f, 4f);
            Vector2 uvOffset = clipMinimum - rectTransform.rect.min;
            float mainOpacity = MainStrokeOpacity;
            float pixel = 1f / Mathf.Max(1f, canvas == null ? 1f : canvas.scaleFactor);

            for (int seriesIndex = 0; seriesIndex < _series.Count; seriesIndex++)
            {
                ChartSeriesData item = _series[seriesIndex];
                double minimum;
                double maximum;
                GetSeriesScale(item, context.Plot, out minimum, out maximum);
                bool signedRate = IsProfitChart && seriesIndex == 1;
                WeeklyChartBrushLine line = WeeklyChartBrushGeometry.BuildLine(
                    item.AlignedPoints,
                    context.FirstEdge,
                    context.LastEdge,
                    _viewStart,
                    _viewCount,
                    context.Plot,
                    minimum,
                    maximum,
                    _mode,
                    seriesIndex,
                    item.StyleKey,
                    signedRate ? _lineWidth * 0.70f : _lineWidth,
                    _risingColor,
                    _fallingColor,
                    signedRate ? ReturnNeutral : _flatColor,
                    GetAssetColor(seriesIndex), signedRate);

                for (int i = 0; i < line.Segments.Count; i++)
                {
                    WeeklyChartBrushSegment segment = line.Segments[i];
                    float width = segment.LocalWidth + UnderStrokeAdditionalWidth;
                    DrawQuadraticStroke(
                        vertexHelper,
                        segment,
                        width,
                        UnderStrokeOpacity,
                        clipMinimum,
                        uvOffset,
                        true);
                }

                for (int i = 1; i < line.Segments.Count; i++)
                {
                    WeeklyChartBrushSegment previous = line.Segments[i - 1];
                    WeeklyChartBrushSegment current = line.Segments[i];
                    if (!ReferenceEquals(previous.To, current.From) || current.From.Index < 0)
                        continue;
                    float tipWidth = Math.Max(
                        previous.Profile[previous.Profile.Length - 1].Width,
                        current.Profile[0].Width);
                    WeeklyChartMesh.AddCircle(
                        vertexHelper,
                        current.From.Position - clipMinimum,
                        tipWidth * 0.5f + (_mode == WeeklyChartMode.Spark ? 0.12f : 0.18f),
                        Paint(signedRate ? RateInk(current.From.Value) : _mode == WeeklyChartMode.Assets ? GetAssetColor(seriesIndex) : current.StartColor, mainOpacity),
                        true,
                        uvOffset);
                }

                for (int i = 0; i < line.Segments.Count; i++)
                {
                    WeeklyChartBrushSegment segment = line.Segments[i];
                    Vector2[] upper;
                    Vector2[] lower;
                    WeeklyChartBrushGeometry.MakeBrushBand(segment, _mode, item.StyleKey, out upper, out lower);
                    int count = Math.Min(upper.Length, lower.Length);
                    for (int j = 0; j < count; j++)
                        WeeklyChartMesh.AddBrushSection(vertexHelper, upper[j] - clipMinimum, lower[j] - clipMinimum,
                            SegmentColor(segment, upper[j], mainOpacity), SegmentColor(segment, lower[j], mainOpacity),
                            uvOffset, pixel, j != 0);
                }

                for (int i = 0; i < line.Segments.Count; i++)
                {
                    WeeklyChartBrushSegment segment = line.Segments[i];
                    for (int threadIndex = 0; threadIndex < segment.ThreadCount; threadIndex++)
                    {
                        WeeklyChartDryThread thread = WeeklyChartBrushGeometry.MakeDryThread(
                            segment,
                            _mode,
                            item.StyleKey,
                            threadIndex);
                        // A narrow overlaid nib needs less dry paper in its core than the full-width amount brush.
                        float dryOpacity = signedRate ? thread.Opacity * 0.45f : thread.Opacity;
                        Color32 threadColor = Paint(new Color32(246, 238, 216, 255), dryOpacity);
                        float threadWidth = Mathf.Max(pixel, thread.Width);
                        threadColor.a = (byte)Mathf.RoundToInt(threadColor.a * thread.Width / threadWidth);
                        if (thread.Quadratic)
                        {
                            DrawQuadraticStroke(
                                vertexHelper,
                                thread.Start,
                                thread.Control,
                                thread.End,
                                threadWidth,
                                threadColor,
                                clipMinimum,
                                uvOffset);
                        }
                        else
                        {
                            Vector2 start = thread.Start - clipMinimum;
                            Vector2 end = thread.End - clipMinimum;
                            WeeklyChartMesh.AddLine(vertexHelper, start, end, threadWidth, threadColor, threadColor, true, uvOffset);
                            WeeklyChartMesh.AddCircle(vertexHelper, start, thread.Width * 0.5f, threadColor, true, uvOffset);
                            WeeklyChartMesh.AddCircle(vertexHelper, end, thread.Width * 0.5f, threadColor, true, uvOffset);
                        }
                    }
                }

                for (int i = 0; i < line.Isolated.Count; i++)
                {
                    WeeklyChartBrushNode node = line.Isolated[i];
                    WeeklyChartMesh.AddCircle(
                        vertexHelper,
                        node.Position - clipMinimum,
                        _mode == WeeklyChartMode.Spark ? 1.25f : IsProfitChart && seriesIndex == 0 ? 3.5f : 1.8f,
                        Paint(signedRate ? RateInk(node.Value) : _mode == WeeklyChartMode.Assets ? GetAssetColor(seriesIndex) : _flatColor, _mode == WeeklyChartMode.Card ? 0.5f : 0.9f),
                        true,
                        uvOffset);
                }
                // Only a genuine observation at zero gets a neutral dot. A computed crossing merely changes ink.
                foreach (WeeklyChartBrushNode zero in line.ObservedZeros)
                    WeeklyChartMesh.AddCircle(vertexHelper, zero.Position - clipMinimum, 1.15f,
                        Paint(ReturnNeutral, mainOpacity), true, uvOffset);

            }
        }

        internal void PopulateOverlay(VertexHelper vertexHelper)
        {
            RenderContext context = CreateRenderContext();
            if (context.Plot.width <= 0f || context.Plot.height <= 0f)
                return;
            DrawAxes(vertexHelper, context);
            DrawCrosshair(vertexHelper, context);
        }

        private RenderContext CreateRenderContext()
        {
            var context = new RenderContext { Plot = GetPlotRect() };
            VisibleBounds(true, out context.FirstEdge, out context.LastEdge);
            VisibleBounds(false, out context.First, out context.Last);
            GetPrimaryScale(context.Plot, out context.Minimum, out context.Maximum);
            if (_mode == WeeklyChartMode.Assets)
            {
                int first;
                int last;
                VisibleBounds(true, out first, out last);
                for (int i = 0; i < _series.Count; i++)
                {
                    IList<WeeklyPoint> points = _series[i].AlignedPoints;
                    for (int j = first; j <= last; j++)
                    {
                        if (j < 0 || j >= points.Count || points[j] == null)
                            continue;
                        context.ReferenceIndex = i;
                        context.Reference = _series[i];
                        break;
                    }
                    if (context.Reference != null)
                        break;
                }
                if (context.Reference != null)
                    GetSeriesScale(context.Reference, context.Plot, out context.ReferenceMinimum, out context.ReferenceMaximum);
                else
                {
                    context.ReferenceMinimum = 0.0;
                    context.ReferenceMaximum = 1.0;
                }
            }
            else
            {
                bool hasReference = _series.Count > 0 && HasVisiblePoints(_series[0]);
                context.ReferenceIndex = hasReference ? 0 : -1;
                context.Reference = hasReference ? _series[0] : null;
                context.ReferenceMinimum = context.Minimum;
                context.ReferenceMaximum = context.Maximum;
            }
            return context;
        }

        private bool HasVisiblePoints(ChartSeriesData item)
        {
            if (item == null)
                return false;
            int first;
            int last;
            VisibleBounds(true, out first, out last);
            for (int i = first; i <= last; i++)
            {
                if (i >= 0 && i < item.AlignedPoints.Length && item.AlignedPoints[i] != null)
                    return true;
            }
            return false;
        }

        private void GetPrimaryScale(Rect plot, out double minimum, out double maximum)
        {
            if (_series.Count == 0)
            {
                minimum = 0.0;
                maximum = 1.0;
                ApplyYZoom(ref minimum, ref maximum);
                return;
            }
            GetSeriesScale(_series[0], plot, out minimum, out maximum);
        }

        private void GetSeriesScale(ChartSeriesData item, Rect plot, out double minimum, out double maximum)
        {
            double low, high;
            bool hasValues = FindRange(item, out low, out high);
            if (hasValues && IsProfitChart)
            {
                // Normalize each unit separately but align their zero levels, including after zoom.
                double positive = 0, negative = 0;
                for (int i = 0; i < _series.Count; i++)
                {
                    double min, max;
                    if (!FindRange(_series[i],out min,out max)) continue;
                    double magnitude = Math.Max(Math.Abs(min),Math.Abs(max));
                    if (magnitude == 0) continue;
                    positive = Math.Max(positive,max/magnitude);
                    negative = Math.Max(negative,-min/magnitude);
                }
                double extent = Math.Max(Math.Abs(low),Math.Abs(high));
                if (extent == 0) extent = 1;
                if (positive == 0 && negative == 0) positive = negative = 1;
                low = -negative*extent;
                high = positive*extent;
            }
            if (!hasValues)
            {
                minimum = 0.0;
                maximum = 1.0;
                ApplyYZoom(ref minimum, ref maximum);
                return;
            }
            double spread = high - low;
            if (spread == 0.0)
            {
                double padding = Math.Max(Math.Abs(high) * 0.08, 1.0);
                low -= padding;
                high += padding;
            }
            else
            {
                low -= spread * 0.1;
                high += spread * 0.1;
            }
            if (double.IsNaN(low) || double.IsInfinity(low) ||
                double.IsNaN(high) || double.IsInfinity(high) || high <= low)
            {
                low = 0.0;
                high = Math.Max(1f, plot.height);
            }
            minimum = low;
            maximum = high;
            ApplyYZoom(ref minimum, ref maximum);
        }

        private bool FindRange(ChartSeriesData series, out double low, out double high)
        {
            low = double.MaxValue;
            high = double.MinValue;
            if (series == null) return false;
            int first, last;
            VisibleBounds(true, out first, out last);
            for (int i = Math.Max(0,first); i <= last && i < series.AlignedPoints.Length; i++)
            {
                WeeklyPoint p = series.AlignedPoints[i];
                if (p == null) continue;
                low = Math.Min(low,p.Price);
                high = Math.Max(high,p.Price);
            }
            return low <= high;
        }

        internal static double AxisTick(double min, double max, int index, int count, bool markZero)
        {
            if (markZero && min < 0 && max > 0 &&
                (int)Math.Round(max / (max-min) * (count-1)) == index) return 0;
            return max - (max-min) * index / Math.Max(1,count-1);
        }

        private void ApplyYZoom(ref double minimum, ref double maximum)
        {
            if (_yZoomFactor == 1f && _yZoomCenterRatio == 0.5f)
                return;
            double baseSpan = maximum - minimum;
            double center = minimum + baseSpan * _yZoomCenterRatio;
            double span = baseSpan / _yZoomFactor;
            minimum = center - span * 0.5;
            maximum = center + span * 0.5;
        }

        private void DrawAxes(VertexHelper vertices, RenderContext context)
        {
            if (_mode == WeeklyChartMode.Spark || context.Reference == null)
                return;
            Rect plot = context.Plot;
            Color32 axisColor = Multiply(_gridColor);
            WeeklyChartMesh.AddLine(
                vertices,
                new Vector2(plot.xMin, plot.yMin),
                new Vector2(plot.xMax, plot.yMin),
                _gridLineWidth,
                axisColor,
                axisColor);

            List<int> ticks = GetTimeTicks(plot);
            for (int i = 0; i < ticks.Count; i++)
            {
                float x = TimeTickPosition(ticks[i], plot);
                if (x < plot.xMin - 1f || x > plot.xMax + 1f)
                    continue;
                float length = _mode == WeeklyChartMode.Card ? 3f : 4f;
                WeeklyChartMesh.AddLine(
                    vertices,
                    new Vector2(x, plot.yMin),
                    new Vector2(x, plot.yMin - length),
                    0.75f,
                    axisColor,
                    axisColor);
            }

            if (_mode != WeeklyChartMode.Card)
            {
                WeeklyChartMesh.AddLine(
                    vertices,
                    new Vector2(plot.xMin, plot.yMin),
                    new Vector2(plot.xMin, plot.yMax),
                    _gridLineWidth,
                    axisColor,
                    axisColor);
            }

            double[] prices = GetPriceTicks(context.ReferenceMinimum, context.ReferenceMaximum, plot);
            for (int i = 0; i < prices.Length; i++)
            {
                float y = PriceTickPosition(prices[i], context.ReferenceMinimum, context.ReferenceMaximum, plot);
                float start = _mode == WeeklyChartMode.Card ? plot.xMin : plot.xMin - 4f;
                float end = _mode == WeeklyChartMode.Card ? plot.xMin + 4f : plot.xMin;
                WeeklyChartMesh.AddLine(
                    vertices,
                    new Vector2(start, y),
                    new Vector2(end, y),
                    0.75f,
                    axisColor,
                    axisColor);
            }
        }

        private void DrawCrosshair(VertexHelper vertices, RenderContext context)
        {
            if (_selectedIndex < context.First || _selectedIndex > context.Last ||
                _selectedIndex < 0 || _selectedIndex >= _timeline.Count)
                return;
            float x = TimeTickPosition(_selectedIndex, context.Plot);
            Color32 crosshair = Multiply(_crosshairColor);
            for (float y = context.Plot.yMin; y < context.Plot.yMax; y += 4f)
            {
                WeeklyChartMesh.AddLine(
                    vertices,
                    new Vector2(x, y),
                    new Vector2(x, Math.Min(y + 2f, context.Plot.yMax)),
                    0.8f,
                    crosshair,
                    crosshair);
            }
        }

        private List<int> GetTimeTicks(Rect plot)
        {
            var ticks = new List<int>();
            int first;
            int last;
            VisibleBounds(false, out first, out last);
            if (_timeline.Count == 0 || last < first)
                return ticks;
            int available = last - first + 1;
            int longest = 7;
            for (int i = first; i <= last; i++)
            {
                WeeklyPoint point = FindPointAt(i);
                if (point != null)
                    longest = Math.Max(longest, FormatAxisDate(point).Length);
            }
            float spacing = _mode == WeeklyChartMode.Card
                ? Math.Max(88f, longest * 6f + 22f)
                : Math.Max(72f, longest * 6f + 18f);
            int byWidth = Math.Max(1, Mathf.FloorToInt(plot.width / spacing) + 1);
            int count = Math.Min(available, Math.Min(byWidth, _mode == WeeklyChartMode.Card ? 4 : 8));
            if (_verticalGridLines > 0)
                count = Math.Min(available, _verticalGridLines);
            if (count == 1)
            {
                ticks.Add(Mathf.RoundToInt((first + last) * 0.5f));
                return ticks;
            }
            for (int i = 0; i < count; i++)
            {
                int index = Mathf.RoundToInt(first + (last - first) * i / (float)(count - 1));
                if (!ticks.Contains(index))
                    ticks.Add(index);
            }
            return ticks;
        }

        private double[] GetPriceTicks(double minimum, double maximum, Rect plot)
        {
            int desired;
            if (_horizontalGridLines > 0)
            {
                desired = Math.Max(2, _horizontalGridLines);
            }
            else if (_mode == WeeklyChartMode.Card)
            {
                desired = plot.height < 52f ? 2 : 3;
            }
            else
            {
                desired = Mathf.Clamp(Mathf.FloorToInt(plot.height / 42f) + 1, 3, 6);
            }
            var values = new double[desired];
            for (int i = 0; i < desired; i++)
                values[i] = AxisTick(minimum,maximum,i,desired,IsProfitChart);
            return values;
        }

        private float TimeTickPosition(int index, Rect plot)
        {
            float intervals = Math.Max(1, _viewCount - 1);
            return plot.xMin + (index - _viewStart) / intervals * plot.width;
        }

        private static float PriceTickPosition(double value, double minimum, double maximum, Rect plot)
        {
            double span = maximum - minimum;
            float normalized = span == 0.0 ? 0.5f : (float)((maximum - value) / span);
            return plot.yMax - normalized * plot.height;
        }

        private Vector2 GetDataPosition(RenderContext context, int seriesIndex, int pointIndex)
        {
            ChartSeriesData item = _series[seriesIndex];
            WeeklyPoint point = item.AlignedPoints[pointIndex];
            double minimum;
            double maximum;
            if (_mode == WeeklyChartMode.Assets)
                GetSeriesScale(item, context.Plot, out minimum, out maximum);
            else
            {
                minimum = context.Minimum;
                maximum = context.Maximum;
            }
            return WeeklyChartBrushGeometry.PointPosition(
                pointIndex,
                point.Price,
                _viewStart,
                _viewCount,
                context.Plot,
                minimum,
                maximum);
        }

        private WeeklyPoint FindPointAt(int timelineIndex)
        {
            for (int i = 0; i < _series.Count; i++)
            {
                WeeklyPoint point = _series[i].AlignedPoints[timelineIndex];
                if (point != null)
                    return point;
            }
            return null;
        }

        private Color32 RateInk(double value)
        {
            return WeeklyChartBrushGeometry.SignedInk(value, _risingColor, _fallingColor, ReturnNeutral);
        }

        private Color32 GetPointColor(int seriesIndex, int pointIndex)
        {
            if (IsProfitChart && seriesIndex == 1)
            {
                WeeklyPoint rate = _series[seriesIndex].AlignedPoints[pointIndex];
                return rate == null ? ReturnNeutral : RateInk(rate.Price);
            }
            if (_mode == WeeklyChartMode.Assets)
                return GetAssetColor(seriesIndex);
            IList<WeeklyPoint> points = _series[seriesIndex].AlignedPoints;
            if (pointIndex > 0 && points[pointIndex] != null && points[pointIndex - 1] != null)
            {
                return WeeklyChartBrushGeometry.DirectionColor(
                    points[pointIndex - 1].Price,
                    points[pointIndex].Price,
                    _risingColor,
                    _fallingColor,
                    _flatColor);
            }
            return _flatColor;
        }

        private Color32 GetAssetColor(int seriesIndex)
        {
            if (seriesIndex >= 0 && seriesIndex < _series.Count && _series[seriesIndex].AssetColor.HasValue)
                return _series[seriesIndex].AssetColor.Value;
            return AssetColors[seriesIndex % AssetColors.Length];
        }

        private Color32 SegmentColor(WeeklyChartBrushSegment segment, Vector2 position, float opacity)
        {
            if (segment.StartColor.Equals(segment.EndColor))
                return Paint(segment.EndColor, opacity);
            Vector2 delta = segment.To.Position - segment.From.Position;
            float lengthSquared = delta.sqrMagnitude;
            float amount = lengthSquared < 0.000001f
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(position - segment.From.Position, delta) / lengthSquared);
            float blend = segment.Transition <= 0f ? 1f : Mathf.Clamp01(amount / segment.Transition);
            Color32 interpolated = (Color32)Color.Lerp(segment.StartColor, segment.EndColor, blend);
            return Paint(interpolated, opacity);
        }

        private void DrawQuadraticStroke(
            VertexHelper vertices,
            WeeklyChartBrushSegment segment,
            float width,
            float opacity,
            Vector2 clipMinimum,
            Vector2 uvOffset,
            bool textured)
        {
            const int steps = 12;
            Vector2 previous = WeeklyChartBrushGeometry.CurvePoint(segment, 0f);
            Vector2 previousNormal = WeeklyChartBrushGeometry.CurveNormal(segment, 0f) * width * 0.5f;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 current = WeeklyChartBrushGeometry.CurvePoint(segment, t);
                Vector2 normal = WeeklyChartBrushGeometry.CurveNormal(segment, t) * width * 0.5f;
                Vector2 p0 = previous - previousNormal;
                Vector2 p1 = previous + previousNormal;
                Vector2 p2 = current + normal;
                Vector2 p3 = current - normal;
                WeeklyChartMesh.AddQuad(
                    vertices,
                    p0 - clipMinimum,
                    p1 - clipMinimum,
                    p2 - clipMinimum,
                    p3 - clipMinimum,
                    SegmentColor(segment, p0, opacity),
                    SegmentColor(segment, p1, opacity),
                    SegmentColor(segment, p2, opacity),
                    SegmentColor(segment, p3, opacity),
                    textured,
                    uvOffset);
                previous = current;
                previousNormal = normal;
            }
            if (segment.From.Index >= 0) WeeklyChartMesh.AddCircle(
                vertices,
                segment.From.Position - clipMinimum,
                width * 0.5f,
                SegmentColor(segment, segment.From.Position, opacity),
                textured,
                uvOffset);
            if (segment.To.Index >= 0) WeeklyChartMesh.AddCircle(
                vertices,
                segment.To.Position - clipMinimum,
                width * 0.5f,
                SegmentColor(segment, segment.To.Position, opacity),
                textured,
                uvOffset);
        }

        private static void DrawQuadraticStroke(
            VertexHelper vertices,
            Vector2 start,
            Vector2 control,
            Vector2 end,
            float width,
            Color32 color,
            Vector2 clipMinimum,
            Vector2 uvOffset)
        {
            const int steps = 5;
            Vector2 previous = start;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float inverse = 1f - t;
                Vector2 current = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
                WeeklyChartMesh.AddLine(
                    vertices,
                    previous - clipMinimum,
                    current - clipMinimum,
                    width,
                    color,
                    color,
                    true,
                    uvOffset);
                previous = current;
            }
            WeeklyChartMesh.AddCircle(vertices, start - clipMinimum, width * 0.5f, color, true, uvOffset);
            WeeklyChartMesh.AddCircle(vertices, end - clipMinimum, width * 0.5f, color, true, uvOffset);
        }
    }
}
