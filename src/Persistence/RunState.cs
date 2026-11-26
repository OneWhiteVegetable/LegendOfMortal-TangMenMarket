using System;
using System.Collections.Generic;
using XingShangMod.Domain;

namespace XingShangMod.Persistence
{
    [Serializable]
    public sealed class RunState
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string RunId = string.Empty;
        public int RunSeed;
        public MarketClockState Clock = new MarketClockState();
        public double CurrentIndex = 1000.0;
        public int CurrentGameYear = 1;
        public List<AnnualIndexBinding> AnnualIndexBindings = new List<AnnualIndexBinding>();
        public List<string> AppearedItemIds = new List<string>();
        public List<SecurityBinding> SecurityBindings = new List<SecurityBinding>();
        public List<WeeklyPoint> IndexHistory = new List<WeeklyPoint>();
        public List<WeeklyPoint> PriceHistory = new List<WeeklyPoint>();
        public ElasticityState IndexElasticity = new ElasticityState();
        public List<ElasticityState> ElasticityStates = new List<ElasticityState>();
        public List<PageUnlockState> PageUnlocks = new List<PageUnlockState>();
        public List<Holding> Holdings = new List<Holding>();
        public List<ShortBatch> ShortBatches = new List<ShortBatch>();
        public List<TradeRecord> TradeHistory = new List<TradeRecord>();
        public List<AssetPoint> AssetHistory = new List<AssetPoint>();
        public List<NewsRecord> NewsHistory = new List<NewsRecord>();
        public long Debt;
        public bool Item09Unlocked;

        public bool HasItemAppeared(string itemId)
        {
            if (AppearedItemIds == null)
                return false;

            for (int i = 0; i < AppearedItemIds.Count; i++)
            {
                if (string.Equals(AppearedItemIds[i], itemId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal bool IsItemVisibleAt(string itemId, int marketWeek)
        {
            if (!HasItemAppeared(itemId))
                return false;
            SecurityBinding binding = FindSecurityBinding(itemId);
            return binding == null || binding.AppearedWeek <= marketWeek;
        }

        public SecurityBinding FindSecurityBinding(string itemId)
        {
            if (SecurityBindings == null)
                return null;

            for (int i = 0; i < SecurityBindings.Count; i++)
            {
                SecurityBinding binding = SecurityBindings[i];
                if (binding != null && string.Equals(binding.ItemId, itemId, StringComparison.Ordinal))
                    return binding;
            }

            return null;
        }

        public AnnualIndexBinding FindAnnualIndexBinding(int year)
        {
            if (AnnualIndexBindings == null)
                return null;

            for (int i = 0; i < AnnualIndexBindings.Count; i++)
            {
                AnnualIndexBinding binding = AnnualIndexBindings[i];
                if (binding != null && binding.Year == year)
                    return binding;
            }

            return null;
        }

        public ElasticityState FindElasticityState(string itemId)
        {
            if (ElasticityStates == null)
                return null;

            for (int i = 0; i < ElasticityStates.Count; i++)
            {
                ElasticityState state = ElasticityStates[i];
                if (state != null && string.Equals(state.ItemId, itemId, StringComparison.Ordinal))
                    return state;
            }

            return null;
        }

        public Holding FindHolding(string itemId)
        {
            if (Holdings == null)
                return null;

            for (int i = 0; i < Holdings.Count; i++)
            {
                Holding holding = Holdings[i];
                if (holding != null && string.Equals(holding.ItemId, itemId, StringComparison.Ordinal))
                    return holding;
            }

            return null;
        }

        public PageUnlockState FindPageUnlock(string sourceId)
        {
            if (PageUnlocks == null)
                return null;

            for (int i = 0; i < PageUnlocks.Count; i++)
            {
                PageUnlockState unlock = PageUnlocks[i];
                if (unlock != null && string.Equals(unlock.SourceId, sourceId, StringComparison.Ordinal))
                    return unlock;
            }

            return null;
        }

        public WeeklyPoint FindWeeklyPoint(string itemId, int marketWeekIndex)
        {
            if (PriceHistory == null)
                return null;

            for (int i = 0; i < PriceHistory.Count; i++)
            {
                WeeklyPoint point = PriceHistory[i];
                if (point != null && point.MarketWeekIndex == marketWeekIndex &&
                    string.Equals(point.ItemId, itemId, StringComparison.Ordinal))
                    return point;
            }

            return null;
        }

        public List<WeeklyPoint> GetPriceHistory(string itemId)
        {
            List<WeeklyPoint> result = new List<WeeklyPoint>();
            if (PriceHistory == null)
                return result;

            for (int i = 0; i < PriceHistory.Count; i++)
            {
                WeeklyPoint point = PriceHistory[i];
                if (point != null && string.Equals(point.ItemId, itemId, StringComparison.Ordinal))
                    result.Add(point);
            }

            return result;
        }

        public List<ShortBatch> GetShortBatches(string itemId)
        {
            List<ShortBatch> result = new List<ShortBatch>();
            if (ShortBatches == null)
                return result;

            for (int i = 0; i < ShortBatches.Count; i++)
            {
                ShortBatch batch = ShortBatches[i];
                if (batch != null && string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    result.Add(batch);
            }

            return result;
        }
    }
}
