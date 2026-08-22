# Arcanum GUI Toolkit

`ArcanumLib.Gui` is a small toolkit for building Vintage Story GUIs with less boilerplate and a consistent look.

## Namespaces

- `ArcanumLib.Gui.Theme` — the `ArcanumGuiTheme` colour palette, `RGBA`, `ArcanumFont` and Cairo drawing helpers.
- `ArcanumLib.Gui.Icons` — `ImageIconCache` for loading and drawing WebP/PNG icons.
- `ArcanumLib.Gui.Controls` — ready-to-use elements: `ArcanumIcon`, `ArcanumCard`, `ArcanumButton`, `ArcanumScrollbar`, `ArcanumDialogBackground`.
- `ArcanumLib.Gui.Layout` — `ArcanumLayout` helpers for vertical/horizontal stacks.
- `ArcanumLib.Gui.Dialogs` — `ArcanumGuiDialog` base class.

## Theme

```csharp
using ArcanumLib.Gui.Theme;

var fill = ArcanumGuiTheme.SurfaceCard;
var border = ArcanumGuiTheme.BorderDefault;
var textColor = ArcanumGuiTheme.TextPrimary;
```

## Fonts

```csharp
var title = ArcanumFont.Title;
var body = ArcanumFont.Body;
var caption = ArcanumFont.Caption;
```

## Layout

Instead of manually computing `ElementBounds.Fixed` coordinates:

```csharp
var rows = ArcanumLayout.VerticalFill(bgBounds, gap: 12, padding: 20,
    headerH: 64,
    bodyH: 120,
    footerH: 36);

var headerBounds = rows[0];
var bodyBounds = rows[1];
var footerBounds = rows[2];
```

## Icon

```csharp
var iconBounds = ElementBounds.Fixed(0, 0, 48, 48);
composer.AddArcanumIcon(
    "albase:textures/icons/bossdebuff.webp",
    iconBounds,
    color: ArcanumGuiTheme.TextPrimary,
    fit: IconFit.Circle);
```

## Card

```csharp
composer.AddArcanumCard(
    bodyBounds,
    fill: ArcanumGuiTheme.SurfaceCard.WithAlpha(0.45),
    border: ArcanumGuiTheme.BorderShadow.WithAlpha(0.55),
    accent: ArcanumGuiTheme.StatusActive);
```

## Dialog base

```csharp
public class MyDialog : ArcanumGuiDialog
{
    protected override void BuildComposer()
    {
        var bgBounds = ArcanumGuiTheme.ArcanumConfigBackgroundBounds();
        var bounds = ArcanumGuiTheme.ArcanumConfigDialogBounds();

        SingleComposer = capi.Gui.CreateCompo("MyDialog", bounds)
            .AddArcanumDialogBackground(bgBounds)
            .AddDialogTitleBar("My Title", TryClose)
            .BeginChildElements(bgBounds)
            .AddArcanumCard(bodyBounds)
            .EndChildElements();
    }
}
```
