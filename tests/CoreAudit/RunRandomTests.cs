using static AuditAssert;
using MarketRunRandom = XingShangMod.Market.RunRandom;
using ProgressionRunRandom = XingShangMod.Progression.RunRandom;

internal sealed class RunRandomTests
{
    public void Register(TestRunner runner)
    {
        runner.Run("market-run-random-golden-vectors", TestMarketVectors);
        runner.Run("progression-run-random-golden-vectors", TestProgressionVectors);
    }

    private static void TestMarketVectors()
    {
        MarketRunRandom random = new MarketRunRandom(123456789);
        Equal(15587530974076623600UL, random.NextUInt64("select", "01_item"), "market select vector");
        Equal(9259053873979010723UL, random.NextUInt64("news", "01_item:7"), "market news vector");
        Equal(14478959133331378367UL, random.NextUInt64("select", "商品甲"), "market UTF-8 vector");
        Equal(0, random.NextIndex("select", "01_item", 6), "market index vector");
        Equal(5, random.NextIndex("news", "01_item:7", 6), "market second index vector");
        Equal("f", random.Choose("news", "01_item:7", new[] { "a", "b", "c", "d", "e", "f" }), "market choose vector");
        Equal(15358464429278101314UL, new MarketRunRandom(0).NextUInt64("select", "01_item"), "market zero seed vector");
        Equal(10056964792675440847UL, new MarketRunRandom(-1).NextUInt64("annual-index", "2"), "market negative seed vector");
    }

    private static void TestProgressionVectors()
    {
        Equal(1175372872u, ProgressionRunRandom.StableHash(123456789, "page", "0", "01_item"), "progression page vector");
        Equal(3302108615u, ProgressionRunRandom.StableHash(123456789, "消息", "7"), "progression UTF-16 vector");
        Equal(511044844u, ProgressionRunRandom.StableHash(0, new string[0]), "progression empty key vector");
        Equal(2422639857u, ProgressionRunRandom.StableHash(-1, null), "progression null key vector");
        Equal(3702578424u, ProgressionRunRandom.StableHash(42, "a", null, "b"), "progression null part vector");
        Equal(4, ProgressionRunRandom.NextIndex(123456789, 6, "page", "0", "01_item"), "progression index vector");
        Equal(-1, ProgressionRunRandom.NextInt(123456789, -3, 4, "page", "0", "01_item"), "progression range vector");
    }
}
