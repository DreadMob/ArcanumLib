using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ArcanumLib.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ModDataStoreMigrationTests
{
    // ─── Schema definitions ────────────────────────────────────────

    private sealed class SchemaV1
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    private sealed class SchemaV2
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public string? Description { get; set; }
    }

    private sealed class SchemaV3
    {
        public string DisplayName { get; set; } = "";
        public int Count { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private static ICoreServerAPI CreateServerApiWithSaveGame(ISaveGame saveGame)
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var worldManager = Substitute.For<IWorldManagerAPI>();
        worldManager.SaveGame.Returns(saveGame);
        sapi.WorldManager.Returns(worldManager);

        var logger = Substitute.For<ILogger>();
        sapi.Logger.Returns(logger);

        return sapi;
    }

    private static ISaveGame CreateSaveGameWithStoredData(string storeKey, byte[]? data)
    {
        var saveGame = Substitute.For<ISaveGame>();
        saveGame.GetData(storeKey).Returns(data);
        return saveGame;
    }

    private static byte[] SerializeEnvelope(int version, object payload)
    {
        var envelope = new ModDataStoreEnvelope
        {
            Version = version,
            Payload = JsonConvert.SerializeObject(payload)
        };
        var json = JsonConvert.SerializeObject(envelope);
        return Encoding.UTF8.GetBytes(json);
    }

    private static byte[] SerializeEnvelopeFromJson(int version, string payloadJson)
    {
        var envelope = new ModDataStoreEnvelope
        {
            Version = version,
            Payload = payloadJson
        };
        var json = JsonConvert.SerializeObject(envelope);
        return Encoding.UTF8.GetBytes(json);
    }

    // ─── No migration needed ───────────────────────────────────────

    [Fact]
    public void Load_WithSameVersion_AppliesDataDirectly()
    {
        var stored = new SchemaV2 { Name = "test", Count = 5, Description = "desc" };
        var storeKey = "arcanumlib:md:mod:migration-same";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(2, stored));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-same", 2, () => new SchemaV2());

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("test", store.Data.Name);
        Assert.Equal(5, store.Data.Count);
        Assert.Equal("desc", store.Data.Description);
    }

    [Fact]
    public void Load_WithNoStoredData_UsesFactory()
    {
        var storeKey = "arcanumlib:md:mod:migration-empty";
        var saveGame = CreateSaveGameWithStoredData(storeKey, null);
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-empty", 2,
            () => new SchemaV2 { Name = "default", Count = 0 });

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("default", store.Data.Name);
        Assert.Equal(0, store.Data.Count);
    }

    [Fact]
    public void Load_WithEmptyByteArray_UsesFactory()
    {
        var storeKey = "arcanumlib:md:mod:migration-empty-bytes";
        var saveGame = CreateSaveGameWithStoredData(storeKey, Array.Empty<byte>());
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-empty-bytes", 2,
            () => new SchemaV2 { Name = "default" });

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("default", store.Data.Name);
    }

    // ─── Single migration (V1 → V2) ────────────────────────────────

    [Fact]
    public void Load_WithSingleMigration_AppliesMigrationAndDeserializes()
    {
        var storedV1 = new SchemaV1 { Name = "test", Count = 5 };
        var storeKey = "arcanumlib:md:mod:migration-v1-to-v2";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(1, storedV1));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-v1-to-v2", 2, () => new SchemaV2());
        store.RegisterMigration(1, token =>
        {
            // Add Description field with a default value.
            var obj = (JObject)token;
            obj["Description"] = "migrated from v1";
            return obj;
        });

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("test", store.Data.Name);
        Assert.Equal(5, store.Data.Count);
        Assert.Equal("migrated from v1", store.Data.Description);
    }

    // ─── Multiple migrations (V1 → V2 → V3) ────────────────────────

    [Fact]
    public void Load_WithMultipleMigrations_AppliesChainAndDeserializes()
    {
        var storedV1 = new SchemaV1 { Name = "test", Count = 5 };
        var storeKey = "arcanumlib:md:mod:migration-v1-to-v3";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(1, storedV1));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV3>(sapi, "mod", "migration-v1-to-v3", 3, () => new SchemaV3());

        // V1 → V2: add Description
        store.RegisterMigration(1, token =>
        {
            var obj = (JObject)token;
            obj["Description"] = "added in v1->v2";
            return obj;
        });

        // V2 → V3: rename Name → DisplayName, add IsActive
        store.RegisterMigration(2, token =>
        {
            var obj = (JObject)token;
            var name = obj["Name"];
            obj.Remove("Name");
            obj["DisplayName"] = name;
            obj["IsActive"] = true;
            return obj;
        });

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("test", store.Data.DisplayName);
        Assert.Equal(5, store.Data.Count);
        Assert.Equal("added in v1->v2", store.Data.Description);
        Assert.True(store.Data.IsActive);
    }

    // ─── Missing migration ─────────────────────────────────────────

    [Fact]
    public void Load_WithMissingMigration_FallsBackToFactory()
    {
        var storedV1 = new SchemaV1 { Name = "test", Count = 5 };
        var storeKey = "arcanumlib:md:mod:migration-missing";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(1, storedV1));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV3>(sapi, "mod", "migration-missing", 3,
            () => new SchemaV3 { DisplayName = "fallback" });

        // Register V2→V3 but NOT V1→V2.
        store.RegisterMigration(2, token =>
        {
            var obj = (JObject)token;
            obj["DisplayName"] = obj["Name"];
            obj.Remove("Name");
            return obj;
        });

        store.Load();

        Assert.True(store.IsLoaded);
        // Should fall back to factory because V1→V2 migration is missing.
        Assert.Equal("fallback", store.Data.DisplayName);
    }

    // ─── Migration returning null ──────────────────────────────────

    [Fact]
    public void Load_WithMigrationReturningNull_FallsBackToFactory()
    {
        var storedV1 = new SchemaV1 { Name = "test", Count = 5 };
        var storeKey = "arcanumlib:md:mod:migration-null-return";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(1, storedV1));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-null-return", 2,
            () => new SchemaV2 { Name = "fallback" });

        store.RegisterMigration(1, _ => null!);

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("fallback", store.Data.Name);
    }

    // ─── Stored version newer than current ─────────────────────────

    [Fact]
    public void Load_WithNewerStoredVersion_FallsBackToFactory()
    {
        var storedV3 = new SchemaV3 { DisplayName = "future", Count = 10 };
        var storeKey = "arcanumlib:md:mod:migration-newer";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(3, storedV3));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        // Current version is 2, stored is 3 — should use defaults.
        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-newer", 2,
            () => new SchemaV2 { Name = "default" });

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("default", store.Data.Name);
    }

    // ─── Corrupted stored data ─────────────────────────────────────

    [Fact]
    public void Load_WithCorruptedJson_FallsBackToFactory()
    {
        var storeKey = "arcanumlib:md:mod:migration-corrupt";
        var saveGame = CreateSaveGameWithStoredData(storeKey, Encoding.UTF8.GetBytes("not valid json"));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-corrupt", 2,
            () => new SchemaV2 { Name = "default" });

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("default", store.Data.Name);
    }

    // ─── RegisterMigration validation ──────────────────────────────

    [Fact]
    public void RegisterMigration_NullMigration_ThrowsArgumentNullException()
    {
        var store = new ModDataStoreInstance<SchemaV2>(null, "mod", "store", 2, () => new SchemaV2());

        Assert.Throws<ArgumentNullException>(() => store.RegisterMigration(1, null!));
    }

    // ─── Save persists current version ─────────────────────────────

    [Fact]
    public void Save_StoresEnvelopeWithCurrentVersion()
    {
        var storeKey = "arcanumlib:md:mod:migration-save";
        byte[]? storedBytes = null;
        var saveGame = Substitute.For<ISaveGame>();
        saveGame.GetData(storeKey).Returns((byte[]?)null);
        saveGame.StoreData(storeKey, Arg.Do<byte[]>(b => storedBytes = b));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-save", 2, () => new SchemaV2());
        store.Load();
        store.Data.Name = "saved";
        store.Data.Count = 42;
        store.Data.Description = "desc";
        store.MarkDirty();

        store.Save();

        // Verify StoreData was called with a valid envelope.
        saveGame.Received(1).StoreData(storeKey, Arg.Any<byte[]>());

        // Inspect the captured bytes to verify the version.
        Assert.NotNull(storedBytes);
        var json = Encoding.UTF8.GetString(storedBytes!);
        var envelope = JsonConvert.DeserializeObject<ModDataStoreEnvelope>(json);
        Assert.NotNull(envelope);
        Assert.Equal(2, envelope!.Version);
    }

    [Fact]
    public void Save_WithoutDirtyFlag_DoesNotStore()
    {
        var saveGame = Substitute.For<ISaveGame>();
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-save-not-dirty", 2, () => new SchemaV2());
        store.Load();
        // No MarkDirty() call.

        store.Save();

        saveGame.DidNotReceive().StoreData(Arg.Any<string>(), Arg.Any<byte[]>());
    }

    // ─── Round-trip: save then load ────────────────────────────────

    [Fact]
    public void SaveThenLoad_RoundTripsDataWithMigrations()
    {
        var storeKey = "arcanumlib:md:mod:migration-roundtrip";
        byte[]? storedBytes = null;

        var saveGame = Substitute.For<ISaveGame>();
        saveGame.GetData(storeKey).Returns(_ => storedBytes);
        saveGame.When(s => s.StoreData(Arg.Any<string>(), Arg.Any<byte[]>()))
            .Do(call => storedBytes = call.ArgAt<byte[]>(1));

        var sapi = CreateServerApiWithSaveGame(saveGame);

        // Save with V2.
        var store1 = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-roundtrip", 2, () => new SchemaV2());
        store1.Load();
        store1.Data.Name = "roundtrip";
        store1.Data.Count = 99;
        store1.Data.Description = "test desc";
        store1.MarkDirty();
        store1.Save();

        // Load with V2 (no migration needed).
        var store2 = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-roundtrip", 2, () => new SchemaV2());
        store2.Load();

        Assert.Equal("roundtrip", store2.Data.Name);
        Assert.Equal(99, store2.Data.Count);
        Assert.Equal("test desc", store2.Data.Description);
    }

    // ─── Round-trip with migration: save V2, load as V3 ────────────

    [Fact]
    public void SaveThenLoad_WithMigration_RoundTripsAcrossVersions()
    {
        var storeKey = "arcanumlib:md:mod:migration-roundtrip-v2-v3";
        byte[]? storedBytes = null;

        var saveGame = Substitute.For<ISaveGame>();
        saveGame.GetData(storeKey).Returns(_ => storedBytes);
        saveGame.When(s => s.StoreData(Arg.Any<string>(), Arg.Any<byte[]>()))
            .Do(call => storedBytes = call.ArgAt<byte[]>(1));

        var sapi = CreateServerApiWithSaveGame(saveGame);

        // Save with V2.
        var store1 = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-roundtrip-v2-v3", 2, () => new SchemaV2());
        store1.Load();
        store1.Data.Name = "migrate-me";
        store1.Data.Count = 7;
        store1.Data.Description = "v2 desc";
        store1.MarkDirty();
        store1.Save();

        // Load with V3, registering V2→V3 migration.
        var store2 = new ModDataStoreInstance<SchemaV3>(sapi, "mod", "migration-roundtrip-v2-v3", 3, () => new SchemaV3());
        store2.RegisterMigration(2, token =>
        {
            var obj = (JObject)token;
            obj["DisplayName"] = obj["Name"];
            obj.Remove("Name");
            obj["IsActive"] = true;
            return obj;
        });

        store2.Load();

        Assert.Equal("migrate-me", store2.Data.DisplayName);
        Assert.Equal(7, store2.Data.Count);
        Assert.Equal("v2 desc", store2.Data.Description);
        Assert.True(store2.Data.IsActive);
    }

    // ─── Without sapi, Load/Save are no-ops ────────────────────────

    [Fact]
    public void Load_WithoutSapi_UsesFactoryAndMarksLoaded()
    {
        var store = new ModDataStoreInstance<SchemaV2>(null, "mod", "no-api", 2,
            () => new SchemaV2 { Name = "default" });

        store.Load();

        Assert.True(store.IsLoaded);
        Assert.Equal("default", store.Data.Name);
    }

    [Fact]
    public void Save_WithoutSapi_DoesNotThrow()
    {
        var store = new ModDataStoreInstance<SchemaV2>(null, "mod", "no-api-save", 2, () => new SchemaV2());
        store.Load();
        store.MarkDirty();

        store.Save();
        // No exception expected; Save is a no-op without sapi.
    }

    // ─── Data auto-loads on first access ───────────────────────────

    [Fact]
    public void Data_AutoLoadsOnFirstAccess()
    {
        var storeKey = "arcanumlib:md:mod:migration-autoload";
        var stored = new SchemaV2 { Name = "auto", Count = 3 };
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(2, stored));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-autoload", 2, () => new SchemaV2());

        // Accessing Data should trigger Load automatically.
        Assert.False(store.IsLoaded);
        var data = store.Data;
        Assert.True(store.IsLoaded);
        Assert.Equal("auto", data.Name);
        Assert.Equal(3, data.Count);
    }

    // ─── Duplicate migration registration ──────────────────────────

    [Fact]
    public void RegisterMigration_DuplicateFromVersion_BothRegistered()
    {
        var storedV1 = new SchemaV1 { Name = "test", Count = 1 };
        var storeKey = "arcanumlib:md:mod:migration-duplicate";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(1, storedV1));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV2>(sapi, "mod", "migration-duplicate", 2, () => new SchemaV2());

        // Register two migrations from V1 — the first one wins (FirstOrDefault).
        store.RegisterMigration(1, token =>
        {
            var obj = (JObject)token;
            obj["Description"] = "first";
            return obj;
        });
        store.RegisterMigration(1, token =>
        {
            var obj = (JObject)token;
            obj["Description"] = "second";
            return obj;
        });

        store.Load();

        // FirstOrDefault returns the first registered migration.
        Assert.Equal("first", store.Data.Description);
    }

    // ─── Migration that transforms payload structure ───────────────

    [Fact]
    public void Load_WithStructuralMigration_TransformsCorrectly()
    {
        // V1 stores Count as a single int; V2 stores it as an object with Min/Max.
        var storeKey = "arcanumlib:md:mod:migration-structural";
        var v1Json = "{\"Name\":\"test\",\"Count\":5}";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelopeFromJson(1, v1Json));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaWithRange>(sapi, "mod", "migration-structural", 2,
            () => new SchemaWithRange());
        store.RegisterMigration(1, token =>
        {
            var obj = (JObject)token;
            var count = obj["Count"]!.Value<int>();
            obj["Count"] = JToken.FromObject(new { Min = count, Max = count });
            return obj;
        });

        store.Load();

        Assert.Equal("test", store.Data.Name);
        Assert.Equal(5, store.Data.Count.Min);
        Assert.Equal(5, store.Data.Count.Max);
    }

    private sealed class SchemaWithRange
    {
        public string Name { get; set; } = "";
        public Range Count { get; set; } = new();
    }

    private sealed class Range
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    // ─── Downgrade preserves disk data and does not overwrite ───────

    [Fact]
    public void Save_AfterDowngrade_DoesNotOverwriteDiskData()
    {
        // Stored data is from a newer schema; current code cannot safely load it.
        var storedV2 = new SchemaV2 { Name = "future", Count = 10, Description = "future desc" };
        var storeKey = "arcanumlib:md:mod:downgrade-preserve";
        var saveGame = CreateSaveGameWithStoredData(storeKey, SerializeEnvelope(2, storedV2));
        var sapi = CreateServerApiWithSaveGame(saveGame);

        var store = new ModDataStoreInstance<SchemaV1>(sapi, "mod", "downgrade-preserve", 1,
            () => new SchemaV1 { Name = "default" });

        store.Load();

        // Should fall back to factory defaults without marking data dirty.
        Assert.True(store.IsLoaded);
        Assert.Equal("default", store.Data.Name);
        Assert.False(store.IsDirty);

        store.Save();

        // Unmodified data must not overwrite the original disk data.
        saveGame.DidNotReceive().StoreData(Arg.Any<string>(), Arg.Any<byte[]>());
    }
}
