using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    public readonly struct WeeklyChartSegment
    {
        public int StartPointIndex { get; }
        public int EndPointIndex { get; }
        public int StartMarketWeekIndex { get; }
        public int EndMarketWeekIndex { get; }
        public double StartPrice { get; }
        public double EndPrice { get; }
        public WeeklyChartDirection Direction { get; }
        public Color32 StartColor { get; }
        public Color32 EndColor { get; }

        internal WeeklyChartSegment(
            int startPointIndex,
            WeeklyPoint start,
            WeeklyPoint end,
            WeeklyChartDirection direction,
            Color32 startColor,
            Color32 endColor)
        {
            StartPointIndex = startPointIndex;
            EndPointIndex = startPointIndex + 1;
            StartMarketWeekIndex = start.MarketWeekIndex;
            EndMarketWeekIndex = end.MarketWeekIndex;
            StartPrice = start.Price;
            EndPrice = end.Price;
            Direction = direction;
            StartColor = startColor;
            EndColor = endColor;
        }
    }

    public static class WeeklyChartSegmentBuilder
    {
        public static readonly Color32 DefaultRisingColor = new Color32(182, 64, 54, 255);
        public static readonly Color32 DefaultFallingColor = new Color32(63, 107, 88, 255);
        public static readonly Color32 DefaultFlatColor = new Color32(116, 103, 84, 255);

        private static readonly ReadOnlyCollection<WeeklyChartSegment> EmptySegments =
            new ReadOnlyCollection<WeeklyChartSegment>(new WeeklyChartSegment[0]);

        public static ReadOnlyCollection<WeeklyChartSegment> Empty
        {
            get { return EmptySegments; }
        }

        public static ReadOnlyCollection<WeeklyChartSegment> Build(List<WeeklyPoint> visiblePoints)
        {
            return Build(
                visiblePoints,
                DefaultRisingColor,
                DefaultFallingColor,
                DefaultFlatColor);
        }

        public static ReadOnlyCollection<WeeklyChartSegment> Build(
            List<WeeklyPoint> visiblePoints,
            Color32 risingColor,
            Color32 fallingColor,
            Color32 flatColor)
        {
            if (visiblePoints == null)
                throw new ArgumentNullException("visiblePoints");

            if (visiblePoints.Count < 2)
            {
                for (int i = 0; i < visiblePoints.Count; i++)
                    Validate(visiblePoints[i], i);
                return EmptySegments;
            }

            var segments = new WeeklyChartSegment[visiblePoints.Count - 1];
            Validate(visiblePoints[0], 0);
            Color32 previousColor = flatColor;
            for (int i = 1; i < visiblePoints.Count; i++)
            {
                WeeklyPoint start = visiblePoints[i - 1];
                WeeklyPoint end = visiblePoints[i];
                Validate(end, i);

                WeeklyChartDirection direction = end.Price > start.Price
                    ? WeeklyChartDirection.Rising
                    : end.Price < start.Price
                        ? WeeklyChartDirection.Falling
                        : WeeklyChartDirection.Flat;
                Color32 endColor = GetColor(direction, risingColor, fallingColor, flatColor);
                Color32 startColor = i == 1 ? endColor : previousColor;
                segments[i - 1] = new WeeklyChartSegment(
                    i - 1,
                    start,
                    end,
                    direction,
                    startColor,
                    endColor);
                previousColor = endColor;
            }

            return new ReadOnlyCollection<WeeklyChartSegment>(segments);
        }

        internal static WeeklyPoint CopyVisiblePoint(WeeklyPoint source, int index)
        {
            Validate(source, index);
            return new WeeklyPoint
            {
                ItemId = source.ItemId,
                MarketWeekIndex = source.MarketWeekIndex,
                GameYear = source.GameYear,
                GameMonth = source.GameMonth,
                GameRound = source.GameRound,
                SourceRecordIndex = source.SourceRecordIndex,
                Price = source.Price,
                BaseReturn = source.BaseReturn,
                Return = source.Return,
                Observed = source.Observed,
                DataExhausted = source.DataExhausted,
                Delisted = source.Delisted
            };
        }

        private static Color32 GetColor(
            WeeklyChartDirection direction,
            Color32 risingColor,
            Color32 fallingColor,
            Color32 flatColor)
        {
            switch (direction)
            {
                case WeeklyChartDirection.Rising:
                    return risingColor;
                case WeeklyChartDirection.Falling:
                    return fallingColor;
                default:
                    return flatColor;
            }
        }

        private static void Validate(WeeklyPoint point, int index)
        {
            if (point == null)
                throw new ArgumentException("Visible point " + index + " is null.", "visiblePoints");
            if (double.IsNaN(point.Price) || double.IsInfinity(point.Price))
            {
                throw new ArgumentException(
                    "Visible point " + index + " must have a finite price.",
                    "visiblePoints");
            }
        }
    }
}
