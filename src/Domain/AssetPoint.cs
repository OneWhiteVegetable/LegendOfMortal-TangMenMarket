using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class AssetPoint
    {
        public int MarketWeekIndex;
        public int GameYear;
        public int GameMonth;
        public int GameRound;
        public long Cash;
        public double LongValue;
        public double ShortLiability;
        public long Debt;
        public double NetAssets;
    }
}
