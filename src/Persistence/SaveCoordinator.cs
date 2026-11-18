using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Mortal.Core;

namespace XingShangMod.Persistence
{
    public sealed class SaveMigrationRequest
    {
        public GameSave GameSave { get; internal set; }
        public long TimeTick { get; internal set; }
        public string Profile { get; internal set; }
        public string Slot { get; internal set; }
        public string OriginalSaveId { get; internal set; }
    }

    public sealed class SaveCoordinator
    {
        private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        private readonly Func<RunState> _currentStateProvider;
        private readonly Func<RunState> _newRunStateProvider;
        private readonly Action<RunState> _stateApplier;
        private readonly Action<SaveMigrationRequest> _missingSidecarHandler;
        private bool _newRunPending;

        public SidecarStore Store { get; private set; }
        public string LastError { get; private set; }
        public bool IsNewRunPending { get { return _newRunPending; } }

        public SaveCoordinator(
            SidecarStore store,
            Func<RunState> currentStateProvider,
            Func<RunState> newRunStateProvider,
            Action<RunState> stateApplier,
            Action<SaveMigrationRequest> missingSidecarHandler)
        {
            if (store == null)
                throw new ArgumentNullException("store");
            if (currentStateProvider == null)
                throw new ArgumentNullException("currentStateProvider");
            if (newRunStateProvider == null)
                throw new ArgumentNullException("newRunStateProvider");
            if (stateApplier == null)
                throw new ArgumentNullException("stateApplier");

            Store = store;
            _currentStateProvider = currentStateProvider;
            _newRunStateProvider = newRunStateProvider;
            _stateApplier = stateApplier;
            _missingSidecarHandler = missingSidecarHandler;
        }

        public void MarkNewGamePending()
        {
            LastError = null;
            _newRunPending = true;
        }

        public void MarkNewActPending()
        {
            LastError = null;
            _newRunPending = true;
        }

        public bool CreateSaveData(GameSave gameSave)
        {
            LastError = null;
            try
            {
                if (gameSave == null || gameSave.TimeTick <= 0)
                    return Fail("CreateSaveData requires a GameSave with a positive TimeTick.");

                RunState state = _newRunPending
                    ? _newRunStateProvider()
                    : _currentStateProvider();
                if (!Store.CaptureMemorySnapshot(gameSave.TimeTick, state))
                    return Fail(Store.LastError);

                _newRunPending = false;
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool ExecuteSaveData(string saveFile, object gameSave)
        {
            LastError = null;
            try
            {
                GameSave innerSave = GetGameSave(gameSave);
                if (innerSave == null || innerSave.TimeTick <= 0)
                    return Fail("ExecuteSaveData requires GameSave or AutoGameSave.GameSave.");

                string profile;
                string slot;
                string originalSaveId;
                if (!TryGetSaveIdentity(saveFile, out profile, out slot, out originalSaveId))
                    return Fail("The original save path is outside Application.persistentDataPath or invalid.");
                if (!VerifyOriginalSave(saveFile, gameSave))
                    return Fail("The original save was not verified; no sidecar was written.");

                RunState state;
                if (!Store.TryGetMemorySnapshot(innerSave.TimeTick, out state))
                    return Fail("No CreateSaveData snapshot exists for this save version.");

                if (!Store.Persist(profile, slot, innerSave.TimeTick, originalSaveId, state))
                    return Fail(Store.LastError);
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool ExecuteLoadGameData(GameSave gameSave)
        {
            return ExecuteLoadGameData(null, null, gameSave);
        }

        public bool ExecuteLoadGameData(string profile, string slot, GameSave gameSave)
        {
            LastError = null;
            _newRunPending = false;
            try
            {
                if (gameSave == null || gameSave.TimeTick <= 0)
                    return Fail("ExecuteLoadGameData requires a GameSave with a positive TimeTick.");

                bool hasLocation = !string.IsNullOrWhiteSpace(profile) &&
                    !string.IsNullOrWhiteSpace(slot);
                RunState state = null;
                bool restored = hasLocation
                    ? Store.TryGetMemorySnapshot(profile, slot, gameSave.TimeTick, out state)
                    : Store.TryGetMemorySnapshot(gameSave.TimeTick, out state);

                if (!restored && hasLocation)
                {
                    restored = Store.TryLoadDisk(profile, slot, gameSave.TimeTick, out state);
                    if (!restored && Store.LastLoadStatus != SidecarLoadStatus.Missing)
                        return Fail(Store.LastError);
                }

                if (!restored)
                {
                    restored = Store.TryLoadDisk(gameSave.TimeTick, out state);
                    if (!restored && Store.LastLoadStatus != SidecarLoadStatus.Missing)
                        return Fail(Store.LastError);
                }

                if (restored)
                {
                    if (state == null)
                        return Fail("The restored run state is null.");
                    _stateApplier(state);
                    return true;
                }

                if (_missingSidecarHandler == null)
                    return Fail("The sidecar is missing and no migration handler is configured.");

                _missingSidecarHandler(new SaveMigrationRequest
                {
                    GameSave = gameSave,
                    TimeTick = gameSave.TimeTick,
                    Profile = profile ?? string.Empty,
                    Slot = slot ?? string.Empty,
                    OriginalSaveId = hasLocation
                        ? profile + "/Save_" + slot + ".dat"
                        : string.Empty
                });
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        private bool TryGetSaveIdentity(
            string saveFile,
            out string profile,
            out string slot,
            out string originalSaveId)
        {
            profile = null;
            slot = null;
            originalSaveId = null;
            if (string.IsNullOrWhiteSpace(saveFile) || !Path.IsPathRooted(saveFile))
                return false;

            string fullPath = Path.GetFullPath(saveFile);
            string rootPrefix = Store.PersistentDataPath;
            if (!rootPrefix.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                rootPrefix += Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, PathComparison))
                return false;

            string relative = fullPath.Substring(rootPrefix.Length).Replace('\\', '/');
            string[] segments = relative.Split('/');
            if (segments.Length != 2 || string.IsNullOrWhiteSpace(segments[0]))
                return false;

            string fileName = segments[1];
            if (!fileName.StartsWith("Save_", StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                return false;

            string parsedSlot = fileName.Substring(5, fileName.Length - 9);
            if (string.IsNullOrWhiteSpace(parsedSlot))
                return false;

            profile = segments[0];
            slot = parsedSlot;
            originalSaveId = relative;
            return true;
        }

        private static bool VerifyOriginalSave(string saveFile, object expectedSave)
        {
            if (!File.Exists(saveFile))
                return false;

            using (FileStream stream = File.Open(saveFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                object actualSave = new BinaryFormatter().Deserialize(stream);
                if (actualSave == null || expectedSave.GetType() != actualSave.GetType())
                    return false;

                GameSave expectedInner = GetGameSave(expectedSave);
                GameSave actualInner = GetGameSave(actualSave);
                return expectedInner != null && actualInner != null &&
                    expectedInner.TimeTick == actualInner.TimeTick;
            }
        }

        private static GameSave GetGameSave(object saveData)
        {
            AutoGameSave autoSave = saveData as AutoGameSave;
            if (autoSave != null)
                return autoSave.GameSave;
            return saveData as GameSave;
        }

        private bool Fail(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message) ? "The save operation failed." : message;
            return false;
        }
    }
}
