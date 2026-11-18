using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class AnnualIndexBinding
    {
        public int Year;
        public string SecurityId = string.Empty;
        public string Code = string.Empty;
        public string DataPath = string.Empty;
        public int StartRecordIndex;
        public int NextRecordIndex;
        public double StartIndexValue;
        public bool IsDataExhausted;
    }
}
