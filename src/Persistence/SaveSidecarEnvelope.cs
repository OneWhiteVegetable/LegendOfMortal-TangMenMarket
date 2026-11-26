using System;

namespace XingShangMod.Persistence
{
    [Serializable]
    public sealed class SaveSidecarEnvelope
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion = CurrentSchemaVersion;
        public string RunId = string.Empty;
        public long TimeTick;
        public string Profile = string.Empty;
        public string Slot = string.Empty;
        public string OriginalSaveId = string.Empty;
        public string OriginalSaveFingerprint = string.Empty;
        public string ModVersion = string.Empty;
        public string Checksum = string.Empty;
        public RunState State = new RunState();
        public long RestoreTimeTick;
        public string RestoreSaveFingerprint = string.Empty;
        public string RestoreChecksum = string.Empty;
        public RunState RestoreState;
    }
}
