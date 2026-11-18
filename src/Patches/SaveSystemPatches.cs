using HarmonyLib;
using Mortal.Core;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    internal static class SaveSystemPatches
    {
        [HarmonyPatch(typeof(SaveSystem), "NewGameData")]
        [HarmonyPrefix]
        private static void NewGamePrefix()
        {
            PatchGuard.Run("SaveSystem.NewGameData", delegate(ModApplication app)
            {
                app.OnNewGamePending(false);
            });
        }

        [HarmonyPatch(typeof(SaveSystem), "NewActGameData")]
        [HarmonyPrefix]
        private static void NewActPrefix()
        {
            PatchGuard.Run("SaveSystem.NewActGameData", delegate(ModApplication app)
            {
                app.OnNewGamePending(true);
            });
        }

        [HarmonyPatch(typeof(SaveSystem), "CreateSaveData")]
        [HarmonyPostfix]
        private static void CreateSaveDataPostfix(GameSave __result)
        {
            PatchGuard.Run("SaveSystem.CreateSaveData", delegate(ModApplication app)
            {
                app.OnCreateSaveData(__result);
            });
        }

        [HarmonyPatch(typeof(SaveSystem), "ExecuteSaveData")]
        [HarmonyPostfix]
        private static void ExecuteSaveDataPostfix(string saveFile, object gameSave)
        {
            PatchGuard.Run("SaveSystem.ExecuteSaveData", delegate(ModApplication app)
            {
                app.OnExecuteSaveData(saveFile, gameSave);
            });
        }

        [HarmonyPatch(typeof(SaveSystem), "ExecuteLoadGameData")]
        [HarmonyPostfix]
        private static void ExecuteLoadGameDataPostfix(GameSave gameSave)
        {
            PatchGuard.Run("SaveSystem.ExecuteLoadGameData", delegate(ModApplication app)
            {
                app.OnExecuteLoadGameData(gameSave);
            });
        }
    }
}
