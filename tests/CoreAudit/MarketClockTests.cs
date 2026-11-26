using System;
using Mortal.Core;
using XingShangMod.Domain;
using XingShangMod.Market;
using static AuditAssert;

internal sealed class MarketClockTests
{
    public void Register(TestRunner runner)
    {
        runner.Run("clock-low-three-points", TestLowClock);
        runner.Run("clock-high-four-points", TestHighClock);
        runner.Run("clock-mixed-and-carry-reset", TestMixedClockAndCarry);
        runner.Run("clock-story-and-stage-values", TestStoryClockAndStages);
        runner.Run("clock-backward-zeroes-advancement", TestBackwardClock);
    }

    private static void TestLowClock()
    {
        MarketClockState state = new MarketClockState();
        MarketClock clock = new MarketClock(state);
        clock.Initialize(new MarketTime(1, 4, 1));
        Equal(1, clock.ApplyActionCost(3, new MarketTime(1, 4, 1)), "low round 1");
        Equal(0, clock.SynchronizeNaturalTime(new MarketTime(1, 4, 2)), "low natural 2");
        Equal(1, clock.ApplyActionCost(3, new MarketTime(1, 4, 2)), "low round 2");
        Equal(0, clock.SynchronizeNaturalTime(new MarketTime(1, 4, 3)), "low natural 3");
        Equal(1, clock.ApplyActionCost(3, new MarketTime(1, 4, 3)), "low round 3");
        Equal(1, clock.SynchronizeNaturalTime(new MarketTime(1, 5, 1)), "low month completion");
        Equal(4, state.MarketWeekIndex, "low month weeks");
        Equal(0, state.Carry, "low carry");
        Equal(0, state.WeeksThisGameMonth, "low new month counter");
    }

    private static void TestHighClock()
    {
        MarketClockState state = new MarketClockState();
        MarketClock clock = new MarketClock(state);
        clock.Initialize(new MarketTime(1, 4, 1));
        Equal(1, clock.ApplyActionCost(4, new MarketTime(1, 4, 1)), "high round 1");
        clock.SynchronizeNaturalTime(new MarketTime(1, 4, 2));
        Equal(1, clock.ApplyActionCost(4, new MarketTime(1, 4, 2)), "high round 2");
        clock.SynchronizeNaturalTime(new MarketTime(1, 4, 3));
        Equal(2, clock.ApplyActionCost(4, new MarketTime(1, 4, 3)), "high round 3");
        Equal(0, clock.SynchronizeNaturalTime(new MarketTime(1, 5, 1)), "high must not add week");
        Equal(4, state.MarketWeekIndex, "high month weeks");
        Equal(0, state.Carry, "high carry");
    }

    private static void TestMixedClockAndCarry()
    {
        MarketClockState state = new MarketClockState();
        MarketClock clock = new MarketClock(state);
        clock.Initialize(new MarketTime(1, 4, 1));
        clock.ApplyActionCost(3, new MarketTime(1, 4, 1));
        clock.SynchronizeNaturalTime(new MarketTime(1, 4, 2));
        clock.ApplyActionCost(4, new MarketTime(1, 4, 2));
        clock.SynchronizeNaturalTime(new MarketTime(1, 4, 3));
        clock.ApplyActionCost(4, new MarketTime(1, 4, 3));
        Equal(1, clock.SynchronizeNaturalTime(new MarketTime(1, 5, 1)), "mixed completion");
        Equal(4, state.MarketWeekIndex, "mixed total");
        Equal(0, clock.ApplyActionCost(1, new MarketTime(1, 5, 1)), "old carry leaked");

        MarketClockState carryState = new MarketClockState();
        MarketClock carryClock = new MarketClock(carryState);
        carryClock.Initialize(new MarketTime(1, 4, 1));
        Equal(0, carryClock.ApplyActionCost(2, new MarketTime(1, 4, 1)), "carry setup");
        Equal(4, carryClock.SynchronizeNaturalTime(new MarketTime(1, 5, 1)), "carry month completion");
        Equal(0, carryClock.ApplyActionCost(1, new MarketTime(1, 5, 1)), "carry was not cleared");
    }

    private static void TestStoryClockAndStages()
    {
        Equal(1, (int)MonthStageType.上旬, "upper stage enum");
        Equal(2, (int)MonthStageType.中旬, "middle stage enum");
        Equal(3, (int)MonthStageType.下旬, "lower stage enum");
        Equal(1, new MarketTime(1, 4, (int)MonthStageType.上旬).Round, "stage conversion");
        MarketClockState state = new MarketClockState();
        MarketClock clock = new MarketClock(state);
        clock.Initialize(new MarketTime(1, 4, 1));
        Equal(2, clock.AdvanceStory(new MarketTime(1, 4, 3)), "same-month story rounds");
        Equal(0, clock.AdvanceStory(new MarketTime(1, 4, 3)), "repeat story callback");
        Equal(7, clock.AdvanceStory(new MarketTime(1, 6, 2)), "cross-month story alignment");
        Equal(9, state.MarketWeekIndex, "story total");
        Equal(1, state.WeeksThisGameMonth, "story target month progress");
    }

    private static void TestBackwardClock()
    {
        Func<MarketClock, MarketTime, int>[] updates =
        {
            delegate(MarketClock clock, MarketTime time) { return clock.ApplyActionCost(3, time); },
            delegate(MarketClock clock, MarketTime time) { return clock.SynchronizeNaturalTime(time); },
            delegate(MarketClock clock, MarketTime time) { return clock.AdvanceStory(time); }
        };
        for (int i = 0; i < updates.Length; i++)
        {
            MarketClockState state = new MarketClockState();
            MarketClock clock = new MarketClock(state);
            MarketTime initial = new MarketTime(1, 4, 3);
            MarketTime earlier = new MarketTime(1, 4, 2);
            clock.Initialize(initial);
            clock.ApplyActionCost(2, initial);
            state.MarketWeekIndex = 5;
            state.WeeksThisGameMonth = 3;
            Equal(0, updates[i](clock, earlier), "backward update " + i);
            Equal(0, clock.LastAdvancement.WeekCount, "backward week count " + i);
            True(clock.LastAdvancement.WasBackward, "backward marker " + i);
            Equal(5, state.MarketWeekIndex, "backward moved market weeks " + i);
            Equal(earlier.AbsoluteRound, state.LastGameRound, "backward round baseline " + i);
            Equal(earlier.AbsoluteMonth, state.CurrentGameMonth, "backward month baseline " + i);
        }
    }
}
