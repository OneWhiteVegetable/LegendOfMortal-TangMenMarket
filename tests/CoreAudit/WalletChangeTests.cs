using System;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class WalletChangeTests
{
    private readonly CoreTestFixture _fixture;

    public WalletChangeTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("wallet-exception-compensation-and-trade-atomicity", TestTradeCompensation);
        runner.Run("wallet-debt-repayment-exception-compensation", TestDebtCompensation);
    }

    private void TestTradeCompensation()
    {
        RunState buyState = _fixture.CreateTradeState(100);
        FaultInjectingWallet buyWallet = new FaultInjectingWallet(1000, true, false);
        TradingService buyTrading = _fixture.CreateTrading(buyState, buyWallet);
        TradeResult buy = buyTrading.Execute(new TradeOrder(TradeAction.Buy, _fixture.Item.ItemId, 1));
        Equal(TradeError.WalletRejected, buy.Error, "spend exception result");
        Equal(1000L, buyWallet.Balance, "spend exception compensation");
        Equal(1, buyWallet.SpendCalls, "spend attempt count");
        Equal(1, buyWallet.DepositCalls, "spend compensation count");
        True(buyState.FindHolding(_fixture.Item.ItemId) == null, "spend exception committed holding");
        Equal(0, buyState.TradeHistory.Count, "spend exception wrote history");

        RunState sellState = _fixture.CreateTradeState(100);
        sellState.Holdings.Add(new Holding { ItemId = _fixture.Item.ItemId, Quantity = 1, TotalCost = 100 });
        FaultInjectingWallet sellWallet = new FaultInjectingWallet(0, false, true);
        TradingService sellTrading = _fixture.CreateTrading(sellState, sellWallet);
        TradeResult sell = sellTrading.Execute(new TradeOrder(TradeAction.Sell, _fixture.Item.ItemId, 1));
        Equal(TradeError.WalletRejected, sell.Error, "deposit exception result");
        Equal(0L, sellWallet.Balance, "deposit exception compensation");
        Equal(1, sellWallet.DepositCalls, "deposit attempt count");
        Equal(1, sellWallet.SpendCalls, "deposit compensation count");
        Equal(1, sellState.FindHolding(_fixture.Item.ItemId).Quantity, "deposit exception changed holding");
        Equal(0, sellState.TradeHistory.Count, "deposit exception wrote history");
    }

    private void TestDebtCompensation()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.Debt = 100;
        FaultInjectingWallet wallet = new FaultInjectingWallet(100, true, false);
        ShortService shorts = new ShortService(_fixture.CreateTrading(state, wallet));
        long repaid;
        True(!shorts.TryRepayDebtFromCash(out repaid), "debt repayment exception reported success");
        Equal(0L, repaid, "debt repayment exception amount");
        Equal(100L, wallet.Balance, "debt repayment compensation");
        Equal(100L, state.Debt, "debt repayment exception changed debt");
        Equal(1, wallet.SpendCalls, "debt spend attempt count");
        Equal(1, wallet.DepositCalls, "debt spend compensation count");
    }

    private sealed class FaultInjectingWallet : IPlayerWallet
    {
        private bool _throwAfterSpend;
        private bool _throwAfterDeposit;

        public long Balance { get; private set; }
        public int SpendCalls { get; private set; }
        public int DepositCalls { get; private set; }

        public FaultInjectingWallet(long balance, bool throwAfterSpend, bool throwAfterDeposit)
        {
            Balance = balance;
            _throwAfterSpend = throwAfterSpend;
            _throwAfterDeposit = throwAfterDeposit;
        }

        public bool CanSpend(long amount)
        {
            return amount >= 0 && amount <= Balance;
        }

        public bool CanDeposit(long amount)
        {
            if (amount < 0)
                return false;
            try
            {
                return checked(Balance + amount) >= Balance;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public bool TrySpend(long amount)
        {
            SpendCalls++;
            if (!CanSpend(amount))
                return false;
            Balance -= amount;
            if (_throwAfterSpend)
            {
                _throwAfterSpend = false;
                throw new InvalidOperationException("Injected spend failure.");
            }
            return true;
        }

        public bool TryDeposit(long amount)
        {
            DepositCalls++;
            if (!CanDeposit(amount))
                return false;
            Balance += amount;
            if (_throwAfterDeposit)
            {
                _throwAfterDeposit = false;
                throw new InvalidOperationException("Injected deposit failure.");
            }
            return true;
        }
    }
}
