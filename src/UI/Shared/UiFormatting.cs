using System;
using System.Globalization;
using UnityEngine;
using XingShangMod.Progression;
using XingShangMod.Trading;

namespace XingShangMod.UI.Shared
{
    internal static class UiFormatting
    {
        internal static readonly Color Ink = new Color32(32, 35, 33, 255);
        internal static readonly Color InkSoft = new Color32(54, 59, 55, 255);
        internal static readonly Color Muted = new Color32(107, 114, 107, 255);
        internal static readonly Color Paper = new Color32(245, 240, 228, 255);
        internal static readonly Color DarkMuted = new Color32(174, 181, 170, 255);
        internal static readonly Color Gold = new Color32(216, 188, 123, 255);
        internal static readonly Color Red = new Color32(182, 64, 54, 255);
        internal static readonly Color RedDark = new Color32(129, 51, 46, 255);
        internal static readonly Color Green = new Color32(63, 107, 88, 255);
        internal static readonly Color GreenDark = new Color32(46, 81, 67, 255);
        internal static readonly Color RiseOnDark = new Color32(239, 154, 130, 255);
        internal static readonly Color FallOnDark = new Color32(142, 196, 168, 255);

        internal static string Money(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        internal static string Money(double value)
        {
            return IsFinite(value) ? value.ToString("N0", CultureInfo.InvariantCulture) : "—";
        }

        internal static string Number(double value, int decimals)
        {
            return IsFinite(value)
                ? value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
                : "—";
        }

        internal static string SettlementBalance(TradeResult quote)
        {
            string text = "结算后银两 " + Money(quote.CashAfter) + " 文";
            if (quote.DebtAfter > 0)
                text += "，另记欠款 " + Money(quote.DebtAfter) + " 文";
            return text;
        }

        internal static string SignedMoney(double value)
        {
            if (!IsFinite(value))
                return "—";
            return value.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);
        }

        internal static string Percent(double fraction)
        {
            if (!IsFinite(fraction))
                return "—";
            double percent = fraction * 100.0;
            string prefix = percent > 0.0 ? "▲ +" : percent < 0.0 ? "▼ " : "— ";
            return prefix + percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        internal static string Delta(double value, string unit, int decimals = 0)
        {
            if (!IsFinite(value))
                return "—";
            string sign = value > 0.0 ? "+" : string.Empty;
            return sign + Number(value, decimals) + " " + unit;
        }

        internal static Color Trend(double value, bool dark = false)
        {
            if (value > 0.0)
                return dark ? RiseOnDark : Red;
            if (value < 0.0)
                return dark ? FallOnDark : Green;
            return dark ? DarkMuted : Muted;
        }

        internal static Color Profit(double value, bool dark = false)
        {
            return Trend(value, dark);
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static string ActionName(TradeAction action)
        {
            switch (action)
            {
                case TradeAction.Buy: return "买货";
                case TradeAction.Sell: return "卖货";
                case TradeAction.Borrow: return "借货";
                case TradeAction.Return: return "还货";
                default: return "交易";
            }
        }

        internal static string RecordActionName(string action)
        {
            switch (action)
            {
                case TradeActionNames.Buy: return "买货";
                case TradeActionNames.Sell: return "卖货";
                case TradeActionNames.Borrow: return "借货";
                case TradeActionNames.Return: return "还货";
                case TradeActionNames.Expire: return "到期还货";
                default: return action ?? "交易";
            }
        }

        internal static string TradeErrorMessage(TradeResult result, ItemCatalog catalog)
        {
            if (result == null)
                return "交易未能完成，请重新预览。";
            string message = string.IsNullOrEmpty(result.Message) ? "交易未能完成，请重新预览。" : result.Message;
            Domain.ItemDefinition item;
            if (!string.IsNullOrEmpty(result.ItemId) && catalog != null && catalog.TryGetItem(result.ItemId, out item))
                message = item.Name + "：" + message;
            if (result.Error == TradeError.InsufficientCash)
                message += string.Format(CultureInfo.InvariantCulture, "\n需付 {0} 文，可用 {1} 文。",
                    result.RequiredAmount, result.AvailableAmount);
            else if (result.Error == TradeError.InsufficientHolding || result.Error == TradeError.InsufficientShortPosition)
                message += string.Format(CultureInfo.InvariantCulture, "\n指令 {0} 件，可用 {1} 件。",
                    result.RequiredAmount, result.AvailableAmount);
            else if (result.Error == TradeError.CapacityExceeded && result.CapacityBefore != null)
                message += "\n当前货囊 " + CapacitySlots(result.CapacityBefore) + "，负重能力 " +
                    result.CapacityBefore.CarryingCapacity + "。";
            return message;
        }

        internal static string CapacitySlots(CapacitySnapshot capacity)
        {
            if (capacity == null) return "—";
            // A stack limit of zero has no finite slot count. The accounting sentinel
            // must never be printed as if the player actually occupied that many slots.
            if (capacity.UsedSlots == int.MaxValue) return "超载 · 共 " + capacity.TotalSlots + " 格";
            string used = capacity.Error == TradeError.None
                ? capacity.UsedSlots.ToString(CultureInfo.InvariantCulture) : "—";
            return used + "/" + capacity.TotalSlots + " 格";
        }
    }
}
