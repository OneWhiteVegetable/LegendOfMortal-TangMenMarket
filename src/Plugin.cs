using System;
using System.Collections.Generic;
using System.Threading;
using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using XingShangMod.App;
using XingShangMod.Patches;
using XingShangMod.UI;

namespace XingShangMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.xingshang.tangmenmarket";
        public const string PluginName = "唐门行市";
        public const string PluginVersion = "1.0.0";

        private static readonly object QueueLock = new object();
        private static readonly Queue<Action> MainThreadActions = new Queue<Action>();
        private static int _mainThreadId;
        private Harmony _harmony;
        private ModUserInterface _ui;

        public static Plugin Current { get; private set; }
        public static bool IsMainThread { get { return _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId; } }

        private void Awake()
        {
            Current = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            ModApplication app = null;
            try
            {
                app = new ModApplication(Logger.LogInfo, Logger.LogWarning, Logger.LogError);
                app.Initialize();
            }
            catch (Exception exception)
            {
                Logger.LogError("Mod initialization failed: " + exception);
                if (app != null)
                    app.Disable("Initialization failed: " + exception.Message);
            }

            _harmony = new Harmony(PluginGuid);
            Type[] patchTypes =
            {
                typeof(GameStatAddValuePatch),
                typeof(NaturalTimePatch),
                typeof(StoryTimePatches),
                typeof(SaveSystemPatches),
                typeof(FreeManagerPatches),
                typeof(DoorWorkPatch),
                typeof(MenuPanelPatches)
            };
            for (int i = 0; i < patchTypes.Length; i++)
            {
                try
                {
                    _harmony.PatchAll(patchTypes[i]);
                    Logger.LogInfo("Patched " + patchTypes[i].Name + ".");
                }
                catch (Exception exception)
                {
                    Logger.LogError("Patch class " + patchTypes[i].Name + " failed: " + exception);
                    if (app != null)
                        app.Disable("Game integration is incompatible; the mod was disabled: " + exception.Message);
                }
            }
        }

        private void Update()
        {
            while (true)
            {
                Action action = null;
                lock (QueueLock)
                {
                    if (MainThreadActions.Count > 0)
                        action = MainThreadActions.Dequeue();
                }
                if (action == null)
                    break;
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Logger.LogError("Main-thread action failed: " + exception);
                    if (ModApplication.Instance != null)
                        ModApplication.Instance.Disable("Main-thread integration failed: " + exception.Message);
                }
            }

            ModApplication app = ModApplication.Instance;
            if (app == null || !app.IsEnabled)
                return;
            try
            {
                if (app.State == null)
                    app.EnsureState();
                if (_ui == null && app.State != null && Mortal.Free.FreeManager.Instance != null)
                {
                    _ui = new ModUserInterface(app);
                    app.AttachUI(_ui);
                }
            }
            catch (Exception exception)
            {
                app.HandleIntegrationError("Delayed initialization", exception);
            }
        }

        private void OnDestroy()
        {
            if (_ui != null)
            {
                _ui.Dispose();
                _ui = null;
            }
            ModApplication app = ModApplication.Instance;
            if (app != null)
                app.Shutdown();
            lock (QueueLock)
                MainThreadActions.Clear();
            if (ReferenceEquals(Current, this))
                Current = null;
            _mainThreadId = 0;
        }

        public static void RunOnMainThread(Action action)
        {
            if (action == null)
                throw new ArgumentNullException("action");
            if (IsMainThread)
            {
                action();
                return;
            }
            lock (QueueLock)
                MainThreadActions.Enqueue(action);
        }
    }
}
