using System;
using System.Collections.Generic;
using XingShangMod.Domain;

namespace XingShangMod.Persistence
{
    public static class RunStateNormalizer
    {
        public static RunState Normalize(RunState state)
        {
            if (state == null)
                state = new RunState();
            if (state.SchemaVersion > RunState.CurrentSchemaVersion)
            {
                throw new NotSupportedException(
                    "Run state schema " + state.SchemaVersion + " is newer than this mod supports.");
            }

            state.SchemaVersion = RunState.CurrentSchemaVersion;
            state.RunId = Text(state.RunId);
            state.RunSeed = NonNegative(state.RunSeed);
            state.CurrentIndex = NonNegative(state.CurrentIndex, 1000.0);
            state.CurrentGameYear = state.CurrentGameYear < 1 ? 1 : state.CurrentGameYear;
            state.Clock = state.Clock ?? new MarketClockState();
            Normalize(state.Clock);
            state.IndexElasticity = state.IndexElasticity ?? new ElasticityState();
            Normalize(state.IndexElasticity);
            state.Debt = NonNegative(state.Debt);

            state.AnnualIndexBindings = Objects(state.AnnualIndexBindings);
            for (int i = 0; i < state.AnnualIndexBindings.Count; i++)
                Normalize(state.AnnualIndexBindings[i]);

            state.AppearedItemIds = Strings(state.AppearedItemIds);
            state.SecurityBindings = Objects(state.SecurityBindings);
            for (int i = 0; i < state.SecurityBindings.Count; i++)
                Normalize(state.SecurityBindings[i]);

            state.IndexHistory = Objects(state.IndexHistory);
            for (int i = 0; i < state.IndexHistory.Count; i++)
                Normalize(state.IndexHistory[i]);

            state.PriceHistory = Objects(state.PriceHistory);
            for (int i = 0; i < state.PriceHistory.Count; i++)
                Normalize(state.PriceHistory[i]);

            state.ElasticityStates = Objects(state.ElasticityStates);
            for (int i = 0; i < state.ElasticityStates.Count; i++)
                Normalize(state.ElasticityStates[i]);

            state.PageUnlocks = Objects(state.PageUnlocks);
            for (int i = 0; i < state.PageUnlocks.Count; i++)
                Normalize(state.PageUnlocks[i]);

            state.Holdings = Objects(state.Holdings);
            for (int i = 0; i < state.Holdings.Count; i++)
                Normalize(state.Holdings[i]);

            state.ShortBatches = Objects(state.ShortBatches);
            for (int i = 0; i < state.ShortBatches.Count; i++)
                Normalize(state.ShortBatches[i]);

            state.TradeHistory = Objects(state.TradeHistory);
            for (int i = 0; i < state.TradeHistory.Count; i++)
                Normalize(state.TradeHistory[i]);

            state.AssetHistory = Objects(state.AssetHistory);
            for (int i = 0; i < state.AssetHistory.Count; i++)
                Normalize(state.AssetHistory[i]);

            state.NewsHistory = Objects(state.NewsHistory);
            for (int i = 0; i < state.NewsHistory.Count; i++)
                Normalize(state.NewsHistory[i]);

            return state;
        }

        private static void Normalize(MarketClockState state)
        {
            state.Carry = NonNegative(state.Carry);
            state.MarketWeekIndex = NonNegative(state.MarketWeekIndex);
            state.LastGameRound = NonNegative(state.LastGameRound);
            state.CurrentGameMonth = NonNegative(state.CurrentGameMonth);
            state.WeeksThisGameMonth = NonNegative(state.WeeksThisGameMonth);
        }

        private static void Normalize(AnnualIndexBinding state)
        {
            state.Year = NonNegative(state.Year);
            state.SecurityId = Text(state.SecurityId);
            state.Code = Text(state.Code);
            state.DataPath = Text(state.DataPath);
            state.StartRecordIndex = NonNegative(state.StartRecordIndex);
            state.NextRecordIndex = NonNegative(state.NextRecordIndex);
            state.StartIndexValue = NonNegative(state.StartIndexValue, 0.0);
        }

        private static void Normalize(SecurityBinding state)
        {
            state.ItemId = Text(state.ItemId);
            state.SecurityId = Text(state.SecurityId);
            state.Code = Text(state.Code);
            state.DataPath = Text(state.DataPath);
            state.StartRecordIndex = NonNegative(state.StartRecordIndex);
            state.NextRecordIndex = NonNegative(state.NextRecordIndex);
            state.AppearedWeek = NonNegative(state.AppearedWeek);
            state.LastPrice = NonNegative(state.LastPrice, 0.0);
            state.DelistedWeek = AtLeastMinusOne(state.DelistedWeek);
        }

        private static void Normalize(WeeklyPoint state)
        {
            state.ItemId = Text(state.ItemId);
            state.MarketWeekIndex = NonNegative(state.MarketWeekIndex);
            state.GameYear = NonNegative(state.GameYear);
            state.GameMonth = NonNegative(state.GameMonth);
            state.GameRound = NonNegative(state.GameRound);
            state.SourceRecordIndex = NonNegative(state.SourceRecordIndex);
            state.Price = NonNegative(state.Price, 0.0);
            state.BaseReturn = Finite(state.BaseReturn);
            state.Return = Finite(state.Return);
        }

        private static void Normalize(ElasticityState state)
        {
            state.ItemId = Text(state.ItemId);
            state.QuietCount = NonNegative(state.QuietCount);
            state.ActiveWeekCount = NonNegative(state.ActiveWeekCount);
            state.Multiplier = NonNegative(state.Multiplier, 1.0);
        }

        private static void Normalize(PageUnlockState state)
        {
            state.SourceId = Text(state.SourceId);
            state.PageIndex = NonNegative(state.PageIndex);
            state.GrantedWeek = AtLeastMinusOne(state.GrantedWeek);
            state.ItemIds = Strings(state.ItemIds);
        }

        private static void Normalize(Holding state)
        {
            state.ItemId = Text(state.ItemId);
            state.Quantity = NonNegative(state.Quantity);
            state.TotalCost = NonNegative(state.TotalCost);
        }

        private static void Normalize(ShortBatch state)
        {
            state.BatchId = Text(state.BatchId);
            state.ItemId = Text(state.ItemId);
            state.Quantity = NonNegative(state.Quantity);
            state.OpenWeek = NonNegative(state.OpenWeek);
            state.Proceeds = NonNegative(state.Proceeds);
            state.DueWeek = NonNegative(state.DueWeek);
        }

        private static void Normalize(TradeRecord state)
        {
            state.TradeId = Text(state.TradeId);
            state.MarketWeekIndex = NonNegative(state.MarketWeekIndex);
            state.GameYear = NonNegative(state.GameYear);
            state.GameMonth = NonNegative(state.GameMonth);
            state.GameRound = NonNegative(state.GameRound);
            state.ItemId = Text(state.ItemId);
            state.Action = Text(state.Action);
            state.BatchId = Text(state.BatchId);
            state.Quantity = NonNegative(state.Quantity);
            state.UnitPrice = NonNegative(state.UnitPrice, 0.0);
        }

        private static void Normalize(AssetPoint state)
        {
            state.MarketWeekIndex = NonNegative(state.MarketWeekIndex);
            state.GameYear = NonNegative(state.GameYear);
            state.GameMonth = NonNegative(state.GameMonth);
            state.GameRound = NonNegative(state.GameRound);
            state.Cash = NonNegative(state.Cash);
            state.LongValue = NonNegative(state.LongValue, 0.0);
            state.ShortLiability = NonNegative(state.ShortLiability, 0.0);
            state.Debt = NonNegative(state.Debt);
            state.NetAssets = Finite(state.NetAssets);
        }

        private static void Normalize(NewsRecord state)
        {
            state.NewsId = Text(state.NewsId);
            state.ItemId = Text(state.ItemId);
            state.MarketWeekIndex = NonNegative(state.MarketWeekIndex);
            state.GameYear = NonNegative(state.GameYear);
            state.GameMonth = NonNegative(state.GameMonth);
            state.GameRound = NonNegative(state.GameRound);
            state.Return = Finite(state.Return);
            state.PoolId = Text(state.PoolId);
            state.SelectionIndex = NonNegative(state.SelectionIndex);
            state.Text = Text(state.Text);
        }

        private static List<T> Objects<T>(List<T> values) where T : class
        {
            if (values == null)
                return new List<T>();
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (values[i] == null)
                    values.RemoveAt(i);
            }
            return values;
        }

        private static List<string> Strings(List<string> values)
        {
            if (values == null)
                return new List<string>();
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (values[i] == null)
                    values.RemoveAt(i);
            }
            return values;
        }

        private static string Text(string value)
        {
            return value ?? string.Empty;
        }

        private static int NonNegative(int value)
        {
            return value < 0 ? 0 : value;
        }

        private static long NonNegative(long value)
        {
            return value < 0 ? 0L : value;
        }

        private static int AtLeastMinusOne(int value)
        {
            return value < -1 ? -1 : value;
        }

        private static double Finite(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value;
        }

        private static double NonNegative(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
                return fallback;
            return value;
        }
    }
}
