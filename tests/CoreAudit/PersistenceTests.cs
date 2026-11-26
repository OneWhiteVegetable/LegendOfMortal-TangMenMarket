using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Mortal.Core;
using UnityEngine;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using static AuditAssert;

internal sealed class PersistenceTests
{
    private const string LegacyStateJson =
        "{\"SchemaVersion\":1,\"RunId\":\"legacy-v1\",\"RunSeed\":123," +
        "\"Clock\":{\"Carry\":2,\"MarketWeekIndex\":4,\"LastGameRound\":10," +
        "\"CurrentGameMonth\":4,\"WeeksThisGameMonth\":1}," +
        "\"CurrentIndex\":1234.5,\"CurrentGameYear\":1," +
        "\"Holdings\":[{\"ItemId\":\"01_item\",\"Quantity\":2,\"TotalCost\":200}],\"Debt\":17}";

    private readonly CoreTestFixture _fixture;

    public PersistenceTests(CoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    public void Register(TestRunner runner)
    {
        runner.Run("dto-clone-and-schema", TestDto);
        runner.Run("run-id-save-load-and-sidecar-checksum", TestRunIdPersistence);
        runner.Run("sidecar-v1-minimal-state-compatibility", TestV1Compatibility);
        runner.Run("sidecar-future-poison-query", TestFuturePoisonQuery);
        runner.Run("save-fingerprint-determinism-and-golden-text", TestFingerprintDeterminism);
        runner.Run("missing-sidecar-migration-request-determinism", TestMigrationRequestDeterminism);
        runner.Run("restore-checkpoint-journal-disk-roundtrip", TestRestoreCheckpoint);
    }

    private void TestDto()
    {
        RunState state = _fixture.CreateTradeState(123.5);
        state.RunId = "run-dto";
        state.RunSeed = 123456;
        state.Debt = long.MaxValue - 1;
        state.TradeHistory.Add(new TradeRecord
        {
            TradeId = "trade-1",
            ItemId = _fixture.Item.ItemId,
            Quantity = 2,
            UnitPrice = 123.5,
            CashDelta = -247,
            DebtDelta = 20
        });
        RunState clone = RunStateCloner.Clone(state);
        Equal(state.RunId, clone.RunId, "DTO RunId");
        Equal(state.Debt, clone.Debt, "DTO Int64");
        Equal(-247L, clone.TradeHistory[0].CashDelta, "DTO signed cash");
        clone.Clock.Carry = 2;
        Equal(0, state.Clock.Carry, "clone aliased original");
        string json = JsonCodec.Serialize(state, false);
        True(json.Contains("\"RunId\":\"run-dto\""), "DTO field name missing");
        RunState roundTrip = JsonCodec.Deserialize<RunState>(json);
        Equal(state.TradeHistory[0].DebtDelta, roundTrip.TradeHistory[0].DebtDelta, "DTO debt delta");
        bool rejected = false;
        try
        {
            RunStateNormalizer.Normalize(new RunState { SchemaVersion = 2 });
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        True(rejected, "future DTO schema accepted");
    }

    private void TestRunIdPersistence()
    {
        string persistenceRoot = Path.Combine(_fixture.Root, "player");
        Directory.CreateDirectory(persistenceRoot);
        Application.persistentDataPath = persistenceRoot;
        RunState current = new RunState { RunId = "run-old", RunSeed = 1 };
        RunState applied = null;
        SidecarStore store = new SidecarStore("audit");
        SaveCoordinator saves = new SaveCoordinator(
            store,
            delegate { return current; },
            delegate
            {
                current = new RunState { RunId = "run-new", RunSeed = 2 };
                return current;
            },
            delegate(RunState state) { applied = state; },
            null);
        GameSave oldSave = new GameSave { TimeTick = 100 };
        GameSave newSave = new GameSave { TimeTick = 200 };
        GameSave actSave = new GameSave { TimeTick = 300 };
        string saveFile = Path.Combine(persistenceRoot, "profile", "Save_slot.dat");
        True(saves.CreateSaveData(oldSave), "old memory snapshot failed: " + saves.LastError);
        True(saves.PrepareSaveData(saveFile, oldSave), "old snapshot binding failed: " + saves.LastError);
        saves.MarkNewGamePending();
        True(saves.CreateSaveData(newSave), "new memory snapshot failed: " + saves.LastError);
        True(saves.PrepareSaveData(saveFile, newSave), "new snapshot binding failed: " + saves.LastError);
        True(saves.ExecuteLoadGameData("profile", "slot", oldSave), "old load failed: " + saves.LastError);
        Equal("run-old", applied.RunId, "old save did not restore old RunId");
        True(saves.ExecuteLoadGameData("profile", "slot", newSave), "new load failed: " + saves.LastError);
        Equal("run-new", applied.RunId, "new save did not restore new RunId");
        saves.MarkNewActPending();
        True(saves.CreateSaveData(actSave), "new Act snapshot failed");
        True(saves.PrepareSaveData(saveFile, actSave), "new Act snapshot binding failed: " + saves.LastError);
        True(saves.ExecuteLoadGameData("profile", "slot", actSave), "new Act load failed");
        Equal("run-new", applied.RunId, "new Act RunId missing");
        Equal(2, applied.RunSeed, "new Act seed missing");

        RunState persisted = new RunState { RunId = "disk-run", RunSeed = 99, Debt = 123 };
        True(store.Persist("profile", "slot", 400, "profile/Save_slot.dat", persisted), "persist failed: " + store.LastError);
        SidecarStore reloadedStore = new SidecarStore("audit");
        RunState loaded;
        True(reloadedStore.TryLoadDisk("profile", "slot", 400, out loaded), "disk load failed: " + reloadedStore.LastError);
        Equal("disk-run", loaded.RunId, "disk RunId");
        Equal(123L, loaded.Debt, "disk debt");
        string path = store.GetSidecarPath("profile", "slot", 400);
        string tampered = File.ReadAllText(path).Replace("disk-run", "tampered");
        File.WriteAllText(path, tampered, new UTF8Encoding(false));
        True(!reloadedStore.TryLoadDisk("profile", "slot", 400, out loaded), "tampered sidecar accepted");
        Equal(SidecarLoadStatus.Corrupt, reloadedStore.LastLoadStatus, "tampered status");
        True(File.Exists(path), "corrupt sidecar deleted");
    }

    private void TestV1Compatibility()
    {
        string persistenceRoot = Path.Combine(_fixture.Root, "legacy-player");
        Directory.CreateDirectory(persistenceRoot);
        Application.persistentDataPath = persistenceRoot;
        SidecarStore store = new SidecarStore("audit");
        string path = WriteLegacyV1(store, "legacy-profile", "slot-1", 700);

        RunState loaded;
        True(store.TryLoadDisk("legacy-profile", "slot-1", 700, out loaded), "v1 direct load failed: " + store.LastError);
        Equal(SidecarLoadStatus.Success, store.LastLoadStatus, "v1 direct status");
        AssertLegacyState(loaded, "direct");

        // TODO: Restore TimeTick-only disk query coverage when SidecarStore exposes a public query API.
        True(File.Exists(path), "v1 load removed source sidecar");
    }

    private void TestFuturePoisonQuery()
    {
        string persistenceRoot = Path.Combine(_fixture.Root, "future-player");
        Directory.CreateDirectory(persistenceRoot);
        Application.persistentDataPath = persistenceRoot;
        SidecarStore store = new SidecarStore("audit");
        string futurePath = store.GetSidecarPath("future-profile", "slot-1", 701);
        Directory.CreateDirectory(Path.GetDirectoryName(futurePath));
        int futureSchema = SaveSidecarEnvelope.CurrentSchemaVersion + 1;
        string futureJson =
            "{\"SchemaVersion\":" + futureSchema + ",\"RunId\":\"future-run\",\"TimeTick\":701," +
            "\"Profile\":\"future-profile\",\"Slot\":\"slot-1\"," +
            "\"OriginalSaveId\":\"future/Save.dat\",\"ModVersion\":\"99.0.0\",\"Checksum\":\"\"," +
            "\"State\":{\"SchemaVersion\":" + futureSchema + ",\"RunId\":\"future-run\",\"Debt\":-999}}";
        File.WriteAllText(futurePath, futureJson, new UTF8Encoding(false));

        RunState loaded;
        True(!store.TryLoadDisk("future-profile", "slot-1", 701, out loaded), "future direct query succeeded");
        True(loaded == null, "future direct query returned state");
        Equal(SidecarLoadStatus.Incompatible, store.LastLoadStatus, "future direct status");
        // TODO: Add mixed valid/future TimeTick query coverage with the public disk query API.
        Equal(futureJson, File.ReadAllText(futurePath), "future poison data was rewritten");
    }

    private static void TestFingerprintDeterminism()
    {
        Equal(
            "5c4cddf6cacf7248810136730b02e1943550df13176f4b209f6ab4409fe0baad",
            GameSaveFingerprint.ComputeText("唐门\n行市"),
            "text fingerprint golden vector");
        Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            GameSaveFingerprint.ComputeText(null),
            "null text fingerprint");
        GameSave save = new GameSave
        {
            TimeTick = 900,
            CurrentYear = 2,
            CurrentMonth = 6,
            CurrentStage = MonthStageType.中旬,
            RestoreData = "checkpoint"
        };
        string first = GameSaveFingerprint.Compute(save);
        string second = GameSaveFingerprint.Compute(save);
        Equal(first, second, "GameSave fingerprint changed without data changes");
        Equal(64, first.Length, "GameSave fingerprint length");
        save.TimeTick = 901;
        True(first != GameSaveFingerprint.Compute(save), "GameSave fingerprint ignored TimeTick");
    }

    private void TestMigrationRequestDeterminism()
    {
        string persistenceRoot = Path.Combine(_fixture.Root, "migration-player");
        Directory.CreateDirectory(persistenceRoot);
        Application.persistentDataPath = persistenceRoot;
        SidecarStore store = new SidecarStore("audit");
        RunState current = new RunState { RunId = "migration-current", RunSeed = 4 };
        var requests = new List<SaveMigrationRequest>();
        SaveCoordinator saves = new SaveCoordinator(
            store,
            delegate { return current; },
            delegate { return current; },
            delegate(RunState ignored) { },
            requests.Add);
        GameSave save = new GameSave { TimeTick = 900, CurrentYear = 1, CurrentMonth = 4 };
        True(saves.ExecuteLoadGameData("missing-profile", "slot", save), "first migration failed: " + saves.LastError);
        True(saves.ExecuteLoadGameData("missing-profile", "slot", save), "repeat migration failed: " + saves.LastError);
        Equal(2, requests.Count, "migration request count");
        for (int i = 0; i < requests.Count; i++)
        {
            SaveMigrationRequest request = requests[i];
            True(ReferenceEquals(save, request.GameSave), "migration GameSave identity " + i);
            Equal(900L, request.TimeTick, "migration TimeTick " + i);
            Equal("missing-profile", request.Profile, "migration profile " + i);
            Equal("slot", request.Slot, "migration slot " + i);
            Equal("missing-profile/Save_slot.dat", request.OriginalSaveId, "migration save ID " + i);
            Equal(GameSaveFingerprint.Compute(save), request.OriginalSaveFingerprint, "migration fingerprint " + i);
            Equal(false, request.IsRestore, "migration restore flag " + i);
        }
        Equal(
            requests[0].OriginalSaveFingerprint,
            requests[1].OriginalSaveFingerprint,
            "migration fingerprint was not deterministic");
    }

    private void TestRestoreCheckpoint()
    {
        string persistenceRoot = Path.Combine(_fixture.Root, "checkpoint-player");
        Directory.CreateDirectory(persistenceRoot);
        Application.persistentDataPath = persistenceRoot;
        SidecarStore store = new SidecarStore("audit");
        RunState current = new RunState { RunId = "checkpoint-run", RunSeed = 77, Debt = 10 };
        current.AssetHistory.Add(new AssetPoint { Cash = 25, NetAssets = 15 });
        RunState applied = null;
        SaveCoordinator saves = new SaveCoordinator(
            store,
            delegate { return current; },
            delegate { return current; },
            delegate(RunState state) { applied = state; },
            null);
        string saveFile = Path.Combine(persistenceRoot, "profile", "Save_slot.dat");
        GameSave checkpoint = new GameSave
        {
            TimeTick = 1000,
            CurrentYear = 1,
            CurrentMonth = 4,
            CurrentStage = MonthStageType.上旬
        };
        True(saves.CreateSaveData(checkpoint), "checkpoint snapshot failed: " + saves.LastError);
        True(saves.PrepareSaveData(saveFile, checkpoint), "checkpoint prepare failed: " + saves.LastError);
        WriteOriginalSave(saveFile, checkpoint);
        True(saves.ExecuteSaveData(saveFile, checkpoint), "checkpoint commit failed: " + saves.LastError);

        current.Debt = 99;
        GameSave later = new GameSave
        {
            TimeTick = 2000,
            CurrentYear = 1,
            CurrentMonth = 5,
            CurrentStage = MonthStageType.中旬,
            RestoreData = JsonCodec.Serialize(checkpoint, false)
        };
        True(saves.CreateSaveData(later), "later snapshot failed: " + saves.LastError);
        True(saves.PrepareSaveData(saveFile, later), "later prepare failed: " + saves.LastError);
        WriteOriginalSave(saveFile, later);
        True(saves.ExecuteSaveData(saveFile, later), "later commit failed: " + saves.LastError);

        string checkpointPath = store.GetSidecarPath("profile", "slot", 1000);
        string laterPath = store.GetSidecarPath("profile", "slot", 2000);
        True(File.Exists(checkpointPath) && File.Exists(laterPath), "committed sidecars missing");
        True(!File.Exists(checkpointPath + ".journal") && !File.Exists(laterPath + ".journal"), "committed journals remained");
        SaveSidecarEnvelope envelope = JsonCodec.Deserialize<SaveSidecarEnvelope>(File.ReadAllText(laterPath));
        Equal(1000L, envelope.RestoreTimeTick, "checkpoint link TimeTick");
        Equal(GameSaveFingerprint.Compute(checkpoint), envelope.RestoreSaveFingerprint, "checkpoint link fingerprint");
        Equal(GameSaveFingerprint.Compute(later), envelope.OriginalSaveFingerprint, "later original fingerprint");
        True(envelope.RestoreState != null, "checkpoint state missing");
        Equal(10L, envelope.RestoreState.Debt, "checkpoint state captured later debt");
        Equal(
            Checksum(JsonCodec.Serialize(envelope.RestoreState, false)),
            envelope.RestoreChecksum,
            "checkpoint checksum");

        SidecarStore diskStore = new SidecarStore("audit");
        SaveCoordinator diskSaves = new SaveCoordinator(
            diskStore,
            delegate { return current; },
            delegate { return current; },
            delegate(RunState state) { applied = state; },
            null);
        True(diskSaves.ExecuteLoadGameData(saveFile, checkpoint, true), "checkpoint restore failed: " + diskSaves.LastError);
        True(applied != null, "checkpoint restore did not apply state");
        Equal(10L, applied.Debt, "checkpoint restore returned later state");
        Equal(77, applied.RunSeed, "checkpoint restore RunSeed");
        True(diskSaves.ExecuteLoadGameData("profile", "slot", later), "later normal load failed: " + diskSaves.LastError);
        Equal(99L, applied.Debt, "normal load returned checkpoint state");
        envelope.RestoreState.AssetHistory[0].TotalProfit = 123;
        File.WriteAllText(laterPath, JsonCodec.Serialize(envelope, true));
        True(!diskStore.TryLoadDisk("profile", "slot", 2000, out applied) &&
            diskStore.LastLoadStatus == SidecarLoadStatus.Corrupt, "tampered restore checkpoint's profit field accepted");
    }

    private static void WriteOriginalSave(string path, GameSave save)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (FileStream stream = File.Create(path))
            new BinaryFormatter().Serialize(stream, save);
    }

    private static string WriteLegacyV1(SidecarStore store, string profile, string slot, long timeTick)
    {
        RunState legacy = JsonCodec.Deserialize<RunState>(LegacyStateJson);
        string checksum = Checksum(JsonCodec.Serialize(legacy, false));
        string envelopeJson =
            "{\"SchemaVersion\":1,\"RunId\":\"legacy-v1\",\"TimeTick\":700," +
            "\"Profile\":\"legacy-profile\",\"Slot\":\"slot-1\"," +
            "\"OriginalSaveId\":\"legacy/Save_slot.dat\",\"ModVersion\":\"1.0.0\"," +
            "\"Checksum\":\"" + checksum + "\",\"State\":" + LegacyStateJson + "}";
        string path = store.GetSidecarPath(profile, slot, timeTick);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, envelopeJson, new UTF8Encoding(false));
        return path;
    }

    private static void AssertLegacyState(RunState state, string source)
    {
        Equal("legacy-v1", state.RunId, "v1 RunId " + source);
        Equal(123, state.RunSeed, "v1 RunSeed " + source);
        Equal(2, state.Clock.Carry, "v1 clock carry " + source);
        Equal(4, state.Clock.MarketWeekIndex, "v1 market week " + source);
        Near(1234.5, state.CurrentIndex, "v1 index " + source);
        Equal(17L, state.Debt, "v1 debt " + source);
        Equal(1, state.Holdings.Count, "v1 holdings " + source);
        Equal(2, state.Holdings[0].Quantity, "v1 holding quantity " + source);
        Equal(200L, state.Holdings[0].TotalCost, "v1 holding cost " + source);
        True(state.TradeHistory != null && state.TradeHistory.Count == 0, "v1 omitted trade history default " + source);
        True(state.NewsHistory != null && state.NewsHistory.Count == 0, "v1 omitted news history default " + source);
    }

    private static string Checksum(string json)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] hash = algorithm.ComputeHash(new UTF8Encoding(false).GetBytes(json));
            var result = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                result.Append(hash[i].ToString("x2"));
            return result.ToString();
        }
    }
}
