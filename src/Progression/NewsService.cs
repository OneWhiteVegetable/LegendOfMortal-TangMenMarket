using System;
using System.Collections.Generic;
using System.Globalization;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;

namespace XingShangMod.Progression
{
    [Flags]
    public enum DelistingNoticeKind
    {
        None = 0,
        LongFrozen = 1,
        ShortSettled = 2,
        ShortOutstanding = 4
    }

    public sealed class DelistingNotice
    {
        public string ItemId = string.Empty;
        public int MarketWeekIndex = -1;
        public DelistingNoticeKind Kind = DelistingNoticeKind.None;
        public long FrozenLongQuantity;
        public long SettledShortQuantity;
        public long OutstandingShortQuantity;
        public double GrossRepayment;
        public long SettlementCashDelta;
        public long SettlementDebtDelta;
    }

    public sealed class NewsService
    {
        public const double ReturnThreshold = 0.04908;

        private readonly NewsCatalog _catalog;

        public NewsService(NewsCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException("catalog");
            _catalog = catalog;
        }

        public NewsRecord Generate(RunState state, WeeklyPoint point)
        {
            NewsRecord record;
            return TryGenerate(state, point, out record) ? record : null;
        }

        public bool TryGenerate(RunState state, WeeklyPoint point, out NewsRecord record)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (point == null)
                throw new ArgumentNullException("point");

            record = null;
            if (state.Clock == null || point.MarketWeekIndex > state.Clock.MarketWeekIndex ||
                point.Delisted || !state.IsItemVisibleAt(point.ItemId, point.MarketWeekIndex))
                return false;

            SecurityBinding binding = state.FindSecurityBinding(point.ItemId);
            if (binding != null && binding.IsDelisted)
                return false;
            if (double.IsNaN(point.Return) || double.IsInfinity(point.Return)
                || Math.Abs(point.Return) <= ReturnThreshold)
                return false;

            NewsRecord existing = FindExisting(state, point.ItemId, point.MarketWeekIndex);
            if (existing != null)
            {
                record = existing;
                return true;
            }

            int direction = point.Return > 0.0 ? 1 : -1;
            int periodIndex = NewsCatalog.MapPeriodIndex(point.GameYear, point.GameMonth);
            IReadOnlyList<NewsEntry> candidates = _catalog.GetCandidates(
                point.ItemId,
                periodIndex,
                direction);
            if (candidates.Count == 0)
                return false;

            int selectionIndex = RunRandom.NextIndex(
                state.RunSeed,
                candidates.Count,
                "news",
                point.ItemId,
                point.MarketWeekIndex.ToString(CultureInfo.InvariantCulture),
                direction.ToString(CultureInfo.InvariantCulture));
            NewsEntry selected = candidates[selectionIndex];
            record = new NewsRecord
            {
                NewsId = point.ItemId + ":" + point.MarketWeekIndex.ToString(CultureInfo.InvariantCulture),
                ItemId = point.ItemId,
                MarketWeekIndex = point.MarketWeekIndex,
                GameYear = point.GameYear,
                GameMonth = point.GameMonth,
                GameRound = point.GameRound,
                Direction = direction,
                Return = point.Return,
                PoolId = selected.PoolId,
                SelectionIndex = selectionIndex,
                Text = RemovePrefix(selected.Text)
            };

            if (state.NewsHistory == null)
                state.NewsHistory = new List<NewsRecord>();
            state.NewsHistory.Add(record);
            return true;
        }

        public static string RemovePrefix(string text)
        {
            string result = (text ?? string.Empty).Trim();
            while (result.Length > 0)
            {
                int originalLength = result.Length;
                result = RemoveNumberPrefix(result);
                result = RemoveBracketPrefix(result);
                if (result.Length == originalLength)
                    break;
            }
            return result.Trim();
        }

        public static DelistingNotice BuildDelistingNotice(RunState state, string itemId, int marketWeekIndex)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("A delisted item ID is required.", "itemId");
            if (marketWeekIndex < 0)
                throw new ArgumentOutOfRangeException("marketWeekIndex");

            DelistingNotice result = new DelistingNotice
            {
                ItemId = itemId,
                MarketWeekIndex = marketWeekIndex
            };
            if (state.Holdings != null)
            {
                for (int i = 0; i < state.Holdings.Count; i++)
                {
                    Holding holding = state.Holdings[i];
                    if (holding != null && holding.Quantity > 0 &&
                        string.Equals(holding.ItemId, itemId, StringComparison.Ordinal))
                        result.FrozenLongQuantity += holding.Quantity;
                }
            }
            if (state.ShortBatches != null)
            {
                for (int i = 0; i < state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = state.ShortBatches[i];
                    if (batch != null && batch.Quantity > 0 &&
                        string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                        result.OutstandingShortQuantity += batch.Quantity;
                }
            }
            if (state.TradeHistory != null)
            {
                for (int i = 0; i < state.TradeHistory.Count; i++)
                {
                    TradeRecord trade = state.TradeHistory[i];
                    if (trade == null || trade.Quantity <= 0 || trade.MarketWeekIndex != marketWeekIndex ||
                        !string.Equals(trade.ItemId, itemId, StringComparison.Ordinal) ||
                        !string.Equals(trade.Action, TradeActionNames.Expire, StringComparison.Ordinal))
                        continue;

                    result.SettledShortQuantity += trade.Quantity;
                    result.SettlementCashDelta += trade.CashDelta;
                    result.SettlementDebtDelta += trade.DebtDelta;
                    if (!double.IsNaN(trade.UnitPrice) && !double.IsInfinity(trade.UnitPrice) && trade.UnitPrice > 0.0)
                        result.GrossRepayment += trade.UnitPrice * trade.Quantity;
                }
            }

            if (result.FrozenLongQuantity > 0)
                result.Kind |= DelistingNoticeKind.LongFrozen;
            if (result.SettledShortQuantity > 0)
                result.Kind |= DelistingNoticeKind.ShortSettled;
            if (result.OutstandingShortQuantity > 0)
                result.Kind |= DelistingNoticeKind.ShortOutstanding;
            return result;
        }

        private static NewsRecord FindExisting(RunState state, string itemId, int marketWeekIndex)
        {
            if (state.NewsHistory == null)
                return null;
            for (int i = 0; i < state.NewsHistory.Count; i++)
            {
                NewsRecord record = state.NewsHistory[i];
                if (record != null && record.MarketWeekIndex == marketWeekIndex
                    && string.Equals(record.ItemId, itemId, StringComparison.Ordinal))
                    return record;
            }
            return null;
        }

        private static string RemoveNumberPrefix(string text)
        {
            int index = 0;
            while (index < text.Length && char.IsDigit(text[index]))
                index++;
            if (index == 0 || index >= text.Length)
                return text;
            if (text[index] != '.' && text[index] != '、' && text[index] != '．')
                return text;

            index++;
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
            return text.Substring(index);
        }

        private static string RemoveBracketPrefix(string text)
        {
            if (!text.StartsWith("【", StringComparison.Ordinal))
                return text;
            int closingIndex = text.IndexOf('】');
            if (closingIndex < 0)
                return text;
            return text.Substring(closingIndex + 1).TrimStart();
        }
    }
}
