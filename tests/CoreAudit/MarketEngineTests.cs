using System.Collections.Generic;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class MarketEngineTests
{
    private readonly CoreTestFixture _fixture;

    public MarketEngineTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("annual-index-year-continuity", TestAnnualContinuity);
        runner.Run("ordinary-index-end-flat", TestIndexEndFlat);
        runner.Run("ordinary-stock-end-flat", TestStockEndFlat);
        runner.Run("delisting-terminal-freeze", TestDelisting);
    }

    private void TestAnnualContinuity()
    {
        RunState state = _fixture.CreateMarketState("index-up-a", "stock-flat", false);
        MarketEngine engine = new MarketEngine(state, _fixture.Repository, new[] { _fixture.Item });
        _fixture.Advance(engine, state, new MarketTime(1, 12, 3));
        double before = state.CurrentIndex;
        _fixture.Advance(engine, state, new MarketTime(2, 1, 1));
        AnnualIndexBinding binding = state.FindAnnualIndexBinding(2);
        True(binding != null, "year 2 binding missing");
        Near(before, binding.StartIndexValue, "year binding reset baseline");
        WeeklySecurity nextSecurity = _fixture.Repository.GetSecurity(binding.SecurityId);
        double yearReturn = nextSecurity.ComputeHfqClose(binding.StartRecordIndex + 1) /
            nextSecurity.ComputeHfqClose(binding.StartRecordIndex) - 1.0;
        Near(before * (1.0 + yearReturn), state.CurrentIndex, "year transition return continuity");
    }

    private void TestIndexEndFlat()
    {
        RunState state = _fixture.CreateMarketState("index-short", "stock-flat", false);
        MarketEngine engine = new MarketEngine(state, _fixture.Repository, new[] { _fixture.Item });
        _fixture.Advance(engine, state, new MarketTime(1, 4, 2));
        _fixture.Advance(engine, state, new MarketTime(1, 4, 3));
        double last = state.CurrentIndex;
        _fixture.Advance(engine, state, new MarketTime(1, 5, 1));
        _fixture.Advance(engine, state, new MarketTime(1, 5, 2));
        Near(last, state.CurrentIndex, "exhausted index moved");
        True(state.IndexHistory[state.IndexHistory.Count - 1].DataExhausted, "index exhaustion not marked");
    }

    private void TestStockEndFlat()
    {
        RunState state = _fixture.CreateMarketState("index-up-a", "stock-short", false);
        MarketEngine engine = new MarketEngine(state, _fixture.Repository, new[] { _fixture.Item });
        _fixture.Advance(engine, state, new MarketTime(1, 4, 2));
        _fixture.Advance(engine, state, new MarketTime(1, 4, 3));
        SecurityBinding binding = state.FindSecurityBinding(_fixture.Item.ItemId);
        double last = binding.LastPrice;
        _fixture.Advance(engine, state, new MarketTime(1, 5, 1));
        Near(last, binding.LastPrice, "exhausted stock followed index instead of staying flat");
        _fixture.Advance(engine, state, new MarketTime(1, 5, 2));
        Near(last, binding.LastPrice, "exhausted stock moved on a later week");
    }

    private void TestDelisting()
    {
        RunState state = _fixture.CreateMarketState("index-flat", "stock-delist", true);
        MarketEngine engine = new MarketEngine(state, _fixture.Repository, new[] { _fixture.Item });
        _fixture.Advance(engine, state, new MarketTime(1, 4, 2));
        _fixture.Advance(engine, state, new MarketTime(1, 4, 3));
        SecurityBinding binding = state.FindSecurityBinding(_fixture.Item.ItemId);
        True(binding.IsDelisted, "delisting flag ignored");
        Equal(2, binding.DelistedWeek, "delisting week");
        double last = binding.LastPrice;
        _fixture.Advance(engine, state, new MarketTime(1, 5, 1));
        Near(last, binding.LastPrice, "delisted price changed");

        FakeWallet wallet = new FakeWallet(1000);
        TradingService trading = _fixture.CreateTrading(state, wallet);
        TradeAction[] actions = { TradeAction.Buy, TradeAction.Sell, TradeAction.Borrow, TradeAction.Return };
        for (int i = 0; i < actions.Length; i++)
        {
            TradeResult result = trading.Execute(new TradeOrder(actions[i], _fixture.Item.ItemId, 1));
            Equal(TradeError.Delisted, result.Error, "delisted action " + actions[i]);
        }
        Equal(1000L, wallet.Balance, "delisted trade changed cash");
    }
}
