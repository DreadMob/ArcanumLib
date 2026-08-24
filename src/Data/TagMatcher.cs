using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.Data;

/// <summary>
/// A reusable matcher that checks collectibles and item stacks against a set of
/// include/exclude tag-sets and optional code patterns. Supports AND/OR semantics
/// across multiple tag groups, and wildcard code prefixes for fine-grained filtering.
/// </summary>
public sealed class TagMatcher
{
    private readonly List<TagSet> _includeAny = new();
    private readonly List<TagSet> _excludeAny = new();
    private readonly List<string> _codePatterns = new();
    private MatchMode _tagMode = MatchMode.Any;

    /// <summary>
    /// How multiple include tag-sets are combined.
    /// </summary>
    public enum MatchMode
    {
        /// <summary>
        /// A collectible matches if it has at least one tag from any include set.
        /// This is the default and matches the common "any of these tags" use case.
        /// </summary>
        Any,

        /// <summary>
        /// A collectible matches if it has at least one tag from every include set.
        /// Use this for "must have a tag from group A AND a tag from group B" logic.
        /// </summary>
        All
    }

    /// <summary>
    /// Creates an empty matcher.
    /// </summary>
    public TagMatcher() { }

    /// <summary>
    /// Creates a matcher with the given include tag-set.
    /// </summary>
    /// <param name="includeTags">The include tags value.</param>
    public TagMatcher(TagSet includeTags) : this()
    {
        AddInclude(includeTags);
    }

    /// <summary>
    /// Creates a matcher with include and exclude tag-sets.
    /// </summary>
    /// <param name="includeTags">The include tags value.</param>
    /// <param name="excludeTags">The exclude tags value.</param>
    public TagMatcher(TagSet includeTags, TagSet excludeTags = default) : this()
    {
        AddInclude(includeTags);
        AddExclude(excludeTags);
    }

    /// <summary>
    /// Adds a tag-set that a collectible must match (at least one tag) to be included.
    /// Multiple include sets are combined according to <see cref="MatchMode" />.
    /// </summary>
    /// <param name="tags">The tags value.</param>
    /// <returns>The add include.</returns>
    public TagMatcher AddInclude(TagSet tags)
    {
        _includeAny.Add(tags);
        return this;
    }

    /// <summary>
    /// Adds a tag-set that excludes any collectible matching at least one tag.
    /// </summary>
    /// <param name="tags">The tags value.</param>
    /// <returns>The add exclude.</returns>
    public TagMatcher AddExclude(TagSet tags)
    {
        _excludeAny.Add(tags);
        return this;
    }

    /// <summary>
    /// Adds a code pattern (e.g. <c>"game:ingot-*"</c>) that a collectible must match.
    /// Multiple patterns are OR-combined: matching any one is sufficient.
    /// If no patterns are added, code matching is skipped.
    /// </summary>
    /// <param name="pattern">The pattern value.</param>
    /// <returns>The add code pattern.</returns>
    public TagMatcher AddCodePattern(string pattern)
    {
        if (!string.IsNullOrWhiteSpace(pattern))
            _codePatterns.Add(pattern);
        return this;
    }

    /// <summary>
    /// Sets how multiple include tag-sets are combined (Any = OR, All = AND).
    /// </summary>
    /// <param name="mode">The mode value.</param>
    /// <returns>The set tag mode.</returns>
    public TagMatcher SetTagMode(MatchMode mode)
    {
        _tagMode = mode;
        return this;
    }

    /// <summary>
    /// Returns true if the collectible matches all configured criteria.
    /// </summary>
    /// <param name="collectible">The collectible value.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public bool Matches(CollectibleObject? collectible)
    {
        if (collectible == null) return false;

        // Code patterns: if any are configured, the collectible must match at least one.
        if (_codePatterns.Count > 0)
        {
            string? code = collectible.Code?.ToString();
            if (code == null) return false;

            bool codeMatch = false;
            foreach (var pattern in _codePatterns)
            {
                if (Helpers.CollectibleNameResolver.MatchesPattern(code, pattern))
                {
                    codeMatch = true;
                    break;
                }
            }
            if (!codeMatch) return false;
        }

        // Tag matching: if no include sets are configured, skip tag matching.
        if (_includeAny.Count == 0) return !MatchesAnyExclude(collectible);

        if (_tagMode == MatchMode.All)
        {
            foreach (var set in _includeAny)
            {
                if (!HasAnyTag(collectible, set)) return false;
            }
        }
        else
        {
            bool anyMatch = false;
            foreach (var set in _includeAny)
            {
                if (HasAnyTag(collectible, set))
                {
                    anyMatch = true;
                    break;
                }
            }
            if (!anyMatch) return false;
        }

        return !MatchesAnyExclude(collectible);
    }

    /// <summary>
    /// Returns true if the item stack's collectible matches all configured criteria.
    /// </summary>
    /// <param name="stack">The item stack.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public bool Matches(ItemStack? stack)
    {
        return Matches(stack?.Collectible);
    }

    /// <summary>
    /// Filters a sequence of collectibles, returning only those that match.
    /// </summary>
    /// <param name="collectibles">The collection of collectibles values.</param>
    /// <returns>A collection of filter values.</returns>
    public IEnumerable<CollectibleObject> Filter(IEnumerable<CollectibleObject> collectibles)
    {
        if (collectibles == null) return Enumerable.Empty<CollectibleObject>();
        return collectibles.Where(Matches);
    }

    /// <summary>
    /// Filters a sequence of item stacks, returning only those whose collectible matches.
    /// </summary>
    /// <param name="stacks">The item stack.</param>
    /// <returns>A collection of filter stacks values.</returns>
    public IEnumerable<ItemStack> FilterStacks(IEnumerable<ItemStack> stacks)
    {
        if (stacks == null) return Enumerable.Empty<ItemStack>();
        return stacks.Where(Matches);
    }

    /// <summary>
    /// Returns true if the collectible has at least one tag from the given set.
    /// </summary>
    /// <param name="collectible">The collectible value.</param>
    /// <param name="tags">The tags value.</param>
    /// <returns>true if the operation has any tag; otherwise, false.</returns>
    private static bool HasAnyTag(CollectibleObject collectible, TagSet tags)
    {
        if (collectible == null) return false;
        return collectible.Tags.Overlaps(in tags);
    }

    private bool MatchesAnyExclude(CollectibleObject collectible)
    {
        foreach (var set in _excludeAny)
        {
            if (HasAnyTag(collectible, set)) return true;
        }
        return false;
    }
}
