using System;

namespace XingShangMod.Domain
{
    [Serializable]
    public sealed class ItemDefinition
    {
        public string ItemId = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Category = string.Empty;
        public string IconPath = string.Empty;
        public double ReferencePrice;
        public double Weight;
        public bool RequiresStoryUnlock;
        public string UnlockStoryId = string.Empty;
    }
}
