using System;
using XingShangMod.Data;

namespace XingShangMod.Market
{
    internal sealed class PriceSeriesSample
    {
        public int SourceRecordIndex { get; private set; }
        public double BaseReturn { get; private set; }
        public bool HasRecord { get; private set; }
        public bool Observed { get; private set; }
        public bool DelistingRecord { get; private set; }

        internal PriceSeriesSample(int sourceRecordIndex, double baseReturn, bool hasRecord, bool observed, bool delistingRecord)
        {
            SourceRecordIndex = sourceRecordIndex;
            BaseReturn = baseReturn;
            HasRecord = hasRecord;
            Observed = observed;
            DelistingRecord = delistingRecord;
        }
    }

    internal static class PriceSeries
    {
        public const double MinimumReturn = -0.99;

        public static PriceSeriesSample Advance(WeeklySecurity security, ref int nextRecordIndex, ref bool isDataExhausted)
        {
            if (security == null)
                throw new ArgumentNullException("security");
            if (nextRecordIndex < 0 || nextRecordIndex > security.Count)
                throw new ArgumentOutOfRangeException("nextRecordIndex");

            if (nextRecordIndex >= security.Count)
            {
                isDataExhausted = true;
                int lastObserved = security.FindObservedAtOrBefore(security.Count - 1);
                return new PriceSeriesSample(lastObserved, 0.0, false, false, false);
            }

            int recordIndex = nextRecordIndex;
            WeeklyRecord record = security.GetRecord(recordIndex);
            nextRecordIndex++;
            if (nextRecordIndex >= security.Count)
                isDataExhausted = true;

            double baseReturn = 0.0;
            bool observed = record.Status == 0;
            if (observed)
            {
                int previousObserved = security.FindObservedAtOrBefore(recordIndex - 1);
                if (previousObserved < 0)
                    throw new InvalidOperationException("The selected series does not have an Observed baseline.");
                double previousClose = security.ComputeHfqClose(previousObserved);
                double currentClose = security.ComputeHfqClose(recordIndex);
                baseReturn = ClampReturn(currentClose / previousClose - 1.0);
            }

            return new PriceSeriesSample(
                recordIndex,
                baseReturn,
                true,
                observed,
                WeeklySecurity.IsDelistingRecord(record));
        }

        public static double ClampReturn(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("A weekly return must be finite.");
            return Math.Max(MinimumReturn, value);
        }
    }
}
