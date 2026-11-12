using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ArcanumLib.Blocks;

/// <summary>
/// Widens vanilla's multiblock filler blocks (<c>game:multiblock-monolithic-{dx}-{dy}-{dz}</c>, offsets −2…+2 sideways
/// and −2…+3 up in vanilla) to <see cref="Side"/> cells sideways and <see cref="Up"/> cells up, so a mod's
/// <c>Multiblock</c> behaviour may have its controller up to that far from any cell. Mods using ArcanumLib need no
/// multiblock patch of their own.
/// <para>Runs on the server after the JSON patches and before the blocktypes are read: it adds the missing offsets
/// and drops repeated ones, so another mod patching the same states (an older version of ours, or someone else's)
/// cannot make two blocks of one code. Clients get the blocks from the server.</para>
/// </summary>
public class MultiblockReach : ModSystem
{
    /// <summary>Furthest filler offset along x and z, either way.</summary>
    public const int Side = 5;
    /// <summary>Furthest filler offset up; down stays vanilla's 2.</summary>
    public const int Up = 8;
    public const int Down = 2;

    public static readonly AssetLocation Asset = new("game", "blocktypes/multiblock.json");

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    /// <summary>After <c>ModJsonPatchLoader</c> (0.05), before <c>ModRegistryObjectTypeLoader</c> (0.2).</summary>
    public override double ExecuteOrder() => 0.1;

    public override void AssetsLoaded(ICoreAPI api)
    {
        var asset = api.Assets.TryGet(Asset);
        if (asset?.Data == null)
        {
            api.Logger.Warning("[ArcanumLib] {0} not found, multiblock fillers stay vanilla", Asset);
            return;
        }
        JObject json;
        try { json = JObject.Parse(Encoding.UTF8.GetString(asset.Data).TrimStart('﻿')); }
        catch (Exception e)
        {
            api.Logger.Error("[ArcanumLib] cannot read {0}: {1}", Asset, e.Message);
            return;
        }
        if (Widen(json)) asset.Data = Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
    }

    /// <summary>Code of an offset as the variant writes it: n2, 0, p5.</summary>
    public static string Code(int offset) => offset < 0 ? "n" + -offset : offset > 0 ? "p" + offset : "0";

    /// <summary>The offsets a variant group must hold, in order.</summary>
    public static IEnumerable<string> Wanted(string group) => group switch
    {
        "dy" => Enumerable.Range(-Down, Down + Up + 1).Select(Code),
        _ => Enumerable.Range(-Side, 2 * Side + 1).Select(Code),
    };

    /// <summary>Gives the dx/dy/dz groups of the multiblock blocktype every wanted offset once, keeping any others
    /// a mod added. Returns whether anything changed.</summary>
    public static bool Widen(JObject blocktype)
    {
        if (blocktype["variantgroups"] is not JArray groups) return false;
        bool changed = false;
        foreach (var group in groups.OfType<JObject>())
        {
            string? code = (string?)group["code"];
            if (code is not ("dx" or "dy" or "dz") || group["states"] is not JArray states) continue;
            var have = states.Select(s => (string?)s).Where(s => s != null).Cast<string>().ToList();
            var merged = have.Concat(Wanted(code)).Distinct().ToList();
            if (merged.SequenceEqual(have)) continue;
            group["states"] = new JArray(merged);
            changed = true;
        }
        return changed;
    }
}
