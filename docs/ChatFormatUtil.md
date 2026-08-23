---
layout: default
title: ChatFormatUtil
nav_order: 41
parent: Common & Utility
---

# ChatFormatUtil

## What is it for?

`ChatFormatUtil` provides helpers for formatting chat and HUD text with Vintage Story `<font color="...">` tags.

## When to use it

- You need to colorize chat messages or HUD text.
- You want alert-prefixed messages with consistent styling.

## Quick example

```csharp
using ArcanumLib.Common;

// Colorize text
string msg = ChatFormatUtil.Font("Hello!", "#4ADE80");

// Alert prefix: red [!] + white text
string alert = ChatFormatUtil.PrefixAlert("Boss defeated!");

// Custom colors
string custom = ChatFormatUtil.PrefixAlert("Warning", "#ff5555", "#fbbf24");

// Custom prefix and colors
string full = ChatFormatUtil.PrefixAlert("Warning", "[?] ", "#fbbf24", "#ffffff");
```

## API overview

| Method | Description |
|--------|-------------|
| `Font(text, hexColor)` | Wraps text in a `<font color="...">` tag. |
| `PrefixAlert(text)` | Default alert: red `[!] ` prefix + white text. |
| `PrefixAlert(text, prefixColor, textColor)` | Custom colors, default `[!] ` prefix. |
| `PrefixAlert(text, prefix, prefixColor, textColor)` | Fully custom prefix and colors. |
