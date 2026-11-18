using System;
using System.Collections.Generic;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Trading
{
    public sealed class TradingService
    {
        private readonly RunState _state;
        private readonly IPlayerWallet _wallet;
        private readonly IReadOnlyDictionary<string, ItemDefinition> _items;
        private readonly CapacityService _capacityService;

        internal RunState State { get { return _state; } }
        internal IPlayerWallet Wallet { get { return _wallet; } }

        public TradingService(
            RunState state,
            IPlayerWallet wallet,
            IReadOnlyDictionary<string, ItemDefinition> items)
            : this(state, wallet, items, new CapacityService())
        {
        }

        public TradingService(
            RunState state,
            IPlayerWallet wallet,
            IReadOnlyDictionary<string, ItemDefinition> items,
            CapacityService capacityService)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            if (items == null)
                throw new ArgumentNullException("items");
            if (capacityService == null)
                throw new ArgumentNullException("capacityService");

            _state = state;
            _wallet = wallet;
            _items = items;
            _capacityService = capacityService;
        }

        public TradeResult Preview(TradeOrder order)
        {
            return Preview(order == null ? null : new[] { order });
        }

        public TradeResult Preview(IEnumerable<TradeOrder> orders)
        {
            return ExecuteCore(orders, false, false, false);
        }

        public TradeResult Execute(TradeOrder order)
        {
            return Execute(order == null ? null : new[] { order });
        }

        public TradeResult Execute(IEnumerable<TradeOrder> orders)
        {
            return ExecuteCore(orders, false, false, true);
        }

        internal TradeResult ExecuteAutomatic(IEnumerable<TradeOrder> orders)
        {
            return ExecuteCore(orders, true, true, true);
        }

        private TradeResult ExecuteCore(IEnumerable<TradeOrder> orders, bool allowDebt, bool automatic, bool commit)
        {
            long balance = _wallet.Balance;
            if (balance < 0 || _state.Debt < 0 || _state.Clock == null)
                return Fail(TradeError.InvalidState, string.Empty);
            if (orders == null)
                return Fail(TradeError.InvalidRequest, string.Empty);

            List<TradeOrder> orderList = new List<TradeOrder>();
            foreach (TradeOrder order in orders)
            {
                if (order == null)
                    return Fail(TradeError.InvalidRequest, string.Empty);
                orderList.Add(order);
            }
            if (orderList.Count == 0)
                return Fail(TradeError.InvalidRequest, string.Empty);

            for (int i = 0; i < orderList.Count; i++)
            {
                TradeOrder order = orderList[i];
                if (string.IsNullOrEmpty(order.ItemId))
                    return Fail(TradeError.InvalidRequest, string.Empty);
                if (order.Quantity <= 0)
                    return Fail(TradeError.InvalidQuantity, order.ItemId);
                if (order.Action != TradeAction.Buy && order.Action != TradeAction.Sell &&
                    order.Action != TradeAction.Borrow && order.Action != TradeAction.Return)
                    return Fail(TradeError.InvalidRequest, order.ItemId);

                ItemDefinition definition;
                if (!_items.TryGetValue(order.ItemId, out definition) || definition == null)
                    return Fail(TradeError.UnknownItem, order.ItemId);
            }

            Simulation simulation = new Simulation();
            TradeError initializationError;
            string initializationItem;
            if (!TryInitialize(simulation, out initializationError, out initializationItem))
                return Fail(initializationError, initializationItem);

            CapacityStats stats;
            try
            {
                stats = _capacityService.GetStats();
            }
            catch (InvalidOperationException)
            {
                return Fail(TradeError.InvalidState, string.Empty);
            }

            CapacitySnapshot before = _capacityService.Evaluate(_state, _items, stats);
            if (before.Error != TradeError.None)
                return Fail(before.Error, before.InvalidItemId);

            bool hasIncrease = false;
            int currentWeek = _state.Clock.MarketWeekIndex;

            try
            {
                for (int i = 0; i < orderList.Count; i++)
                {
                    TradeOrder order = orderList[i];
                    WeeklyPoint point;
                    TradeError priceError = GetCurrentPoint(order.ItemId, out point);
                    if (priceError != TradeError.None)
                        return Fail(priceError, order.ItemId);

                    long amount;
                    switch (order.Action)
                    {
                        case TradeAction.Buy:
                            if (!TryWholeWen(point.Price, order.Quantity, out amount))
                                return Fail(TradeError.CashOverflow, order.ItemId);
                            hasIncrease = true;
                            AddLong(simulation, order.ItemId, order.Quantity, amount);
                            AddRecord(simulation, order.ItemId, TradeActionNames.Buy, string.Empty,
                                order.Quantity, point, -amount);
                            break;

                        case TradeAction.Sell:
                            if (!RemoveLong(simulation, order.ItemId, order.Quantity, out amount))
                                return Fail(TradeError.InsufficientHolding, order.ItemId, order.Quantity, amount);
                            if (!TryWholeWen(point.Price, order.Quantity, out long saleProceeds))
                                return Fail(TradeError.CashOverflow, order.ItemId);
                            AddRecord(simulation, order.ItemId, TradeActionNames.Sell, string.Empty,
                                order.Quantity, point, saleProceeds);
                            break;

                        case TradeAction.Borrow:
                            if (!TryShortProceeds(point.Price, order.Quantity, out amount))
                                return Fail(TradeError.CashOverflow, order.ItemId);
                            hasIncrease = true;
                            AddShort(simulation, order.ItemId, order.Quantity, amount, currentWeek);
                            AddRecord(simulation, order.ItemId, TradeActionNames.Borrow, simulation.LastBatchId,
                                order.Quantity, point, amount);
                            break;

                        case TradeAction.Return:
                            long shortQuantity = simulation.GetShortQuantity(order.ItemId);
                            if (shortQuantity < order.Quantity)
                                return Fail(TradeError.InsufficientShortPosition, order.ItemId, order.Quantity, shortQuantity);
                            if (!TryWholeWen(point.Price, order.Quantity, out amount))
                                return Fail(TradeError.CashOverflow, order.ItemId);
                            RemoveShort(simulation, order.ItemId, order.Quantity, amount, point, automatic);
                            break;
                    }
                }

                string conflictItem;
                if (simulation.HasPositionConflict(out conflictItem))
                    return Fail(TradeError.PositionConflict, conflictItem);

                foreach (KeyValuePair<string, long> pair in simulation.LongQuantities)
                {
                    if (pair.Value > int.MaxValue)
                        return Fail(TradeError.InvalidQuantity, pair.Key);
                }
            }
            catch (OverflowException)
            {
                return Fail(TradeError.CashOverflow, string.Empty);
            }

            Dictionary<string, long> shortQuantities = simulation.GetShortQuantities();
            CapacitySnapshot after = _capacityService.EvaluateQuantities(
                simulation.LongQuantities, shortQuantities, _items, stats);
            if (after.Error != TradeError.None)
                return Fail(after.Error, after.InvalidItemId, before, after);

            if ((before.IsOverCapacity && hasIncrease) ||
                (after.IsOverCapacity && (!before.IsOverCapacity || after.UsedSlots > before.UsedSlots)))
            {
                TradeResult capacityFailure = Fail(TradeError.CapacityExceeded, string.Empty, before, after);
                capacityFailure.RequiredAmount = after.UsedSlots;
                capacityFailure.AvailableAmount = after.TotalSlots;
                return capacityFailure;
            }

            long positiveCash = 0;
            for (int i = 0; i < simulation.Records.Count; i++)
            {
                if (simulation.Records[i].GrossCashDelta > 0)
                {
                    try
                    {
                        positiveCash = checked(positiveCash + simulation.Records[i].GrossCashDelta);
                    }
                    catch (OverflowException)
                    {
                        return Fail(TradeError.CashOverflow, simulation.Records[i].ItemId, before, after);
                    }
                }
            }

            long debtPayment = Math.Min(_state.Debt, positiveCash);
            long cashBeforeNewDebt;
            try
            {
                cashBeforeNewDebt = checked(simulation.GrossCashDelta - debtPayment);
            }
            catch (OverflowException)
            {
                return Fail(TradeError.CashOverflow, string.Empty, before, after);
            }

            long cashDelta;
            long newDebt = 0;
            long required = 0;
            if (cashBeforeNewDebt < 0)
            {
                try
                {
                    required = checked(-cashBeforeNewDebt);
                }
                catch (OverflowException)
                {
                    return Fail(TradeError.CashOverflow, string.Empty, before, after);
                }

                if (balance < required)
                {
                    if (!allowDebt)
                    {
                        TradeResult cashFailure = Fail(TradeError.InsufficientCash, string.Empty, before, after);
                        cashFailure.RequiredAmount = required;
                        cashFailure.AvailableAmount = balance;
                        return cashFailure;
                    }
                    newDebt = required - balance;
                    cashDelta = -balance;
                }
                else
                {
                    cashDelta = cashBeforeNewDebt;
                }
            }
            else
            {
                cashDelta = cashBeforeNewDebt;
            }

            long debtDelta;
            long finalDebt;
            long finalCash;
            try
            {
                debtDelta = checked(newDebt - debtPayment);
                finalDebt = checked(_state.Debt + debtDelta);
                finalCash = checked(balance + cashDelta);
            }
            catch (OverflowException)
            {
                return Fail(TradeError.CashOverflow, string.Empty, before, after);
            }
            if (finalDebt < 0 || finalCash < 0)
                return Fail(TradeError.InvalidState, string.Empty, before, after);

            if (cashDelta < 0 && !_wallet.CanSpend(-cashDelta))
                return Fail(TradeError.WalletRejected, string.Empty, before, after);
            if (cashDelta > 0 && !_wallet.CanDeposit(cashDelta))
                return Fail(TradeError.CashOverflow, string.Empty, before, after);

            TradeRecord[] records = CreateRecords(simulation, currentWeek, debtPayment, newDebt, commit);
            TradeResult result = TradeResult.Ok();
            result.GrossCashDelta = simulation.GrossCashDelta;
            result.CashDelta = cashDelta;
            result.DebtDelta = debtDelta;
            result.CashAfter = finalCash;
            result.DebtAfter = finalDebt;
            result.RequiredAmount = required;
            result.AvailableAmount = balance;
            result.CapacityBefore = before;
            result.CapacityAfter = after;
            result.Records = records;

            if (!commit)
                return result;

            List<Holding> committedHoldings = BuildHoldings(simulation);
            List<ShortBatch> committedBatches = BuildBatches(simulation);
            List<TradeRecord> committedHistory = _state.TradeHistory == null
                ? new List<TradeRecord>()
                : new List<TradeRecord>(_state.TradeHistory);
            committedHistory.AddRange(records);

            bool walletChanged = false;
            if (cashDelta < 0)
            {
                if (!_wallet.TrySpend(-cashDelta))
                    return Fail(TradeError.WalletRejected, string.Empty, before, after);
                walletChanged = true;
            }
            else if (cashDelta > 0)
            {
                if (!_wallet.TryDeposit(cashDelta))
                    return Fail(TradeError.CashOverflow, string.Empty, before, after);
                walletChanged = true;
            }

            if (walletChanged && _wallet.Balance != finalCash)
                throw new InvalidOperationException("The wallet did not apply the validated cash delta exactly.");

            _state.Holdings = committedHoldings;
            _state.ShortBatches = committedBatches;
            _state.TradeHistory = committedHistory;
            _state.Debt = finalDebt;
            return result;
        }

        private bool TryInitialize(Simulation simulation, out TradeError error, out string itemId)
        {
            error = TradeError.None;
            itemId = string.Empty;

            if (_state.Holdings != null)
            {
                for (int i = 0; i < _state.Holdings.Count; i++)
                {
                    Holding holding = _state.Holdings[i];
                    if (holding == null || holding.Quantity < 0 || holding.TotalCost < 0 ||
                        (holding.Quantity == 0 && holding.TotalCost != 0))
                    {
                        error = TradeError.InvalidState;
                        itemId = holding == null ? string.Empty : holding.ItemId;
                        return false;
                    }
                    if (holding.Quantity == 0)
                        continue;

                    long quantity;
                    long cost;
                    simulation.LongQuantities.TryGetValue(holding.ItemId, out quantity);
                    simulation.LongCosts.TryGetValue(holding.ItemId, out cost);
                    try
                    {
                        simulation.LongQuantities[holding.ItemId] = checked(quantity + holding.Quantity);
                        simulation.LongCosts[holding.ItemId] = checked(cost + holding.TotalCost);
                    }
                    catch (OverflowException)
                    {
                        error = TradeError.CashOverflow;
                        itemId = holding.ItemId;
                        return false;
                    }
                }
            }

            if (_state.ShortBatches != null)
            {
                for (int i = 0; i < _state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = _state.ShortBatches[i];
                    if (batch == null || batch.Quantity < 0 || batch.Proceeds < 0 ||
                        (batch.Quantity == 0 && batch.Proceeds != 0))
                    {
                        error = TradeError.InvalidState;
                        itemId = batch == null ? string.Empty : batch.ItemId;
                        return false;
                    }
                    if (batch.Quantity == 0)
                        continue;
                    simulation.Batches.Add(new SimBatch(batch, i));
                }
            }

            return true;
        }

        private TradeError GetCurrentPoint(string itemId, out WeeklyPoint point)
        {
            point = null;
            SecurityBinding binding = _state.FindSecurityBinding(itemId);
            if (binding != null && binding.IsDelisted)
                return TradeError.Delisted;

            point = _state.FindWeeklyPoint(itemId, _state.Clock.MarketWeekIndex);
            if (point == null)
                return TradeError.PriceUnavailable;
            if (point.Delisted)
                return TradeError.Delisted;
            if (double.IsNaN(point.Price) || double.IsInfinity(point.Price) || point.Price <= 0.0)
                return TradeError.InvalidPrice;
            return TradeError.None;
        }

        private static void AddLong(Simulation simulation, string itemId, int quantity, long cost)
        {
            long currentQuantity;
            long currentCost;
            simulation.LongQuantities.TryGetValue(itemId, out currentQuantity);
            simulation.LongCosts.TryGetValue(itemId, out currentCost);
            simulation.LongQuantities[itemId] = checked(currentQuantity + quantity);
            simulation.LongCosts[itemId] = checked(currentCost + cost);
        }

        private static bool RemoveLong(Simulation simulation, string itemId, int quantity, out long available)
        {
            long currentQuantity;
            simulation.LongQuantities.TryGetValue(itemId, out currentQuantity);
            available = currentQuantity;
            if (currentQuantity < quantity)
                return false;

            long currentCost;
            simulation.LongCosts.TryGetValue(itemId, out currentCost);
            long newQuantity = currentQuantity - quantity;
            if (newQuantity == 0)
            {
                simulation.LongQuantities.Remove(itemId);
                simulation.LongCosts.Remove(itemId);
                return true;
            }

            long removedCost = Proportion(currentCost, quantity, currentQuantity);
            simulation.LongQuantities[itemId] = newQuantity;
            simulation.LongCosts[itemId] = currentCost - removedCost;
            return true;
        }

        private static void AddShort(Simulation simulation, string itemId, int quantity, long proceeds, int currentWeek)
        {
            string batchId = NewId("batch");
            SimBatch batch = new SimBatch
            {
                OriginalIndex = -1,
                BatchId = batchId,
                ItemId = itemId,
                Quantity = quantity,
                OpenWeek = currentWeek,
                Proceeds = proceeds,
                DueWeek = checked(currentWeek + ShortService.DurationWeeks)
            };
            simulation.Batches.Add(batch);
            simulation.LastBatchId = batchId;
        }

        private static void RemoveShort(
            Simulation simulation,
            string itemId,
            int quantity,
            long totalCost,
            WeeklyPoint point,
            bool automatic)
        {
            List<SimBatch> batches = new List<SimBatch>();
            for (int i = 0; i < simulation.Batches.Count; i++)
            {
                SimBatch batch = simulation.Batches[i];
                if (string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    batches.Add(batch);
            }
            batches.Sort(CompareBatches);

            int remainingQuantity = quantity;
            long remainingCost = totalCost;
            for (int i = 0; i < batches.Count && remainingQuantity > 0; i++)
            {
                SimBatch batch = batches[i];
                int part = Math.Min(remainingQuantity, batch.Quantity);
                long partCost;
                if (part == remainingQuantity)
                {
                    partCost = remainingCost;
                }
                else if (!TryWholeWen(point.Price, part, out partCost))
                {
                    throw new OverflowException();
                }

                int oldQuantity = batch.Quantity;
                long removedProceeds = Proportion(batch.Proceeds, part, oldQuantity);
                batch.Quantity -= part;
                batch.Proceeds -= removedProceeds;
                remainingQuantity -= part;
                remainingCost -= partCost;
                AddRecord(simulation, itemId, automatic ? TradeActionNames.Expire : TradeActionNames.Return,
                    batch.BatchId, part, point, -partCost);
            }
        }

        private static int CompareBatches(SimBatch left, SimBatch right)
        {
            int comparison = left.DueWeek.CompareTo(right.DueWeek);
            if (comparison != 0)
                return comparison;
            comparison = left.OpenWeek.CompareTo(right.OpenWeek);
            if (comparison != 0)
                return comparison;
            return string.Compare(left.BatchId, right.BatchId, StringComparison.Ordinal);
        }

        private static void AddRecord(
            Simulation simulation,
            string itemId,
            string action,
            string batchId,
            int quantity,
            WeeklyPoint point,
            long grossCashDelta)
        {
            simulation.Records.Add(new SimRecord
            {
                ItemId = itemId,
                Action = action,
                BatchId = batchId ?? string.Empty,
                Quantity = quantity,
                Point = point,
                GrossCashDelta = grossCashDelta
            });
            simulation.GrossCashDelta = checked(simulation.GrossCashDelta + grossCashDelta);
        }

        private static TradeRecord[] CreateRecords(
            Simulation simulation,
            int currentWeek,
            long debtPayment,
            long newDebt,
            bool committed)
        {
            TradeRecord[] records = new TradeRecord[simulation.Records.Count];
            for (int i = 0; i < simulation.Records.Count; i++)
            {
                SimRecord source = simulation.Records[i];
                records[i] = new TradeRecord
                {
                    TradeId = committed ? NewId("trade") : string.Empty,
                    MarketWeekIndex = currentWeek,
                    GameYear = source.Point.GameYear,
                    GameMonth = source.Point.GameMonth,
                    GameRound = source.Point.GameRound,
                    ItemId = source.ItemId,
                    Action = source.Action,
                    BatchId = source.BatchId,
                    Quantity = source.Quantity,
                    UnitPrice = source.Point.Price,
                    CashDelta = source.GrossCashDelta
                };
            }

            long remainingPayment = debtPayment;
            for (int i = 0; i < records.Length && remainingPayment > 0; i++)
            {
                long gross = simulation.Records[i].GrossCashDelta;
                if (gross <= 0)
                    continue;
                long payment = Math.Min(gross, remainingPayment);
                records[i].CashDelta -= payment;
                records[i].DebtDelta -= payment;
                remainingPayment -= payment;
            }

            long remainingDebt = newDebt;
            for (int i = records.Length - 1; i >= 0 && remainingDebt > 0; i--)
            {
                long gross = simulation.Records[i].GrossCashDelta;
                if (gross >= 0)
                    continue;
                long funded = Math.Min(-gross, remainingDebt);
                records[i].CashDelta += funded;
                records[i].DebtDelta += funded;
                remainingDebt -= funded;
            }

            return records;
        }

        private static List<Holding> BuildHoldings(Simulation simulation)
        {
            List<string> itemIds = new List<string>();
            foreach (KeyValuePair<string, long> pair in simulation.LongQuantities)
            {
                if (pair.Value > 0)
                    itemIds.Add(pair.Key);
            }
            itemIds.Sort(StringComparer.Ordinal);

            List<Holding> holdings = new List<Holding>(itemIds.Count);
            for (int i = 0; i < itemIds.Count; i++)
            {
                string itemId = itemIds[i];
                long cost;
                simulation.LongCosts.TryGetValue(itemId, out cost);
                holdings.Add(new Holding
                {
                    ItemId = itemId,
                    Quantity = (int)simulation.LongQuantities[itemId],
                    TotalCost = cost
                });
            }
            return holdings;
        }

        private static List<ShortBatch> BuildBatches(Simulation simulation)
        {
            List<ShortBatch> batches = new List<ShortBatch>();
            for (int i = 0; i < simulation.Batches.Count; i++)
            {
                SimBatch source = simulation.Batches[i];
                if (source.Quantity <= 0)
                    continue;
                batches.Add(new ShortBatch
                {
                    BatchId = source.BatchId,
                    ItemId = source.ItemId,
                    Quantity = source.Quantity,
                    OpenWeek = source.OpenWeek,
                    Proceeds = source.Proceeds,
                    DueWeek = source.DueWeek
                });
            }
            return batches;
        }

        internal static bool TryWholeWen(double unitPrice, long quantity, out long amount)
        {
            amount = 0;
            if (double.IsNaN(unitPrice) || double.IsInfinity(unitPrice) || unitPrice <= 0.0 || quantity < 0)
                return false;

            try
            {
                decimal total = decimal.Floor((decimal)unitPrice * quantity);
                if (total < 0m || total > long.MaxValue)
                    return false;
                amount = (long)total;
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool TryShortProceeds(double unitPrice, long quantity, out long proceeds)
        {
            proceeds = 0;
            if (double.IsNaN(unitPrice) || double.IsInfinity(unitPrice) || unitPrice <= 0.0 || quantity < 0)
                return false;

            try
            {
                decimal total = decimal.Floor((decimal)unitPrice * quantity * 0.97m);
                if (total < 0m || total > long.MaxValue)
                    return false;
                proceeds = (long)total;
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static long Proportion(long total, long part, long whole)
        {
            if (part <= 0 || total == 0)
                return 0;
            if (part >= whole)
                return total;
            return (long)decimal.Floor((decimal)total * part / whole);
        }

        private static string NewId(string prefix)
        {
            return prefix + ":" + Guid.NewGuid().ToString("N");
        }

        private TradeResult Fail(TradeError error, string itemId)
        {
            return Fail(error, itemId, null, null);
        }

        private TradeResult Fail(TradeError error, string itemId, CapacitySnapshot before, CapacitySnapshot after)
        {
            TradeResult result = TradeResult.Fail(error, GetMessage(error), itemId);
            result.CashAfter = _wallet.Balance;
            result.DebtAfter = _state.Debt;
            result.CapacityBefore = before;
            result.CapacityAfter = after;
            return result;
        }

        private TradeResult Fail(TradeError error, string itemId, long required, long available)
        {
            TradeResult result = Fail(error, itemId);
            result.RequiredAmount = required;
            result.AvailableAmount = available;
            return result;
        }

        private static string GetMessage(TradeError error)
        {
            switch (error)
            {
                case TradeError.InvalidRequest: return "契匣为空或包含无效交易指令。";
                case TradeError.InvalidState: return "当前行市或仓位状态无效。";
                case TradeError.UnknownItem: return "找不到该商品定义。";
                case TradeError.InvalidQuantity: return "交易数量必须大于零且不超出上限。";
                case TradeError.PriceUnavailable: return "当前市场周没有可见成交价。";
                case TradeError.InvalidPrice: return "当前成交价无效。";
                case TradeError.Delisted: return "商品已退市，仓位冻结，禁止交易与平仓。";
                case TradeError.InsufficientCash: return "银两不足，整份契匣未结算。";
                case TradeError.InsufficientHolding: return "持货数量不足，整份契匣未结算。";
                case TradeError.InsufficientShortPosition: return "借货数量不足，整份契匣未结算。";
                case TradeError.PositionConflict: return "同一商品不能同时持有多头和借货。";
                case TradeError.CapacityExceeded: return "货囊格位或单格负重不足，禁止继续加仓。";
                case TradeError.InvalidItemWeight: return "商品重量必须大于零。";
                case TradeError.CashOverflow: return "结算金额超出银两可记录范围。";
                case TradeError.WalletRejected: return "原版银两未接受本次结算。";
                default: return string.Empty;
            }
        }

        private sealed class Simulation
        {
            public readonly Dictionary<string, long> LongQuantities = new Dictionary<string, long>(StringComparer.Ordinal);
            public readonly Dictionary<string, long> LongCosts = new Dictionary<string, long>(StringComparer.Ordinal);
            public readonly List<SimBatch> Batches = new List<SimBatch>();
            public readonly List<SimRecord> Records = new List<SimRecord>();
            public long GrossCashDelta;
            public string LastBatchId = string.Empty;

            public long GetShortQuantity(string itemId)
            {
                long quantity = 0;
                for (int i = 0; i < Batches.Count; i++)
                {
                    if (string.Equals(Batches[i].ItemId, itemId, StringComparison.Ordinal))
                        quantity = checked(quantity + Batches[i].Quantity);
                }
                return quantity;
            }

            public Dictionary<string, long> GetShortQuantities()
            {
                Dictionary<string, long> result = new Dictionary<string, long>(StringComparer.Ordinal);
                for (int i = 0; i < Batches.Count; i++)
                {
                    SimBatch batch = Batches[i];
                    if (batch.Quantity <= 0)
                        continue;
                    long quantity;
                    result.TryGetValue(batch.ItemId, out quantity);
                    result[batch.ItemId] = checked(quantity + batch.Quantity);
                }
                return result;
            }

            public bool HasPositionConflict(out string itemId)
            {
                foreach (KeyValuePair<string, long> pair in LongQuantities)
                {
                    if (pair.Value > 0 && GetShortQuantity(pair.Key) > 0)
                    {
                        itemId = pair.Key;
                        return true;
                    }
                }
                itemId = string.Empty;
                return false;
            }
        }

        private sealed class SimBatch
        {
            public int OriginalIndex;
            public string BatchId;
            public string ItemId;
            public int Quantity;
            public int OpenWeek;
            public long Proceeds;
            public int DueWeek;

            public SimBatch()
            {
            }

            public SimBatch(ShortBatch source, int originalIndex)
            {
                OriginalIndex = originalIndex;
                BatchId = source.BatchId ?? string.Empty;
                ItemId = source.ItemId ?? string.Empty;
                Quantity = source.Quantity;
                OpenWeek = source.OpenWeek;
                Proceeds = source.Proceeds;
                DueWeek = source.DueWeek;
            }
        }

        private sealed class SimRecord
        {
            public string ItemId;
            public string Action;
            public string BatchId;
            public int Quantity;
            public WeeklyPoint Point;
            public long GrossCashDelta;
        }
    }
}
