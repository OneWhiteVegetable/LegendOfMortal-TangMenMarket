using HarmonyLib;
using Mortal.Core;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    [HarmonyPatch(typeof(PlayerStatManagerData), "UpdateGameTime")]
    internal static class NaturalTimePatch
    {
        private static void Postfix()
        {
            PatchGuard.Run("PlayerStatManagerData.UpdateGameTime", delegate(ModApplication app)
            {
                app.OnNaturalTimeUpdated();
            });
        }
    }
}
