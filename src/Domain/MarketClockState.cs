using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class MarketClockState
    {
        public int Carry;
        public int MarketWeekIndex;
        public int LastGameRound;
        public int CurrentGameMonth;
        public int WeeksThisGameMonth;
    }
}
