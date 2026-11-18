using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class Holding
    {
        public string ItemId = string.Empty;
        public int Quantity;
        public long TotalCost;
    }
}
