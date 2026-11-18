using System;
using System.Collections.Generic;
using Mortal.Core;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Trading
{
    public struct CapacityStats
    {
        public int Life;
        public int Lightness;
        public int InternalStrength;

        public CapacityStats(int life, int lightness, int internalStrength)
        {
            Life = life;
            Lightness = lightness;
            InternalStrength = internalStrength;
        }
    }

    public sealed class ItemCapacity
    {
        public string ItemId { get; internal set; }
        public long LongQuantity { get; internal set; }
        public long ShortQuantity { get; internal set; }
        public int StackLimit { get; internal set; }
        public int UsedSlots { get; internal set; }
    }

    public sealed class CapacitySnapshot
    {
        private static readonly ItemCapacity[] EmptyItems = new ItemCapacity[0];

        public CapacityStats Stats { get; internal set; }
        public int TotalSlots { get; internal set; }
        public int UsedSlots { get; internal set; }
        public int AvailableSlots { get; internal set; }
        public int CarryingCapacity { get; internal set; }
        public bool IsOverCapacity { get; internal set; }
        public TradeError Error { get; internal set; }
        public string InvalidItemId { get; internal set; }
        public IReadOnlyList<ItemCapacity> Items { get; internal set; }

        public CapacitySnapshot()
        {
            Items = EmptyItems;
            InvalidItemId = string.Empty;
        }

        public ItemCapacity FindItem(string itemId)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (string.Equals(Items[i].ItemId, itemId, StringComparison.Ordinal))
                    return Items[i];
            }
            return null;
        }
    }

    public sealed class CapacityService
    {
        public const int BaseSlots = 2;

        private readonly Func<CapacityStats> _statReader;

        public CapacityService()
            : this(ReadMortalStats)
        {
        }

        public CapacityService(Func<CapacityStats> statReader)
        {
            if (statReader == null)
                throw new ArgumentNullException("statReader");
            _statReader = statReader;
        }

        public CapacityStats GetStats()
        {
            return _statReader();
        }

        public CapacitySnapshot Evaluate(RunState state, IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            return Evaluate(state, definitions, GetStats());
        }

        public CapacitySnapshot Evaluate(RunState state, IReadOnlyDictionary<string, ItemDefinition> definitions, CapacityStats stats)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (definitions == null)
                throw new ArgumentNullException("definitions");

            Dictionary<string, long> longQuantities = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> shortQuantities = new Dictionary<string, long>(StringComparer.Ordinal);
            CapacitySnapshot invalid = null;

            if (state.Holdings != null)
            {
                for (int i = 0; i < state.Holdings.Count; i++)
                {
                    Holding holding = state.Holdings[i];
                    if (holding == null || holding.Quantity < 0)
                    {
                        invalid = CreateInvalid(TradeError.InvalidState, holding == null ? string.Empty : holding.ItemId, stats);
                        break;
                    }
                    if (holding.Quantity > 0)
                        AddQuantity(longQuantities, holding.ItemId, holding.Quantity);
                }
            }

            if (invalid == null && state.ShortBatches != null)
            {
                for (int i = 0; i < state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = state.ShortBatches[i];
                    if (batch == null || batch.Quantity < 0)
                    {
                        invalid = CreateInvalid(TradeError.InvalidState, batch == null ? string.Empty : batch.ItemId, stats);
                        break;
                    }
                    if (batch.Quantity > 0)
                        AddQuantity(shortQuantities, batch.ItemId, batch.Quantity);
                }
            }

            return invalid ?? EvaluateQuantities(longQuantities, shortQuantities, definitions, stats);
        }

        public static int GetBonusSlots(int life)
        {
            return life <= 0 ? 0 : 1 + (life - 1) / 10;
        }

        public static int GetTotalSlots(int life)
        {
            return BaseSlots + GetBonusSlots(life);
        }

        public static int GetCarryingCapacity(int lightness, int internalStrength)
        {
            long capacity = checked(((long)lightness + internalStrength) * 5L);
            if (capacity <= 0)
                return 0;
            return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        }

        public static int GetStackLimit(ItemDefinition item, int carryingCapacity)
        {
            if (item == null || carryingCapacity <= 0 || double.IsNaN(item.Weight) ||
                double.IsInfinity(item.Weight) || item.Weight <= 0.0)
                return 0;

            double limit = Math.Floor(carryingCapacity / item.Weight);
            if (limit <= 0.0)
                return 0;
            return limit >= int.MaxValue ? int.MaxValue : (int)limit;
        }

        internal CapacitySnapshot EvaluateQuantities(
            IDictionary<string, long> longQuantities,
            IDictionary<string, long> shortQuantities,
            IReadOnlyDictionary<string, ItemDefinition> definitions,
            CapacityStats stats)
        {
            CapacitySnapshot snapshot = new CapacitySnapshot
            {
                Stats = stats,
                TotalSlots = GetTotalSlots(stats.Life),
                CarryingCapacity = GetCarryingCapacity(stats.Lightness, stats.InternalStrength),
                Error = TradeError.None
            };

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> pair in longQuantities)
            {
                if (pair.Value > 0)
                    keys.Add(pair.Key);
            }
            foreach (KeyValuePair<string, long> pair in shortQuantities)
            {
                if (pair.Value > 0)
                    keys.Add(pair.Key);
            }

            List<string> itemIds = new List<string>(keys);
            itemIds.Sort(StringComparer.Ordinal);
            ItemCapacity[] items = new ItemCapacity[itemIds.Count];
            long usedSlots = 0;

            for (int i = 0; i < itemIds.Count; i++)
            {
                string itemId = itemIds[i];
                long longQuantity;
                long shortQuantity;
                longQuantities.TryGetValue(itemId, out longQuantity);
                shortQuantities.TryGetValue(itemId, out shortQuantity);

                ItemDefinition definition;
                if (!definitions.TryGetValue(itemId, out definition) || definition == null)
                {
                    snapshot.Error = TradeError.UnknownItem;
                    snapshot.InvalidItemId = itemId;
                }
                else if (double.IsNaN(definition.Weight) || double.IsInfinity(definition.Weight) || definition.Weight <= 0.0)
                {
                    snapshot.Error = TradeError.InvalidItemWeight;
                    snapshot.InvalidItemId = itemId;
                }

                int stackLimit = definition == null ? 0 : GetStackLimit(definition, snapshot.CarryingCapacity);
                long totalQuantity = longQuantity > long.MaxValue - shortQuantity ? long.MaxValue : longQuantity + shortQuantity;
                int itemSlots = GetSlots(totalQuantity, stackLimit);
                usedSlots = Math.Min(int.MaxValue, usedSlots + itemSlots);
                items[i] = new ItemCapacity
                {
                    ItemId = itemId,
                    LongQuantity = longQuantity,
                    ShortQuantity = shortQuantity,
                    StackLimit = stackLimit,
                    UsedSlots = itemSlots
                };
            }

            snapshot.Items = items;
            snapshot.UsedSlots = usedSlots >= int.MaxValue ? int.MaxValue : (int)usedSlots;
            snapshot.AvailableSlots = Math.Max(0, snapshot.TotalSlots - snapshot.UsedSlots);
            snapshot.IsOverCapacity = snapshot.UsedSlots > snapshot.TotalSlots;
            return snapshot;
        }

        private static CapacitySnapshot CreateInvalid(TradeError error, string itemId, CapacityStats stats)
        {
            return new CapacitySnapshot
            {
                Stats = stats,
                TotalSlots = GetTotalSlots(stats.Life),
                CarryingCapacity = GetCarryingCapacity(stats.Lightness, stats.InternalStrength),
                Error = error,
                InvalidItemId = itemId ?? string.Empty,
                IsOverCapacity = true
            };
        }

        private static int GetSlots(long quantity, int stackLimit)
        {
            if (quantity <= 0)
                return 0;
            if (stackLimit <= 0)
                return int.MaxValue;

            long slots = quantity / stackLimit;
            if (quantity % stackLimit != 0)
                slots++;
            return slots >= int.MaxValue ? int.MaxValue : (int)slots;
        }

        private static void AddQuantity(IDictionary<string, long> quantities, string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId))
                itemId = string.Empty;

            long current;
            quantities.TryGetValue(itemId, out current);
            quantities[itemId] = current + quantity;
        }

        private static CapacityStats ReadMortalStats()
        {
            PlayerStatManagerData manager = PlayerStatManagerData.Instance;
            if (manager == null || manager.Stats == null)
                throw new InvalidOperationException("The original player stats are unavailable.");

            return new CapacityStats(
                manager.Stats.Get(GameStatType.體力).FinalValue,
                manager.Stats.Get(GameStatType.輕功).FinalValue,
                manager.Stats.Get(GameStatType.內力).FinalValue);
        }
    }
}
