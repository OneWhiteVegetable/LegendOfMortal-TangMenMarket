using System;
using System.Threading;
using Mortal.Story;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    internal enum DoorWorkCloseReason
    {
        UserClosed,
        SceneChanged,
        LoadGame,
        NewGame,
        NewAct,
        ModDisabled,
        UiDisposed,
        PluginDestroyed,
        Cancelled
    }

    internal sealed class DoorWorkSession
    {
        private static DoorWorkSession _active;
        private static DoorWorkSession _continuing;

        private readonly LuaManager _manager;
        private readonly string _name;
        private readonly string _key;
        private readonly string _nextScene;
        private readonly string _runId;
        private StoryManager _storyManager;
        private bool _storyManagerWasEnabled;
        private int _closed;

        internal DoorWorkSession(
            LuaManager manager,
            string name,
            string key,
            string nextScene,
            string runId)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");
            if (string.IsNullOrWhiteSpace(runId))
                throw new ArgumentException("A door-work session requires a RunId.", "runId");

            _manager = manager;
            _name = name;
            _key = key;
            _nextScene = nextScene;
            _runId = runId;
        }

        internal bool Activate()
        {
            if (Volatile.Read(ref _closed) != 0)
                return false;
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _active, this, null), null))
                return false;

            try
            {
                SuspendStoryInput();
                return true;
            }
            catch
            {
                Interlocked.Exchange(ref _closed, 1);
                Interlocked.CompareExchange(ref _active, null, this);
                RestoreStoryInput();
                throw;
            }
        }

        internal void OnClosed()
        {
            OnClosed(DoorWorkCloseReason.UserClosed);
        }

        internal void OnClosed(DoorWorkCloseReason reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return;
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _active, null, this), this))
                return;

            RestoreStoryInput();
            if (reason == DoorWorkCloseReason.UserClosed)
                Continue(this);
        }

        internal static void CloseActive()
        {
            CloseActive(DoorWorkCloseReason.Cancelled);
        }

        internal static void CloseActive(DoorWorkCloseReason reason)
        {
            DoorWorkSession session = Volatile.Read(ref _active);
            if (session != null)
                session.OnClosed(reason);
        }

        internal static void Cancel()
        {
            CloseActive(DoorWorkCloseReason.Cancelled);
        }

        internal static bool IsContinuation(
            LuaManager manager,
            string name,
            string key,
            string nextScene)
        {
            DoorWorkSession session = Volatile.Read(ref _continuing);
            return session != null && ReferenceEquals(session._manager, manager) &&
                string.Equals(session._name, name, StringComparison.Ordinal) &&
                string.Equals(session._key, key, StringComparison.Ordinal) &&
                string.Equals(session._nextScene, nextScene, StringComparison.Ordinal);
        }

        private void SuspendStoryInput()
        {
            StoryManager story = StoryManager.Instance;
            if (story == null)
                return;

            _storyManager = story;
            _storyManagerWasEnabled = story.enabled;
            if (_storyManagerWasEnabled)
                story.enabled = false;
        }

        private void RestoreStoryInput()
        {
            StoryManager story = _storyManager;
            bool wasEnabled = _storyManagerWasEnabled;
            _storyManager = null;
            _storyManagerWasEnabled = false;
            if (story != null && wasEnabled)
                story.enabled = true;
        }

        private static void Continue(DoorWorkSession session)
        {
            if (!IsCurrentContext(session))
                return;

            Volatile.Write(ref _continuing, session);
            try
            {
                session._manager.ChangeScene(session._name, session._key, session._nextScene);
            }
            finally
            {
                Volatile.Write(ref _continuing, null);
            }
        }

        private static bool IsCurrentContext(DoorWorkSession session)
        {
            if (session._manager == null || !ReferenceEquals(LuaManager.Instance, session._manager))
                return false;

            ModApplication app = ModApplication.Instance;
            return app != null && app.IsEnabled && app.State != null &&
                string.Equals(app.State.RunId, session._runId, StringComparison.Ordinal);
        }
    }
}
