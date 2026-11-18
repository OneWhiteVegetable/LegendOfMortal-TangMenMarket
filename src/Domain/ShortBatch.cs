using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class ShortBatch
    {
        public string BatchId = string.Empty;
        public string ItemId = string.Empty;
        public int Quantity;
        public int OpenWeek;
        public long Proceeds;
        public int DueWeek;
    }
}
