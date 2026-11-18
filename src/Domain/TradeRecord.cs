using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class TradeRecord
    {
        public string TradeId = string.Empty;
        public int MarketWeekIndex;
        public int GameYear;
        public int GameMonth;
        public int GameRound;
        public string ItemId = string.Empty;
        public string Action = string.Empty;
        public string BatchId = string.Empty;
        public int Quantity;
        public double UnitPrice;
        public long CashDelta;
        public long DebtDelta;
    }
}
