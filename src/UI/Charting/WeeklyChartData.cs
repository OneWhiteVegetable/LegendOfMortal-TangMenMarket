using System;
using System.Collections.Generic;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    internal static class WeeklyChartData
    {
        internal const int HeaderSparkPoints = 14;
        internal const int RankingSparkPoints = 20;
        internal const int CardPoints = 26;
        internal const int DetailPoints = 26;

        internal static int DefaultRange(WeeklyChartMode mode, int availablePoints)
        {
            if (mode == WeeklyChartMode.Spark)
                return availablePoints > HeaderSparkPoints ? RankingSparkPoints : HeaderSparkPoints;
            return mode == WeeklyChartMode.Card ? CardPoints : DetailPoints;
        }

        internal static string StyleKey(string styleSeed, WeeklyChartMode mode, string seriesId)
        {
            string root = string.IsNullOrWhiteSpace(styleSeed) ? "default" : styleSeed.Trim();
            return root + "|" + mode + "|" + (seriesId ?? string.Empty);
        }

        internal static List<WeeklyPoint> VisibleThrough(IList<WeeklyPoint> source, int currentWeek)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            var result = new List<WeeklyPoint>(source.Count);
            if (currentWeek < 0)
                return result;
            for (int i = 0; i < source.Count; i++)
            {
                WeeklyPoint point = source[i];
                if (point != null && point.MarketWeekIndex >= 0 && point.MarketWeekIndex <= currentWeek)
                    result.Add(point);
            }
            return result;
        }

        internal static bool HasCustomPriceZoom(float factor, float centerRatio)
        {
            return Math.Abs(factor - 1f) > 0.000001f || Math.Abs(centerRatio - 0.5f) > 0.000001f;
        }

        internal static WeeklyChartViewport LoadViewport(
            int oldCount,
            float oldStart,
            int oldLength,
            int newLength,
            bool preserve,
            int defaultRange,
            int requestedRange,
            bool customRange)
        {
            bool wasAtEnd = oldLength == 0 ||
                oldStart >= Math.Max(0, oldLength - oldCount) - 0.000001f;
            int retainedRange = customRange || requestedRange < 1 ? oldCount : requestedRange;
            int desiredCount = preserve ? retainedRange : defaultRange;
            int count = Clamp(desiredCount, 1, Math.Max(1, newLength));
            float start = newLength > 0
                ? preserve && !wasAtEnd
                    ? Clamp(oldStart, 0f, newLength - count)
                    : newLength - count
                : 0f;
            return new WeeklyChartViewport(count, start);
        }

        internal static WeeklyChartViewport ResetViewport(int length, int defaultRange)
        {
            int count = Math.Max(1, Math.Min(defaultRange, length));
            return new WeeklyChartViewport(count, Math.Max(0, length - count));
        }

        internal static WeeklyChartViewport ChangeRange(
            float currentStart,
            int currentCount,
            int length,
            int nextCount,
            float? anchorRatio)
        {
            if (length == 0)
                return new WeeklyChartViewport(1, 0f);
            int count = Clamp(nextCount, 1, length);
            bool wasAtEnd = currentStart >= length - currentCount - 0.000001f;
            float nextStart;
            if (anchorRatio.HasValue)
            {
                float anchor = currentStart + anchorRatio.Value * Math.Max(0, currentCount - 1);
                nextStart = anchor - anchorRatio.Value * Math.Max(0, count - 1);
            }
            else if (wasAtEnd)
            {
                nextStart = length - count;
            }
            else
            {
                float center = currentStart + (currentCount - 1) * 0.5f;
                nextStart = center - (count - 1) * 0.5f;
            }
            return new WeeklyChartViewport(
                count,
                Clamp(nextStart, 0f, Math.Max(0, length - count)));
        }

        internal static float Pan(float currentStart, int count, int length, float deltaX, float plotWidth)
        {
            if (plotWidth <= 0f)
                return currentStart;
            float nextStart = currentStart - deltaX * Math.Max(0, count - 1) / plotWidth;
            return Clamp(nextStart, 0f, Math.Max(0, length - count));
        }

        internal static float ScrollRangeFactor(float scrollDelta)
        {
            return Clamp((float)Math.Exp(-scrollDelta * 0.12f), 0.5f, 2f);
        }

        internal static void ChangePriceZoom(
            float startFactor,
            float startCenter,
            float nextFactor,
            float anchorRatio,
            out float factor,
            out float center)
        {
            factor = Clamp(nextFactor, 0.6f, 8f);
            float offset = anchorRatio - 0.5f;
            center = Clamp(
                startCenter + offset / startFactor - offset / factor,
                -2f,
                3f);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal struct WeeklyChartViewport
    {
        internal WeeklyChartViewport(int count, float start)
        {
            Count = count;
            Start = start;
        }

        internal int Count;
        internal float Start;
    }
}
