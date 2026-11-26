using System;
using System.Collections.Generic;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Persistence;

namespace XingShangMod.Market
{
    public sealed class AnnualIndexService
    {
        private readonly WeeklyDataRepository _repository;
        private readonly MarketSelector _selector;

        public AnnualIndexService(WeeklyDataRepository repository, MarketSelector selector)
        {
            if (repository == null)
                throw new ArgumentNullException("repository");
            if (selector == null)
                throw new ArgumentNullException("selector");
            _repository = repository;
            _selector = selector;
        }

        public AnnualIndexBinding EnsureBinding(RunState state, int year)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (year < 1)
                throw new ArgumentOutOfRangeException("year");

            AnnualIndexBinding previous = state.FindAnnualIndexBinding(year - 1);
            return EnsureBinding(state, year, previous == null ? null : previous.SecurityId);
        }

        public AnnualIndexBinding EnsureBinding(RunState state, int year, string previousSecurityId)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (year < 1)
                throw new ArgumentOutOfRangeException("year");
            if (state.AnnualIndexBindings == null)
                state.AnnualIndexBindings = new List<AnnualIndexBinding>();

            AnnualIndexBinding existing = state.FindAnnualIndexBinding(year);
            if (existing != null)
                return existing;

            AnnualIndexBinding binding = _selector.CreateAnnualIndexBinding(
                year,
                state.CurrentIndex,
                previousSecurityId);
            state.AnnualIndexBindings.Add(binding);
            return binding;
        }

        internal PriceSeriesSample Advance(AnnualIndexBinding binding)
        {
            if (binding == null)
                throw new ArgumentNullException("binding");
            WeeklySecurity security = _repository.GetSecurity(binding.SecurityId);
            return PriceSeries.Advance(security, ref binding.NextRecordIndex, ref binding.IsDataExhausted);
        }
    }
}
