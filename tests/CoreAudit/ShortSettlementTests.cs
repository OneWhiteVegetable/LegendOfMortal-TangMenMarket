using System.Collections.Generic;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class ShortSettlementTests
{
    private readonly CoreTestFixture _fixture;

    public ShortSettlementTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("short-oversized-due-settlement-chunks", TestOversizedDueSettlement);
        runner.Run("short-delisted-system-settlement", TestDelistedSettlement);
    }

    private void TestOversizedDueSettlement()
    {
        RunState state = _fixture.CreateTradeState(1);
        state.ShortBatches.Add(new ShortBatch
        {
            ItemId = _fixture.Item.ItemId,
            BatchId = "oversized-a",
            Quantity = int.MaxValue,
            OpenWeek = 0,
            DueWeek = 24,
            Proceeds = 97
        });
        state.ShortBatches.Add(new ShortBatch
        {
            ItemId = _fixture.Item.ItemId,
            BatchId = "oversized-b",
            Quantity = 1,
            OpenWeek = 0,
            DueWeek = 24,
            Proceeds = 1
        });
        _fixture.SetPrice(state, 24, 1);
        FakeWallet wallet = new FakeWallet(100000000L);
        ShortService shorts = new ShortService(_fixture.CreateTrading(state, wallet));
        IReadOnlyList<TradeResult> results = shorts.SettleDueBatches();
        Equal(2, results.Count, "oversized due chunk count");
        True(results[0].Success, "oversized first chunk failed: " + results[0].Message);
        True(results[1].Success, "oversized second chunk failed: " + results[1].Message);
        Equal(int.MaxValue, results[0].Records[0].Quantity, "oversized first chunk quantity");
        Equal(1, results[1].Records[0].Quantity, "oversized second chunk quantity");
        Equal(-2047483648L, wallet.Balance, "oversized settlement cash");
        Equal(0L, state.Debt, "oversized settlement debt");
        Equal(0, state.ShortBatches.Count, "oversized settlement left batches");
        Equal(2, state.TradeHistory.Count, "oversized settlement history");
        Equal(TradeActionNames.Expire, state.TradeHistory[0].Action, "oversized first history action");
        Equal(TradeActionNames.Expire, state.TradeHistory[1].Action, "oversized second history action");
    }

    private void TestDelistedSettlement()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.ShortBatches.Add(new ShortBatch
        {
            ItemId = _fixture.Item.ItemId,
            BatchId = "delisted-short",
            Quantity = 2,
            OpenWeek = 0,
            DueWeek = 24,
            Proceeds = 194
        });
        _fixture.SetPrice(state, 1, 80);
        state.Clock.MarketWeekIndex = 2;
        SecurityBinding binding = state.FindSecurityBinding(_fixture.Item.ItemId);
        binding.IsDelisted = true;
        binding.DelistedWeek = 2;
        state.PriceHistory.Add(new WeeklyPoint
        {
            ItemId = _fixture.Item.ItemId,
            MarketWeekIndex = 2,
            GameYear = 1,
            GameMonth = 4,
            GameRound = 3,
            Price = 1,
            Delisted = true,
            Observed = true
        });
        FakeWallet wallet = new FakeWallet(300);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        ShortService shorts = new ShortService(trading);

        TradeResult ordinary = shorts.Repay(_fixture.Item.ItemId, 1);
        Equal(TradeError.Delisted, ordinary.Error, "ordinary delisted repayment was accepted");
        Equal(300L, wallet.Balance, "ordinary delisted repayment changed cash");
        Equal(2, state.ShortBatches[0].Quantity, "ordinary delisted repayment changed batch");

        IReadOnlyList<TradeResult> results = shorts.SettleDelistedBatches(_fixture.Item.ItemId);
        Equal(1, results.Count, "delisted settlement result count");
        True(results[0].Success, "delisted settlement failed: " + results[0].Message);
        Equal(140L, wallet.Balance, "delisted settlement cash");
        Equal(0, state.ShortBatches.Count, "delisted settlement left batches");
        Equal(1, state.TradeHistory.Count, "delisted settlement history");
        Equal(80.0, state.TradeHistory[0].UnitPrice, "delisted settlement did not use last valid price");
        Equal(TradeActionNames.Expire, state.TradeHistory[0].Action, "delisted settlement history action");
        Equal("delisted-short", state.TradeHistory[0].BatchId, "delisted settlement batch ID");
    }
}
