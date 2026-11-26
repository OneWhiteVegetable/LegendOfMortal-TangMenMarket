using System.Collections.Generic;
using Mortal.Core;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class TradingTests
{
    private readonly CoreTestFixture _fixture;

    public TradingTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("trade-atomicity-and-direction", TestTradeAtomicity);
        runner.Run("cash-lower-upper-limits", TestCashLimits);
        runner.Run("short-proceeds-batches-due-price", TestShortDuePrice);
        runner.Run("automatic-debt-and-repayment", TestDebt);
        runner.Run("trade-preview-buy-and-sell", TestPreviewBuyAndSell);
        runner.Run("short-open-and-manual-close", TestShortManualClose);
    }

    private void TestTradeAtomicity()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 2, TotalCost = 200 });
        FakeWallet wallet = new FakeWallet(100);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        TradeResult result = trading.Execute(new[]
        {
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1),
            new TradeOrder(TradeAction.Sell, _fixture.Item.ItemId, 4)
        });
        Equal(TradeError.InsufficientHolding, result.Error, "mixed atomic failure");
        Equal(100L, wallet.Balance, "failed basket changed cash");
        Equal(2, state.FindHolding(_fixture.Item.ItemId).Quantity, "failed basket changed holding");
        Equal(0, state.TradeHistory.Count, "failed basket wrote history");

        state.ShortBatches.Add(new ShortBatch
        {
            ItemId = _fixture.Item.ItemId,
            BatchId = "b1",
            Quantity = 1,
            OpenWeek = 0,
            DueWeek = 24,
            Proceeds = 97
        });
        result = trading.Execute(new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        Equal(TradeError.PositionConflict, result.Error, "final direction conflict");
        Equal(100L, wallet.Balance, "conflict changed cash");
    }

    private void TestCashLimits()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(50);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        TradeResult result = trading.Execute(new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        Equal(TradeError.InsufficientCash, result.Error, "cash lower bound");
        Equal(50L, wallet.Balance, "insufficient buy changed cash");

        state.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 1, TotalCost = 100 });
        wallet.Set(90, 0, 100);
        result = trading.Execute(new TradeOrder(TradeAction.Sell, _fixture.Item.ItemId, 1));
        Equal(TradeError.CashOverflow, result.Error, "cash upper bound");
        Equal(90L, wallet.Balance, "upper overflow changed cash");
        Equal(1, state.FindHolding(_fixture.Item.ItemId).Quantity, "upper overflow changed holding");

        PlayerStatData stats = new PlayerStatData();
        stats.Add(new GameStat(GameStatType.銀兩, 95, 0, 100));
        MortalWallet mortalWallet = new MortalWallet(stats);
        True(mortalWallet.TryDeposit(100000000 - 95), "MortalWallet exact cap");
        True(!mortalWallet.TryDeposit(1) && !mortalWallet.CanDeposit(long.MaxValue), "MortalWallet accepted overflow");
        Equal(100000000L, mortalWallet.Balance, "rejected credit changed original");
        long fullSpan = 100000000L - int.MinValue;
        True(mortalWallet.TrySpend(fullSpan), "MortalWallet full signed debit span");
        Equal((long)int.MinValue, mortalWallet.Balance, "MortalWallet lower bound");
        True(!mortalWallet.TrySpend(1) && !mortalWallet.CanSpend(long.MaxValue), "MortalWallet spent below minimum");
        True(mortalWallet.TryDeposit(fullSpan), "MortalWallet full signed credit span");
        Equal(100000000L, mortalWallet.Balance, "MortalWallet upper bound restored");
    }

    private void TestShortDuePrice()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(0);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        ShortService shorts = new ShortService(trading);
        TradeResult opened = shorts.Open(_fixture.Item.ItemId, 3);
        True(opened.Success, "short open failed: " + opened.Message);
        Equal(291L, wallet.Balance, "97 percent proceeds");
        Equal(24, state.ShortBatches[0].DueWeek, "short duration");

        _fixture.SetPrice(state, 1, 100);
        True(shorts.Open(_fixture.Item.ItemId, 1).Success, "second short open failed");
        Equal(24, state.ShortBatches[0].DueWeek, "old batch due refreshed");
        Equal(25, state.ShortBatches[1].DueWeek, "new batch due");

        state.ShortBatches.RemoveAt(1);
        _fixture.SetPrice(state, 24, 40);
        IReadOnlyList<TradeResult> settled = shorts.SettleDueBatches();
        Equal(1, settled.Count, "due result count");
        True(settled[0].Success, "due settlement failed: " + settled[0].Message);
        Equal(268L, wallet.Balance, "due price was not week 24 price");
        Equal(0, state.ShortBatches.Count, "due batches remained");
        Equal(40.0, state.TradeHistory[state.TradeHistory.Count - 1].UnitPrice, "expiry record price");
        Equal(TradeActionNames.Expire, state.TradeHistory[state.TradeHistory.Count - 1].Action, "expiry action");
    }

    private void TestDebt()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(0);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        ShortService shorts = new ShortService(trading);
        True(shorts.Open(_fixture.Item.ItemId, 2).Success, "debt setup open");
        wallet.Set(0);
        _fixture.SetPrice(state, 24, 150);
        TradeResult result = shorts.SettleDueBatches()[0];
        True(result.Success, "automatic debt settlement failed");
        Equal(-300L, wallet.Balance, "automatic debit becomes actual negative cash");
        Equal(0L, state.Debt, "representable debit must not create a sidecar debt");
        Equal(0L, result.DebtDelta, "no duplicate debt principal");
        Equal(-300L, result.CashDelta, "whole buyback in wallet ledger");

        True(wallet.TryDeposit(100), "native income accepted");
        long repaid;
        True(shorts.TryRepayDebtFromCash(out repaid), "income repayment rejected");
        Equal(0L, repaid, "normal signed cash must not be collected again");
        Equal(-200L, wallet.Balance, "income reduced overdraft");
        Equal(0L, state.Debt, "only unrepresentable excess belongs outside wallet");

        RunState saleState = _fixture.CreateTradeState(100);
        saleState.Debt = 50;
        saleState.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 1, TotalCost = 100 });
        FakeWallet saleWallet = new FakeWallet(0);
        TradingService saleTrading = _fixture.CreateTrading(saleState, saleWallet);
        result = saleTrading.Execute(new TradeOrder(TradeAction.Sell, _fixture.Item.ItemId, 1));
        True(result.Success, "sale with debt failed");
        Equal(50L, saleWallet.Balance, "sale debt-first cash");
        Equal(0L, saleState.Debt, "sale debt-first debt");
    }

    private void TestPreviewBuyAndSell()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(1000);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        TradeOrder buy = new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 2);
        TradeResult preview = trading.Preview(buy);
        True(preview.Success, "buy preview failed: " + preview.Message);
        Equal(1000L, wallet.Balance, "preview changed cash");
        True(state.FindHolding(_fixture.Item.ItemId) == null, "preview changed holding");
        Equal(0, state.TradeHistory.Count, "preview wrote history");
        Equal(string.Empty, preview.Records[0].TradeId, "preview allocated persisted trade ID");

        True(trading.Execute(buy).Success, "buy execution failed");
        Equal(800L, wallet.Balance, "buy cash");
        Equal(2, state.FindHolding(_fixture.Item.ItemId).Quantity, "buy quantity");
        Equal(200L, state.FindHolding(_fixture.Item.ItemId).TotalCost, "buy cost basis");

        _fixture.SetPrice(state, 1, 120);
        TradeResult sale = trading.Execute(new TradeOrder(TradeAction.Sell, _fixture.Item.ItemId, 1));
        True(sale.Success, "sell execution failed: " + sale.Message);
        Equal(920L, wallet.Balance, "sell cash");
        Equal(1, state.FindHolding(_fixture.Item.ItemId).Quantity, "sell quantity");
        Equal(100L, state.FindHolding(_fixture.Item.ItemId).TotalCost, "remaining cost basis");
        Equal(2, state.TradeHistory.Count, "buy and sell history");
        Equal(TradeActionNames.Sell, state.TradeHistory[1].Action, "sell history action");
        Equal(120.0, state.TradeHistory[1].UnitPrice, "sell history price");
    }

    private void TestShortManualClose()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(0);
        ShortService shorts = new ShortService(_fixture.CreateTrading(state, wallet));
        True(shorts.Open(_fixture.Item.ItemId, 2).Success, "manual close setup open failed");
        Equal(194L, wallet.Balance, "manual close opening proceeds");
        Equal(24, shorts.GetNextDueWeek(_fixture.Item.ItemId), "manual close next due week");

        _fixture.SetPrice(state, 1, 80);
        Equal(23, shorts.GetWeeksRemaining(_fixture.Item.ItemId), "manual close weeks remaining");
        TradeResult first = shorts.Repay(_fixture.Item.ItemId, 1);
        True(first.Success, "first manual close failed: " + first.Message);
        Equal(114L, wallet.Balance, "first manual close cash");
        Equal(1, state.ShortBatches.Count, "first manual close removed batch");
        Equal(1, state.ShortBatches[0].Quantity, "first manual close remaining quantity");
        Equal(97L, state.ShortBatches[0].Proceeds, "first manual close remaining proceeds");

        _fixture.SetPrice(state, 2, 60);
        TradeResult second = shorts.Repay(_fixture.Item.ItemId, 1);
        True(second.Success, "final manual close failed: " + second.Message);
        Equal(54L, wallet.Balance, "final manual close cash");
        Equal(0, state.ShortBatches.Count, "final manual close left debt batch");
        Equal(3, state.TradeHistory.Count, "short open and close history");
        Equal(TradeActionNames.Return, state.TradeHistory[2].Action, "manual close history action");
        Equal(60.0, state.TradeHistory[2].UnitPrice, "manual close history price");
    }
}
