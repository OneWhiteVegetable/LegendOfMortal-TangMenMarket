using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using XingShangMod.UI.Shared;
using static AuditAssert;

internal sealed class RevealedHistoryTests
{
    private readonly WeeklyDataRepository _data;
    private readonly CoreTestFixture _fixture;
    private readonly ItemDefinition _item = new ItemDefinition { ItemId = "05", Name = "仿南宫尖肩甲", ReferencePrice = 3500, Weight = 1 };
    private readonly ItemDefinition _other = new ItemDefinition { ItemId = "39", Name = "坐名旗", ReferencePrice = 1840, Weight = 1 };

    internal RevealedHistoryTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
        _data = new WeeklyDataRepository(Path.Combine(RealRuntimePriceProgressionTests.FindRuntimeRoot(), "data"));
    }

    internal void Register(TestRunner runner)
    {
        runner.Run("late-listing-replays-real-run-index-from-week-zero-across-years", LateHistory);
        runner.Run("reveal-before-batched-settlement-backfills-without-early-notices", PendingWeeks);
        runner.Run("legacy-quoted-price-and-positions-stay-at-their-recorded-basis", OldQuote);
        runner.Run("delisting-compresses-353-no-trade-weeks-and-keeps-reopening-return", Resumption);
        runner.Run("delisting-marker-in-empty-terminal-week-is-never-skipped", TerminalMarker);
        runner.Run("newly-revealed-already-delisted-stock-is-frozen-immediately", DeadOnReveal);
    }

    private MarketEngine NewMarket()
    {
        var state = new RunState { RunId = "week-zero", RunSeed = 1282861803 };
        new MarketClock(state.Clock).Initialize(new MarketTime(1, 4, 1));
        return new MarketEngine(state, _data, new[] { _item, _other });
    }

    private static List<MarketWeekChange> Advance(MarketEngine engine, int count)
    {
        int first = engine.State.Clock.MarketWeekIndex + 1;
        var times = new List<MarketTime>();
        for (int i = first; i < first + count; i++)
        {
            int month = 4 + i / 4;
            times.Add(new MarketTime(1 + (month - 1) / 12, 1 + (month - 1) % 12, Math.Min(3, 1 + i % 4)));
        }
        engine.State.Clock.MarketWeekIndex += count;
        return engine.AdvanceWeeks(new MarketClockAdvancement(times, first, first + count - 1, false)).ToList();
    }

    private static string Prices(RunState state, string itemId)
    {
        return JsonCodec.Serialize(state.GetPriceHistory(itemId), false);
    }

    private void SameAsEarly(MarketEngine early, MarketEngine late)
    {
        string id = _item.ItemId;
        Equal(Prices(early.State, id), Prices(late.State, id), "all prices, returns, flags, source rows, dates");
        var a = early.State.FindSecurityBinding(id);
        var b = late.State.FindSecurityBinding(id);
        Equal(a.SecurityId, b.SecurityId, "same independent security");
        Equal(a.StartRecordIndex, b.StartRecordIndex, "same independent start");
        Equal(a.NextRecordIndex, b.NextRecordIndex, "same next-week cursor");
        Equal(a.LastPrice, b.LastPrice, "identical unrounded compound price");
        Equal(JsonCodec.Serialize(early.State.FindElasticityState(id), false),
            JsonCodec.Serialize(late.State.FindElasticityState(id), false), "elasticity path");
    }

    private void LateHistory()
    {
        MarketEngine early = NewMarket(), late = NewMarket();
        early.RegisterAppearedItem(_other);
        early.RegisterAppearedItem(_item);
        late.RegisterAppearedItem(_other);
        Advance(early, 43);
        Advance(late, 43);
        True(late.State.FindSecurityBinding(_item.ItemId) == null && late.State.GetPriceHistory(_item.ItemId).Count == 0,
            "a hidden good already had a public quote or a binding");
        string index = JsonCodec.Serialize(late.State.IndexHistory, false);
        string other = Prices(late.State, _other.ItemId);
        int emitted = 0;
        late.WeekAdvanced += ignored => emitted++;
        var future = new WeeklyPoint { MarketWeekIndex = 1000, Price = 9e8, Return = 32 };
        late.State.IndexHistory.Add(future);
        late.RegisterAppearedItem(_item);
        late.RegisterAppearedItem(_item);
        late.State.IndexHistory.Remove(future);
        Equal(43, late.State.FindSecurityBinding(_item.ItemId).AppearedWeek, "reveal date must not become week zero");
        Equal(44, late.State.GetPriceHistory(_item.ItemId).Count, "exactly weeks zero through 43");
        Equal(0, emitted, "backfill emitted a financial event");
        Equal(index, JsonCodec.Serialize(late.State.IndexHistory, false), "backfill mutated the realized index");
        Equal(other, Prices(late.State, _other.ItemId), "backfill rerolled another security");
        Equal("开局至今", MarketQuery.ItemTotalReturnCaption(late.State.GetPriceHistory(_item.ItemId)), "ranking baseline");
        SameAsEarly(early, late);
        var restored = new MarketEngine(RunStateCloner.Clone(late.State), _data, new[] { _item, _other });
        Advance(early, 1);
        Advance(restored, 1);
        SameAsEarly(early, restored);
        var p = restored.State.FindWeeklyPoint(_item.ItemId, 43);
        Console.WriteLine("  late fixture week 43: {0}, row {1}, price {2}, weekly {3:P3}; baseline 3500",
            restored.State.FindSecurityBinding(_item.ItemId).SecurityId, p.SourceRecordIndex, p.Price, p.Return);
    }

    private void PendingWeeks()
    {
        MarketEngine early = NewMarket(), late = NewMarket();
        early.RegisterAppearedItem(_item);
        Advance(early, 6);
        Advance(late, 2);
        late.State.Clock.MarketWeekIndex = 6;
        var notYetSettled = new WeeklyPoint { MarketWeekIndex = 5, Price = 999999, Return = 20 };
        late.State.IndexHistory.Add(notYetSettled);
        late.RegisterAppearedItem(_item);
        Equal(3, late.State.GetPriceHistory(_item.ItemId).Count, "must stop at last settled index, not clock target");
        late.State.IndexHistory.Remove(notYetSettled);
        var times = new List<MarketTime>();
        for (int i = 3; i <= 6; i++)
        {
            WeeklyPoint point = early.State.IndexHistory[i];
            times.Add(new MarketTime(point.GameYear, point.GameMonth, point.GameRound));
        }
        var changes = late.AdvanceWeeks(new MarketClockAdvancement(times, 3, 6, false));
        for (int i = 0; i < 3; i++)
            Equal(0, changes[i].Items.Count, "pre-listing week escaped as market news");
        Equal(1, changes[3].Items.Count, "listing-week new change should be eligible for news");
        SameAsEarly(early, late);
        late.State.Clock.MarketWeekIndex = 5;
        True(!late.State.IsItemVisibleAt(_item.ItemId, 5), "hidden until revealed even though backfilled prices exist");
        var trading = new TradingService(late.State, new FakeWallet(10000),
            new Dictionary<string, ItemDefinition> { { _item.ItemId, _item } }, new CapacityService(() => new CapacityStats(30, 10, 10)));
        var buy = new TradeOrder(TradeAction.Buy, _item.ItemId, 1);
        Equal(TradeError.PriceUnavailable, trading.PreviewCapacity(buy).Error, "pre-listing draft");
        Equal(TradeError.PriceUnavailable, trading.Execute(buy).Error, "pre-listing execution");
        late.State.Clock.MarketWeekIndex = 6;
        True(trading.Preview(buy).Success, "revealed good should now be tradable");
    }

    private void OldQuote()
    {
        var engine = NewMarket();
        engine.RegisterAppearedItem(_item);
        Advance(engine, 9);
        var state = engine.State;
        var binding = state.FindSecurityBinding(_item.ItemId);
        binding.AppearedWeek = 7;
        state.PriceHistory.RemoveAll(p => p.ItemId == _item.ItemId && p.MarketWeekIndex < 7);
        state.Holdings.Add(new Holding { ItemId = _item.ItemId, Quantity = 3, TotalCost = 789 });
        state.TradeHistory.Add(new TradeRecord { ItemId = _item.ItemId, TradeId = "old", Quantity = 3, CashDelta = -789 });
        string before = JsonCodec.Serialize(state, false);
        var restored = new MarketEngine(RunStateCloner.Clone(state), _data, new[] { _item });
        restored.RegisterAppearedItem(_item);
        Equal(before, JsonCodec.Serialize(restored.State, false), "existing published stock, position or history rewritten");
        Equal("市周 7 起", MarketQuery.ItemTotalReturnCaption(restored.State.GetPriceHistory(_item.ItemId)), "old basis mislabeled");

        var orphan = RunStateCloner.Clone(state);
        orphan.SecurityBindings.Clear();
        var recovered = new MarketEngine(orphan, _data, new[] { _item });
        Equal(Prices(state, _item.ItemId), Prices(recovered.State, _item.ItemId), "missing binding caused duplicate or replaced public quotes");
        Equal(binding.NextRecordIndex, recovered.State.FindSecurityBinding(_item.ItemId).NextRecordIndex, "recovered source cursor");
    }

    private void Resumption()
    {
        WeeklySecurity stock = _data.GetSecurity("SZ.000622");
        int cursor = 331;
        bool exhausted = false;
        var ordinary = PriceSeries.Advance(stock, ref cursor, ref exhausted);
        Equal(331, ordinary.SourceRecordIndex, "unchanged ordinary calendar rule");
        True(!ordinary.Observed, "ordinary empty calendar week");
        cursor = 331;
        PriceSeriesSample reopened = PriceSeries.Advance(stock, ref cursor, ref exhausted, true);
        Equal(684, reopened.SourceRecordIndex, "353 empty rows should cost a single player step");
        Equal(685, cursor, "next cursor");
        Near(3.7321428571428568, reopened.BaseReturn, "first resumption return against pre-suspension close");
        var engine = NewMarket();
        var binding = engine.RegisterAppearedItem(_item);
        binding.SecurityId = "SZ.000622";
        binding.CanDelist = true;
        binding.NextRecordIndex = 331;
        var change = Advance(engine, 1)[0];
        Equal(684, change.Items[0].SourceRecordIndex, "ordinary market flow must compress flagged security");
        Near(reopened.BaseReturn + change.Index.Return, change.Items[0].Return, "market spread after reopening");
        bool testedFlat = false;
        for (int i = 1; i < stock.Count; i++)
        {
            if (stock.GetRecord(i).Status != 0 || stock.GetRecord(i-1).Status != 0 ||
                stock.ComputeHfqClose(i) != stock.ComputeHfqClose(i-1)) continue;
            cursor = i;
            var flat = PriceSeries.Advance(stock, ref cursor, ref exhausted, true);
            True(flat.Observed && flat.SourceRecordIndex == i && flat.BaseReturn == 0, "real flat quote was swallowed");
            testedFlat = true;
            break;
        }
        True(testedFlat, "real observed-flat sample required");
    }

    private void TerminalMarker()
    {
        WeeklySecurity stock = _data.GetSecurity("SZ.000622");
        True(stock.GetRecord(stock.Count-1).Status == 1 && WeeklySecurity.IsDelistingRecord(stock.GetRecord(stock.Count-1)),
            "expected resuming security with a terminal non-trading marker");
        var engine = NewMarket();
        var bind = engine.RegisterAppearedItem(_item);
        bind.CanDelist = true;
        bind.SecurityId = "SZ.000622";
        bind.NextRecordIndex = stock.LastObservedIndex + 1;
        double priceBefore = bind.LastPrice;
        var change = Advance(engine, 1)[0];
        True(change.Items[0].Delisted && !change.Items[0].Observed, "terminal no-trade record remains WASTED");
        Equal(stock.Count - 1, change.Items[0].SourceRecordIndex, "skipped terminal event");
        Equal(0.0, change.Items[0].Return, "terminal empty row added a fabricated index-driven price change");
        Equal(priceBefore, bind.LastPrice, "last real sale price changed on a no-trade terminal row");
        Equal(1, change.NewlyDelistedItemIds.Count, "delisting notification");
        Equal(0, Advance(engine,1)[0].NewlyDelistedItemIds.Count, "duplicate terminal notification");
    }

    private void DeadOnReveal()
    {
        int seed = 1;
        while (seed < 100 && !new MarketSelector(_fixture.Repository, seed).CreateSecurityBinding(_item, 0).CanDelist)
            seed++;
        True(seed < 100, "fixture needs a deterministically drawn terminal security");
        var state = new RunState { RunSeed = seed, RunId = "late-terminal" };
        new MarketClock(state.Clock).Initialize(new MarketTime(1,4,1));
        var engine = new MarketEngine(state, _fixture.Repository);
        Advance(engine, 7);
        var binding = engine.RegisterAppearedItem(_item);
        True(binding.IsDelisted && binding.DelistedWeek < binding.AppearedWeek, "already dead on first display");
        Equal(8, state.GetPriceHistory(_item.ItemId).Count, "complete historical and frozen weeks");
        var trade = new TradingService(state, new FakeWallet(10000),
            new Dictionary<string, ItemDefinition> { { _item.ItemId, _item } }, new CapacityService(() => new CapacityStats(30,10,10)));
        foreach (TradeAction action in Enum.GetValues(typeof(TradeAction)))
        {
            var order = new TradeOrder(action, _item.ItemId, 1);
            Equal(TradeError.Delisted, trade.PreviewCapacity(order).Error, "no WASTED draft");
            Equal(TradeError.Delisted, trade.Execute(order).Error, "no WASTED transaction");
        }
    }
}
