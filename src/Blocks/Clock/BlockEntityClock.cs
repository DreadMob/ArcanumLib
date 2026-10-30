using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ArcanumLib.Blocks.Clock;

/// <summary>
/// Clock that shows game time, strikes the hour (configurable per hour), plays a melody at new year and a
/// warning before temporal storms. Look and sounds come from <see cref="ClockConfig"/> (<c>attributes.clock</c>).
/// Register under your own class name: <c>api.RegisterBlockEntityClass("mymod.BEClock", typeof(BlockEntityClock))</c>.
/// </summary>
public class BlockEntityClock : BlockEntity
{
    private long serverTickId;
    private long clientTickId;
    private long observedHour = -1;
    private long nextChimeAtMs;
    private int remainingChimes;
    private int observedYear = -1;
    private int newYearNoteIndex;
    private long nextNewYearNoteAtMs;
    private SystemTemporalStability? temporalStability;
    private bool wasStormActive;
    private int warningNoteIndex;
    private long nextWarningNoteAtMs;
    private bool tock;
    private ClockRenderer? renderer;
    private GuiDialogClock? clientDialog;

    internal const int PacketOpenGui = 4000;
    internal const int PacketSave = 4001;
    private const int DefaultEnabledHours = 0xFFFFFF;
    private int _enabledChimeHours = DefaultEnabledHours;

    private static readonly float[] NewYearPitches = new[]
    {
        1.0f, 1.125f, 1.25f, 1.5f, 1.25f, 1.125f, 1.0f, 2.0f,
        1.0f, 1.125f, 1.25f, 1.5f, 1.25f, 1.125f, 1.0f, 2.0f
    };

    private const int NewYearNoteIntervalMs = 1800;

    private static readonly float[] WarningPitches = new[]
    {
        0.60f, 0.55f, 0.50f, 0.45f, 0.50f, 0.55f, 0.60f, 0.55f, 0.50f, 0.45f
    };

    private const int WarningNoteIntervalMs = 1800;

    public ClockConfig Config { get; private set; } = new();

    public int EnabledChimeHours => _enabledChimeHours;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        Config = ClockConfig.From(Block);

        if (api is ICoreServerAPI sapi)
        {
            observedHour = (long)Math.Floor(api.World.Calendar.TotalHours);
            observedYear = api.World.Calendar.Year;
            newYearNoteIndex = NewYearPitches.Length;
            temporalStability = sapi.ModLoader.GetModSystem<SystemTemporalStability>();
            wasStormActive = temporalStability?.StormData?.nowStormActive == true;
            warningNoteIndex = WarningPitches.Length;
            serverTickId = sapi.Event.RegisterGameTickListener(OnServerTick, 250);
        }

        if (api is ICoreClientAPI capi)
        {
            renderer = new ClockRenderer(capi, this);
            if (ClockConfig.Sound(Config.TickSound) != null && Config.TickIntervalMs > 0)
                clientTickId = capi.Event.RegisterGameTickListener(OnClientTick, Config.TickIntervalMs);
        }
    }

    public override void OnBlockRemoved()
    {
        DisposeRuntime();
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        DisposeRuntime();
        base.OnBlockUnloaded();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("enabledChimeHours", _enabledChimeHours);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        _enabledChimeHours = tree.GetInt("enabledChimeHours", DefaultEnabledHours);
        clientDialog?.UpdateFromServer(BuildSettingsData());
    }

    internal void OnInteract(IPlayer byPlayer)
    {
        if (byPlayer == null) return;

        if (Api.Side == EnumAppSide.Server)
        {
            var sp = byPlayer as IServerPlayer;
            if (sp == null) return;
            PlayAt(Config.WindSound, 1f, Config.WindRange, 1f);
            var data = BuildSettingsData();
            (Api as ICoreServerAPI)?.Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
        }
        else if (Api.Side == EnumAppSide.Client)
        {
            var capi = Api as ICoreClientAPI;
            if (capi == null) return;
            if (clientDialog == null || !clientDialog.IsOpened())
            {
                OpenDialog(capi, BuildSettingsData());
            }
        }
    }

    public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
    {
        if (packetid != PacketSave) return;

        // Packet bytes are attacker-controlled — a malformed payload throws
        // inside Deserialize and must not escape the handler.
        ClockSettingsData? data;
        try { data = SerializerUtil.Deserialize<ClockSettingsData>(bytes); }
        catch (Exception) { return; }
        // 24 chime-hour bits — mask off anything beyond.
        _enabledChimeHours = (data?.EnabledHours ?? DefaultEnabledHours) & 0xFFFFFF;
        MarkDirty();

        var refreshed = BuildSettingsData();
        (Api as ICoreServerAPI)?.Network.SendBlockEntityPacket(fromPlayer as IServerPlayer, Pos, PacketOpenGui, SerializerUtil.Serialize(refreshed));
    }

    public override void OnReceivedServerPacket(int packetid, byte[] bytes)
    {
        if (packetid != PacketOpenGui) return;

        ClockSettingsData? data;
        try { data = SerializerUtil.Deserialize<ClockSettingsData>(bytes); }
        catch (Exception) { return; }   // malformed payload — drop, don't crash
        if (data == null) return;

        if (clientDialog == null || !clientDialog.IsOpened())
        {
            if (Api is ICoreClientAPI capi) OpenDialog(capi, data);
        }
        else
        {
            clientDialog.UpdateFromServer(data);
        }
    }

    private void OpenDialog(ICoreClientAPI capi, ClockSettingsData data)
    {
        clientDialog = new GuiDialogClock(Pos, capi, data, Config.LangPrefix);
        clientDialog.TryOpen();
        clientDialog.OnClosed += () =>
        {
            clientDialog?.Dispose();
            clientDialog = null;
        };
    }

    private ClockSettingsData BuildSettingsData()
    {
        return new ClockSettingsData { EnabledHours = _enabledChimeHours };
    }

    private void OnServerTick(float dt)
    {
        long currentHour = (long)Math.Floor(Api.World.Calendar.TotalHours);
        if (currentHour != observedHour)
        {
            bool movedForward = currentHour > observedHour;
            observedHour = currentHour;
            int h = (int)((currentHour % 24 + 24) % 24);
            bool enabled = movedForward && ((_enabledChimeHours >> h) & 1) != 0;
            remainingChimes = enabled ? ClockTime.GetChimeCount(currentHour) : 0;
            nextChimeAtMs = Api.World.ElapsedMilliseconds;
        }

        int currentYear = Api.World.Calendar.Year;
        if (currentYear > observedYear)
        {
            observedYear = currentYear;
            newYearNoteIndex = 0;
            nextNewYearNoteAtMs = Api.World.ElapsedMilliseconds;
        }

        if (remainingChimes > 0 && Api.World.ElapsedMilliseconds >= nextChimeAtMs)
        {
            if (!PlayAt(Config.ChimeSound, 1f, Config.ChimeRange, Config.ChimeVolume)) remainingChimes = 0;
            else remainingChimes--;
            nextChimeAtMs = Api.World.ElapsedMilliseconds + Config.ChimeIntervalMs;
        }

        if (newYearNoteIndex < NewYearPitches.Length && Api.World.ElapsedMilliseconds >= nextNewYearNoteAtMs)
        {
            if (!PlayAt(MelodySound, NewYearPitches[newYearNoteIndex], Config.NewYearRange, Config.ChimeVolume)) newYearNoteIndex = NewYearPitches.Length;
            else newYearNoteIndex++;
            nextNewYearNoteAtMs = Api.World.ElapsedMilliseconds + NewYearNoteIntervalMs;
        }

        bool stormActive = temporalStability?.StormData?.nowStormActive == true;
        if (stormActive && !wasStormActive)
        {
            warningNoteIndex = 0;
            nextWarningNoteAtMs = Api.World.ElapsedMilliseconds;
        }
        wasStormActive = stormActive;

        if (warningNoteIndex < WarningPitches.Length && Api.World.ElapsedMilliseconds >= nextWarningNoteAtMs)
        {
            if (!PlayAt(MelodySound, WarningPitches[warningNoteIndex], Config.WarningRange, Config.ChimeVolume)) warningNoteIndex = WarningPitches.Length;
            else warningNoteIndex++;
            nextWarningNoteAtMs = Api.World.ElapsedMilliseconds + WarningNoteIntervalMs;
        }
    }

    private string? MelodySound => Config.MelodySound ?? Config.ChimeSound;

    private void OnClientTick(float dt)
    {
        if (Api is not ICoreClientAPI capi) return;
        var plr = capi.World.Player?.Entity;
        if (plr == null) return;
        if (plr.Pos.SquareDistanceTo(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5) > Config.TickRange * Config.TickRange) return;

        tock = !tock;
        var loc = ClockConfig.Sound(Config.TickSound);
        if (loc == null) return;
        // Local-only sound: the server never streams a tick per clock per second.
        capi.World.PlaySoundAt(loc, Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, tock ? 0.94f : 1f, Config.TickRange, Config.TickVolume);
    }

    /// <summary>False when the sound failed to play (bad asset) — callers stop the sequence.</summary>
    private bool PlayAt(string? sound, float pitch, float range, float volume)
    {
        var loc = ClockConfig.Sound(sound);
        if (loc == null) return true;
        try
        {
            Api.World.PlaySoundAt(loc, Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, pitch, range, volume);
            return true;
        }
        catch (Exception ex)
        {
            Api.Logger.Error("[Clock] Failed to play {0} at {1}: {2}", loc, Pos, ex);
            return false;
        }
    }

    private void DisposeRuntime()
    {
        if (Api is ICoreServerAPI sapi && serverTickId != 0)
        {
            sapi.Event.UnregisterGameTickListener(serverTickId);
            serverTickId = 0;
        }
        if (Api is ICoreClientAPI capi && clientTickId != 0)
        {
            capi.Event.UnregisterGameTickListener(clientTickId);
            clientTickId = 0;
        }

        renderer?.Dispose();
        renderer = null;

        clientDialog?.TryClose();
        clientDialog?.Dispose();
        clientDialog = null;
    }
}
