---
layout: default
title: TagSetExtensions
---

# TagSetExtensions

Convenience helpers for `Vintagestory.API.Datastructures.TagSet` set operations.
The Vintage Story `ITagRegistry<TagSet>` already provides the low-level API;
these extensions add `Union`, `Intersect`, `Except`, and readable comparison
aliases.

## Usage

```csharp
using ArcanumLib.Data;
using Vintagestory.API.Datastructures;

ITagRegistry<TagSet> registry = api.CollectibleTagRegistry;

TagSet a = registry.ToTagSet("humanoid", "player");
TagSet b = registry.ToTagSet("player", "hunter");

TagSet union = registry.Union(a, b);
TagSet withExtra = registry.Union(a, "warrior");
TagSet intersection = registry.Intersect(a, b);
TagSet without = registry.Except(a, b);

bool sub = a.IsSubsetOf(b);
bool super = a.IsSupersetOf(b);
bool overlap = a.Overlaps(b);
bool equal = a.SetEquals(b);

IEnumerable<string> names = registry.GetNames(a);
```

## API

```csharp
public static class TagSetExtensions
{
    public static TagSet ToTagSet(this ITagRegistry<TagSet> registry, params string[] tags);
    public static TagSet ToCollectibleTagSet(this ICoreAPI api, params string[] tags);

    public static TagSet Union(this ITagRegistry<TagSet> registry, TagSet first, TagSet second);
    public static TagSet Union(this ITagRegistry<TagSet> registry, TagSet first, params string[] tags);

    public static TagSet Intersect(this ITagRegistry<TagSet> registry, TagSet first, TagSet second);
    public static TagSet Intersect(this ITagRegistry<TagSet> registry, TagSet first, params string[] tags);

    public static TagSet Except(this ITagRegistry<TagSet> registry, TagSet first, TagSet second);
    public static TagSet Except(this ITagRegistry<TagSet> registry, TagSet first, params string[] tags);

    public static IEnumerable<string> GetNames(this ITagRegistry<TagSet> registry, TagSet set);

    public static bool IsSubsetOf(this TagSet first, TagSet second);
    public static bool IsSupersetOf(this TagSet first, TagSet second);
    public static bool IsProperSubsetOf(this TagSet first, TagSet second);
    public static bool IsProperSupersetOf(this TagSet first, TagSet second);
    public static bool SetEquals(this TagSet first, TagSet second);
    public static bool Overlaps(this TagSet first, TagSet second);
}
```
