using System;
using System.Collections.Generic;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Trading
{
    public sealed class AssetSnapshot
    {
        private static readonly string[] EmptyItemIds = new string[0];

        public long Cash { get; internal set; }
        public double LongValue { get; internal set; }
        public double ShortLiability { get; internal set; }
        public long Debt { get; internal set; }
        public double NetAssets { get; internal set; }
        public double LongCost { get; internal set; }
        public double ShortProceeds { get; internal set; }
        public double RealizedProfit { get; internal set; }
        public double FloatingProfit { get; internal set; }
        public double TotalProfit { get; internal set; }
        public double CapitalBasis { get; internal set; }
        public double ReturnRate { get; internal set; }
        public double FloatingReturnRate { get; internal set; }
        public bool HasCompletePrices { get; internal set; }
        public bool IsRealizedProfitComplete { get; internal set; }
        public IReadOnlyList<string> MissingPriceItemIds { get; internal set; }

        public AssetSnapshot()
        {
            MissingPriceItemIds = EmptyItemIds;
        }
    }

    public sealed class AssetService
    {
        private readonly RunState _state;
        private readonly IPlayerWallet _wallet;
        private readonly IReadOnlyDictionary<string, ItemDefinition> _items;

        public AssetService(
            RunState state,
            IPlayerWallet wallet,
            IReadOnlyDictionary<string, ItemDefinition> items)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            if (items == null)
                throw new ArgumentNullException("items");

            _state = state;
            _wallet = wallet;
            _items = items;
        }

        public AssetSnapshot GetSnapshot()
        {
            AssetSnapshot snapshot = new AssetSnapshot
            {
                Cash = _wallet.Balance,
                Debt = _state.Debt,
                HasCompletePrices = true,
                IsRealizedProfitComplete = true
            };
            HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);

            if (_state.Holdings != null)
            {
                for (int i = 0; i < _state.Holdings.Count; i++)
                {
                    Holding holding = _state.Holdings[i];
                    if (holding == null || holding.Quantity <= 0)
                        continue;

                    snapshot.LongCost += holding.TotalCost;
                    double price;
                    if (TryGetVisiblePrice(holding.ItemId, out price))
                        snapshot.LongValue += price * holding.Quantity;
                    else
                        missing.Add(holding.ItemId ?? string.Empty);
                }
            }

            if (_state.ShortBatches != null)
            {
                for (int i = 0; i < _state.ShortBatches.Count; i++)
                {
                    ShortBatch batch = _state.ShortBatches[i];
                    if (batch == null || batch.Quantity <= 0)
                        continue;

                    snapshot.ShortProceeds += batch.Proceeds;
                    double price;
                    if (TryGetVisiblePrice(batch.ItemId, out price))
                        snapshot.ShortLiability += price * batch.Quantity;
                    else
                        missing.Add(batch.ItemId ?? string.Empty);
                }
            }

            RealizedLedger ledger = ReplayTrades();
            snapshot.RealizedProfit = ledger.RealizedProfit;
            snapshot.CapitalBasis = ledger.CapitalBasis;
            snapshot.IsRealizedProfitComplete = ledger.IsComplete;
            snapshot.FloatingProfit = snapshot.LongValue - snapshot.LongCost +
                snapshot.ShortProceeds - snapshot.ShortLiability;
            snapshot.TotalProfit = snapshot.RealizedProfit + snapshot.FloatingProfit;
            snapshot.NetAssets = snapshot.Cash + snapshot.LongValue - snapshot.ShortLiability - snapshot.Debt;

            double positionBasis = snapshot.LongCost + snapshot.ShortProceeds;
            snapshot.ReturnRate = snapshot.CapitalBasis > 0.0
                ? snapshot.TotalProfit / snapshot.CapitalBasis
                : 0.0;
            snapshot.FloatingReturnRate = positionBasis > 0.0
                ? snapshot.FloatingProfit / positionBasis
                : 0.0;

            if (missing.Count > 0)
            {
                snapshot.HasCompletePrices = false;
                string[] itemIds = new string[missing.Count];
                missing.CopyTo(itemIds);
                Array.Sort(itemIds, StringComparer.Ordinal);
                snapshot.MissingPriceItemIds = itemIds;
            }
            return snapshot;
        }

        public bool TryGetVisiblePrice(string itemId, out double price)
        {
            price = 0.0;
            if (_state.Clock == null || string.IsNullOrEmpty(itemId) || _state.PriceHistory == null)
                return false;

            int currentWeek = _state.Clock.MarketWeekIndex;
            int bestWeek = -1;
            double bestPrice = 0.0;
            bool found = false;
            for (int i = 0; i < _state.PriceHistory.Count; i++)
            {
                WeeklyPoint point = _state.PriceHistory[i];
                if (point == null || point.MarketWeekIndex > currentWeek ||
                    point.MarketWeekIndex < bestWeek ||
                    !string.Equals(point.ItemId, itemId, StringComparison.Ordinal) ||
                    double.IsNaN(point.Price) || double.IsInfinity(point.Price) || point.Price <= 0.0)
                    continue;

                if (point.MarketWeekIndex > bestWeek || !found)
                {
                    bestWeek = point.MarketWeekIndex;
                    bestPrice = point.Price;
                    found = true;
                }
            }

            if (!found)
                return false;
            price = bestPrice;
            return true;
        }

        public AssetPoint CaptureWeeklySnapshot()
        {
            AssetPoint point = CreatePoint();
            EnsureAssetHistory();
            for (int i = _state.AssetHistory.Count - 1; i >= 0; i--)
            {
                AssetPoint existing = _state.AssetHistory[i];
                if (existing != null && existing.MarketWeekIndex == point.MarketWeekIndex)
                {
                    _state.AssetHistory[i] = point;
                    return point;
                }
            }
            _state.AssetHistory.Add(point);
            return point;
        }

        public AssetPoint CaptureTradeSnapshot()
        {
            AssetPoint point = CreatePoint();
            EnsureAssetHistory();
            _state.AssetHistory.Add(point);
            return point;
        }

        private AssetPoint CreatePoint()
        {
            if (_state.Clock == null)
                throw new InvalidOperationException("The market clock is unavailable.");

            AssetSnapshot snapshot = GetSnapshot();
            return new AssetPoint
            {
                MarketWeekIndex = _state.Clock.MarketWeekIndex,
                GameYear = _state.CurrentGameYear,
                GameMonth = _state.Clock.CurrentGameMonth,
                GameRound = _state.Clock.LastGameRound,
                Cash = snapshot.Cash,
                LongValue = snapshot.LongValue,
                ShortLiability = snapshot.ShortLiability,
                Debt = snapshot.Debt,
                NetAssets = snapshot.NetAssets
            };
        }

        private void EnsureAssetHistory()
        {
            if (_state.AssetHistory == null)
                _state.AssetHistory = new List<AssetPoint>();
        }

        private RealizedLedger ReplayTrades()
        {
            RealizedLedger ledger = new RealizedLedger();
            ledger.IsComplete = true;
            if (_state.TradeHistory == null)
                return ledger;

            Dictionary<string, LedgerHolding> holdings = new Dictionary<string, LedgerHolding>(StringComparer.Ordinal);
            List<LedgerBatch> batches = new List<LedgerBatch>();

            for (int i = 0; i < _state.TradeHistory.Count; i++)
            {
                TradeRecord trade = _state.TradeHistory[i];
                if (trade == null || trade.Quantity <= 0)
                {
                    ledger.IsComplete = false;
                    continue;
                }

                double gross = (double)trade.CashDelta - trade.DebtDelta;
                if (trade.Action == TradeActionNames.Buy)
                {
                    LedgerHolding holding;
                    if (!holdings.TryGetValue(trade.ItemId, out holding))
                    {
                        holding = new LedgerHolding();
                        holdings.Add(trade.ItemId, holding);
                    }
                    double cost = -gross;
                    holding.Quantity += trade.Quantity;
                    holding.Cost += cost;
                    ledger.CapitalBasis += cost;
                }
                else if (trade.Action == TradeActionNames.Sell)
                {
                    LedgerHolding holding;
                    if (!holdings.TryGetValue(trade.ItemId, out holding) || holding.Quantity < trade.Quantity)
                    {
                        ledger.IsComplete = false;
                        continue;
                    }

                    double removedCost = Proportion(holding.Cost, trade.Quantity, holding.Quantity);
                    ledger.RealizedProfit += gross - removedCost;
                    holding.Quantity -= trade.Quantity;
                    holding.Cost -= removedCost;
                    if (holding.Quantity == 0)
                    {
                        holding.Cost = 0.0;
                        holdings.Remove(trade.ItemId);
                    }
                }
                else if (trade.Action == TradeActionNames.Borrow)
                {
                    LedgerBatch batch = FindBatch(batches, trade.ItemId, trade.BatchId);
                    if (batch == null)
                    {
                        batch = new LedgerBatch
                        {
                            ItemId = trade.ItemId,
                            BatchId = trade.BatchId ?? string.Empty
                        };
                        batches.Add(batch);
                    }
                    batch.Quantity += trade.Quantity;
                    batch.Proceeds += gross;
                    ledger.CapitalBasis += gross;
                }
                else if (trade.Action == TradeActionNames.Return || trade.Action == TradeActionNames.Expire)
                {
                    LedgerBatch batch = FindBatch(batches, trade.ItemId, trade.BatchId);
                    if (batch == null && string.IsNullOrEmpty(trade.BatchId))
                        batch = FindFirstBatch(batches, trade.ItemId);
                    if (batch == null || batch.Quantity < trade.Quantity)
                    {
                        ledger.IsComplete = false;
                        continue;
                    }

                    double removedProceeds = Proportion(batch.Proceeds, trade.Quantity, batch.Quantity);
                    ledger.RealizedProfit += removedProceeds + gross;
                    batch.Quantity -= trade.Quantity;
                    batch.Proceeds -= removedProceeds;
                    if (batch.Quantity == 0)
                    {
                        batch.Proceeds = 0.0;
                        batches.Remove(batch);
                    }
                }
                else
                {
                    ledger.IsComplete = false;
                }
            }
            return ledger;
        }

        private static LedgerBatch FindBatch(List<LedgerBatch> batches, string itemId, string batchId)
        {
            if (string.IsNullOrEmpty(batchId))
                return null;
            for (int i = 0; i < batches.Count; i++)
            {
                LedgerBatch batch = batches[i];
                if (string.Equals(batch.ItemId, itemId, StringComparison.Ordinal) &&
                    string.Equals(batch.BatchId, batchId, StringComparison.Ordinal))
                    return batch;
            }
            return null;
        }

        private static LedgerBatch FindFirstBatch(List<LedgerBatch> batches, string itemId)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                if (string.Equals(batches[i].ItemId, itemId, StringComparison.Ordinal))
                    return batches[i];
            }
            return null;
        }

        private static double Proportion(double total, double part, double whole)
        {
            if (total == 0.0 || part <= 0.0)
                return 0.0;
            if (part >= whole)
                return total;
            return Math.Floor(total * part / whole);
        }

        private sealed class RealizedLedger
        {
            public double RealizedProfit;
            public double CapitalBasis;
            public bool IsComplete;
        }

        private sealed class LedgerHolding
        {
            public double Quantity;
            public double Cost;
        }

        private sealed class LedgerBatch
        {
            public string ItemId;
            public string BatchId;
            public double Quantity;
            public double Proceeds;
        }
    }
}
