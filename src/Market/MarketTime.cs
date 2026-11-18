using System;

namespace XingShangMod.Market
{
    [Serializable]
    public struct MarketTime
    {
        public int Year;
        public int Month;
        public int Round;

        public MarketTime(int year, int month, int round)
        {
            if (year < 1)
                throw new ArgumentOutOfRangeException("year");
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException("month");
            if (round < 1 || round > 3)
                throw new ArgumentOutOfRangeException("round");

            Year = year;
            Month = month;
            Round = round;
        }

        public int AbsoluteMonth
        {
            get { return checked((Year - 1) * 12 + Month); }
        }

        public int AbsoluteRound
        {
            get { return checked((AbsoluteMonth - 1) * 3 + Round - 1); }
        }

        public static MarketTime FromAbsolute(int gameRound, int gameMonth)
        {
            if (gameRound < 0)
                throw new ArgumentOutOfRangeException("gameRound");
            if (gameMonth < 1)
                throw new ArgumentOutOfRangeException("gameMonth");

            int zeroBasedMonth = gameMonth - 1;
            int round = gameRound % 3;
            return new MarketTime(zeroBasedMonth / 12 + 1, zeroBasedMonth % 12 + 1, round + 1);
        }
    }
}
