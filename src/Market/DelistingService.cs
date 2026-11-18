using System;
using XingShangMod.Data;
using XingShangMod.Domain;

namespace XingShangMod.Market
{
    public sealed class DelistingService
    {
        public bool ApplyInitialRecord(SecurityBinding binding, WeeklySecurity security, int marketWeekIndex)
        {
            if (binding == null)
                throw new ArgumentNullException("binding");
            if (security == null)
                throw new ArgumentNullException("security");
            if (binding.StartRecordIndex < 0 || binding.StartRecordIndex >= security.Count)
                throw new ArgumentOutOfRangeException("binding.StartRecordIndex");
            return ApplyRecord(binding, security.GetRecord(binding.StartRecordIndex), marketWeekIndex);
        }

        public bool ApplyRecord(SecurityBinding binding, WeeklyRecord record, int marketWeekIndex)
        {
            if (binding == null)
                throw new ArgumentNullException("binding");
            if (marketWeekIndex < 0)
                throw new ArgumentOutOfRangeException("marketWeekIndex");
            if (binding.IsDelisted || !WeeklySecurity.IsDelistingRecord(record))
                return false;

            binding.IsDelisted = true;
            binding.IsDataExhausted = true;
            binding.DelistedWeek = marketWeekIndex;
            return true;
        }
    }
}
