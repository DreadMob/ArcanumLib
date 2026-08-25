using System;
using System.Collections.Generic;
using ArcanumLib.Gui.Controls;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.Theme;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Gui.Layout;

/// <summary>
/// Fluent, stack-based builder for Arcanum-themed Vintage Story GUIs.
/// Tracks a cursor and nesting so you can write dialog composition without
/// manual <see cref="ElementBounds.Fixed(double, double, double, double)" /> arithmetic.
/// </summary>
public class ArcanumComposer
{
    private readonly Stack<Container> _containers = new();

    /// <summary>
    /// The client API passed to the underlying <see cref="GuiComposer" />.
    /// </summary>
    public ICoreClientAPI Api { get; }

    /// <summary>
    /// The wrapped <see cref="GuiComposer" />.
    /// </summary>
    public GuiComposer Underlying { get; }

    /// <summary>
    /// The dialog key used to create the <see cref="GuiComposer" />.
    /// </summary>
    public string DialogKey { get; }

    /// <summary>
    /// Default content width for elements that fill horizontally.
    /// </summary>
    public double DefaultItemWidth { get; set; }

    /// <summary>
    /// Default height for buttons and other fixed-height rows.
    /// </summary>
    public double DefaultRowHeight { get; set; } = 36.0;

    /// <summary>
    /// Default height for <see cref="AddList{TItem}" /> when not specified.
    /// </summary>
    public double DefaultListHeight { get; set; } = 240.0;

    /// <summary>
    /// Default height for text/number inputs.
    /// </summary>
    public double DefaultInputHeight { get; set; } = 30.0;

    /// <summary>
    /// Minimum height for text labels.
    /// </summary>
    public double DefaultTextHeight { get; set; } = 22.0;

    /// <summary>
    /// Creates an Arcanum composer with the given dialog and background bounds.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="dialogKey">The dialog key value.</param>
    /// <param name="dialogBounds">The dialog bounds value.</param>
    /// <param name="backgroundBounds">The background bounds value.</param>
    /// <param name="defaultItemWidth">The default item width value.</param>
    private ArcanumComposer(
        ICoreClientAPI capi,
        string dialogKey,
        ElementBounds dialogBounds,
        ElementBounds backgroundBounds,
        double defaultItemWidth)
    {
        Api = capi;
        DialogKey = dialogKey;
        DefaultItemWidth = defaultItemWidth;
        Underlying = capi.Gui.CreateCompo(dialogKey, dialogBounds);

        var root = new Container(
            this,
            backgroundBounds,
            ContainerOrientation.Vertical,
            backgroundBounds.fixedPaddingX,
            0,
            null);
        _containers.Push(root);
    }

    /// <summary>
    /// Creates a new composer using the standard Arcanum dialog shell.
    /// </summary>
    /// <param name="capi">The client API instance.</param>
    /// <param name="dialogKey">The dialog key value.</param>
    /// <param name="dialogBounds">The dialog bounds value.</param>
    /// <param name="backgroundBounds">The background bounds value.</param>
    /// <param name="defaultItemWidth">The default item width value.</param>
    /// <returns>The value.</returns>
    public static ArcanumComposer Create(
        ICoreClientAPI capi,
        string dialogKey,
        ElementBounds? dialogBounds = null,
        ElementBounds? backgroundBounds = null,
        double defaultItemWidth = 440.0)
    {
        dialogBounds ??= ArcanumGuiTheme.ArcanumConfigDialogBounds();
        backgroundBounds ??= ArcanumGuiTheme.ArcanumConfigBackgroundBounds();
        return new ArcanumComposer(capi, dialogKey, dialogBounds, backgroundBounds, defaultItemWidth);
    }

    private Container Current => _containers.Peek();

    /// <summary>
    /// Adds an Arcanum dialog background and title bar, then opens the root child scope.
    /// </summary>
    /// <param name="title">The title value.</param>
    /// <param name="onClose">The on close value.</param>
    /// <returns>The with title bar.</returns>
    public ArcanumComposer WithTitleBar(string title, Action onClose)
    {
        Underlying
            .AddArcanumDialogBackground(Current.Bounds)
            .AddDialogTitleBar(title, onClose)
            .BeginChildElements(Current.Bounds);
        return this;
    }

    /// <summary>
    /// Adds a dialog background and opens the root child scope without a title bar.
    /// </summary>
    /// <returns>The with background.</returns>
    public ArcanumComposer WithBackground()
    {
        Underlying
            .AddArcanumDialogBackground(Current.Bounds)
            .BeginChildElements(Current.Bounds);
        return this;
    }

    /// <summary>
    /// Starts a vertical container. If <paramref name="height" /> is null, the container
    /// sizes itself to fit its children.
    /// </summary>
    /// <param name="padding">The padding value.</param>
    /// <param name="gap">The gap value.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The begin vertical.</returns>
    public ArcanumComposer BeginVertical(double padding = 0, double gap = 0, double? width = null, double? height = null)
    {
        double outerW = width ?? Current.AvailableWidth;
        double outerH = height ?? 0;
        double innerW = Math.Max(0, outerW - padding * 2);
        double innerH = Math.Max(0, outerH - padding * 2);

        var childBounds = Current.NextBounds(innerW, innerH)
            .WithFixedPadding(padding)
            .WithSizing(ElementSizing.Fixed, outerH > 0 ? ElementSizing.Fixed : ElementSizing.FitToChildren);

        var child = new Container(this, childBounds, ContainerOrientation.Vertical, padding, gap, Current);
        Underlying.BeginChildElements(childBounds);
        _containers.Push(child);
        return this;
    }

    /// <summary>
    /// Starts a horizontal container. If <paramref name="width" /> is null, the container
    /// sizes itself to fit its children.
    /// </summary>
    /// <param name="padding">The padding value.</param>
    /// <param name="gap">The gap value.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The begin horizontal.</returns>
    public ArcanumComposer BeginHorizontal(double padding = 0, double gap = 0, double? width = null, double? height = null)
    {
        double outerH = height ?? Current.AvailableHeight;
        double outerW = width ?? 0;
        double innerW = Math.Max(0, outerW - padding * 2);
        double innerH = Math.Max(0, outerH - padding * 2);

        var childBounds = Current.NextBounds(innerW, innerH)
            .WithFixedPadding(padding)
            .WithSizing(outerW > 0 ? ElementSizing.Fixed : ElementSizing.FitToChildren, ElementSizing.Fixed);

        var child = new Container(this, childBounds, ContainerOrientation.Horizontal, padding, gap, Current);
        Underlying.BeginChildElements(childBounds);
        _containers.Push(child);
        return this;
    }

    /// <summary>
    /// Ends the current vertical or horizontal container.
    /// </summary>
    /// <returns>The end container.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the operation is invalid for the current state.</exception>
    public ArcanumComposer EndContainer()
    {
        if (_containers.Count <= 1)
            throw new InvalidOperationException("Cannot end the root container.");

        _containers.Pop();
        Underlying.EndChildElements();
        return this;
    }

    /// <summary>
    /// Alias for <see cref="EndContainer" />.
    /// </summary>
    /// <returns>The end vertical.</returns>
    public ArcanumComposer EndVertical() => EndContainer();

    /// <summary>
    /// Alias for <see cref="EndContainer" />.
    /// </summary>
    /// <returns>The end horizontal.</returns>
    public ArcanumComposer EndHorizontal() => EndContainer();

    /// <summary>
    /// Adds a static text label. The height is computed automatically from the font and text.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="font">The font value.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add text.</returns>
    public ArcanumComposer AddText(string text, CairoFont? font = null, double? width = null, double? height = null, string? key = null)
    {
        font ??= ArcanumFont.Body;
        double w = ResolveWidth(text, font, width);
        double h = height ?? ComputeTextHeight(text, font, w, Current.Padding);
        var bounds = Current.NextBounds(w, h);
        Underlying.AddStaticText(text, font, bounds, key);
        return this;
    }

    /// <summary>
    /// Adds a themed button with a <see cref="Func{Boolean}" /> click handler.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="onClick">The on click value.</param>
    /// <param name="height">The height.</param>
    /// <param name="width">The width.</param>
    /// <param name="style">The style value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add button.</returns>
    public ArcanumComposer AddButton(string text, System.Func<bool> onClick, double? height = null, double? width = null, ArcanumButtonStyle style = ArcanumButtonStyle.Default, string? key = null)
    {
        double h = height ?? DefaultRowHeight;
        double w = ResolveButtonWidth(text, width);
        var bounds = Current.NextBounds(w, h);
        Underlying.AddArcanumButton(text, onClick, bounds, style, key);
        return this;
    }

    /// <summary>
    /// Adds a themed button with an <see cref="Action" /> click handler.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="onClick">The on click value.</param>
    /// <param name="height">The height.</param>
    /// <param name="width">The width.</param>
    /// <param name="style">The style value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add button.</returns>
    public ArcanumComposer AddButton(string text, Action onClick, double? height = null, double? width = null, ArcanumButtonStyle style = ArcanumButtonStyle.Default, string? key = null)
        => AddButton(text, () => { onClick?.Invoke(); return true; }, height, width, style, key);

    /// <summary>
    /// Adds two buttons side by side.
    /// </summary>
    /// <param name="leftText">The left text value.</param>
    /// <param name="left">The left value.</param>
    /// <param name="rightText">The right text value.</param>
    /// <param name="right">The right value.</param>
    /// <param name="height">The height.</param>
    /// <param name="gap">The gap value.</param>
    /// <param name="leftStyle">The left style value.</param>
    /// <param name="rightStyle">The right style value.</param>
    /// <param name="leftKey">The left key value.</param>
    /// <param name="rightKey">The right key value.</param>
    /// <returns>The add button row.</returns>
    public ArcanumComposer AddButtonRow(
        string leftText,
        Action left,
        string rightText,
        Action right,
        double? height = null,
        double? gap = null,
        ArcanumButtonStyle leftStyle = ArcanumButtonStyle.Default,
        ArcanumButtonStyle rightStyle = ArcanumButtonStyle.Primary,
        string? leftKey = null,
        string? rightKey = null)
    {
        double h = height ?? DefaultRowHeight;
        double g = gap ?? 12.0;
        double buttonW = Math.Max(0, (Current.AvailableWidth - g) / 2.0);

        BeginHorizontal(padding: 0, gap: g, width: Current.AvailableWidth, height: h);
        AddButton(leftText, left, h, buttonW, leftStyle, leftKey);
        AddButton(rightText, right, h, buttonW, rightStyle, rightKey);
        EndHorizontal();
        return this;
    }

    /// <summary>
    /// Adds a static icon.
    /// </summary>
    /// <param name="assetPath">The asset path value.</param>
    /// <param name="size">The size.</param>
    /// <param name="color">The color value.</param>
    /// <param name="fit">The fit value.</param>
    /// <param name="tint">The tint value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add icon.</returns>
    public ArcanumComposer AddIcon(string assetPath, double size, RGBA? color = null, IconFit fit = IconFit.None, bool tint = false, string? key = null)
    {
        var bounds = Current.NextBounds(size, size);
        Underlying.AddArcanumIcon(assetPath, bounds, color, fit, tint, key);
        return this;
    }

    /// <summary>
    /// Adds a themed card and runs the given builder inside it. If <paramref name="height" /> is null,
    /// the card sizes itself to fit its content.
    /// </summary>
    /// <param name="build">The callback to invoke.</param>
    /// <param name="fill">The fill value.</param>
    /// <param name="border">The border value.</param>
    /// <param name="accent">The accent value.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="padding">The padding value.</param>
    /// <param name="gap">The gap value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add card.</returns>
    public ArcanumComposer AddCard(
        Action<ArcanumComposer> build,
        RGBA? fill = null,
        RGBA? border = null,
        RGBA? accent = null,
        double? width = null,
        double? height = null,
        double padding = 14.0,
        double gap = 0,
        string? key = null)
    {
        double outerW = width ?? Current.AvailableWidth;
        double outerH = height ?? 0;
        double innerW = Math.Max(0, outerW - padding * 2);
        double innerH = Math.Max(0, outerH - padding * 2);

        var cardBounds = Current.NextBounds(innerW, innerH)
            .WithFixedPadding(padding)
            .WithSizing(ElementSizing.Fixed, outerH > 0 ? ElementSizing.Fixed : ElementSizing.FitToChildren);

        Underlying.AddArcanumCard(cardBounds, fill, border, accent, true, true, null, key);
        Underlying.BeginChildElements(cardBounds);

        var child = new Container(this, cardBounds, ContainerOrientation.Vertical, padding, gap, Current);
        _containers.Push(child);
        build(this);
        _containers.Pop();
        Underlying.EndChildElements();
        return this;
    }

    /// <summary>
    /// Adds a scrollable, selectable list.
    /// </summary>
    /// <typeparam name="TItem">The type of the titem value.</typeparam>
    /// <param name="items">The collection of items values.</param>
    /// <param name="label">The label value.</param>
    /// <param name="rowHeight">The row height value.</param>
    /// <param name="onSelected">The callback to invoke.</param>
    /// <param name="height">The height.</param>
    /// <param name="width">The width.</param>
    /// <param name="font">The font value.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add list.</returns>
    public ArcanumComposer AddList<TItem>(
        IEnumerable<TItem> items,
        System.Func<TItem, string> label,
        double rowHeight,
        System.Action<TItem, int>? onSelected = null,
        double? height = null,
        double? width = null,
        CairoFont? font = null,
        string? key = null)
    {
        double h = height ?? DefaultListHeight;
        double w = width ?? ResolveListWidth(width);
        var bounds = Current.NextBounds(w, h);
        var list = new ArcanumList<TItem>(Api, bounds, label, rowHeight, onSelected, font);
        list.SetItems(items);
        Underlying.AddInteractiveElement(list, key);
        return this;
    }

    /// <summary>
    /// Adds a single-line text input.
    /// </summary>
    /// <param name="onTextChanged">The callback to invoke.</param>
    /// <param name="font">The font value.</param>
    /// <param name="height">The height.</param>
    /// <param name="width">The width.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add text input.</returns>
    public ArcanumComposer AddTextInput(
        Action<string> onTextChanged,
        CairoFont? font = null,
        double? height = null,
        double? width = null,
        string? key = null)
    {
        font ??= ArcanumFont.Body;
        double h = height ?? DefaultInputHeight;
        double w = width ?? (Current.Orientation == ContainerOrientation.Horizontal && Current.Bounds.horizontalSizing == ElementSizing.FitToChildren ? 140.0 : Current.AvailableWidth);
        var bounds = Current.NextBounds(w, h);
        Underlying.AddTextInput(bounds, onTextChanged, font, key);
        return this;
    }

    /// <summary>
    /// Adds a single-line number input.
    /// </summary>
    /// <param name="onTextChanged">The callback to invoke.</param>
    /// <param name="font">The font value.</param>
    /// <param name="height">The height.</param>
    /// <param name="width">The width.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add number input.</returns>
    public ArcanumComposer AddNumberInput(
        Action<string> onTextChanged,
        CairoFont? font = null,
        double? height = null,
        double? width = null,
        string? key = null)
    {
        font ??= ArcanumFont.Body;
        double h = height ?? DefaultInputHeight;
        double w = width ?? (Current.Orientation == ContainerOrientation.Horizontal && Current.Bounds.horizontalSizing == ElementSizing.FitToChildren ? 100.0 : Current.AvailableWidth);
        var bounds = Current.NextBounds(w, h);
        Underlying.AddNumberInput(bounds, onTextChanged, font, key);
        return this;
    }

    /// <summary>
    /// Adds a dropdown selector.
    /// </summary>
    /// <param name="values">The collection of values values.</param>
    /// <param name="names">The collection of names values.</param>
    /// <param name="selectedIndex">The selected index value.</param>
    /// <param name="onSelected">The callback to invoke.</param>
    /// <param name="font">The font value.</param>
    /// <param name="height">The height.</param>
    /// <param name="width">The width.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add dropdown.</returns>
    public ArcanumComposer AddDropdown(
        string[] values,
        string[] names,
        int selectedIndex,
        Action<string, bool> onSelected,
        CairoFont? font = null,
        double? height = null,
        double? width = null,
        string? key = null)
    {
        font ??= ArcanumFont.Body;
        double h = height ?? DefaultInputHeight;
        double w = width ?? Current.AvailableWidth;
        var bounds = Current.NextBounds(w, h);
        SelectionChangedDelegate onSelectionChanged = (code, selected) => onSelected?.Invoke(code, selected);
        Underlying.AddDropDown(values, names, selectedIndex, onSelectionChanged, bounds, font, key);
        return this;
    }

    /// <summary>
    /// Adds a themed vertical scrollbar. Useful when paired with a custom clipping area.
    /// </summary>
    /// <param name="onNewValue">The callback to invoke.</param>
    /// <param name="height">The height.</param>
    /// <param name="width">The width.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The add scrollbar.</returns>
    public ArcanumComposer AddScrollbar(Action<float> onNewValue, double? height = null, double? width = null, string? key = null)
    {
        double h = height ?? DefaultListHeight;
        double w = width ?? GuiElement.scaled(12.0);
        var bounds = Current.NextBounds(w, h);
        Underlying.AddArcanumScrollbar(onNewValue, bounds, key);
        return this;
    }

    /// <summary>
    /// Finalises the composer and returns the wrapped <see cref="GuiComposer" />.
    /// </summary>
    /// <returns>The compose.</returns>
    public GuiComposer Compose()
    {
        while (_containers.Count > 1)
        {
            EndContainer();
        }

        _containers.Pop();
        Underlying.EndChildElements();
        return Underlying.Compose();
    }

    private double ResolveWidth(string text, CairoFont font, double? requested)
    {
        if (requested.HasValue) return requested.Value;
        if (Current.Orientation == ContainerOrientation.Vertical || Current.Bounds.horizontalSizing == ElementSizing.Fixed)
            return Current.AvailableWidth;

        return Math.Max(20.0, MeasureTextWidth(text, font) + Current.Padding * 2);
    }

    private double ResolveButtonWidth(string text, double? requested)
    {
        if (requested.HasValue) return requested.Value;
        if (Current.Orientation == ContainerOrientation.Vertical || Current.Bounds.horizontalSizing == ElementSizing.Fixed)
            return Current.AvailableWidth;

        return Math.Max(80.0, MeasureTextWidth(text, ArcanumFont.Body) + 40.0);
    }

    private double ResolveListWidth(double? requested)
    {
        if (requested.HasValue) return requested.Value;
        if (Current.Orientation == ContainerOrientation.Vertical || Current.Bounds.horizontalSizing == ElementSizing.Fixed)
            return Current.AvailableWidth;

        return Math.Max(160.0, Current.AvailableWidth);
    }

    private double MeasureTextWidth(string text, CairoFont font)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        try
        {
            using var surface = new ImageSurface(Format.Argb32, 1, 1);
            using var ctx = new Context(surface);
            font.SetupContext(ctx);
            var ext = ctx.TextExtents(text);
            return ext.Width / RuntimeEnv.GUIScale;
        }
        catch (Exception ex)
        {
            Api?.Logger?.Warning("[ArcanumLib] [ArcanumComposer] MeasureTextWidth failed for '{0}': {1}", text, ex.Message);
            return 0;
        }
    }

    private double ComputeTextHeight(string text, CairoFont font, double width, double padding)
    {
        if (string.IsNullOrWhiteSpace(text)) return DefaultTextHeight;

        try
        {
            var util = new TextDrawUtil();
            double boxW = Math.Max(1.0, width - padding * 2) * RuntimeEnv.GUIScale;
            double pixelHeight = util.GetMultilineTextHeight(font, text, boxW);
            return Math.Max(DefaultTextHeight, pixelHeight / RuntimeEnv.GUIScale);
        }
        catch (Exception ex)
        {
            Api?.Logger?.Warning("[ArcanumLib] [ArcanumComposer] ComputeTextHeight failed for '{0}': {1}", text, ex.Message);
            return DefaultTextHeight;
        }
    }

    private enum ContainerOrientation
    {
        Vertical,
        Horizontal
    }

    private sealed class Container
    {
        public ArcanumComposer Composer;
        public ElementBounds Bounds;
        public ContainerOrientation Orientation;
        public double Padding;
        public double Gap;
        public ElementBounds? LastChild;
        public Container? Parent;

        /// <summary>Available inner width accounting for fixed sizing, padding, and parent.</summary>
        public double AvailableWidth
        {
            get
            {
                if (Bounds.horizontalSizing == ElementSizing.FitToChildren) return 0;
                if (Bounds.horizontalSizing == ElementSizing.Fixed && Bounds.fixedWidth > 0)
                    return Math.Max(0, Bounds.fixedWidth);
                if (Bounds.fixedWidth > 0)
                    return Math.Max(0, Bounds.fixedWidth);
                if (Parent != null)
                    return Math.Max(0, Parent.AvailableWidth - Padding * 2);
                return Math.Max(0, Composer.DefaultItemWidth - Padding * 2);
            }
        }

        /// <summary>Available inner height accounting for fixed sizing, padding, and parent.</summary>
        public double AvailableHeight
        {
            get
            {
                if (Bounds.verticalSizing == ElementSizing.FitToChildren) return 0;
                if (Bounds.verticalSizing == ElementSizing.Fixed && Bounds.fixedHeight > 0)
                    return Math.Max(0, Bounds.fixedHeight);
                if (Bounds.fixedHeight > 0)
                    return Math.Max(0, Bounds.fixedHeight);
                if (Parent != null)
                    return Math.Max(0, Parent.AvailableHeight - Padding * 2);
                return Composer.DefaultRowHeight;
            }
        }

        /// <summary>Creates a layout container bound to a composer with the given orientation and spacing.</summary>
        /// <param name="composer">The owning composer.</param>
        /// <param name="bounds">The bounds of this container.</param>
        /// <param name="orientation">Layout direction (horizontal or vertical).</param>
        /// <param name="padding">Inner padding in pixels.</param>
        /// <param name="gap">Gap between children in pixels.</param>
        /// <param name="parent">Optional parent container for nested layouts.</param>
        public Container(ArcanumComposer composer, ElementBounds bounds, ContainerOrientation orientation, double padding, double gap, Container? parent)
        {
            Composer = composer;
            Bounds = bounds;
            Orientation = orientation;
            Padding = padding;
            Gap = gap;
            Parent = parent;
        }

        /// <summary>Computes the next child bounds within this container, advancing the layout cursor.</summary>
        /// <param name="width">Optional fixed width; when null the available width is used.</param>
        /// <param name="height">Optional fixed height; when null the available height is used.</param>
        /// <returns>The <see cref="ElementBounds" /> for the next child.</returns>
        public ElementBounds NextBounds(double? width = null, double? height = null)
        {
            double innerW = Math.Max(0, width ?? AvailableWidth);
            double innerH = Math.Max(0, height ?? AvailableHeight);

            ElementBounds result;
            if (LastChild == null)
            {
                result = ElementBounds.Fixed(0, 0, innerW, innerH);
            }
            else if (Orientation == ContainerOrientation.Vertical)
            {
                result = LastChild.BelowCopy(0, Gap).WithFixedSize(innerW, innerH);
            }
            else
            {
                result = LastChild.RightCopy(Gap).WithFixedSize(innerW, innerH);
            }

            result.fixedPaddingX = 0;
            result.fixedPaddingY = 0;
            result.fixedMarginX = 0;
            result.fixedMarginY = 0;
            result.WithAlignment(EnumDialogArea.None);
            result.WithParent(Bounds);

            LastChild = result;
            return result;
        }
    }
}
