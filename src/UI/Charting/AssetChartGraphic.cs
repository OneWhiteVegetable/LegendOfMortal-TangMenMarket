using System;
using System.Collections.Generic;
using UnityEngine;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    internal sealed class AssetChartGraphic
    {
        private readonly WeeklyChartView _view;

        private AssetChartGraphic(WeeklyChartView view)
        {
            _view = view;
        }

        internal WeeklyChartGraphic Chart { get { return _view.Chart; } }

        internal static AssetChartGraphic Mount(
            RectTransform host,
            Font labelFont,
            Texture2D dryBrushTexture)
        {
            if (host == null)
                throw new ArgumentNullException("host");
            return new AssetChartGraphic(new WeeklyChartView(
                host,
                WeeklyChartMode.Assets,
                labelFont,
                dryBrushTexture));
        }

        internal int SetPoints(IReadOnlyList<AssetPoint> visiblePoints, int currentWeek, bool profit)
        {
            if (visiblePoints == null) throw new ArgumentNullException("visiblePoints");
            var left = new List<WeeklyPoint>(visiblePoints.Count);
            var right = new List<WeeklyPoint>(visiblePoints.Count);
            var weeks = new HashSet<int>();
            for (int i = 0; i < visiblePoints.Count; i++)
            {
                AssetPoint source = visiblePoints[i];
                if (source == null || source.MarketWeekIndex < 0 || source.MarketWeekIndex > currentWeek) continue;
                if (profit && !source.HasProfitMetrics) continue;
                double first = profit ? source.TotalProfit : source.Cash;
                double second = profit ? (source.CapitalBasis > 0 ? source.TotalProfit / source.CapitalBasis * 100.0 : 0.0) : source.LongValue;
                if (double.IsNaN(first) || double.IsInfinity(first) || double.IsNaN(second) || double.IsInfinity(second)) continue;
                left.Add(CreatePoint(source, first));
                if (!profit || source.CapitalBasis > 0) right.Add(CreatePoint(source, second));
                weeks.Add(source.MarketWeekIndex);
            }
            _view.SetAssetPoints(left, right, profit);
            return weeks.Count;
        }

        internal void ResetView()
        {
            Chart.ResetView();
        }

        private static WeeklyPoint CreatePoint(AssetPoint source, double value)
        {
            return new WeeklyPoint
            {
                ItemId = "Assets",
                MarketWeekIndex = source.MarketWeekIndex,
                GameYear = source.GameYear,
                GameMonth = source.GameMonth,
                GameRound = source.GameRound,
                SourceRecordIndex = -1,
                Price = value,
                Observed = true
            };
        }
    }
}
