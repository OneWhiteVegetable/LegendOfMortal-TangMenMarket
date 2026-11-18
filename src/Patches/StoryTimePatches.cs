using HarmonyLib;
using Mortal.Story;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    internal static class StoryTimePatches
    {
        [HarmonyPatch(typeof(LuaManager), "NextRound")]
        [HarmonyPostfix]
        private static void NextRoundPostfix()
        {
            PatchGuard.Run("LuaManager.NextRound", delegate(ModApplication app)
            {
                app.OnStoryTimeUpdated();
            });
        }

        [HarmonyPatch(typeof(LuaManager), "NextMonth")]
        [HarmonyPostfix]
        private static void NextMonthPostfix()
        {
            PatchGuard.Run("LuaManager.NextMonth", delegate(ModApplication app)
            {
                app.OnStoryTimeUpdated();
            });
        }

        [HarmonyPatch(typeof(LuaManager), "SetGameTime")]
        [HarmonyPostfix]
        private static void SetGameTimePostfix()
        {
            PatchGuard.Run("LuaManager.SetGameTime", delegate(ModApplication app)
            {
                app.OnStoryTimeUpdated();
            });
        }
    }
}
