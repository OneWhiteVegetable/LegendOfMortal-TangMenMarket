using System;
using System.Collections.Generic;
using UnityEngine;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    public sealed class WeeklyChartSeries
    {
        public WeeklyChartSeries(
            string id,
            string label,
            List<WeeklyPoint> visiblePoints,
            Color32? color = null)
        {
            if (visiblePoints == null)
                throw new ArgumentNullException("visiblePoints");
            Id = string.IsNullOrWhiteSpace(id) ? "series" : id.Trim();
            Label = label ?? string.Empty;
            VisiblePoints = visiblePoints;
            Color = color;
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public List<WeeklyPoint> VisiblePoints { get; private set; }
        public Color32? Color { get; private set; }
    }
}
