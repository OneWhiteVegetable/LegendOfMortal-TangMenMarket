#if XINGSHANG_CHART_AUDIT
using System;
using System.Collections.Generic;
using UnityEngine;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Shared;

internal sealed class WeeklyChartExperienceTests
{
#if XINGSHANG_CHART_EXPERIENCE_STANDALONE
    public static int Main()
    {
        var runner = new TestRunner();
        new WeeklyChartExperienceTests().Register(runner);
        return runner.Complete();
    }
#endif

    public void Register(TestRunner runner)
    {
        runner.Run("chart-experience-history-grows-from-one-real-point", HistoryGrowsFromOneRealPoint);
        runner.Run("asset-return-brush-uses-level-and-splits-only-at-zero", SignedReturnBrush);
        runner.Run("chart-experience-continuous-history-preserves-custom-view", ContinuousHistoryPreservesCustomView);
        runner.Run("chart-experience-range-reset-pan-and-wheel", RangeResetPanAndWheel);
        runner.Run("chart-experience-price-zoom-and-clamps", PriceZoomAndClamps);
        runner.Run("chart-experience-latest-quote-follows-appended-close", LatestQuoteFollowsAppendedClose);
        runner.Run("chart-experience-close-only-and-single-point", CloseOnlyAndSinglePoint);
        runner.Run("chart-experience-index-total-and-appeared-constituents", IndexTotalAndAppearedConstituents);
    }

    private static void HistoryGrowsFromOneRealPoint()
    {
        WeeklyChartViewport one = WeeklyChartData.LoadViewport(1, 0f, 0, 1, false, 26, 26, false);
        AuditAssert.Equal(1, one.Count, "single initial close should render one point");
        WeeklyChartViewport two = WeeklyChartData.LoadViewport(one.Count, one.Start, 1, 2, true, 26, 26, false);
        AuditAssert.Equal(2, two.Count, "second real close remained stuck in a one-point viewport");
        WeeklyChartViewport three = WeeklyChartData.LoadViewport(two.Count, two.Start, 2, 3, true, 26, 26, false);
        AuditAssert.Equal(3, three.Count, "third real close did not expand toward the selected range");
        WeeklyChartViewport year = WeeklyChartData.LoadViewport(one.Count, one.Start, 1, 60, true, 26, 52, false);
        AuditAssert.Equal(52, year.Count, "short history did not grow into the selected year range");

        WeeklyChartViewport customOne = WeeklyChartData.LoadViewport(one.Count, one.Start, 1, 2, true, 26, 1, true);
        AuditAssert.Equal(1, customOne.Count, "an explicit one-point wheel range was not preserved");

        using (var fixture = new CoreTestFixture())
        {
            RunState state = fixture.CreateMarketState("index-up-a", "stock-flat", false);
            var engine = new MarketEngine(state, fixture.Repository, new[] { fixture.Item });
            AuditAssert.Equal(1, state.PriceHistory.Count, "real engine initial history");
            fixture.Advance(engine, state, new MarketTime(1, 4, 2));
            List<WeeklyPoint> visible = WeeklyChartData.VisibleThrough(
                state.PriceHistory,
                state.Clock.MarketWeekIndex);
            AuditAssert.Equal(2, visible.Count, "real engine second close was filtered out");
            AuditAssert.Near(110.0, visible[visible.Count - 1].Price, "real engine latest close");
            AuditAssert.Near(0.1, MarketQuery.WeeklyReturn(visible), "real engine weekly return");
            var segments = WeeklyChartSegmentBuilder.Build(visible);
            AuditAssert.Equal(1, segments.Count, "real engine second close did not create a segment");
            AuditAssert.Equal(WeeklyChartDirection.Rising, segments[0].Direction, "real engine segment direction");

            state.PriceHistory.Add(Point(fixture.Item.ItemId, 2, 1000000000000.0, 999.0));
            List<WeeklyPoint> closeOnly = WeeklyChartData.VisibleThrough(
                state.PriceHistory,
                state.Clock.MarketWeekIndex);
            AuditAssert.Equal(2, closeOnly.Count, "future poison entered the real engine chart");
            AuditAssert.Near(110.0, closeOnly[closeOnly.Count - 1].Price, "future poison changed the real latest close");
        }
    }

    private static void ContinuousHistoryPreservesCustomView()
    {
        WeeklyChartViewport initial = WeeklyChartData.LoadViewport(1, 0f, 0, 78, false, 26, 26, false);
        AuditAssert.Equal(26, initial.Count, "initial detail range");
        AuditAssert.Near(52f, initial.Start, "initial detail end");

        WeeklyChartViewport year = WeeklyChartData.ChangeRange(initial.Start, initial.Count, 78, 52, null);
        float panned = WeeklyChartData.Pan(year.Start, year.Count, 78, 100f, 500f);
        AuditAssert.Near(15.8f, panned, "drag did not move the viewport");

        float factor = WeeklyChartData.ScrollRangeFactor(1f);
        int wheelCount = (int)Math.Round(year.Count * factor);
        WeeklyChartViewport wheeled = WeeklyChartData.ChangeRange(panned, year.Count, 78, wheelCount, 0.75f);
        AuditAssert.Equal(46, wheeled.Count, "wheel range did not change");
        AuditAssert.Near(20.3f, wheeled.Start, "wheel anchor drifted");

        WeeklyChartViewport appended = WeeklyChartData.LoadViewport(
            wheeled.Count,
            wheeled.Start,
            78,
            79,
            true,
            26,
            52,
            true);
        AuditAssert.Equal(wheeled.Count, appended.Count, "new week reset wheel range");
        AuditAssert.Near(wheeled.Start, appended.Start, "new week reset drag position");

        WeeklyChartViewport switchedSeries = WeeklyChartData.LoadViewport(
            appended.Count,
            appended.Start,
            79,
            79,
            false,
            26,
            26,
            false);
        AuditAssert.Equal(26, switchedSeries.Count, "series switch retained the old series range");
        AuditAssert.Near(53f, switchedSeries.Start, "series switch did not use the latest close");

        WeeklyChartViewport followedEnd = WeeklyChartData.LoadViewport(26, 52f, 78, 79, true, 26, 26, false);
        AuditAssert.Equal(26, followedEnd.Count, "end-following range changed");
        AuditAssert.Near(53f, followedEnd.Start, "end-following viewport did not advance");

        WeeklyChartViewport reset = WeeklyChartData.ResetViewport(79, 26);
        AuditAssert.Equal(26, reset.Count, "reset range");
        AuditAssert.Near(53f, reset.Start, "reset did not return to the latest close");
    }

    private static void RangeResetPanAndWheel()
    {
        WeeklyChartViewport all = WeeklyChartData.ChangeRange(52f, 26, 78, int.MaxValue, null);
        AuditAssert.Equal(78, all.Count, "all range did not include every close");
        AuditAssert.Near(0f, all.Start, "all range did not start at the first close");

        WeeklyChartViewport month = WeeklyChartData.ChangeRange(all.Start, all.Count, 78, 12, null);
        AuditAssert.Equal(12, month.Count, "month range");
        AuditAssert.Near(66f, month.Start, "month range lost the end anchor");

        WeeklyChartViewport zoomOut = WeeklyChartData.ChangeRange(20f, 20, 78, 2000, 0.5f);
        AuditAssert.Equal(78, zoomOut.Count, "zoom-out exceeded available history");
        AuditAssert.Near(0f, zoomOut.Start, "zoom-out exceeded the first close");
        WeeklyChartViewport zoomIn = WeeklyChartData.ChangeRange(20f, 20, 78, 0, 0.5f);
        AuditAssert.Equal(1, zoomIn.Count, "zoom-in produced an invalid point count");

        AuditAssert.Near(0f, WeeklyChartData.Pan(-10f, 26, 78, 1000f, 500f), "pan crossed the first close");
        AuditAssert.Near(52f, WeeklyChartData.Pan(50f, 26, 78, -1000f, 500f), "pan crossed the latest close");
        AuditAssert.Near(7f, WeeklyChartData.Pan(7f, 26, 78, 100f, 0f), "zero-width plot changed the view");
    }

    private static void PriceZoomAndClamps()
    {
        float factor;
        float center;
        WeeklyChartData.ChangePriceZoom(1f, 0.5f, 2f, 0.75f, out factor, out center);
        AuditAssert.Near(2f, factor, "price wheel factor");
        AuditAssert.Near(0.625f, center, "price wheel anchor");
        AuditAssert.True(WeeklyChartData.HasCustomPriceZoom(factor, center), "price zoom was not observable");

        WeeklyChartData.ChangePriceZoom(factor, center, 1000f, 0.5f, out factor, out center);
        AuditAssert.Near(8f, factor, "price zoom maximum");
        WeeklyChartData.ChangePriceZoom(factor, center, 0.001f, 0.5f, out factor, out center);
        AuditAssert.Near(0.6f, factor, "price zoom minimum");
        WeeklyChartData.ChangePriceZoom(1f, 0.5f, 1f, 0.5f, out factor, out center);
        AuditAssert.True(!WeeklyChartData.HasCustomPriceZoom(factor, center), "reset price zoom remained custom");
    }

    private static void LatestQuoteFollowsAppendedClose()
    {
        var history = new List<WeeklyPoint>
        {
            Point("01", 0, 100.0, 0.0),
            Point("01", 1, 105.0, 0.05)
        };
        AuditAssert.Near(105.0, history[history.Count - 1].Price, "initial latest close");
        AuditAssert.Near(0.05, MarketQuery.WeeklyReturn(history), "initial weekly return");
        AuditAssert.Equal(1, WeeklyChartSegmentBuilder.Build(history).Count, "initial segment count");

        history.Add(Point("01", 2, 96.0, -0.04));
        var segments = WeeklyChartSegmentBuilder.Build(history);
        AuditAssert.Near(96.0, history[history.Count - 1].Price, "appended close did not become current");
        AuditAssert.Near(-0.04, MarketQuery.WeeklyReturn(history), "weekly return remained at its initial value");
        AuditAssert.Equal(2, segments.Count, "appended close did not create a segment");
        AuditAssert.Equal(WeeklyChartDirection.Falling, segments[segments.Count - 1].Direction, "latest segment direction");
        AuditAssert.Near(96.0, segments[segments.Count - 1].EndPrice, "latest segment close");
    }

    private static void CloseOnlyAndSinglePoint()
    {
        var source = new List<WeeklyPoint>
        {
            Point("01", 0, 100.0, 0.0),
            Point("01", 1, 110.0, 0.1),
            Point("01", 2, 1000000000000.0, 999.0),
            Point("01", 10, -1000000000000.0, -999.0)
        };
        List<WeeklyPoint> visible = WeeklyChartData.VisibleThrough(source, 1);
        AuditAssert.Equal(2, visible.Count, "future close entered the visible series");
        AuditAssert.Near(110.0, visible[visible.Count - 1].Price, "latest visible close");
        AuditAssert.Equal(4, source.Count, "close-only filter mutated source history");

        var single = new List<WeeklyPoint> { Point("01", 0, 100.0, 0.0) };
        AuditAssert.Equal(0, WeeklyChartSegmentBuilder.Build(single).Count, "single close invented a historical segment");
        WeeklyChartBrushLine line = WeeklyChartBrushGeometry.BuildLine(
            single,
            0,
            0,
            0f,
            1,
            new Rect(0f, 0f, 300f, 100f),
            90.0,
            110.0,
            WeeklyChartMode.Detail,
            0,
            "audit|single",
            4.2f,
            new Color32(182, 64, 54, 255),
            new Color32(63, 107, 88, 255),
            new Color32(116, 103, 84, 255),
            new Color32(116, 103, 84, 255));
        AuditAssert.Equal(0, line.Segments.Count, "single close invented brush history");
        AuditAssert.Equal(1, line.Isolated.Count, "single close was not drawn as one observed point");
    }

    private static void SignedReturnBrush()
    {
        var red = new Color32(182,64,54,255);
        var green = new Color32(63,107,88,255);
        var gray = new Color32(103,106,101,255);
        var values = new[] { 8d, 3d, -9d, -2d, 0d, 0d, 6d };
        var points = new List<WeeklyPoint>();
        for (int i=0; i<values.Length; i++) points.Add(Point("returnRate",i,values[i],0));
        var area = new Rect(0,0,600,200);
        WeeklyChartBrushLine result = WeeklyChartBrushGeometry.BuildLine(points,0,6,0,7,area,-10,10,
            WeeklyChartMode.Assets,1,"profit-rate",1.75f,red,green,gray,green,true);
        AuditAssert.Equal(7,result.Segments.Count,"one crossing subdivides one paint span");
        var expected = new[]{red,red,green,green,green,gray,red};
        for (int i=0; i<expected.Length; i++)
        {
            AuditAssert.Equal(expected[i],result.Segments[i].StartColor,"correct ink for sign at start");
            AuditAssert.Equal(expected[i],result.Segments[i].EndColor,"correct ink for sign at end");
            if (i>0) AuditAssert.True(ReferenceEquals(result.Segments[i-1].To,result.Segments[i].From),
                "brush geometry must cover every consecutive observation without a dash gap");
        }
        WeeklyChartBrushNode cut = result.Segments[1].To;
        AuditAssert.Near(125d,cut.Position.x,"3 -> -9 crosses at one quarter of the week");
        AuditAssert.Near(100d,cut.Position.y,"the paint seam is on zero");
        AuditAssert.Equal(-1,cut.Index,"a calculated split cannot become an observation");
        AuditAssert.Equal(2,result.ObservedZeros.Count,"only the two observed zeros get gray point marks");
        points.Add(Point("returnRate",7,-10000d,0));
        WeeklyChartBrushLine same = WeeklyChartBrushGeometry.BuildLine(points,0,6,0,7,area,-10,10,
            WeeklyChartMode.Assets,1,"profit-rate",1.75f,red,green,gray,green,true);
        AuditAssert.Equal(result.Segments.Count,same.Segments.Count,"future poison leaked into visible ink");
        for (int i=0;i<expected.Length;i++)
            AuditAssert.Equal(result.Segments[i].EndColor,same.Segments[i].EndColor,"future poison changed earlier ink");
    }

    private static void IndexTotalAndAppearedConstituents()
    {
        var index = new List<WeeklyPoint>
        {
            Point(string.Empty, 0, 1000.0, 0.0),
            Point(string.Empty, 1, 1120.0, 0.12),
            Point(string.Empty, 2, 1097.6, -0.02)
        };
        AuditAssert.Near(0.0976, MarketQuery.TotalReturn(index), "index cumulative return ignored the 1000-point baseline");
        AuditAssert.Near(-0.02, MarketQuery.WeeklyReturn(index), "index weekly return");

        var catalog = new ItemCatalog();
        catalog.Items.Add(new ItemDefinition { ItemId = "01", Name = "已出现甲" });
        catalog.Items.Add(new ItemDefinition { ItemId = "02", Name = "未出现" });
        catalog.Items.Add(new ItemDefinition { ItemId = "03", Name = "已出现乙" });
        var state = new RunState();
        state.AppearedItemIds.Add("03");
        state.AppearedItemIds.Add("01");
        state.AppearedItemIds.Add("03");
        state.AppearedItemIds.Add("missing");
        List<ItemDefinition> constituents = MarketQuery.AppearedItems(state, catalog);
        AuditAssert.Equal(2, constituents.Count, "constituents included unappeared or duplicate items");
        AuditAssert.Equal("03", constituents[0].ItemId, "constituent navigation order changed");
        AuditAssert.Equal("01", constituents[1].ItemId, "second constituent mismatch");
    }

    private static WeeklyPoint Point(string itemId, int week, double price, double weeklyReturn)
    {
        return new WeeklyPoint
        {
            ItemId = itemId,
            MarketWeekIndex = week,
            GameYear = 1,
            GameMonth = 4,
            GameRound = 1,
            Price = price,
            Return = weeklyReturn,
            Observed = true
        };
    }
}
#endif
