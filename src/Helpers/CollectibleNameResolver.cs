using System;
using System.Collections.Generic;
using System.Linq;
using ArcanumLib.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ArcanumLib.Helpers;

/// <summary>
/// Resolves human-readable names and icon codes for Vintage Story collectibles.
/// Works for items, blocks and entity types, including wildcard patterns.
/// Results are cached per language to avoid repeated registry scans.
/// </summary>
public static class CollectibleNameResolver
{
    private static string? _nameCacheLanguage;
    private static readonly Dictionary<string, string> _nameCache = new(StringComparer.OrdinalIgnoreCase);

    private static string? _iconCacheLanguage;
    private static readonly Dictionary<string, string?> _iconCodeCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves a readable display name for an item, block or entity code.
    /// Wildcards are accepted. Falls back to a pretty-printed code string.
    /// </summary>
    /// <param name="api">The core API for registry lookups.</param>
    /// <param name="code">The asset code or wildcard pattern.</param>
    /// <param name="mobNameResolver">
    /// Optional resolver for entity codes (e.g. <c>MobLocalizationUtils.GetMobDisplayName</c>).
    /// If null, entity names fall back to the code path.
    /// </param>
    public static string GetDisplayName(ICoreAPI api, string code, System.Func<string?, string?>? mobNameResolver = null)
    {
        if (string.IsNullOrWhiteSpace(code)) return code ?? "";

        if (code.EndsWith("*"))
        {
            string prefix = code.Substring(0, code.Length - 1);
            return ResolveNameFromLangKey(code, prefix)
                ?? ResolveFirstMatchingName(api, prefix, mobNameResolver)
                ?? Pretty.TargetCode(prefix);
        }

        var loc = AssetLocation.CreateOrNull(code);
        if (loc != null)
        {
            var item = api.World.GetItem(loc);
            if (item != null) return GetCollectibleDisplayName(item, tryItemStackName: true, api);

            var block = api.World.GetBlock(loc);
            if (block != null) return GetCollectibleDisplayName(block, tryItemStackName: true, api);

            var entity = api.World.GetEntityType(loc);
            if (entity != null)
            {
                string? name = mobNameResolver?.Invoke(entity.Code?.ToString()) ?? entity.Code?.Path;
                return !string.IsNullOrWhiteSpace(name) ? name! : Pretty.TargetCode(code);
            }
        }

        string? fromLang = ResolveNameFromLangKey(code + "-*", code + "-");
        if (!string.IsNullOrWhiteSpace(fromLang)) return fromLang!;

        string? fromWildcard = ResolveFirstMatchingName(api, code + "-", mobNameResolver);
        if (!string.IsNullOrWhiteSpace(fromWildcard)) return fromWildcard!;

        return Pretty.TargetCode(code);
    }

    /// <summary>
    /// Resolves the first collectible that matches a wildcard prefix and has a
    /// valid display name. Scans blocks, then items, then entity types.
    /// </summary>
    public static string? ResolveFirstMatchingName(ICoreAPI api, string prefix, System.Func<string?, string?>? mobNameResolver = null)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return null;

        string pattern = prefix.EndsWith("*", StringComparison.Ordinal) ? prefix : prefix + "*";

        foreach (var bl in api.World.Blocks)
        {
            var c = bl.Code?.ToString();
            if (c != null && MatchesPattern(c, pattern))
            {
                string name = GetCollectibleDisplayName(bl, tryItemStackName: true, api);
                if (IsValidDisplayName(bl, name)) return name;
            }
        }

        foreach (var it in api.World.Items)
        {
            var c = it.Code?.ToString();
            if (c != null && MatchesPattern(c, pattern))
            {
                string name = GetCollectibleDisplayName(it, tryItemStackName: true, api);
                if (IsValidDisplayName(it, name)) return name;
            }
        }

        foreach (var et in api.World.EntityTypes)
        {
            var c = et.Code?.ToString();
            if (c != null && MatchesPattern(c, pattern))
            {
                string? name = mobNameResolver?.Invoke(c) ?? et.Code?.Path;
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name!.Equals(et.Code?.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return name!;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a display name for a specific collectible, preferring language keys
    /// and falling back to <c>ItemStack.GetName()</c> or a pretty-printed code path.
    /// </summary>
    /// <param name="obj">The item or block.</param>
    /// <param name="tryItemStackName">Whether to allocate an <c>ItemStack</c> if lang keys are missing.</param>
    /// <param name="api">Optional API for logging non-critical exceptions.</param>
    public static string GetCollectibleDisplayName(CollectibleObject obj, bool tryItemStackName = true, ICoreAPI? api = null)
    {
        if (obj?.Code == null) return "";

        string code = obj.Code.ToString();
        EnsureNameCacheLanguage();

        if (_nameCache.TryGetValue(code, out string? cached) && cached != null)
            return cached;

        string? result = null;

        string domain = obj.Code.Domain;
        string path = obj.Code.Path;
        string type = obj is Block ? "block" : "item";

        string[] keys = new[]
        {
            $"{domain}:{type}-{path}",
            $"{domain}:{type}-{path}-name"
        };

        foreach (var key in keys)
        {
            string title = Lang.GetIfExists(key);
            if (!string.IsNullOrWhiteSpace(title) && !title.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                result = Pretty.Sanitize(title);
                break;
            }
        }

        if (result == null && tryItemStackName)
        {
            try
            {
                string name = new ItemStack(obj).GetName();
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name.Equals(code, StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    result = Pretty.Sanitize(name);
                }
            }
            catch (Exception ex)
            {
                api?.Logger?.Warning("[CollectibleNameResolver] failed to read ItemStack name for {0}: {1}", code, ex.Message);
            }
        }

        string fallback = Pretty.Readable(path);
        if (result == null)
            result = fallback;

        if (tryItemStackName || !string.Equals(result, fallback, StringComparison.OrdinalIgnoreCase))
            _nameCache[code] = result;

        return result;
    }

    /// <summary>
    /// Returns true when <paramref name="name"/> is a real display name and not just
    /// the code, path, or a mechanical pretty-print of the path.
    /// </summary>
    public static bool IsValidDisplayName(CollectibleObject obj, string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || obj?.Code == null) return false;

        string code = obj.Code.ToString();
        string path = obj.Code.Path;

        if (name.Equals(code, StringComparison.OrdinalIgnoreCase)) return false;
        if (name.Equals(path, StringComparison.OrdinalIgnoreCase)) return false;

        string pretty = Pretty.Readable(path);
        if (name.Equals(pretty, StringComparison.OrdinalIgnoreCase)) return false;

        string compact = pretty.Replace(" ", "-");
        if (name.Equals(compact, StringComparison.OrdinalIgnoreCase)) return false;

        string expanded = path.Replace('-', ' ');
        if (name.Equals(expanded, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    /// <summary>
    /// Tries to resolve a wildcard target using generic language keys
    /// (e.g. <c>game:item-flower-*</c>). The key must be present in the language file.
    /// </summary>
    public static string? ResolveNameFromLangKey(string code, string prefix)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(prefix)) return null;

        int colon = prefix.IndexOf(':');
        if (colon <= 0 || colon >= prefix.Length - 1) return null;

        string domain = prefix.Substring(0, colon);
        string path = prefix.Substring(colon + 1);

        string[] candidates = new[]
        {
            $"{domain}:item-{path}*",
            $"{domain}:block-{path}*"
        };

        foreach (var key in candidates)
        {
            string name = Lang.GetIfExists(key);
            if (!string.IsNullOrWhiteSpace(name) && !name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return Pretty.Sanitize(name);
        }

        return null;
    }

    /// <summary>
    /// Matches a collectible code against a pattern. Simple prefix patterns use
    /// <c>StartsWith</c>; patterns with a wildcard in the middle use full wildcard matching.
    /// </summary>
    public static bool MatchesPattern(string code, string pattern)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(pattern)) return false;

        if (!pattern.Contains("*")) return code.Equals(pattern, StringComparison.OrdinalIgnoreCase);

        if (Wildcard.IsSimplePrefix(pattern))
        {
            string prefix = pattern.Substring(0, pattern.Length - 1);
            return code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return Wildcard.Match(code, pattern);
    }

    /// <summary>
    /// Resolves a wildcard pattern to a concrete item or block code that can be used
    /// for an icon. Returns the original code if it already points to a real collectible.
    /// </summary>
    public static string? ResolveIconCode(ICoreAPI api, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        EnsureIconCacheLanguage();
        if (_iconCodeCache.TryGetValue(code, out string? cached) && cached != null)
            return cached;

        if (!code.EndsWith("*"))
        {
            var loc = AssetLocation.CreateOrNull(code);
            if (loc != null && (api.World.GetItem(loc) != null || api.World.GetBlock(loc) != null))
            {
                _iconCodeCache[code] = code;
                return code;
            }

            _iconCodeCache[code] = null;
            return null;
        }

        foreach (var it in api.World.Items)
        {
            var c = it.Code?.ToString();
            if (c != null && MatchesPattern(c, code))
            {
                _iconCodeCache[code] = c;
                return c;
            }
        }

        foreach (var bl in api.World.Blocks)
        {
            var c = bl.Code?.ToString();
            if (c != null && MatchesPattern(c, code))
            {
                _iconCodeCache[code] = c;
                return c;
            }
        }

        _iconCodeCache[code] = null;
        return null;
    }

    private static void EnsureNameCacheLanguage()
    {
        string lang = Lang.CurrentLocale ?? "en";
        if (!string.Equals(_nameCacheLanguage, lang, StringComparison.OrdinalIgnoreCase))
        {
            _nameCacheLanguage = lang;
            _nameCache.Clear();
        }
    }

    private static void EnsureIconCacheLanguage()
    {
        string lang = Lang.CurrentLocale ?? "en";
        if (!string.Equals(_iconCacheLanguage, lang, StringComparison.OrdinalIgnoreCase))
        {
            _iconCacheLanguage = lang;
            _iconCodeCache.Clear();
        }
    }
}
