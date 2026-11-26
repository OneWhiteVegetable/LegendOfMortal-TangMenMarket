using System.Collections.Generic;
using System.IO;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class BorrowHorizonTests
{
    private readonly CoreTestFixture _fixture;

    internal BorrowHorizonTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    internal void Register(TestRunner runner)
    {
        runner.Run("borrow-cutoff-pending-mixed-order-preview-and-commit-atomic", TestPendingOrder);
        runner.Run("borrow-cutoff-calendar-independent-of-market-week-and-points", TestCalendar);
        runner.Run("borrow-cutoff-existing-loan-reload-debt-and-repeated-due-week", TestReloadedExpiry);
        runner.Run("short-expiry-unrepresentable-debt-leaves-batch-and-wallet-intact", TestDebtOverflow);
    }

    private RunState InAugust()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.Clock = new MarketClockState();
        state.CurrentGameYear = 3;
        new MarketClock(state.Clock).Initialize(new MarketTime(3, 8, 3));
        return state;
    }

    private TradeOrder Order(TradeAction action, int quantity = 1)
    {
        return new TradeOrder(action, _fixture.Item.ItemId, quantity);
    }

    private void TestPendingOrder()
    {
        RunState state = InAugust();
        var wallet = new FakeWallet(1000);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        True(trading.Execute(Order(TradeAction.Borrow)).Success, "August new loan");
        TradeOrder[] mixed = { Order(TradeAction.Return), Order(TradeAction.Borrow, 2) };
        True(trading.PreviewCapacity(mixed).Success && trading.Preview(mixed).Success, "August basket");
        state.Clock.WeeksThisGameMonth = 4;
        Equal(0, new MarketClock(state.Clock).SynchronizeNaturalTime(new MarketTime(3, 9, 1)),
            "full-action August must not insert a fifth week");
        string before = JsonCodec.Serialize(state, false);
        Equal(TradeError.BorrowWindowClosed, trading.PreviewCapacity(mixed).Error, "September capacity projection");
        Equal(TradeError.BorrowWindowClosed, trading.Preview(mixed).Error, "confirmation revalidation");
        Equal(TradeError.BorrowWindowClosed, trading.Execute(mixed).Error, "stale mixed order commit");
        Equal(before, JsonCodec.Serialize(state, false), "rejected basket changed positions, debt or receipts");
        Equal(1097L, wallet.Balance, "rejected basket changed cash");
        True(trading.Execute(Order(TradeAction.Return)).Success, "existing loan can be returned");
        True(trading.Execute(Order(TradeAction.Buy)).Success, "ordinary buy still available");
        True(trading.Execute(Order(TradeAction.Sell)).Success, "ordinary sale still available");
        Equal(997L, wallet.Balance, "buy/sell/manual return cash");
    }

    private void TestCalendar()
    {
        foreach (int points in new[] { 0, 9, 12, 301 })
        {
            RunState state = InAugust();
            var clock = new MarketClock(state.Clock);
            var trading = _fixture.CreateTrading(state, new FakeWallet(1000));
            clock.ApplyActionCost(points, new MarketTime(3, 8, 3));
            True(trading.PreviewCapacity(Order(TradeAction.Borrow)).Success, "late August after " + points + " points");
            clock.SynchronizeNaturalTime(new MarketTime(3, 9, 1));
            Equal(TradeError.BorrowWindowClosed, trading.PreviewCapacity(Order(TradeAction.Borrow)).Error,
                "first September stage after " + points + " points");
            clock.AdvanceStory(new MarketTime(4, 2, 3));
            Equal(TradeError.BorrowWindowClosed, trading.PreviewCapacity(Order(TradeAction.Borrow)).Error,
                "last free stage cannot borrow");
        }
    }

    private void TestReloadedExpiry()
    {
        RunState state = InAugust();
        var wallet = new FakeWallet(0);
        True(_fixture.CreateTrading(state, wallet).Execute(Order(TradeAction.Borrow, 2)).Success, "open two pieces");
        Equal(194L, wallet.Balance, "opening proceeds");
        string batchId = state.ShortBatches[0].BatchId;
        Equal(24, state.ShortBatches[0].DueWeek, "original due week");
        state.Clock.CurrentGameMonth = new MarketTime(4, 2, 1).AbsoluteMonth;
        _fixture.SetPrice(state, 23, 80);
        UnityEngine.Application.persistentDataPath = Path.Combine(_fixture.Root, "borrow-horizon");
        var store = new SidecarStore("audit");
        True(store.Persist("profile", "002", 23L, "profile/Save_002.dat", state), store.LastError);
        RunState restored;
        True(store.TryLoadDisk("profile", "002", 23L, out restored), store.LastError);
        TradingService trading = _fixture.CreateTrading(restored, wallet);
        var shorts = new ShortService(trading);
        Equal(TradeError.BorrowWindowClosed, trading.Preview(Order(TradeAction.Borrow)).Error, "loaded cutoff");
        Equal(0, shorts.SettleDueBatches().Count, "one week early");
        Equal(194L, wallet.Balance, "early settlement changed wallet");
        _fixture.SetPrice(restored, 24, 120);
        IReadOnlyList<TradeResult> due = shorts.SettleDueBatches();
        True(due.Count == 1 && due[0].Success, "due batch did not settle");
        Equal(-46L, wallet.Balance, "signed wallet at expiry");
        Equal(0L, restored.Debt, "buyback principal already went into wallet");
        TradeRecord record = restored.TradeHistory[1];
        Equal(batchId, record.BatchId, "expiry allocated another contract");
        Equal(TradeActionNames.Expire, record.Action, "missing expiry ledger row");
        Equal(-240L, record.CashDelta, "whole buyback in signed cash ledger");
        Equal(0L, record.DebtDelta, "no sidecar double charge");
        True(restored.ShortBatches.Count == 0 && restored.Holdings.Count == 0, "buyback did not close loan");
        True(store.Persist("profile", "002", 24L, "profile/Save_002.dat", restored), store.LastError);
        True(store.TryLoadDisk("profile", "002", 24L, out restored), store.LastError);
        shorts = new ShortService(_fixture.CreateTrading(restored, wallet));
        Equal(0, shorts.SettleDueBatches().Count, "replayed due week charged twice");
        Equal(2, restored.TradeHistory.Count, "duplicate expiry record");
    }

    private void TestDebtOverflow()
    {
        RunState state = InAugust();
        var wallet = new FakeWallet(0);
        var shorts = new ShortService(_fixture.CreateTrading(state, wallet));
        True(shorts.Open(_fixture.Item.ItemId, 1).Success, "open loan for overflow");
        wallet.Set(int.MinValue);
        state.Debt = long.MaxValue - 6;
        _fixture.SetPrice(state, 24, 10);
        string before = JsonCodec.Serialize(state, false);
        Equal(TradeError.CashOverflow, shorts.SettleDueBatches()[0].Error, "unrepresentable debt");
        Equal(before, JsonCodec.Serialize(state, false), "overflow changed contract or debt");
        Equal((long)int.MinValue, wallet.Balance, "overflow changed wallet");
        wallet.Set((long)int.MinValue + 4);
        True(shorts.SettleDueBatches()[0].Success, "exact upper debt boundary");
        Equal(long.MaxValue, state.Debt, "exact debt result");
        Equal((long)int.MinValue, wallet.Balance, "exact debt wallet");
        Equal(0, shorts.SettleDueBatches().Count, "duplicate settlement on retry");
    }
}
