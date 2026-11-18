using HarmonyLib;
using Mortal.Core;
using XingShangMod.App;
using XingShangMod.UI;

namespace XingShangMod.Patches
{
    internal static class MenuPanelPatches
    {
        [HarmonyPatch(typeof(MenuPanel), "Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(MenuPanel __instance)
        {
            Attach(__instance);
        }

        [HarmonyPatch(typeof(MenuPanel), "OnPanelOpen")]
        [HarmonyPostfix]
        private static void OpenPostfix(MenuPanel __instance)
        {
            Attach(__instance);
        }

        private static void Attach(MenuPanel panel)
        {
            PatchGuard.Run("MenuPanel.Attach", delegate(ModApplication app)
            {
                ModUserInterface ui = app.UI as ModUserInterface;
                if (ui != null)
                    ui.AttachSystemMenu(panel);
            });
        }
    }
}
