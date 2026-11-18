using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using XingShangMod.Data;
using XingShangMod.Domain;

namespace XingShangMod.Progression
{
    public sealed class ItemCatalog
    {
        public const int ExpectedItemCount = 54;
        public const string StoryLockedItemId = "09";
        public const string Item09StoryId = "ch2_2_5_1";

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly Dictionary<string, ItemDefinition> _itemsById;

        public int SchemaVersion { get; private set; }
        public IReadOnlyList<ItemDefinition> Items { get; private set; }
        public int Count { get { return Items.Count; } }

        private ItemCatalog(ItemCatalogDto dto)
        {
            if (dto == null)
                throw new FormatException("Item catalog JSON must contain an object.");
            if (dto.schemaVersion != 1)
                throw new FormatException("items.schemaVersion must be 1.");
            if (dto.items == null || dto.items.Length != ExpectedItemCount)
                throw new FormatException("items.json must contain exactly 54 items.");

            var byNumber = new Dictionary<int, ItemDefinition>();
            for (int i = 0; i < dto.items.Length; i++)
            {
                ItemDefinition item = ConvertItem(dto.items[i], i);
                int number = int.Parse(item.ItemId, NumberStyles.None, CultureInfo.InvariantCulture);
                if (!byNumber.ContainsKey(number))
                    byNumber.Add(number, item);
                else
                    throw new FormatException("Duplicate item ID: " + item.ItemId);
            }

            var ordered = new ItemDefinition[ExpectedItemCount];
            _itemsById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            for (int number = 1; number <= ExpectedItemCount; number++)
            {
                ItemDefinition item;
                if (!byNumber.TryGetValue(number, out item))
                    throw new FormatException("Item IDs must cover 01 through 54; missing " + FormatItemId(number) + ".");
                ordered[number - 1] = item;
                _itemsById.Add(item.ItemId, item);
            }

            SchemaVersion = dto.schemaVersion;
            Items = new ReadOnlyCollection<ItemDefinition>(ordered);
        }

        public static ItemCatalog Load()
        {
            return Load(GetDefaultPath());
        }

        public static ItemCatalog Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("An item catalog path is required.", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException("Item catalog was not found.", path);

            string json;
            try
            {
                json = File.ReadAllText(path, StrictUtf8);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Item catalog is not valid UTF-8: " + path, exception);
            }
            return Parse(json);
        }

        public static ItemCatalog Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException("json");
            return new ItemCatalog(JsonCodec.Deserialize<ItemCatalogDto>(json));
        }

        public static string GetDefaultPath()
        {
            if (string.IsNullOrWhiteSpace(Paths.PluginPath))
                throw new InvalidOperationException("BepInEx Paths.PluginPath is not available.");
            return Path.Combine(Paths.PluginPath, "XingShangMod", "Runtime", "items.json");
        }

        public ItemDefinition GetItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("An item ID is required.", "itemId");

            ItemDefinition item;
            if (!_itemsById.TryGetValue(itemId, out item))
                throw new KeyNotFoundException("Item '" + itemId + "' is not present in items.json.");
            return item;
        }

        public ItemDefinition GetItem(int itemId)
        {
            if (itemId < 1 || itemId > ExpectedItemCount)
                throw new ArgumentOutOfRangeException("itemId");
            return GetItem(FormatItemId(itemId));
        }

        public bool TryGetItem(string itemId, out ItemDefinition item)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                item = null;
                return false;
            }
            return _itemsById.TryGetValue(itemId, out item);
        }

        public static string FormatItemId(int itemId)
        {
            if (itemId < 1 || itemId > ExpectedItemCount)
                throw new ArgumentOutOfRangeException("itemId");
            return itemId.ToString("00", CultureInfo.InvariantCulture);
        }

        private static ItemDefinition ConvertItem(ItemDefinitionDto dto, int index)
        {
            if (dto == null)
                throw new FormatException("items[" + index + "] must be an object.");
            if (dto.id < 1 || dto.id > ExpectedItemCount)
                throw new FormatException("items[" + index + "].id must be between 1 and 54.");
            if (string.IsNullOrWhiteSpace(dto.name))
                throw new FormatException("items[" + index + "].name is required.");
            if (string.IsNullOrWhiteSpace(dto.description))
                throw new FormatException("items[" + index + "].description is required.");
            if (string.IsNullOrWhiteSpace(dto.iconPath))
                throw new FormatException("items[" + index + "].iconPath is required.");
            if (double.IsNaN(dto.referencePrice) || double.IsInfinity(dto.referencePrice) || dto.referencePrice <= 0.0)
                throw new FormatException("items[" + index + "].referencePrice must be finite and positive.");
            if (double.IsNaN(dto.weight) || double.IsInfinity(dto.weight) || dto.weight <= 0.0)
                throw new FormatException("items[" + index + "].weight must be finite and positive.");

            bool requiresStoryUnlock = dto.id == 9 || dto.requiresStoryUnlock;
            string unlockStoryId = dto.unlockStoryId ?? string.Empty;
            if (dto.id == 9 && string.IsNullOrWhiteSpace(unlockStoryId))
                unlockStoryId = Item09StoryId;

            return new ItemDefinition
            {
                ItemId = FormatItemId(dto.id),
                Name = dto.name,
                Description = dto.description,
                Category = dto.category ?? string.Empty,
                IconPath = dto.iconPath,
                ReferencePrice = dto.referencePrice,
                Weight = dto.weight,
                RequiresStoryUnlock = requiresStoryUnlock,
                UnlockStoryId = unlockStoryId
            };
        }
    }

    [Serializable]
    public sealed class ItemCatalogDto
    {
        public int schemaVersion = 0;
        public ItemDefinitionDto[] items = null;
    }

    [Serializable]
    public sealed class ItemDefinitionDto
    {
        public int id = 0;
        public string name = string.Empty;
        public string description = string.Empty;
        public string category = string.Empty;
        public string iconPath = string.Empty;
        public double referencePrice = 0.0;
        public double weight = 0.0;
        public bool requiresStoryUnlock = false;
        public string unlockStoryId = string.Empty;
    }
}
