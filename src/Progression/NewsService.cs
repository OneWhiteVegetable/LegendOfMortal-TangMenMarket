using System;
using System.Collections.Generic;
using System.Globalization;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Progression
{
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
            if (point.Delisted || !state.HasItemAppeared(point.ItemId))
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
