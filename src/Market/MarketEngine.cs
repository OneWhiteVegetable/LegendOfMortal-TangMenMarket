using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Market
{
    public sealed class MarketWeekChange
    {
        public int MarketWeekIndex { get; private set; }
        public MarketTime Time { get; private set; }
        public WeeklyPoint Index { get; private set; }
        public IReadOnlyList<WeeklyPoint> Items { get; private set; }
        public IReadOnlyList<string> NewlyDelistedItemIds { get; private set; }

        internal MarketWeekChange(
            int marketWeekIndex,
            MarketTime time,
            WeeklyPoint index,
            IList<WeeklyPoint> items,
            IList<string> newlyDelistedItemIds)
        {
            MarketWeekIndex = marketWeekIndex;
            Time = time;
            Index = index;
            Items = new ReadOnlyCollection<WeeklyPoint>(new List<WeeklyPoint>(items));
            NewlyDelistedItemIds = new ReadOnlyCollection<string>(new List<string>(newlyDelistedItemIds));
        }
    }

    public sealed class MarketEngine
    {
        private readonly RunState _state;
        private readonly WeeklyDataRepository _repository;
        private readonly MarketSelector _selector;
        private readonly AnnualIndexService _annualIndices;
        private readonly ElasticityStateMachine _elasticity;
        private readonly DelistingService _delisting;
        private int _lastSettledWeek;

        public RunState State { get { return _state; } }
        public AnnualIndexService AnnualIndices { get { return _annualIndices; } }
        public int PendingWeekCount { get { return Math.Max(0, _state.Clock.MarketWeekIndex - _lastSettledWeek); } }

        public event Action<MarketWeekChange> WeekAdvanced;

        public MarketEngine(RunState state, WeeklyDataRepository repository)
            : this(state, repository, null)
        {
        }

        public MarketEngine(
            RunState state,
            WeeklyDataRepository repository,
            IEnumerable<ItemDefinition> itemDefinitions)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (repository == null)
                throw new ArgumentNullException("repository");

            _state = state;
            _repository = repository;
            NormalizeState();
            _selector = new MarketSelector(repository, state.RunSeed);
            _annualIndices = new AnnualIndexService(repository, _selector);
            _elasticity = new ElasticityStateMachine();
            _delisting = new DelistingService();

            _annualIndices.EnsureBinding(_state, _state.CurrentGameYear);
            if (_state.IndexHistory.Count == 0)
            {
                AnnualIndexBinding binding = _state.FindAnnualIndexBinding(_state.CurrentGameYear);
                MarketTime time = GetCurrentTime();
                _state.IndexHistory.Add(new WeeklyPoint
                {
                    ItemId = string.Empty,
                    MarketWeekIndex = _state.Clock.MarketWeekIndex,
                    GameYear = time.Year,
                    GameMonth = time.Month,
                    GameRound = time.Round,
                    SourceRecordIndex = binding.StartRecordIndex,
                    Price = _state.CurrentIndex,
                    BaseReturn = 0.0,
                    Return = 0.0,
                    Observed = true,
                    DataExhausted = binding.IsDataExhausted,
                    Delisted = false
                });
            }

            _lastSettledWeek = LastSavedIndexWeek();
            var definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            if (itemDefinitions != null)
            {
                foreach (ItemDefinition definition in itemDefinitions)
                {
                    if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
                        throw new ArgumentException("Item definitions must have stable IDs.", "itemDefinitions");
                    definitions.Add(definition.ItemId, definition);
                }
            }

            for (int i = 0; i < _state.AppearedItemIds.Count; i++)
            {
                string itemId = _state.AppearedItemIds[i];
                SecurityBinding binding = _state.FindSecurityBinding(itemId);
                if (binding == null)
                {
                    ItemDefinition definition;
                    if (!definitions.TryGetValue(itemId, out definition))
                    {
                        throw new InvalidOperationException(
                            "An item definition is required to initialize appeared item '" + itemId + "'.");
                    }
                    RegisterAppearedItem(definition);
                }
                else
                {
                    EnsureElasticityState(itemId);
                }
            }
        }

        public SecurityBinding RegisterAppearedItem(ItemDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            SecurityBinding existing = _state.FindSecurityBinding(definition.ItemId);
            if (existing != null)
            {
                if (!_state.HasItemAppeared(definition.ItemId))
                    _state.AppearedItemIds.Add(definition.ItemId);
                EnsureElasticityState(definition.ItemId);
                return existing;
            }

            int revealWeek = _state.Clock.MarketWeekIndex;
            SecurityBinding binding = _selector.CreateSecurityBinding(definition, revealWeek);
            List<WeeklyPoint> retained = _state.GetPriceHistory(binding.ItemId);
            retained.RemoveAll(point => point.MarketWeekIndex > revealWeek);
            if (retained.Count != 0)
                return ReattachPublishedHistory(binding, retained, revealWeek);
            var elasticity = new ElasticityState { ItemId = definition.ItemId, Multiplier = 1.0 };
            List<WeeklyPoint> indexPath = SettledIndexPath(revealWeek);
            WeeklyPoint first = indexPath[0];
            var newPrices = new List<WeeklyPoint>(indexPath.Count);
            _delisting.ApplyInitialRecord(binding, _repository.GetSecurity(binding.SecurityId), first.MarketWeekIndex);
            newPrices.Add(new WeeklyPoint
            {
                ItemId = definition.ItemId,
                MarketWeekIndex = first.MarketWeekIndex,
                GameYear = first.GameYear,
                GameMonth = first.GameMonth,
                GameRound = first.GameRound,
                SourceRecordIndex = binding.StartRecordIndex,
                Price = RoundToWen(binding.LastPrice),
                Observed = true,
                DataExhausted = binding.IsDataExhausted,
                Delisted = binding.IsDelisted
            });
            // A historical quote is not a past market event. Do not settle, publish news or
            // capture player assets while catching up this newly revealed security.
            for (int i = 1; i < indexPath.Count; i++)
                newPrices.Add(AdvanceItem(binding, elasticity, indexPath[i]));

            _state.SecurityBindings.Add(binding);
            _state.ElasticityStates.RemoveAll(old => old != null && old.ItemId == binding.ItemId);
            _state.ElasticityStates.Add(elasticity);
            _state.PriceHistory.AddRange(newPrices);
            if (!_state.HasItemAppeared(definition.ItemId))
                _state.AppearedItemIds.Add(definition.ItemId);
            return binding;
        }

        // A damaged or early save may retain public prices but omit their binding. Never
        // append a second opening series over amounts that could already be in the player's ledger.
        private SecurityBinding ReattachPublishedHistory(SecurityBinding binding, List<WeeklyPoint> prices, int currentWeek)
        {
            prices.Sort((a, b) => a.MarketWeekIndex.CompareTo(b.MarketWeekIndex));
            WeeklyPoint last = prices[prices.Count - 1];
            WeeklySecurity security = _repository.GetSecurity(binding.SecurityId);
            binding.AppearedWeek = prices[0].MarketWeekIndex;
            binding.LastPrice = last.Price;
            if (last.SourceRecordIndex >= 0 && last.SourceRecordIndex < security.Count)
                binding.NextRecordIndex = last.SourceRecordIndex + 1;
            binding.IsDataExhausted = last.DataExhausted || binding.NextRecordIndex >= security.Count;
            for (int i = 0; i < prices.Count; i++)
            {
                if (!prices[i].Delisted)
                    continue;
                binding.IsDelisted = binding.IsDataExhausted = true;
                binding.DelistedWeek = prices[i].MarketWeekIndex;
                break;
            }
            ElasticityState elasticity = EnsureElasticityState(binding.ItemId);
            List<WeeklyPoint> newPrices = new List<WeeklyPoint>();
            foreach (WeeklyPoint index in SettledIndexPath(currentWeek))
            {
                if (index.MarketWeekIndex > last.MarketWeekIndex)
                    newPrices.Add(AdvanceItem(binding, elasticity, index));
            }
            _state.PriceHistory.AddRange(newPrices);
            _state.SecurityBindings.Add(binding);
            if (!_state.HasItemAppeared(binding.ItemId))
                _state.AppearedItemIds.Add(binding.ItemId);
            return binding;
        }

        private List<WeeklyPoint> SettledIndexPath(int throughWeek)
        {
            var sorted = new SortedDictionary<int, WeeklyPoint>();
            foreach (WeeklyPoint index in _state.IndexHistory)
            {
                if (index != null && index.MarketWeekIndex >= 0 && index.MarketWeekIndex <= throughWeek &&
                    index.MarketWeekIndex <= _lastSettledWeek)
                    sorted[index.MarketWeekIndex] = index;
            }
            var path = new List<WeeklyPoint>(sorted.Count);
            foreach (WeeklyPoint index in sorted.Values)
            {
                // A partial legacy index cannot justify invented returns across missing weeks.
                if (path.Count > 0 && index.MarketWeekIndex - path[path.Count - 1].MarketWeekIndex != 1)
                    path.Clear();
                path.Add(index);
            }
            if (path.Count == 0)
            {
                MarketTime time = GetCurrentTime();
                path.Add(new WeeklyPoint { MarketWeekIndex = throughWeek, GameYear = time.Year,
                    GameMonth = time.Month, GameRound = time.Round });
            }
            return path;
        }

        public IList<MarketWeekChange> AdvanceWeeks(MarketClockAdvancement advancement)
        {
            if (advancement == null)
                throw new ArgumentNullException("advancement");
            var changes = new List<MarketWeekChange>();
            if (advancement.WeekCount == 0)
                return changes;
            if (advancement.EndWeekIndex > _state.Clock.MarketWeekIndex)
                throw new InvalidOperationException("The clock has not advanced through all supplied weeks.");

            int lastProcessed = _lastSettledWeek;
            if (advancement.EndWeekIndex <= lastProcessed)
                return changes;

            int offset = 0;
            if (advancement.StartWeekIndex <= lastProcessed)
                offset = lastProcessed - advancement.StartWeekIndex + 1;
            int nextWeek = advancement.StartWeekIndex + offset;
            if (nextWeek != lastProcessed + 1)
                throw new InvalidOperationException("Earlier market weeks must be settled before this advancement.");

            for (int i = offset; i < advancement.WeekCount; i++)
            {
                MarketWeekChange change = AdvanceOne(nextWeek++, advancement.Weeks[i]);
                changes.Add(change);
                Action<MarketWeekChange> handler = WeekAdvanced;
                if (handler != null)
                    handler(change);
            }
            return changes;
        }

        public IList<MarketWeekChange> AdvanceWeeks(int weekCount, MarketTime time)
        {
            if (weekCount < 0)
                throw new ArgumentOutOfRangeException("weekCount");
            weekCount = Math.Min(weekCount, PendingWeekCount);
            if (weekCount == 0)
                return new List<MarketWeekChange>();

            int lastProcessed = _lastSettledWeek;
            var times = new MarketTime[weekCount];
            for (int i = 0; i < weekCount; i++)
                times[i] = time;
            return AdvanceWeeks(new MarketClockAdvancement(
                times,
                lastProcessed + 1,
                lastProcessed + weekCount,
                false));
        }

        public double GetCurrentPrice(string itemId)
        {
            SecurityBinding binding = _state.FindSecurityBinding(itemId);
            if (binding == null || !_state.IsItemVisibleAt(itemId, _state.Clock.MarketWeekIndex))
                throw new KeyNotFoundException("Item '" + itemId + "' has not appeared.");
            return RoundToWen(binding.LastPrice);
        }

        public static double RoundToWen(double price)
        {
            if (double.IsNaN(price) || double.IsInfinity(price))
                throw new ArgumentOutOfRangeException("price");
            if (price <= 1.0)
                return 1.0;
            return Math.Max(1.0, Math.Round(price, 0, MidpointRounding.AwayFromZero));
        }

        private MarketWeekChange AdvanceOne(int marketWeekIndex, MarketTime time)
        {
            ValidateTime(time);
            if (time.Year < _state.CurrentGameYear)
                throw new InvalidOperationException("Market time cannot move backwards during weekly settlement.");

            AnnualIndexBinding annualBinding = _annualIndices.EnsureBinding(_state, time.Year);
            PriceSeriesSample indexSample = _annualIndices.Advance(annualBinding);
            ElasticityResult indexElasticity = _elasticity.Apply(_state.IndexElasticity, indexSample.BaseReturn);
            double indexReturn = PriceSeries.ClampReturn(indexElasticity.Return);
            double indexValue = _state.CurrentIndex * (1.0 + indexReturn);
            if (double.IsNaN(indexValue) || double.IsInfinity(indexValue) || indexValue <= 0.0)
                throw new InvalidOperationException("The index compound price must remain finite and positive.");

            _state.CurrentIndex = indexValue;
            _state.CurrentGameYear = time.Year;
            var indexPoint = new WeeklyPoint
            {
                ItemId = string.Empty,
                MarketWeekIndex = marketWeekIndex,
                GameYear = time.Year,
                GameMonth = time.Month,
                GameRound = time.Round,
                SourceRecordIndex = indexSample.SourceRecordIndex,
                Price = indexValue,
                BaseReturn = indexSample.BaseReturn,
                Return = indexReturn,
                Observed = indexSample.Observed,
                DataExhausted = annualBinding.IsDataExhausted,
                Delisted = false
            };
            _state.IndexHistory.Add(indexPoint);

            var itemPoints = new List<WeeklyPoint>();
            var newlyDelisted = new List<string>();
            for (int i = 0; i < _state.SecurityBindings.Count; i++)
            {
                SecurityBinding binding = _state.SecurityBindings[i];
                if (binding == null
                    || !_state.HasItemAppeared(binding.ItemId)
                    || (binding.AppearedWeek > marketWeekIndex && !HasPricePoint(binding.ItemId, marketWeekIndex - 1))
                    || HasPricePoint(binding.ItemId, marketWeekIndex))
                {
                    continue;
                }
                bool wasDelisted = binding.IsDelisted;
                WeeklyPoint point = AdvanceItem(binding, EnsureElasticityState(binding.ItemId), indexPoint);
                _state.PriceHistory.Add(point);
                if (binding.AppearedWeek <= marketWeekIndex)
                {
                    itemPoints.Add(point);
                    if (!wasDelisted && binding.IsDelisted)
                        newlyDelisted.Add(binding.ItemId);
                }
            }

            _lastSettledWeek = marketWeekIndex;
            return new MarketWeekChange(marketWeekIndex, time, indexPoint, itemPoints, newlyDelisted);
        }

        private WeeklyPoint AdvanceItem(SecurityBinding binding, ElasticityState elasticity, WeeklyPoint indexPoint)
        {
            PriceSeriesSample sample;
            double baseReturn;
            double finalReturn;

            if (binding.IsDelisted)
            {
                sample = new PriceSeriesSample(binding.NextRecordIndex - 1, 0.0, false, false, false);
                baseReturn = 0.0;
                finalReturn = 0.0;
            }
            else
            {
                WeeklySecurity security = _repository.GetSecurity(binding.SecurityId);
                sample = PriceSeries.Advance(security, ref binding.NextRecordIndex, ref binding.IsDataExhausted, binding.CanDelist);
                if (sample.HasRecord && (!binding.CanDelist || sample.Observed))
                {
                    baseReturn = PriceSeries.ClampReturn(sample.BaseReturn + indexPoint.Return);
                    ElasticityResult elasticityResult = _elasticity.Apply(elasticity, baseReturn);
                    finalReturn = PriceSeries.ClampReturn(elasticityResult.Return);
                }
                else
                {
                    baseReturn = 0.0;
                    finalReturn = 0.0;
                }
            }

            double price = binding.LastPrice * (1.0 + finalReturn);
            if (double.IsNaN(price) || double.IsInfinity(price) || price <= 0.0)
                throw new InvalidOperationException("An item compound price must remain finite and positive.");
            binding.LastPrice = Math.Max(1.0, price);

            if (sample.HasRecord && sample.DelistingRecord)
            {
                WeeklyRecord record = _repository.GetSecurity(binding.SecurityId).GetRecord(sample.SourceRecordIndex);
                _delisting.ApplyRecord(binding, record, indexPoint.MarketWeekIndex);
            }

            var point = new WeeklyPoint
            {
                ItemId = binding.ItemId,
                MarketWeekIndex = indexPoint.MarketWeekIndex,
                GameYear = indexPoint.GameYear,
                GameMonth = indexPoint.GameMonth,
                GameRound = indexPoint.GameRound,
                SourceRecordIndex = sample.SourceRecordIndex,
                Price = RoundToWen(binding.LastPrice),
                BaseReturn = baseReturn,
                Return = finalReturn,
                Observed = sample.Observed,
                DataExhausted = binding.IsDataExhausted,
                Delisted = binding.IsDelisted
            };
            return point;
        }

        private ElasticityState EnsureElasticityState(string itemId)
        {
            ElasticityState state = _state.FindElasticityState(itemId);
            if (state != null)
                return state;
            state = new ElasticityState { ItemId = itemId, Multiplier = 1.0 };
            _state.ElasticityStates.Add(state);
            return state;
        }

        private int LastSavedIndexWeek()
        {
            int last = -1;
            for (int i = 0; i < _state.IndexHistory.Count; i++)
            {
                WeeklyPoint point = _state.IndexHistory[i];
                if (point != null && point.MarketWeekIndex <= _state.Clock.MarketWeekIndex && point.MarketWeekIndex > last)
                    last = point.MarketWeekIndex;
            }
            return last;
        }

        private MarketTime GetCurrentTime()
        {
            if (_state.Clock.CurrentGameMonth > 0)
            {
                return MarketTime.FromAbsolute(
                    _state.Clock.LastGameRound,
                    _state.Clock.CurrentGameMonth);
            }
            return new MarketTime(_state.CurrentGameYear, 4, 1);
        }

        private bool HasPricePoint(string itemId, int marketWeekIndex)
        {
            return _state.FindWeeklyPoint(itemId, marketWeekIndex) != null;
        }

        private void NormalizeState()
        {
            if (_state.Clock == null)
                _state.Clock = new MarketClockState();
            if (_state.AnnualIndexBindings == null)
                _state.AnnualIndexBindings = new List<AnnualIndexBinding>();
            if (_state.AppearedItemIds == null)
                _state.AppearedItemIds = new List<string>();
            if (_state.SecurityBindings == null)
                _state.SecurityBindings = new List<SecurityBinding>();
            if (_state.IndexHistory == null)
                _state.IndexHistory = new List<WeeklyPoint>();
            if (_state.PriceHistory == null)
                _state.PriceHistory = new List<WeeklyPoint>();
            if (_state.ElasticityStates == null)
                _state.ElasticityStates = new List<ElasticityState>();
            if (_state.IndexElasticity == null)
                _state.IndexElasticity = new ElasticityState();
            if (_state.CurrentGameYear < 1)
                _state.CurrentGameYear = 1;

            if (_state.IndexHistory.Count == 0 && _state.AnnualIndexBindings.Count == 0
                && (double.IsNaN(_state.CurrentIndex) || double.IsInfinity(_state.CurrentIndex)
                    || _state.CurrentIndex <= 0.0))
            {
                _state.CurrentIndex = 1000.0;
            }
            if (double.IsNaN(_state.CurrentIndex) || double.IsInfinity(_state.CurrentIndex)
                || _state.CurrentIndex <= 0.0)
            {
                throw new InvalidOperationException("RunState.CurrentIndex must be finite and positive.");
            }
        }

        private static void ValidateTime(MarketTime time)
        {
            if (time.Year < 1 || time.Month < 1 || time.Month > 12 || time.Round < 1 || time.Round > 3)
                throw new ArgumentOutOfRangeException("time");
        }
    }
}
