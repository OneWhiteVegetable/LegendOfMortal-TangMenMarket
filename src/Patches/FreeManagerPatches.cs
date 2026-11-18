using HarmonyLib;
using Mortal.Free;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    internal static class FreeManagerPatches
    {
        [HarmonyPatch(typeof(FreeManager), "Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix()
        {
            PatchGuard.Run("FreeManager.Awake", delegate(ModApplication app)
            {
                app.OnFreeAwake();
            });
        }

        [HarmonyPatch(typeof(FreeManager), "Update")]
        [HarmonyPostfix]
        private static void UpdatePostfix(FreeManager __instance)
        {
            PatchGuard.Run("FreeManager.Update", delegate(ModApplication app)
            {
                app.OnFreeFrame(__instance);
            });
        }
    }
}
