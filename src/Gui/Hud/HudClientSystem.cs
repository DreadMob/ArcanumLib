using System;
using System.Collections.Generic;
using ArcanumLib.Assets;
using ArcanumLib.Definitions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Generic client-side ModSystem for a data-driven HUD.
/// Loads definitions and themes, registers the network channel, and creates
/// a <typeparamref name="TDialog"/> when a snapshot arrives.
/// </summary>
public abstract class HudClientSystem<TSnapshot, THudDefinition, TTheme, TPanel, TDialog> : ModSystem
    where TSnapshot : class, IHudSnapshot
    where THudDefinition : class, IValidatableDefinition
    where TTheme : HudTheme
    where TPanel : HudPanel<TSnapshot, THudDefinition, TTheme>
    where TDialog : HudDialog<TSnapshot, THudDefinition, TTheme, TPanel>
{
    /// <summary>Client API reference.</summary>
    protected ICoreClientAPI? _capi;

    /// <summary>Currently active HUD dialog.</summary>
    protected TDialog? _hud;

    /// <summary>Loaded definitions keyed by their identifier.</summary>
    protected readonly Dictionary<string, THudDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loaded HUD themes keyed by name.</summary>
    protected readonly Dictionary<string, TTheme> _hudThemes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Asset path for definition JSON files (e.g. "config/encounters").</summary>
    protected abstract string DefinitionAssetPath { get; }

    /// <summary>Asset path for theme JSON files (e.g. "config/gui/hud-themes").</summary>
    protected abstract string ThemeAssetPath { get; }

    /// <summary>Network channel name used for snapshots.</summary>
    protected abstract string NetworkChannelName { get; }

    /// <summary>Unique identifier for the timeout tick listener.</summary>
    protected long _hudTimeoutListenerId;

    /// <summary>Client time in ms when the last snapshot was received.</summary>
    protected long _lastSnapshotMs;

    /// <summary>Default timeout after which the HUD is closed if no snapshots arrive.</summary>
    protected virtual long HudTimeoutMs => 5000;

    /// <summary>Returns the loaded definition for the given identifier, or null.</summary>
    public THudDefinition? GetDefinition(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _definitions.TryGetValue(id, out var def) ? def : null;
    }

    /// <summary>Returns the loaded HUD theme for the given name, or null.</summary>
    public TTheme? GetHudTheme(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _hudThemes.TryGetValue(name, out var theme) ? theme : null;
    }

    /// <summary>Resolves a theme by name, merging over the built-in default.</summary>
    public virtual TTheme ResolveTheme(string name) => HudThemeResolver.Resolve(name, _hudThemes, ResolveBuiltInTheme, CreateDefaultTheme());

    /// <summary>Returns a built-in theme for a name, or null when the name is not a built-in.</summary>
    protected abstract TTheme? ResolveBuiltInTheme(string name);

    /// <summary>Creates the default (fallback) theme.</summary>
    protected abstract TTheme CreateDefaultTheme();

    /// <summary>Creates the concrete HUD dialog instance.</summary>
    protected abstract TDialog CreateHud();

    /// <summary>Returns true when the snapshot should be shown for the given definition.</summary>
    protected virtual bool IsActive(TSnapshot snapshot, THudDefinition? definition)
        => !snapshot.IsRemoved() && definition != null;

    /// <summary>Called after a snapshot has been applied to the dialog and before cleanup.</summary>
    protected virtual void OnSnapshotApplied(TSnapshot snapshot) { }

    /// <summary>Pushes a snapshot directly into the HUD.</summary>
    public virtual void UpdateFromSnapshot(TSnapshot snapshot) => OnSnapshotReceived(snapshot);

    /// <summary>Client-side only.</summary>
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    /// <summary>
    /// Starts the client system: loads definitions and themes, then registers
    /// the network channel. Derived types can override <see cref="OnStarted"/>.
    /// </summary>
    public override void StartClientSide(ICoreClientAPI api)
    {
        _capi = api;

        LoadDefinitions();
        LoadThemes();

        var builder = api.Network.RegisterChannel(NetworkChannelName)
            .RegisterMessageType<TSnapshot>()
            .SetMessageHandler<TSnapshot>(OnSnapshotReceived);
        RegisterExtraMessageTypes(builder);

        _hudTimeoutListenerId = api.Event.RegisterGameTickListener(OnHudTimeoutTick, 1000);
        OnStarted();
    }

    /// <summary>Allows derived systems to register additional message types on the same channel.</summary>
    protected virtual void RegisterExtraMessageTypes(IClientNetworkChannel channel) { }

    /// <summary>Called after the network channel is registered.</summary>
    protected virtual void OnStarted() { }

    /// <summary>Reloads definitions and themes after asset hot-reload.</summary>
    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        if (_capi != null)
        {
            LoadDefinitions();
            LoadThemes();
        }
    }

    /// <summary>Loads definitions from <see cref="DefinitionAssetPath"/>.</summary>
    protected virtual void LoadDefinitions()
    {
        if (_capi == null) return;
        _definitions.Clear();
        foreach (var asset in ModAssetLoader.LoadAll<THudDefinition>(_capi, DefinitionAssetPath))
        {
            if (asset.Value?.IsValid() != true) continue;
            var id = GetDefinitionId(asset.Value);
            if (!string.IsNullOrWhiteSpace(id))
                _definitions[id] = asset.Value;
        }
    }

    /// <summary>Extracts the unique identifier from a loaded definition.</summary>
    protected abstract string GetDefinitionId(THudDefinition definition);

    /// <summary>Loads themes from <see cref="ThemeAssetPath"/>.</summary>
    protected virtual void LoadThemes()
    {
        if (_capi == null) return;
        _hudThemes.Clear();
        foreach (var kvp in ModAssetLoader.LoadFlatDictionary<TTheme>(_capi, ThemeAssetPath))
        {
            if (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value != null)
                _hudThemes[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>Called when a snapshot packet arrives.</summary>
    protected virtual void OnSnapshotReceived(TSnapshot snapshot)
    {
        if (snapshot == null || _capi == null) return;

        string id = GetSnapshotId(snapshot);
        var definition = string.IsNullOrWhiteSpace(id) ? null : GetDefinition(id);

        bool active = IsActive(snapshot, definition);
        if (!active)
        {
            _hud?.TryClose();
            _hud?.Dispose();
            _hud = null;
            return;
        }

        _lastSnapshotMs = _capi.World.ElapsedMilliseconds;

        if (_hud == null)
        {
            _hud = CreateHud();
            _capi.Gui.RegisterDialog(_hud);
        }

        _hud.SetDefinition(definition);
        _hud.OnSnapshotReceived(snapshot);
        OnSnapshotApplied(snapshot);
    }

    /// <summary>Extracts the definition identifier from a snapshot.</summary>
    protected abstract string GetSnapshotId(TSnapshot snapshot);

    /// <summary>Closes the HUD if no snapshot has been received within <see cref="HudTimeoutMs"/>.</summary>
    protected virtual void OnHudTimeoutTick(float dt)
    {
        if (_hud == null || _capi?.World == null) return;

        if (_capi.World.ElapsedMilliseconds - _lastSnapshotMs > HudTimeoutMs)
        {
            _hud.TryClose();
            _hud.Dispose();
            _hud = null;
            _lastSnapshotMs = 0;
        }
    }

    /// <summary>Disposes the dialog, tick listener and calls <see cref="OnDisposed"/>.</summary>
    public override void Dispose()
    {
        if (_hudTimeoutListenerId != 0 && _capi != null)
        {
            _capi.Event.UnregisterGameTickListener(_hudTimeoutListenerId);
            _hudTimeoutListenerId = 0;
        }

        _hud?.Dispose();
        _hud = null;
        OnDisposed();
        base.Dispose();
    }

    /// <summary>Called during <see cref="Dispose"/> after the HUD is closed.</summary>
    protected virtual void OnDisposed() { }
}
