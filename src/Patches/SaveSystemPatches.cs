using System.Reflection;
using HarmonyLib;
using Mortal.Core;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    internal static class SaveSystemPatches
    {
        private static readonly MethodInfo GetSaveDataFileMethod = typeof(SaveSystem).GetMethod(
            "GetSaveDataFile",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static string _loadSaveFile;
        private static string _activeSaveFile;
        private static bool _restoreLoad;

        [HarmonyPatch(typeof(SaveSystem), "NewGameData")]
        [HarmonyPrefix]
        private static void NewGamePrefix()
        {
            _activeSaveFile = null;
            ClearLoadContext();
            DoorWorkSession.Cancel();
            PatchGuard.Run("SaveSystem.NewGameData", delegate(ModApplication app)
            {
                app.OnNewGamePending(false);
            });
        }

        [HarmonyPatch(typeof(SaveSystem), "NewActGameData")]
        [HarmonyPrefix]
        private static void NewActPrefix()
        {
            _activeSaveFile = null;
            ClearLoadContext();
            DoorWorkSession.Cancel();
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
        [HarmonyPrefix]
        private static void ExecuteSaveDataPrefix(string saveFile, object gameSave)
        {
            if (!(gameSave is GameSave) && !(gameSave is AutoGameSave))
                return;
            PatchGuard.Run("SaveSystem.ExecuteSaveData.Prepare", delegate(ModApplication app)
            {
                app.OnExecuteSaveDataPending(saveFile, gameSave);
            });
        }

        [HarmonyPatch(typeof(SaveSystem), "ExecuteSaveData")]
        [HarmonyPostfix]
        private static void ExecuteSaveDataPostfix(string saveFile, object gameSave)
        {
            if (!(gameSave is GameSave) && !(gameSave is AutoGameSave))
                return;
            PatchGuard.Run("SaveSystem.ExecuteSaveData", delegate(ModApplication app)
            {
                app.OnExecuteSaveData(saveFile, gameSave);
            });
            _activeSaveFile = saveFile;
        }

        [HarmonyPatch(typeof(SaveSystem), "LoadGameData")]
        [HarmonyPrefix]
        private static void LoadGameDataPrefix(SaveSystem __instance)
        {
            _restoreLoad = false;
            _loadSaveFile = GetSaveDataFile(__instance, __instance.CurrentSlot);
        }

        [HarmonyPatch(typeof(SaveSystem), "LoadGameData")]
        [HarmonyPostfix]
        private static void LoadGameDataPostfix()
        {
            ClearLoadContext();
        }

        [HarmonyPatch(typeof(SaveSystem), "AutoLoadGameData")]
        [HarmonyPrefix]
        private static void AutoLoadGameDataPrefix(SaveSystem __instance, string slot)
        {
            _restoreLoad = false;
            _loadSaveFile = GetSaveDataFile(__instance, slot);
        }

        [HarmonyPatch(typeof(SaveSystem), "AutoLoadGameData")]
        [HarmonyPostfix]
        private static void AutoLoadGameDataPostfix()
        {
            ClearLoadContext();
        }

        [HarmonyPatch(typeof(SaveSystem), "LoadRestoreSaveData")]
        [HarmonyPrefix]
        private static void LoadRestoreSaveDataPrefix(SaveSystem __instance)
        {
            _restoreLoad = true;
            _loadSaveFile = _activeSaveFile;
            if (string.IsNullOrWhiteSpace(_loadSaveFile))
                _loadSaveFile = GetSaveDataFile(__instance, __instance.CurrentSlot);
        }

        [HarmonyPatch(typeof(SaveSystem), "LoadRestoreSaveData")]
        [HarmonyPostfix]
        private static void LoadRestoreSaveDataPostfix()
        {
            ClearLoadContext();
        }

        [HarmonyPatch(typeof(SaveSystem), "ExecuteLoadGameData")]
        [HarmonyPrefix]
        private static void ExecuteLoadGameDataPrefix()
        {
            DoorWorkSession.Cancel();
        }

        [HarmonyPatch(typeof(SaveSystem), "ExecuteLoadGameData")]
        [HarmonyPostfix]
        private static void ExecuteLoadGameDataPostfix(GameSave gameSave)
        {
            DoorWorkSession.Cancel();
            string saveFile = _loadSaveFile;
            bool isRestore = _restoreLoad;
            if (!isRestore && !string.IsNullOrWhiteSpace(saveFile))
                _activeSaveFile = saveFile;
            ClearLoadContext();
            PatchGuard.Run("SaveSystem.ExecuteLoadGameData", delegate(ModApplication app)
            {
                app.OnExecuteLoadGameData(gameSave, saveFile, isRestore);
            });
        }

        private static string GetSaveDataFile(SaveSystem instance, string slot)
        {
            if (instance == null || string.IsNullOrWhiteSpace(slot) || GetSaveDataFileMethod == null)
                return null;
            try
            {
                return GetSaveDataFileMethod.Invoke(instance, new object[] { slot }) as string;
            }
            catch
            {
                return null;
            }
        }

        private static void ClearLoadContext()
        {
            _loadSaveFile = null;
            _restoreLoad = false;
        }
    }
}
