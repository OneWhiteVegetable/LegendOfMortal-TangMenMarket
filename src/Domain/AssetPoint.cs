using System;
using System.ComponentModel;

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
        // Preserve the checksum of snapshots written before profit history existed.
        // A recorded zero is distinguished by the version and availability fields.
        [DefaultValue(0)] public int ProfitRecordVersion;
        [DefaultValue(false)] public bool HasProfitMetrics;
        [DefaultValue(0d)] public double TotalProfit;
        [DefaultValue(0d)] public double CapitalBasis;

        internal AssetPoint Copy() { return (AssetPoint)MemberwiseClone(); }
    }
}
