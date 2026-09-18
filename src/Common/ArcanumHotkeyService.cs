using System;
using System.Collections.Concurrent;
using ArcanumLib.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ArcanumLib.Common;

/// <summary>
/// Registry of named hotkeys backed by <see cref="IInputAPI.RegisterHotKey" /> and
/// <see cref="IInputAPI.SetHotKeyHandler" />. Centralizes key-combination parsing,
/// tracks which hotkey ids were registered through the service, and detaches
/// handlers on <see cref="Unregister" /> — Vintage Story itself exposes no public
/// hotkey-removal API.
/// </summary>
/// <remarks>
/// Hotkeys are a client-only concept. When constructed with an
/// <see cref="ICoreServerAPI" /> the service still records registrations so that
/// <see cref="IsRegistered" />, <see cref="Unregister" /> and
/// <see cref="UnregisterAll" /> behave uniformly in common code, but no key
/// handling is wired up and handlers never fire.
/// Resolve a shared per-side instance via <see cref="For" />; the instance is kept
/// in <see cref="ArcanumServices" /> and disposed automatically on runtime shutdown.
/// </remarks>
public sealed class ArcanumHotkeyService : IDisposable
{
    private sealed class HotkeyRegistration
    {
        public string Id = "";
        public string KeyCombination = "";
        public string? Description;
        public Action Handler = static () => { };
        public bool Active = true;
    }

    private readonly ICoreAPI _api;
    private readonly ICoreClientAPI? _capi;
    private readonly ConcurrentDictionary<string, HotkeyRegistration> _registrations = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>
    /// Creates a hotkey service bound to the given API. The API's side decides
    /// whether real key handling is wired: client APIs register live hotkeys,
    /// server APIs only record bookkeeping.
    /// </summary>
    /// <param name="api">The core API, either client or server.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    public ArcanumHotkeyService(ICoreAPI api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _capi = api as ICoreClientAPI;
    }

    /// <summary>
    /// Returns the shared <see cref="ArcanumHotkeyService" /> for the side of the
    /// given API, creating and registering it in <see cref="ArcanumServices" /> on
    /// first use.
    /// </summary>
    /// <param name="api">The core API, either client or server.</param>
    /// <returns>The per-side shared hotkey service.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no <see cref="ArcanumRuntime" /> is active.</exception>
    public static ArcanumHotkeyService For(ICoreAPI api)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        return ArcanumServices.EnsureInitialized(
            () => new ArcanumHotkeyService(api),
            ArcanumServiceRegistry.ScopeFor(api));
    }

    /// <summary>
    /// Returns the shared <see cref="ArcanumHotkeyService" /> for the side of the
    /// given API if one was already created via <see cref="For" />, otherwise
    /// <c>null</c> (also when no runtime is active).
    /// </summary>
    /// <param name="api">The core API, either client or server.</param>
    /// <returns>The shared service instance, or <c>null</c>.</returns>
    public static ArcanumHotkeyService? Get(ICoreAPI api)
    {
        if (api == null) return null;
        return ArcanumServices.Get<ArcanumHotkeyService>(ArcanumServiceRegistry.ScopeFor(api));
    }

    /// <summary>
    /// Registers a hotkey under <paramref name="id" /> and wires
    /// <paramref name="handler" /> to fire when the key combination is pressed.
    /// </summary>
    /// <param name="id">Unique hotkey code, e.g. <c>"mymod-mainmenu"</c>.</param>
    /// <param name="keyCombination">
    /// Key combination such as <c>"K"</c>, <c>"Ctrl+K"</c> or
    /// <c>"ctrl+shift+f5"</c>. Modifier tokens (<c>ctrl</c>/<c>control</c>,
    /// <c>alt</c>, <c>shift</c>) plus a <see cref="GlKeys" /> key name, separated
    /// by <c>+</c>, spaces, commas or semicolons.
    /// </param>
    /// <param name="description">Human-readable name shown in the controls dialog.</param>
    /// <param name="handler">Invoked when the hotkey fires; the keypress is marked as consumed.</param>
    /// <param name="type">Hotkey category for the controls dialog. Defaults to <see cref="HotkeyType.GUIOrOtherControls" />.</param>
    /// <returns><c>true</c> when the hotkey was registered (or updated); <c>false</c> when the combination could not be parsed or the game rejected it.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler" /> is <see langword="null" />.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the service has been disposed.</exception>
    public bool Register(string id, string keyCombination, string description, Action handler, HotkeyType? type = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Hotkey id cannot be empty.", nameof(id));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (_disposed) throw new ObjectDisposedException(nameof(ArcanumHotkeyService));

        if (!TryParseKeyCombination(keyCombination, out var key, out var ctrl, out var alt, out var shift))
        {
            _api.Logger?.Warning(
                "[ArcanumLib] [ArcanumHotkeyService] Cannot register hotkey '{0}': unparseable key combination '{1}'.",
                id, keyCombination);
            return false;
        }

        var reg = new HotkeyRegistration
        {
            Id = id,
            KeyCombination = keyCombination,
            Description = description,
            Handler = handler,
        };

        if (_capi != null)
        {
            if (!_registrations.ContainsKey(id))
            {
                if (_capi.Input.GetHotKeyByCode(id) != null)
                {
                    _api.Logger?.Warning(
                        "[ArcanumLib] [ArcanumHotkeyService] Hotkey '{0}' is already registered outside this service; attaching handler anyway.",
                        id);
                }
                else
                {
                    try
                    {
                        _capi.Input.RegisterHotKey(id, description ?? id, key,
                            type ?? HotkeyType.GUIOrOtherControls,
                            altPressed: alt, ctrlPressed: ctrl, shiftPressed: shift);
                    }
                    catch (Exception ex)
                    {
                        _api.Logger?.Warning(
                            "[ArcanumLib] [ArcanumHotkeyService] RegisterHotKey('{0}') failed: {1}", id, ex.Message);
                        return false;
                    }
                }
            }

            var captured = reg;
            try
            {
                _capi.Input.SetHotKeyHandler(id, _ =>
                {
                    if (!captured.Active) return false;
                    captured.Handler();
                    return true;
                });
            }
            catch (Exception ex)
            {
                _api.Logger?.Warning(
                    "[ArcanumLib] [ArcanumHotkeyService] SetHotKeyHandler('{0}') failed: {1}", id, ex.Message);
                return false;
            }
        }

        _registrations[id] = reg;
        return true;
    }

    /// <summary>
    /// Removes a hotkey previously registered through this service. The handler is
    /// detached immediately; the keybinding entry is also removed from the game's
    /// hotkey table when the game allows it.
    /// </summary>
    /// <param name="id">The hotkey code passed to <see cref="Register" />.</param>
    /// <returns><c>true</c> when a registration existed and was removed.</returns>
    public bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (!_registrations.TryRemove(id, out var reg)) return false;

        // The wired closure checks Active — this detaches the handler even if the
        // keybinding entry itself cannot be removed from the game registry.
        reg.Active = false;

        if (_capi != null)
        {
            try
            {
                _capi.Input.HotKeys.Remove(id);
            }
            catch (Exception ex)
            {
                _api.Logger?.Warning(
                    "[ArcanumLib] [ArcanumHotkeyService] Could not remove hotkey '{0}' from the game registry; its handler is now inert. {1}",
                    id, ex.Message);
            }
        }
        return true;
    }

    /// <summary>
    /// Returns whether a hotkey with the given <paramref name="id" /> is currently
    /// registered through this service.
    /// </summary>
    /// <param name="id">The hotkey code to test.</param>
    /// <returns><c>true</c> if registered; otherwise <c>false</c>.</returns>
    public bool IsRegistered(string id)
        => !string.IsNullOrWhiteSpace(id) && _registrations.ContainsKey(id);

    /// <summary>
    /// Unregisters every hotkey registered through this service.
    /// </summary>
    /// <returns>The number of registrations removed.</returns>
    public int UnregisterAll()
    {
        var count = 0;
        foreach (var id in _registrations.Keys)
        {
            if (Unregister(id)) count++;
        }
        return count;
    }

    /// <summary>Detaches all handlers and clears the registration table.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
    }

    private static bool TryParseKeyCombination(
        string combination, out GlKeys key, out bool ctrl, out bool alt, out bool shift)
    {
        key = GlKeys.Unknown;
        ctrl = alt = shift = false;
        if (string.IsNullOrWhiteSpace(combination)) return false;

        foreach (var raw in combination.Split('+', ' ', ',', ';'))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;

            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                case "lctrl":
                case "rctrl":
                    ctrl = true;
                    continue;
                case "alt":
                case "lalt":
                case "ralt":
                    alt = true;
                    continue;
                case "shift":
                case "lshift":
                case "rshift":
                    shift = true;
                    continue;
            }

            if (key == GlKeys.Unknown
                && Enum.TryParse(token, ignoreCase: true, out GlKeys parsed)
                && parsed != GlKeys.Unknown)
            {
                key = parsed;
                continue;
            }
            return false;
        }

        return key != GlKeys.Unknown;
    }
}
