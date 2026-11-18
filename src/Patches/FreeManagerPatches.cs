using HarmonyLib;
using Mortal.Free;
using XingShangMod.App;
using XingShangMod.UI;

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
                ModUserInterface ui = app.UI as ModUserInterface;
                if (ui == null || !ui.IsAlive)
                    app.AttachUI(new ModUserInterface(app));
                app.OnFreeFrame(__instance);
            });
        }
    }
}
