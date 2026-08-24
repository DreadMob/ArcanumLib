---
layout: default
title: HUDs & Overlays
nav_order: 14
parent: Arcanum GUI Toolkit
---

# HUDs & Overlays

## What is it for?

`ArcanumLib.Gui.Hud` provides generic, reusable building blocks for in-game heads-up displays and short-lived overlays. Instead of writing boilerplate for every new HUD, consumer mods can focus on the snapshot data, theme, and any custom drawing.

## When to use it

- You need a client-side HUD panel driven by server snapshots.
- You want a short notification/toast overlay with slide/fade timing.
- You have a packet of icon entries to show (abilities, status effects, buffs).
- You want a client-system that handles snapshot syncing and dialog lifecycle automatically.

## Core types

| Type | Purpose |
|------|---------|
| `HudPanel<TSnapshot, THudDefinition, TTheme>` | Cairo-backed `GuiElement` that receives a snapshot and draws the panel. |
| `HudDialog<TSnapshot, THudDefinition, TTheme, TPanel>` | `GuiDialog` wrapper that owns the `HudPanel`. |
| `HudClientSystem<TSnapshot, THudDefinition, TTheme, TPanel, TDialog>` | Client `ModSystem` that loads definitions/themes, syncs snapshots, and opens/closes the HUD. |
| `HudSnapshotSync<TSnapshot>` | Base packet for snapshot messages. |
| `IHudSnapshot` | Marker for snapshot models. |
| `HudTheme` / `HudThemeResolver` | Theme loading and color/font/layout resolution. |
| `HudTextResolver` | Resolve text keys and localization. |
| `IHudElementRenderer` | Draw/measure a single element type inside a `HudPanel`. |
| `HudElementRendererRegistry` | Registry of `IHudElementRenderer` by element type string. |

## Transient overlays

`TransientOverlay<TModel>` is a generic `GuiDialog` for short-lived overlays such as milestone, combat, or achievement toasts.

```csharp
using ArcanumLib.Gui.Hud;

public class MyNotificationOverlay : TransientOverlay<NotificationModel>
{
    protected override string OverlayName => "mydomain:notification";
    protected override string DrawKey => "content";
    protected override float DurationSeconds => 6f;

    public MyNotificationOverlay(ICoreClientAPI capi) : base(capi) { }

    protected override void OnDrawContent(Context ctx, ImageSurface surface,
        ElementBounds currentBounds, float elapsed, NotificationModel? data)
    {
        // Draw card, icon, text, etc.
    }
}
```

To open:

```csharp
var overlay = new MyNotificationOverlay(capi);
overlay.Show(new NotificationModel { Title = "Ready!" });
```

`TransientOverlay` handles:

- slide in / hold / slide out timing using `DurationSeconds`
- per-frame `OnRenderGUI` and `Redraw`
- auto-close after the duration
- `OpenSound` property for an optional open sound

## Packet-driven icon HUDs

`PacketIconHud<TPacket, TEntry>` is a generic `HudElement` for panels that display rows of icon entries from a server packet.

```csharp
using ArcanumLib.Gui.Hud;

public class MyIconPacket : IHudPacket<MyIconEntry>
{
    public MyIconEntry[]? Entries { get; set; }
}

public class MyIconHud : PacketIconHud<MyIconPacket, MyIconEntry>
{
    protected override string HudName => "mydomain:myhud";

    public MyIconHud(ICoreClientAPI capi) : base(capi) { }

    protected override void PreloadIcon(MyIconEntry entry)
        => ImageIconCache.Preload(entry?.IconAsset);

    protected override void OnDrawHud(Context ctx, ImageSurface surface, ElementBounds currentBounds)
    {
        // Iterate _lastPacket.Entries and draw icons.
    }
}
```

The base class takes care of:

- packet updates via `UpdateFromPacket`
- preloading icon assets
- visibility and opening/closing
- periodic redrawing
- composing and disposing

## Implementing a custom `IHudElementRenderer`

For `HudPanel` element rendering, register a renderer by element type:

```csharp
public class TitleRenderer : IHudElementRenderer
{
    public string ElementType => "title";

    public double Draw(HudElementRenderArgs args)
    {
        var ctx = args.Context;
        var text = args.Snapshot.Title;
        // draw title
        return 24.0; // height consumed
    }

    public double MeasureHeight(HudElementMeasureArgs args) => 24.0;
    public double MeasureMinWidth(HudElementMeasureArgs args) => 80.0;
}

panel.Renderers.Register(new TitleRenderer());
```

## Quick start

1. Create a `TSnapshot` and a `THudDefinition`.
2. Derive `HudClientSystem<TSnapshot, THudDefinition, TTheme, TPanel, TDialog>`.
3. Implement `HudPanel<TSnapshot, THudDefinition, TTheme>` and draw the content.
4. Load theme assets from `config/gui/hud-themes` or use the built-in theme resolver.
5. Broadcast snapshots via the client-system's registered network channel.
