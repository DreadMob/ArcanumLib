---
layout: default
title: CustomTabContent
nav_order: 13
parent: Arcanum GUI Toolkit
---

# GuiElementCustomTabContent

## What is it for?

`GuiElementCustomTabContent` is a Cairo-rendered scrollable content element for custom info tabs. It renders sections with decorative icons, section headers, entry bullets, and wrapped text — all drawn via Cairo for a polished look. Decoration is data-driven: the tab data carries a `decorPrefix` that controls which icon style to use.

## When to use it

- You need a scrollable, richly-formatted info panel with sections and entries.
- You want Cairo-drawn decorative icons (dividers, bullets, stars) rather than plain text.
- You have data-driven tab content where sections and entries are built at runtime.
- You want consistent theming via `ArcanumGuiTheme` colours.

## Quick example

```csharp
using ArcanumLib.Gui.Controls;

var tabData = new CustomTabData
{
    tabNameKey = "mydomain:info-tab-title",
    decorPrefix = "mydomain",
    sections = new List<CustomTabSection>
    {
        new CustomTabSection
        {
            titleKey = "mydomain:section-about",
            introKey = "mydomain:section-about-intro",
            entries = new List<CustomTabEntry>
            {
                new CustomTabEntry
                {
                    nameKey = "mydomain:entry-1-name",
                    descKey = "mydomain:entry-1-desc",
                    isActive = true
                }
            }
        }
    }
};

var contentEl = new GuiElementCustomTabContent(capi, contentBounds, tabData);
composer.AddInteractiveElement(contentEl, "customtab");
```

## API overview

### CustomTabData

ProtoContract-serializable data model for a tab.

| Property | Type | Description |
|----------|------|-------------|
| `tabNameKey` | `string?` | Localization key or plain text for the tab name. |
| `sections` | `List<CustomTabSection>?` | Ordered content sections. |
| `decorPrefix` | `string?` | Optional prefix for decoration localization keys. If set, decorative icons are drawn. |

### CustomTabSection

| Property | Type | Description |
|----------|------|-------------|
| `titleKey` | `string?` | Section header text or lang key. |
| `introKey` | `string?` | Optional rich-text intro below the header. |
| `entries` | `List<CustomTabEntry>?` | Entries in this section. |

### CustomTabEntry

| Property | Type | Description |
|----------|------|-------------|
| `nameKey` | `string?` | Entry name text or lang key. |
| `descKey` | `string?` | Entry description text or lang key (wrapped). |
| `isActive` | `bool` | If true, the entry is highlighted with a star icon. |

### GuiElementCustomTabContent

```csharp
public GuiElementCustomTabContent(ICoreClientAPI capi, ElementBounds bounds, CustomTabData data)
```

| Method / Property | Description |
|-------------------|-------------|
| `SetScroll(double value)` | Set vertical scroll offset. |
| `UpdateScrollbar(ArcanumScrollbar sb)` | Sync a scrollbar's heights to the content. |
| `OnScroll` | `Action<float>?` callback fired when the mouse wheel scrolls content (0–1 range). |
| `TotalContentHeight` | Total measured content height in pixels. |
| `Resolver` | Static `Func<string, string>?` for resolving localization keys. If set, called for keys containing `:`. Falls back to `Lang.Get` if null. |

### Localization resolver

Consumers with custom localization systems should set `Resolver` at startup:

```csharp
GuiElementCustomTabContent.Resolver = MyLocalization.ResolveKeyOrText;
```

## Notes

- Text wrapping uses an estimate (~0.55 × font size per character) for height measurement; Cairo `TextExtents` is used for actual line breaking.
- Font tags (`<font>...</font>`) are stripped before measuring and drawing descriptions.
- The content texture is rendered as a single tall surface and clipped via scroll offset.
- Decoration icons are drawn by `CustomTabIconRenderer` from the `ArcanumLib.Gui.Icons` namespace.
