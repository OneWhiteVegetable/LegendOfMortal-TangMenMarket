#if XINGSHANG_CHART_AUDIT
using System;
using System.Collections.Generic;
using UnityEngine;
using XingShangMod.Domain;
using XingShangMod.UI.Charting;

internal sealed class WeeklyChartDataTests
{
    public void Register(TestRunner runner)
    {
        runner.Run("chart-mode-default-ranges", DefaultRanges);
        runner.Run("chart-future-week-poison-is-invisible", FutureWeekPoisonIsInvisible);
        runner.Run("chart-style-seed-is-instance-independent", StyleSeedIsInstanceIndependent);
        runner.Run("chart-custom-price-zoom-state", CustomPriceZoomState);
        runner.Run("chart-first-visible-seed-gradient", FirstVisibleSegmentGradient);
    }

#if XINGSHANG_CHART_AUDIT_STANDALONE
    public static int Main()
    {
        var runner = new TestRunner();
        new WeeklyChartDataTests().Register(runner);
        return runner.Complete();
    }
#endif

    private static void DefaultRanges()
    {
        AuditAssert.Equal(14, WeeklyChartData.DefaultRange(WeeklyChartMode.Spark, 14), "header spark range");
        AuditAssert.Equal(20, WeeklyChartData.DefaultRange(WeeklyChartMode.Spark, 20), "ranking spark range");
        AuditAssert.Equal(26, WeeklyChartData.DefaultRange(WeeklyChartMode.Card, 26), "goods card range");
        AuditAssert.Equal(26, WeeklyChartData.DefaultRange(WeeklyChartMode.Detail, 78), "detail reset range");
        AuditAssert.Equal(26, WeeklyChartData.DefaultRange(WeeklyChartMode.Assets, 78), "asset reset range");
    }

    private static void FutureWeekPoisonIsInvisible()
    {
        const int currentWeek = 40;
        var clean = new List<WeeklyPoint>
        {
            Point(39, 100.0),
            Point(currentWeek, 110.0)
        };
        var poisoned = new List<WeeklyPoint>(clean)
        {
            Point(currentWeek + 1, 1000000000000.0),
            Point(currentWeek + 10, -1000000000000.0)
        };
        List<WeeklyPoint> visible = WeeklyChartData.VisibleThrough(poisoned, currentWeek);
        AuditAssert.Equal(2, visible.Count, "future points entered chart data");
        AuditAssert.Equal(4, poisoned.Count, "filter mutated caller history");
        for (int i = 0; i < clean.Count; i++)
        {
            AuditAssert.Equal(clean[i].MarketWeekIndex, visible[i].MarketWeekIndex, "visible week changed");
            AuditAssert.Near(clean[i].Price, visible[i].Price, "visible price changed");
        }
        var cleanSegments = WeeklyChartSegmentBuilder.Build(clean);
        var visibleSegments = WeeklyChartSegmentBuilder.Build(visible);
        AuditAssert.Equal(cleanSegments.Count, visibleSegments.Count, "future poison changed segment count");
        for (int i = 0; i < cleanSegments.Count; i++)
        {
            AuditAssert.Equal(cleanSegments[i].Direction, visibleSegments[i].Direction, "future poison changed direction");
            AuditAssert.Equal(cleanSegments[i].StartColor, visibleSegments[i].StartColor, "future poison changed start color");
            AuditAssert.Equal(cleanSegments[i].EndColor, visibleSegments[i].EndColor, "future poison changed end color");
            AuditAssert.Near(cleanSegments[i].StartPrice, visibleSegments[i].StartPrice, "future poison changed segment start");
            AuditAssert.Near(cleanSegments[i].EndPrice, visibleSegments[i].EndPrice, "future poison changed segment end");
        }
        AuditAssert.Equal(0, WeeklyChartData.VisibleThrough(poisoned, -1).Count, "unknown current week leaked data");
    }

    private static void StyleSeedIsInstanceIndependent()
    {
        string first = WeeklyChartData.StyleKey(string.Empty, WeeklyChartMode.Detail, "01_item");
        string second = WeeklyChartData.StyleKey(null, WeeklyChartMode.Detail, "01_item");
        AuditAssert.Equal(first, second, "default style depends on construction order");
        AuditAssert.Equal(
            WeeklyChartBrushGeometry.StableNoise(first + "|9|width"),
            WeeklyChartBrushGeometry.StableNoise(second + "|9|width"),
            "stable noise changed for identical series");
        AuditAssert.True(first != WeeklyChartData.StyleKey(string.Empty, WeeklyChartMode.Detail, "02_item"), "series share a style key");
        AuditAssert.True(first != WeeklyChartData.StyleKey("alternate", WeeklyChartMode.Detail, "01_item"), "explicit style seed ignored");
    }

    private static void CustomPriceZoomState()
    {
        AuditAssert.True(!WeeklyChartData.HasCustomPriceZoom(1f, 0.5f), "reset view marked custom");
        AuditAssert.True(WeeklyChartData.HasCustomPriceZoom(1.2f, 0.5f), "price factor not marked custom");
        AuditAssert.True(WeeklyChartData.HasCustomPriceZoom(1f, 0.7f), "price center not marked custom");
    }

    private static void FirstVisibleSegmentGradient()
    {
        var points = new List<WeeklyPoint>
        {
            Point(0, 100.0),
            Point(1, 90.0),
            Point(2, 95.0),
            Point(3, 90.0)
        };
        Color32 rising = new Color32(182, 64, 54, 255);
        Color32 falling = new Color32(63, 107, 88, 255);
        Color32 flat = new Color32(116, 103, 84, 255);
        WeeklyChartBrushLine line = WeeklyChartBrushGeometry.BuildLine(
            points,
            1,
            3,
            1f,
            2,
            new Rect(0f, 0f, 300f, 100f),
            80.0,
            110.0,
            WeeklyChartMode.Detail,
            0,
            "audit|detail|item",
            4.2f,
            rising,
            falling,
            flat,
            flat);
        foreach (WeeklyChartBrushSegment segment in line.Segments)
        {
            Vector2[] upper, lower;
            WeeklyChartBrushGeometry.MakeBrushBand(segment, WeeklyChartMode.Detail, "audit|detail|item", out upper, out lower);
            for (int i = 0; i < upper.Length; i++)
                AuditAssert.True((upper[i] - lower[i]).magnitude < 10f, "Opposite ink edges must describe the same date.");
        }
        AuditAssert.Equal(2, line.Segments.Count, "visible segment count");
        AuditAssert.Equal(rising, line.Segments[0].StartColor, "first visible segment read an offscreen direction");
        AuditAssert.Equal(rising, line.Segments[0].EndColor, "first visible segment direction");
        AuditAssert.Equal(rising, line.Segments[1].StartColor, "later segment lost visible predecessor");
        AuditAssert.Equal(falling, line.Segments[1].EndColor, "later segment direction");
    }

    private static WeeklyPoint Point(int week, double price)
    {
        return new WeeklyPoint
        {
            ItemId = "01_item",
            MarketWeekIndex = week,
            GameYear = 1,
            GameMonth = 4,
            GameRound = 1,
            Price = price,
            Observed = true
        };
    }
}

namespace UnityEngine
{
    public struct Color32 : IEquatable<Color32>
    {
        internal byte r;
        internal byte g;
        internal byte b;
        internal byte a;

        internal Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public bool Equals(Color32 other)
        {
            return r == other.r && g == other.g && b == other.b && a == other.a;
        }

        public override bool Equals(object obj)
        {
            return obj is Color32 && Equals((Color32)obj);
        }

        public override int GetHashCode()
        {
            return r | (g << 8) | (b << 16) | (a << 24);
        }
    }

    internal struct Vector2
    {
        internal float x;
        internal float y;

        internal Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        internal float magnitude
        {
            get { return Mathf.Sqrt(x * x + y * y); }
        }

        internal static Vector2 zero
        {
            get { return new Vector2(0f, 0f); }
        }

        public static Vector2 operator +(Vector2 left, Vector2 right)
        {
            return new Vector2(left.x + right.x, left.y + right.y);
        }

        public static Vector2 operator -(Vector2 left, Vector2 right)
        {
            return new Vector2(left.x - right.x, left.y - right.y);
        }

        public static Vector2 operator *(Vector2 value, float scale)
        {
            return new Vector2(value.x * scale, value.y * scale);
        }

        public static Vector2 operator *(float scale, Vector2 value)
        {
            return value * scale;
        }
    }

    internal struct Rect
    {
        internal float xMin;
        internal float yMin;
        internal float width;
        internal float height;

        internal Rect(float xMin, float yMin, float width, float height)
        {
            this.xMin = xMin;
            this.yMin = yMin;
            this.width = width;
            this.height = height;
        }

        internal float xMax
        {
            get { return xMin + width; }
        }

        internal float yMax
        {
            get { return yMin + height; }
        }
    }

    internal static class Mathf
    {
        internal static float Max(float left, float right)
        {
            return Math.Max(left, right);
        }

        internal static float Min(float left, float right)
        {
            return Math.Min(left, right);
        }

        internal static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        internal static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        internal static float Sqrt(float value)
        {
            return (float)Math.Sqrt(value);
        }
    }
}
#endif
