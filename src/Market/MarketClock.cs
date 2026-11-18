using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XingShangMod.Domain;

namespace XingShangMod.Market
{
    public sealed class MarketClockAdvancement
    {
        public IReadOnlyList<MarketTime> Weeks { get; private set; }
        public int WeekCount { get { return Weeks.Count; } }
        public int StartWeekIndex { get; private set; }
        public int EndWeekIndex { get; private set; }
        public bool WasBackward { get; private set; }

        internal MarketClockAdvancement(
            IList<MarketTime> weeks,
            int startWeekIndex,
            int endWeekIndex,
            bool wasBackward)
        {
            Weeks = new ReadOnlyCollection<MarketTime>(new List<MarketTime>(weeks));
            StartWeekIndex = startWeekIndex;
            EndWeekIndex = endWeekIndex;
            WasBackward = wasBackward;
        }
    }

    public sealed class MarketClock
    {
        public const int PointsPerWeek = 3;
        public const int WeeksPerGameMonth = 4;
        public const int RoundsPerGameMonth = 3;

        private readonly MarketClockState _state;

        public MarketClockState State { get { return _state; } }
        public MarketClockAdvancement LastAdvancement { get; private set; }
        public bool IsInitialized { get { return _state.CurrentGameMonth > 0; } }

        public MarketClock(MarketClockState state)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (state.Carry < 0 || state.Carry >= PointsPerWeek)
                throw new ArgumentOutOfRangeException("state.Carry");
            if (state.MarketWeekIndex < 0 || state.LastGameRound < 0
                || state.CurrentGameMonth < 0 || state.WeeksThisGameMonth < 0)
            {
                throw new ArgumentOutOfRangeException("state");
            }

            _state = state;
            LastAdvancement = Empty(false);
        }

        public void Initialize(MarketTime time)
        {
            if (IsInitialized)
            {
                LastAdvancement = Empty(false);
                return;
            }

            SetBaseline(time.AbsoluteRound, time.AbsoluteMonth);
            _state.Carry = 0;
            _state.WeeksThisGameMonth = 0;
            LastAdvancement = Empty(false);
        }

        public int ApplyActionCost(int actualPoints, MarketTime time)
        {
            return ApplyActionCost(actualPoints, time.AbsoluteRound, time.AbsoluteMonth);
        }

        public int ApplyActionCost(int actualPoints, int gameRound, int gameMonth)
        {
            if (actualPoints < 0)
                throw new ArgumentOutOfRangeException("actualPoints");
            ValidateTime(gameRound, gameMonth);

            if (!IsInitialized)
                SetBaseline(gameRound, gameMonth);
            if (IsBackward(gameRound, gameMonth))
                return CompleteBackward(gameRound, gameMonth);

            var weeks = new List<MarketTime>();
            if (gameMonth > _state.CurrentGameMonth)
                weeks.AddRange(CrossMonths(gameRound, gameMonth, false));

            long total = (long)_state.Carry + actualPoints;
            int elapsedWeeks = checked((int)(total / PointsPerWeek));
            _state.Carry = (int)(total % PointsPerWeek);
            _state.WeeksThisGameMonth = checked(_state.WeeksThisGameMonth + elapsedWeeks);
            MarketTime time = MarketTime.FromAbsolute(gameRound, gameMonth);
            for (int i = 0; i < elapsedWeeks; i++)
                weeks.Add(time);

            return Commit(weeks, gameRound, gameMonth);
        }

        public int SynchronizeNaturalTime(MarketTime time)
        {
            return SynchronizeNaturalTime(time.AbsoluteRound, time.AbsoluteMonth);
        }

        public int SynchronizeNaturalTime(int gameRound, int gameMonth)
        {
            ValidateTime(gameRound, gameMonth);
            if (!IsInitialized)
            {
                SetBaseline(gameRound, gameMonth);
                LastAdvancement = Empty(false);
                return 0;
            }
            if (IsBackward(gameRound, gameMonth))
                return CompleteBackward(gameRound, gameMonth);

            var weeks = new List<MarketTime>();
            if (gameMonth > _state.CurrentGameMonth)
                weeks.AddRange(CrossMonths(gameRound, gameMonth, false));
            return Commit(weeks, gameRound, gameMonth);
        }

        public int AdvanceStory(MarketTime time)
        {
            return AdvanceStory(time.AbsoluteRound, time.AbsoluteMonth);
        }

        public int AdvanceStory(int gameRound, int gameMonth)
        {
            ValidateTime(gameRound, gameMonth);
            if (!IsInitialized)
            {
                SetBaseline(gameRound, gameMonth);
                LastAdvancement = Empty(false);
                return 0;
            }
            if (IsBackward(gameRound, gameMonth))
                return CompleteBackward(gameRound, gameMonth);

            var weeks = new List<MarketTime>();
            if (gameMonth > _state.CurrentGameMonth)
            {
                weeks.AddRange(CrossMonths(gameRound, gameMonth, true));
            }
            else
            {
                int elapsedRounds = gameRound - _state.LastGameRound;
                int oldRound = _state.LastGameRound;
                _state.WeeksThisGameMonth = checked(_state.WeeksThisGameMonth + elapsedRounds);
                for (int i = 1; i <= elapsedRounds; i++)
                    weeks.Add(MarketTime.FromAbsolute(oldRound + i, gameMonth));
            }

            return Commit(weeks, gameRound, gameMonth);
        }

        private List<MarketTime> CrossMonths(int gameRound, int gameMonth, bool story)
        {
            int oldMonth = _state.CurrentGameMonth;
            int oldRound = _state.LastGameRound;
            int crossedMonths = gameMonth - oldMonth;
            var weeks = new List<MarketTime>();

            AppendMonthCompletion(oldMonth, oldRound, weeks);
            for (int i = 1; i < crossedMonths; i++)
            {
                int month = oldMonth + i;
                int lowerRound = checked((month - 1) * RoundsPerGameMonth + RoundsPerGameMonth - 1);
                MarketTime time = MarketTime.FromAbsolute(lowerRound, month);
                for (int week = 0; week < WeeksPerGameMonth; week++)
                    weeks.Add(time);
            }

            _state.CurrentGameMonth = gameMonth;
            _state.Carry = 0;
            _state.WeeksThisGameMonth = 0;
            if (story)
            {
                int targetPosition = gameRound % RoundsPerGameMonth;
                int firstTargetRound = gameRound - targetPosition + 1;
                for (int i = 0; i < targetPosition; i++)
                    weeks.Add(MarketTime.FromAbsolute(firstTargetRound + i, gameMonth));
                _state.WeeksThisGameMonth = targetPosition;
            }
            return weeks;
        }

        private void AppendMonthCompletion(int gameMonth, int gameRound, List<MarketTime> weeks)
        {
            int completion = Math.Max(0, WeeksPerGameMonth - _state.WeeksThisGameMonth);
            MarketTime time = MarketTime.FromAbsolute(gameRound, gameMonth);
            for (int i = 0; i < completion; i++)
                weeks.Add(time);
            _state.WeeksThisGameMonth = Math.Max(_state.WeeksThisGameMonth, WeeksPerGameMonth);
            _state.Carry = 0;
        }

        private int Commit(List<MarketTime> weeks, int gameRound, int gameMonth)
        {
            int start = weeks.Count == 0 ? _state.MarketWeekIndex : checked(_state.MarketWeekIndex + 1);
            _state.MarketWeekIndex = checked(_state.MarketWeekIndex + weeks.Count);
            _state.LastGameRound = gameRound;
            _state.CurrentGameMonth = gameMonth;
            LastAdvancement = new MarketClockAdvancement(weeks, start, _state.MarketWeekIndex, false);
            return weeks.Count;
        }

        private int CompleteBackward(int gameRound, int gameMonth)
        {
            SetBaseline(gameRound, gameMonth);
            LastAdvancement = Empty(true);
            return 0;
        }

        private bool IsBackward(int gameRound, int gameMonth)
        {
            return gameMonth < _state.CurrentGameMonth || gameRound < _state.LastGameRound;
        }

        private void SetBaseline(int gameRound, int gameMonth)
        {
            _state.LastGameRound = gameRound;
            _state.CurrentGameMonth = gameMonth;
        }

        private MarketClockAdvancement Empty(bool wasBackward)
        {
            return new MarketClockAdvancement(
                new MarketTime[0],
                _state.MarketWeekIndex,
                _state.MarketWeekIndex,
                wasBackward);
        }

        private static void ValidateTime(int gameRound, int gameMonth)
        {
            if (gameRound < 0)
                throw new ArgumentOutOfRangeException("gameRound");
            if (gameMonth < 1)
                throw new ArgumentOutOfRangeException("gameMonth");
        }
    }
}
