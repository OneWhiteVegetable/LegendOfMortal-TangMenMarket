using System;

internal static class Program
{
    private static int Main()
    {
        var runner = new TestRunner();
        using (var fixture = new CoreTestFixture())
        {
            new MarketClockTests().Register(runner);
            new MarketEngineTests(fixture).Register(runner);
            new ElasticityTests().Register(runner);
            new CapacityTests(fixture).Register(runner);
            new TradingTests(fixture).Register(runner);
            new WalletChangeTests(fixture).Register(runner);
            new SignedSilverTests(fixture).Register(runner);
            new ShortSettlementTests(fixture).Register(runner);
            new BorrowHorizonTests(fixture).Register(runner);
            new RunRandomTests().Register(runner);
            new PersistenceTests(fixture).Register(runner);
            new BusinessPresentationTests(fixture).Register(runner);
            new ProfitHistoryTests().Register(runner);
            new WeeklyChartDataTests().Register(runner);
            new RealRuntimePriceProgressionTests().Register(runner);
            new RevealedHistoryTests(fixture).Register(runner);
            new RankingTotalListTests().Register(runner);
            new CartCapacityTests(fixture).Register(runner);
            new InventoryExperienceTests().Register(runner);
            new WeeklyChartExperienceTests().Register(runner);
            new UiMotionFoundationTests().Register(runner);
        }
        return runner.Complete();
    }
}
