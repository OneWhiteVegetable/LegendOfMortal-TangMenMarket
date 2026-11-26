using System;
using System.Collections.Generic;

namespace XingShangMod.UI.Inventory
{
    internal static class InventorySectionLayout
    {
        private const float RowSpacing = 8f;

        internal static bool HasOccupiedCells(IList<InventoryCellModel> cells)
        {
            if (cells == null)
                return false;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null && cells[i].Position != null)
                    return true;
            }
            return false;
        }

        internal static int RowCount(int cellCount, int columns)
        {
            columns = Math.Max(1, columns);
            return cellCount <= 0 ? 0 : (cellCount + columns - 1) / columns;
        }

        internal static float Spacing(bool compact)
        {
            return compact ? 7f : 12f;
        }

        internal static float SectionHeight(float availableHeight, bool compact)
        {
            return Math.Max(0f, availableHeight - Spacing(compact)) / 2f;
        }

        internal static int VisibleRows(int cellCount, int columns, float viewportHeight, float cellHeight)
        {
            int rows = RowCount(cellCount, columns);
            if (rows == 0 || viewportHeight <= 0f || cellHeight <= 0f)
                return 0;
            int visible = (int)Math.Floor((viewportHeight + RowSpacing) / (cellHeight + RowSpacing) + 0.0001f);
            return Math.Min(rows, Math.Max(0, visible));
        }

        internal static float ContentHeight(int cellCount, int columns, float cellHeight)
        {
            int rows = RowCount(cellCount, columns);
            if (rows == 0)
                return 0f;
            return rows * Math.Max(0f, cellHeight) + (rows - 1) * RowSpacing;
        }

        internal static float ScrollRange(int cellCount, int columns, float viewportHeight, float cellHeight)
        {
            return Math.Max(0f, ContentHeight(cellCount, columns, cellHeight) - Math.Max(0f, viewportHeight));
        }

        internal static bool NeedsScroll(int cellCount, int columns, float viewportHeight, float cellHeight)
        {
            return ScrollRange(cellCount, columns, viewportHeight, cellHeight) > 0.01f;
        }
    }

    internal static class InventorySummaryLayout
    {
        internal static float Height(bool compact)
        {
            return compact ? 50f : 63f;
        }

        internal static float GapAfterSummary(bool compact)
        {
            return compact ? 7f : 10f;
        }

    }
}
