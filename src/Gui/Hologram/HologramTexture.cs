using System;
using Vintagestory.API.Client;

namespace ArcanumLib.Gui.Hologram;

/// <summary>
/// Holds the generated <see cref="LoadedTexture"/> for a hologram, plus metadata used to track updates.
/// </summary>
public sealed class HologramTexture : IDisposable
{
    /// <summary>The loaded OpenGL texture, or null if the generation failed.</summary>
    public LoadedTexture? Texture { get; internal set; }

    /// <summary>Version that produced this texture. Compare with <see cref="IHologramTextSource.GetHologramVersion"/>.</summary>
    public long Version { get; internal set; }

    /// <summary>True when the texture is loaded and has a valid id.</summary>
    public bool IsValid => Texture != null && Texture.TextureId != 0;

    /// <summary>Texture width in pixels.</summary>
    public int Width => Texture?.Width ?? 0;

    /// <summary>Texture height in pixels.</summary>
    public int Height => Texture?.Height ?? 0;

    /// <summary>Disposes the underlying loaded texture.</summary>
    public void Dispose()
    {
        Texture?.Dispose();
        Texture = null;
    }
}
