# Changelog

All notable changes to ArcanumLib are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Unit test project (`ArcanumLib.Tests`) and Atlas integration test project (`ArcanumLib.AtlasTests`) covering `ArcanumServices`, `CommandArgs`, `EventBus`, `PityTracker`, `PityDefinition`, `WeightedRandom`, `LootTable`, `TimedCache`, `TagMatcher`, `PositionUtils`, and `OnlinePlayerCache`.
- Atlas headless server scenarios for mod load, `OnlinePlayerCache`, `CooldownTracker`, and `TagMatcher`.
- `ArcanumServiceScope` for explicit client/server/world service registration in `ArcanumServices`.
- `ArcanumServices.ScopeFor(ICoreAPI)` helper.
- `TypedNetworkChannel.SendToPlayers<T>` and `TypedNetworkChannel.SendToAllExcept<T>` for targeted server-side packet delivery.
- `IDeferredWork` interface and `DeferredWork.Client`/`DeferredWork.Server` scopes.
- `docs/GettingStarted.md` with copy-paste examples for third-party mods.
- `CHANGELOG.md`.

### Changed

- Consolidated multiple `ModSystem` classes into `ArcanumDataModSystem`, `ArcanumPerformanceModSystem`, and `ArcanumLibModSystem`.
- `ArcanumLibModSystem` now registers client and server APIs under `ArcanumServiceScope.Client` and `ArcanumServiceScope.Server`.
- `PlaytimeTracker.Current` and `PityTracker.Current` now use `ArcanumServiceScope.Server`.
- `DeferredWork` now keeps separate client and server schedulers, routing static calls by thread with a fallback.
- `OnlinePlayerCache` now returns immutable snapshots (`All`, `ByUid`) and uses a snapshot publication pattern for lock-free reads.
- `InventoryChangeTracker` now implements `IDisposable` and protects its cache under a single lock.
- `CooldownTracker` now synchronizes all watched-attribute operations with a shared lock.
- `ArcanumServices` now supports per-scope `Register`, `Unregister`, `Get`, `TryGet`, `EnsureInitialized`, and `Shutdown`.

### Fixed

- `OnlinePlayerCache` now also removes players on `PlayerDisconnect` (kicks and timeouts), not just `PlayerLeave`.
- `LootTable.Roll` now returns `default` when total effective weight is zero, matching its documented contract.
- `OnlinePlayerCache` no longer exposes unsynchronized mutable collections.
- `TypedNetworkChannel.Send<T>` no longer silently broadcasts to an empty player list.
- `ModDataStoreInstance` data/loading/dirty state is now protected by `ReaderWriterLockSlim`.
- `StatusEffectService` operations are now synchronized with a service-level lock.
- `ModDataStore` migration now passes `JToken` directly instead of re-serializing.
- `ItemCharge` legacy suffix handling simplified.
- `CommandBuilder` now uses Vintage Story argument parsers and supports autocomplete for word arguments.

## [1.0.0-rc1] - 2025-XX-XX

### Added

- Initial shared library for Vintage Story mods.
- Status effect system with stacking, refresh, override, and independent modes.
- `ModDataStore` for versioned per-savegame JSON persistence.
- `CommandBuilder` for fluent, typed, permission-gated commands.
- `TypedNetworkChannel` for typed client/server networking.
- `DeferredWork` for one-shot, coalesced, and end-of-tick scheduling.
- GUI toolkit (`ArcanumCard`, `ArcanumIcon`, `ArcanumButton`, `ArcanumList<T>`, etc.).
- Hologram renderers, custom icon registry, radial menus, and HUD helpers.
- `EventBus` for typed cross-mod publish/subscribe.
- `PlaytimeTracker`, `PityTracker`, `CooldownTracker`, and `InventoryChangeTracker`.
- `ItemCharge` and `ItemMode` helpers.
- `ArcanumServices` lightweight service registry.

[Unreleased]: https://github.com/dreadmob/arcanumlib/compare/v1.0.0-rc1...HEAD
[1.0.0-rc1]: https://github.com/dreadmob/arcanumlib/releases/tag/v1.0.0-rc1
