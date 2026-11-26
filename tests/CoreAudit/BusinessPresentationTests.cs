#if PRESENTATION_AUDIT
using System;
using System.Collections.Generic;
using System.Globalization;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;
using static AuditAssert;

namespace XingShangMod.App
{
    public sealed class ModApplication
    {
        public ItemCatalog Items { get; set; }

        public IReadOnlyList<WeeklyPoint> GetItemHistory(string itemId)
        {
            return new WeeklyPoint[0];
        }

        public IReadOnlyList<WeeklyPoint> GetIndexHistory()
        {
            return new WeeklyPoint[0];
        }

        public IReadOnlyList<NewsRecord> GetNewsHistory()
        {
            return new NewsRecord[0];
        }
    }
}

namespace XingShangMod.Progression
{
    public sealed class ItemCatalog
    {
        public List<ItemDefinition> Items = new List<ItemDefinition>();

        public bool TryGetItem(string itemId, out ItemDefinition item)
        {
            item = null;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] != null && string.Equals(Items[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    item = Items[i];
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class NewsCatalog
    {
        public static int MapPeriodIndex(int gameYear, int gameMonth)
        {
            return gameMonth - 1;
        }

        public IReadOnlyList<NewsEntry> GetCandidates(string itemId, int periodIndex, int direction)
        {
            return new NewsEntry[0];
        }
    }

    public sealed class NewsEntry
    {
        public string PoolId = string.Empty;
        public string Text = string.Empty;
    }
}

namespace XingShangMod.UI.Shared
{
    internal static class UiFormatting
    {
        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static string Number(double value, int decimals)
        {
            return IsFinite(value)
                ? value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
                : "—";
        }
    }
}

internal sealed class BusinessPresentationTests
{
    private readonly CoreTestFixture _fixture;

    public BusinessPresentationTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("presentation-late-item-uses-appearance-baseline", TestAppearanceReturn);
        runner.Run("presentation-ranking-direction-missing-and-ties", TestRankingOrder);
        runner.Run("presentation-round-and-market-week-remain-distinct", TestMarketTime);
        runner.Run("presentation-short-keeps-original-and-net-prices", TestShortPrices);
        runner.Run("presentation-short-missing-original-is-not-inferred", TestMissingShortOriginal);
        runner.Run("presentation-short-projection-follows-real-trades", TestRealTradeShortProjection);
        runner.Run("presentation-portfolio-floating-and-weekly-revaluation", TestPortfolioProjection);
        runner.Run("presentation-portfolio-marks-incomplete-prices", TestIncompletePortfolio);
        runner.Run("presentation-delisting-notice-separates-position-results", TestDelistingNotice);
        runner.Run("presentation-delisting-notice-follows-system-settlement", TestRealDelistingNotice);
        runner.Run("presentation-capacity-is-shared-and-multi-slot", TestCapacityPresentation);
    }

    private static void TestAppearanceReturn()
    {
        List<WeeklyPoint> history = new List<WeeklyPoint>
        {
            Point("late", 12, 80),
            Point("late", 13, 100)
        };
        Near(0.25, MarketQuery.TotalReturn(history), "late item return must use its first visible week");
        Equal(12, MarketQuery.FirstPriceWeek(history), "legacy quote baseline week");
        Equal("市周 12 起", MarketQuery.ItemTotalReturnCaption(history), "legacy total-return caption");
        Equal(
            "自各货首条行情至今 · 累计涨跌",
            MarketQuery.RankingPeriodCaption,
            "ranking period caption");
        Near(0.0, MarketQuery.TotalReturn(history.GetRange(0, 1)), "single visible week return");
    }

    private static void TestRankingOrder()
    {
        double lateReturn = MarketQuery.TotalReturn(new List<WeeklyPoint>
        {
            Point("late", 12, 80),
            Point("late", 13, 100)
        });
        True(CompareRank(-0.1, 0, true, lateReturn, 1, true, false) > 0, "default ranking must be descending");
        True(CompareRank(-0.1, 0, true, lateReturn, 1, true, true) < 0, "ascending toggle did not reverse returns");
        True(CompareRank(lateReturn, 0, true, lateReturn, 1, true, false) < 0, "equal returns must retain appearance order");
        True(CompareRank(lateReturn, 1, true, lateReturn, 0, true, false) > 0, "equal-return appearance order was reversed");
        True(CompareRank(0, 0, false, -0.1, 1, true, false) > 0, "missing quote must be last");
        True(CompareRank(0, 0, false, -0.1, 1, true, true) > 0, "ascending moved missing quote above priced quote");
        True(CompareRank(double.NaN, 0, true, -0.1, 1, true, false) > 0, "non-finite return must be unavailable");
    }

    private static void TestMarketTime()
    {
        Equal("上旬", MarketQuery.GameRoundName(1), "round one name");
        Equal("中旬", MarketQuery.GameRoundName(2), "round two name");
        Equal("下旬", MarketQuery.GameRoundName(3), "round three name");
        Equal("2年5月下旬", MarketQuery.FormatGamePeriod(2, 5, 3), "game period format");
        string first = MarketQuery.FormatMarketTime(2, 5, 3, 17);
        string second = MarketQuery.FormatMarketTime(2, 5, 3, 18);
        Equal("2年5月下旬 · 市周 17", first, "full market timestamp");
        Equal("2年5月下旬 · 市周 18", second, "second week in same round");
        True(first != second, "same-round market weeks were collapsed");
        Equal("市周 17", MarketQuery.FormatMarketTime(0, 0, 0, 17), "invalid game period fallback");
        Equal(string.Empty, MarketQuery.FormatMarketTime(null), "null timestamp");
    }

    private static void TestShortPrices()
    {
        RunState state = State(5);
        state.ShortBatches.Add(Batch("a", "item", 3, 1, 291));
        state.ShortBatches.Add(Batch("b", "item", 2, 3, 388));
        state.TradeHistory.Add(Borrow("a", "item", 10, 1, 100, 970));
        state.TradeHistory.Add(Borrow("b", "item", 4, 3, 200, 776));
        state.TradeHistory.Add(Borrow("a", "other", 10, 1, 900, 8730));
        state.TradeHistory.Add(Borrow("a", "item", 10, 2, 900, 8730));
        state.TradeHistory.Add(new TradeRecord
        {
            ItemId = "item",
            Action = TradeActionNames.Return,
            BatchId = "a",
            MarketWeekIndex = 4,
            Quantity = 7,
            UnitPrice = 80,
            CashDelta = -560
        });

        ShortPriceProjection projection = MarketQuery.BuildShortPriceProjection(state, "item");
        Equal(5L, projection.Quantity, "remaining short quantity");
        Equal(679L, projection.NetProceeds, "remaining actual proceeds");
        Near(700, projection.OriginalValue, "remaining original transaction value");
        Near(140, projection.AverageOriginalPrice, "weighted original borrow price");
        Near(135.8, projection.AverageNetProceeds, "weighted actual net proceeds");
        True(projection.HasCompleteOriginalPrices, "valid borrow records marked incomplete");
        Equal("借价 140.00 文 · 97%实收 135.80 文", MarketQuery.ShortPriceText(projection), "short price text");
        Equal(5L, MarketQuery.ShortQuantity(state, "item"), "short quantity aggregate");
        Equal(679L, MarketQuery.ShortProceeds(state, "item"), "short proceeds aggregate");
    }

    private static void TestMissingShortOriginal()
    {
        RunState state = State(5);
        state.ShortBatches.Add(Batch("known", "item", 3, 1, 291));
        state.ShortBatches.Add(Batch("missing", "item", 2, 1, 194));
        state.TradeHistory.Add(Borrow("known", "item", 3, 1, 100, 291));
        ShortPriceProjection projection = MarketQuery.BuildShortPriceProjection(state, "item");
        True(!projection.HasCompleteOriginalPrices, "missing borrow record was inferred");
        Near(300, projection.OriginalValue, "known original value should remain available for diagnostics");
        Near(0, projection.AverageOriginalPrice, "partial original value must not become a complete average");
        Near(97, projection.AverageNetProceeds, "actual proceeds remain readable without borrow history");
        Equal("借价 — · 97%实收 97.00 文", MarketQuery.ShortPriceText(projection), "missing original price text");

        RunState legacy = State(1);
        legacy.ShortBatches.Add(Batch(string.Empty, "item", 1, 0, 97));
        legacy.TradeHistory.Add(Borrow(string.Empty, "item", 1, 0, 500, 485));
        True(
            !MarketQuery.BuildShortPriceProjection(legacy, "item").HasCompleteOriginalPrices,
            "empty batch ID must not guess an original transaction");
    }

    private void TestRealTradeShortProjection()
    {
        RunState state = _fixture.CreateTradeState(100);
        FakeWallet wallet = new FakeWallet(1000);
        ShortService shorts = new ShortService(_fixture.CreateTrading(state, wallet));
        TradeResult opened = shorts.Open(_fixture.Item.ItemId, 3);
        True(opened.Success, "real short open failed: " + opened.Message);
        ShortPriceProjection projection = MarketQuery.BuildShortPriceProjection(state, _fixture.Item.ItemId);
        Equal(3L, projection.Quantity, "real open remaining quantity");
        Equal(291L, projection.NetProceeds, "real open actual proceeds");
        Near(300, projection.OriginalValue, "real open original value");
        Near(100, projection.AverageOriginalPrice, "real open original unit price");
        Near(97, projection.AverageNetProceeds, "real open net unit proceeds");

        TradeResult repaid = shorts.Repay(_fixture.Item.ItemId, 1);
        True(repaid.Success, "real partial repayment failed: " + repaid.Message);
        projection = MarketQuery.BuildShortPriceProjection(state, _fixture.Item.ItemId);
        Equal(2L, projection.Quantity, "partial repayment remaining quantity");
        Equal(194L, projection.NetProceeds, "partial repayment actual proceeds");
        Near(200, projection.OriginalValue, "partial repayment original value");
        Near(100, projection.AverageOriginalPrice, "partial repayment original unit price");
        Near(97, projection.AverageNetProceeds, "partial repayment net unit proceeds");
    }

    private static void TestPortfolioProjection()
    {
        RunState state = State(2);
        state.Holdings.Add(new Holding { ItemId = "long", Quantity = 4, TotalCost = 400 });
        state.ShortBatches.Add(Batch("short", "short", 2, 0, 194));
        AddPrice(state, "long", 0, 90);
        AddPrice(state, "short", 0, 95);
        AddPrice(state, "long", 1, 100);
        AddPrice(state, "short", 1, 100);
        AddPrice(state, "long", 2, 120);
        AddPrice(state, "short", 2, 90);
        AddPrice(state, "long", 3, 10000);
        AddPrice(state, "short", 3, 1);
        state.AssetHistory.Add(new AssetPoint { MarketWeekIndex = 1, NetAssets = 90000 });
        state.AssetHistory.Add(new AssetPoint { MarketWeekIndex = 2, NetAssets = -90000 });
        state.TradeHistory.Add(new TradeRecord
        {
            ItemId = "long",
            Action = TradeActionNames.Sell,
            MarketWeekIndex = 2,
            Quantity = 1,
            UnitPrice = 120,
            CashDelta = 9999
        });

        PortfolioProjection projection = MarketQuery.BuildPortfolioProjection(state);
        True(projection.HasCompletePrices, "complete current prices marked missing");
        True(projection.HasCompleteWeeklyPrices, "complete weekly prices marked missing");
        Near(480, projection.LongValue, "long market value");
        Near(180, projection.ShortLiability, "short market liability");
        Near(94, projection.FloatingProfit, "floating profit must exclude realized and wallet changes");
        Near(94.0 / 594.0, projection.FloatingReturnRate, "floating return basis");
        Near(100, projection.WeeklyRevaluation, "weekly long and short revaluation");
        Near(600, projection.WeeklyBasis, "weekly previous-price basis");
        Near(1.0 / 6.0, projection.WeeklyReturnRate, "weekly revaluation rate");
    }

    private static void TestIncompletePortfolio()
    {
        RunState state = State(2);
        state.Holdings.Add(new Holding { ItemId = "new", Quantity = 2, TotalCost = 200 });
        AddPrice(state, "new", 2, 110);
        PortfolioProjection projection = MarketQuery.BuildPortfolioProjection(state);
        True(projection.HasCompletePrices, "current-only price should still value the position");
        True(!projection.HasCompleteWeeklyPrices, "missing previous week must not become zero revaluation");
        Equal("new", projection.MissingWeeklyPriceItemIds[0], "missing weekly item ID");
        Near(20, projection.FloatingProfit, "current-only floating value");

        state.Holdings.Add(new Holding { ItemId = "absent", Quantity = 1, TotalCost = 100 });
        projection = MarketQuery.BuildPortfolioProjection(state);
        True(!projection.HasCompletePrices, "missing current price was ignored");
        Equal("absent", projection.MissingPriceItemIds[0], "missing current item ID");
        Equal("absent", projection.MissingWeeklyPriceItemIds[0], "missing weekly IDs must be sorted");
        Equal("new", projection.MissingWeeklyPriceItemIds[1], "second missing weekly ID");

        PortfolioProjection empty = MarketQuery.BuildPortfolioProjection(State(0));
        True(empty.HasCompletePrices && empty.HasCompleteWeeklyPrices, "empty portfolio should have zero complete values");
        Near(0, empty.FloatingProfit, "empty floating value");
        Near(0, empty.WeeklyRevaluation, "empty weekly revaluation");
    }

    private static void TestDelistingNotice()
    {
        RunState state = State(7);
        state.Holdings.Add(new Holding { ItemId = "item", Quantity = 3, TotalCost = 300 });
        state.ShortBatches.Add(Batch("outstanding", "item", 1, 0, 97));
        state.TradeHistory.Add(new TradeRecord
        {
            ItemId = "item",
            Action = TradeActionNames.Expire,
            BatchId = "settled",
            MarketWeekIndex = 7,
            Quantity = 2,
            UnitPrice = 80,
            CashDelta = -100,
            DebtDelta = 60
        });
        state.TradeHistory.Add(new TradeRecord
        {
            ItemId = "item",
            Action = TradeActionNames.Expire,
            BatchId = "old",
            MarketWeekIndex = 6,
            Quantity = 9,
            UnitPrice = 70,
            CashDelta = -630
        });

        DelistingNotice notice = NewsService.BuildDelistingNotice(state, "item", 7);
        Equal(3L, notice.FrozenLongQuantity, "frozen long quantity");
        Equal(2L, notice.SettledShortQuantity, "settled short quantity");
        Equal(1L, notice.OutstandingShortQuantity, "outstanding short quantity");
        Near(160, notice.GrossRepayment, "gross delisting repayment");
        Equal(-100L, notice.SettlementCashDelta, "actual settlement cash delta");
        Equal(60L, notice.SettlementDebtDelta, "actual settlement debt delta");
        Equal(
            DelistingNoticeKind.LongFrozen | DelistingNoticeKind.ShortSettled | DelistingNoticeKind.ShortOutstanding,
            notice.Kind,
            "mixed delisting notice flags");

        state.ShortBatches.Clear();
        notice = NewsService.BuildDelistingNotice(state, "item", 7);
        Equal(
            DelistingNoticeKind.LongFrozen | DelistingNoticeKind.ShortSettled,
            notice.Kind,
            "fully settled delisting notice flags");

        DelistingNotice empty = NewsService.BuildDelistingNotice(State(7), "item", 7);
        Equal(DelistingNoticeKind.None, empty.Kind, "no-position delisting must not claim frozen or settled stock");
        Equal(0.04908, NewsService.ReturnThreshold, "news threshold must remain unchanged");
    }

    private void TestRealDelistingNotice()
    {
        RunState state = _fixture.CreateTradeState(100);
        state.ShortBatches.Add(Batch("delisted", _fixture.Item.ItemId, 2, 0, 194));
        _fixture.SetPrice(state, 1, 80);
        state.Clock.MarketWeekIndex = 2;
        SecurityBinding binding = state.FindSecurityBinding(_fixture.Item.ItemId);
        binding.IsDelisted = true;
        binding.DelistedWeek = 2;
        WeeklyPoint terminal = Point(_fixture.Item.ItemId, 2, 1);
        terminal.Delisted = true;
        state.PriceHistory.Add(terminal);
        FakeWallet wallet = new FakeWallet(300);
        ShortService shorts = new ShortService(_fixture.CreateTrading(state, wallet));
        IReadOnlyList<TradeResult> results = shorts.SettleDelistedBatches(_fixture.Item.ItemId);
        Equal(1, results.Count, "real delisting settlement result count");
        True(results[0].Success, "real delisting settlement failed: " + results[0].Message);

        DelistingNotice notice = NewsService.BuildDelistingNotice(state, _fixture.Item.ItemId, 2);
        Equal(DelistingNoticeKind.ShortSettled, notice.Kind, "real delisting settlement notice kind");
        Equal(2L, notice.SettledShortQuantity, "real delisting settled quantity");
        Equal(0L, notice.OutstandingShortQuantity, "real delisting outstanding quantity");
        Near(160, notice.GrossRepayment, "real delisting gross repayment");
        Equal(-160L, notice.SettlementCashDelta, "real delisting cash delta");
        Equal(0L, notice.SettlementDebtDelta, "real delisting debt delta");
    }

    private void TestCapacityPresentation()
    {
        ItemDefinition definition = new ItemDefinition { ItemId = _fixture.Item.ItemId, Weight = 20 };
        Dictionary<string, ItemDefinition> definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        definitions.Add(definition.ItemId, definition);
        RunState state = _fixture.CreateTradeState(100);
        state.Holdings.Add(new Holding { ItemId = definition.ItemId, Quantity = 4, TotalCost = 400 });
        state.ShortBatches.Add(Batch("capacity", definition.ItemId, 1, 0, 97));
        CapacitySnapshot snapshot = new CapacityService().Evaluate(
            state,
            definitions,
            new CapacityStats(30, 7, 8));
        ItemCapacity item = snapshot.FindItem(definition.ItemId);
        Equal(5, snapshot.TotalSlots, "total shared slots");
        Equal(2, snapshot.UsedSlots, "multi-slot shared usage");
        Equal(3, snapshot.AvailableSlots, "shared remaining slots");
        Equal(snapshot.TotalSlots, snapshot.UsedSlots + snapshot.AvailableSlots, "capacity display total");
        Equal(4L, item.LongQuantity, "item long quantity");
        Equal(1L, item.ShortQuantity, "item short quantity");
        Equal(3, item.StackLimit, "item stack limit");
        Equal(2, item.UsedSlots, "combined item quantity must share stack capacity");
        Equal(snapshot.UsedSlots, item.UsedSlots, "item slot sum must equal total usage");
    }

    private static RunState State(int currentWeek)
    {
        RunState state = new RunState();
        state.Clock.MarketWeekIndex = currentWeek;
        return state;
    }

    private static WeeklyPoint Point(string itemId, int week, double price)
    {
        return new WeeklyPoint
        {
            ItemId = itemId,
            MarketWeekIndex = week,
            GameYear = 1,
            GameMonth = 4,
            GameRound = 1,
            Price = price,
            Observed = true
        };
    }

    private static void AddPrice(RunState state, string itemId, int week, double price)
    {
        state.PriceHistory.Add(Point(itemId, week, price));
    }

    private static ShortBatch Batch(string batchId, string itemId, int quantity, int openWeek, long proceeds)
    {
        return new ShortBatch
        {
            BatchId = batchId,
            ItemId = itemId,
            Quantity = quantity,
            OpenWeek = openWeek,
            DueWeek = openWeek + 24,
            Proceeds = proceeds
        };
    }

    private static TradeRecord Borrow(
        string batchId,
        string itemId,
        int quantity,
        int week,
        double unitPrice,
        long cashDelta)
    {
        return new TradeRecord
        {
            BatchId = batchId,
            ItemId = itemId,
            Action = TradeActionNames.Borrow,
            Quantity = quantity,
            MarketWeekIndex = week,
            UnitPrice = unitPrice,
            CashDelta = cashDelta
        };
    }

    private static int CompareRank(
        double leftReturn,
        int leftAppearanceOrder,
        bool leftHasPrice,
        double rightReturn,
        int rightAppearanceOrder,
        bool rightHasPrice,
        bool ascending)
    {
        return RankingOrdering.Compare(
            leftHasPrice,
            leftReturn,
            leftAppearanceOrder,
            rightHasPrice,
            rightReturn,
            rightAppearanceOrder,
            ascending);
    }
}
#endif
