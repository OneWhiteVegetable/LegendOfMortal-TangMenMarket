using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class SecurityBinding
    {
        public string ItemId = string.Empty;
        public string SecurityId = string.Empty;
        public string Code = string.Empty;
        public string DataPath = string.Empty;
        public int StartRecordIndex;
        public int NextRecordIndex;
        public int AppearedWeek;
        public double LastPrice;
        public bool CanDelist;
        public bool IsDataExhausted;
        public bool IsDelisted;
        public int DelistedWeek = -1;
    }
}
