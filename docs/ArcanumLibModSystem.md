---
layout: default
title: ArcanumLibModSystem
nav_order: 6
---

# ArcanumLibModSystem

Central `ModSystem` that boots and shuts down ArcanumLib lifecycle: API registration, icon cache initialization, and service/clear cleanup on world unload.

## What is it for?

- Registers `ICoreAPI`, `ICoreServerAPI`, and `ICoreClientAPI` with `ArcanumServices`.
- Initializes `ImageIconCache` on the client.
- Disposes `ImageIconCache`, clears `CollectibleNameResolver`, and shuts down `ArcanumServices` on unload.

## Quick example

No consumer code is required. The system is auto-discovered by Vintage Story and loads early (`ExecuteOrder` = -1000).

## Notes

- Loads on both sides (`ShouldLoad` returns `true`).
- `ArcanumLibModSystem.Dispose` runs when the world unloads; it does not destroy per-world data stores itself, but clears the `ArcanumServices` registry, which lets services be garbage collected.
