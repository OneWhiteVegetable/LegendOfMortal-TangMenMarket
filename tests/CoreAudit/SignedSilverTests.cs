using System.Collections.Generic;
using System.IO;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class SignedSilverTests
{
    private readonly CoreTestFixture _fixture;

    internal SignedSilverTests(CoreTestFixture fixture) { _fixture = fixture; }

    internal void Register(TestRunner runner)
    {
        runner.Run("signed-wallet-negative-net-zero-credit-and-voluntary-debit", NegativeBasket);
        runner.Run("signed-wallet-migration-uses-paired-raw-native-balance", MigrateRawCash);
        runner.Run("signed-wallet-extreme-residual-clears-as-income-arrives", ResidualIncome);
        runner.Run("signed-wallet-assetpoint-checksum-detects-altered-profit", CheckAssetChecksum);
    }

    private void NegativeBasket()
    {
        const string other = "second";
        string first = _fixture.Item.ItemId;
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = first, Quantity = 2, TotalCost = 200 });
        state.PriceHistory.Add(new WeeklyPoint { ItemId = other, MarketWeekIndex = 0, Price = 100, Observed = true });
        var definitions = new Dictionary<string, ItemDefinition>
        {
            { first, _fixture.Item }, { other, new ItemDefinition { ItemId = other, Weight = 1, ReferencePrice = 100 } }
        };
        var wallet = new FakeWallet(-200);
        var trade = new TradingService(state, wallet, definitions, new CapacityService(() => new CapacityStats(30, 10, 10)));
        var zero = new[] { new TradeOrder(TradeAction.Sell, first, 1), new TradeOrder(TradeAction.Buy, other, 1) };
        True(trade.Preview(zero).Success && trade.Execute(zero).Success, "negative-wallet net-neutral basket");
        Equal(-200L, wallet.Balance, "neutral basket changed cash");
        True(trade.Execute(new TradeOrder(TradeAction.Sell, first, 1)).Success, "credit while overdrawn");
        Equal(-100L, wallet.Balance, "credit should offset the deficit");
        TradeResult declined = trade.Execute(new TradeOrder(TradeAction.Buy, first, 1));
        Equal(TradeError.InsufficientCash, declined.Error, "no voluntary deficit spending");
        Equal(0L, declined.AvailableAmount, "negative money is not available for spending");
        wallet.Set(MortalMoneyLimit.Maximum);
        string unchanged = JsonCodec.Serialize(state, false);
        Equal(TradeError.CashOverflow, trade.Execute(new TradeOrder(TradeAction.Sell, other, 1)).Error, "upper bound");
        Equal(unchanged, JsonCodec.Serialize(state, false), "overflow changed a position");
        Equal((long)MortalMoneyLimit.Maximum, wallet.Balance, "overflow changed actual cash");
    }

    private void MigrateRawCash()
    {
        var wallet = new FakeWallet(MortalMoneyLimit.Maximum);
        var source = new RunState { Debt = 30000000 };
        for (int i = 0; i < 2; i++)
        {
            RunState restored = RunStateCloner.Clone(source);
            LegacySilverDebt.Restore(restored, wallet, 120000000);
            Equal(90000000L, wallet.Balance, "must fold the full raw save before capping");
            Equal(0L, restored.Debt, "legacy debt already in host");
            Equal(30000000L, source.Debt, "original save version was changed");
        }
        var ordinary = new RunState { Debt = 4941 };
        wallet.Set(0);
        LegacySilverDebt.Restore(ordinary, wallet, 0);
        Equal(-4941L, wallet.Balance, "negative old slot migration");
        Equal(0L, ordinary.Debt, "negative old slot debt counted twice");
    }

    private void ResidualIncome()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.Debt = 2147483655L;
        var wallet = new FakeWallet(0);
        LegacySilverDebt.Restore(state, wallet, 0);
        Equal((long)int.MinValue, wallet.Balance, "signed cash lower bound");
        Equal(7L, state.Debt, "only unrepresentable seven wen remain");
        var shorts = new ShortService(_fixture.CreateTrading(state, wallet));
        True(wallet.TryDeposit(5) && shorts.TryRepayDebtFromCash(out long repaid) && repaid == 5, "first income");
        Equal(2L, state.Debt, "first income lost or counted twice");
        True(wallet.TryDeposit(5) && shorts.TryRepayDebtFromCash(out repaid) && repaid == 2, "second income");
        Equal((long)int.MinValue + 3, wallet.Balance, "second income remainder");
        Equal(0L, state.Debt, "residual extinguished");
    }

    private void CheckAssetChecksum()
    {
        var point = new AssetPoint { Cash = 15, NetAssets = 15 };
        string legacy = JsonCodec.Serialize(point, false);
        Equal("{\"Cash\":15,\"Debt\":0,\"GameMonth\":0,\"GameRound\":0,\"GameYear\":0,\"LongValue\":0,\"MarketWeekIndex\":0,\"NetAssets\":15,\"ShortLiability\":0}",
            legacy, "old asset point's signed JSON bytes changed");
        UnityEngine.Application.persistentDataPath = Path.Combine(_fixture.Root, "new-profit-integrity");
        var store = new SidecarStore("signed");
        var state = new RunState { RunId = "signed-integrity" };
        point.ProfitRecordVersion = 1;
        point.HasProfitMetrics = true;
        point.TotalProfit = -15;
        state.AssetHistory.Add(point);
        True(store.Persist("test", "slot", 99, "test/Save_slot.dat", state), store.LastError);
        string file = store.GetSidecarPath("test", "slot", 99);
        string saved = File.ReadAllText(file);
        string tampered = saved.Replace("\"TotalProfit\": -15", "\"TotalProfit\": -14");
        True(tampered != saved, "expected signed profit field missing");
        File.WriteAllText(file, tampered);
        True(!store.TryLoadDisk("test", "slot", 99, out _) && store.LastLoadStatus == SidecarLoadStatus.Corrupt,
            "changed new profit field accepted with original checksum");
    }
}
