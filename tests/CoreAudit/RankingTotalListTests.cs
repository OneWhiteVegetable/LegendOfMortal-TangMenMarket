using System;
using System.Collections.Generic;
using System.IO;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;
using static AuditAssert;

internal sealed class RankingTotalListTests
{
    public void Register(TestRunner runner)
    {
        runner.Run("ranking-total-list-has-no-goods-pagination", TestSingleUnpagedList);
        runner.Run("ranking-fixed-header-scroll-and-keyboard-structure", TestScrollStructure);
        runner.Run("ranking-order-is-continuous-in-both-directions", TestContinuousOrdering);
        runner.Run("ranking-source-includes-only-real-appeared-items", TestOnlyAppearedRealStateEntries);
        runner.Run("ranking-detail-roundtrip-restores-ranking-context", TestDetailRoundtrip);
        runner.Run("ranking-does-not-change-six-card-market-pagination", TestMarketPaginationUnchanged);
    }

    private static void TestSingleUnpagedList()
    {
        string panel = ReadSource("src/UI/Market/MarketRankingPanel.cs");
        string window = ReadSource("src/UI/Market/MarketWindow.cs");
        True(panel.Contains("MarketQuery.AppearedItems(state, catalog)"), "ranking must use real appeared items");
        True(!panel.Contains("catalog.Items"), "ranking must not fill rows from the full catalog");
        True(!panel.Contains("pageIndex") && !panel.Contains("pageSize"), "ranking still accepts goods-page slicing");
        True(!window.Contains("_rankingPage"), "market window still owns a ranking page index");
        True(panel.Contains("BindRows(entries)"), "ranking must bind the complete sorted entry list");
        True(panel.Contains("row.Bind(entries[i], i + 1)"), "ranking rows must use continuous one-based ranks");
        True(panel.Contains("Dictionary<string, RankingRowView>"), "ranking rows must be reusable by item ID");
        True(panel.Contains("SetSiblingIndex"), "sorting should reuse and reorder row views");
        True(!panel.Contains("new ItemDefinition"), "ranking must not synthesize catalog entries");
    }

    private static void TestScrollStructure()
    {
        string panel = ReadSource("src/UI/Market/MarketRankingPanel.cs");
        string chrome = ReadSource("src/UI/Market/RankingChrome.cs");
        string row = ReadSource("src/UI/Market/RankingRowView.cs");
        True(panel.Contains("ScrollRect") && panel.Contains("RectMask2D"), "ranking needs a clipped scroll viewport");
        True(panel.Contains("RankingLayout.Rect(\"Content\", viewport)"), "rows must live in scroll content");
        True(panel.Contains("new RankingChrome(services, _root"), "fixed chrome must be outside the scroll viewport");
        True(chrome.Contains("RankingLayout.Rect(\"RankingTableHead\", content)"), "table header must belong to fixed chrome");
        True(panel.Contains("ContentSizeFitter"), "scroll content must grow with every appeared item");
        string[] keys = { "UpArrow", "DownArrow", "PageUp", "PageDown", "Home", "End" };
        for (int i = 0; i < keys.Length; i++)
            True(panel.Contains("KeyCode." + keys[i]), "missing keyboard scroll key " + keys[i]);
        True(row.Contains("SparkPointCount = 20"), "ranking sparkline must retain twenty points");
        True(panel.Contains("DetailRequested") && panel.Contains("callback(row.ItemId)"), "row detail navigation was disconnected");
    }

    private static void TestContinuousOrdering()
    {
        List<double> returns = new List<double> { 0.08, -0.03, 0.21, 0.0, 0.11, -0.09, 0.04, 0.16, -0.01 };
        List<int> order = new List<int>();
        for (int i = 0; i < returns.Count; i++)
            order.Add(i);
        order.Sort(delegate(int left, int right)
        {
            return RankingOrdering.Compare(true, returns[left], left, true, returns[right], right, false);
        });
        Equal(returns.Count, order.Count, "sorting must retain every entry");
        Equal(2, order[0], "descending sort must start with the largest return");
        Equal(5, order[order.Count - 1], "descending sort must include the smallest return");
        for (int i = 1; i < order.Count; i++)
            True(returns[order[i - 1]] >= returns[order[i]], "descending order was truncated or reversed");
        order.Sort(delegate(int left, int right)
        {
            return RankingOrdering.Compare(true, returns[left], left, true, returns[right], right, true);
        });
        Equal(5, order[0], "ascending sort must start with the smallest return");
        Equal(2, order[order.Count - 1], "ascending sort must include the largest return");
    }

    private static void TestOnlyAppearedRealStateEntries()
    {
        ItemCatalog catalog = new ItemCatalog();
        for (int i = 0; i < 12; i++)
        {
            catalog.Items.Add(new ItemDefinition
            {
                ItemId = "item-" + i,
                Name = "货物 " + i
            });
        }
        int[] appeared = { 8, 0, 5, 2, 6, 1, 10, 3, 7 };
        RunState state = new RunState();
        for (int i = 0; i < appeared.Length; i++)
            state.AppearedItemIds.Add("item-" + appeared[i]);
        state.AppearedItemIds.Add("missing-item");
        state.AppearedItemIds.Add("item-8");

        List<ItemDefinition> items = MarketQuery.AppearedItems(state, catalog);
        Equal(appeared.Length, items.Count, "appeared items must not be truncated to six or filled from catalog");
        for (int i = 0; i < appeared.Length; i++)
            Equal("item-" + appeared[i], items[i].ItemId, "appearance order or filtering changed");
    }

    private static void TestDetailRoundtrip()
    {
        string navigation = ReadSource("src/UI/ModUserInterface.cs");
        string panel = ReadSource("src/UI/Market/MarketRankingPanel.cs");
        True(navigation.Contains("_navigation.Add(NavigationFrame.Market(_market.IsRanking))"), "detail navigation must capture ranking mode");
        True(navigation.Contains("_market.Hide();") && navigation.Contains("_detail.Show(itemId);"), "detail must open from the retained market instance");
        True(navigation.Contains("if (frame.ShowRanking)") && navigation.Contains("_market.ShowRanking();"), "back navigation must restore ranking mode");
        int toggleSort = panel.IndexOf("internal void ToggleSort()", StringComparison.Ordinal);
        True(toggleSort > 0, "sort method missing");
        True(!panel.Substring(0, toggleSort).Contains("ScrollToTop();"), "ordinary detail return must not reset ranking scroll");
        True(panel.Substring(toggleSort).Contains("ScrollToTop();"), "explicit sort toggle should return to the top");
    }

    private static void TestMarketPaginationUnchanged()
    {
        string window = ReadSource("src/UI/Market/MarketWindow.cs");
        True(window.Contains("_marketPage * 6 + i"), "market goods no longer use six-card pages");
        True(window.Contains("(items.Count + 5) / 6"), "market goods page count must remain six-based");
        True(window.Contains("pager.gameObject.SetActive(!_showRanking)"), "goods pager must be hidden only for ranking");
    }

    private static string ReadSource(string relativePath)
    {
        DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            string path = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Unable to locate source file", relativePath);
    }

#if RANKING_TOTAL_LIST_AUDIT
    public static int Main()
    {
        TestRunner runner = new TestRunner();
        new RankingTotalListTests().Register(runner);
        return runner.Complete();
    }
#endif
}
