using System;
using System.Collections.Generic;
using System.Linq;
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
                if (point == null || point.MarketWeekIndex < 0 || point.MarketWeekIndex > currentWeek ||
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

            return DescribeSnapshot(GetSnapshot());
        }

        internal AssetPoint DescribeSnapshot(AssetSnapshot snapshot)
        {
            bool complete = snapshot.HasCompletePrices && snapshot.IsRealizedProfitComplete;
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
                NetAssets = snapshot.NetAssets,
                ProfitRecordVersion = 1,
                HasProfitMetrics = complete,
                TotalProfit = complete ? snapshot.TotalProfit : 0,
                CapitalBasis = complete ? snapshot.CapitalBasis : 0
            };
        }

        // Each chart point represents the latest capture within its market week.
        // Revalue legacy positions only when both the current lots and the old valuation reconcile.
        internal List<AssetPoint> ProfitHistory(IReadOnlyList<AssetPoint> history, AssetSnapshot current)
        {
            int now = _state.Clock == null ? -1 : _state.Clock.MarketWeekIndex;
            var last = new SortedDictionary<int, AssetPoint>();
            for (int i = 0; history != null && i < history.Count; i++)
            {
                AssetPoint p = history[i];
                if (p != null && p.MarketWeekIndex >= 0 && p.MarketWeekIndex <= now) last[p.MarketWeekIndex] = p;
            }
            if (now >= 0 && current != null) last[now] = DescribeSnapshot(current);
            var output = new List<AssetPoint>();
            bool recover = current != null && current.IsRealizedProfitComplete && _state.TradeHistory != null &&
                _state.TradeHistory.Count > 0 && last.Values.Any(p => p.ProfitRecordVersion == 0);
            var ledger = new RealizedLedger();
            var trades = recover ? ChronologicalTrades().ToList() : null;
            var quotes = recover && _state.PriceHistory != null ? _state.PriceHistory.Where(p => p != null &&
                !string.IsNullOrEmpty(p.ItemId) && p.MarketWeekIndex >= 0 && p.MarketWeekIndex <= now &&
                !double.IsNaN(p.Price) && !double.IsInfinity(p.Price) && p.Price > 0).OrderBy(p => p.MarketWeekIndex).ToList() : null;
            var prices = new Dictionary<string, WeeklyPoint>(StringComparer.Ordinal);
            int tradeIndex = 0, quoteIndex = 0;
            foreach (AssetPoint source in last.Values)
            {
                if (source.HasProfitMetrics) { output.Add(source); continue; }
                if (!recover || source.ProfitRecordVersion != 0) continue;
                while (tradeIndex < trades.Count && (trades[tradeIndex] == null || trades[tradeIndex].MarketWeekIndex <= source.MarketWeekIndex))
                    ApplyTrade(ledger, trades[tradeIndex++]);
                if (!ledger.IsComplete) continue;
                while (quotes != null && quoteIndex < quotes.Count && quotes[quoteIndex].MarketWeekIndex <= source.MarketWeekIndex)
                {
                    WeeklyPoint quote = quotes[quoteIndex++];
                    WeeklyPoint previous;
                    if (!prices.TryGetValue(quote.ItemId, out previous) || previous.MarketWeekIndex < quote.MarketWeekIndex)
                        prices[quote.ItemId] = quote;
                }
                double longs = 0, liabilities = 0, costs = 0, proceeds = 0;
                bool valued = true;
                foreach (var lot in ledger.Holdings)
                {
                    WeeklyPoint q;
                    if (!prices.TryGetValue(lot.Key, out q)) { valued = false; break; }
                    longs += lot.Value.Quantity * q.Price;
                    costs += (double)lot.Value.Cost;
                }
                foreach (var batch in ledger.Batches)
                {
                    WeeklyPoint q;
                    if (!prices.TryGetValue(batch.ItemId, out q)) { valued = false; break; }
                    liabilities += batch.Quantity * q.Price;
                    proceeds += (double)batch.Proceeds;
                }
                if (!valued || !SameValue(longs, source.LongValue) || !SameValue(liabilities, source.ShortLiability) ||
                    !SameValue(source.Cash + longs - liabilities - source.Debt, source.NetAssets)) continue;
                AssetPoint point = source.Copy();
                point.HasProfitMetrics = true;
                point.TotalProfit = ledger.RealizedProfit + longs - costs + proceeds - liabilities;
                point.CapitalBasis = ledger.CapitalBasis;
                output.Add(point);
            }
            return output;
        }

        private static bool SameValue(double a, double b)
        {
            // Sub-wen tolerance for floating-point multiplication, never a rounded chart label.
            return Math.Abs(a-b) <= Math.Min(0.25, Math.Max(0.000001, Math.Max(Math.Abs(a),Math.Abs(b)) * 1e-13));
        }

        private void EnsureAssetHistory()
        {
            if (_state.AssetHistory == null)
                _state.AssetHistory = new List<AssetPoint>();
        }

        private RealizedLedger ReplayTrades()
        {
            var ledger = new RealizedLedger();
            foreach (TradeRecord trade in ChronologicalTrades()) ApplyTrade(ledger, trade);
            int now = _state.Clock == null ? -1 : _state.Clock.MarketWeekIndex;
            // A prior position before the first recorded opening proves the journal is incomplete,
            // even when everything has subsequently been closed and the current inventory is empty.
            if (_state.AssetHistory != null && _state.AssetHistory.Any(p => p != null && p.ProfitRecordVersion == 0 &&
                p.MarketWeekIndex >= 0 && p.MarketWeekIndex <= now &&
                ((p.LongValue > 0 && p.MarketWeekIndex < ledger.FirstBuyWeek) ||
                 (p.ShortLiability > 0 && p.MarketWeekIndex < ledger.FirstBorrowWeek))))
                ledger.IsComplete = false;
            // Missing opening records in a legacy save cannot be inferred from the cash balance.
            foreach (Holding held in _state.Holdings ?? new List<Holding>())
            {
                if (held == null || held.Quantity <= 0) continue;
                LedgerHolding open;
                if (!ledger.Holdings.TryGetValue(held.ItemId ?? string.Empty, out open) ||
                    open.Quantity != held.Quantity || open.Cost != held.TotalCost)
                    ledger.IsComplete = false;
                else ledger.Holdings.Remove(held.ItemId);
            }
            foreach (ShortBatch held in _state.ShortBatches ?? new List<ShortBatch>())
            {
                if (held == null || held.Quantity <= 0) continue;
                LedgerBatch open = string.IsNullOrEmpty(held.BatchId) ? FindFirstBatch(ledger.Batches, held.ItemId) :
                    FindBatch(ledger.Batches, held.ItemId, held.BatchId);
                if (open == null || open.Quantity != held.Quantity || open.Proceeds != held.Proceeds)
                    ledger.IsComplete = false;
                else ledger.Batches.Remove(open);
            }
            if (ledger.Holdings.Count != 0 || ledger.Batches.Count != 0) ledger.IsComplete = false;
            return ledger;
        }

        private IEnumerable<TradeRecord> ChronologicalTrades()
        {
            if (_state.TradeHistory == null) return Enumerable.Empty<TradeRecord>();
            int week = _state.Clock == null ? -1 : _state.Clock.MarketWeekIndex;
            return _state.TradeHistory.Where(t => t == null || t.MarketWeekIndex <= week)
                .OrderBy(t => t == null ? -1 : t.MarketWeekIndex);
        }

        private static void ApplyTrade(RealizedLedger ledger, TradeRecord trade)
        {
            if (trade == null || trade.Quantity <= 0 || string.IsNullOrEmpty(trade.ItemId))
            {
                ledger.IsComplete = false;
                return;
            }

            decimal gross = (decimal)trade.CashDelta - trade.DebtDelta;
            if ((trade.Action == TradeActionNames.Buy || trade.Action == TradeActionNames.Return || trade.Action == TradeActionNames.Expire)
                ? gross > 0 : gross < 0)
            {
                ledger.IsComplete = false;
                return;
            }
            if (trade.Action == TradeActionNames.Buy)
            {
                ledger.FirstBuyWeek = Math.Min(ledger.FirstBuyWeek,trade.MarketWeekIndex);
                LedgerHolding holding;
                if (!ledger.Holdings.TryGetValue(trade.ItemId, out holding))
                {
                    holding = new LedgerHolding();
                    ledger.Holdings.Add(trade.ItemId, holding);
                }
                decimal cost = -gross;
                holding.Quantity += trade.Quantity;
                holding.Cost += cost;
                ledger.CapitalBasis += (double)cost;
            }
            else if (trade.Action == TradeActionNames.Sell)
            {
                LedgerHolding holding;
                if (!ledger.Holdings.TryGetValue(trade.ItemId, out holding) || holding.Quantity < trade.Quantity)
                {
                    ledger.IsComplete = false;
                    return;
                }

                decimal removedCost = Proportion(holding.Cost, trade.Quantity, holding.Quantity);
                ledger.RealizedProfit += (double)(gross - removedCost);
                holding.Quantity -= trade.Quantity;
                holding.Cost -= removedCost;
                if (holding.Quantity == 0)
                {
                    ledger.Holdings.Remove(trade.ItemId);
                }
            }
            else if (trade.Action == TradeActionNames.Borrow)
            {
                ledger.FirstBorrowWeek = Math.Min(ledger.FirstBorrowWeek,trade.MarketWeekIndex);
                LedgerBatch batch = FindBatch(ledger.Batches, trade.ItemId, trade.BatchId);
                if (batch == null)
                {
                    batch = new LedgerBatch
                    {
                        ItemId = trade.ItemId,
                        BatchId = trade.BatchId ?? string.Empty
                    };
                    ledger.Batches.Add(batch);
                }
                batch.Quantity += trade.Quantity;
                batch.Proceeds += gross;
                ledger.CapitalBasis += (double)gross;
            }
            else if (trade.Action == TradeActionNames.Return || trade.Action == TradeActionNames.Expire)
            {
                LedgerBatch batch = FindBatch(ledger.Batches, trade.ItemId, trade.BatchId);
                if (batch == null && string.IsNullOrEmpty(trade.BatchId))
                    batch = FindFirstBatch(ledger.Batches, trade.ItemId);
                if (batch == null || batch.Quantity < trade.Quantity)
                {
                    ledger.IsComplete = false;
                    return;
                }

                decimal removedProceeds = Proportion(batch.Proceeds, trade.Quantity, batch.Quantity);
                ledger.RealizedProfit += (double)(removedProceeds + gross);
                batch.Quantity -= trade.Quantity;
                batch.Proceeds -= removedProceeds;
                if (batch.Quantity == 0)
                {
                    ledger.Batches.Remove(batch);
                }
            }
            else
            {
                ledger.IsComplete = false;
            }
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

        private static decimal Proportion(decimal total, long part, long whole)
        {
            if (total == 0 || part <= 0) return 0;
            if (part >= whole)
                return total;
            return decimal.Floor(total * part / whole);
        }

        private sealed class RealizedLedger
        {
            public double RealizedProfit;
            public double CapitalBasis;
            public bool IsComplete = true;
            public int FirstBuyWeek = int.MaxValue, FirstBorrowWeek = int.MaxValue;
            public readonly Dictionary<string, LedgerHolding> Holdings = new Dictionary<string, LedgerHolding>(StringComparer.Ordinal);
            public readonly List<LedgerBatch> Batches = new List<LedgerBatch>();
        }

        private sealed class LedgerHolding
        {
            public long Quantity;
            public decimal Cost;
        }

        private sealed class LedgerBatch
        {
            public string ItemId;
            public string BatchId;
            public long Quantity;
            public decimal Proceeds;
        }
    }
}
