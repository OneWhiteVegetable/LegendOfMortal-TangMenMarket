using System;
using System.Collections.Generic;
using System.Globalization;
using XingShangMod.Data;
using XingShangMod.Domain;

namespace XingShangMod.Market
{
    public sealed class MarketSelector
    {
        public const int IndexReserveWeeks = 52;
        public const int SecurityReserveWeeks = 192;

        private readonly WeeklyDataRepository _repository;
        private readonly RunRandom _random;

        public MarketSelector(WeeklyDataRepository repository, int runSeed)
        {
            if (repository == null)
                throw new ArgumentNullException("repository");
            _repository = repository;
            _random = new RunRandom(runSeed);
        }

        public AnnualIndexBinding CreateAnnualIndexBinding(int year, double startIndexValue)
        {
            return CreateAnnualIndexBinding(year, startIndexValue, null);
        }

        public AnnualIndexBinding CreateAnnualIndexBinding(
            int year,
            double startIndexValue,
            string previousSecurityId)
        {
            if (year < 1)
                throw new ArgumentOutOfRangeException("year");
            if (double.IsNaN(startIndexValue) || double.IsInfinity(startIndexValue) || startIndexValue <= 0.0)
                throw new ArgumentOutOfRangeException("startIndexValue");

            string stableKey = year.ToString(CultureInfo.InvariantCulture);
            SelectionResult selection = Select(
                _repository.Catalog.Indices,
                "annual-index",
                stableKey,
                IndexReserveWeeks,
                true,
                previousSecurityId);
            return new AnnualIndexBinding
            {
                Year = year,
                SecurityId = selection.Entry.SecurityId,
                Code = selection.Security.Header.Code,
                DataPath = selection.Entry.Path,
                StartRecordIndex = selection.StartRecordIndex,
                NextRecordIndex = selection.StartRecordIndex + 1,
                StartIndexValue = startIndexValue,
                IsDataExhausted = selection.StartRecordIndex + 1 >= selection.Security.Count
            };
        }

        public SecurityBinding CreateSecurityBinding(ItemDefinition item, int appearedWeek)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            if (string.IsNullOrWhiteSpace(item.ItemId))
                throw new ArgumentException("An item ID is required.", "item");
            if (appearedWeek < 0)
                throw new ArgumentOutOfRangeException("appearedWeek");
            if (double.IsNaN(item.ReferencePrice) || double.IsInfinity(item.ReferencePrice)
                || item.ReferencePrice < 1.0)
            {
                throw new ArgumentOutOfRangeException("item.ReferencePrice");
            }

            SelectionResult selection = Select(
                _repository.Catalog.Securities,
                "item-security",
                item.ItemId,
                SecurityReserveWeeks,
                false);
            return new SecurityBinding
            {
                ItemId = item.ItemId,
                SecurityId = selection.Entry.SecurityId,
                Code = selection.Security.Header.Code,
                DataPath = selection.Entry.Path,
                StartRecordIndex = selection.StartRecordIndex,
                NextRecordIndex = selection.StartRecordIndex + 1,
                AppearedWeek = appearedWeek,
                LastPrice = item.ReferencePrice,
                CanDelist = selection.CanDelist,
                IsDataExhausted = selection.StartRecordIndex + 1 >= selection.Security.Count,
                IsDelisted = false,
                DelistedWeek = -1
            };
        }

        private SelectionResult Select(
            IReadOnlyList<SelectionEntry> source,
            string purpose,
            string stableKey,
            int normalReserveWeeks,
            bool isIndex)
        {
            return Select(source, purpose, stableKey, normalReserveWeeks, isIndex, null);
        }

        private SelectionResult Select(
            IReadOnlyList<SelectionEntry> source,
            string purpose,
            string stableKey,
            int normalReserveWeeks,
            bool isIndex,
            string previousSecurityId)
        {
            var candidates = new List<SelectionEntry>();
            for (int i = 0; i < source.Count; i++)
            {
                SelectionEntry entry = source[i];
                if (entry.Status == "success"
                    && !string.IsNullOrWhiteSpace(entry.Path)
                    && entry.ObservedRows > 0
                    && entry.SourceMissingRows == 0
                    && !string.Equals(entry.SecurityId, previousSecurityId, StringComparison.Ordinal))
                {
                    candidates.Add(entry);
                }
            }
            if (candidates.Count == 0)
                throw new InvalidOperationException("No eligible weekly securities are available.");
            candidates.Sort(delegate(SelectionEntry left, SelectionEntry right)
            {
                return string.CompareOrdinal(left.SecurityId, right.SecurityId);
            });

            int first = _random.NextIndex(purpose + ".entry", stableKey, candidates.Count);
            for (int attempt = 0; attempt < candidates.Count; attempt++)
            {
                SelectionEntry entry = candidates[(first + attempt) % candidates.Count];
                WeeklySecurity security = _repository.GetSecurity(entry.SecurityId);
                if (!security.HasObserved || ContainsSourceMissing(security))
                    continue;

                bool canDelist = !isIndex && HasDelistingRecord(security);
                int reserve = canDelist ? 0 : normalReserveWeeks;
                int lastStart = security.Count - 1 - reserve;
                if (lastStart < 0)
                    continue;

                var validStarts = new List<int>();
                for (int i = 0; i <= lastStart; i++)
                {
                    if (security.GetRecord(i).Status == 0)
                        validStarts.Add(i);
                }
                if (validStarts.Count == 0)
                    continue;

                int start = validStarts[_random.NextIndex(
                    purpose + ".start",
                    stableKey + ":" + entry.SecurityId,
                    validStarts.Count)];
                return new SelectionResult(entry, security, start, canDelist);
            }

            throw new InvalidOperationException("No weekly security satisfies the observed-start and reserve requirements.");
        }

        private static bool ContainsSourceMissing(WeeklySecurity security)
        {
            for (int i = 0; i < security.Count; i++)
            {
                if (security.GetRecord(i).Status == 2)
                    return true;
            }
            return false;
        }

        private static bool HasDelistingRecord(WeeklySecurity security)
        {
            for (int i = 0; i < security.Count; i++)
            {
                if (WeeklySecurity.IsDelistingRecord(security.GetRecord(i)))
                    return true;
            }
            return false;
        }

        private sealed class SelectionResult
        {
            public SelectionEntry Entry { get; private set; }
            public WeeklySecurity Security { get; private set; }
            public int StartRecordIndex { get; private set; }
            public bool CanDelist { get; private set; }

            public SelectionResult(
                SelectionEntry entry,
                WeeklySecurity security,
                int startRecordIndex,
                bool canDelist)
            {
                Entry = entry;
                Security = security;
                StartRecordIndex = startRecordIndex;
                CanDelist = canDelist;
            }
        }
    }
}
