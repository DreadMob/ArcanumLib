using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;

namespace ArcanumLib.Diagnostics;

/// <summary>
/// Lightweight process-wide counters for Cairo→<c>LoadedTexture</c> bakes done by
/// GUI controls. Controls opt in: call <see cref="Register" /> on construction,
/// <see cref="Regen" /> each time a texture is (re)baked, and
/// <see cref="Unregister" /> on dispose. GUI work happens on the main thread, so
/// no locking is required; <see cref="Interlocked" /> is still used for safety.
/// </summary>
public static class GuiTextureTracker
{
    private sealed class Counters
    {
        public int Alive;
        public int Regens;
    }

    private static readonly ConcurrentDictionary<string, Counters> Table = new(StringComparer.Ordinal);

    /// <summary>
    /// Marks one control instance of <paramref name="controlName" /> as alive.
    /// Pair every call with a matching <see cref="Unregister" /> on dispose.
    /// </summary>
    /// <param name="controlName">The control name, typically <c>nameof(TheControl)</c>.</param>
    public static void Register(string controlName)
    {
        if (string.IsNullOrEmpty(controlName)) return;
        Interlocked.Increment(ref Table.GetOrAdd(controlName, _ => new Counters()).Alive);
    }

    /// <summary>
    /// Marks one control instance of <paramref name="controlName" /> as disposed.
    /// A negative alive count in <see cref="Dump" /> indicates a double dispose.
    /// </summary>
    /// <param name="controlName">The control name, typically <c>nameof(TheControl)</c>.</param>
    public static void Unregister(string controlName)
    {
        if (string.IsNullOrEmpty(controlName)) return;
        if (Table.TryGetValue(controlName, out var counters))
            Interlocked.Decrement(ref counters.Alive);
    }

    /// <summary>
    /// Counts one texture (re)bake for <paramref name="controlName" /> and returns
    /// the new regen count. A regen count that climbs every frame is the classic
    /// texture-leak / regen-thrash symptom.
    /// </summary>
    /// <param name="controlName">The control name, typically <c>nameof(TheControl)</c>.</param>
    /// <returns>The updated regen count for the control.</returns>
    public static int Regen(string controlName)
    {
        if (string.IsNullOrEmpty(controlName)) return 0;
        return Interlocked.Increment(ref Table.GetOrAdd(controlName, _ => new Counters()).Regens);
    }

    /// <summary>
    /// Returns the current number of texture (re)bakes counted for
    /// <paramref name="controlName" />.
    /// </summary>
    /// <param name="controlName">The control name, typically <c>nameof(TheControl)</c>.</param>
    /// <returns>The regen count, or 0 when the control was never tracked.</returns>
    public static int RegenCount(string controlName)
    {
        if (string.IsNullOrEmpty(controlName)) return 0;
        return Table.TryGetValue(controlName, out var counters) ? counters.Regens : 0;
    }

    /// <summary>
    /// Returns the current number of living (registered but not unregistered)
    /// control instances for <paramref name="controlName" />.
    /// </summary>
    /// <param name="controlName">The control name, typically <c>nameof(TheControl)</c>.</param>
    /// <returns>The alive count, or 0 when the control was never tracked.</returns>
    public static int AliveCount(string controlName)
    {
        if (string.IsNullOrEmpty(controlName)) return 0;
        return Table.TryGetValue(controlName, out var counters) ? counters.Alive : 0;
    }

    /// <summary>
    /// Returns a multi-line snapshot of all tracked controls, one
    /// <c>control: alive=N regens=M</c> line per control, sorted by name.
    /// </summary>
    /// <param name="onlyAlive">When true, only controls with at least one live instance are included.</param>
    /// <returns>The dump text; empty when nothing matches.</returns>
    public static string Dump(bool onlyAlive = false)
    {
        var sb = new StringBuilder();
        foreach (var pair in Table.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            int alive = pair.Value.Alive;
            if (onlyAlive && alive <= 0) continue;
            sb.Append(pair.Key).Append(": alive=").Append(alive)
                .Append(" regens=").Append(pair.Value.Regens).Append('\n');
        }
        return sb.ToString();
    }
}
