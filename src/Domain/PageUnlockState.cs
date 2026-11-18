using System;
using System.Collections.Generic;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class PageUnlockState
    {
        public string SourceId = string.Empty;
        public int PageIndex;
        public bool IsUnlocked;
        public int GrantedWeek = -1;
        public List<string> ItemIds = new List<string>();
    }
}
