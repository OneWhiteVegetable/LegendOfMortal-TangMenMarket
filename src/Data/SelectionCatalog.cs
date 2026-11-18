using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace XingShangMod.Data
{
    public sealed class SelectionCatalog
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly Dictionary<string, SelectionEntry> _entriesById;

        public int SchemaVersion { get; private set; }
        public IReadOnlyList<SelectionEntry> Securities { get; private set; }
        public IReadOnlyList<SelectionEntry> Stocks { get { return Securities; } }
        public IReadOnlyList<SelectionEntry> Indices { get; private set; }
        public IReadOnlyList<SelectionEntry> Entries { get; private set; }

        private SelectionCatalog(SelectionCatalogDto dto)
        {
            if (dto == null)
                throw new FormatException("Selection catalog JSON must contain an object.");
            if (dto.schema_version != 0 && dto.schemaVersion != 0 && dto.schema_version != dto.schemaVersion)
                throw new FormatException("Selection catalog has conflicting schema_version and schemaVersion fields.");

            SchemaVersion = dto.schema_version != 0 ? dto.schema_version : dto.schemaVersion;
            if (SchemaVersion != 1)
                throw new FormatException("selection_catalog.schema_version must be 1.");
            if (dto.securities != null && dto.stocks != null)
                throw new FormatException("Selection catalog cannot contain both securities and stocks collections.");

            SelectionEntryDto[] stocks = dto.stocks ?? dto.securities;
            if (stocks == null)
                throw new FormatException("selection_catalog.stocks or selection_catalog.securities is required.");
            if (dto.indices == null)
                throw new FormatException("selection_catalog.indices is required.");

            Securities = ConvertEntries(stocks, false, dto.stocks != null ? "stocks" : "securities");
            Indices = ConvertEntries(dto.indices, true, "indices");

            var entries = new List<SelectionEntry>(Securities.Count + Indices.Count);
            _entriesById = new Dictionary<string, SelectionEntry>(StringComparer.Ordinal);
            AddEntries(entries, Securities);
            AddEntries(entries, Indices);
            if (entries.Count == 0)
                throw new FormatException("Selection catalog must contain at least one entry.");
            Entries = new ReadOnlyCollection<SelectionEntry>(entries);
        }

        public static SelectionCatalog Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A selection catalog path is required.", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException("Selection catalog was not found.", path);

            string json;
            try
            {
                json = File.ReadAllText(path, StrictUtf8);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Selection catalog is not valid UTF-8: " + path, exception);
            }

            try
            {
                return Parse(json);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Invalid selection catalog '" + path + "': " + exception.Message, exception);
            }
        }

        public static SelectionCatalog Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException("json");
            return new SelectionCatalog(SelectionJsonParser.Parse(json));
        }

        public SelectionEntry GetEntry(string securityId)
        {
            if (string.IsNullOrWhiteSpace(securityId))
                throw new ArgumentException("A security ID is required.", "securityId");

            SelectionEntry entry;
            if (!_entriesById.TryGetValue(securityId, out entry))
                throw new KeyNotFoundException("Security '" + securityId + "' is not present in selection_catalog.json.");
            return entry;
        }

        public bool TryGetEntry(string securityId, out SelectionEntry entry)
        {
            if (string.IsNullOrWhiteSpace(securityId))
            {
                entry = null;
                return false;
            }
            return _entriesById.TryGetValue(securityId, out entry);
        }

        private static IReadOnlyList<SelectionEntry> ConvertEntries(
            SelectionEntryDto[] source,
            bool isIndex,
            string collectionName)
        {
            var result = new SelectionEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = new SelectionEntry(source[i], isIndex, collectionName + "[" + i + "]");
            return new ReadOnlyCollection<SelectionEntry>(result);
        }

        private void AddEntries(
            List<SelectionEntry> destination,
            IReadOnlyList<SelectionEntry> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                SelectionEntry entry = source[i];
                if (_entriesById.ContainsKey(entry.SecurityId))
                    throw new FormatException("Duplicate selection catalog security ID: " + entry.SecurityId);
                _entriesById.Add(entry.SecurityId, entry);
                destination.Add(entry);
            }
        }
    }

    internal sealed class SelectionCatalogDto
    {
        public int schema_version;
        public int schemaVersion;
        public SelectionEntryDto[] securities;
        public SelectionEntryDto[] stocks;
        public SelectionEntryDto[] indices;
    }
}
