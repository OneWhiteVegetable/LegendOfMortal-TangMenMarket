using System;
using System.Collections.Generic;
using System.Globalization;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;

namespace XingShangMod.UI.Shared
{
    internal sealed class ShortPriceProjection
    {
        internal long Quantity;
        internal long NetProceeds;
        internal double OriginalValue;
        internal bool HasCompleteOriginalPrices;

        internal double AverageOriginalPrice
        {
            get { return Quantity <= 0 || !HasCompleteOriginalPrices ? 0.0 : OriginalValue / Quantity; }
        }

        internal double AverageNetProceeds
        {
            get { return Quantity <= 0 ? 0.0 : (double)NetProceeds / Quantity; }
        }
    }

    internal sealed class PortfolioProjection
    {
        private static readonly string[] EmptyItemIds = new string[0];

        internal double LongCost;
        internal double ShortProceeds;
        internal double LongValue;
        internal double ShortLiability;
        internal double FloatingProfit;
        internal double FloatingReturnRate;
        internal double WeeklyRevaluation;
        internal double WeeklyBasis;
        internal double WeeklyReturnRate;
        internal bool HasCompletePrices;
        internal bool HasCompleteWeeklyPrices;
        internal IReadOnlyList<string> MissingPriceItemIds = EmptyItemIds;
        internal IReadOnlyList<string> MissingWeeklyPriceItemIds = EmptyItemIds;
    }

    internal static class MarketQuery
    {
        internal static string ItemTotalReturnCaption(IList<WeeklyPoint> history)
        {
            int firstWeek = FirstPriceWeek(history);
            if (firstWeek < 0)
                return "尚无货价";
            return firstWeek == 0 ? "开局至今" : "市周 " + firstWeek + " 起";
        }
        internal const string RankingPeriodCaption = "自各货首条行情至今 · 累计涨跌";

        internal static Dictionary<string, ItemDefinition> BuildDefinitions(ItemCatalog catalog)
        {
            Dictionary<string, ItemDefinition> result = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            if (catalog == null || catalog.Items == null)
                return result;
            for (int i = 0; i < catalog.Items.Count; i++)
            {
                ItemDefinition item = catalog.Items[i];
                if (item != null && !string.IsNullOrEmpty(item.ItemId) && !result.ContainsKey(item.ItemId))
                    result.Add(item.ItemId, item);
            }
            return result;
        }

        internal static List<ItemDefinition> AppearedItems(RunState state, ItemCatalog catalog)
        {
            List<ItemDefinition> result = new List<ItemDefinition>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (state == null || state.AppearedItemIds == null || catalog == null)
                return result;
            for (int i = 0; i < state.AppearedItemIds.Count; i++)
            {
                string id = state.AppearedItemIds[i];
                ItemDefinition item;
                if (!string.IsNullOrEmpty(id) && seen.Add(id) &&
                    (state.Clock == null || state.IsItemVisibleAt(id, state.Clock.MarketWeekIndex)) &&
                    catalog.TryGetItem(id, out item))
                    result.Add(item);
            }
            return result;
        }

        internal static List<WeeklyPoint> ItemHistory(ModApplication application, string itemId)
        {
            return application == null
                ? new List<WeeklyPoint>()
                : CopyValidPoints(application.GetItemHistory(itemId));
        }

        internal static List<WeeklyPoint> IndexHistory(ModApplication application)
        {
            return application == null
                ? new List<WeeklyPoint>()
                : CopyValidPoints(application.GetIndexHistory());
        }

        internal static WeeklyPoint CurrentPoint(ModApplication application, string itemId)
        {
            List<WeeklyPoint> history = ItemHistory(application, itemId);
            return history.Count == 0 ? null : history[history.Count - 1];
        }

        internal static double WeeklyReturn(IList<WeeklyPoint> history)
        {
            if (history == null || history.Count == 0)
                return 0.0;
            WeeklyPoint current = history[history.Count - 1];
            if (UiFormatting.IsFinite(current.Return))
                return current.Return;
            if (history.Count < 2 || history[history.Count - 2].Price == 0.0)
                return 0.0;
            return current.Price / history[history.Count - 2].Price - 1.0;
        }

        internal static double TotalReturn(IList<WeeklyPoint> history)
        {
            WeeklyPoint first = null;
            WeeklyPoint last = null;
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    WeeklyPoint point = history[i];
                    if (!IsValidPricePoint(point))
                        continue;
                    if (first == null)
                        first = point;
                    last = point;
                }
            }
            if (first == null || ReferenceEquals(first, last))
                return 0.0;
            return last.Price / first.Price - 1.0;
        }

        internal static int FirstPriceWeek(IList<WeeklyPoint> history)
        {
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    if (IsValidPricePoint(history[i]))
                        return history[i].MarketWeekIndex;
                }
            }
            return -1;
        }

        internal static string GameRoundName(int gameRound)
        {
            switch (gameRound)
            {
                case 1: return "上旬";
                case 2: return "中旬";
                case 3: return "下旬";
                default: return string.Empty;
            }
        }

        internal static string FormatGamePeriod(int gameYear, int gameMonth, int gameRound)
        {
            string round = GameRoundName(gameRound);
            if (gameYear < 1 || gameMonth < 1 || gameMonth > 12 || round.Length == 0)
                return string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}年{1}月{2}",
                gameYear,
                gameMonth,
                round);
        }

        internal static string FormatMarketWeek(int marketWeekIndex)
        {
            return marketWeekIndex < 0
                ? string.Empty
                : "市周 " + marketWeekIndex.ToString(CultureInfo.InvariantCulture);
        }

        internal static string FormatMarketTime(
            int gameYear,
            int gameMonth,
            int gameRound,
            int marketWeekIndex)
        {
            string period = FormatGamePeriod(gameYear, gameMonth, gameRound);
            string week = FormatMarketWeek(marketWeekIndex);
            if (period.Length == 0)
                return week;
            return week.Length == 0 ? period : period + " · " + week;
        }

        internal static string FormatMarketTime(WeeklyPoint point)
        {
            return point == null
                ? string.Empty
                : FormatMarketTime(point.GameYear, point.GameMonth, point.GameRound, point.MarketWeekIndex);
        }

        internal static bool IsDelisted(ModApplication application, RunState state, string itemId)
        {
            SecurityBinding binding = state == null ? null : state.FindSecurityBinding(itemId);
            if (binding != null && binding.IsDelisted)
                return true;
            WeeklyPoint current = CurrentPoint(application, itemId);
            return current != null && current.Delisted;
        }

        internal static long ShortQuantity(RunState state, string itemId)
        {
            long result = 0;
            if (state == null || state.ShortBatches == null)
                return result;
            for (int i = 0; i < state.ShortBatches.Count; i++)
            {
                ShortBatch batch = state.ShortBatches[i];
                if (batch != null && batch.Quantity > 0 &&
                    string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    result += batch.Quantity;
            }
            return result;
        }

        internal static long ShortProceeds(RunState state, string itemId)
        {
            long result = 0;
            if (state == null || state.ShortBatches == null)
                return result;
            for (int i = 0; i < state.ShortBatches.Count; i++)
            {
                ShortBatch batch = state.ShortBatches[i];
                if (batch != null && batch.Quantity > 0 &&
                    string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    result += batch.Proceeds;
            }
            return result;
        }

        internal static ShortPriceProjection BuildShortPriceProjection(RunState state, string itemId)
        {
            ShortPriceProjection result = new ShortPriceProjection
            {
                HasCompleteOriginalPrices = true
            };
            if (state == null || state.ShortBatches == null)
                return result;

            for (int i = 0; i < state.ShortBatches.Count; i++)
            {
                ShortBatch batch = state.ShortBatches[i];
                if (batch == null || batch.Quantity <= 0 ||
                    !string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    continue;

                result.Quantity += batch.Quantity;
                result.NetProceeds += batch.Proceeds;
                double unitPrice;
                if (TryGetOriginalBorrowPrice(state.TradeHistory, batch, out unitPrice))
                    result.OriginalValue += unitPrice * batch.Quantity;
                else
                    result.HasCompleteOriginalPrices = false;
            }
            return result;
        }

        internal static string ShortPriceText(ShortPriceProjection projection)
        {
            if (projection == null || projection.Quantity <= 0)
                return "借价 — · 97%实收 —";
            string original = projection.HasCompleteOriginalPrices
                ? UiFormatting.Number(projection.AverageOriginalPrice, 2) + " 文"
                : "—";
            return "借价 " + original + " · 97%实收 " +
                UiFormatting.Number(projection.AverageNetProceeds, 2) + " 文";
        }

        internal static PortfolioProjection BuildPortfolioProjection(RunState state)
        {
            PortfolioProjection result = new PortfolioProjection
            {
                HasCompletePrices = true,
                HasCompleteWeeklyPrices = true
            };
            if (state == null)
            {
                result.HasCompletePrices = false;
                result.HasCompleteWeeklyPrices = false;
                return result;
            }

            int currentWeek = CurrentWeek(state);
            HashSet<string> missingPrices = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> missingWeeklyPrices = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, ValuationPrices> priceCache =
                new Dictionary<string, ValuationPrices>(StringComparer.Ordinal);

            if (state.Holdings != null)
            {
                for (int i = 0; i < state.Holdings.Count; i++)
                {
                    Holding holding = state.Holdings[i];
                    if (holding == null || holding.Quantity <= 0)
                        continue;
                    result.LongCost += holding.TotalCost;
                    ValuationPrices prices = GetValuationPrices(state, holding.ItemId, currentWeek, priceCache);
                    AddPositionValuation(
                        result,
                        holding.ItemId,
                        holding.Quantity,
                        true,
                        prices,
                        missingPrices,
                        missingWeeklyPrices);
                }
            }

            if (state.ShortBatches != null)
            {
                for (int i = 0; i < state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = state.ShortBatches[i];
                    if (batch == null || batch.Quantity <= 0)
                        continue;
                    result.ShortProceeds += batch.Proceeds;
                    ValuationPrices prices = GetValuationPrices(state, batch.ItemId, currentWeek, priceCache);
                    AddPositionValuation(
                        result,
                        batch.ItemId,
                        batch.Quantity,
                        false,
                        prices,
                        missingPrices,
                        missingWeeklyPrices);
                }
            }

            result.HasCompletePrices = missingPrices.Count == 0;
            result.HasCompleteWeeklyPrices = missingWeeklyPrices.Count == 0;
            result.MissingPriceItemIds = SortedItemIds(missingPrices);
            result.MissingWeeklyPriceItemIds = SortedItemIds(missingWeeklyPrices);
            result.FloatingProfit = result.LongValue - result.LongCost +
                result.ShortProceeds - result.ShortLiability;
            double positionBasis = result.LongCost + result.ShortProceeds;
            result.FloatingReturnRate = positionBasis > 0.0
                ? result.FloatingProfit / positionBasis
                : 0.0;
            result.WeeklyReturnRate = result.WeeklyBasis > 0.0
                ? result.WeeklyRevaluation / result.WeeklyBasis
                : 0.0;
            return result;
        }

        internal static List<NewsRecord> CurrentWeekNews(
            ModApplication application,
            RunState state,
            string itemId = null)
        {
            List<NewsRecord> result = new List<NewsRecord>();
            if (application == null || state == null)
                return result;
            int currentWeek = CurrentWeek(state);
            IReadOnlyList<NewsRecord> history = application.GetNewsHistory();
            for (int i = 0; i < history.Count; i++)
            {
                NewsRecord record = history[i];
                if (record != null && record.MarketWeekIndex == currentWeek &&
                    state.HasItemAppeared(record.ItemId) &&
                    (string.IsNullOrEmpty(itemId) ||
                     string.Equals(record.ItemId, itemId, StringComparison.Ordinal)))
                    result.Add(record);
            }
            result.Sort(delegate (NewsRecord left, NewsRecord right)
            {
                return string.Compare(left.NewsId, right.NewsId, StringComparison.Ordinal);
            });
            return result;
        }

        internal static NewsRecord LatestNews(ModApplication application, RunState state, string itemId = null)
        {
            List<NewsRecord> news = CurrentWeekNews(application, state, itemId);
            return news.Count == 0 ? null : news[news.Count - 1];
        }

        internal static string IconKey(ItemDefinition item)
        {
            string path = item == null ? string.Empty : item.IconPath ?? string.Empty;
            int slash = path.LastIndexOf('/');
            if (slash >= 0)
                path = path.Substring(slash + 1);
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 4);
            return path;
        }

        internal static int CurrentWeek(RunState state)
        {
            return state == null || state.Clock == null ? -1 : state.Clock.MarketWeekIndex;
        }

        private static bool TryGetOriginalBorrowPrice(
            IReadOnlyList<TradeRecord> history,
            ShortBatch batch,
            out double unitPrice)
        {
            unitPrice = 0.0;
            if (history == null || batch == null)
                return false;

            bool found = false;
            bool valid = true;
            long quantity = 0;
            double originalValue = 0.0;
            string batchId = batch.BatchId ?? string.Empty;
            if (batchId.Length == 0)
                return false;
            for (int i = 0; i < history.Count; i++)
            {
                TradeRecord trade = history[i];
                if (trade == null || !string.Equals(trade.Action, TradeActionNames.Borrow, StringComparison.Ordinal) ||
                    trade.MarketWeekIndex != batch.OpenWeek ||
                    !string.Equals(trade.ItemId, batch.ItemId, StringComparison.Ordinal) ||
                    !string.Equals(trade.BatchId ?? string.Empty, batchId, StringComparison.Ordinal))
                    continue;

                found = true;
                if (trade.Quantity <= 0 || !UiFormatting.IsFinite(trade.UnitPrice) || trade.UnitPrice <= 0.0)
                {
                    valid = false;
                    continue;
                }
                quantity += trade.Quantity;
                originalValue += trade.UnitPrice * trade.Quantity;
            }

            if (!found || !valid || quantity <= 0 || !UiFormatting.IsFinite(originalValue))
                return false;
            unitPrice = originalValue / quantity;
            return UiFormatting.IsFinite(unitPrice) && unitPrice > 0.0;
        }

        private static ValuationPrices GetValuationPrices(
            RunState state,
            string itemId,
            int currentWeek,
            IDictionary<string, ValuationPrices> cache)
        {
            string key = itemId ?? string.Empty;
            ValuationPrices result;
            if (cache.TryGetValue(key, out result))
                return result;
            result = FindValuationPrices(state, key, currentWeek);
            cache.Add(key, result);
            return result;
        }

        private static ValuationPrices FindValuationPrices(RunState state, string itemId, int currentWeek)
        {
            ValuationPrices result = new ValuationPrices();
            if (state.PriceHistory == null || currentWeek < 0)
                return result;

            int currentPointWeek = -1;
            for (int i = 0; i < state.PriceHistory.Count; i++)
            {
                WeeklyPoint point = state.PriceHistory[i];
                if (!IsValidPricePoint(point) || point.MarketWeekIndex < 0 ||
                    point.MarketWeekIndex > currentWeek ||
                    !string.Equals(point.ItemId, itemId, StringComparison.Ordinal))
                    continue;
                if (!result.HasCurrent || point.MarketWeekIndex > currentPointWeek)
                {
                    result.HasCurrent = true;
                    result.Current = point.Price;
                    currentPointWeek = point.MarketWeekIndex;
                }
            }

            if (!result.HasCurrent)
                return result;
            int previousPointWeek = -1;
            for (int i = 0; i < state.PriceHistory.Count; i++)
            {
                WeeklyPoint point = state.PriceHistory[i];
                if (!IsValidPricePoint(point) || point.MarketWeekIndex < 0 ||
                    point.MarketWeekIndex >= currentPointWeek ||
                    !string.Equals(point.ItemId, itemId, StringComparison.Ordinal))
                    continue;
                if (!result.HasPrevious || point.MarketWeekIndex > previousPointWeek)
                {
                    result.HasPrevious = true;
                    result.Previous = point.Price;
                    previousPointWeek = point.MarketWeekIndex;
                }
            }
            return result;
        }

        private static void AddPositionValuation(
            PortfolioProjection result,
            string itemId,
            long quantity,
            bool isLong,
            ValuationPrices prices,
            ISet<string> missingPrices,
            ISet<string> missingWeeklyPrices)
        {
            string key = itemId ?? string.Empty;
            if (!prices.HasCurrent)
            {
                missingPrices.Add(key);
                missingWeeklyPrices.Add(key);
                return;
            }

            double currentValue = prices.Current * quantity;
            if (isLong)
                result.LongValue += currentValue;
            else
                result.ShortLiability += currentValue;

            if (!prices.HasPrevious)
            {
                missingWeeklyPrices.Add(key);
                return;
            }
            double previousValue = prices.Previous * quantity;
            result.WeeklyBasis += previousValue;
            result.WeeklyRevaluation += isLong
                ? currentValue - previousValue
                : previousValue - currentValue;
        }

        private static IReadOnlyList<string> SortedItemIds(HashSet<string> itemIds)
        {
            if (itemIds.Count == 0)
                return new string[0];
            string[] result = new string[itemIds.Count];
            itemIds.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static bool IsValidPricePoint(WeeklyPoint point)
        {
            return point != null && UiFormatting.IsFinite(point.Price) && point.Price > 0.0;
        }

        private static List<WeeklyPoint> CopyValidPoints(IReadOnlyList<WeeklyPoint> source)
        {
            List<WeeklyPoint> result = new List<WeeklyPoint>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                WeeklyPoint point = source[i];
                if (IsValidPricePoint(point))
                    result.Add(point);
            }
            return result;
        }

        private struct ValuationPrices
        {
            internal double Current;
            internal double Previous;
            internal bool HasCurrent;
            internal bool HasPrevious;
        }
    }
}
