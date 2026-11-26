using System;
using System.Collections.Generic;
using UnityEngine;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    internal sealed class WeeklyChartView : IDisposable
    {
        private bool _hasSignature;
        private int _signature;

        internal WeeklyChartView(
            RectTransform host,
            WeeklyChartMode mode,
            Font font,
            Texture2D dryBrushTexture,
            Font axisFont = null)
        {
            Chart = WeeklyChartGraphic.Mount(host, mode, font, dryBrushTexture, axisFont);
        }

        internal WeeklyChartGraphic Chart { get; private set; }

        internal bool SetPoints(List<WeeklyPoint> points)
        {
            int signature = Signature(points, null);
            if (_hasSignature && _signature == signature)
                return false;
            _hasSignature = true;
            _signature = signature;
            Chart.SetPoints(points);
            return true;
        }

        internal bool SetPoints(List<WeeklyPoint> points, int currentWeek)
        {
            int signature = Signature(points, null) * 31 + currentWeek;
            if (_hasSignature && _signature == signature)
                return false;
            _hasSignature = true;
            _signature = signature;
            Chart.SetPoints(points, currentWeek);
            return true;
        }

        internal bool SetAssetPoints(List<WeeklyPoint> left, List<WeeklyPoint> right, bool profit)
        {
            int signature = unchecked(Signature(left, right) * 31 + (profit ? 1 : 0));
            if (_hasSignature && _signature == signature)
                return false;
            _hasSignature = true;
            _signature = signature;
            if (profit) Chart.SetProfitPoints(left, right);
            else Chart.SetAssetPoints(left, right);
            return true;
        }

        internal void ResetSignature()
        {
            _hasSignature = false;
        }

        public void Dispose()
        {
            _hasSignature = false;
            if (Chart != null)
                Chart.ReleaseInteractionResources();
        }

        private static int Signature(List<WeeklyPoint> first, List<WeeklyPoint> second)
        {
            unchecked
            {
                int hash = 17;
                hash = AddSeries(hash, first);
                hash = hash * 31 + (second == null ? 0 : 1);
                hash = AddSeries(hash, second);
                return hash;
            }
        }

        private static int AddSeries(int hash, List<WeeklyPoint> points)
        {
            if (points == null)
                return hash * 31 - 1;
            hash = hash * 31 + points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                WeeklyPoint point = points[i];
                if (point == null)
                {
                    hash = hash * 31 - 1;
                    continue;
                }
                hash = hash * 31 + (point.ItemId == null ? 0 : StringComparer.Ordinal.GetHashCode(point.ItemId));
                hash = hash * 31 + point.MarketWeekIndex;
                hash = hash * 31 + point.Price.GetHashCode();
                hash = hash * 31 + point.Return.GetHashCode();
                hash = hash * 31 + (point.Delisted ? 1 : 0);
            }
            return hash;
        }
    }
}
