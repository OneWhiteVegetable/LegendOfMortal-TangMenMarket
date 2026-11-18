using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Progression
{
    public sealed class RosterService
    {
        public const int ItemsPerPage = 6;
        public const string InitialSourceId = "initial";
        public const string HomeLevel2SourceId = "home-level-2";
        public const string HomeLevel3SourceId = "home-level-3";

        private static readonly IReadOnlyList<string> SupportedSources =
            new ReadOnlyCollection<string>(new[]
            {
                HomeLevel2SourceId,
                HomeLevel3SourceId,
                "Facility_001",
                "Facility_002",
                "Facility_003",
                "Facility_004",
                "Facility_005"
            });

        private readonly ItemCatalog _catalog;

        public RosterService(ItemCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException("catalog");
            _catalog = catalog;
        }

        public static IReadOnlyList<string> SupportedUnlockSources
        {
            get { return SupportedSources; }
        }

        public PageUnlockState Initialize(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            return Initialize(state, GetCurrentWeek(state));
        }

        public PageUnlockState Initialize(RunState state, int grantedWeek)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            EnsureStateCollections(state);

            PageUnlockState existing = state.FindPageUnlock(InitialSourceId);
            if (existing != null)
                return existing;
            return AddPage(state, InitialSourceId, grantedWeek, true);
        }

        public PageUnlockState Unlock(RunState state, string sourceId)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            return Unlock(state, sourceId, GetCurrentWeek(state));
        }

        public PageUnlockState Unlock(RunState state, string sourceId, int grantedWeek)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("An unlock source ID is required.", "sourceId");
            if (!IsSupportedUnlockSource(sourceId))
                throw new ArgumentException("Unsupported roster unlock source: " + sourceId, "sourceId");

            EnsureStateCollections(state);
            if (state.FindPageUnlock(sourceId) != null)
                return null;
            if (state.FindPageUnlock(InitialSourceId) == null)
                AddPage(state, InitialSourceId, grantedWeek, true);
            return AddPage(state, sourceId, grantedWeek, false);
        }

        public bool TryUnlock(
            RunState state,
            string sourceId,
            int grantedWeek,
            out PageUnlockState page)
        {
            page = Unlock(state, sourceId, grantedWeek);
            return page != null;
        }

        public static bool IsSupportedUnlockSource(string sourceId)
        {
            for (int i = 0; i < SupportedSources.Count; i++)
            {
                if (string.Equals(SupportedSources[i], sourceId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private PageUnlockState AddPage(RunState state, string sourceId, int grantedWeek, bool isInitial)
        {
            int pageIndex = GetNextPageIndex(state);
            var page = new PageUnlockState
            {
                SourceId = sourceId,
                PageIndex = pageIndex,
                IsUnlocked = true,
                GrantedWeek = grantedWeek,
                ItemIds = new List<string>()
            };

            var candidates = new List<ItemDefinition>();
            for (int i = 0; i < _catalog.Items.Count; i++)
            {
                ItemDefinition item = _catalog.Items[i];
                if (state.HasItemAppeared(item.ItemId))
                    continue;
                if (item.ItemId == ItemCatalog.StoryLockedItemId
                    && (isInitial || !state.Item09Unlocked))
                    continue;
                candidates.Add(item);
            }

            string pageKey = pageIndex.ToString(CultureInfo.InvariantCulture);
            int drawCount = Math.Min(ItemsPerPage, candidates.Count);
            for (int slot = 0; slot < drawCount; slot++)
            {
                int selectedIndex = RunRandom.NextIndex(
                    state.RunSeed,
                    candidates.Count,
                    "roster",
                    sourceId,
                    pageKey,
                    slot.ToString(CultureInfo.InvariantCulture));
                ItemDefinition selected = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex);
                page.ItemIds.Add(selected.ItemId);
                state.AppearedItemIds.Add(selected.ItemId);
            }

            state.PageUnlocks.Add(page);
            return page;
        }

        private static int GetNextPageIndex(RunState state)
        {
            int result = 0;
            for (int i = 0; i < state.PageUnlocks.Count; i++)
            {
                PageUnlockState page = state.PageUnlocks[i];
                if (page != null && page.PageIndex >= result)
                    result = page.PageIndex + 1;
            }
            return result;
        }

        private static int GetCurrentWeek(RunState state)
        {
            return state.Clock == null ? 0 : state.Clock.MarketWeekIndex;
        }

        private static void EnsureStateCollections(RunState state)
        {
            if (state.AppearedItemIds == null)
                state.AppearedItemIds = new List<string>();
            if (state.PageUnlocks == null)
                state.PageUnlocks = new List<PageUnlockState>();
        }
    }
}
