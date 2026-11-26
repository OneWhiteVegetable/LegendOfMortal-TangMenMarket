using System;
using XingShangMod.Persistence;

namespace XingShangMod.Trading
{
    internal static class LegacySilverDebt
    {
        internal static void Restore(RunState state, IPlayerWallet wallet, long savedCash)
        {
            // Use the silver in this particular host save (including RestoreData), before its new
            // upper limit was applied. Leave the original slot untouched until the next real save.
            long absorbed = Math.Min(state.Debt, savedCash - MortalMoneyLimit.Minimum);
            long target = MortalMoneyLimit.Clamp(savedCash - absorbed);
            long balance = wallet.Balance;
            if (!WalletChange.TryApply(wallet, balance, target - balance))
                throw new InvalidOperationException("Could not migrate saved silver and market debt.");
            state.Debt -= absorbed;
        }
    }
}
