using System;

namespace XingShangMod.Trading
{
    // A single economic order can occupy several slots. Prices always round on the whole order.
    internal struct CartStackLayout
    {
        internal readonly int Count;
        private readonly int _quantity;
        private readonly int _firstLimit;
        private readonly int _stackLimit;

        internal CartStackLayout(int quantity, int stackLimit, long held, bool opening)
        {
            _quantity = quantity;
            // A legacy or newly overweight order must remain visible and removable.
            _stackLimit = stackLimit > 0 ? stackLimit : Math.Max(1, quantity);
            int partial = (int)(Math.Max(0L, held) % _stackLimit);
            _firstLimit = partial == 0 ? _stackLimit : opening ? _stackLimit - partial : partial;
            long rest = Math.Max(0L, (long)quantity - _firstLimit);
            Count = quantity <= 0 ? 0 : (int)(1L + (rest + _stackLimit - 1L) / _stackLimit);
        }

        internal void GetRow(int index, out int offset, out int quantity, out int limit)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index");
            limit = index == 0 ? _firstLimit : _stackLimit;
            offset = index == 0 ? 0 : (int)(_firstLimit + (long)(index - 1) * _stackLimit);
            quantity = Math.Min(limit, _quantity - offset);
        }

        internal static bool TryAmount(double price, int quantity, bool borrow, out long amount)
        {
            return borrow
                ? TradingService.TryShortProceeds(price, quantity, out amount)
                : TradingService.TryWholeWen(price, quantity, out amount);
        }

        internal static bool TryRowAmount(double price, int offset, int quantity, bool borrow, out long amount)
        {
            long previous;
            long cumulative;
            amount = 0;
            if (!TryAmount(price, offset, borrow, out previous) ||
                !TryAmount(price, offset + quantity, borrow, out cumulative))
                return false;
            amount = cumulative - previous;
            return true;
        }
    }
}
