namespace XingShangMod.Trading
{
    public static class CartCapacityFeedback
    {
        public static string Rejection(
            TradeResult result,
            string itemId,
            string itemName,
            int quantity)
        {
            string name = string.IsNullOrEmpty(itemName) ? itemId ?? string.Empty : itemName;
            if (result == null)
                return "「" + name + "」本次 " + quantity + " 件未写入：行市尚未就绪，无法核验货囊。契匣未更改。";
            if (result.Success)
                return string.Empty;

            if (result.Error == TradeError.CapacityExceeded)
            {
                CapacitySnapshot capacity = result.CapacityAfter;
                ItemCapacity item = capacity == null ? null : capacity.FindItem(itemId);
                if (capacity == null)
                {
                    return "「" + name + "」本次 " + quantity +
                        " 件未写入：货囊格位或单格负重不足。契匣未更改。";
                }
                if (item != null && item.StackLimit <= 0)
                {
                    return "「" + name + "」本次 " + quantity + " 件未写入：当前负重 " +
                        capacity.CarryingCapacity + "，单件货物过重，每格可装 0 件。契匣未更改。";
                }

                int stackLimit = item == null ? 0 : item.StackLimit;
                return "「" + name + "」本次 " + quantity + " 件未写入：货囊共 " +
                    capacity.TotalSlots + " 格，现有仓位与整份契匣投影需 " + capacity.UsedSlots +
                    " 格；该货每格最多 " + stackLimit + " 件（当前负重 " +
                    capacity.CarryingCapacity + "）。契匣未更改，请减少数量或移除其它货签。";
            }

            if (result.Error == TradeError.PositionConflict)
            {
                return "「" + name + "」本次 " + quantity +
                    " 件未写入：同一商品不能同时持有多头和借货（含契匣待结算指令）。契匣未更改。";
            }
            if (result.Error == TradeError.InsufficientHolding)
            {
                return "「" + name + "」本次 " + quantity +
                    " 件未写入：现有持仓与契匣指令可卖出的数量不足。契匣未更改。";
            }
            if (result.Error == TradeError.InsufficientShortPosition)
            {
                return "「" + name + "」本次 " + quantity +
                    " 件未写入：现有借货与契匣指令可归还的数量不足。契匣未更改。";
            }

            return "「" + name + "」本次 " + quantity + " 件未写入：" +
                result.Message + " 契匣未更改。";
        }
    }
}
