using System;
using Mortal.Core;

namespace XingShangMod.Trading
{
    public interface IPlayerWallet
    {
        long Balance { get; }
        bool CanSpend(long amount);
        bool CanDeposit(long amount);
        bool TrySpend(long amount);
        bool TryDeposit(long amount);
    }

    public sealed class MortalWallet : IPlayerWallet
    {
        private readonly GameStat _money;

        public MortalWallet()
            : this(GetMortalStats())
        {
        }

        public MortalWallet(PlayerStatData stats)
        {
            if (stats == null)
                throw new ArgumentNullException("stats");

            _money = stats.Get(GameStatType.銀兩);
            if (_money == null)
                throw new InvalidOperationException("The original silver stat is unavailable.");
            if (_money.Value > MortalMoneyLimit.Maximum)
                _money.SetValue(MortalMoneyLimit.Maximum);
        }

        public long Balance
        {
            get { return _money.Value; }
        }

        public bool CanSpend(long amount)
        {
            return amount >= 0 && amount <= Balance - MortalMoneyLimit.Minimum;
        }

        public bool CanDeposit(long amount)
        {
            return amount >= 0 && amount <= MortalMoneyLimit.Maximum - Balance;
        }

        public bool TrySpend(long amount)
        {
            if (!CanSpend(amount))
                return false;

            Add(-amount);
            return true;
        }

        public bool TryDeposit(long amount)
        {
            if (!CanDeposit(amount))
                return false;

            Add(amount);
            return true;
        }

        private void Add(long delta)
        {
            // The full signed silver range is wider than one Int32 delta.
            int first = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, delta));
            _money.AddValue(first);
            if (delta != first)
                _money.AddValue((int)(delta - first));
        }

        private static PlayerStatData GetMortalStats()
        {
            PlayerStatManagerData manager = PlayerStatManagerData.Instance;
            if (manager == null || manager.Stats == null)
                throw new InvalidOperationException("The original player stats are unavailable.");
            return manager.Stats;
        }
    }
}
