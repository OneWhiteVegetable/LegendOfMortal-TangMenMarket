using System;
using System.Collections.Generic;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Trading
{
    public sealed class ShortService
    {
        public const int DurationWeeks = 24;

        private readonly TradingService _tradingService;
        private readonly RunState _state;
        private readonly IPlayerWallet _wallet;

        public ShortService(TradingService tradingService)
        {
            if (tradingService == null)
                throw new ArgumentNullException("tradingService");
            _tradingService = tradingService;
            _state = tradingService.State;
            _wallet = tradingService.Wallet;
        }

        public TradeResult Open(string itemId, int quantity)
        {
            return _tradingService.Execute(new TradeOrder(TradeAction.Borrow, itemId, quantity));
        }

        public TradeResult Repay(string itemId, int quantity)
        {
            return _tradingService.Execute(new TradeOrder(TradeAction.Return, itemId, quantity));
        }

        public int GetNextDueWeek(string itemId)
        {
            int nextDueWeek = -1;
            if (_state.ShortBatches == null)
                return nextDueWeek;

            for (int i = 0; i < _state.ShortBatches.Count; i++)
            {
                ShortBatch batch = _state.ShortBatches[i];
                if (batch == null || batch.Quantity <= 0 ||
                    !string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    continue;
                if (nextDueWeek < 0 || batch.DueWeek < nextDueWeek)
                    nextDueWeek = batch.DueWeek;
            }
            return nextDueWeek;
        }

        public int GetWeeksRemaining(string itemId)
        {
            if (_state.Clock == null)
                return -1;
            int dueWeek = GetNextDueWeek(itemId);
            return dueWeek < 0 ? -1 : Math.Max(0, dueWeek - _state.Clock.MarketWeekIndex);
        }

        public IReadOnlyList<TradeResult> SettleDueBatches()
        {
            List<TradeResult> results = new List<TradeResult>();
            if (_state.Clock == null)
            {
                results.Add(AutomaticFailure(TradeError.InvalidState, string.Empty));
                return results;
            }

            int currentWeek = _state.Clock.MarketWeekIndex;
            Dictionary<string, long> dueQuantities = new Dictionary<string, long>(StringComparer.Ordinal);
            if (_state.ShortBatches != null)
            {
                for (int i = 0; i < _state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = _state.ShortBatches[i];
                    if (batch == null || batch.Quantity <= 0 || batch.DueWeek > currentWeek)
                        continue;

                    long quantity;
                    dueQuantities.TryGetValue(batch.ItemId, out quantity);
                    try
                    {
                        dueQuantities[batch.ItemId] = checked(quantity + batch.Quantity);
                    }
                    catch (OverflowException)
                    {
                        results.Add(AutomaticFailure(TradeError.InvalidQuantity, batch.ItemId));
                    }
                }
            }

            List<string> itemIds = new List<string>(dueQuantities.Keys);
            itemIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < itemIds.Count; i++)
            {
                string itemId = itemIds[i];
                long quantity = dueQuantities[itemId];
                if (quantity > int.MaxValue)
                {
                    results.Add(AutomaticFailure(TradeError.InvalidQuantity, itemId));
                    continue;
                }
                results.Add(_tradingService.ExecuteAutomatic(new[]
                {
                    new TradeOrder(TradeAction.Return, itemId, (int)quantity)
                }));
            }
            return results;
        }

        public bool TryRepayDebtFromCash(out long repaid)
        {
            repaid = 0;
            if (_state.Debt < 0 || _wallet.Balance < 0)
                return false;

            long payment = Math.Min(_state.Debt, _wallet.Balance);
            if (payment <= 0)
                return true;
            if (!_wallet.TrySpend(payment))
                return false;

            _state.Debt -= payment;
            repaid = payment;
            return true;
        }

        private TradeResult AutomaticFailure(TradeError error, string itemId)
        {
            TradeResult result = TradeResult.Fail(error,
                error == TradeError.InvalidQuantity ? "到期借货数量超出可结算范围。" : "当前行市状态无效，暂不能自动平仓。",
                itemId);
            result.CashAfter = _wallet.Balance;
            result.DebtAfter = _state.Debt;
            return result;
        }
    }
}
