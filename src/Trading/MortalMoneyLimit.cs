using System;

namespace XingShangMod.Trading
{
    internal static class MortalMoneyLimit
    {
        internal const string UncapPluginId = "com.lom.mod.uncapsixstats";
        internal const int Minimum = int.MinValue;
        internal const int Maximum = 100000000;

        internal static int Clamp(long value)
        {
            return (int)Math.Max(Minimum, Math.Min(Maximum, value));
        }
    }
}
