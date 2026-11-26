using System;
using System.Collections.Generic;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class CapacityTests
{
    private readonly CoreTestFixture _fixture;

    public CapacityTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("capacity-life-examples-seven-to-thirty", TestLifeExamples);
        runner.Run("capacity-original-weight-formula", TestWeightFormula);
        runner.Run("capacity-long-short-slot-accounting", TestSlotAccounting);
        runner.Run("capacity-trade-limit-is-atomic", TestTradeLimit);
    }

    private static void TestLifeExamples()
    {
        int[] lives = { 7, 18, 20, 21, 30 };
        int[] bonuses = { 1, 2, 2, 3, 3 };
        int[] totals = { 3, 4, 4, 5, 5 };
        for (int i = 0; i < lives.Length; i++)
        {
            Equal(bonuses[i], CapacityService.GetBonusSlots(lives[i]), "life bonus " + lives[i]);
            Equal(totals[i], CapacityService.GetTotalSlots(lives[i]), "life total slots " + lives[i]);
        }
        Equal(2, CapacityService.BaseSlots, "base slot formula");
    }

    private static void TestWeightFormula()
    {
        Equal(100, CapacityService.GetCarryingCapacity(10, 10), "lightness plus internal strength times five");
        Equal(75, CapacityService.GetCarryingCapacity(7, 8), "original mixed stats formula");
        Equal(0, CapacityService.GetCarryingCapacity(-3, 2), "non-positive carrying capacity");
        Equal(4, CapacityService.GetStackLimit(new ItemDefinition { Weight = 25 }, 100), "exact stack division");
        Equal(3, CapacityService.GetStackLimit(new ItemDefinition { Weight = 30 }, 100), "stack limit rounds down");
        Equal(40, CapacityService.GetStackLimit(new ItemDefinition { Weight = 2.5 }, 100), "fractional item weight");
        Equal(0, CapacityService.GetStackLimit(new ItemDefinition { Weight = 101 }, 100), "overweight single item");
        Equal(0, CapacityService.GetStackLimit(new ItemDefinition { Weight = 0 }, 100), "invalid item weight");
    }

    private void TestSlotAccounting()
    {
        ItemDefinition definition = new ItemDefinition { ItemId = _fixture.Item.ItemId, Weight = 20 };
        var definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        definitions.Add(definition.ItemId, definition);
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = definition.ItemId, Quantity = 4, TotalCost = 400 });
        state.ShortBatches.Add(new ShortBatch
        {
            ItemId = definition.ItemId,
            BatchId = "capacity-batch",
            Quantity = 1,
            DueWeek = 24,
            Proceeds = 97
        });
        CapacitySnapshot snapshot = new CapacityService().Evaluate(
            state,
            definitions,
            new CapacityStats(30, 7, 8));
        Equal(TradeError.None, snapshot.Error, "capacity evaluation error");
        Equal(5, snapshot.TotalSlots, "life 30 slots");
        Equal(75, snapshot.CarryingCapacity, "stats carrying capacity");
        Equal(3, snapshot.FindItem(definition.ItemId).StackLimit, "item stack limit");
        Equal(2, snapshot.UsedSlots, "long and short shared slot accounting");
        Equal(3, snapshot.AvailableSlots, "remaining slots");
        True(!snapshot.IsOverCapacity, "valid combined quantity marked over capacity");
    }

    private void TestTradeLimit()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(1000);
        TradingService trading = _fixture.CreateTrading(
            state,
            wallet,
            new CapacityStats(7, 10, 10),
            40);
        TradeResult filled = trading.Execute(new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 6));
        True(filled.Success, "fill exact capacity failed: " + filled.Message);
        Equal(400L, wallet.Balance, "exact capacity cash");
        Equal(3, filled.CapacityAfter.TotalSlots, "life 7 trade slots");
        Equal(2, filled.CapacityAfter.FindItem(_fixture.Item.ItemId).StackLimit, "trade stack limit");
        Equal(3, filled.CapacityAfter.UsedSlots, "exact capacity used slots");

        TradeResult rejected = trading.Execute(new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        Equal(TradeError.CapacityExceeded, rejected.Error, "over-capacity buy error");
        Equal(400L, wallet.Balance, "over-capacity buy changed cash");
        Equal(6, state.FindHolding(_fixture.Item.ItemId).Quantity, "over-capacity buy changed holding");
        Equal(1, state.TradeHistory.Count, "over-capacity buy wrote history");

        TradeResult sold = trading.Execute(new TradeOrder(TradeAction.Sell, _fixture.Item.ItemId, 2));
        True(sold.Success, "capacity-reducing sale failed: " + sold.Message);
        Equal(600L, wallet.Balance, "capacity-reducing sale cash");
        Equal(4, state.FindHolding(_fixture.Item.ItemId).Quantity, "capacity-reducing sale quantity");
        Equal(2, sold.CapacityAfter.UsedSlots, "sale did not release slot");
    }
}
