using System;

namespace XingShangMod.UI.Market
{
    internal static class RankingOrdering
    {
        internal static int Compare(
            bool leftHasPrice,
            double leftReturn,
            int leftAppearanceOrder,
            bool rightHasPrice,
            double rightReturn,
            int rightAppearanceOrder,
            bool ascending)
        {
            bool leftRankable = leftHasPrice && IsFinite(leftReturn);
            bool rightRankable = rightHasPrice && IsFinite(rightReturn);
            if (leftRankable != rightRankable)
                return leftRankable ? -1 : 1;
            if (leftRankable)
            {
                int comparison = ascending
                    ? leftReturn.CompareTo(rightReturn)
                    : rightReturn.CompareTo(leftReturn);
                if (comparison != 0)
                    return comparison;
            }
            return leftAppearanceOrder.CompareTo(rightAppearanceOrder);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
