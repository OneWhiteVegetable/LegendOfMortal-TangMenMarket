using System;
using System.Collections.Generic;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;

namespace XingShangMod.Trading
{
    public sealed class ShortService
    {
        public const int DurationMonths = 6;
        public const int DurationWeeks = DurationMonths * MarketClock.WeeksPerGameMonth;
        // M7003_01 starts the final battle at year 4, month 3, upper stage.
        internal const int FinalBattleMonth = (4 - 1) * 12 + 3;
        internal const int FirstClosedMonth = FinalBattleMonth - DurationMonths;
        internal const string ClosedMessage = "为预留还货期，第三年九月起停开新借契。旧契可提前归还，到期自动买回。";

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
            HashSet<string> overflowedItemIds = new HashSet<string>(StringComparer.Ordinal);
            if (_state.ShortBatches != null)
            {
                for (int i = 0; i < _state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = _state.ShortBatches[i];
                    if (batch == null || batch.Quantity <= 0 || batch.DueWeek > currentWeek ||
                        overflowedItemIds.Contains(batch.ItemId))
                        continue;

                    long quantity;
                    dueQuantities.TryGetValue(batch.ItemId, out quantity);
                    try
                    {
                        dueQuantities[batch.ItemId] = checked(quantity + batch.Quantity);
                    }
                    catch (OverflowException)
                    {
                        dueQuantities.Remove(batch.ItemId);
                        overflowedItemIds.Add(batch.ItemId);
                    }
                }
            }

            List<string> itemIds = new List<string>(dueQuantities.Keys);
            itemIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < itemIds.Count; i++)
            {
                IReadOnlyList<TradeResult> itemResults = SettleInChunks(
                    itemIds[i], dueQuantities[itemIds[i]], false);
                for (int j = 0; j < itemResults.Count; j++)
                    results.Add(itemResults[j]);
            }

            List<string> failedItemIds = new List<string>(overflowedItemIds);
            failedItemIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < failedItemIds.Count; i++)
                results.Add(AutomaticFailure(TradeError.InvalidQuantity, failedItemIds[i]));
            return results;
        }

        public IReadOnlyList<TradeResult> SettleDelistedBatches(string itemId)
        {
            List<TradeResult> results = new List<TradeResult>();
            if (string.IsNullOrEmpty(itemId))
            {
                results.Add(AutomaticFailure(TradeError.InvalidRequest, itemId));
                return results;
            }
            if (_state.Clock == null)
            {
                results.Add(AutomaticFailure(TradeError.InvalidState, itemId));
                return results;
            }

            long quantity = 0;
            if (_state.ShortBatches != null)
            {
                for (int i = 0; i < _state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = _state.ShortBatches[i];
                    if (batch == null || batch.Quantity <= 0 ||
                        !string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                        continue;
                    try
                    {
                        quantity = checked(quantity + batch.Quantity);
                    }
                    catch (OverflowException)
                    {
                        results.Add(AutomaticFailure(TradeError.InvalidQuantity, itemId));
                        return results;
                    }
                }
            }

            return SettleInChunks(itemId, quantity, true);
        }

        private IReadOnlyList<TradeResult> SettleInChunks(string itemId, long quantity, bool delisted)
        {
            List<TradeResult> results = new List<TradeResult>();
            long remaining = quantity;
            while (remaining > 0)
            {
                int part = remaining > int.MaxValue ? int.MaxValue : (int)remaining;
                TradeResult result = delisted
                    ? _tradingService.ExecuteDelistedShort(itemId, part)
                    : _tradingService.ExecuteAutomatic(new[]
                    {
                        new TradeOrder(TradeAction.Return, itemId, part)
                    });
                results.Add(result);
                if (!result.Success)
                    break;
                remaining -= part;
            }
            return results;
        }

        public bool TryRepayDebtFromCash(out long repaid)
        {
            repaid = 0;
            long balance = _wallet.Balance;
            if (_state.Debt < 0 || balance < MortalMoneyLimit.Minimum)
                return false;

            // Only an unrepresentably large pre-signed-wallet debt can remain here.
            long payment = Math.Min(_state.Debt, balance - MortalMoneyLimit.Minimum);
            if (payment <= 0)
                return true;
            if (!WalletChange.TryApply(_wallet, balance, -payment))
                return false;

            _state.Debt -= payment;
            repaid = payment;
            return true;
        }

        private TradeResult AutomaticFailure(TradeError error, string itemId)
        {
            string message = "当前行市状态无效，暂不能自动平仓。";
            if (error == TradeError.InvalidQuantity)
                message = "到期借货数量超出可结算范围。";
            else if (error == TradeError.InvalidRequest)
                message = "退市结算请求无效。";
            TradeResult result = TradeResult.Fail(error, message, itemId);
            result.CashAfter = _wallet.Balance;
            result.DebtAfter = _state.Debt;
            return result;
        }
    }
}
