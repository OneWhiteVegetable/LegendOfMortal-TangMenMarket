using System;
using System.Collections.Generic;
using System.IO;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using static AuditAssert;

internal sealed class RealRuntimePriceProgressionTests
{
    private const int ObservedRunSeed = 1282861803;
    private readonly string _runtimeRoot;

    public RealRuntimePriceProgressionTests()
        : this(FindRuntimeRoot())
    {
    }

    public RealRuntimePriceProgressionTests(string runtimeRoot)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
            throw new ArgumentException("A Runtime root is required.", "runtimeRoot");
        _runtimeRoot = Path.GetFullPath(runtimeRoot);
        if (!File.Exists(Path.Combine(_runtimeRoot, "selection_catalog.json")))
            throw new FileNotFoundException("The real Runtime selection catalog is missing.", _runtimeRoot);
    }

    public void Register(TestRunner runner)
    {
        runner.Run("real-runtime-selector-binding-cursors", TestSelectorBindingCursors);
        runner.Run("real-runtime-partial-week-is-not-stalled", TestPartialWeekIsNotStalled);
        runner.Run("real-runtime-four-nonzero-weeks", TestFourNonZeroWeeks);
        runner.Run("real-runtime-low-action-month-completion", TestLowActionMonthCompletion);
        runner.Run("real-runtime-no-trade-remains-flat", TestNoTradeRemainsFlat);
        runner.Run("real-runtime-save-roundtrip-continues-weeks", TestSaveRoundtripContinuesWeeks);
    }

    private void TestSelectorBindingCursors()
    {
        WeeklyDataRepository repository = CreateRepository();
        MarketSelector selector = new MarketSelector(repository, ObservedRunSeed);
        AnnualIndexBinding index = selector.CreateAnnualIndexBinding(1, 1000.0);
        Equal("399001.SZ", index.SecurityId, "observed annual index selection");
        Equal(0, index.StartRecordIndex, "annual start cursor");
        Equal(1, index.NextRecordIndex, "annual next cursor");
        Near(1000.0, index.StartIndexValue, "annual start value");

        SecurityBinding item = selector.CreateSecurityBinding(CreateItem05(), 0);
        Equal("SH.601208", item.SecurityId, "observed item security selection");
        Equal(54, item.StartRecordIndex, "item start cursor");
        Equal(55, item.NextRecordIndex, "item next cursor");
        Near(3500.0, item.LastPrice, "item initial last price");
        Equal(0, item.AppearedWeek, "item appearance week");

        WeeklySecurity indexSecurity = repository.GetSecurity(index.SecurityId);
        WeeklySecurity itemSecurity = repository.GetSecurity(item.SecurityId);
        Equal(0, indexSecurity.GetRecord(1).Status, "first annual advance must be observed");
        Equal(0, itemSecurity.GetRecord(55).Status, "first item advance must be observed");
        True(indexSecurity.ComputeHfqClose(1) != indexSecurity.ComputeHfqClose(0),
            "annual fixture has no first-week movement");
        True(itemSecurity.ComputeHfqClose(55) != itemSecurity.ComputeHfqClose(54),
            "item fixture has no first-week movement");
    }

    private void TestPartialWeekIsNotStalled()
    {
        ProgressionContext context = CreateProgressionContext();
        AdvanceActionPoints(context, new MarketTime(1, 4, 1), 2);
        Equal(0, context.State.Clock.MarketWeekIndex, "two action points advanced a week");
        Equal(2, context.State.Clock.Carry, "two action points were not accumulated");
        Equal(1, context.State.IndexHistory.Count, "partial week added an index point");
        Equal(1, context.State.PriceHistory.Count, "partial week added an item point");
        Near(1000.0, context.State.CurrentIndex, "partial week moved the index");
        Near(3500.0, context.State.FindSecurityBinding("05").LastPrice,
            "partial week moved the item");

        AdvanceActionPoints(context, new MarketTime(1, 4, 1), 1);
        Equal(1, context.State.Clock.MarketWeekIndex, "third action point did not advance a week");
        Equal(2, context.State.IndexHistory.Count, "third action point did not settle the index");
        Equal(2, context.State.PriceHistory.Count, "third action point did not settle the item");
        True(context.State.CurrentIndex != 1000.0, "third action point left the index at baseline");
        True(context.State.FindSecurityBinding("05").LastPrice != 3500.0,
            "third action point left the item at baseline");
    }

    private void TestFourNonZeroWeeks()
    {
        ProgressionContext context = CreateProgressionContext();
        AdvanceThreeHighActionRounds(context);

        Equal(4, context.State.Clock.MarketWeekIndex, "12 action points must advance four weeks");
        Equal(0, context.State.Clock.Carry, "12 action points must not leave a carry");
        Equal(4, context.State.Clock.WeeksThisGameMonth, "completed month week count");
        AssertFourNonZeroWeeks(context, 6, 54);

        int nextMonthChanges = Synchronize(context, new MarketTime(1, 5, 1));
        Equal(0, nextMonthChanges, "an aligned four-week month must not add a fifth week");
        Equal(4, context.State.Clock.MarketWeekIndex, "month synchronization added a week");
        Equal(5, context.State.IndexHistory.Count, "month synchronization rewrote index history");
        Equal(5, context.State.PriceHistory.Count, "month synchronization rewrote item history");
    }

    private void TestLowActionMonthCompletion()
    {
        ProgressionContext context = CreateProgressionContext();
        AdvanceActionPoints(context, new MarketTime(1, 4, 1), 3);
        Equal(0, Synchronize(context, new MarketTime(1, 4, 2)), "low-action round one added a week");
        AdvanceActionPoints(context, new MarketTime(1, 4, 2), 3);
        Equal(0, Synchronize(context, new MarketTime(1, 4, 3)), "low-action round two added a week");
        AdvanceActionPoints(context, new MarketTime(1, 4, 3), 3);
        Equal(3, context.State.Clock.MarketWeekIndex, "three low-action rounds did not advance three weeks");
        Equal(1, Synchronize(context, new MarketTime(1, 5, 1)),
            "low-action month crossing did not complete exactly one week");

        Equal(4, context.State.Clock.MarketWeekIndex, "low-action month did not reach four weeks");
        Equal(0, context.State.Clock.Carry, "month completion leaked an action carry");
        Equal(0, context.State.Clock.WeeksThisGameMonth, "new month retained the old week count");
        AssertFourNonZeroWeeks(context, 6, 54);
        Equal(0, Synchronize(context, new MarketTime(1, 5, 1)), "repeated month callback added a week");
    }

    private void TestNoTradeRemainsFlat()
    {
        WeeklyDataRepository repository = CreateRepository();
        RunState state = CreateEmptyState();
        state.AnnualIndexBindings.Add(new AnnualIndexBinding
        {
            Year = 1,
            SecurityId = "399001.SZ",
            Code = "399001.SZ",
            DataPath = "indices/a_share/399001_SZ.wkg",
            StartRecordIndex = 3,
            NextRecordIndex = 4,
            StartIndexValue = 1000.0
        });
        state.AppearedItemIds.Add("39");
        state.SecurityBindings.Add(new SecurityBinding
        {
            ItemId = "39",
            SecurityId = "SZ.301088",
            Code = "SZ.301088",
            DataPath = "stocks/a_share_active/SZ/SZ_301088.wkg",
            StartRecordIndex = 64,
            NextRecordIndex = 65,
            AppearedWeek = 0,
            LastPrice = 1840.0,
            DelistedWeek = -1
        });
        state.PriceHistory.Add(new WeeklyPoint
        {
            ItemId = "39",
            MarketWeekIndex = 0,
            GameYear = 1,
            GameMonth = 4,
            GameRound = 1,
            SourceRecordIndex = 64,
            Price = 1840.0,
            Observed = true
        });

        WeeklySecurity indexSecurity = repository.GetSecurity("399001.SZ");
        WeeklySecurity itemSecurity = repository.GetSecurity("SZ.301088");
        Equal(1, indexSecurity.GetRecord(4).Status, "annual NoTrade fixture changed");
        Equal(1, itemSecurity.GetRecord(65).Status, "item NoTrade fixture changed");

        var item = new ItemDefinition { ItemId = "39", ReferencePrice = 1840.0, Weight = 1.0 };
        MarketEngine engine = new MarketEngine(state, repository, new[] { item });
        MarketClock clock = new MarketClock(state.Clock);
        Equal(1, clock.ApplyActionCost(3, new MarketTime(1, 4, 1)), "NoTrade test week was not reached");
        IList<MarketWeekChange> changes = engine.AdvanceWeeks(clock.LastAdvancement);
        Equal(1, changes.Count, "NoTrade week was not settled");

        WeeklyPoint indexPoint = state.IndexHistory[1];
        WeeklyPoint itemPoint = state.PriceHistory[1];
        Near(1000.0, state.CurrentIndex, "NoTrade moved the index");
        Near(1840.0, state.FindSecurityBinding("39").LastPrice, "NoTrade moved the item");
        Near(1000.0, indexPoint.Price, "NoTrade index point price");
        Near(1840.0, itemPoint.Price, "NoTrade item point price");
        Near(0.0, indexPoint.Return, "NoTrade index return");
        Near(0.0, itemPoint.Return, "NoTrade item return");
        True(!indexPoint.Observed && !itemPoint.Observed, "NoTrade was rewritten as Observed");
        Equal(4, indexPoint.SourceRecordIndex, "NoTrade index source cursor");
        Equal(65, itemPoint.SourceRecordIndex, "NoTrade item source cursor");
        Equal(5, state.AnnualIndexBindings[0].NextRecordIndex, "NoTrade annual cursor did not advance");
        Equal(66, state.FindSecurityBinding("39").NextRecordIndex, "NoTrade item cursor did not advance");
        True(!state.FindSecurityBinding("39").IsDelisted, "NoTrade was treated as delisting");
        Equal(1, indexSecurity.GetRecord(4).Status, "annual NoTrade record was mutated");
        Equal(1, itemSecurity.GetRecord(65).Status, "item NoTrade record was mutated");
    }

    private void TestSaveRoundtripContinuesWeeks()
    {
        ProgressionContext initial = CreateProgressionContext();
        AdvanceHighActionRound(initial, new MarketTime(1, 4, 1));
        Synchronize(initial, new MarketTime(1, 4, 2));
        AdvanceHighActionRound(initial, new MarketTime(1, 4, 2));
        Equal(2, initial.State.Clock.MarketWeekIndex, "save fixture did not reach week two");

        RunState restored = RunStateCloner.Clone(initial.State);
        Equal(2, restored.Clock.MarketWeekIndex, "save lost the market week");
        Equal(2, restored.Clock.Carry, "save lost the action carry");
        Equal(9, restored.AnnualIndexBindings[0].NextRecordIndex, "save lost the annual cursor");
        Equal(57, restored.FindSecurityBinding("05").NextRecordIndex, "save lost the item cursor");
        Near(initial.State.FindSecurityBinding("05").LastPrice,
            restored.FindSecurityBinding("05").LastPrice, "save changed LastPrice");

        var context = new ProgressionContext
        {
            State = restored,
            Repository = initial.Repository,
            Engine = new MarketEngine(restored, initial.Repository, new[] { CreateItem05() })
        };
        context.Clock = new MarketClock(restored.Clock);
        Synchronize(context, new MarketTime(1, 4, 3));
        AdvanceHighActionRound(context, new MarketTime(1, 4, 3));

        Equal(4, restored.Clock.MarketWeekIndex, "restored clock did not reach week four");
        AssertFourNonZeroWeeks(context, 6, 54);
        Equal(11, restored.AnnualIndexBindings[0].NextRecordIndex, "restored annual cursor is wrong");
        Equal(59, restored.FindSecurityBinding("05").NextRecordIndex, "restored item cursor is wrong");
    }

    private ProgressionContext CreateProgressionContext()
    {
        RunState state = CreateEmptyState();
        state.AnnualIndexBindings.Add(new AnnualIndexBinding
        {
            Year = 1,
            SecurityId = "000001.SH",
            Code = "000001.SH",
            DataPath = "indices/a_share/000001_SH.wkg",
            StartRecordIndex = 6,
            NextRecordIndex = 7,
            StartIndexValue = 1000.0
        });
        state.AppearedItemIds.Add("05");
        WeeklyDataRepository repository = CreateRepository();
        MarketEngine engine = new MarketEngine(state, repository, new[] { CreateItem05() });
        SecurityBinding item = state.FindSecurityBinding("05");
        Equal("SH.601208", item.SecurityId, "progression item binding changed");
        Equal(54, item.StartRecordIndex, "progression item start changed");
        Equal(55, item.NextRecordIndex, "progression item next cursor");
        Near(3500.0, item.LastPrice, "progression item initial price");
        return new ProgressionContext
        {
            State = state,
            Repository = repository,
            Engine = engine,
            Clock = new MarketClock(state.Clock)
        };
    }

    private static RunState CreateEmptyState()
    {
        var state = new RunState
        {
            RunId = "real-runtime-price-progression",
            RunSeed = ObservedRunSeed,
            CurrentIndex = 1000.0,
            CurrentGameYear = 1
        };
        new MarketClock(state.Clock).Initialize(new MarketTime(1, 4, 1));
        return state;
    }

    private WeeklyDataRepository CreateRepository()
    {
        return new WeeklyDataRepository(Path.Combine(_runtimeRoot, "data"));
    }

    private static ItemDefinition CreateItem05()
    {
        return new ItemDefinition { ItemId = "05", ReferencePrice = 3500.0, Weight = 1.0 };
    }

    private static void AdvanceThreeHighActionRounds(ProgressionContext context)
    {
        AdvanceHighActionRound(context, new MarketTime(1, 4, 1));
        Synchronize(context, new MarketTime(1, 4, 2));
        AdvanceHighActionRound(context, new MarketTime(1, 4, 2));
        Synchronize(context, new MarketTime(1, 4, 3));
        AdvanceHighActionRound(context, new MarketTime(1, 4, 3));
    }

    private static void AdvanceHighActionRound(ProgressionContext context, MarketTime time)
    {
        AdvanceActionPoints(context, time, 4);
    }

    private static void AdvanceActionPoints(ProgressionContext context, MarketTime time, int points)
    {
        for (int point = 0; point < points; point++)
        {
            int clockChanges = context.Clock.ApplyActionCost(1, time);
            IList<MarketWeekChange> changes = context.Engine.AdvanceWeeks(context.Clock.LastAdvancement);
            Equal(clockChanges, changes.Count, "clock and engine weekly settlement count");
        }
    }

    private static int Synchronize(ProgressionContext context, MarketTime time)
    {
        int clockChanges = context.Clock.SynchronizeNaturalTime(time);
        IList<MarketWeekChange> changes = context.Engine.AdvanceWeeks(context.Clock.LastAdvancement);
        Equal(clockChanges, changes.Count, "natural-time clock and engine settlement count");
        return changes.Count;
    }

    private static void AssertFourNonZeroWeeks(
        ProgressionContext context,
        int indexStart,
        int itemStart)
    {
        Equal(5, context.State.IndexHistory.Count, "four-week index history length");
        Equal(5, context.State.PriceHistory.Count, "four-week item history length");
        AnnualIndexBinding index = context.State.AnnualIndexBindings[0];
        SecurityBinding item = context.State.FindSecurityBinding("05");
        WeeklySecurity indexSecurity = context.Repository.GetSecurity(index.SecurityId);
        WeeklySecurity itemSecurity = context.Repository.GetSecurity(item.SecurityId);
        Equal(indexStart + 5, index.NextRecordIndex, "four-week annual next cursor");
        Equal(itemStart + 5, item.NextRecordIndex, "four-week item next cursor");

        for (int week = 1; week <= 4; week++)
        {
            WeeklyPoint previousIndex = context.State.IndexHistory[week - 1];
            WeeklyPoint currentIndex = context.State.IndexHistory[week];
            WeeklyPoint previousItem = context.State.PriceHistory[week - 1];
            WeeklyPoint currentItem = context.State.PriceHistory[week];
            Equal(week, currentIndex.MarketWeekIndex, "index market week sequence");
            Equal(week, currentItem.MarketWeekIndex, "item market week sequence");
            Equal(indexStart + week, currentIndex.SourceRecordIndex, "index source record sequence");
            Equal(itemStart + week, currentItem.SourceRecordIndex, "item source record sequence");
            True(currentIndex.Observed && currentItem.Observed, "nonzero fixture contains an empty observation");
            True(Math.Abs(currentIndex.Return) > 0.0, "index return is zero in week " + week);
            True(Math.Abs(currentItem.Return) > 0.0, "item return is zero in week " + week);
            True(currentIndex.Price != previousIndex.Price, "index price is flat in week " + week);
            True(currentItem.Price != previousItem.Price, "item price is flat in week " + week);
            double indexBaseReturn = indexSecurity.ComputeHfqClose(indexStart + week) /
                indexSecurity.ComputeHfqClose(indexStart + week - 1) - 1.0;
            double itemBaseReturn = itemSecurity.ComputeHfqClose(itemStart + week) /
                itemSecurity.ComputeHfqClose(itemStart + week - 1) - 1.0;
            Near(indexBaseReturn, currentIndex.BaseReturn, "index source return in week " + week);
            Near(PriceSeries.ClampReturn(itemBaseReturn + currentIndex.Return),
                currentItem.BaseReturn, "item plus index return in week " + week);
#if PRICE_PROGRESSION_STANDALONE
            Console.WriteLine("REAL week " + week + ": index=" + currentIndex.Price +
                ", item=" + currentItem.Price + ", indexReturn=" + currentIndex.Return +
                ", itemReturn=" + currentItem.Return);
#endif
        }
        Near(context.State.IndexHistory[4].Price, context.State.CurrentIndex, "current index disagrees with history");
        Near(context.State.PriceHistory[4].Price, context.Engine.GetCurrentPrice("05"), "current price disagrees with history");
    }

#if PRICE_PROGRESSION_STANDALONE
    public static int Main()
    {
        var runner = new TestRunner();
        new RealRuntimePriceProgressionTests().Register(runner);
        return runner.Complete();
    }
#endif

    internal static string FindRuntimeRoot()
    {
        string configured = Environment.GetEnvironmentVariable("XINGSHANG_RUNTIME_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) &&
            File.Exists(Path.Combine(configured, "selection_catalog.json")))
        {
            return configured;
        }

        string[] starts = { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        for (int i = 0; i < starts.Length; i++)
        {
            var directory = new DirectoryInfo(starts[i]);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "XingShangMod.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "Runtime", "selection_catalog.json")))
                {
                    return Path.Combine(directory.FullName, "Runtime");
                }
                directory = directory.Parent;
            }
        }
        throw new FileNotFoundException(
            "Cannot locate the real Runtime directory; set XINGSHANG_RUNTIME_ROOT to its absolute path.");
    }

    private sealed class ProgressionContext
    {
        public RunState State;
        public WeeklyDataRepository Repository;
        public MarketEngine Engine;
        public MarketClock Clock;
    }
}
