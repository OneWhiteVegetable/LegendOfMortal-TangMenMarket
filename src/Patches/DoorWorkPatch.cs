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
        private static void Prefix(LuaManager __instance, string name, out bool __state)
        {
            __state = false;
            if (!string.Equals(name, "Free", StringComparison.Ordinal))
                return;
            bool accepted = false;
            PatchGuard.Run("door_work_001.ChangeScene(prefix)", delegate(ModApplication app)
            {
                SceneController scene = SceneController.Instance;
                accepted = scene != null && !scene.IsPrepare && !scene.IsLoading &&
                    string.Equals(__instance.ScriptName, "door_work_001", StringComparison.Ordinal);
            });
            __state = accepted;
        }

        private static void Postfix(bool __state)
        {
            if (!__state)
                return;
            PatchGuard.Run("door_work_001.ChangeScene", delegate(ModApplication app)
            {
                app.MarkDoorWorkCompleted();
            });
        }
    }
}
