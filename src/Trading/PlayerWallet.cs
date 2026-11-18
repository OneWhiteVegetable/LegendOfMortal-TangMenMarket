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
        }

        public long Balance
        {
            get { return _money.Value; }
        }

        public bool CanSpend(long amount)
        {
            long balance = Balance;
            if (amount < 0 || amount > balance)
                return false;

            long target = balance - amount;
            return target >= _money.Min && target <= _money.Max && amount <= int.MaxValue;
        }

        public bool CanDeposit(long amount)
        {
            if (amount < 0)
                return false;

            long balance = Balance;
            long target;
            try
            {
                target = checked(balance + amount);
            }
            catch (OverflowException)
            {
                return false;
            }

            return target >= _money.Min && target <= _money.Max && amount <= int.MaxValue;
        }

        public bool TrySpend(long amount)
        {
            if (!CanSpend(amount))
                return false;

            _money.AddValue(-(int)amount);
            return true;
        }

        public bool TryDeposit(long amount)
        {
            if (!CanDeposit(amount))
                return false;

            _money.AddValue((int)amount);
            return true;
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
