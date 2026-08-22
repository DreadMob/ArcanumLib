---
layout: default
title: TagSetExtensions
---

# TagSetExtensions

## What is it for?

`ArcanumLib.Data.TagSetExtensions` provides convenience helpers for `Vintagestory.API.Datastructures.TagSet` set operations. The Vintage Story `ITagRegistry<TagSet>` already provides the low-level API; these extensions add `Union`, `Intersect`, `Except`, and readable comparison aliases.

## When to use it

- You need set operations (`Union`, `Intersect`, `Except`) on `TagSet` values.
- You want readable subset, superset, equality, and overlap checks.
- You want to convert string tags into a `TagSet` in one call.
- You need to enumerate the tag names in a set.

## Quick example

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

## Usage

### Registry helpers

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
```

### Set comparison helpers

```csharp
    public static bool IsSubsetOf(this TagSet first, TagSet second);
    public static bool IsSupersetOf(this TagSet first, TagSet second);
    public static bool IsProperSubsetOf(this TagSet first, TagSet second);
    public static bool IsProperSupersetOf(this TagSet first, TagSet second);
    public static bool SetEquals(this TagSet first, TagSet second);
    public static bool Overlaps(this TagSet first, TagSet second);
}
```

| Method | Description |
| --- | --- |
| `ToTagSet` | Builds a `TagSet` from string tags using the registry. |
| `ToCollectibleTagSet` | Shortcut to build a `TagSet` from `api.CollectibleTagRegistry`. |
| `Union` | Returns the combined tags of two sets or a set plus raw tags. |
| `Intersect` | Returns the tags shared by two sets or a set plus raw tags. |
| `Except` | Returns the tags in the first set that are not in the second. |
| `GetNames` | Enumerates the tag names contained in a set. |
| `IsSubsetOf` / `IsSupersetOf` | Subset and superset checks. |
| `IsProperSubsetOf` / `IsProperSupersetOf` | Strict subset and superset checks. |
| `SetEquals` | Returns `true` when both sets contain the same tags. |
| `Overlaps` | Returns `true` when the sets share at least one tag. |

## Notes

- All set operations use the `ITagRegistry<TagSet>` to create new `TagSet` instances.
- `ToCollectibleTagSet` is a convenience helper on `ICoreAPI`.
- Methods that accept `params string[]` let you combine a set with raw tag names without first building a second `TagSet`.
