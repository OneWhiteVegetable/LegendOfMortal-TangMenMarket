using System;
using XingShangMod.Domain;

namespace XingShangMod.Market
{
    public sealed class ElasticityResult
    {
        public double BaseReturn { get; private set; }
        public double Return { get; private set; }
        public double Multiplier { get; private set; }
        public bool Entered { get; private set; }
        public bool Exited { get; private set; }

        internal ElasticityResult(double baseReturn, double finalReturn, double multiplier, bool entered, bool exited)
        {
            BaseReturn = baseReturn;
            Return = finalReturn;
            Multiplier = multiplier;
            Entered = entered;
            Exited = exited;
        }
    }

    public sealed class ElasticityStateMachine
    {
        public const double QuietThreshold = 0.02;
        public const double FourthWeekThreshold = 0.032;
        public const double FifthWeekThreshold = 0.048;
        public const double ExitThreshold = 0.05;

        public ElasticityResult Apply(ElasticityState state, double baseReturn)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (double.IsNaN(baseReturn) || double.IsInfinity(baseReturn))
                throw new ArgumentOutOfRangeException("baseReturn");

            if (!state.IsActive)
                return ApplyInactive(state, baseReturn);
            return ApplyActive(state, baseReturn);
        }

        private static ElasticityResult ApplyInactive(ElasticityState state, double baseReturn)
        {
            state.Multiplier = 1.0;
            state.ActiveWeekCount = 0;
            if (Math.Abs(baseReturn) <= QuietThreshold)
                state.QuietCount++;
            else
                state.QuietCount = 0;

            bool entered = state.QuietCount >= 3;
            if (entered)
            {
                state.QuietCount = 3;
                state.IsActive = true;
                state.Multiplier = 2.0;
            }

            return new ElasticityResult(baseReturn, baseReturn, 1.0, entered, false);
        }

        private static ElasticityResult ApplyActive(ElasticityState state, double baseReturn)
        {
            double multiplier = state.Multiplier;
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier < 2.0)
                multiplier = 2.0;

            double finalReturn = baseReturn * multiplier;
            if (double.IsNaN(finalReturn) || double.IsInfinity(finalReturn))
                throw new InvalidOperationException("Elasticity produced a non-finite return.");
            if (state.ActiveWeekCount < int.MaxValue)
                state.ActiveWeekCount++;

            if (Math.Abs(finalReturn) > ExitThreshold)
            {
                Reset(state);
                return new ElasticityResult(baseReturn, finalReturn, multiplier, false, true);
            }

            long overallWeek = (long)state.ActiveWeekCount + 3;
            if (overallWeek == 4)
            {
                state.Multiplier = Math.Abs(finalReturn) <= FourthWeekThreshold ? 2.5 : multiplier;
            }
            else if (overallWeek == 5)
            {
                state.Multiplier = Math.Abs(finalReturn) <= FifthWeekThreshold ? 3.0 : multiplier;
            }
            else
            {
                state.Multiplier = 3.0 + 0.5 * Math.Max(0, overallWeek - 6);
            }

            return new ElasticityResult(baseReturn, finalReturn, multiplier, false, false);
        }

        private static void Reset(ElasticityState state)
        {
            state.QuietCount = 0;
            state.IsActive = false;
            state.ActiveWeekCount = 0;
            state.Multiplier = 1.0;
        }
    }
}
