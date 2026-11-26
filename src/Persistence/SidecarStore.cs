using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using XingShangMod.Data;

namespace XingShangMod.Persistence
{
    public enum SidecarLoadStatus
    {
        None,
        Success,
        Missing,
        Corrupt,
        Incompatible,
        Ambiguous,
        Error
    }

    public sealed class SidecarStore
    {
        private const int MemorySnapshotLimit = 24;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        private readonly Dictionary<long, SaveSidecarEnvelope> _memorySnapshots =
            new Dictionary<long, SaveSidecarEnvelope>();
        private readonly List<long> _memorySnapshotOrder = new List<long>();

        public string PersistentDataPath { get; private set; }
        public string RootPath { get; private set; }
        public string ModVersion { get; private set; }
        public string LastError { get; private set; }
        public SidecarLoadStatus LastLoadStatus { get; private set; }

        public SidecarStore()
            : this(null)
        {
        }

        public SidecarStore(string modVersion)
        {
            if (string.IsNullOrWhiteSpace(Application.persistentDataPath))
                throw new InvalidOperationException("Application.persistentDataPath is not available.");

            PersistentDataPath = Path.GetFullPath(Application.persistentDataPath);
            RootPath = Path.Combine(PersistentDataPath, "XingShangMod", "Sidecars");
            if (!string.IsNullOrWhiteSpace(modVersion))
                ModVersion = modVersion;
            else
                ModVersion = typeof(SidecarStore).Assembly.GetName().Version.ToString();
        }

        public bool CaptureMemorySnapshot(long timeTick, RunState state)
        {
            return CaptureMemorySnapshot(timeTick, state, 0L, string.Empty, null);
        }

        public bool CaptureMemorySnapshot(
            long timeTick,
            RunState state,
            long restoreTimeTick,
            string restoreSaveFingerprint,
            RunState restoreState)
        {
            ResetStatus();
            try
            {
                if (timeTick <= 0)
                    return Fail("A positive GameSave.TimeTick is required.");
                if (state == null)
                    return Fail("Cannot snapshot a null run state.");

                RunState snapshot = NormalizeClone(state);
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.RunId))
                    return Fail("Cannot snapshot a run state without a RunId.");

                RunState restoreSnapshot = null;
                if (restoreState != null)
                {
                    if (restoreTimeTick <= 0)
                        return Fail("A RestoreData snapshot requires a positive TimeTick.");
                    restoreSnapshot = NormalizeClone(restoreState);
                    if (!string.Equals(snapshot.RunId, restoreSnapshot.RunId, StringComparison.Ordinal))
                        return Fail("The RestoreData snapshot belongs to a different run.");
                }
                else
                {
                    restoreTimeTick = 0L;
                    restoreSaveFingerprint = string.Empty;
                }

                var envelope = new SaveSidecarEnvelope
                {
                    SchemaVersion = SaveSidecarEnvelope.CurrentSchemaVersion,
                    RunId = snapshot.RunId,
                    TimeTick = timeTick,
                    State = snapshot,
                    Checksum = ComputeChecksum(snapshot),
                    RestoreTimeTick = restoreTimeTick,
                    RestoreSaveFingerprint = restoreSaveFingerprint ?? string.Empty,
                    RestoreState = restoreSnapshot,
                    RestoreChecksum = restoreSnapshot == null ? string.Empty : ComputeChecksum(restoreSnapshot)
                };
                Remember(envelope);
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool TryGetMemorySnapshot(long timeTick, out RunState state)
        {
            SaveSidecarEnvelope envelope;
            if (!TryGetMemoryEnvelope(timeTick, out envelope))
            {
                state = null;
                return false;
            }
            state = RunStateCloner.Clone(envelope.State);
            LastLoadStatus = SidecarLoadStatus.Success;
            LastError = null;
            return state != null;
        }

        public bool TryGetMemorySnapshot(
            string profile,
            string slot,
            long timeTick,
            out RunState state)
        {
            return TryGetMemorySnapshot(profile, slot, timeTick, null, out state);
        }

        public bool TryGetMemorySnapshot(
            string profile,
            string slot,
            long timeTick,
            string originalSaveFingerprint,
            out RunState state)
        {
            SaveSidecarEnvelope envelope;
            if (!TryGetMemoryEnvelope(
                profile,
                slot,
                timeTick,
                originalSaveFingerprint,
                out envelope))
            {
                state = null;
                return false;
            }
            state = RunStateCloner.Clone(envelope.State);
            LastLoadStatus = SidecarLoadStatus.Success;
            LastError = null;
            return state != null;
        }

        internal bool TryGetMemoryEnvelope(long timeTick, out SaveSidecarEnvelope envelope)
        {
            ResetStatus();
            envelope = null;
            if (timeTick <= 0)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = "A positive GameSave.TimeTick is required.";
                return false;
            }

            SaveSidecarEnvelope snapshot;
            if (!_memorySnapshots.TryGetValue(timeTick, out snapshot))
            {
                LastLoadStatus = SidecarLoadStatus.Missing;
                return false;
            }
            envelope = CloneEnvelope(snapshot);
            LastLoadStatus = SidecarLoadStatus.Success;
            return true;
        }

        internal bool TryGetMemoryEnvelope(
            string profile,
            string slot,
            long timeTick,
            string originalSaveFingerprint,
            out SaveSidecarEnvelope envelope)
        {
            envelope = null;
            if (!TryGetMemoryEnvelope(timeTick, out envelope))
                return false;

            if (string.IsNullOrWhiteSpace(envelope.Profile) ||
                !string.Equals(envelope.Profile, profile, StringComparison.Ordinal) ||
                !string.Equals(envelope.Slot, slot, StringComparison.Ordinal) ||
                !FingerprintsMatch(envelope.OriginalSaveFingerprint, originalSaveFingerprint))
            {
                envelope = null;
                LastLoadStatus = SidecarLoadStatus.Missing;
                LastError = null;
                return false;
            }
            return true;
        }

        internal bool TryGetRestoreStateForSave(
            long restoreTimeTick,
            string restoreSaveFingerprint,
            out RunState state)
        {
            state = null;
            SaveSidecarEnvelope exact;
            if (_memorySnapshots.TryGetValue(restoreTimeTick, out exact) &&
                FingerprintsMatch(exact.OriginalSaveFingerprint, restoreSaveFingerprint))
            {
                state = RunStateCloner.Clone(exact.State);
                return state != null;
            }

            foreach (SaveSidecarEnvelope envelope in _memorySnapshots.Values)
            {
                if (IsRestoreMatch(envelope, restoreTimeTick, restoreSaveFingerprint))
                {
                    state = RunStateCloner.Clone(envelope.RestoreState);
                    return state != null;
                }
            }
            return false;
        }

        public bool Persist(
            string profile,
            string slot,
            long timeTick,
            string originalSaveId,
            RunState state)
        {
            ResetStatus();
            if (state == null)
                return Fail("Cannot persist a null run state.");
            try
            {
                RunState snapshot = NormalizeClone(state);
                var envelope = new SaveSidecarEnvelope
                {
                    SchemaVersion = SaveSidecarEnvelope.CurrentSchemaVersion,
                    RunId = snapshot.RunId,
                    TimeTick = timeTick,
                    Profile = profile,
                    Slot = slot,
                    OriginalSaveId = originalSaveId,
                    State = snapshot
                };
                return WriteEnvelope(envelope, false);
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        internal bool Prepare(SaveSidecarEnvelope envelope)
        {
            return WriteEnvelope(envelope, true);
        }

        internal bool Persist(SaveSidecarEnvelope envelope)
        {
            return WriteEnvelope(envelope, false);
        }

        internal bool Commit(
            string profile,
            string slot,
            long timeTick,
            string originalSaveFingerprint)
        {
            ResetStatus();
            try
            {
                string journalPath = GetJournalPath(profile, slot, timeTick);
                if (!File.Exists(journalPath))
                {
                    SidecarLoadStatus finalStatus;
                    string finalError;
                    SaveSidecarEnvelope finalEnvelope = ReadEnvelope(
                        GetSidecarPath(profile, slot, timeTick),
                        profile,
                        slot,
                        timeTick,
                        originalSaveFingerprint,
                        false,
                        out finalStatus,
                        out finalError);
                    LastLoadStatus = finalStatus;
                    LastError = finalError;
                    if (finalEnvelope == null)
                        return false;
                    Remember(finalEnvelope);
                    return true;
                }

                SidecarLoadStatus status;
                string error;
                SaveSidecarEnvelope envelope = ReadEnvelope(
                    journalPath,
                    profile,
                    slot,
                    timeTick,
                    originalSaveFingerprint,
                    true,
                    out status,
                    out error);
                LastLoadStatus = status;
                LastError = error;
                if (envelope == null)
                    return false;
                if (!ValidateExistingFinal(envelope))
                    return false;

                WriteEnvelopeAt(envelope, GetSidecarPath(profile, slot, timeTick));
                TryDeleteTemporaryFile(journalPath);
                Remember(envelope);
                LastLoadStatus = SidecarLoadStatus.Success;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = exception.Message;
                return false;
            }
        }

        public bool TryLoadDisk(
            string profile,
            string slot,
            long timeTick,
            out RunState state)
        {
            return TryLoadDisk(profile, slot, timeTick, null, out state);
        }

        public bool TryLoadDisk(
            string profile,
            string slot,
            long timeTick,
            string originalSaveFingerprint,
            out RunState state)
        {
            SaveSidecarEnvelope envelope;
            if (!TryLoadDiskEnvelope(
                profile,
                slot,
                timeTick,
                originalSaveFingerprint,
                out envelope))
            {
                state = null;
                return false;
            }
            state = RunStateCloner.Clone(envelope.State);
            return state != null;
        }

        internal bool TryLoadDiskEnvelope(
            string profile,
            string slot,
            long timeTick,
            string originalSaveFingerprint,
            out SaveSidecarEnvelope envelope)
        {
            ResetStatus();
            envelope = null;
            try
            {
                string path = GetSidecarPath(profile, slot, timeTick);
                SidecarLoadStatus status;
                string error;
                envelope = ReadEnvelope(
                    path,
                    profile,
                    slot,
                    timeTick,
                    originalSaveFingerprint,
                    false,
                    out status,
                    out error);
                if (envelope == null && status == SidecarLoadStatus.Missing)
                {
                    string journalPath = GetJournalPath(profile, slot, timeTick);
                    envelope = ReadEnvelope(
                        journalPath,
                        profile,
                        slot,
                        timeTick,
                        originalSaveFingerprint,
                        true,
                        out status,
                        out error);
                    if (envelope != null)
                    {
                        try
                        {
                            if (ValidateExistingFinal(envelope))
                            {
                                WriteEnvelopeAt(envelope, path);
                                TryDeleteTemporaryFile(journalPath);
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                LastLoadStatus = status;
                LastError = error;
                if (envelope == null)
                    return false;
                Remember(envelope);
                LastLoadStatus = SidecarLoadStatus.Success;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = exception.Message;
                envelope = null;
                return false;
            }
        }

        internal bool TryLoadRestore(
            string profile,
            string slot,
            long restoreTimeTick,
            string restoreSaveFingerprint,
            out RunState state)
        {
            ResetStatus();
            state = null;
            try
            {
                if (restoreTimeTick <= 0)
                {
                    LastLoadStatus = SidecarLoadStatus.Error;
                    LastError = "A positive RestoreData TimeTick is required.";
                    return false;
                }

                SaveSidecarEnvelope exact;
                if (_memorySnapshots.TryGetValue(restoreTimeTick, out exact) &&
                    string.IsNullOrWhiteSpace(exact.Profile) &&
                    FingerprintsMatch(exact.OriginalSaveFingerprint, restoreSaveFingerprint))
                {
                    state = RunStateCloner.Clone(exact.State);
                    LastLoadStatus = SidecarLoadStatus.Success;
                    return state != null;
                }

                SaveSidecarEnvelope match = null;
                foreach (SaveSidecarEnvelope envelope in _memorySnapshots.Values)
                {
                    if (!IsRestoreMatch(envelope, profile, slot, restoreTimeTick, restoreSaveFingerprint))
                        continue;
                    if (!TryMergeRestoreCandidate(envelope, ref match))
                        return false;
                }

                if (match == null)
                {
                    string path = GetSidecarPath(profile, slot, restoreTimeTick);
                    string directory = Path.GetDirectoryName(path);
                    if (Directory.Exists(directory))
                    {
                        string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
                        for (int i = 0; i < files.Length; i++)
                        {
                            SidecarLoadStatus status;
                            string error;
                            SaveSidecarEnvelope envelope = ReadEnvelope(
                                files[i],
                                profile,
                                slot,
                                null,
                                null,
                                false,
                                out status,
                                out error);
                            if (envelope == null ||
                                !IsRestoreMatch(envelope, profile, slot, restoreTimeTick, restoreSaveFingerprint))
                                continue;
                            if (!TryMergeRestoreCandidate(envelope, ref match))
                                return false;
                        }
                    }
                }

                if (match != null)
                {
                    state = RunStateCloner.Clone(match.RestoreState);
                    Remember(match);
                    LastLoadStatus = SidecarLoadStatus.Success;
                    LastError = null;
                    return state != null;
                }

                if (_memorySnapshots.TryGetValue(restoreTimeTick, out exact) &&
                    string.Equals(exact.Profile, profile, StringComparison.Ordinal) &&
                    string.Equals(exact.Slot, slot, StringComparison.Ordinal) &&
                    FingerprintsMatch(exact.OriginalSaveFingerprint, restoreSaveFingerprint))
                {
                    state = RunStateCloner.Clone(exact.State);
                    LastLoadStatus = SidecarLoadStatus.Success;
                    LastError = null;
                    return state != null;
                }

                SaveSidecarEnvelope ordinary;
                if (!TryLoadDiskEnvelope(
                    profile,
                    slot,
                    restoreTimeTick,
                    restoreSaveFingerprint,
                    out ordinary))
                    return false;

                state = RunStateCloner.Clone(ordinary.State);
                LastLoadStatus = SidecarLoadStatus.Success;
                LastError = null;
                return state != null;
            }
            catch (Exception exception)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = exception.Message;
                state = null;
                return false;
            }
        }

        public string GetSidecarPath(string profile, string slot, long timeTick)
        {
            if (string.IsNullOrWhiteSpace(profile))
                throw new ArgumentException("A user profile is required.", "profile");
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("A save slot is required.", "slot");
            if (timeTick <= 0)
                throw new ArgumentOutOfRangeException("timeTick");

            return Path.Combine(
                RootPath,
                "p-" + EncodeSegment(profile),
                "s-" + EncodeSegment(slot),
                timeTick.ToString(CultureInfo.InvariantCulture) + ".json");
        }

        private string GetJournalPath(string profile, string slot, long timeTick)
        {
            return GetSidecarPath(profile, slot, timeTick) + ".journal";
        }

        private bool WriteEnvelope(SaveSidecarEnvelope envelope, bool journal)
        {
            ResetStatus();
            string temporaryPath = null;
            try
            {
                SaveSidecarEnvelope snapshot = PrepareEnvelope(envelope);
                string finalPath = GetSidecarPath(snapshot.Profile, snapshot.Slot, snapshot.TimeTick);
                string path = journal ? finalPath + ".journal" : finalPath;
                if (!ValidateExistingFinal(snapshot))
                    return false;
                if (journal && File.Exists(path))
                {
                    SidecarLoadStatus status;
                    string error;
                    SaveSidecarEnvelope existing = ReadEnvelope(
                        path,
                        snapshot.Profile,
                        snapshot.Slot,
                        snapshot.TimeTick,
                        null,
                        true,
                        out status,
                        out error);
                    if (existing == null)
                        return Fail("The existing journal was preserved: " + error);
                    if (!SameOriginalSave(existing, snapshot))
                        return Fail("The existing journal belongs to a different original save.");
                }

                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                temporaryPath = path + ".tmp";
                WriteEnvelopeAt(snapshot, path);
                temporaryPath = null;
                Remember(snapshot);
                return true;
            }
            catch (Exception exception)
            {
                if (temporaryPath != null)
                    TryDeleteTemporaryFile(temporaryPath);
                return Fail(exception.Message);
            }
        }

        private SaveSidecarEnvelope PrepareEnvelope(SaveSidecarEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException("envelope");
            SaveSidecarEnvelope snapshot = CloneEnvelope(envelope);
            if (snapshot.TimeTick <= 0)
                throw new InvalidOperationException("A positive GameSave.TimeTick is required.");
            if (string.IsNullOrWhiteSpace(snapshot.Profile) || string.IsNullOrWhiteSpace(snapshot.Slot))
                throw new InvalidOperationException("The original save profile and slot are required.");
            string originalSaveId = NormalizeOriginalSaveId(snapshot.OriginalSaveId);
            if (originalSaveId == null)
                throw new InvalidOperationException("The original save identifier must be a safe relative path.");
            snapshot.OriginalSaveId = originalSaveId;
            snapshot.State = NormalizeClone(snapshot.State);
            if (snapshot.State == null || string.IsNullOrWhiteSpace(snapshot.State.RunId))
                throw new InvalidOperationException("Cannot persist a run state without a RunId.");
            if (!string.IsNullOrWhiteSpace(snapshot.RunId) &&
                !string.Equals(snapshot.RunId, snapshot.State.RunId, StringComparison.Ordinal))
                throw new InvalidOperationException("The sidecar RunId is inconsistent.");

            snapshot.SchemaVersion = SaveSidecarEnvelope.CurrentSchemaVersion;
            snapshot.RunId = snapshot.State.RunId;
            snapshot.OriginalSaveFingerprint = snapshot.OriginalSaveFingerprint ?? string.Empty;
            snapshot.ModVersion = ModVersion ?? string.Empty;
            snapshot.Checksum = ComputeChecksum(snapshot.State);
            if (snapshot.RestoreState == null)
            {
                snapshot.RestoreTimeTick = 0L;
                snapshot.RestoreSaveFingerprint = string.Empty;
                snapshot.RestoreChecksum = string.Empty;
            }
            else
            {
                if (snapshot.RestoreTimeTick <= 0)
                    throw new InvalidOperationException("A RestoreData snapshot requires a positive TimeTick.");
                snapshot.RestoreState = NormalizeClone(snapshot.RestoreState);
                if (!string.Equals(snapshot.RunId, snapshot.RestoreState.RunId, StringComparison.Ordinal))
                    throw new InvalidOperationException("The RestoreData snapshot belongs to a different run.");
                snapshot.RestoreSaveFingerprint = snapshot.RestoreSaveFingerprint ?? string.Empty;
                snapshot.RestoreChecksum = ComputeChecksum(snapshot.RestoreState);
            }
            return snapshot;
        }

        private bool ValidateExistingFinal(SaveSidecarEnvelope envelope)
        {
            string finalPath = GetSidecarPath(envelope.Profile, envelope.Slot, envelope.TimeTick);
            if (!File.Exists(finalPath))
                return true;
            SidecarLoadStatus status;
            string error;
            SaveSidecarEnvelope existing = ReadEnvelope(
                finalPath,
                envelope.Profile,
                envelope.Slot,
                envelope.TimeTick,
                null,
                false,
                out status,
                out error);
            if (existing == null)
                return Fail("The existing sidecar was preserved: " + error);
            if (!SameOriginalSave(existing, envelope))
                return Fail("The existing sidecar belongs to a different original save.");
            return true;
        }

        private static bool SameOriginalSave(
            SaveSidecarEnvelope existing,
            SaveSidecarEnvelope envelope)
        {
            if (!string.Equals(existing.OriginalSaveId, envelope.OriginalSaveId, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(existing.OriginalSaveFingerprint) &&
                string.IsNullOrWhiteSpace(envelope.OriginalSaveFingerprint))
                return false;
            return FingerprintsMatch(existing.OriginalSaveFingerprint, envelope.OriginalSaveFingerprint);
        }

        private SaveSidecarEnvelope ReadEnvelope(
            string path,
            string expectedProfile,
            string expectedSlot,
            long? expectedTimeTick,
            string expectedOriginalSaveFingerprint,
            bool allowJournal,
            out SidecarLoadStatus status,
            out string error)
        {
            status = SidecarLoadStatus.Corrupt;
            error = null;
            try
            {
                if (!File.Exists(path))
                {
                    status = SidecarLoadStatus.Missing;
                    return null;
                }

                string json = File.ReadAllText(path, StrictUtf8);
                SaveSidecarEnvelope envelope = JsonCodec.Deserialize<SaveSidecarEnvelope>(json);
                if (envelope == null)
                {
                    error = "The sidecar JSON is empty or invalid.";
                    return null;
                }
                if (envelope.SchemaVersion != 1 &&
                    envelope.SchemaVersion != SaveSidecarEnvelope.CurrentSchemaVersion)
                {
                    status = SidecarLoadStatus.Incompatible;
                    error = "Unsupported sidecar schema " + envelope.SchemaVersion + ".";
                    return null;
                }
                if (envelope.TimeTick <= 0 ||
                    (expectedTimeTick.HasValue && envelope.TimeTick != expectedTimeTick.Value))
                {
                    error = "The sidecar TimeTick does not match its index.";
                    return null;
                }
                if (string.IsNullOrWhiteSpace(envelope.Profile) ||
                    string.IsNullOrWhiteSpace(envelope.Slot))
                {
                    error = "The sidecar profile or slot is missing.";
                    return null;
                }
                if (expectedProfile != null &&
                    (!string.Equals(envelope.Profile, expectedProfile, StringComparison.Ordinal) ||
                     !string.Equals(envelope.Slot, expectedSlot, StringComparison.Ordinal)))
                {
                    error = "The sidecar profile or slot does not match its index.";
                    return null;
                }
                if (!FingerprintsMatch(envelope.OriginalSaveFingerprint, expectedOriginalSaveFingerprint))
                {
                    error = "The sidecar original GameSave fingerprint does not match.";
                    return null;
                }

                string indexedPath = GetSidecarPath(
                    envelope.Profile,
                    envelope.Slot,
                    envelope.TimeTick);
                string fullPath = Path.GetFullPath(path);
                if (!string.Equals(fullPath, indexedPath, PathComparison) &&
                    (!allowJournal ||
                     !string.Equals(fullPath, indexedPath + ".journal", PathComparison)))
                {
                    error = "The sidecar content does not match its indexed location.";
                    return null;
                }

                string originalSaveId = NormalizeOriginalSaveId(envelope.OriginalSaveId);
                if (originalSaveId == null ||
                    !string.Equals(originalSaveId, envelope.OriginalSaveId, StringComparison.Ordinal))
                {
                    error = "The original save identifier is invalid.";
                    return null;
                }
                if (envelope.State == null || string.IsNullOrWhiteSpace(envelope.RunId) ||
                    !string.Equals(envelope.RunId, envelope.State.RunId, StringComparison.Ordinal))
                {
                    error = "The sidecar RunId is missing or inconsistent.";
                    return null;
                }
                if (!string.Equals(envelope.Checksum, ComputeChecksum(envelope.State), StringComparison.Ordinal))
                {
                    error = "The sidecar checksum does not match.";
                    return null;
                }

                if (envelope.RestoreState != null)
                {
                    if (envelope.SchemaVersion < 2 || envelope.RestoreTimeTick <= 0 ||
                        !string.Equals(envelope.RunId, envelope.RestoreState.RunId, StringComparison.Ordinal) ||
                        !string.Equals(
                            envelope.RestoreChecksum,
                            ComputeChecksum(envelope.RestoreState),
                            StringComparison.Ordinal))
                    {
                        error = "The RestoreData sidecar state is missing or inconsistent.";
                        return null;
                    }
                    RunStateNormalizer.Normalize(envelope.RestoreState);
                    envelope.RestoreSaveFingerprint = envelope.RestoreSaveFingerprint ?? string.Empty;
                }
                else
                {
                    envelope.RestoreTimeTick = 0L;
                    envelope.RestoreSaveFingerprint = string.Empty;
                    envelope.RestoreChecksum = string.Empty;
                }

                RunStateNormalizer.Normalize(envelope.State);
                envelope.OriginalSaveFingerprint = envelope.OriginalSaveFingerprint ?? string.Empty;
                envelope.ModVersion = envelope.ModVersion ?? string.Empty;
                status = SidecarLoadStatus.Success;
                return envelope;
            }
            catch (NotSupportedException exception)
            {
                status = SidecarLoadStatus.Incompatible;
                error = exception.Message;
                return null;
            }
            catch (Exception exception)
            {
                status = SidecarLoadStatus.Corrupt;
                error = exception.Message;
                return null;
            }
        }

        private bool TryMergeRestoreCandidate(
            SaveSidecarEnvelope candidate,
            ref SaveSidecarEnvelope match)
        {
            if (match == null)
            {
                match = candidate;
                return true;
            }
            if (string.Equals(match.RestoreChecksum, candidate.RestoreChecksum, StringComparison.Ordinal) &&
                string.Equals(match.RestoreState.RunId, candidate.RestoreState.RunId, StringComparison.Ordinal))
                return true;

            LastLoadStatus = SidecarLoadStatus.Ambiguous;
            LastError = "More than one RestoreData sidecar state matches the checkpoint.";
            return false;
        }

        private static bool IsRestoreMatch(
            SaveSidecarEnvelope envelope,
            long restoreTimeTick,
            string restoreSaveFingerprint)
        {
            return envelope != null && envelope.RestoreState != null &&
                envelope.RestoreTimeTick == restoreTimeTick &&
                FingerprintsMatch(envelope.RestoreSaveFingerprint, restoreSaveFingerprint);
        }

        private static bool IsRestoreMatch(
            SaveSidecarEnvelope envelope,
            string profile,
            string slot,
            long restoreTimeTick,
            string restoreSaveFingerprint)
        {
            return IsRestoreMatch(envelope, restoreTimeTick, restoreSaveFingerprint) &&
                string.Equals(envelope.Profile, profile, StringComparison.Ordinal) &&
                string.Equals(envelope.Slot, slot, StringComparison.Ordinal);
        }

        private static bool FingerprintsMatch(string actual, string expected)
        {
            return string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual) ||
                string.Equals(actual, expected, StringComparison.Ordinal);
        }

        private void Remember(SaveSidecarEnvelope envelope)
        {
            if (envelope == null || envelope.TimeTick <= 0)
                return;
            if (!_memorySnapshots.ContainsKey(envelope.TimeTick))
            {
                while (_memorySnapshots.Count >= MemorySnapshotLimit && _memorySnapshotOrder.Count > 0)
                {
                    long oldest = _memorySnapshotOrder[0];
                    _memorySnapshotOrder.RemoveAt(0);
                    _memorySnapshots.Remove(oldest);
                }
                _memorySnapshotOrder.Add(envelope.TimeTick);
            }
            _memorySnapshots[envelope.TimeTick] = CloneEnvelope(envelope);
        }

        private static SaveSidecarEnvelope CloneEnvelope(SaveSidecarEnvelope envelope)
        {
            return envelope == null ? null : JsonCodec.DeepClone(envelope);
        }

        private static RunState NormalizeClone(RunState state)
        {
            return RunStateNormalizer.Normalize(RunStateCloner.Clone(state));
        }

        private static void WriteEnvelopeAt(SaveSidecarEnvelope envelope, string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = path + ".tmp";
            try
            {
                WriteJson(temporaryPath, JsonCodec.Serialize(envelope, true));
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private static void WriteJson(string path, string json)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, StrictUtf8))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }
            }
        }

        private static string ComputeChecksum(RunState state)
        {
            string json = JsonCodec.Serialize(state, false);
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(StrictUtf8.GetBytes(json));
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static string NormalizeOriginalSaveId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
                return null;
            string normalized = value.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.IndexOf(':') >= 0)
                return null;

            string[] segments = normalized.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 || segments[i] == "." || segments[i] == "..")
                    return null;
            }
            return normalized;
        }

        private static string EncodeSegment(string value)
        {
            var result = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if ((current >= 'a' && current <= 'z') ||
                    (current >= 'A' && current <= 'Z') ||
                    (current >= '0' && current <= '9') ||
                    current == '-' || current == '_')
                {
                    result.Append(current);
                }
                else
                {
                    result.Append('%');
                    result.Append(((int)current).ToString("X4", CultureInfo.InvariantCulture));
                }
            }
            return result.ToString();
        }

        private void ResetStatus()
        {
            LastError = null;
            LastLoadStatus = SidecarLoadStatus.None;
        }

        private bool Fail(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message) ? "The sidecar operation failed." : message;
            return false;
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
