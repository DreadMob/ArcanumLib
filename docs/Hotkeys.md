---
layout: default
title: Hotkeys
parent: "Misc Helpers"
nav_order: 20
---

# Hotkeys

`ArcanumHotkeyService` (`ArcanumLib.Common`) — a per-side registry of named hotkeys over `RegisterHotKey` / `SetHotKeyHandler`, with key-combination parsing and real unregistration (the game has no public API to remove a hotkey).

```csharp
var keys = ArcanumHotkeyService.For(capi);          // shared per side, disposed with the runtime

keys.Register("mymod-mainmenu", "Ctrl+K", "Open My Mod menu", () => dialog.Toggle());
keys.Register("mymod-quick", "shift+f5", "Quick action", DoQuick, HotkeyType.CharacterControls);

keys.IsRegistered("mymod-quick");
keys.Unregister("mymod-quick");
keys.UnregisterAll();
```

- Combinations: modifier tokens `ctrl`/`control`, `alt`, `shift` plus a `GlKeys` name, separated by `+`, spaces, commas or semicolons, any case.
- `Register` returns `false` when the combination cannot be parsed or the game rejects it; registering an id again updates it. The handler marks the keypress as consumed.
- On a server API the service only keeps the bookkeeping (so common code can call it), handlers never fire.
- `ArcanumHotkeyService.Get(api)` returns the shared instance without creating one.
