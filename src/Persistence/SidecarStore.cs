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
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        private readonly Dictionary<long, MemorySnapshot> _memorySnapshots =
            new Dictionary<long, MemorySnapshot>();

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
            LastError = null;
            try
            {
                if (timeTick <= 0)
                    return Fail("A positive GameSave.TimeTick is required.");
                if (state == null)
                    return Fail("Cannot snapshot a null run state.");

                RunState snapshot = RunStateNormalizer.Normalize(RunStateCloner.Clone(state));
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.RunId))
                    return Fail("Cannot snapshot a run state without a RunId.");

                _memorySnapshots[timeTick] = new MemorySnapshot
                {
                    TimeTick = timeTick,
                    State = snapshot
                };
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool TryGetMemorySnapshot(long timeTick, out RunState state)
        {
            LastError = null;
            LastLoadStatus = SidecarLoadStatus.None;
            state = null;
            if (timeTick <= 0)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = "A positive GameSave.TimeTick is required.";
                return false;
            }

            MemorySnapshot snapshot;
            if (!_memorySnapshots.TryGetValue(timeTick, out snapshot))
            {
                LastLoadStatus = SidecarLoadStatus.Missing;
                return false;
            }

            state = RunStateCloner.Clone(snapshot.State);
            LastLoadStatus = SidecarLoadStatus.Success;
            return state != null;
        }

        public bool TryGetMemorySnapshot(
            string profile,
            string slot,
            long timeTick,
            out RunState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(slot))
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = "Profile and slot are required.";
                return false;
            }

            MemorySnapshot snapshot;
            if (!_memorySnapshots.TryGetValue(timeTick, out snapshot))
                return TryGetMemorySnapshot(timeTick, out state);
            if (snapshot.Profile == null ||
                (string.Equals(snapshot.Profile, profile, StringComparison.Ordinal) &&
                 string.Equals(snapshot.Slot, slot, StringComparison.Ordinal)))
            {
                return TryGetMemorySnapshot(timeTick, out state);
            }

            LastError = null;
            LastLoadStatus = SidecarLoadStatus.Missing;
            return false;
        }

        public bool Persist(
            string profile,
            string slot,
            long timeTick,
            string originalSaveId,
            RunState state)
        {
            LastError = null;
            string temporaryPath = null;
            try
            {
                if (state == null)
                    return Fail("Cannot persist a null run state.");
                string normalizedOriginalId = NormalizeOriginalSaveId(originalSaveId);
                if (normalizedOriginalId == null)
                    return Fail("The original save identifier must be a safe relative path.");

                RunState snapshot = RunStateNormalizer.Normalize(RunStateCloner.Clone(state));
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.RunId))
                    return Fail("Cannot persist a run state without a RunId.");

                string path = GetSidecarPath(profile, slot, timeTick);
                if (File.Exists(path))
                {
                    SidecarLoadStatus existingStatus;
                    string existingError;
                    SaveSidecarEnvelope existing = ReadEnvelope(
                        path,
                        profile,
                        slot,
                        timeTick,
                        out existingStatus,
                        out existingError);
                    if (existingStatus != SidecarLoadStatus.Success)
                    {
                        return Fail("The existing sidecar was preserved: " + existingError);
                    }
                    if (!string.Equals(existing.OriginalSaveId, normalizedOriginalId, StringComparison.Ordinal))
                        return Fail("The existing sidecar belongs to a different original save.");
                }

                var envelope = new SaveSidecarEnvelope
                {
                    SchemaVersion = SaveSidecarEnvelope.CurrentSchemaVersion,
                    RunId = snapshot.RunId,
                    TimeTick = timeTick,
                    Profile = profile,
                    Slot = slot,
                    OriginalSaveId = normalizedOriginalId,
                    ModVersion = ModVersion ?? string.Empty,
                    State = snapshot
                };
                envelope.Checksum = ComputeChecksum(snapshot);

                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                temporaryPath = path + ".tmp";
                WriteJson(temporaryPath, JsonCodec.Serialize(envelope, true));
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
                temporaryPath = null;

                _memorySnapshots[timeTick] = new MemorySnapshot
                {
                    TimeTick = timeTick,
                    Profile = profile,
                    Slot = slot,
                    OriginalSaveId = normalizedOriginalId,
                    State = RunStateCloner.Clone(snapshot)
                };
                return true;
            }
            catch (Exception exception)
            {
                if (temporaryPath != null)
                    TryDeleteTemporaryFile(temporaryPath);
                return Fail(exception.Message);
            }
        }

        public bool TryLoadDisk(
            string profile,
            string slot,
            long timeTick,
            out RunState state)
        {
            LastError = null;
            LastLoadStatus = SidecarLoadStatus.None;
            state = null;
            try
            {
                string path = GetSidecarPath(profile, slot, timeTick);
                SidecarLoadStatus status;
                string error;
                SaveSidecarEnvelope envelope = ReadEnvelope(
                    path,
                    profile,
                    slot,
                    timeTick,
                    out status,
                    out error);
                LastLoadStatus = status;
                LastError = error;
                if (envelope == null)
                    return false;

                state = RunStateCloner.Clone(envelope.State);
                return state != null;
            }
            catch (Exception exception)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = exception.Message;
                return false;
            }
        }

        public bool TryLoadDisk(long timeTick, out RunState state)
        {
            LastError = null;
            LastLoadStatus = SidecarLoadStatus.None;
            state = null;
            if (timeTick <= 0)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = "A positive GameSave.TimeTick is required.";
                return false;
            }

            try
            {
                if (!Directory.Exists(RootPath))
                {
                    LastLoadStatus = SidecarLoadStatus.Missing;
                    return false;
                }

                string fileName = timeTick.ToString(CultureInfo.InvariantCulture) + ".json";
                string[] files = Directory.GetFiles(RootPath, fileName, SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    LastLoadStatus = SidecarLoadStatus.Missing;
                    return false;
                }

                SaveSidecarEnvelope match = null;
                SidecarLoadStatus failureStatus = SidecarLoadStatus.Missing;
                string failureError = null;
                for (int i = 0; i < files.Length; i++)
                {
                    SidecarLoadStatus status;
                    string error;
                    SaveSidecarEnvelope envelope = ReadEnvelope(
                        files[i],
                        null,
                        null,
                        timeTick,
                        out status,
                        out error);
                    if (status == SidecarLoadStatus.Success)
                    {
                        if (match != null)
                        {
                            LastLoadStatus = SidecarLoadStatus.Ambiguous;
                            LastError = "More than one valid sidecar has TimeTick " + timeTick + ".";
                            return false;
                        }
                        match = envelope;
                    }
                    else if (status != SidecarLoadStatus.Missing)
                    {
                        if (failureStatus == SidecarLoadStatus.Missing ||
                            status == SidecarLoadStatus.Corrupt)
                        {
                            failureStatus = status;
                        }
                        failureError = error;
                    }
                }

                if (match != null)
                {
                    state = RunStateCloner.Clone(match.State);
                    LastLoadStatus = SidecarLoadStatus.Success;
                    LastError = null;
                    return state != null;
                }

                LastLoadStatus = failureStatus;
                LastError = failureError;
                return false;
            }
            catch (Exception exception)
            {
                LastLoadStatus = SidecarLoadStatus.Error;
                LastError = exception.Message;
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

        private SaveSidecarEnvelope ReadEnvelope(
            string path,
            string expectedProfile,
            string expectedSlot,
            long expectedTimeTick,
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
                if (envelope.SchemaVersion != SaveSidecarEnvelope.CurrentSchemaVersion)
                {
                    status = SidecarLoadStatus.Incompatible;
                    error = "Unsupported sidecar schema " + envelope.SchemaVersion + ".";
                    return null;
                }
                if (envelope.TimeTick <= 0 || envelope.TimeTick != expectedTimeTick)
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

                string indexedPath = GetSidecarPath(
                    envelope.Profile,
                    envelope.Slot,
                    envelope.TimeTick);
                if (!string.Equals(Path.GetFullPath(path), indexedPath, PathComparison))
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

                RunStateNormalizer.Normalize(envelope.State);
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

        private bool Fail(string message)
        {
            LastError = message;
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

        private sealed class MemorySnapshot
        {
            public long TimeTick;
            public string Profile;
            public string Slot;
            public string OriginalSaveId;
            public RunState State;
        }
    }
}
