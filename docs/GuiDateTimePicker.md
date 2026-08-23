---
layout: default
title: GuiDateTimePicker
nav_order: 14
parent: Arcanum GUI Toolkit
---

# GuiDateTimePicker

## What is it for?

`GuiDateTimePicker` is a static helper class that composes a date/time picker into any `GuiComposer`. It provides day/month/year/hour/minute inputs with Now and Clear buttons, and helper methods to set and get the value as a string.

## When to use it

- You need a date/time input in a GUI dialog (e.g. scheduling, validity periods, expiry).
- You want a compact, pre-built picker without manually laying out five number inputs and two buttons.
- You need to serialize the selected date/time to a string format.

## Quick example

```csharp
using ArcanumLib.Gui.Controls;

// Inside a dialog's ComposeElements method:
double nextY = GuiDateTimePicker.Compose(
    SingleComposer,
    "mydomain:label-valid-from",  // title lang key or plain text
    "validFrom",                   // unique prefix for this picker's inputs
    x: 0,
    y: currentY,
    width: 300,
    nowLangKey: "mydomain:btn-now",
    clearLangKey: "mydomain:btn-clear");

// Later, to read the value:
string? dateStr = GuiDateTimePicker.GetDate(SingleComposer, "validFrom");
// dateStr is "yyyy-MM-dd HH:mm" or null if no year is set.

// To set the value:
GuiDateTimePicker.SetDate(SingleComposer, "validFrom", "2025-03-15 14:30");
```

## API overview

### Compose

```csharp
public static double Compose(
    GuiComposer composer,
    string titleLangCode,
    string prefix,
    double x, double y, double width,
    string nowLangKey = "now",
    string clearLangKey = "clear")
```

Composes the picker and returns the Y position after the picker for chaining.

The `prefix` must be unique within the composer — it is used to generate element keys (`{prefix}-day`, `{prefix}-month`, etc.).

### SetDate

```csharp
public static void SetDate(GuiComposer composer, string prefix, string? value)
```

Sets the picker from a string in `yyyy-MM-dd HH:mm` format. If the value is null or unparseable, the picker is cleared.

### GetDate

```csharp
public static string? GetDate(GuiComposer composer, string prefix)
```

Returns the date/time as `yyyy-MM-dd HH:mm` (UTC), or null if no year is set.

## Notes

- All number inputs are set to `IntMode`.
- Month is a dropdown (1–12).
- The Now button sets the current UTC time.
- The Clear button resets all fields to zero.
- Lang keys for the Now/Clear buttons are parameterized so consumers can use their own localization domain.
