using System;
using System.Reflection;
using HarmonyLib;
using Mortal.Core;
using XingShangMod.Trading;

namespace XingShangMod.Patches
{
    [HarmonyPatch(typeof(GameStat))]
    internal static class SilverStatPatches
    {
        [HarmonyPatch(nameof(GameStat.AddValue))]
        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyBefore(MortalMoneyLimit.UncapPluginId)]
        private static void BeforeAdd(GameStat __instance, ref int value, ref int ____min, ref int ____max)
        {
            if (__instance.StatType != GameStatType.銀兩)
                return;
            ____min = MortalMoneyLimit.Minimum;
            ____max = MortalMoneyLimit.Maximum;
            // Give the host a representable delta. Its original AddValue still dispatches its event and totalAdd.
            int current = __instance.Value;
            long sum = (long)current + value;
            if (sum < int.MinValue || sum > int.MaxValue)
                value = checked((int)((long)MortalMoneyLimit.Clamp(sum) - current));
        }

        [HarmonyPatch(nameof(GameStat.SetValue))]
        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyBefore(MortalMoneyLimit.UncapPluginId)]
        private static void BeforeSet(GameStat __instance, ref int value, ref int ____min, ref int ____max)
        {
            if (__instance.StatType != GameStatType.銀兩)
                return;
            ____min = MortalMoneyLimit.Minimum;
            ____max = MortalMoneyLimit.Maximum;
            value = MortalMoneyLimit.Clamp(value);
        }

        [HarmonyPatch(nameof(GameStat.SetMax))]
        [HarmonyPrefix]
        private static void BeforeSetMax(GameStat __instance, ref int max)
        {
            if (__instance.StatType == GameStatType.銀兩)
                max = MortalMoneyLimit.Maximum;
        }

        [HarmonyPatch(nameof(GameStat.Min), MethodType.Getter)]
        [HarmonyPostfix]
        private static void Minimum(GameStat __instance, ref int __result)
        {
            if (__instance.StatType == GameStatType.銀兩)
                __result = MortalMoneyLimit.Minimum;
        }

        [HarmonyPatch(nameof(GameStat.Max), MethodType.Getter)]
        [HarmonyPostfix]
        private static void Maximum(GameStat __instance, ref int __result)
        {
            if (__instance.StatType == GameStatType.銀兩)
                __result = MortalMoneyLimit.Maximum;
        }

        [HarmonyPatch(nameof(GameStat.FinalValue), MethodType.Getter)]
        [HarmonyPostfix, HarmonyAfter(MortalMoneyLimit.UncapPluginId)]
        private static void EffectiveValue(GameStat __instance, ref int __result)
        {
            if (__instance.StatType == GameStatType.銀兩)
                __result = MortalMoneyLimit.Clamp((long)__instance.Value + __instance.AdditionValue);
        }

        // UncapSixStats normally replaces SetValue/AddValue. For silver, the host already has our
        // real bounds and must run its original body, including events and other mods' postfixes.
        internal static void InstallUncapCompatibility(Harmony harmony)
        {
            Type plugin = Type.GetType("UncapSixStats.Plugin, UncapSixStats", false);
            if (plugin == null)
                return;
            MethodInfo filter = typeof(SilverStatPatches).GetMethod(nameof(UseNativeSilverBounds),
                BindingFlags.NonPublic | BindingFlags.Static);
            foreach (string name in new[] { "IsUncapped", "TryGetMaxOverride" })
            {
                MethodInfo rule = AccessTools.Method(plugin, name);
                if (rule != null)
                    harmony.Patch(rule, postfix: new HarmonyMethod(filter));
            }
        }

        private static void UseNativeSilverBounds(GameStat stat, ref bool __result)
        {
            if (__result && stat != null && stat.StatType == GameStatType.銀兩)
                __result = false;
        }
    }
}
