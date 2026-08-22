# ArcanumLib Documentation

This folder contains API documentation for `ArcanumLib` — a shared Vintage Story modding library for client-side GUI rendering, color manipulation, geometry, caching, and other cross-mod utilities.

## Available APIs

- [ImageIconCache](ImageIconCache.md) — load, cache and draw icon image surfaces with clipping and tinting.
- [RGBA](RGBA.md) — lightweight Cairo-friendly color struct.
- [Arcanum GUI Toolkit](ArcanumGui.md) — `ArcanumGuiTheme`, controls, layout helpers and dialog base.
- [ModeIconBuilder](ModeIconBuilder.md) — factory for tool-mode and skill-bar `SkillItem` icons.
- [ShapeCloner](ShapeCloner.md) — deep-clones Vintage Story `Shape` objects for safe mutation.
- [TimedCache](TimedCache.md) — thread-safe cache with TTL eviction and optional size limit.
- [TagSetExtensions](TagSetExtensions.md) — set operations for `Vintagestory.API.Datastructures.TagSet`.
- [ValidationResult](ValidationResult.md) — accumulate errors and warnings from validation pipelines.
- [ModAssetLoader](ModAssetLoader.md) — load, merge and override JSON assets from multiple mods.
- [ModAssetRegistry](ModAssetRegistry.md) — build validated, keyed, source-tracked registries from JSON assets.
- [WeightedRandom](WeightedRandom.md) — weighted random picks and reusable weighted tables.
- [DeferredWork](DeferredWork.md) — game-tick scheduler for one-shot, coalesced and end-of-tick work.
- [CooldownTracker](CooldownTracker.md) — per-entity cooldown timestamps in `WatchedAttributes`.
- [WatchedAttributes](WatchedAttributes.md) — get-or-create, set-if-missing, and set-and-mark-dirty helpers for `ITreeAttribute`.
- [EventScope](EventScope.md) — disposable event subscription scope with automatic unsubscription.
- [ApiExtensions](ApiExtensions.md) — `IsClient` / `IsServer` helpers for `ICoreAPI` and `IWorldAccessor`.
- [EntityHealthExtensions](EntityHealthExtensions.md) — read and scale entity health.
- [PlayerExtensions](PlayerExtensions.md) — `IPlayer` / `IServerPlayer` helpers.
- [LoggerExtensions](LoggerExtensions.md) — non-critical warning logging and `SafeExecute`.
- [CleanupScope](CleanupScope.md) — cancel deferred work, listeners, and nested disposables in one place.
- [Inventory / ItemStack helpers](InventoryHelpers.md) — give, count, find, and consume items.
- [TypedNetworkChannel](TypedNetworkChannel.md) — typed network channel wrapper for send/receive.

## Target Framework

- .NET 10
- Vintage Story 1.22.1+
