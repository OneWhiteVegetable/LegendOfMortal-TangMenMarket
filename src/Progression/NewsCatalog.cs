using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using XingShangMod.Data;

namespace XingShangMod.Progression
{
    public sealed class NewsCatalog
    {
        public const int PeriodCount = 12;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly IReadOnlyList<NewsEntry> EmptyCandidates =
            new ReadOnlyCollection<NewsEntry>(new NewsEntry[0]);
        private readonly Dictionary<NewsPoolKey, IReadOnlyList<NewsEntry>> _pools;

        public int SchemaVersion { get; private set; }
        public IReadOnlyList<NewsPeriod> Periods { get; private set; }
        public IReadOnlyList<NewsEntry> Entries { get; private set; }

        private NewsCatalog(NewsCatalogDto dto)
        {
            if (dto == null)
                throw new FormatException("News catalog JSON must contain an object.");
            if (dto.schemaVersion != 1)
                throw new FormatException("news.schemaVersion must be 1.");
            if (dto.periods == null || dto.periods.Length != PeriodCount)
                throw new FormatException("news.json must contain exactly 12 periods.");
            if (dto.news == null)
                throw new FormatException("news.json must contain a news collection.");

            var periods = new NewsPeriod[PeriodCount];
            for (int i = 0; i < dto.periods.Length; i++)
            {
                NewsPeriod period = ConvertPeriod(dto.periods[i], i);
                if (period.Index < 0 || period.Index >= PeriodCount)
                    throw new FormatException("News period indices must be between 0 and 11.");
                if (periods[period.Index] != null)
                    throw new FormatException("Duplicate news period index: " + period.Index);
                if (MapPeriodIndex(period.Year, period.StartMonth) != period.Index
                    || MapPeriodIndex(period.Year, period.EndMonth) != period.Index)
                    throw new FormatException("News period metadata does not match the game-date mapping: " + period.Index);
                periods[period.Index] = period;
            }
            for (int i = 0; i < periods.Length; i++)
            {
                if (periods[i] == null)
                    throw new FormatException("News periods must cover indices 0 through 11; missing " + i + ".");
            }

            var entries = new List<NewsEntry>(dto.news.Length);
            var pools = new Dictionary<NewsPoolKey, List<NewsEntry>>();
            for (int i = 0; i < dto.news.Length; i++)
            {
                NewsEntry entry = ConvertEntry(dto.news[i], i);
                entries.Add(entry);
                var key = new NewsPoolKey(entry.ItemId, entry.PeriodIndex, entry.Direction);
                List<NewsEntry> pool;
                if (!pools.TryGetValue(key, out pool))
                {
                    pool = new List<NewsEntry>();
                    pools.Add(key, pool);
                }
                pool.Add(entry);
            }

            _pools = new Dictionary<NewsPoolKey, IReadOnlyList<NewsEntry>>();
            foreach (KeyValuePair<NewsPoolKey, List<NewsEntry>> pair in pools)
                _pools.Add(pair.Key, new ReadOnlyCollection<NewsEntry>(pair.Value));

            SchemaVersion = dto.schemaVersion;
            Periods = new ReadOnlyCollection<NewsPeriod>(periods);
            Entries = new ReadOnlyCollection<NewsEntry>(entries);
        }

        public static NewsCatalog Load()
        {
            return Load(GetDefaultPath());
        }

        public static NewsCatalog Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A news catalog path is required.", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException("News catalog was not found.", path);

            string json;
            try
            {
                json = File.ReadAllText(path, StrictUtf8);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("News catalog is not valid UTF-8: " + path, exception);
            }
            return Parse(json);
        }

        public static NewsCatalog Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException("json");
            return new NewsCatalog(JsonCodec.Deserialize<NewsCatalogDto>(json));
        }

        public static string GetDefaultPath()
        {
            if (string.IsNullOrWhiteSpace(Paths.PluginPath))
                throw new InvalidOperationException("BepInEx Paths.PluginPath is not available.");
            return Path.Combine(Paths.PluginPath, "XingShangMod", "Runtime", "news.json");
        }

        public IReadOnlyList<NewsEntry> GetCandidates(string itemId, int periodIndex, int direction)
        {
            ValidateQuery(itemId, periodIndex, direction);
            IReadOnlyList<NewsEntry> candidates;
            if (!_pools.TryGetValue(new NewsPoolKey(itemId, periodIndex, direction), out candidates))
                return EmptyCandidates;
            return candidates;
        }

        public IReadOnlyList<NewsEntry> GetCandidates(int itemId, int periodIndex, int direction)
        {
            return GetCandidates(ItemCatalog.FormatItemId(itemId), periodIndex, direction);
        }

        public bool TryGetCandidates(
            string itemId,
            int periodIndex,
            int direction,
            out IReadOnlyList<NewsEntry> candidates)
        {
            ValidateQuery(itemId, periodIndex, direction);
            return _pools.TryGetValue(new NewsPoolKey(itemId, periodIndex, direction), out candidates);
        }

        public static int MapPeriodIndex(int gameYear, int gameMonth)
        {
            if (gameYear < 1)
                throw new ArgumentOutOfRangeException("gameYear");
            if (gameMonth < 1 || gameMonth > 12)
                throw new ArgumentOutOfRangeException("gameMonth");

            if (gameYear >= 4)
                return 11;
            if (gameYear == 1)
            {
                if (gameMonth <= 6)
                    return 0;
                return gameMonth <= 9 ? 1 : 2;
            }

            int quarter = (gameMonth - 1) / 3;
            return gameYear == 2 ? 3 + quarter : 7 + quarter;
        }

        private static NewsPeriod ConvertPeriod(NewsPeriodDto dto, int index)
        {
            if (dto == null)
                throw new FormatException("periods[" + index + "] must be an object.");
            if (string.IsNullOrWhiteSpace(dto.name))
                throw new FormatException("periods[" + index + "].name is required.");
            if (dto.year < 1 || dto.startMonth < 1 || dto.startMonth > 12
                || dto.endMonth < dto.startMonth || dto.endMonth > 12)
                throw new FormatException("periods[" + index + "] contains an invalid game-date range.");
            return new NewsPeriod(dto.index, dto.name, dto.year, dto.startMonth, dto.endMonth);
        }

        private static NewsEntry ConvertEntry(NewsEntryDto dto, int index)
        {
            if (dto == null)
                throw new FormatException("news[" + index + "] must be an object.");
            if (dto.itemId < 1 || dto.itemId > ItemCatalog.ExpectedItemCount)
                throw new FormatException("news[" + index + "].itemId must be between 1 and 54.");
            if (dto.periodIndex < 0 || dto.periodIndex >= PeriodCount)
                throw new FormatException("news[" + index + "].periodIndex must be between 0 and 11.");
            int direction;
            if (string.Equals(dto.direction, "up", StringComparison.Ordinal))
                direction = 1;
            else if (string.Equals(dto.direction, "down", StringComparison.Ordinal))
                direction = -1;
            else
                throw new FormatException("news[" + index + "].direction must be up or down.");
            if (string.IsNullOrWhiteSpace(dto.text))
                throw new FormatException("news[" + index + "].text is required.");

            string itemId = ItemCatalog.FormatItemId(dto.itemId);
            return new NewsEntry(
                itemId,
                dto.periodIndex,
                direction,
                dto.direction,
                dto.text,
                dto.eventName ?? string.Empty,
                dto.intensityLevel ?? dto.intensity ?? string.Empty);
        }

        private static void ValidateQuery(string itemId, int periodIndex, int direction)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("An item ID is required.", "itemId");
            if (periodIndex < 0 || periodIndex >= PeriodCount)
                throw new ArgumentOutOfRangeException("periodIndex");
            if (direction != 1 && direction != -1)
                throw new ArgumentOutOfRangeException("direction");
        }

        private struct NewsPoolKey : IEquatable<NewsPoolKey>
        {
            private readonly string _itemId;
            private readonly int _periodIndex;
            private readonly int _direction;

            public NewsPoolKey(string itemId, int periodIndex, int direction)
            {
                _itemId = itemId;
                _periodIndex = periodIndex;
                _direction = direction;
            }

            public bool Equals(NewsPoolKey other)
            {
                return string.Equals(_itemId, other._itemId, StringComparison.Ordinal)
                    && _periodIndex == other._periodIndex
                    && _direction == other._direction;
            }

            public override bool Equals(object obj)
            {
                return obj is NewsPoolKey && Equals((NewsPoolKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.Ordinal.GetHashCode(_itemId);
                    hash = hash * 31 + _periodIndex;
                    return hash * 31 + _direction;
                }
            }
        }
    }

    public sealed class NewsPeriod
    {
        public int Index { get; private set; }
        public string Name { get; private set; }
        public int Year { get; private set; }
        public int StartMonth { get; private set; }
        public int EndMonth { get; private set; }

        internal NewsPeriod(int index, string name, int year, int startMonth, int endMonth)
        {
            Index = index;
            Name = name;
            Year = year;
            StartMonth = startMonth;
            EndMonth = endMonth;
        }
    }

    public sealed class NewsEntry
    {
        public string ItemId { get; private set; }
        public int PeriodIndex { get; private set; }
        public int Direction { get; private set; }
        public string DirectionName { get; private set; }
        public string Text { get; private set; }
        public string EventName { get; private set; }
        public string Intensity { get; private set; }
        public string PoolId { get; private set; }

        internal NewsEntry(
            string itemId,
            int periodIndex,
            int direction,
            string directionName,
            string text,
            string eventName,
            string intensity)
        {
            ItemId = itemId;
            PeriodIndex = periodIndex;
            Direction = direction;
            DirectionName = directionName;
            Text = text;
            EventName = eventName;
            Intensity = intensity;
            PoolId = itemId + ":" + periodIndex.ToString(CultureInfo.InvariantCulture) + ":" + directionName;
        }
    }

    [Serializable]
    public sealed class NewsCatalogDto
    {
        public int schemaVersion = 0;
        public NewsPeriodDto[] periods = null;
        public NewsEntryDto[] news = null;
    }

    [Serializable]
    public sealed class NewsPeriodDto
    {
        public int index = 0;
        public string name = string.Empty;
        public int year = 0;
        public int startMonth = 0;
        public int endMonth = 0;
    }

    [Serializable]
    public sealed class NewsEntryDto
    {
        public int itemId = 0;
        public string period = string.Empty;
        public int periodIndex = 0;
        public string direction = string.Empty;
        public string text = string.Empty;
        public string eventName = string.Empty;
        public string intensity = string.Empty;
        public string intensityLevel = string.Empty;
    }
}
