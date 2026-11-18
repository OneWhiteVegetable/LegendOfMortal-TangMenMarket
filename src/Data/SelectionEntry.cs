using System;

namespace XingShangMod.Data
{
    public sealed class SelectionEntry
    {
        public string SecurityId { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }
        public string Status { get; private set; }
        public int RowCount { get; private set; }
        public int FirstWeek { get; private set; }
        public int LastWeek { get; private set; }
        public int ObservedRows { get; private set; }
        public int NoTradeRows { get; private set; }
        public int SourceMissingRows { get; private set; }
        public bool IsIndex { get; private set; }

        internal SelectionEntry(SelectionEntryDto dto, bool isIndex, string location)
        {
            if (dto == null)
                throw new FormatException(location + " must be an object.");

            IsIndex = isIndex;
            SecurityId = SelectTextAlias(dto.security_id, dto.securityId, "security_id", "securityId", location, false);
            SecurityId = SelectTextAlias(SecurityId, dto.index_id, "security_id", "index_id", location, false);
            SecurityId = SelectTextAlias(SecurityId, dto.indexId, "security_id", "indexId", location);
            Name = dto.name;
            Path = SelectTextAlias(dto.path, dto.relative_path, "path", "relative_path", location, false);
            Status = dto.status;
            RowCount = SelectIntegerAlias(dto.row_count, dto.rowCount, "row_count", "rowCount", location);
            FirstWeek = SelectIntegerAlias(dto.first_week, dto.firstWeek, "first_week", "firstWeek", location);
            LastWeek = SelectIntegerAlias(dto.last_week, dto.lastWeek, "last_week", "lastWeek", location);
            ObservedRows = SelectIntegerAlias(dto.observed_rows, dto.observedRows, "observed_rows", "observedRows", location);
            NoTradeRows = SelectIntegerAlias(dto.no_trade_rows, dto.noTradeRows, "no_trade_rows", "noTradeRows", location);
            SourceMissingRows = SelectIntegerAlias(dto.source_missing_rows, dto.sourceMissingRows, "source_missing_rows", "sourceMissingRows", location);

            RequireText(SecurityId, location + ".security_id");
            RequireText(Status, location + ".status");
            if (Status != "success" && Status != "partial" && Status != "failed" && Status != "excluded")
                throw new FormatException(location + ".status must be success, partial, failed, or excluded.");
            if (RowCount < 0 || ObservedRows < 0 || NoTradeRows < 0 || SourceMissingRows < 0)
                throw new FormatException(location + " row counts cannot be negative.");
            if ((Status == "success" || Status == "partial") && string.IsNullOrWhiteSpace(Path))
                throw new FormatException(location + ".path is required for available weekly data.");
            if (Status == "success" && (RowCount <= 0 || ObservedRows <= 0))
                throw new FormatException(location + " must contain rows and an Observed record for success entries.");
            if (ObservedRows > RowCount || NoTradeRows > RowCount || SourceMissingRows > RowCount)
                throw new FormatException(location + " row counts exceed row_count.");
            if (RowCount > 0 && (FirstWeek > LastWeek
                || (FirstWeek % 7 + 7) % 7 != 4
                || (LastWeek % 7 + 7) % 7 != 4))
            {
                throw new FormatException(location + " first_week and last_week must identify an ordered Monday range.");
            }
        }

        private static string SelectTextAlias(
            string primary,
            string alias,
            string primaryName,
            string aliasName,
            string location,
            bool required = true)
        {
            bool hasPrimary = !string.IsNullOrWhiteSpace(primary);
            bool hasAlias = !string.IsNullOrWhiteSpace(alias);
            if (hasPrimary && hasAlias && !string.Equals(primary, alias, StringComparison.Ordinal))
                throw new FormatException(location + " has conflicting " + primaryName + " and " + aliasName + " fields.");
            if (hasPrimary)
                return primary;
            if (hasAlias)
                return alias;
            if (required)
                throw new FormatException(location + "." + primaryName + " is required.");
            return null;
        }

        private static int SelectIntegerAlias(
            int primary,
            int alias,
            string primaryName,
            string aliasName,
            string location)
        {
            if (primary != 0 && alias != 0 && primary != alias)
                throw new FormatException(location + " has conflicting " + primaryName + " and " + aliasName + " fields.");
            return primary != 0 ? primary : alias;
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException(name + " must be a non-empty string.");
        }
    }

    [Serializable]
    internal sealed class SelectionEntryDto
    {
        public string security_id;
        public string securityId;
        public string index_id;
        public string indexId;
        public string name;
        public string path;
        public string relative_path;
        public string status;
        public int row_count;
        public int rowCount;
        public int first_week;
        public int firstWeek;
        public int last_week;
        public int lastWeek;
        public int observed_rows;
        public int observedRows;
        public int no_trade_rows;
        public int noTradeRows;
        public int source_missing_rows;
        public int sourceMissingRows;
    }
}
