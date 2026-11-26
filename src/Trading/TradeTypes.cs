using System;
using System.Collections.Generic;
using XingShangMod.Domain;

namespace XingShangMod.Trading
{
    public enum TradeAction
    {
        Buy,
        Sell,
        Borrow,
        Return
    }

    public enum TradeError
    {
        None,
        InvalidRequest,
        InvalidState,
        UnknownItem,
        InvalidQuantity,
        PriceUnavailable,
        InvalidPrice,
        Delisted,
        InsufficientCash,
        InsufficientHolding,
        InsufficientShortPosition,
        PositionConflict,
        CapacityExceeded,
        InvalidItemWeight,
        CashOverflow,
        WalletRejected,
        BorrowWindowClosed
    }

    public sealed class TradeOrder
    {
        public TradeAction Action { get; private set; }
        public string ItemId { get; private set; }
        public int Quantity { get; private set; }

        public TradeOrder(TradeAction action, string itemId, int quantity)
        {
            Action = action;
            ItemId = itemId;
            Quantity = quantity;
        }
    }

    public static class TradeActionNames
    {
        public const string Buy = "Buy";
        public const string Sell = "Sell";
        public const string Borrow = "Borrow";
        public const string Return = "Return";
        public const string Expire = "Expire";
    }

    public sealed class TradeResult
    {
        private static readonly TradeRecord[] EmptyRecords = new TradeRecord[0];

        public bool Success { get; internal set; }
        public TradeError Error { get; internal set; }
        public string Message { get; internal set; }
        public string ItemId { get; internal set; }
        public long RequiredAmount { get; internal set; }
        public long AvailableAmount { get; internal set; }
        public long GrossCashDelta { get; internal set; }
        public long CashDelta { get; internal set; }
        public long DebtDelta { get; internal set; }
        public long CashAfter { get; internal set; }
        public long DebtAfter { get; internal set; }
        public CapacitySnapshot CapacityBefore { get; internal set; }
        public CapacitySnapshot CapacityAfter { get; internal set; }
        public IReadOnlyList<TradeRecord> Records { get; internal set; }

        internal static TradeResult Ok()
        {
            return new TradeResult
            {
                Success = true,
                Error = TradeError.None,
                Message = string.Empty,
                ItemId = string.Empty,
                Records = EmptyRecords
            };
        }

        internal static TradeResult Fail(TradeError error, string message, string itemId)
        {
            return new TradeResult
            {
                Success = false,
                Error = error,
                Message = message,
                ItemId = itemId ?? string.Empty,
                Records = EmptyRecords
            };
        }
    }
}
