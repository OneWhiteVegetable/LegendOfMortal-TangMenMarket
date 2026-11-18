using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class NewsRecord
    {
        public string NewsId = string.Empty;
        public string ItemId = string.Empty;
        public int MarketWeekIndex;
        public int GameYear;
        public int GameMonth;
        public int GameRound;
        public int Direction;
        public double Return;
        public string PoolId = string.Empty;
        public int SelectionIndex;
        public string Text = string.Empty;
    }
}
