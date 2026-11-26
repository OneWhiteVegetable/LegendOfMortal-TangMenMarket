using System;

namespace XingShangMod.Trading
{
    internal static class WalletChange
    {
        public static bool TryApply(IPlayerWallet wallet, long initialBalance, long delta)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");

            long expectedBalance;
            long amount;
            try
            {
                expectedBalance = checked(initialBalance + delta);
                amount = delta == long.MinValue ? long.MinValue : Math.Abs(delta);
            }
            catch (OverflowException)
            {
                return false;
            }

            if (amount == long.MinValue)
                return false;

            try
            {
                if (wallet.Balance != initialBalance)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            if (delta == 0)
                return true;

            bool accepted = false;
            Exception operationException = null;
            long actualBalance;
            try
            {
                accepted = delta < 0 ? wallet.TrySpend(amount) : wallet.TryDeposit(amount);
                actualBalance = wallet.Balance;
            }
            catch (Exception exception)
            {
                operationException = exception;
                try
                {
                    actualBalance = wallet.Balance;
                }
                catch (Exception balanceException)
                {
                    throw CompensationFailure(balanceException);
                }
            }

            if (accepted && actualBalance == expectedBalance)
                return true;
            if (actualBalance == initialBalance)
                return false;

            TryRestore(wallet, initialBalance, actualBalance, operationException);
            return false;
        }

        private static void TryRestore(
            IPlayerWallet wallet,
            long initialBalance,
            long actualBalance,
            Exception operationException)
        {
            long delta;
            try
            {
                delta = checked(initialBalance - actualBalance);
            }
            catch (OverflowException exception)
            {
                throw CompensationFailure(exception);
            }

            if (delta == 0 || delta == long.MinValue)
            {
                if (delta == 0)
                    return;
                throw CompensationFailure(operationException);
            }

            Exception restoreException = operationException;
            try
            {
                long amount = Math.Abs(delta);
                if (delta < 0)
                    wallet.TrySpend(amount);
                else
                    wallet.TryDeposit(amount);
            }
            catch (Exception exception)
            {
                restoreException = exception;
            }

            try
            {
                if (wallet.Balance == initialBalance)
                    return;
            }
            catch (Exception exception)
            {
                restoreException = exception;
            }

            throw CompensationFailure(restoreException);
        }

        private static InvalidOperationException CompensationFailure(Exception innerException)
        {
            return new InvalidOperationException(
                "The wallet could not be restored after a failed cash mutation; trading state was not committed.",
                innerException);
        }
    }
}
