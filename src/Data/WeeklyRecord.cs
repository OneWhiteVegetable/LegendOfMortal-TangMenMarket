using System;

namespace XingShangMod.Data
{
    public struct WeeklyRecord
    {
        public int WeekMondayDays;
        public int CloseDateDays;
        public long RawCloseTicks;
        public double HfqFactor;
        public ushort Flags;
        public byte Status;
        public byte Reserved;

        public void Validate()
        {
            if (RawCloseTicks < 0)
                throw new FormatException("RawCloseTicks must be non-negative.");
            if (Reserved != 0)
                throw new FormatException("Reserved must be zero.");
            if ((WeekMondayDays % 7 + 7) % 7 != 4)
                throw new FormatException("WeekMondayDays must identify a Monday.");

            if (Status == 0)
            {
                if (RawCloseTicks <= 0)
                    throw new FormatException("RawCloseTicks must be positive for Observed records.");
                if (double.IsNaN(HfqFactor) || double.IsInfinity(HfqFactor) || HfqFactor <= 0.0)
                    throw new FormatException("HfqFactor must be finite and positive for Observed records.");
                if (CloseDateDays < WeekMondayDays || CloseDateDays > (long)WeekMondayDays + 6)
                    throw new FormatException("CloseDateDays must be a valid date within the record week for Observed records.");
            }
            else if (Status == 1 || Status == 2)
            {
                if (CloseDateDays != -1)
                    throw new FormatException("CloseDateDays must be -1 for empty observations.");
                if (RawCloseTicks != 0)
                    throw new FormatException("RawCloseTicks must be zero for empty observations.");
                if (HfqFactor != 0.0)
                    throw new FormatException("HfqFactor must be zero for empty observations.");
            }
            else
            {
                throw new FormatException("Status must be 0 (Observed), 1 (NoTrade), or 2 (SourceMissing).");
            }
        }
    }
}
