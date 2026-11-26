using System;
using System.Collections.Generic;
using UnityEngine;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    internal sealed class WeeklyChartBrushNode
    {
        internal int Index;
        internal double Value;
        internal Vector2 Position;
    }

    internal struct WeeklyChartBrushProfileSample
    {
        internal float T;
        internal float Width;

        internal WeeklyChartBrushProfileSample(float t, float width)
        {
            T = t;
            Width = width;
        }
    }

    internal sealed class WeeklyChartBrushSegment
    {
        internal WeeklyChartBrushNode From;
        internal WeeklyChartBrushNode To;
        internal Vector2 Control;
        internal Color32 StartColor;
        internal Color32 EndColor;
        internal float Transition;
        internal float LocalWidth;
        internal int SeriesIndex;
        internal WeeklyChartBrushProfileSample[] Profile;
        internal int ThreadCount;
    }

    internal sealed class WeeklyChartBrushLine
    {
        internal readonly List<WeeklyChartBrushSegment> Segments = new List<WeeklyChartBrushSegment>();
        internal readonly List<WeeklyChartBrushNode> Isolated = new List<WeeklyChartBrushNode>();
        internal readonly List<WeeklyChartBrushNode> ObservedZeros = new List<WeeklyChartBrushNode>();
    }

    internal struct WeeklyChartDryThread
    {
        internal Vector2 Start;
        internal Vector2 Control;
        internal Vector2 End;
        internal bool Quadratic;
        internal float Width;
        internal float Opacity;
    }

    internal static class WeeklyChartBrushGeometry
    {
        internal static float StableNoise(string key)
        {
            uint hash = 2166136261;
            unchecked
            {
                for (int i = 0; i < key.Length; i++)
                    hash = (hash ^ key[i]) * 16777619;
            }
            return (float)(hash / 4294967295.0 * 2.0 - 1.0);
        }

        internal static Vector2 PointPosition(
            int index,
            double close,
            float viewStart,
            int viewCount,
            Rect plot,
            double minimum,
            double maximum)
        {
            float intervals = Math.Max(1, viewCount - 1);
            double span = maximum - minimum;
            double normalized = span == 0.0 ? 0.5 : (maximum - close) / span;
            return new Vector2(
                plot.xMin + (index - viewStart) / intervals * plot.width,
                plot.yMax - (float)normalized * plot.height);
        }

        internal static WeeklyChartBrushLine BuildLine(
            IList<WeeklyPoint> points,
            int first,
            int last,
            float viewStart,
            int viewCount,
            Rect plot,
            double minimum,
            double maximum,
            WeeklyChartMode mode,
            int seriesIndex,
            string styleKey,
            float strokeWidth,
            Color32 risingColor,
            Color32 fallingColor,
            Color32 flatColor,
            Color32 assetColor,
            bool inkBySign = false)
        {
            var line = new WeeklyChartBrushLine();
            var nodes = new Dictionary<int, WeeklyChartBrushNode>();
            float amplitude = mode == WeeklyChartMode.Spark
                ? 0.18f
                : mode == WeeklyChartMode.Card ? 0.3f : 0.4f;

            for (int i = first; i <= last; i++)
            {
                WeeklyPoint point = points[i];
                if (point == null)
                    continue;
                Vector2 position = PointPosition(i, point.Price, viewStart, viewCount, plot, minimum, maximum);
                string key = styleKey + "|" + seriesIndex + "|" + point.MarketWeekIndex + "|" + i;
                nodes[i] = new WeeklyChartBrushNode
                {
                    Index = i,
                    Value = point.Price,
                    Position = inkBySign ? position : position + new Vector2(
                        StableNoise(key + "|x") * amplitude,
                        -StableNoise(key + "|y") * amplitude * 0.7f)
                };
            }

            for (int i = first + 1; i <= last; i++)
            {
                WeeklyPoint fromPoint = points[i - 1];
                WeeklyPoint toPoint = points[i];
                WeeklyChartBrushNode from;
                WeeklyChartBrushNode to;
                if (fromPoint == null || toPoint == null ||
                    (mode == WeeklyChartMode.Assets && (long)toPoint.MarketWeekIndex - fromPoint.MarketWeekIndex > 1) ||
                    !nodes.TryGetValue(i - 1, out from) || !nodes.TryGetValue(i, out to))
                    continue;

                Color32 color = mode == WeeklyChartMode.Assets
                    ? assetColor
                    : DirectionColor(fromPoint.Price, toPoint.Price, risingColor, fallingColor, flatColor);
                WeeklyPoint previousPoint = i - 2 >= first ? points[i - 2] : null;
                Color32 startColor = mode != WeeklyChartMode.Assets && previousPoint != null
                    ? DirectionColor(previousPoint.Price, fromPoint.Price, risingColor, fallingColor, flatColor)
                    : color;
                Vector2 delta = to.Position - from.Position;
                float length = Mathf.Max(1f, delta.magnitude);
                float bend = StableNoise(styleKey + "|" + seriesIndex + "|" + i + "|bend") *
                             Mathf.Min(0.32f, length * 0.02f);
                Vector2 control = (from.Position + to.Position) * 0.5f +
                                  new Vector2(-delta.y / length * bend, delta.x / length * bend);
                float transition = Mathf.Clamp(
                    0.375f + StableNoise(styleKey + "|" + seriesIndex + "|" + i + "|transition") * 0.05f,
                    0.3f,
                    0.45f);
                var segment = new WeeklyChartBrushSegment
                {
                    From = from,
                    To = to,
                    Control = control,
                    StartColor = startColor,
                    EndColor = color,
                    Transition = transition,
                    SeriesIndex = seriesIndex
                };
                float widthNoise = StableNoise(styleKey + "|" + seriesIndex + "|" + i + "|width");
                segment.LocalWidth = strokeWidth * (0.94f + (widthNoise + 1f) * 0.07f);
                segment.Profile = MakeBrushProfile(segment, mode, seriesIndex, styleKey, strokeWidth);
                float countNoise = StableNoise(styleKey + "|" + seriesIndex + "|" + i + "|dry-count");
                segment.ThreadCount = mode == WeeklyChartMode.Spark
                    ? 1
                    : mode == WeeklyChartMode.Detail
                        ? 2 + (countNoise > 0.05f ? 1 : 0)
                        : 1 + (countNoise > 0.25f ? 1 : 0);
                if (inkBySign)
                    AddSignedSegment(line, segment, risingColor, fallingColor, flatColor);
                else
                    line.Segments.Add(segment);
            }

            var connected = new HashSet<int>();
            for (int i = 0; i < line.Segments.Count; i++)
            {
                connected.Add(line.Segments[i].From.Index);
                connected.Add(line.Segments[i].To.Index);
            }
            foreach (WeeklyChartBrushNode node in nodes.Values)
            {
                if (!connected.Contains(node.Index))
                    line.Isolated.Add(node);
                else if (inkBySign && node.Value == 0)
                    line.ObservedZeros.Add(node);
            }
            line.Isolated.Sort((a, b) => a.Index.CompareTo(b.Index));
            return line;
        }

        internal static Color32 SignedInk(double value, Color32 positive, Color32 negative, Color32 zero)
        {
            return value > 0 ? positive : value < 0 ? negative : zero;
        }

        private static void AddSignedSegment(WeeklyChartBrushLine line, WeeklyChartBrushSegment segment,
            Color32 positive, Color32 negative, Color32 zero)
        {
            double a = segment.From.Value, b = segment.To.Value;
            if ((a < 0 && b > 0) || (a > 0 && b < 0))
            {
                // The observations are joined linearly. Insert a paint seam at the actual
                // fraction of this interval that reaches zero; it is not a selectable record.
                float t = (float)(a / (a - b));
                var crossing = new WeeklyChartBrushNode
                {
                    Index = -1, Value = 0,
                    Position = segment.From.Position + (segment.To.Position - segment.From.Position) * t
                };
                line.Segments.Add(SignedSlice(segment, segment.From, crossing, 0f, t, SignedInk(a,positive,negative,zero)));
                line.Segments.Add(SignedSlice(segment, crossing, segment.To, t, 1f, SignedInk(b,positive,negative,zero)));
                return;
            }
            segment.StartColor = segment.EndColor = SignedInk(a != 0 ? a : b, positive, negative, zero);
            // Brush edges may vary, but its centerline should stay on the correct side.
            Vector2 control = segment.Control;
            control.y = Mathf.Clamp(control.y, Math.Min(segment.From.Position.y,segment.To.Position.y),
                Math.Max(segment.From.Position.y,segment.To.Position.y));
            segment.Control = control;
            line.Segments.Add(segment);
        }

        private static WeeklyChartBrushSegment SignedSlice(WeeklyChartBrushSegment source, WeeklyChartBrushNode from,
            WeeklyChartBrushNode to, float start, float end, Color32 ink)
        {
            // Interpolate the existing ink pressure at the cut instead of tapering to two tips.
            var samples = new List<WeeklyChartBrushProfileSample>();
            samples.Add(new WeeklyChartBrushProfileSample(0f, BrushProfileWidth(source.Profile, start)));
            foreach (WeeklyChartBrushProfileSample sample in source.Profile)
                if (sample.T > start && sample.T < end)
                    samples.Add(new WeeklyChartBrushProfileSample((sample.T - start) / (end - start), sample.Width));
            samples.Add(new WeeklyChartBrushProfileSample(1f, BrushProfileWidth(source.Profile, end)));
            return new WeeklyChartBrushSegment
            {
                From = from, To = to, Control = (from.Position + to.Position) * .5f,
                StartColor = ink, EndColor = ink, SeriesIndex = source.SeriesIndex, LocalWidth = source.LocalWidth,
                Profile = samples.ToArray(), ThreadCount = source.ThreadCount
            };
        }

        internal static Vector2 CurvePoint(WeeklyChartBrushSegment segment, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * segment.From.Position +
                   2f * inverse * t * segment.Control +
                   t * t * segment.To.Position;
        }

        internal static Vector2 CurveNormal(WeeklyChartBrushSegment segment, float t)
        {
            float x = 2f * (1f - t) * (segment.Control.x - segment.From.Position.x) +
                      2f * t * (segment.To.Position.x - segment.Control.x);
            float y = 2f * (1f - t) * (segment.Control.y - segment.From.Position.y) +
                      2f * t * (segment.To.Position.y - segment.Control.y);
            float length = Mathf.Sqrt(x * x + y * y);
            return length < 0.0001f ? new Vector2(0f, 1f) : new Vector2(-y / length, x / length);
        }

        internal static void MakeBrushBand(
            WeeklyChartBrushSegment segment,
            WeeklyChartMode mode,
            string styleKey,
            out Vector2[] upper,
            out Vector2[] lower)
        {
            var upperProfile = new List<Vector2>(segment.Profile.Length);
            var lowerProfile = new List<Vector2>(segment.Profile.Length);
            float roughness = mode == WeeklyChartMode.Spark
                ? 0.05f
                : mode == WeeklyChartMode.Card
                    ? 0.09f
                    : mode == WeeklyChartMode.Detail ? 0.22f : 0.15f;
            for (int i = 0; i < segment.Profile.Length; i++)
            {
                WeeklyChartBrushProfileSample sample = segment.Profile[i];
                Vector2 center = CurvePoint(segment, sample.T);
                Vector2 normal = CurveNormal(segment, sample.T);
                string key = styleKey + "|" + segment.SeriesIndex + "|" + segment.To.Index + "|edge|" + i;
                bool seam = (i == 0 && segment.From.Index < 0) ||
                    (i == segment.Profile.Length - 1 && segment.To.Index < 0);
                float edgeNoise = seam ? 0f : roughness;
                float upperWidth = Mathf.Max(0.2f, sample.Width * 0.5f + StableNoise(key + "|upper") * edgeNoise);
                float lowerWidth = Mathf.Max(0.2f, sample.Width * 0.5f + StableNoise(key + "|lower") * edgeNoise);
                upperProfile.Add(center + normal * upperWidth);
                lowerProfile.Add(center - normal * lowerWidth);
            }
            // Paired cross-sections traverse time in the same direction on both sides of the ink band.
            upper = SmoothEdge(upperProfile, 5);
            lower = SmoothEdge(lowerProfile, 5);
        }

        internal static WeeklyChartDryThread MakeDryThread(
            WeeklyChartBrushSegment segment,
            WeeklyChartMode mode,
            string styleKey,
            int threadIndex)
        {
            string key = styleKey + "|" + segment.SeriesIndex + "|" + segment.To.Index + "|dry|" + threadIndex;
            float start = -0.035f + (StableNoise(key + "|start") + 1f) * 0.085f;
            float end = 0.82f + (StableNoise(key + "|end") + 1f) * 0.11f;
            float lane = segment.ThreadCount == 1
                ? StableNoise(key + "|lane") * 0.14f
                : (threadIndex / (float)(segment.ThreadCount - 1) - 0.5f) * 0.42f +
                  StableNoise(key + "|lane") * 0.035f;
            int sampleCount = mode == WeeklyChartMode.Spark ? 2 : 3;
            var points = new Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = start + (end - start) * i / (sampleCount - 1);
                Vector2 center = CurvePoint(segment, t);
                Vector2 normal = CurveNormal(segment, t);
                float offset = BrushProfileWidth(segment.Profile, t) * lane;
                points[i] = center + normal * offset;
            }
            float baseWidth = mode == WeeklyChartMode.Spark
                ? 0.28f
                : mode == WeeklyChartMode.Card
                    ? 0.38f
                    : mode == WeeklyChartMode.Detail ? 0.72f : 0.58f;
            float baseOpacity = mode == WeeklyChartMode.Card ? 0.34f : mode == WeeklyChartMode.Spark ? 0.46f : 0.58f;
            return new WeeklyChartDryThread
            {
                Start = points[0],
                Control = points.Length == 3 ? points[1] : Vector2.zero,
                End = points[points.Length - 1],
                Quadratic = points.Length == 3,
                Width = baseWidth * (0.8f + (StableNoise(key + "|width") + 1f) * 0.1f),
                Opacity = baseOpacity + (StableNoise(key + "|opacity") + 1f) * 0.08f
            };
        }

        internal static Color32 DirectionColor(
            double from,
            double to,
            Color32 rising,
            Color32 falling,
            Color32 flat)
        {
            return to > from ? rising : to < from ? falling : flat;
        }

        private static WeeklyChartBrushProfileSample[] MakeBrushProfile(
            WeeklyChartBrushSegment segment,
            WeeklyChartMode mode,
            int seriesIndex,
            string styleKey,
            float baseWidth)
        {
            float bodyNoise = StableNoise(styleKey + "|" + seriesIndex + "|" + segment.To.Index + "|brush-body");
            float middleWidth = segment.LocalWidth * (1.01f + (bodyNoise + 1f) * 0.06f);
            if (mode == WeeklyChartMode.Spark)
            {
                return new[]
                {
                    new WeeklyChartBrushProfileSample(0f, TipWidth(segment.From, seriesIndex, styleKey, baseWidth)),
                    new WeeklyChartBrushProfileSample(0.5f, middleWidth),
                    new WeeklyChartBrushProfileSample(1f, TipWidth(segment.To, seriesIndex, styleKey, baseWidth))
                };
            }
            float tailNoise = StableNoise(styleKey + "|" + seriesIndex + "|" + segment.To.Index + "|brush-tail");
            return new[]
            {
                new WeeklyChartBrushProfileSample(0f, TipWidth(segment.From, seriesIndex, styleKey, baseWidth)),
                new WeeklyChartBrushProfileSample(0.3f, middleWidth),
                new WeeklyChartBrushProfileSample(0.72f, segment.LocalWidth * (0.94f + (tailNoise + 1f) * 0.06f)),
                new WeeklyChartBrushProfileSample(1f, TipWidth(segment.To, seriesIndex, styleKey, baseWidth))
            };
        }

        private static float TipWidth(WeeklyChartBrushNode node, int seriesIndex, string styleKey, float baseWidth)
        {
            float noise = StableNoise(styleKey + "|" + seriesIndex + "|" + node.Index + "|brush-tip");
            return baseWidth * (0.56f + (noise + 1f) * 0.06f);
        }

        private static float BrushProfileWidth(WeeklyChartBrushProfileSample[] profile, float t)
        {
            float position = Mathf.Clamp01(t);
            for (int i = 1; i < profile.Length; i++)
            {
                WeeklyChartBrushProfileSample next = profile[i];
                if (position > next.T)
                    continue;
                WeeklyChartBrushProfileSample previous = profile[i - 1];
                float ratio = (position - previous.T) / (next.T - previous.T);
                return previous.Width + (next.Width - previous.Width) * ratio;
            }
            return profile[profile.Length - 1].Width;
        }

        private static Vector2[] SmoothEdge(List<Vector2> points, int subdivisions)
        {
            var result = new List<Vector2>();
            if (points.Count == 0)
                return result.ToArray();
            result.Add(points[0]);
            Vector2 start = points[0];
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 control = points[i];
                Vector2 end = (control + points[i + 1]) * 0.5f;
                for (int step = 1; step <= subdivisions; step++)
                {
                    float t = step / (float)subdivisions;
                    float inverse = 1f - t;
                    result.Add(inverse * inverse * start + 2f * inverse * t * control + t * t * end);
                }
                start = end;
            }
            if (points.Count > 1)
                result.Add(points[points.Count - 1]);
            return result.ToArray();
        }
    }
}
