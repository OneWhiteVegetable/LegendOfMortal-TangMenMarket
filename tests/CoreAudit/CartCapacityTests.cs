using System;
using System.Collections.Generic;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class CartCapacityTests
{
    private readonly CoreTestFixture _fixture;

    public CartCapacityTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("cart-split-uses-open-slot-remainder-and-single-order-rounding", TestSplitRows);
        runner.Run("cart-capacity-exact-fill-then-one-more-is-rejected-pure", TestExactFillAndIncrement);
        runner.Run("cart-capacity-rejects-999-before-settlement", TestRejects999);
        runner.Run("cart-capacity-pending-long-short-share-real-slots", TestSharedPendingSlots);
        runner.Run("cart-capacity-floor-stack-and-overweight-message", TestStackRoundingAndMessages);
        runner.Run("cart-capacity-existing-short-and-direction-conflict", TestShortOccupancyAndConflict);
        runner.Run("cart-capacity-mixed-release-and-increase-keeps-trade-rules", TestMixedReleaseAndIncrease);
        runner.Run("cart-capacity-preview-and-full-preview-do-not-mutate", TestPreviewPurity);
        runner.Run("cart-capacity-ui-message-explains-other-rejections", TestOtherFeedback);
    }

    private void TestSplitRows()
    {
        var empty = new CartStackLayout(5, 4, 0, true);
        Equal(2, empty.Count, "five pieces need two contracts");
        empty.GetRow(0, out int offset, out int quantity, out int limit);
        Equal(4, quantity, "first contract fills one slot");
        empty.GetRow(1, out offset, out quantity, out limit);
        Equal(4, offset, "second contract begins after four");
        Equal(1, quantity, "last contract can be removed by one decrement");

        var partial = new CartStackLayout(5, 4, 2, true);
        partial.GetRow(0, out offset, out quantity, out limit);
        Equal(2, limit, "two held pieces leave room for two in the first slot");
        Equal(2, quantity, "first two join the held stack");
        partial.GetRow(1, out offset, out quantity, out limit);
        Equal(3, quantity, "three more enter the next slot");

        var extreme = new CartStackLayout(int.MaxValue, 1, 0, true);
        Equal(int.MaxValue, extreme.Count, "large unlocked backpacks project without materializing billions of rows");
        extreme.GetRow(int.MaxValue - 1, out offset, out quantity, out limit);
        Equal(int.MaxValue - 1, offset, "last row offset within quantity");
        long sum = 0;
        for (int i = 0; i < empty.Count; i++)
        {
            empty.GetRow(i, out offset, out quantity, out limit);
            True(CartStackLayout.TryRowAmount(10.75, offset, quantity, true, out long subtotal), "valid borrow subtotal");
            sum += subtotal;
        }
        // floor(4 * 10.75 * .97) + floor(10.75 * .97) would lose one wen.
        True(TradingService.TryShortProceeds(10.75, 5, out long aggregate), "valid whole borrow proceeds");
        Equal(52L, sum, "visual contracts must receive 41 + 11 wen, not 41 + 10");
        Equal(aggregate, sum, "visual split preserves single-order proceeds");
        RunState state = _fixture.CreateTradeState(10.75);
        var wallet = new FakeWallet(1000);
        TradingService trading = _fixture.CreateTrading(state, wallet, new CapacityStats(0, 10, 10), 25);
        var order = new TradeOrder(TradeAction.Borrow, _fixture.Item.ItemId, 5);
        TradeResult preview = trading.Preview(order);
        True(preview.Success, preview.Message);
        Equal(sum, preview.CashDelta, "two displayed rows reconcile with the preview");
        TradeResult settled = trading.Execute(order);
        True(settled.Success, settled.Message);
        Equal(1052L, wallet.Balance, "two displayed rows settle exactly 52 wen once");
        Equal(1, state.TradeHistory.Count, "one economic order records one borrow");
        Equal(1, state.ShortBatches.Count, "one economic order opens one loan");
        Equal(5, state.ShortBatches[0].Quantity, "one batch includes all five pieces");
        Equal(24, state.ShortBatches[0].DueWeek, "one due date for the complete order");
    }

    private void TestExactFillAndIncrement()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 4, TotalCost = 400 });
        FakeWallet wallet = new FakeWallet(1000);
        TradingService trading = _fixture.CreateTrading(
            state,
            wallet,
            new CapacityStats(7, 10, 10),
            40);

        TradeResult exact = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 2));
        True(exact.Success, "existing four plus cart two must exactly fill three slots: " + exact.Message);
        Equal(3, exact.CapacityAfter.TotalSlots, "real total slots");
        Equal(2, exact.CapacityAfter.FindItem(_fixture.Item.ItemId).StackLimit, "floor stack limit");
        Equal(3, exact.CapacityAfter.UsedSlots, "exact fill used slots");
        Equal(0, exact.CapacityAfter.AvailableSlots, "exact fill available slots");

        TradeResult excess = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 3));
        Equal(TradeError.CapacityExceeded, excess.Error, "one more than exact fill must fail");
        Equal(4L, excess.RequiredAmount, "projected required slots");
        Equal(3L, excess.AvailableAmount, "projected total slots");
        Equal(4, excess.CapacityAfter.UsedSlots, "one extra stack requires another slot");
        Equal(4, state.FindHolding(_fixture.Item.ItemId).Quantity, "preview mutated existing holding");
        Equal(1000L, wallet.Balance, "capacity preview changed cash");
        Equal(0, state.TradeHistory.Count, "capacity preview wrote history");
    }

    private void TestRejects999()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(1000000);
        TradingService trading = _fixture.CreateTrading(
            state,
            wallet,
            new CapacityStats(7, 10, 10),
            40);
        string before = JsonCodec.Serialize(state, false);

        TradeResult result = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 999));
        Equal(TradeError.CapacityExceeded, result.Error, "999 must use real capacity rather than a UI cap");
        Equal(500, result.CapacityAfter.UsedSlots, "999 items at two per slot");
        Equal(3, result.CapacityAfter.TotalSlots, "999 rejection total slots");
        Equal(before, JsonCodec.Serialize(state, false), "999 projection changed run state");
        Equal(1000000L, wallet.Balance, "999 projection changed cash");
    }

    private void TestSharedPendingSlots()
    {
        const string otherId = "02_other";
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 2, TotalCost = 200 });
        state.ShortBatches.Add(Batch(otherId, 2));
        FakeWallet wallet = new FakeWallet(1000);
        TradingService trading = CreateTrading(
            state,
            wallet,
            new CapacityStats(18, 10, 10),
            Definition(_fixture.Item.ItemId, 50),
            Definition(otherId, 50));

        var exactOrders = new[]
        {
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 2),
            new TradeOrder(TradeAction.Borrow, otherId, 2)
        };
        TradeResult exact = trading.PreviewCapacity(exactOrders);
        True(exact.Success, "long and short pending orders should exactly share four slots: " + exact.Message);
        Equal(4, exact.CapacityAfter.TotalSlots, "life eighteen total slots");
        Equal(2, exact.CapacityAfter.FindItem(_fixture.Item.ItemId).UsedSlots, "existing plus pending long slots");
        Equal(2, exact.CapacityAfter.FindItem(otherId).UsedSlots, "existing plus pending short slots");
        Equal(4, exact.CapacityAfter.UsedSlots, "long and short must not receive separate slot pools");

        TradeResult excess = trading.PreviewCapacity(new[]
        {
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 3),
            new TradeOrder(TradeAction.Borrow, otherId, 2)
        });
        Equal(TradeError.CapacityExceeded, excess.Error, "pending order on either item must respect shared capacity");
        Equal(5, excess.CapacityAfter.UsedSlots, "one pending increment requires a fifth shared slot");
        Equal(2, state.ShortBatches[0].Quantity, "shared-slot preview changed existing short");
        Equal(2, state.FindHolding(_fixture.Item.ItemId).Quantity, "shared-slot preview changed existing long");
    }

    private void TestStackRoundingAndMessages()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 3, TotalCost = 300 });
        TradingService trading = _fixture.CreateTrading(
            state,
            new FakeWallet(1000),
            new CapacityStats(20, 7, 8),
            30);

        TradeResult exact = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 5));
        True(exact.Success, "floor(75 / 30) must allow eight items in four slots: " + exact.Message);
        Equal(75, exact.CapacityAfter.CarryingCapacity, "mixed-stat carrying capacity");
        Equal(2, exact.CapacityAfter.FindItem(_fixture.Item.ItemId).StackLimit, "stack limit must round down");
        Equal(4, exact.CapacityAfter.UsedSlots, "rounded stack exact fill");

        TradeResult excess = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 6));
        Equal(TradeError.CapacityExceeded, excess.Error, "rounded stack boundary accepted excess");
        string message = CartCapacityFeedback.Rejection(excess, _fixture.Item.ItemId, "测试货", 1);
        True(message.Contains("货囊共 4 格"), "capacity message omits real total slots: " + message);
        True(message.Contains("投影需 5 格"), "capacity message omits projected slots: " + message);
        True(message.Contains("每格最多 2 件"), "capacity message omits per-slot limit: " + message);
        True(message.Contains("当前负重 75"), "capacity message omits carrying capacity: " + message);
        True(message.Contains("契匣未更改"), "capacity message does not explain atomic rejection: " + message);

        TradingService overweight = _fixture.CreateTrading(
            _fixture.CreateTradeState(100),
            new FakeWallet(1000),
            new CapacityStats(20, 7, 8),
            80);
        TradeResult tooHeavy = overweight.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        Equal(TradeError.CapacityExceeded, tooHeavy.Error, "item heavier than one slot must be rejected");
        string heavyMessage = CartCapacityFeedback.Rejection(
            tooHeavy,
            _fixture.Item.ItemId,
            "沉重货",
            1);
        True(heavyMessage.Contains("每格可装 0 件"), "overweight message is not actionable: " + heavyMessage);
        True(heavyMessage.Contains("当前负重 75"), "overweight message omits actual capacity: " + heavyMessage);
    }

    private void TestShortOccupancyAndConflict()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.ShortBatches.Add(Batch(_fixture.Item.ItemId, 2));
        TradingService trading = _fixture.CreateTrading(
            state,
            new FakeWallet(1000),
            new CapacityStats(7, 10, 10),
            40);

        TradeResult conflict = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        Equal(TradeError.PositionConflict, conflict.Error, "existing short must block a pending long");
        TradeResult excessReturn = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Return, _fixture.Item.ItemId, 3));
        Equal(TradeError.InsufficientShortPosition, excessReturn.Error, "pending return exceeded real short position");

        TradeResult exact = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Borrow, _fixture.Item.ItemId, 4));
        True(exact.Success, "existing and pending same-direction shorts should exactly fill: " + exact.Message);
        Equal(6L, exact.CapacityAfter.FindItem(_fixture.Item.ItemId).ShortQuantity, "projected short quantity");
        Equal(3, exact.CapacityAfter.UsedSlots, "existing and pending short slots");
        TradeResult excess = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Borrow, _fixture.Item.ItemId, 5));
        Equal(TradeError.CapacityExceeded, excess.Error, "existing short occupancy was omitted from pending limit");
        Equal(1, state.ShortBatches.Count, "short projection added a persisted batch");
        Equal(2, state.ShortBatches[0].Quantity, "short projection changed existing batch");
    }

    private void TestMixedReleaseAndIncrease()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 6, TotalCost = 600 });
        FakeWallet wallet = new FakeWallet(0);
        TradingService trading = _fixture.CreateTrading(
            state,
            wallet,
            new CapacityStats(7, 10, 10),
            40);
        var orders = new[]
        {
            new TradeOrder(TradeAction.Sell, _fixture.Item.ItemId, 6),
            new TradeOrder(TradeAction.Borrow, _fixture.Item.ItemId, 6)
        };

        TradeResult preview = trading.PreviewCapacity(orders);
        True(preview.Success, "sale releasing all long slots before a borrow must project correctly: " + preview.Message);
        Equal(3, preview.CapacityAfter.UsedSlots, "replacement short exact capacity");
        Equal(6, state.FindHolding(_fixture.Item.ItemId).Quantity, "mixed preview sold real holdings");
        Equal(0, state.ShortBatches.Count, "mixed preview opened real short");

        TradeResult executed = trading.Execute(orders);
        True(executed.Success, "mixed execution regressed after shared projection: " + executed.Message);
        Equal(1182L, wallet.Balance, "sale proceeds plus 97 percent short proceeds");
        Equal(1, state.ShortBatches.Count, "execution short batch count");
        Equal(24, state.ShortBatches[0].DueWeek, "execution must retain 24-week short duration");
        Equal(6, state.ShortBatches[0].Quantity, "execution short quantity");
        True(state.FindHolding(_fixture.Item.ItemId) == null, "execution left a conflicting long");
    }

    private void TestPreviewPurity()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(0);
        TradingService trading = _fixture.CreateTrading(
            state,
            wallet,
            new CapacityStats(7, 10, 10),
            40);
        string before = JsonCodec.Serialize(state, false);

        TradeResult capacity = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Borrow, _fixture.Item.ItemId, 2));
        True(capacity.Success, "short capacity preview failed: " + capacity.Message);
        TradeResult full = trading.Preview(
            new TradeOrder(TradeAction.Borrow, _fixture.Item.ItemId, 2));
        True(full.Success, "full short preview failed: " + full.Message);
        Equal(194L, full.CashDelta, "full preview no longer uses 97 percent proceeds");
        Equal(string.Empty, full.Records[0].TradeId, "preview allocated a persisted trade ID");
        Equal(before, JsonCodec.Serialize(state, false), "preview changed serialized run state");
        Equal(0L, wallet.Balance, "preview changed wallet balance");
        Equal(0, state.ShortBatches.Count, "preview persisted a short batch");
        Equal(0, state.TradeHistory.Count, "preview persisted trade history");

        TradeResult noCashNeededForCapacity = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 6));
        True(noCashNeededForCapacity.Success, "cart capacity projection must not reject merely for insufficient cash");
        TradeResult cashFailure = trading.Preview(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        Equal(TradeError.InsufficientCash, cashFailure.Error, "full preview must retain cash validation");
        Equal(before, JsonCodec.Serialize(state, false), "cash preview changed serialized run state");
    }

    private void TestOtherFeedback()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.ShortBatches.Add(Batch(_fixture.Item.ItemId, 1));
        TradingService trading = _fixture.CreateTrading(state, new FakeWallet(1000));
        TradeResult conflict = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        string conflictMessage = CartCapacityFeedback.Rejection(
            conflict,
            _fixture.Item.ItemId,
            "测试货",
            1);
        True(conflictMessage.Contains("含契匣待结算指令"), "conflict message omits pending-order scope");
        True(conflictMessage.Contains("契匣未更改"), "conflict message omits unchanged cart");

        TradeResult insufficient = trading.PreviewCapacity(
            new TradeOrder(TradeAction.Return, _fixture.Item.ItemId, 2));
        string returnMessage = CartCapacityFeedback.Rejection(
            insufficient,
            _fixture.Item.ItemId,
            "测试货",
            1);
        True(returnMessage.Contains("可归还的数量不足"), "return message is not understandable: " + returnMessage);
        True(returnMessage.Contains("契匣未更改"), "return message omits unchanged cart");
    }

    private TradingService CreateTrading(
        RunState state,
        IPlayerWallet wallet,
        CapacityStats stats,
        params ItemDefinition[] definitions)
    {
        var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Length; i++)
            items.Add(definitions[i].ItemId, definitions[i]);
        return new TradingService(state, wallet, items, new CapacityService(delegate { return stats; }));
    }

    private static ItemDefinition Definition(string itemId, double weight)
    {
        return new ItemDefinition
        {
            ItemId = itemId,
            Name = itemId,
            ReferencePrice = 100,
            Weight = weight
        };
    }

    private static ShortBatch Batch(string itemId, int quantity)
    {
        return new ShortBatch
        {
            ItemId = itemId,
            BatchId = "cart-capacity-" + itemId,
            Quantity = quantity,
            OpenWeek = 0,
            DueWeek = 24,
            Proceeds = quantity * 97
        };
    }
}
