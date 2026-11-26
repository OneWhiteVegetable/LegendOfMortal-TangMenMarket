#if PRESENTATION_AUDIT
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using static AuditAssert;

internal sealed class InventoryExperienceTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string InventoryNamespace = "XingShangMod.UI.Inventory.";

    private static readonly Assembly RuntimeAssembly = LoadRuntimeAssembly();
    private static readonly Type SlotModelType = RuntimeType(InventoryNamespace + "InventorySlotModel");
    private static readonly Type ItemCapacityType = RuntimeType("XingShangMod.Trading.ItemCapacity");
    private static readonly Type ItemDefinitionType = RuntimeType("XingShangMod.Domain.ItemDefinition");
    private static readonly Type ShortBatchType = RuntimeType("XingShangMod.Domain.ShortBatch");
    private static readonly Type ProjectionType = RuntimeType(InventoryNamespace + "InventoryGridProjection");
    private static readonly Type BatchProjectionType = RuntimeType(InventoryNamespace + "InventoryBatchProjection");
    private static readonly Type SectionLayoutType = RuntimeType(InventoryNamespace + "InventorySectionLayout");
    private static readonly Type SummaryLayoutType = RuntimeType(InventoryNamespace + "InventorySummaryLayout");

    public void Register(TestRunner runner)
    {
        runner.Run("inventory-shared-cells-one-to-nineteen-and-beyond", TestSharedCellAllocation);
        runner.Run("inventory-empty-state-uses-occupied-not-projected-cells", TestEmptyStateProjection);
        runner.Run("inventory-sections-fill-parent-and-scroll-within-viewport", TestSectionLayout);
        runner.Run("inventory-summary-uses-explicit-responsive-grid", TestSummaryLayout);
        runner.Run("inventory-mixed-position-preserves-shared-stack-and-basis", TestMixedPosition);
        runner.Run("inventory-short-batches-are-sliced-by-real-cell", TestShortBatchSlices);
    }

    private static void TestSharedCellAllocation()
    {
        int[] capacities = new int[34];
        for (int i = 1; i <= 32; i++)
            capacities[i - 1] = i;
        capacities[32] = 64;
        capacities[33] = 199;
        for (int i = 0; i < capacities.Length; i++)
        {
            int total = capacities[i];
            object empty = Build(null, null, Capacity(total, 0));
            Equal(total, Int(empty, "DisplayedSlots"), "empty warehouse logical slots " + total);
            Equal(total, Count(Get(empty, "LongCells")) + Count(Get(empty, "ShortCells")), "empty cells are not duplicated " + total);
            Equal(0, Int(empty, "OccupiedSlots"), "empty warehouse occupied " + total);
            Equal(total, Int(empty, "AvailableSlots"), "empty warehouse available " + total);
            Equal((total + 1) / 2, Count(Get(empty, "LongCells")), "long-side empty allocation " + total);
            Equal(total / 2, Count(Get(empty, "ShortCells")), "short-side empty allocation " + total);

            object model = Model("shared", true, 1, 100);
            object occupied = Build(
                Models(model),
                null,
                Capacity(total, 1, Item("shared", 1, 0, 1, 1)));
            int emptyCount = total - 1;
            int longEmpty = Math.Min(emptyCount, Math.Max(0, (total + 1) / 2 - 1));
            Equal(1, Int(occupied, "OccupiedSlots"), "one real occupied cell " + total);
            Equal(emptyCount, Int(occupied, "AvailableSlots"), "real remaining cells " + total);
            Equal(total, Int(occupied, "DisplayedSlots"), "occupied plus empty equals total " + total);
            Equal(1 + longEmpty, Count(Get(occupied, "LongCells")), "long cells follow remaining shared capacity " + total);
            Equal(emptyCount - longEmpty, Count(Get(occupied, "ShortCells")), "short cells do not receive a second quota " + total);
            True(Bool(occupied, "HasExactCapacity"), "capacity projection must remain exact " + total);
        }
    }

    private static void TestEmptyStateProjection()
    {
        object empty = Build(null, null, Capacity(5, 0));
        Equal(5, Int(empty, "DisplayedSlots"), "empty warehouse still has physical cells");
        True(!InvokeBool("HasOccupiedCells", Get(empty, "LongCells")), "long empty state must ignore empty cells");
        True(!InvokeBool("HasOccupiedCells", Get(empty, "ShortCells")), "short empty state must ignore empty cells");

        object longOnly = Build(
            Models(Model("long", true, 1, 100)),
            null,
            Capacity(5, 1, Item("long", 1, 0, 1, 1)));
        True(InvokeBool("HasOccupiedCells", Get(longOnly, "LongCells")), "occupied long cell must hide empty state");
        True(!InvokeBool("HasOccupiedCells", Get(longOnly, "ShortCells")), "empty short section must remain empty");
    }

    private static void TestSectionLayout()
    {
        Equal(0, InvokeInt("RowCount", 0, 6), "empty row count");
        Equal(1, InvokeInt("RowCount", 1, 6), "single row");
        Equal(4, InvokeInt("RowCount", 19, 6), "nineteen cells on desktop");
        Equal(5, InvokeInt("RowCount", 19, 4), "nineteen cells on compact");

        Near(244, InvokeDouble("SectionHeight", 500f, false), "both sections share all remaining height");
        Near(500, 2 * InvokeDouble("SectionHeight", 500f, false) + 12, "desktop sections and their gap fill the parent");
        Near(246.5, InvokeDouble("SectionHeight", 500f, true), "compact sections use the V4 low-height gap");
        Near(500, 2 * InvokeDouble("SectionHeight", 500f, true) + 7, "compact sections and their gap fill the parent");
        Near(0, InvokeDouble("SectionHeight", 12f, false), "section gap cannot create negative heights");
        Near(592, InvokeDouble("ContentHeight", 19, 6, 142f), "desktop content keeps every row");
        Near(692, InvokeDouble("ContentHeight", 19, 4, 132f), "compact content keeps every row");

        Equal(2, InvokeInt("VisibleRows", 19, 6, 300f, 142f), "desktop visible rows come from viewport height");
        Equal(4, InvokeInt("VisibleRows", 19, 6, 592f, 142f), "a taller desktop viewport reveals all rows");
        Equal(2, InvokeInt("VisibleRows", 19, 4, 400f, 132f), "compact visible rows are not capped to one");
        Equal(5, InvokeInt("VisibleRows", 19, 4, 692f, 132f), "compact full-content viewport reveals all rows");
        Near(191, InvokeDouble("SectionHeight", 389f, true), "reduced-height compact sections retain a full card viewport");
        Equal(1, InvokeInt("VisibleRows", 4, 4, 133f, 132f), "a 133px compact viewport shows one complete row");
        True(!InvokeBool("NeedsScroll", 4, 4, 133f, 132f), "a complete compact row does not scroll");
        True(InvokeBool("NeedsScroll", 5, 4, 133f, 132f), "a second compact row scrolls inside its section");
        True(InvokeBool("NeedsScroll", 19, 6, 300f, 142f), "overflowing desktop content scrolls");
        True(!InvokeBool("NeedsScroll", 12, 6, 292f, 142f), "exactly two desktop rows need no scrolling");
        True(InvokeBool("NeedsScroll", 12, 6, 291f, 142f), "one pixel of overflow enables scrolling");
        Near(292, InvokeDouble("ScrollRange", 19, 6, 300f, 142f), "scroll range is content minus viewport");
        Near(1, InvokeDouble("ScrollRange", 12, 6, 291f, 142f), "scroll range preserves small overflow");
        Near(0, InvokeDouble("ScrollRange", 19, 6, 592f, 142f), "full-content viewport has no scroll range");
        True(!InvokeBool("NeedsScroll", 0, 6, 0f, 142f), "empty content never scrolls");

        Equal(17, InvokeInt("RowCount", 97, 6), "large inventories are not truncated by projection");
        Equal(17, InvokeInt("VisibleRows", 97, 6, 2542f, 142f), "all large-inventory rows remain reachable");
        True(!InvokeBool("NeedsScroll", 97, 6, 2542f, 142f), "large inventory only stops scrolling at its full boundary");
    }

    private static void TestSummaryLayout()
    {
        Near(63, InvokeSummaryDouble("Height", false), "summary reserves one row for financial values");
        Near(50, InvokeSummaryDouble("Height", true), "compact summary preserves the inventory sections");
        Near(7, InvokeSummaryDouble("GapAfterSummary", true), "compact spacing");
        Near(10, InvokeSummaryDouble("GapAfterSummary", false), "standard spacing");
    }

    private static void TestMixedPosition()
    {
        object longModel = Model("mixed", true, 4, 400);
        object shortModel = Model("mixed", false, 1, 97);
        object projection = Build(
            Models(longModel),
            Models(shortModel),
            Capacity(2, 2, Item("mixed", 4, 1, 3, 2)));
        Equal(2, Int(projection, "OccupiedSlots"), "mixed quantities share stack capacity");
        IList longCells = (IList)Get(projection, "LongCells");
        IList shortCells = (IList)Get(projection, "ShortCells");
        Equal(2, longCells.Count, "both physical cells remain visible in the long section");
        Equal(0, shortCells.Count, "mixed physical cell is not duplicated in the short section");
        Equal(3L, Long(longCells[0], "Quantity"), "first stack long quantity");
        Equal(300L, Long(longCells[0], "Basis"), "first stack cost basis");
        True(Bool(longCells[1], "IsMixed"), "last stack contains remaining long and short");
        Equal(1L, Long(longCells[1], "Quantity"), "last stack long quantity");
        Equal(1L, Long(longCells[1], "SecondaryQuantity"), "last stack short quantity");
        Equal(100L, Long(longCells[1], "Basis"), "last stack remaining long basis");
        Equal(97L, Long(longCells[1], "SecondaryBasis"), "last stack short proceeds");
    }

    private static void TestShortBatchSlices()
    {
        object model = Model("batches", false, 0, 0);
        Accumulate(model, Batch("later", 3, 10, 300), 2);
        Accumulate(model, Batch("soon", 2, 4, 200), 2);
        Accumulate(model, Batch("latest", 4, 20, 400), 2);
        Equal(9L, Long(model, "Quantity"), "aggregate short quantity");
        Equal(900L, Long(model, "Basis"), "aggregate short proceeds");
        Equal(4, Int(model, "DueWeek"), "aggregate earliest due week");
        Equal(2L, Long(model, "EarliestDueQuantity"), "aggregate earliest quantity");
        Equal(7L, Long(model, "OtherBatchQuantity"), "aggregate later quantity");
        Equal(2, Int(model, "OtherBatchCount"), "aggregate later batch count");

        object projection = Build(
            null,
            Models(model),
            Capacity(3, 3, Item("batches", 0, 9, 3, 3)));
        IList cells = (IList)Get(projection, "ShortCells");
        Equal(3, cells.Count, "nine items require three real cells");
        Equal(3L, Long(cells[0], "CellShortQuantity"), "first cell quantity");
        Equal(4, Int(cells[0], "ShortSliceDueWeek"), "first cell earliest due");
        Equal(2, Int(cells[0], "ShortSliceRemainingWeeks"), "first cell remaining weeks");
        Equal(2L, Long(cells[0], "ShortSliceEarliestQuantity"), "first cell earliest quantity");
        Equal(2, Int(cells[0], "ShortSliceBatchCount"), "first cell contains two batches");
        Equal(1L, Long(cells[0], "ShortSliceOtherQuantity"), "first cell later quantity");
        Equal(1, Int(cells[0], "ShortSliceOtherBatchCount"), "first cell later batch count");
        var firstDues = (System.Collections.Generic.SortedDictionary<int,long>)Get(cells[0],"DueQuantities");
        Equal(2,firstDues.Count,"first ticket lists both distinct deadlines");
        Equal(2L,firstDues[4],"first deadline quantity");
        Equal(1L,firstDues[10],"later deadline counts only this cell's quantity");

        Equal(10, Int(cells[1], "ShortSliceDueWeek"), "second cell earliest due");
        Equal(8, Int(cells[1], "ShortSliceRemainingWeeks"), "second cell remaining weeks");
        Equal(2L, Long(cells[1], "ShortSliceEarliestQuantity"), "second cell earliest quantity");
        Equal(2, Int(cells[1], "ShortSliceBatchCount"), "second cell contains two batches");
        Equal(1L, Long(cells[1], "ShortSliceOtherQuantity"), "second cell later quantity");

        Equal(20, Int(cells[2], "ShortSliceDueWeek"), "third cell due");
        Equal(18, Int(cells[2], "ShortSliceRemainingWeeks"), "third cell remaining weeks");
        Equal(3L, Long(cells[2], "ShortSliceEarliestQuantity"), "third cell has one complete batch slice");
        Equal(1, Int(cells[2], "ShortSliceBatchCount"), "third cell has one batch");
        Equal(0L, Long(cells[2], "ShortSliceOtherQuantity"), "third cell has no later quantity");
    }

    private static object Build(object longs, object shorts, object capacity)
    {
        return InvokeStatic(ProjectionType, "Build", longs, shorts, capacity);
    }

    private static void Accumulate(object model, object batch, int currentWeek)
    {
        InvokeStatic(BatchProjectionType, "Accumulate", model, batch, currentWeek);
    }

    private static object Models(params object[] models)
    {
        IList result = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(SlotModelType));
        for (int i = 0; i < models.Length; i++)
            result.Add(models[i]);
        return result;
    }

    private static object Model(string itemId, bool isLong, long quantity, long basis)
    {
        object definition = Activator.CreateInstance(ItemDefinitionType);
        Set(definition, "ItemId", itemId);
        Set(definition, "Name", itemId);
        Set(definition, "Weight", 10.0);
        object model = Activator.CreateInstance(SlotModelType, true);
        Set(model, "Item", definition);
        Set(model, "IsLong", isLong);
        Set(model, "Quantity", quantity);
        Set(model, "Basis", basis);
        Set(model, "CurrentPrice", 100.0);
        Set(model, "PriceAvailable", true);
        return model;
    }

    private static object Batch(string batchId, int quantity, int dueWeek, long proceeds)
    {
        object batch = Activator.CreateInstance(ShortBatchType);
        Set(batch, "BatchId", batchId);
        Set(batch, "ItemId", "batches");
        Set(batch, "Quantity", quantity);
        Set(batch, "DueWeek", dueWeek);
        Set(batch, "Proceeds", proceeds);
        return batch;
    }

    private static object Item(
        string itemId,
        long longQuantity,
        long shortQuantity,
        int stackLimit,
        int usedSlots)
    {
        object item = Activator.CreateInstance(ItemCapacityType);
        Set(item, "ItemId", itemId);
        Set(item, "LongQuantity", longQuantity);
        Set(item, "ShortQuantity", shortQuantity);
        Set(item, "StackLimit", stackLimit);
        Set(item, "UsedSlots", usedSlots);
        return item;
    }

    private static object Capacity(int total, int used, params object[] items)
    {
        Type type = RuntimeType("XingShangMod.Trading.CapacitySnapshot");
        object capacity = Activator.CreateInstance(type);
        Set(capacity, "TotalSlots", total);
        Set(capacity, "UsedSlots", used);
        Set(capacity, "AvailableSlots", Math.Max(0, total - used));
        Set(capacity, "IsOverCapacity", used > total);
        Set(capacity, "Error", Enum.Parse(RuntimeType("XingShangMod.Trading.TradeError"), "None"));
        Array typedItems = Array.CreateInstance(ItemCapacityType, items.Length);
        for (int i = 0; i < items.Length; i++)
            typedItems.SetValue(items[i], i);
        Set(capacity, "Items", typedItems);
        return capacity;
    }

    private static int InvokeInt(string name, params object[] arguments)
    {
        return Convert.ToInt32(InvokeStatic(SectionLayoutType, name, arguments));
    }

    private static double InvokeDouble(string name, params object[] arguments)
    {
        return Convert.ToDouble(InvokeStatic(SectionLayoutType, name, arguments));
    }

    private static double InvokeSummaryDouble(string name, params object[] arguments)
    {
        return Convert.ToDouble(InvokeStatic(SummaryLayoutType, name, arguments));
    }


    private static bool InvokeBool(string name, params object[] arguments)
    {
        return Convert.ToBoolean(InvokeStatic(SectionLayoutType, name, arguments));
    }

    private static object InvokeStatic(Type type, string name, params object[] arguments)
    {
        MethodInfo method = type.GetMethod(name, StaticMembers);
        if (method == null)
            throw new MissingMethodException(type.FullName, name);
        return method.Invoke(null, arguments);
    }

    private static object Get(object target, string name)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null)
            return field.GetValue(target);
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null)
            return property.GetValue(target, null);
        throw new MissingMemberException(type.FullName, name);
    }

    private static void Set(object target, string name, object value)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null)
        {
            property.SetValue(target, value, null);
            return;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static int Int(object target, string name)
    {
        return Convert.ToInt32(Get(target, name));
    }

    private static long Long(object target, string name)
    {
        return Convert.ToInt64(Get(target, name));
    }

    private static bool Bool(object target, string name)
    {
        return Convert.ToBoolean(Get(target, name));
    }

    private static int Count(object value)
    {
        return ((IList)value).Count;
    }

    private static Type RuntimeType(string name)
    {
        Type type = RuntimeAssembly.GetType(name, false);
        if (type == null)
            throw new TypeLoadException("Runtime type is missing: " + name);
        return type;
    }

    private static Assembly LoadRuntimeAssembly()
    {
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "bin/Release/XingShangMod.dll"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../bin/Release/XingShangMod.dll"))
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
                return Assembly.LoadFrom(candidates[i]);
        }
        throw new FileNotFoundException("Build bin/Release/XingShangMod.dll before running inventory experience tests.");
    }
}
#endif
