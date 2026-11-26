using HarmonyLib;
using Mortal.Core;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.Patches
{
    [HarmonyPatch(typeof(GameStatText), "Awake")]
    internal static class NativeMoneyHudPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameStatText __instance, GameStat ____statData, Text ____text)
        {
            if (____statData == null || ____statData.StatType != GameStatType.銀兩 || ____text == null)
                return;
            NativeMoneyHud.Attach(____text);
        }
    }
}
