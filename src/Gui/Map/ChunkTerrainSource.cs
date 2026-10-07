#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ArcanumLib.Gui.Map;

/// <summary>
/// Client-side terrain thumbnails per chunk column (32×32 px) in the vanilla
/// world-map parchment style, for GUI maps that are not the vanilla world map.
/// <para>
/// Two sources, newest wins: (1) the client's own world-map cache DB
/// (<c>Maps/&lt;save&gt;.db</c> — every piece vanilla ever rendered, so explored
/// but unloaded land shows too), read through a separate read-only connection;
/// (2) chunks currently loaded on the client, rendered with the vanilla colour
/// table (simplified hill shading). Pixels are Cairo ARGB32 (<c>0xAARRGGBB</c>).
/// </para>
/// Main-thread only. Call <see cref="Request"/> for what is on screen, then
/// <see cref="Process"/> from a tick; <see cref="Version"/> bumps when new
/// pixels land, so a view can redraw lazily.
/// </summary>
public sealed class ChunkTerrainSource : IDisposable
{
    /// <summary>Thumbnail edge in pixels (one pixel per block column).</summary>
    public const int Size = 32;

    private const int MaxCached = 6000;
    private const long RetryMissingMs = 4000;

    // Vanilla ChunkMapLayer colour table (hexColorsByCode / defaultMapColorCodes).
    private static readonly (string code, uint rgb)[] Palette =
    {
        ("ink", 0x483018), ("settlement", 0x856844), ("wateredge", 0x483018), ("land", 0xAC8858),
        ("desert", 0xC4A468), ("forest", 0x98844C), ("road", 0x805030), ("plant", 0x808650),
        ("lake", 0xCCC890), ("lava", 0xCCC890), ("ocean", 0xCCC890), ("glacier", 0xE0E0C0),
        ("devastation", 0x755C3C),
    };

    private static readonly Dictionary<EnumBlockMaterial, string> MaterialCodes = new()
    {
        { EnumBlockMaterial.Soil, "land" }, { EnumBlockMaterial.Sand, "desert" }, { EnumBlockMaterial.Ore, "land" },
        { EnumBlockMaterial.Gravel, "desert" }, { EnumBlockMaterial.Stone, "land" }, { EnumBlockMaterial.Leaves, "forest" },
        { EnumBlockMaterial.Plant, "plant" }, { EnumBlockMaterial.Wood, "forest" }, { EnumBlockMaterial.Snow, "glacier" },
        { EnumBlockMaterial.Water, "lake" }, { EnumBlockMaterial.Ice, "glacier" }, { EnumBlockMaterial.Lava, "lava" },
    };

    private readonly ICoreClientAPI capi;
    private readonly Dictionary<long, int[]> pixels = new();
    private readonly LinkedList<long> lru = new();
    private readonly Dictionary<long, LinkedListNode<long>> lruNodes = new();
    private readonly Dictionary<long, long> missingSince = new();
    private readonly HashSet<long> fresh = new();          // rendered from a live chunk this session
    private readonly List<long> pendingDb = new();
    private readonly List<long> pendingGen = new();
    private readonly HashSet<long> pendingSet = new();

    private SqliteConnection? db;
    private SqliteCommand? dbGet;
    private bool dbFailed;
    private byte[]? block2Color;
    private uint[]? colors;
    private int waterEdgeIdx;

    /// <summary>Bumps whenever a thumbnail is added or replaced.</summary>
    public int Version { get; private set; }

    /// <summary>Creates a source bound to the client world.</summary>
    public ChunkTerrainSource(ICoreClientAPI capi) { this.capi = capi; }

    /// <summary>Chunk key used by <see cref="Get"/> and grid selections.</summary>
    public static long Key(int cx, int cz) => ((long)cx << 32) | (uint)cz;

    /// <summary>Cached thumbnail, or null while unknown/unexplored.</summary>
    public int[]? Get(int cx, int cz)
    {
        long k = Key(cx, cz);
        if (!pixels.TryGetValue(k, out var px)) return null;
        if (lruNodes.TryGetValue(k, out var node)) { lru.Remove(node); lru.AddLast(node); }
        return px;
    }

    /// <summary>Queue a chunk for lookup. Cheap to call every frame for everything on screen.</summary>
    public void Request(int cx, int cz)
    {
        if (cx < 0 || cz < 0) return;
        long k = Key(cx, cz);
        if (pendingSet.Contains(k)) return;
        bool have = pixels.ContainsKey(k);
        if (have && (fresh.Contains(k) || !IsLoaded(cx, cz))) return;
        if (!have && missingSince.TryGetValue(k, out long t) && capi.ElapsedMilliseconds - t < RetryMissingMs) return;
        pendingSet.Add(k);
        if (!have) pendingDb.Add(k);
        else pendingGen.Add(k);   // cached from DB, but live data is available — refresh
    }

    /// <summary>Forget the live render of a chunk (e.g. after blocks changed) so the next request re-renders it.</summary>
    public void Invalidate(int cx, int cz) => fresh.Remove(Key(cx, cz));

    /// <summary>
    /// Resolve queued requests: all DB lookups, then up to <paramref name="maxRenders"/>
    /// live-chunk renders. Returns true when anything changed.
    /// </summary>
    public bool Process(int maxRenders = 24)
    {
        int before = Version;
        if (pendingDb.Count > 0)
        {
            foreach (long k in pendingDb)
            {
                int cx = (int)(k >> 32), cz = (int)k;
                var px = ReadDb(cx, cz);
                if (px != null) Store(k, px);
                if (IsLoaded(cx, cz)) pendingGen.Add(k);
                else
                {
                    pendingSet.Remove(k);
                    if (px == null) missingSince[k] = capi.ElapsedMilliseconds;
                }
            }
            pendingDb.Clear();
        }

        int done = 0;
        while (pendingGen.Count > 0 && done < maxRenders)
        {
            long k = pendingGen[^1];
            pendingGen.RemoveAt(pendingGen.Count - 1);
            pendingSet.Remove(k);
            int cx = (int)(k >> 32), cz = (int)k;
            var px = Render(cx, cz);
            done++;
            if (px == null) { if (!pixels.ContainsKey(k)) missingSince[k] = capi.ElapsedMilliseconds; continue; }
            Store(k, px);
            fresh.Add(k);
        }
        return Version != before;
    }

    private void Store(long k, int[] px)
    {
        pixels[k] = px;
        missingSince.Remove(k);
        if (lruNodes.TryGetValue(k, out var node)) lru.Remove(node);
        lruNodes[k] = lru.AddLast(k);
        while (lru.Count > MaxCached)
        {
            long old = lru.First!.Value;
            lru.RemoveFirst();
            lruNodes.Remove(old);
            pixels.Remove(old);
            fresh.Remove(old);
        }
        Version++;
    }

    private bool IsLoaded(int cx, int cz)
    {
        var ba = capi.World.BlockAccessor;
        int cy = ba.MapSizeY / Size;
        for (int y = 0; y < cy; y++)
        {
            var ch = ba.GetChunk(cx, y, cz);
            if (ch is not IClientChunk { LoadedFromServer: true }) return false;
        }
        return ba.GetMapChunk(cx, cz) != null;
    }

    // ── world-map cache DB ───────────────────────────────────────────────

    [ProtoContract]
    private sealed class MapPiece { [ProtoMember(1)] public int[]? Pixels = null; }   // wire-compatible with vanilla MapPieceDB

    private int[]? ReadDb(int cx, int cz)
    {
        if (dbFailed) return null;
        try
        {
            if (db == null)
            {
                string path = Path.Combine(GamePaths.DataPath, "Maps", capi.World.SavegameIdentifier + ".db");
                if (!File.Exists(path)) { dbFailed = true; return null; }
                db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                db.Open();
                dbGet = db.CreateCommand();
                dbGet.CommandText = "SELECT data FROM mappiece WHERE position=@pos";
                dbGet.Parameters.Add("@pos", SqliteType.Integer);
                dbGet.Prepare();
            }
            dbGet!.Parameters["@pos"].Value = (long)new FastVec2i(cx, cz).ToChunkIndex();
            if (dbGet.ExecuteScalar() is not byte[] blob) return null;
            var src = SerializerUtil.Deserialize<MapPiece>(blob)?.Pixels;
            if (src == null || src.Length != Size * Size) return null;
            var dst = new int[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                uint c = (uint)src[i];   // vanilla stores RGBA bytes (0xAABBGGRR)
                dst[i] = (int)(0xFF000000u | ((c & 0xFFu) << 16) | (c & 0xFF00u) | ((c >> 16) & 0xFFu));
            }
            return dst;
        }
        catch (Exception ex)
        {
            // locked/corrupt/absent cache — live chunks still render
            capi.Logger.Debug("[ArcanumLib] ChunkTerrainSource: map cache unavailable ({0})", ex.Message);
            dbFailed = true;
            DisposeDb();
            return null;
        }
    }

    private void DisposeDb()
    {
        try { dbGet?.Dispose(); db?.Dispose(); } catch { /* shutting down */ }
        dbGet = null; db = null;
    }

    // ── live chunk render (vanilla ChunkMapLayer, simplified) ────────────

    private void EnsureColors()
    {
        if (block2Color != null) return;
        colors = new uint[Palette.Length];
        var idx = new Dictionary<string, int>();
        for (int i = 0; i < Palette.Length; i++) { colors[i] = Palette[i].rgb; idx[Palette[i].code] = i; }
        waterEdgeIdx = idx["wateredge"];
        var blocks = capi.World.Blocks;
        block2Color = new byte[blocks.Count];
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            string code = "land";
            if (b?.Attributes != null)
            {
                code = b.Attributes["mapColorCode"].AsString(null!) ?? (MaterialCodes.TryGetValue(b.BlockMaterial, out var c) ? c : "land");
            }
            else if (b != null && MaterialCodes.TryGetValue(b.BlockMaterial, out var c2)) code = c2;
            block2Color[i] = (byte)(idx.TryGetValue(code, out int ci) ? ci : idx["land"]);
        }
    }

    private static bool IsLake(Block b)
        => b.BlockMaterial == EnumBlockMaterial.Water || (b.BlockMaterial == EnumBlockMaterial.Ice && b.Code?.Path != "glacierice");

    private int[]? Render(int cx, int cz)
    {
        if (!IsLoaded(cx, cz)) return null;
        EnsureColors();
        var ba = capi.World.BlockAccessor;
        int cyCount = ba.MapSizeY / Size;
        var chunks = new IWorldChunk[cyCount];
        for (int y = 0; y < cyCount; y++) chunks[y] = ba.GetChunk(cx, y, cz);
        var mc = ba.GetMapChunk(cx, cz);
        var west = ba.GetMapChunk(cx - 1, cz);
        var north = ba.GetMapChunk(cx, cz - 1);
        var heights = mc.RainHeightMap;
        var blocks = capi.World.Blocks;
        var ids = new int[Size * Size];
        var outPx = new int[Size * Size];

        for (int i = 0; i < ids.Length; i++)
        {
            int x = i % Size, z = i / Size;
            int h = heights[i];
            int cy = h / Size;
            if (cy >= cyCount) { ids[i] = 0; continue; }
            int id = chunks[cy].UnpackAndReadBlock(MapUtil.Index3d(x, h % Size, z, Size, Size), BlockLayersAccess.FluidOrSolid);
            if (id < blocks.Count && blocks[id].BlockMaterial == EnumBlockMaterial.Snow && h > 0)
            {
                h--; cy = h / Size;
                id = chunks[cy].UnpackAndReadBlock(MapUtil.Index3d(x, h % Size, z, Size, Size), BlockLayersAccess.FluidOrSolid);
            }
            ids[i] = id < blocks.Count ? id : 0;
        }

        for (int i = 0; i < ids.Length; i++)
        {
            int x = i % Size, z = i / Size;
            var b = blocks[ids[i]];
            uint rgb = colors![block2Color![ids[i]]];
            if (IsLake(b))
            {
                bool edge = (x > 0 && !IsLake(blocks[ids[i - 1]])) || (x < Size - 1 && !IsLake(blocks[ids[i + 1]]))
                         || (z > 0 && !IsLake(blocks[ids[i - Size]])) || (z < Size - 1 && !IsLake(blocks[ids[i + Size]]));
                if (edge) rgb = colors[waterEdgeIdx];
                outPx[i] = (int)(0xFF000000u | rgb);
                continue;
            }

            // Hill shading against the north-west neighbours, like vanilla.
            int h = heights[i];
            int hw = x > 0 ? heights[i - 1] : west?.RainHeightMap[z * Size + Size - 1] ?? h;
            int hn = z > 0 ? heights[i - Size] : north?.RainHeightMap[(Size - 1) * Size + x] ?? h;
            int d1 = h - hw, d2 = h - hn;
            int sign = Math.Sign(d1) + Math.Sign(d2);
            float mag = Math.Max(Math.Abs(d1), Math.Abs(d2));
            float f = sign > 0 ? 1.08f + Math.Min(0.5f, mag / 10f) / 1.25f
                    : sign < 0 ? 0.92f - Math.Min(0.5f, mag / 10f) / 1.25f : 1f;
            outPx[i] = (int)(0xFF000000u | Mul(rgb, f));
        }
        return outPx;
    }

    private static uint Mul(uint rgb, float f)
    {
        uint r = (uint)Math.Clamp((int)(((rgb >> 16) & 0xFF) * f), 0, 255);
        uint g = (uint)Math.Clamp((int)(((rgb >> 8) & 0xFF) * f), 0, 255);
        uint b = (uint)Math.Clamp((int)((rgb & 0xFF) * f), 0, 255);
        return (r << 16) | (g << 8) | b;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeDb();
        pixels.Clear(); lru.Clear(); lruNodes.Clear(); fresh.Clear();
    }
}
