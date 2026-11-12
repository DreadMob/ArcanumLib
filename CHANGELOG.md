# Changelog

All notable changes to ArcanumLib are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- `ArcanumLib.Common.MachineClaims.MayWork` — land-claim test for machines working in a tick path (quiet `TestAccess`, the refusal told to the driver/owner at most every 4 s; owner-offline block entities only in unclaimed land and own claims). Moved from AlegacySteamworks.
- `ArcanumLib.Rendering.ChunkItemMeshes` / `BlockAtlasItemSource` — stack meshes for block-entity chunk meshes with block-atlas textures, cached by stack and handed out as copies. Moved from AlegacySteamworks; replaces ad-hoc texture sources in AlegacyCuriosities.
- `ArcanumLib.Entities.SeatExit` — stands someone getting up from a block seat or bed on the nearest free floor around the block's footprint. Moved from AlegacyCuriosities.
- `ArcanumLib.Entities.WheelClip` — a looping client clip whose eased speed follows a value, never stopped, survives server animation packets. Moved from AlegacySteamworks.
- `/arcanum transfer <from> <to> [confirm]` and `/arcanum transfer status` (`PlayerTransferCommands`) — the account progress transfer command, formerly the Quest Framework's `/avq datatransfer`; `PlayerDataTransfer.RegisterAfterTransfer` adds steps for data outside the world (website DB). The `/arcanum` root is shared via `GetOrCreate`.
- `ArcanumLib.Blocks.GroundPiles` — one-per-stack items with attribute `groundPile: { group, capacity, step, rows, fan, openOnDisplay }` pile up to 12 on one ground storage (flat with a little jitter, fanned, or as a log pile of rolls); a lone item with a `displayedShape` lies with its own shape. Replaces the pattern pile of AlegacyAttire and the `stackPile` of AlegacySteamworks (one patch set, filtered to ground storage on the shared `BlockEntityDisplay` mesh hooks; empty grown slots are dropped before a pit kiln takes the pile).
- Block behavior `arcanumlib.FacePlayer` (`BlockBehaviorFacePlayer`) — a horizontally orientable block turns its front to the player placing it.
- `IMultiBlockForwarding` / `IMultiBlockForwardingBoxes` (`ArcanumLib.Blocks`) — a multiblock controller block declares them and every click, hold, help line, pick, sound and box of its filler cells is handed to its own methods; `MultiblockCells.ForController` / `Shift` for custom ones.
- `ArcanumLib.Entities.RiderPoses.Keep` — restarts a seat's suggested rider pose when the game has dropped it (vanilla starts it only once on mounting).
- `ArcanumLib.Blocks.MultiblockReach` — vanilla multiblock filler blocks reach 5 cells sideways and 8 up (vanilla: 2 and 3), so dependent mods need no multiblock patch of their own; offsets other mods patch in again are merged, not repeated.
- `ArcanumSlider.ReadoutWidth` — keeps the value readout beside the track instead of above its right end, so it never sits on the track.
- `ArcanumLib.Blocks.Clock` — data-driven clock (`BlockClock`, `BlockEntityClock`, `ClockRenderer`, chime-hours dialog): hands per dial, pendulums/gears, chime, new-year melody, storm warning, client-side ticking and a winding sound, all from the blocktype's `attributes.clock` (`ClockConfig`). Mods register the classes under their own names.
- `GlyphText` — draws symbols the GUI fonts lack (★ ⚙ ● ○ ∞ ✓ ✕ −) as Cairo shapes inline with text; `ArcanumList`, `ArcanumDropdown` and `ArcanumButton` labels use it.
- `ArcanumLib.Gui.Map`: `ChunkTerrainSource` (client terrain thumbnails per chunk — vanilla world-map cache DB + live chunks, vanilla colour table) and `GuiElementChunkGrid` (`AddChunkGrid`) — zoomable/pannable chunk map with selection, overlay painters and a player marker.
- Unit test project (`ArcanumLib.Tests`) and Atlas integration test project (`ArcanumLib.AtlasTests`) covering `ArcanumServices`, `EventBus`, `PityTracker`, `PityDefinition`, `WeightedRandom`, `LootTable`, `TimedCache`, `TagMatcher`, `ItemCharge`, `Wildcard`, `ValidationResult`, `ModDataStore`, `WatchedAttributesExtensions`, `EffectResistanceStore`, `StatusEffectService`, `CooldownTracker`, `InventoryFingerprint`, `ChatFormatUtil`, `PlaytimeTracker`, `PlaytimeCooldownManager`, `DamageHelper`, `EntityHealthExtensions`, `EntityControlExtensions`, `CleanupScope`, `EventScope`, `BlockEntitySearchUtils`, `CollectibleNameResolver`, `PositionUtils`, `OnlinePlayerCache` and `PlayerProximityTracker`.
- Atlas headless server scenarios for mod load, `OnlinePlayerCache`, `CooldownTracker`, `TagMatcher` and `PlayerProximityTracker`.
- Dedicated `ArcanumLib.AtlasFixture` project to author a small fixture world (`fixtures/world.vcdbs`) via `atlas fixture`.
- `ArcanumServiceScope` for explicit client/server/world service registration in `ArcanumServices`.
- `ArcanumServices.ScopeFor(ICoreAPI)` helper.
- `TypedNetworkChannel.SendToPlayers<T>` and `TypedNetworkChannel.SendToAllExcept<T>` for targeted server-side packet delivery.
- `IDeferredWork` interface and `DeferredWork.Client`/`DeferredWork.Server` scopes.
- `docs/GettingStarted.md` with copy-paste examples for third-party mods.
- `CHANGELOG.md`.
- `PaletteScope` and `ArcanumGuiTheme.WithPalette(...)` for scoped, restore-on-dispose palette overrides.
- `ArcanumHotkeyService` (`ArcanumHotkeyService.For(api)`) — shared per-side hotkey registry wrapping `RegisterHotKey`/`SetHotKeyHandler` with key-combination parsing and clean unregistration.
- `ArcanumChannelHub` — shared `TypedNetworkChannel` registry keyed by (side, channel name) so mods share one channel registration.
- `ArcanumTexturePool` — shared `LoadedTexture` pool keyed by caller-supplied string, so identical baked surfaces are uploaded once.

### Changed

- Documentation brought up to the code: new pages for blocks (FacePlayer, multiblock forwarding/reach, ground piles), the clock, vehicles and rider poses, player animations, account transfer, player trackers, GUI controls, rendering helpers, packet security, hotkeys and chat announcements; stale API in InventoryHelpers, ModConfig, ItemMode, TagMatcher, Diagnostics, ModDataStore and PityTracker fixed; pages whose navigation parent no longer existed are reachable again.
- Consolidated multiple `ModSystem` classes into `ArcanumDataModSystem`, `ArcanumPerformanceModSystem`, and `ArcanumLibModSystem`.
- `ArcanumLibModSystem` now registers client and server APIs under `ArcanumServiceScope.Client` and `ArcanumServiceScope.Server`.
- `PlaytimeTracker.Current` and `PityTracker.Current` now use `ArcanumServiceScope.Server`.
- `DeferredWork` now keeps separate client and server schedulers, routing static calls by thread with a fallback.
- `OnlinePlayerCache` now returns immutable snapshots (`All`, `ByUid`) and uses a snapshot publication pattern for lock-free reads.
- `InventoryChangeTracker` now implements `IDisposable` and protects its cache under a single lock.
- `CooldownTracker` now synchronizes all watched-attribute operations with a shared lock.
- `ArcanumServices` now supports per-scope `Register`, `Unregister`, `Get`, `TryGet`, `EnsureInitialized`, and `Shutdown`.
- Idle subsystems no longer tick: `StatusEffectModSystem` ticks only while some entity has an effect, `PlayerZoneTracker` (and so `PlayerProximityTracker`) only while a zone is registered, `PlayerInLandTracker` only while one of its events has a subscriber. `/arcanum diagnose` reports a missing action registry/executor as a warning, not a failure.

### Fixed

- Singleplayer: `StatusEffectModSystem` shared one Global `StatusEffectService` between the client and the server, so effects ticked on both sides (twice as fast) and a client-side despawn could clear server effects. Each side now owns its service in its own scope.
- Singleplayer: `EventBusService.PublishAsync` queued handlers on the main thread of whichever API was registered last (often the client's for a server bus); each bus now knows its side. `CleanupScope` and `StatCoalescingEngine` cancel deferred work on their own side, `StatusEffectService` reads the resistance service of the entity's side.
- World unload now empties the static registries that held the old world's state: `GuiItemFit` outlines (keyed by server-assigned item ids: wrong icon sizes on the next server), `GuiOccluders`, `ArcanumChannelHub` wrappers (old api), `ChatAnnounce.Handler`, the `BlockEntityAccessGate` denial stamps (clock restarts at 0) and, on the client, `ArcanumTexturePool`. `PlayerDataTransfer` is emptied when the server stops, so a second world in one process no longer runs every participant twice on dead objects. The static events of `PlayerZoneTracker` and `PlayerInLandTracker` are emptied with the world too.
- `ArcanumCullFaces` items on shelves and in display cases: the display mesh patch existed but was never applied. It is now, and it only moves no-cull faces to the opaque pass instead of overwriting every pass and the extra tesselator bits.
- Drivable step-up: the hop keeps a minimum forward speed and does not brake for its whole duration, so slow-accelerating vehicles clear the step instead of falling back.
- `ArcanumCheckbox` ignored every click: `OnMouseUp` cleared the pressed flag before the base call routed to `OnMouseUpOnElement`, which needs it.
- `ArcanumDropdown` menu opened at the wrong spot and oversized at GUI scale ≠ 1 (screen px were passed to `ElementBounds.Fixed` and scaled twice).
- `ModConfig.Save()` passed `StoreModConfig` arguments in the wrong order and never wrote the config file.
- `OnlinePlayerCache` now also removes players on `PlayerDisconnect` (kicks and timeouts), not just `PlayerLeave`.
- `LootTable.Roll` now returns `default` when total effective weight is zero, matching its documented contract.
- `PlayerProximityTracker` null-warning for `Position.dimension` resolved.
- `OnlinePlayerCache` no longer exposes unsynchronized mutable collections.
- `TypedNetworkChannel.Send<T>` no longer silently broadcasts to an empty player list.
- `ModDataStoreInstance` data/loading/dirty state is now protected by `ReaderWriterLockSlim`.
- `StatusEffectService` operations are now synchronized with a service-level lock.
- `ModDataStore` migration now passes `JToken` directly instead of re-serializing.
- `ItemCharge` legacy suffix handling simplified.

## [1.0.0-rc1] - 2025-XX-XX

### Added

- Initial shared library for Vintage Story mods.
- Status effect system with stacking, refresh, override, and independent modes.
- `ModDataStore` for versioned per-savegame JSON persistence.
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
