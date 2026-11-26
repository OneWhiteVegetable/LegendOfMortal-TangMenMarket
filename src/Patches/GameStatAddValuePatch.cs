using HarmonyLib;
using Mortal.Core;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    [HarmonyPatch(typeof(GameStat), "AddValue")]
    internal static class GameStatAddValuePatch
    {
        private static void Prefix(GameStat __instance, out int __state)
        {
            __state = __instance.Value;
        }

        private static void Postfix(GameStat __instance, int value, int __state)
        {
            long actualDelta = (long)__instance.Value - __state;
            if (__instance.StatType == GameStatType.行動次數 && value < 0 && actualDelta < 0)
            {
                int spent = (int)System.Math.Min(int.MaxValue, -actualDelta);
                PatchGuard.Run("GameStat.AddValue(action)", delegate(ModApplication app)
                {
                    app.OnActionPointsSpent(spent);
                });
            }
            else if (__instance.StatType == GameStatType.銀兩 && value > 0 && actualDelta > 0)
            {
                PatchGuard.Run("GameStat.AddValue(silver)", delegate(ModApplication app)
                {
                    app.OnNonTradeMoneyIncome();
                });
            }
        }
    }
}
