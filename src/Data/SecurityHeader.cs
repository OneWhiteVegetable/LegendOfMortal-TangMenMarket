using System;

namespace XingShangMod.Data
{
    public sealed class SecurityHeader
    {
        public int SchemaVersion { get; internal set; }
        public int RecordSize { get; internal set; }
        public string SecurityId { get; internal set; }
        public string Code { get; internal set; }
        public string Currency { get; internal set; }
        public int RawScale { get; internal set; }
        public string CalendarId { get; internal set; }
        public int RowCount { get; internal set; }
        public int? FirstWeek { get; internal set; }
        public int? LastWeek { get; internal set; }
        public string Source { get; internal set; }
        public string AdjustmentAlgorithm { get; internal set; }

        public void Validate()
        {
            if (SchemaVersion != 1)
                throw new FormatException("SchemaVersion must be 1.");
            if (RecordSize != 28)
                throw new FormatException("RecordSize must be 28.");
            RequireText(SecurityId, "SecurityId");
            RequireText(Code, "Code");
            RequireText(Currency, "Currency");
            if (RawScale <= 0)
                throw new FormatException("RawScale must be positive.");
            RequireText(CalendarId, "CalendarId");
            if (RowCount < 0)
                throw new FormatException("RowCount cannot be negative.");
            RequireText(Source, "Source");
            RequireText(AdjustmentAlgorithm, "AdjustmentAlgorithm");
            if (RowCount == 0 && (FirstWeek.HasValue || LastWeek.HasValue))
                throw new FormatException("FirstWeek and LastWeek must be null for an empty file.");
            if (RowCount > 0 && (!FirstWeek.HasValue || !LastWeek.HasValue))
                throw new FormatException("FirstWeek and LastWeek are required for non-empty files.");
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
                throw new FormatException(name + " must be a non-empty string.");
        }
    }
}
