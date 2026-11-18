using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class ElasticityState
    {
        public string ItemId = string.Empty;
        public int QuietCount;
        public bool IsActive;
        public int ActiveWeekCount;
        public double Multiplier = 1.0;
    }
}
