using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XingShangMod.Data
{
    public sealed class WeeklySecurity
    {
        private readonly WeeklyRecord[] _records;

        public SecurityHeader Header { get; private set; }
        public IReadOnlyList<WeeklyRecord> Records { get; private set; }
        public int Count { get { return _records.Length; } }
        public int FirstObservedIndex { get; private set; }
        public int LastObservedIndex { get; private set; }
        public bool HasObserved { get { return FirstObservedIndex >= 0; } }
        public WeeklyRecord? FirstObserved
        {
            get { return HasObserved ? _records[FirstObservedIndex] : (WeeklyRecord?)null; }
        }
        public WeeklyRecord? LastObserved
        {
            get { return HasObserved ? _records[LastObservedIndex] : (WeeklyRecord?)null; }
        }

        public WeeklySecurity(SecurityHeader header, IList<WeeklyRecord> records)
        {
            if (header == null)
                throw new ArgumentNullException("header");
            if (records == null)
                throw new ArgumentNullException("records");

            header.Validate();
            if (header.RowCount != records.Count)
                throw new FormatException("Header RowCount does not match the supplied records.");

            _records = new WeeklyRecord[records.Count];
            FirstObservedIndex = -1;
            LastObservedIndex = -1;
            for (int i = 0; i < records.Count; i++)
            {
                WeeklyRecord record = records[i];
                try
                {
                    record.Validate();
                }
                catch (FormatException exception)
                {
                    throw new FormatException("Invalid record " + i + ": " + exception.Message, exception);
                }
                if (i > 0 && record.WeekMondayDays <= records[i - 1].WeekMondayDays)
                    throw new FormatException("Records must be sorted by strictly increasing WeekMondayDays.");
                if (record.Status == 0)
                {
                    if (FirstObservedIndex < 0)
                        FirstObservedIndex = i;
                    LastObservedIndex = i;
                }
                _records[i] = record;
            }

            if (_records.Length > 0)
            {
                if (header.FirstWeek.Value != _records[0].WeekMondayDays)
                    throw new FormatException("FirstWeek does not match the first record.");
                if (header.LastWeek.Value != _records[_records.Length - 1].WeekMondayDays)
                    throw new FormatException("LastWeek does not match the last record.");
            }

            Header = header;
            Records = new ReadOnlyCollection<WeeklyRecord>(_records);
        }

        public static WeeklySecurity Read(string path)
        {
            SecurityHeader header;
            List<WeeklyRecord> records = WeeklyFileReader.Read(path, out header);
            return new WeeklySecurity(header, records);
        }

        public WeeklyRecord GetRecord(int index)
        {
            if (index < 0 || index >= _records.Length)
                throw new ArgumentOutOfRangeException("index");
            return _records[index];
        }

        public int FindObservedAtOrAfter(int startIndex)
        {
            if (startIndex < 0 || startIndex > _records.Length)
                throw new ArgumentOutOfRangeException("startIndex");
            for (int i = startIndex; i < _records.Length; i++)
            {
                if (_records[i].Status == 0)
                    return i;
            }
            return -1;
        }

        public int FindObservedAtOrBefore(int startIndex)
        {
            if (startIndex < 0 || startIndex >= _records.Length)
                throw new ArgumentOutOfRangeException("startIndex");
            for (int i = startIndex; i >= 0; i--)
            {
                if (_records[i].Status == 0)
                    return i;
            }
            return -1;
        }

        public double ComputeHfqClose(int index)
        {
            return ComputeHfqClose(GetRecord(index), Header.RawScale);
        }

        public double ComputeHfqClose(WeeklyRecord record)
        {
            return ComputeHfqClose(record, Header.RawScale);
        }

        public static double ComputeHfqClose(WeeklyRecord record, int rawScale)
        {
            if (record.Status != 0)
                throw new InvalidOperationException("HFQ close requires an Observed record.");
            if (rawScale <= 0)
                throw new ArgumentOutOfRangeException("rawScale");
            record.Validate();

            double result = record.RawCloseTicks / (double)rawScale * record.HfqFactor;
            if (double.IsNaN(result) || double.IsInfinity(result) || result <= 0.0)
                throw new FormatException("Computed HFQ close must be finite and positive.");
            return result;
        }

        public static bool IsDelistingRecord(WeeklyRecord record)
        {
            return (record.Flags & 2) != 0;
        }
    }
}
