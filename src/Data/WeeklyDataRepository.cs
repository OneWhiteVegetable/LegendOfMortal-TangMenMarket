using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;

namespace XingShangMod.Data
{
    public sealed class WeeklyDataRepository
    {
        public const int CacheCapacity = 64;

        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache;
        private readonly LinkedList<CacheEntry> _recency;
        private readonly Thread _ownerThread;
        private readonly string _rootPrefix;
        private readonly StringComparison _pathComparison;

        public string DataRoot { get; private set; }
        public string CatalogPath { get; private set; }
        public SelectionCatalog Catalog { get; private set; }
        public int CachedSecurityCount
        {
            get
            {
                EnsureOwnerThread();
                return _cache.Count;
            }
        }

        public WeeklyDataRepository()
            : this(GetDefaultDataRoot())
        {
        }

        public WeeklyDataRepository(string dataRoot)
        {
            if (string.IsNullOrWhiteSpace(dataRoot))
                throw new ArgumentException("A runtime data root is required.", "dataRoot");

            DataRoot = Path.GetFullPath(dataRoot);
            string runtimeRoot = Path.GetDirectoryName(DataRoot);
            if (string.IsNullOrEmpty(runtimeRoot))
                throw new ArgumentException("The runtime data root must have a parent directory.", "dataRoot");
            CatalogPath = Path.Combine(runtimeRoot, "selection_catalog.json");
            _rootPrefix = DataRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? DataRoot
                : DataRoot + Path.DirectorySeparatorChar;
            _pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            _cache = new Dictionary<string, LinkedListNode<CacheEntry>>(StringComparer.Ordinal);
            _recency = new LinkedList<CacheEntry>();
            _ownerThread = Thread.CurrentThread;
            Catalog = SelectionCatalog.Load(CatalogPath);
        }

        public static string GetDefaultDataRoot()
        {
            if (string.IsNullOrWhiteSpace(Paths.PluginPath))
                throw new InvalidOperationException("BepInEx Paths.PluginPath is not available.");
            return Path.Combine(Paths.PluginPath, "XingShangMod", "Runtime", "data");
        }

        public WeeklySecurity GetSecurity(string securityId)
        {
            EnsureOwnerThread();
            SelectionEntry selection = Catalog.GetEntry(securityId);

            LinkedListNode<CacheEntry> cached;
            if (_cache.TryGetValue(securityId, out cached))
            {
                _recency.Remove(cached);
                _recency.AddFirst(cached);
                return cached.Value.Security;
            }

            string path = ResolvePath(selection);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Weekly data for security '" + securityId + "' was not found.",
                    path);
            }

            WeeklySecurity security;
            try
            {
                security = WeeklySecurity.Read(path);
            }
            catch (Exception exception)
            {
                if (!(exception is FormatException)
                    && !(exception is EndOfStreamException)
                    && !(exception is InvalidDataException)
                    && !(exception is DecoderFallbackException))
                {
                    throw;
                }
                throw new InvalidDataException(
                    "Weekly data for security '" + securityId + "' is invalid: " + exception.Message,
                    exception);
            }

            if (!string.Equals(security.Header.SecurityId, selection.SecurityId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Weekly data identity mismatch for '" + securityId + "': header contains '"
                    + security.Header.SecurityId + "'.");
            }

            var entry = new CacheEntry(securityId, security);
            var node = new LinkedListNode<CacheEntry>(entry);
            _recency.AddFirst(node);
            _cache.Add(securityId, node);
            while (_cache.Count > CacheCapacity)
            {
                LinkedListNode<CacheEntry> oldest = _recency.Last;
                _recency.RemoveLast();
                _cache.Remove(oldest.Value.SecurityId);
            }
            return security;
        }

        public void ClearCache()
        {
            EnsureOwnerThread();
            _cache.Clear();
            _recency.Clear();
        }

        private string ResolvePath(SelectionEntry selection)
        {
            if (string.IsNullOrWhiteSpace(selection.Path))
            {
                throw new FileNotFoundException(
                    "No weekly data path is available for security '" + selection.SecurityId
                    + "' with status '" + selection.Status + "'.");
            }
            if (Path.IsPathRooted(selection.Path))
                throw new InvalidDataException("Weekly data path must be relative: " + selection.Path);

            string relativePath = selection.Path
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(DataRoot, relativePath));
            if (!fullPath.StartsWith(_rootPrefix, _pathComparison))
                throw new InvalidDataException("Weekly data path escapes the runtime data root: " + selection.Path);
            return fullPath;
        }

        private void EnsureOwnerThread()
        {
            if (Thread.CurrentThread != _ownerThread)
            {
                throw new InvalidOperationException(
                    "WeeklyDataRepository must be constructed and used on the Unity main thread.");
            }
        }

        private sealed class CacheEntry
        {
            public string SecurityId { get; private set; }
            public WeeklySecurity Security { get; private set; }

            public CacheEntry(string securityId, WeeklySecurity security)
            {
                SecurityId = securityId;
                Security = security;
            }
        }
    }
}
