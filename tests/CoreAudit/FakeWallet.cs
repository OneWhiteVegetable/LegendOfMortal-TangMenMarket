using System;
using XingShangMod.Trading;

internal sealed class FakeWallet : IPlayerWallet
{
    private long _min;
    private long _max;

    public long Balance { get; private set; }

    public FakeWallet(long balance)
    {
        Balance = balance;
        _min = MortalMoneyLimit.Minimum;
        _max = MortalMoneyLimit.Maximum;
    }

    public void Set(long balance)
    {
        Balance = balance;
    }

    public void Set(long balance, long min, long max)
    {
        Balance = balance;
        _min = min;
        _max = max;
    }

    public bool CanSpend(long amount)
    {
        return amount >= 0 && Balance >= _min && amount <= Balance - _min;
    }

    public bool CanDeposit(long amount)
    {
        if (amount < 0)
            return false;
        try
        {
            return checked(Balance + amount) <= _max;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public bool TrySpend(long amount)
    {
        if (!CanSpend(amount))
            return false;
        Balance -= amount;
        return true;
    }

    public bool TryDeposit(long amount)
    {
        if (!CanDeposit(amount))
            return false;
        Balance += amount;
        return true;
    }
}
