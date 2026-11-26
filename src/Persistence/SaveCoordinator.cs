using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Mortal.Core;
using UnityEngine;

namespace XingShangMod.Persistence
{
    public sealed class SaveMigrationRequest
    {
        public GameSave GameSave { get; internal set; }
        public long TimeTick { get; internal set; }
        public string Profile { get; internal set; }
        public string Slot { get; internal set; }
        public string OriginalSaveId { get; internal set; }
        public string OriginalSaveFingerprint { get; internal set; }
        public bool IsRestore { get; internal set; }
    }

    public sealed class SaveCoordinator
    {
        private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        private readonly Func<RunState> _currentStateProvider;
        private readonly Func<RunState> _newRunStateProvider;
        private readonly Action<RunState, GameSave> _stateApplier;
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
            : this(store, currentStateProvider, newRunStateProvider,
                stateApplier == null ? null : (Action<RunState, GameSave>)((state, save) => stateApplier(state)),
                missingSidecarHandler)
        {
        }

        internal SaveCoordinator(
            SidecarStore store,
            Func<RunState> currentStateProvider,
            Func<RunState> newRunStateProvider,
            Action<RunState, GameSave> stateApplier,
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
                long restoreTimeTick = 0L;
                string restoreSaveFingerprint = string.Empty;
                RunState restoreState = null;
                GameSave restoreSave = ReadRestoreSave(gameSave.RestoreData);
                if (restoreSave != null && restoreSave.TimeTick > 0)
                {
                    restoreTimeTick = restoreSave.TimeTick;
                    try
                    {
                        restoreSaveFingerprint = GameSaveFingerprint.Compute(restoreSave);
                        Store.TryGetRestoreStateForSave(
                            restoreTimeTick,
                            restoreSaveFingerprint,
                            out restoreState);
                    }
                    catch
                    {
                        restoreTimeTick = 0L;
                        restoreSaveFingerprint = string.Empty;
                        restoreState = null;
                    }
                }

                if (!Store.CaptureMemorySnapshot(
                    gameSave.TimeTick,
                    state,
                    restoreTimeTick,
                    restoreSaveFingerprint,
                    restoreState))
                    return Fail(Store.LastError);

                _newRunPending = false;
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool PrepareSaveData(string saveFile, object gameSave)
        {
            LastError = null;
            try
            {
                SaveSidecarEnvelope envelope;
                if (!TryCreateEnvelope(saveFile, gameSave, out envelope))
                    return false;
                if (!Store.Prepare(envelope))
                    return Fail(Store.LastError);
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
                string originalSaveFingerprint = GameSaveFingerprint.Compute(innerSave);
                if (!VerifyOriginalSave(saveFile, gameSave, originalSaveFingerprint))
                    return Fail("The original save was not verified; no sidecar was committed.");

                if (Store.Commit(profile, slot, innerSave.TimeTick, originalSaveFingerprint))
                    return true;
                if (Store.LastLoadStatus != SidecarLoadStatus.Missing)
                    return Fail(Store.LastError);

                SaveSidecarEnvelope envelope;
                if (!TryCreateEnvelope(saveFile, gameSave, out envelope))
                    return false;
                if (!Store.Persist(envelope))
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
            return ExecuteLoadGameData(null, null, null, gameSave, false);
        }

        public bool ExecuteLoadGameData(string profile, string slot, GameSave gameSave)
        {
            return ExecuteLoadGameData(profile, slot, null, gameSave, false);
        }

        public bool ExecuteLoadGameData(string saveFile, GameSave gameSave, bool isRestore)
        {
            return ExecuteLoadGameData(null, null, saveFile, gameSave, isRestore);
        }

        private bool ExecuteLoadGameData(
            string profile,
            string slot,
            string saveFile,
            GameSave gameSave,
            bool isRestore)
        {
            LastError = null;
            _newRunPending = false;
            try
            {
                if (gameSave == null || gameSave.TimeTick <= 0)
                    return Fail("ExecuteLoadGameData requires a GameSave with a positive TimeTick.");

                string originalSaveId = null;
                if (!string.IsNullOrWhiteSpace(saveFile) &&
                    !TryGetSaveIdentity(saveFile, out profile, out slot, out originalSaveId))
                    return Fail("The original load path is outside Application.persistentDataPath or invalid.");

                bool hasLocation = !string.IsNullOrWhiteSpace(profile) &&
                    !string.IsNullOrWhiteSpace(slot);
                if (hasLocation && string.IsNullOrWhiteSpace(originalSaveId))
                    originalSaveId = profile + "/Save_" + slot + ".dat";
                string originalSaveFingerprint = GameSaveFingerprint.Compute(gameSave);
                RunState state = null;
                bool restored = false;

                if (hasLocation && isRestore)
                {
                    restored = Store.TryLoadRestore(
                        profile,
                        slot,
                        gameSave.TimeTick,
                        originalSaveFingerprint,
                        out state);
                }
                else if (hasLocation)
                {
                    restored = Store.TryGetMemorySnapshot(
                        profile,
                        slot,
                        gameSave.TimeTick,
                        originalSaveFingerprint,
                        out state);
                    if (!restored)
                    {
                        restored = Store.TryLoadDisk(
                            profile,
                            slot,
                            gameSave.TimeTick,
                            originalSaveFingerprint,
                            out state);
                    }
                }
                else if (isRestore)
                {
                    return Fail("The RestoreData checkpoint has no original save context.");
                }

                if (!restored && hasLocation && Store.LastLoadStatus != SidecarLoadStatus.Missing)
                    return Fail(Store.LastError);

                if (restored)
                {
                    if (state == null)
                        return Fail("The restored run state is null.");
                    _stateApplier(state, gameSave);
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
                    OriginalSaveId = originalSaveId ?? string.Empty,
                    OriginalSaveFingerprint = originalSaveFingerprint,
                    IsRestore = isRestore
                });
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        private bool TryCreateEnvelope(
            string saveFile,
            object gameSave,
            out SaveSidecarEnvelope envelope)
        {
            envelope = null;
            GameSave innerSave = GetGameSave(gameSave);
            if (innerSave == null || innerSave.TimeTick <= 0)
                return Fail("A GameSave or AutoGameSave.GameSave with a positive TimeTick is required.");

            string profile;
            string slot;
            string originalSaveId;
            if (!TryGetSaveIdentity(saveFile, out profile, out slot, out originalSaveId))
                return Fail("The original save path is outside Application.persistentDataPath or invalid.");
            if (!Store.TryGetMemoryEnvelope(innerSave.TimeTick, out envelope))
                return Fail("No CreateSaveData snapshot exists for this save version.");

            envelope.SchemaVersion = SaveSidecarEnvelope.CurrentSchemaVersion;
            envelope.Profile = profile;
            envelope.Slot = slot;
            envelope.OriginalSaveId = originalSaveId;
            envelope.OriginalSaveFingerprint = GameSaveFingerprint.Compute(innerSave);
            return true;
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

        private static bool VerifyOriginalSave(
            string saveFile,
            object expectedSave,
            string expectedFingerprint)
        {
            if (!File.Exists(saveFile))
                return false;

            using (FileStream stream = File.Open(saveFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                object actualSave = new BinaryFormatter().Deserialize(stream);
                if (actualSave == null || expectedSave.GetType() != actualSave.GetType())
                    return false;

                GameSave actualInner = GetGameSave(actualSave);
                return actualInner != null &&
                    string.Equals(
                        expectedFingerprint,
                        GameSaveFingerprint.Compute(actualInner),
                        StringComparison.Ordinal);
            }
        }

        private static GameSave ReadRestoreSave(string restoreData)
        {
            if (string.IsNullOrWhiteSpace(restoreData))
                return null;
            try
            {
                return JsonUtility.FromJson<GameSave>(restoreData);
            }
            catch
            {
                return null;
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
