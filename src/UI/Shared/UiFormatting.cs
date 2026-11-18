using System;
using System.Globalization;
using UnityEngine;

namespace XingShangMod.UI.Shared
{
    public static class UiFormatting
    {
        public static readonly Color Paper = new Color(0.969f, 0.941f, 0.875f, 1f);
        public static readonly Color MutedPaper = new Color(0.80f, 0.75f, 0.64f, 1f);
        public static readonly Color Gold = new Color(0.76f, 0.60f, 0.33f, 1f);
        public static readonly Color Rise = new Color(0.894f, 0.443f, 0.376f, 1f);
        public static readonly Color Fall = new Color(0.439f, 0.667f, 0.580f, 1f);
        public static readonly Color Ink = new Color(0.126f, 0.122f, 0.106f, 1f);

        public static string Money(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string Money(double value)
        {
            return IsFinite(value)
                ? value.ToString("N0", CultureInfo.InvariantCulture)
                : "—";
        }

        public static string Index(double value)
        {
            return IsFinite(value)
                ? value.ToString("N2", CultureInfo.InvariantCulture)
                : "—";
        }

        public static string Percent(double fraction)
        {
            if (!IsFinite(fraction))
            {
                return "—";
            }
            double percent = fraction * 100.0;
            string prefix = percent > 0.0 ? "▲ +" : percent < 0.0 ? "▼ " : "— ";
            return prefix + percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        public static string SignedMoney(double value)
        {
            if (!IsFinite(value))
            {
                return "—";
            }
            return (value > 0.0 ? "+" : string.Empty) + Money(value);
        }

        public static Color TrendColor(double value)
        {
            return value > 0.0 ? Rise : value < 0.0 ? Fall : MutedPaper;
        }

        public static Color TrendColorOnLight(double value)
        {
            return value > 0.0
                ? new Color(0.64f, 0.20f, 0.14f, 1f)
                : value < 0.0
                    ? new Color(0.18f, 0.43f, 0.30f, 1f)
                    : Ink;
        }

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
