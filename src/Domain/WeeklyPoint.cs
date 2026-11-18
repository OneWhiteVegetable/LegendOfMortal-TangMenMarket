using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class WeeklyPoint
    {
        public string ItemId = string.Empty;
        public int MarketWeekIndex;
        public int GameYear;
        public int GameMonth;
        public int GameRound;
        public int SourceRecordIndex;
        public double Price;
        public double BaseReturn;
        public double Return;
        public bool Observed;
        public bool DataExhausted;
        public bool Delisted;
    }
}
