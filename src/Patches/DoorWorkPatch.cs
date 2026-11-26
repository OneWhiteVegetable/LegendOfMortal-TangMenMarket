using System;
using HarmonyLib;
using Mortal.Core;
using Mortal.Story;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    [HarmonyPatch(typeof(LuaManager), "ChangeScene")]
    internal static class DoorWorkPatch
    {
        private static bool Prefix(LuaManager __instance, string name, string key, string nextScene)
        {
            if (DoorWorkSession.IsContinuation(__instance, name, key, nextScene))
                return true;

            DoorWorkSession.CloseActive(DoorWorkCloseReason.SceneChanged);
            if (!string.Equals(name, "Free", StringComparison.Ordinal))
                return true;

            bool skipOriginal = false;
            PatchGuard.Run("door_work.ChangeScene(prefix)", delegate(ModApplication app)
            {
                SceneController scene = SceneController.Instance;
                if (scene == null || scene.IsPrepare || scene.IsLoading ||
                    !IsSupportedScript(__instance.ScriptName))
                    return;

                skipOriginal = app.TryOpenMarketFromDoorWork(__instance, name, key, nextScene);
            });
            return !skipOriginal;
        }

        private static bool IsSupportedScript(string scriptName)
        {
            switch (scriptName)
            {
                case "door_work_001":
                case "door_work_101":
                case "door_work_201":
                case "door_work_301":
                case "door_work_401":
                case "door_work_402":
                    return true;
                default:
                    return false;
            }
        }
    }
}
