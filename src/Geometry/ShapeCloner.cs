using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ArcanumLib.Geometry;

/// <summary>
/// Deep-clones <see cref="Shape" /> instances so they can be safely modified
/// without sharing mutable state with the original. Useful for wearable/tessellation
/// patching where the engine may cache and reuse a single shape for many renderers.
/// </summary>
public static class ShapeCloner
{
    /// <summary>
    /// Creates a deep copy of the source shape. The returned shape has its own
    /// <see cref="Shape.Textures" />, <see cref="Shape.TextureSizes" />,
    /// <see cref="ShapeElement.FacesResolved" />, and <see cref="ShapeElement.AttachmentPoints" />.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <returns>The deep clone, or null if none is found.</returns>
    public static Shape? DeepClone(Shape? source)
    {
        if (source == null) return null;

        var output = source.Clone();

        if (output == null) return null;

        if (source.Textures != null)
        {
            output.Textures = new Dictionary<string, AssetLocation>(source.Textures.Count);
            foreach (var (key, value) in source.Textures)
            {
                output.Textures[key] = value == null
                    ? null
                    : new AssetLocation(value.Domain, value.Path);
            }
        }

        if (source.TextureSizes != null)
        {
            output.TextureSizes = new Dictionary<string, int[]>(source.TextureSizes.Count);
            foreach (var (key, value) in source.TextureSizes)
            {
                output.TextureSizes[key] = value == null ? null : (int[])value.Clone();
            }
        }

        if (source.Elements != null)
        {
            for (int i = 0; i < source.Elements.Length; i++)
            {
                if (source.Elements[i] != null)
                {
                    DeepCloneElement(source.Elements[i], output.Elements[i]);
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Loads a shape from the asset pipeline and returns a deep clone suitable for
    /// in-place mutation. Returns <c>null</c> if the shape could not be loaded.
    /// </summary>
    /// <param name="api">The core API instance.</param>
    /// <param name="location">The asset location.</param>
    /// <returns>The load and clone, or null if none is found.</returns>
    public static Shape? LoadAndClone(ICoreAPI api, AssetLocation location)
    {
        if (api?.Assets == null || location == null) return null;

        var asset = api.Assets.TryGet(location.Clone());
        var source = asset?.ToObject<Shape>();
        return DeepClone(source);
    }

    /// <summary>
    /// Loads a shape from the asset pipeline and returns a deep clone.
    /// </summary>
    /// <param name="api">The core API instance.</param>
    /// <param name="path">The path.</param>
    /// <returns>The load and clone, or null if none is found.</returns>
    public static Shape? LoadAndClone(ICoreAPI api, string path)
    {
        if (api?.Assets == null || string.IsNullOrWhiteSpace(path)) return null;

        var asset = api.Assets.TryGet(path);
        var source = asset?.ToObject<Shape>();
        return DeepClone(source);
    }

    private static void DeepCloneElement(ShapeElement? source, ShapeElement? output)
    {
        if (source == null || output == null) return;

        if (source.FacesResolved != null)
        {
            output.FacesResolved = new ShapeElementFace[6];
            for (int i = 0; i < source.FacesResolved.Length; i++)
            {
                output.FacesResolved[i] = CloneFace(source.FacesResolved[i])!;
            }
        }

        if (source.AttachmentPoints != null)
        {
            output.AttachmentPoints = new AttachmentPoint[source.AttachmentPoints.Length];
            for (int i = 0; i < source.AttachmentPoints.Length; i++)
            {
                var sourceAp = source.AttachmentPoints[i];
                output.AttachmentPoints[i] = sourceAp == null
                    ? null!
                    : new AttachmentPoint
                    {
                        Code = sourceAp.Code,
                        PosX = sourceAp.PosX,
                        PosY = sourceAp.PosY,
                        PosZ = sourceAp.PosZ,
                        RotationX = sourceAp.RotationX,
                        RotationY = sourceAp.RotationY,
                        RotationZ = sourceAp.RotationZ,
                        ParentElement = output
                    };
            }
        }

        if (source.Children != null && output.Children != null)
        {
            for (int i = 0; i < source.Children.Length; i++)
            {
                if (source.Children[i] != null)
                {
                    DeepCloneElement(source.Children[i], output.Children[i]);
                }
            }
        }
    }

    private static ShapeElementFace? CloneFace(ShapeElementFace? face)
    {
        if (face == null) return null;

        return new ShapeElementFace
        {
            Texture = face.Texture,
            Uv = face.Uv == null ? null : (float[])face.Uv.Clone(),
            ReflectiveMode = face.ReflectiveMode,
            WindMode = face.WindMode == null ? null : (sbyte[])face.WindMode.Clone(),
            WindData = face.WindData == null ? null : (sbyte[])face.WindData.Clone(),
            Rotation = face.Rotation,
            Glow = face.Glow,
            Enabled = face.Enabled
        };
    }
}
