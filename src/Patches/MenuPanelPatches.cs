using HarmonyLib;
using Mortal.Core;
using Mortal.Free;
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

        [HarmonyPatch(typeof(ModUserInterface), "AttachSystemMenu")]
        [HarmonyPrefix]
        private static bool AttachSystemMenuPrefix(MenuPanel panel)
        {
            return IsFreeContext(panel);
        }

        private static void Attach(MenuPanel panel)
        {
            PatchGuard.Run("MenuPanel.Attach", delegate(ModApplication app)
            {
                if (!IsFreeContext(panel))
                    return;

                ModUserInterface ui = app.UI as ModUserInterface;
                if (ui == null || !ui.IsAlive)
                {
                    ui = new ModUserInterface(app);
                    app.AttachUI(ui);
                }
                ui.AttachSystemMenu(panel);
            });
        }

        private static bool IsFreeContext(MenuPanel panel)
        {
            FreeManager free = FreeManager.Instance;
            return panel != null && free != null && free.isActiveAndEnabled &&
                string.Equals(panel.gameObject.scene.name, "Free", System.StringComparison.Ordinal);
        }
    }
}
