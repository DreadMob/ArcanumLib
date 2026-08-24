using System;
using System.Collections.Generic;
using System.Text;
using Cairo;
using ArcanumLib.Gui.Icons;
using ArcanumLib.Gui.Theme;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace ArcanumLib.Gui.Controls
{
    /// <summary>
    /// Cairo-rendered scrollable content element for custom info tabs.
    /// Renders sections with decorative icons, section headers, entry bullets,
    /// and wrapped text — all drawn via Cairo for a polished look.
    /// Decoration is data-driven: the tab data carries a decorPrefix whose
    /// localization keys select which icon style to use.
    /// </summary>
    public class GuiElementCustomTabContent : GuiElement
    {
        private readonly CustomTabData tabData;
        private LoadedTexture contentTexture;
        private double scrollY;
        private double maxScrollY;
        private double totalContentHeight;

        // Layout tokens
        private double PadX => scaled(12.0);
        private double PadTop => scaled(8.0);
        private double SectionGap => scaled(14.0);
        private double EntryGap => scaled(6.0);
        private double TitleSize => scaled(18.0);
        private double IntroSize => scaled(14.0);
        private double NameSize => scaled(15.0);
        private double DescSize => scaled(14.0);
        private double IconSize => scaled(10.0);
        private double DividerPad => scaled(8.0);
        private double LineHeightMul => 1.35;

        /// <summary>
        /// Scrollbar callback set by the composer.
        /// </summary>
        public Action<float>? OnScroll;

        /// <summary>
        /// Optional localization resolver. If set, called to resolve localization keys
        /// (strings containing ':'). If null, falls back to <see cref="Lang.Get" />.
        /// Consumers with custom localization systems should set this.
        /// </summary>
        public static Func<string, string>? Resolver { get; set; }

        /// <summary>Gets the total content height.</summary>
        public double TotalContentHeight => totalContentHeight;

        /// <summary>Performs the gui element custom tab content operation.</summary>
        /// <param name="capi">The client API instance.</param>
        /// <param name="bounds">The bounds value.</param>
        /// <param name="data">The associated data.</param>
        public GuiElementCustomTabContent(ICoreClientAPI capi, ElementBounds bounds, CustomTabData data)
            : base(capi, bounds)
        {
            tabData = data;
            contentTexture = new LoadedTexture(capi);
        }

        /// <summary>Performs the compose elements operation.</summary>
        /// <param name="ctxStatic">The ctx static value.</param>
        /// <param name="surfaceStatic">The surface static value.</param>
        public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
        {
            RegenerateTexture();
        }

        /// <summary>Sets scroll.</summary>
        /// <param name="value">The value to set or compare.</param>
        public void SetScroll(double value)
        {
            scrollY = Math.Max(0, Math.Min(maxScrollY, value));
        }

        /// <summary>Updates scrollbar.</summary>
        /// <param name="sb">The sb value.</param>
        public void UpdateScrollbar(ArcanumScrollbar sb)
        {
            if (sb == null) return;
            sb.SetHeights((float)Bounds.InnerHeight, (float)totalContentHeight);
        }

        /// <summary>Performs the on mouse wheel operation.</summary>
        /// <param name="api">The client API instance.</param>
        /// <param name="args">The arguments.</param>
        public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
        {
            if (args.IsHandled) return;
            if (maxScrollY <= 0) return;
            double delta = -args.delta * scaled(20.0);
            double newScroll = scrollY + delta;
            newScroll = Math.Max(0, Math.Min(maxScrollY, newScroll));
            if (Math.Abs(newScroll - scrollY) > 0.1)
            {
                scrollY = newScroll;
                OnScroll?.Invoke((float)(scrollY / Math.Max(1, maxScrollY)));
                args.SetHandled(true);
            }
        }

        private static string ResolveKeyOrText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value ?? "";
            if (Resolver != null && LooksLikeLangKey(value))
                return Resolver(value);
            if (LooksLikeLangKey(value))
            {
                string resolved = Lang.Get(value);
                return string.IsNullOrEmpty(resolved) ? value : resolved;
            }
            return value;
        }

        private static bool LooksLikeLangKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(':') > 0;
        }

        private void RegenerateTexture()
        {
            Bounds.CalcWorldBounds();
            int width = Math.Max(1, (int)Bounds.InnerWidth);
            int height = Math.Max(1, (int)Bounds.InnerHeight);

            // First pass: measure total content height
            double measuredHeight = MeasureContent(width);
            totalContentHeight = measuredHeight;
            maxScrollY = Math.Max(0, measuredHeight - height);
            if (scrollY > maxScrollY) scrollY = maxScrollY;

            // Render texture — use a tall surface to fit all content, then clip during render
            // Fill with an opaque background matching the dialog surface so Cairo text
            // antialiasing composites correctly (transparent backgrounds produce ghostly edges).
            int texHeight = Math.Max(height, (int)Math.Ceiling(measuredHeight + scaled(16)));
            using var surface = new ImageSurface(Format.Argb32, width, texHeight);
            using var ctx = new Context(surface);
            var bg = ArcanumGuiTheme.SurfaceBase;
            ctx.SetSourceRGBA(bg.R, bg.G, bg.B, 1.0);
            ctx.Paint();

            DrawContent(ctx, width);

            TryGenerateTexture(surface, ref contentTexture);
        }

        private double MeasureContent(int width)
        {
            if (tabData?.sections == null || tabData.sections.Count == 0) return 0;
            double textW = width - PadX * 2;
            double y = PadTop;

            for (int sIdx = 0; sIdx < tabData.sections.Count; sIdx++)
            {
                var section = tabData.sections[sIdx];
                if (section == null) continue;

                if (sIdx > 0) y += SectionGap + DividerPad * 2;

                string title = ResolveKeyOrText(section.titleKey);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    y += TitleSize * LineHeightMul;
                }

                if (!string.IsNullOrWhiteSpace(section.introKey))
                {
                    string intro = ResolveKeyOrText(section.introKey);
                    if (!string.IsNullOrWhiteSpace(intro))
                    {
                        y += MeasureWrappedHeight(intro, IntroSize, textW) + scaled(4);
                    }
                }

                y += scaled(4);

                if (section.entries != null)
                {
                    bool first = true;
                    foreach (var entry in section.entries)
                    {
                        if (entry == null) continue;
                        if (!first) y += EntryGap;
                        first = false;

                        string name = ResolveKeyOrText(entry.nameKey);
                        string desc = ResolveKeyOrText(entry.descKey);

                        if (!string.IsNullOrWhiteSpace(name))
                            y += NameSize * LineHeightMul;
                        if (!string.IsNullOrWhiteSpace(desc))
                            y += MeasureWrappedHeight(StripFontTags(desc), DescSize, textW - scaled(8));
                    }
                }

                y += scaled(6);
            }

            return y;
        }

        private double MeasureWrappedHeight(string text, double fontSize, double maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            // Estimate: ~0.55 * fontSize per character width
            double charW = fontSize * 0.55;
            double charsPerLine = Math.Max(1, maxWidth / charW);
            string clean = StripFontTags(text);
            int totalChars = clean.Length;
            int lines = Math.Max(1, (int)Math.Ceiling(totalChars / charsPerLine));
            // Account for explicit newlines
            string[] explicitLines = clean.Split('\n');
            if (explicitLines.Length > 1)
            {
                lines = 0;
                foreach (var line in explicitLines)
                    lines += Math.Max(1, (int)Math.Ceiling(line.Length / charsPerLine));
            }
            return lines * fontSize * LineHeightMul;
        }

        private static string StripFontTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder();
            bool inTag = false;
            foreach (char c in text)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString();
        }

        private void DrawContent(Context ctx, int width)
        {
            if (tabData?.sections == null || tabData.sections.Count == 0) return;

            double textW = width - PadX * 2;
            double y = PadTop;

            // Resolve decoration
            string? decorPrefix = tabData.decorPrefix;
            bool useIcons = !string.IsNullOrWhiteSpace(decorPrefix);

            for (int sIdx = 0; sIdx < tabData.sections.Count; sIdx++)
            {
                var section = tabData.sections[sIdx];
                if (section == null) continue;

                // Section divider
                if (sIdx > 0)
                {
                    y += DividerPad;
                    CustomTabIconRenderer.DrawSectionDivider(ctx, PadX, y, textW, ArcanumGuiTheme.BorderSilver);
                    y += DividerPad + scaled(4);
                }

                // Section header with icon
                string title = ResolveKeyOrText(section.titleKey);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    double iconCx = PadX + IconSize * 0.5;
                    double iconCy = y + TitleSize * 0.5;

                    if (useIcons)
                        CustomTabIconRenderer.DrawSectionHeaderIcon(ctx, iconCx, iconCy, IconSize, ArcanumGuiTheme.Accent);

                    ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
                    ctx.SetFontSize(TitleSize);
                    ctx.SetSourceRGBA(
                        ArcanumGuiTheme.AccentBright.R,
                        ArcanumGuiTheme.AccentBright.G,
                        ArcanumGuiTheme.AccentBright.B, 1.0);
                    double textStartX = PadX + (useIcons ? IconSize * 1.6 : 0);
                    ctx.MoveTo(textStartX, y + TitleSize * 0.85);
                    ctx.ShowText(title);
                    y += TitleSize * LineHeightMul;
                }

                // Section intro
                if (!string.IsNullOrWhiteSpace(section.introKey))
                {
                    string intro = ResolveKeyOrText(section.introKey);
                    if (!string.IsNullOrWhiteSpace(intro))
                    {
                        ctx.SelectFontFace("Sans", FontSlant.Italic, FontWeight.Normal);
                        ctx.SetFontSize(IntroSize);
                        ctx.SetSourceRGBA(
                            ArcanumGuiTheme.StatusActive.R,
                            ArcanumGuiTheme.StatusActive.G,
                            ArcanumGuiTheme.StatusActive.B, 1.0);
                        y += DrawWrappedText(ctx, StripFontTags(intro), PadX, y, textW, IntroSize) + scaled(4);
                    }
                }

                y += scaled(2);

                // Entries
                if (section.entries != null)
                {
                    bool first = true;
                    foreach (var entry in section.entries)
                    {
                        if (entry == null) continue;
                        if (!first) y += EntryGap;
                        first = false;

                        string name = ResolveKeyOrText(entry.nameKey);
                        string desc = ResolveKeyOrText(entry.descKey);

                        // Entry bullet/star icon
                        if (useIcons && !string.IsNullOrWhiteSpace(name))
                        {
                            double bulletCx = PadX + IconSize * 0.5;
                            double bulletCy = y + NameSize * 0.55;
                            if (entry.isActive)
                                CustomTabIconRenderer.DrawActiveStar(ctx, bulletCx, bulletCy, IconSize * 0.5, ArcanumGuiTheme.AccentBright);
                            else
                                CustomTabIconRenderer.DrawEntryBullet(ctx, bulletCx, bulletCy, IconSize, ArcanumGuiTheme.Accent);
                        }

                        // Entry name
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
                            ctx.SetFontSize(NameSize);
                            RGBA nameColor = entry.isActive ? ArcanumGuiTheme.Highlight : ArcanumGuiTheme.TextPrimary;
                            ctx.SetSourceRGBA(nameColor.R, nameColor.G, nameColor.B, 1.0);
                            double nameX = PadX + (useIcons ? IconSize * 1.6 : 0);
                            ctx.MoveTo(nameX, y + NameSize * 0.85);
                            ctx.ShowText(name);
                            y += NameSize * LineHeightMul;
                        }

                        // Entry description (wrapped)
                        if (!string.IsNullOrWhiteSpace(desc))
                        {
                            string cleanDesc = StripFontTags(desc);
                            double descX = PadX + (useIcons ? IconSize * 1.6 : 0) + scaled(4);
                            double descW = textW - (descX - PadX);
                            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
                            ctx.SetFontSize(DescSize);
                            ctx.SetSourceRGBA(
                                ArcanumGuiTheme.TextMuted.R,
                                ArcanumGuiTheme.TextMuted.G,
                                ArcanumGuiTheme.TextMuted.B, 1.0);
                            y += DrawWrappedText(ctx, cleanDesc, descX, y, descW, DescSize);
                        }
                    }
                }

                y += scaled(6);
            }
        }

        private double DrawWrappedText(Context ctx, string text, double x, double y, double maxWidth, double fontSize)
        {
            string[] paragraphs = text.Split('\n');
            double lineH = fontSize * LineHeightMul;
            double curY = y;

            foreach (string para in paragraphs)
            {
                string trimmed = para.TrimEnd('\r').Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    curY += lineH * 0.5;
                    continue;
                }

                string[] words = trimmed.Split(' ');
                string current = "";

                foreach (string word in words)
                {
                    string test = string.IsNullOrEmpty(current) ? word : current + " " + word;
                    TextExtents ext = ctx.TextExtents(test);
                    if (ext.Width > maxWidth && !string.IsNullOrEmpty(current))
                    {
                        ctx.MoveTo(x, curY + fontSize * 0.85);
                        ctx.ShowText(current);
                        curY += lineH;
                        current = word;
                    }
                    else
                    {
                        current = test;
                    }
                }

                if (!string.IsNullOrEmpty(current))
                {
                    ctx.MoveTo(x, curY + fontSize * 0.85);
                    ctx.ShowText(current);
                    curY += lineH;
                }
            }

            return curY - y;
        }

        /// <summary>Performs the render interactive elements operation.</summary>
        /// <param name="deltaTime">The delta time value.</param>
        public override void RenderInteractiveElements(float deltaTime)
        {
            if (contentTexture == null || contentTexture.TextureId == 0) return;
            api.Render.Render2DLoadedTexture(contentTexture, (float)Bounds.absX, (float)(Bounds.absY - scrollY));
        }

        /// <summary>Releases all resources used by the current object.</summary>
        public override void Dispose()
        {
            contentTexture?.Dispose();
            base.Dispose();
        }

        private void TryGenerateTexture(ImageSurface surface, ref LoadedTexture texture)
        {
            if (api?.Render == null) return;
            try
            {
                generateTexture(surface, ref texture);
            }
            catch (Exception ex)
            {
                api?.Logger?.Warning("[GuiElementCustomTabContent] Texture generation failed: {0}", ex.Message);
                texture?.Dispose();
                texture = new LoadedTexture(api);
            }
        }
    }
}
