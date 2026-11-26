using System;
using System.Collections.Generic;
using XingShangMod.Domain;
using XingShangMod.Trading;

namespace XingShangMod.UI.Inventory
{
    internal sealed class InventorySlotModel
    {
        internal ItemDefinition Item;
        internal bool IsLong;
        internal long Quantity;
        internal long Basis;
        internal double CurrentPrice;
        internal bool PriceAvailable;
        internal bool Delisted;
        internal int DueWeek = -1;
        internal int RemainingWeeks = -1;
        internal int CurrentWeek = -1;
        internal int ShortBatchCount;
        internal int EarliestBatchCount;
        internal long EarliestDueQuantity;
        internal readonly List<ShortBatch> Batches = new List<ShortBatch>();

        internal long OtherBatchQuantity
        {
            get { return Math.Max(0, Quantity - EarliestDueQuantity); }
        }

        internal int OtherBatchCount
        {
            get { return Math.Max(0, ShortBatchCount - EarliestBatchCount); }
        }

        internal double Floating
        {
            get
            {
                if (!PriceAvailable)
                    return 0.0;
                return IsLong
                    ? CurrentPrice * Quantity - Basis
                    : Basis - CurrentPrice * Quantity;
            }
        }
    }

    internal sealed class InventoryCellModel
    {
        internal InventorySlotModel Position;
        internal InventorySlotModel SecondaryPosition;
        internal long Quantity;
        internal long SecondaryQuantity;
        internal long Basis;
        internal long SecondaryBasis;
        internal int SlotIndex;
        internal int SlotCount;
        internal int StackLimit;
        internal int ShortSliceBatchCount;
        internal int ShortSliceEarliestBatchCount;
        internal long ShortSliceEarliestQuantity;
        internal int ShortSliceDueWeek = -1;
        internal int ShortSliceRemainingWeeks = -1;
        internal readonly SortedDictionary<int, long> DueQuantities = new SortedDictionary<int, long>();

        internal long CellShortQuantity
        {
            get
            {
                if (IsMixed)
                    return SecondaryQuantity;
                return Position != null && !Position.IsLong ? Quantity : 0;
            }
        }

        internal long ShortSliceOtherQuantity
        {
            get { return Math.Max(0, CellShortQuantity - ShortSliceEarliestQuantity); }
        }

        internal int ShortSliceOtherBatchCount
        {
            get { return Math.Max(0, ShortSliceBatchCount - ShortSliceEarliestBatchCount); }
        }

        internal bool IsMixed
        {
            get { return Position != null && SecondaryPosition != null; }
        }

        internal bool PriceAvailable
        {
            get
            {
                return Position != null && Position.PriceAvailable &&
                    (SecondaryPosition == null || SecondaryPosition.PriceAvailable);
            }
        }

        internal double Floating
        {
            get
            {
                if (!PriceAvailable)
                    return 0.0;
                double result = Position.IsLong
                    ? Position.CurrentPrice * Quantity - Basis
                    : Basis - Position.CurrentPrice * Quantity;
                if (SecondaryPosition != null)
                {
                    result += SecondaryPosition.IsLong
                        ? SecondaryPosition.CurrentPrice * SecondaryQuantity - SecondaryBasis
                        : SecondaryBasis - SecondaryPosition.CurrentPrice * SecondaryQuantity;
                }
                return result;
            }
        }

        internal double FloatingRate
        {
            get
            {
                double basis = Basis + SecondaryBasis;
                return !PriceAvailable || basis <= 0.0 ? 0.0 : Floating / basis;
            }
        }
    }

    internal sealed class InventoryGridProjection
    {
        private readonly Dictionary<string, InventorySlotModel> _shorts =
            new Dictionary<string, InventorySlotModel>(StringComparer.Ordinal);
        private readonly HashSet<string> _handledShorts = new HashSet<string>(StringComparer.Ordinal);
        private CapacitySnapshot _capacity;

        internal List<InventoryCellModel> LongCells { get; private set; }
        internal List<InventoryCellModel> ShortCells { get; private set; }
        internal int OccupiedSlots { get; private set; }
        internal int AvailableSlots { get; private set; }
        internal int DisplayedSlots { get; private set; }
        internal bool HasExactCapacity { get; private set; }

        private InventoryGridProjection()
        {
            LongCells = new List<InventoryCellModel>();
            ShortCells = new List<InventoryCellModel>();
            HasExactCapacity = true;
        }

        internal static InventoryGridProjection Build(
            IList<InventorySlotModel> longs,
            IList<InventorySlotModel> shorts,
            CapacitySnapshot capacity)
        {
            InventoryGridProjection result = new InventoryGridProjection();
            if (capacity == null || capacity.Error != TradeError.None)
            {
                result.AddUncountedCells(longs);
                result.AddUncountedCells(shorts);
                result.HasExactCapacity = false;
                result.Finish();
                return result;
            }

            result._capacity = capacity;
            if (shorts != null)
            {
                for (int i = 0; i < shorts.Count; i++)
                {
                    InventorySlotModel model = shorts[i];
                    if (model != null && model.Item != null)
                        result._shorts[model.Item.ItemId ?? string.Empty] = model;
                }
            }
            if (longs != null)
            {
                for (int i = 0; i < longs.Count; i++)
                {
                    InventorySlotModel model = longs[i];
                    if (model == null)
                        continue;
                    string itemId = model.Item == null ? string.Empty : model.Item.ItemId ?? string.Empty;
                    InventorySlotModel shortModel;
                    result._shorts.TryGetValue(itemId, out shortModel);
                    result.ExpandPair(model, shortModel);
                    if (shortModel != null)
                        result._handledShorts.Add(itemId);
                }
            }
            if (shorts != null)
            {
                for (int i = 0; i < shorts.Count; i++)
                {
                    InventorySlotModel model = shorts[i];
                    if (model == null)
                        continue;
                    string itemId = model.Item == null ? string.Empty : model.Item.ItemId ?? string.Empty;
                    if (!result._handledShorts.Contains(itemId))
                        result.ExpandPair(null, model);
                }
            }
            result.Finish();
            return result;
        }

        private void ExpandPair(InventorySlotModel longModel, InventorySlotModel shortModel)
        {
            InventorySlotModel primary = longModel ?? shortModel;
            string itemId = primary == null || primary.Item == null
                ? string.Empty
                : primary.Item.ItemId ?? string.Empty;
            ItemCapacity item = _capacity.FindItem(itemId);
            if (item == null || item.StackLimit <= 0 || item.UsedSlots <= 0 || item.UsedSlots == int.MaxValue)
            {
                HasExactCapacity = false;
                AddUncountedCell(longModel);
                AddUncountedCell(shortModel);
                return;
            }

            long longRemaining = longModel == null ? 0 : longModel.Quantity;
            long shortRemaining = shortModel == null ? 0 : shortModel.Quantity;
            long longBasis = longModel == null ? 0 : longModel.Basis;
            long shortBasis = shortModel == null ? 0 : shortModel.Basis;
            int firstLongCell = LongCells.Count;
            int firstShortCell = ShortCells.Count;
            ShortSliceCursor shortCursor = new ShortSliceCursor(shortModel);
            int slot = 0;
            while (slot < item.UsedSlots && (longRemaining > 0 || shortRemaining > 0))
            {
                long longTake = Math.Min(longRemaining, item.StackLimit);
                long shortTake = Math.Min(shortRemaining, item.StackLimit - longTake);
                if (longTake <= 0 && shortTake <= 0)
                    break;

                InventoryCellModel cell = new InventoryCellModel
                {
                    Position = longTake > 0 ? longModel : shortModel,
                    SecondaryPosition = longTake > 0 && shortTake > 0 ? shortModel : null,
                    Quantity = longTake > 0 ? longTake : shortTake,
                    SecondaryQuantity = longTake > 0 && shortTake > 0 ? shortTake : 0,
                    Basis = longTake > 0
                        ? TakeBasis(longModel, ref longBasis, longTake, longRemaining)
                        : TakeBasis(shortModel, ref shortBasis, shortTake, shortRemaining),
                    SecondaryBasis = longTake > 0 && shortTake > 0
                        ? TakeBasis(shortModel, ref shortBasis, shortTake, shortRemaining)
                        : 0,
                    SlotIndex = slot + 1,
                    SlotCount = item.UsedSlots,
                    StackLimit = item.StackLimit
                };
                if (shortTake > 0)
                    shortCursor.Bind(cell, shortTake);
                AddOccupied(cell);
                longRemaining -= longTake;
                shortRemaining -= shortTake;
                slot++;
            }

            if (slot != item.UsedSlots || longRemaining != 0 || shortRemaining != 0)
                HasExactCapacity = false;
            for (int i = firstLongCell; i < LongCells.Count; i++)
                LongCells[i].SlotCount = Math.Max(slot, LongCells[i].SlotIndex);
            for (int i = firstShortCell; i < ShortCells.Count; i++)
                ShortCells[i].SlotCount = Math.Max(slot, ShortCells[i].SlotIndex);
        }

        private static long TakeBasis(
            InventorySlotModel model,
            ref long basisRemaining,
            long quantity,
            long quantityBefore)
        {
            if (model == null || quantity <= 0)
                return 0;
            long result = quantity == quantityBefore
                ? basisRemaining
                : (long)Math.Floor(model.Basis * (double)quantity / model.Quantity);
            result = model.Basis >= 0
                ? Math.Max(0, Math.Min(result, basisRemaining))
                : Math.Min(0, Math.Max(result, basisRemaining));
            basisRemaining -= result;
            return result;
        }

        private void AddUncountedCells(IList<InventorySlotModel> models)
        {
            if (models == null)
                return;
            for (int i = 0; i < models.Count; i++)
                AddUncountedCell(models[i]);
        }

        private void AddUncountedCell(InventorySlotModel model)
        {
            if (model == null)
                return;
            InventoryCellModel cell = new InventoryCellModel
            {
                Position = model,
                Quantity = model.Quantity,
                Basis = model.Basis,
                SlotIndex = 1,
                SlotCount = 1
            };
            if (!model.IsLong && model.Quantity > 0)
                new ShortSliceCursor(model).Bind(cell, model.Quantity);
            AddOccupied(cell);
        }

        private void AddOccupied(InventoryCellModel cell)
        {
            if (cell.Position != null && cell.Position.IsLong)
                LongCells.Add(cell);
            else
                ShortCells.Add(cell);
        }

        private void Finish()
        {
            OccupiedSlots = LongCells.Count + ShortCells.Count;
            int total = _capacity == null || !HasExactCapacity
                ? OccupiedSlots
                : Math.Max(_capacity.TotalSlots, OccupiedSlots);
            int empty = Math.Max(0, total - OccupiedSlots);
            AvailableSlots = empty;
            int longEmpty = Math.Min(empty, Math.Max(0, (total + 1) / 2 - LongCells.Count));
            for (int i = 0; i < longEmpty; i++)
                LongCells.Add(new InventoryCellModel());
            for (int i = longEmpty; i < empty; i++)
                ShortCells.Add(new InventoryCellModel());
            DisplayedSlots = LongCells.Count + ShortCells.Count;
        }
    }

    internal sealed class ShortSliceCursor
    {
        private readonly InventorySlotModel _model;
        private readonly List<ShortBatch> _batches;
        private int _index;
        private long _remaining;

        internal ShortSliceCursor(InventorySlotModel model)
        {
            _model = model;
            _batches = model == null ? new List<ShortBatch>() : new List<ShortBatch>(model.Batches);
            _batches.Sort(CompareBatches);
            if (_batches.Count > 0)
                _remaining = _batches[0].Quantity;
        }

        internal void Bind(InventoryCellModel cell, long quantity)
        {
            if (cell == null || quantity <= 0 || _model == null)
                return;
            if (_batches.Count == 0)
            {
                BindAggregate(cell, quantity);
                return;
            }

            while (quantity > 0 && _index < _batches.Count)
            {
                ShortBatch batch = _batches[_index];
                long take = Math.Min(quantity, _remaining);
                long dueQuantity;
                cell.DueQuantities.TryGetValue(batch.DueWeek, out dueQuantity);
                cell.DueQuantities[batch.DueWeek] = dueQuantity + take;
                if (cell.ShortSliceBatchCount == 0)
                {
                    cell.ShortSliceDueWeek = batch.DueWeek;
                    cell.ShortSliceRemainingWeeks = RemainingWeeks(batch.DueWeek, _model.CurrentWeek);
                }
                cell.ShortSliceBatchCount++;
                if (batch.DueWeek == cell.ShortSliceDueWeek)
                {
                    cell.ShortSliceEarliestQuantity += take;
                    cell.ShortSliceEarliestBatchCount++;
                }
                quantity -= take;
                _remaining -= take;
                if (_remaining > 0)
                    continue;
                _index++;
                if (_index < _batches.Count)
                    _remaining = _batches[_index].Quantity;
            }
        }

        private void BindAggregate(InventoryCellModel cell, long quantity)
        {
            cell.ShortSliceBatchCount = Math.Max(1, _model.ShortBatchCount);
            cell.ShortSliceEarliestBatchCount = Math.Max(1, _model.EarliestBatchCount);
            cell.ShortSliceEarliestQuantity = _model.EarliestDueQuantity > 0
                ? Math.Min(quantity, _model.EarliestDueQuantity)
                : quantity;
            cell.ShortSliceDueWeek = _model.DueWeek;
            cell.ShortSliceRemainingWeeks = _model.RemainingWeeks;
            cell.DueQuantities[_model.DueWeek] = cell.ShortSliceEarliestQuantity;
        }

        private static int RemainingWeeks(int dueWeek, int currentWeek)
        {
            return currentWeek < 0 || dueWeek < 0 ? -1 : Math.Max(0, dueWeek - currentWeek);
        }

        private static int CompareBatches(ShortBatch left, ShortBatch right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int result = left.DueWeek.CompareTo(right.DueWeek);
            if (result != 0)
                return result;
            result = left.OpenWeek.CompareTo(right.OpenWeek);
            return result != 0
                ? result
                : string.Compare(left.BatchId, right.BatchId, StringComparison.Ordinal);
        }
    }

    internal static class InventoryBatchProjection
    {
        internal static void Accumulate(InventorySlotModel model, ShortBatch batch, int currentWeek)
        {
            if (model == null || batch == null || batch.Quantity <= 0)
                return;
            model.Quantity += batch.Quantity;
            model.Basis += batch.Proceeds;
            model.CurrentWeek = currentWeek;
            model.Batches.Add(batch);
            model.ShortBatchCount++;
            if (model.ShortBatchCount == 1 || batch.DueWeek < model.DueWeek)
            {
                model.DueWeek = batch.DueWeek;
                model.EarliestDueQuantity = batch.Quantity;
                model.EarliestBatchCount = 1;
            }
            else if (batch.DueWeek == model.DueWeek)
            {
                model.EarliestDueQuantity += batch.Quantity;
                model.EarliestBatchCount++;
            }
            model.RemainingWeeks = currentWeek < 0 || model.DueWeek < 0
                ? -1
                : Math.Max(0, model.DueWeek - currentWeek);
        }
    }

}
