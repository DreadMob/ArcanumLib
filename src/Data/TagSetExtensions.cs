using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.Data;

/// <summary>
/// Convenience helpers for <see cref="TagSet"/> manipulation. The Vintage Story
/// <see cref="ITagRegistry{TagSet}"/> already provides the heavy lifting; this
/// class adds set-style operations and readable aliases.
/// </summary>
public static class TagSetExtensions
{
    // =====================================================================
    // Creation
    // =====================================================================

    /// <summary>
    /// Creates a <see cref="TagSet"/> from the given tag names.
    /// </summary>
    public static TagSet ToTagSet(this ITagRegistry<TagSet> registry, params string[] tags)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        return registry.CreateTagSet(tags);
    }

    /// <summary>
    /// Creates a <see cref="TagSet"/> from the given tag names.
    /// </summary>
    public static TagSet ToTagSet(this ITagRegistry<TagSet> registry, IEnumerable<string> tags)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        return registry.CreateTagSet(tags);
    }

    /// <summary>
    /// Convenience wrapper around <see cref="ICoreAPI.CollectibleTagRegistry"/>.
    /// </summary>
    public static TagSet ToCollectibleTagSet(this ICoreAPI api, params string[] tags)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        return api.CollectibleTagRegistry.CreateTagSet(tags);
    }

    /// <summary>
    /// Convenience wrapper around <see cref="ICoreAPI.CollectibleTagRegistry"/>.
    /// </summary>
    public static TagSet ToCollectibleTagSet(this ICoreAPI api, IEnumerable<string> tags)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));
        return api.CollectibleTagRegistry.CreateTagSet(tags);
    }

    // =====================================================================
    // Set operations on ITagRegistry<TagSet>
    // =====================================================================

    /// <summary>
    /// Returns the union of two tag sets.
    /// </summary>
    public static TagSet Union(this ITagRegistry<TagSet> registry, TagSet first, TagSet second)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        return registry.CreateTagSet(GetNames(registry, first).Concat(GetNames(registry, second)).Distinct());
    }

    /// <summary>
    /// Returns the union of a tag set and one or more tag names.
    /// </summary>
    public static TagSet Union(this ITagRegistry<TagSet> registry, TagSet first, params string[] tags)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        return registry.CreateTagSet(GetNames(registry, first).Concat(tags).Distinct());
    }

    /// <summary>
    /// Returns the intersection of two tag sets.
    /// </summary>
    public static TagSet Intersect(this ITagRegistry<TagSet> registry, TagSet first, TagSet second)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        var secondSet = new HashSet<string>(GetNames(registry, second));
        return registry.CreateTagSet(GetNames(registry, first).Where(secondSet.Contains));
    }

    /// <summary>
    /// Returns the intersection of a tag set and one or more tag names.
    /// </summary>
    public static TagSet Intersect(this ITagRegistry<TagSet> registry, TagSet first, params string[] tags)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        var secondSet = new HashSet<string>(tags);
        return registry.CreateTagSet(GetNames(registry, first).Where(secondSet.Contains));
    }

    /// <summary>
    /// Returns the set difference (first minus second).
    /// </summary>
    public static TagSet Except(this ITagRegistry<TagSet> registry, TagSet first, TagSet second)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        var secondSet = new HashSet<string>(GetNames(registry, second));
        return registry.CreateTagSet(GetNames(registry, first).Where(n => !secondSet.Contains(n)));
    }

    /// <summary>
    /// Returns the set difference of a tag set minus one or more tag names.
    /// </summary>
    public static TagSet Except(this ITagRegistry<TagSet> registry, TagSet first, params string[] tags)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        var secondSet = new HashSet<string>(tags);
        return registry.CreateTagSet(GetNames(registry, first).Where(n => !secondSet.Contains(n)));
    }

    /// <summary>
    /// Enumerates the tag names contained in the given set.
    /// </summary>
    public static IEnumerable<string> GetNames(this ITagRegistry<TagSet> registry, TagSet set)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        return registry.SlowEnumerateTagNames(set);
    }

    // =====================================================================
    // Set comparisons on TagSet itself
    // =====================================================================

    /// <summary>
    /// Returns true if all tags in <paramref name="first"/> are also in <paramref name="second"/>.
    /// </summary>
    public static bool IsSubsetOf(this TagSet first, TagSet second) => first.IsFullyContainedIn(second);

    /// <summary>
    /// Returns true if all tags in <paramref name="second"/> are also in <paramref name="first"/>.
    /// </summary>
    public static bool IsSupersetOf(this TagSet first, TagSet second) => second.IsFullyContainedIn(first);

    /// <summary>
    /// Returns true if <paramref name="first"/> is a strict subset of <paramref name="second"/>.
    /// </summary>
    public static bool IsProperSubsetOf(this TagSet first, TagSet second)
        => first.IsFullyContainedIn(second) && first != second;

    /// <summary>
    /// Returns true if <paramref name="first"/> is a strict superset of <paramref name="second"/>.
    /// </summary>
    public static bool IsProperSupersetOf(this TagSet first, TagSet second)
        => second.IsFullyContainedIn(first) && first != second;

    /// <summary>
    /// Returns true if the two sets contain exactly the same tags.
    /// </summary>
    public static bool SetEquals(this TagSet first, TagSet second) => first == second;

    /// <summary>
    /// Returns true if <paramref name="first"/> and <paramref name="second"/> share at least one tag.
    /// </summary>
    public static bool Overlaps(this TagSet first, TagSet second) => first.Overlaps(second);
}
