using System;
using System.Collections.Generic;

namespace ArcanumLib.Gui.Hud;

/// <summary>
/// Registry for <see cref="IHudElementRenderer"/> instances keyed by element <c>type</c>.
/// </summary>
public sealed class HudElementRendererRegistry
{
    private readonly Dictionary<string, IHudElementRenderer> _renderers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a renderer for all its <see cref="IHudElementRenderer.SupportedTypes"/>.</summary>
    public void Register(IHudElementRenderer renderer)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        foreach (var type in renderer.SupportedTypes)
        {
            if (string.IsNullOrWhiteSpace(type)) continue;
            _renderers[type] = renderer;
        }
    }

    /// <summary>Returns a renderer for the given type, or null if none is registered.</summary>
    public IHudElementRenderer? Get(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        return _renderers.TryGetValue(type, out var renderer) ? renderer : null;
    }

    /// <summary>Attempts to get a renderer for the given type.</summary>
    public bool TryGet(string type, out IHudElementRenderer? renderer)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            renderer = null;
            return false;
        }
        return _renderers.TryGetValue(type, out renderer);
    }

    /// <summary>Measures the height of an element using its registered renderer. Returns 0 if no renderer is found.</summary>
    public double MeasureHeight(string type, HudElementMeasureArgs args)
    {
        var renderer = Get(type);
        return renderer?.MeasureHeight(args) ?? 0;
    }

    /// <summary>Measures the minimum width of an element using its registered renderer. Returns 0 if no renderer is found.</summary>
    public double MeasureMinWidth(string type, HudElementMeasureArgs args)
    {
        var renderer = Get(type);
        return renderer?.MeasureMinWidth(args) ?? 0;
    }
}
