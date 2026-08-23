using System;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ArcanumLib.Gui.Controls
{
    /// <summary>
    /// Generic, reusable date/time picker composed of day/month/year/hour/minute inputs
    /// with Now/Clear buttons. Use with any GuiComposer and a unique key prefix.
    /// </summary>
    public static class GuiDateTimePicker
    {
        private const double InputH = 29;
        private const double LabelH = 20;
        private const double RowGap = 4;

        private static readonly string[] MonthValues = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };
        private static readonly string[] MonthNames = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };

        /// <summary>
        /// Composes a date/time picker into the given composer.
        /// </summary>
        /// <param name="composer">The composer to add the picker to.</param>
        /// <param name="titleLangCode">Localization key (or plain text) for the picker title.</param>
        /// <param name="prefix">Unique key prefix for the picker's input elements.</param>
        /// <param name="x">X position within the composer.</param>
        /// <param name="y">Y position within the composer.</param>
        /// <param name="width">Total width of the picker.</param>
        /// <param name="nowLangKey">Localization key for the "Now" button. Defaults to <c>"now"</c>.</param>
        /// <param name="clearLangKey">Localization key for the "Clear" button. Defaults to <c>"clear"</c>.</param>
        /// <returns>The Y position after the picker (for chaining subsequent elements).</returns>
        public static double Compose(GuiComposer composer, string titleLangCode, string prefix, double x, double y, double width,
            string nowLangKey = "now", string clearLangKey = "clear")
        {
            composer.AddStaticText(Lang.Get(titleLangCode), CairoFont.WhiteDetailText(), ElementBounds.Fixed(x, y, width, LabelH));

            double row1Y = y + LabelH + 2;
            double row2Y = row1Y + InputH + RowGap;

            double dayW = 42;
            double monthW = 52;
            double yearW = 52;
            double timeW = 42;
            double btnW = 46;
            double gap = 4;

            double cx = x;
            composer.AddNumberInput(ElementBounds.Fixed(cx, row1Y, dayW, InputH), null, CairoFont.WhiteDetailText(), DayKey(prefix));
            cx += dayW + gap;
            composer.AddDropDown(MonthValues, MonthNames, 0, null, ElementBounds.Fixed(cx, row1Y, monthW, InputH), MonthKey(prefix));
            cx += monthW + gap;
            composer.AddNumberInput(ElementBounds.Fixed(cx, row1Y, yearW, InputH), null, CairoFont.WhiteDetailText(), YearKey(prefix));
            cx += yearW + gap;
            composer.AddSmallButton(Lang.Get(nowLangKey), () => OnNow(composer, prefix), ElementBounds.Fixed(cx, row1Y, btnW, InputH));

            cx = x;
            composer.AddNumberInput(ElementBounds.Fixed(cx, row2Y, timeW, InputH), null, CairoFont.WhiteDetailText(), HourKey(prefix));
            cx += timeW + gap;
            composer.AddNumberInput(ElementBounds.Fixed(cx, row2Y, timeW, InputH), null, CairoFont.WhiteDetailText(), MinuteKey(prefix));
            cx += timeW + gap;
            composer.AddSmallButton(Lang.Get(clearLangKey), () => OnClear(composer, prefix), ElementBounds.Fixed(cx, row2Y, btnW, InputH));

            return row2Y + InputH + 8;
        }

        /// <summary>Sets the date/time from a string in "yyyy-MM-dd HH:mm" format.</summary>
        public static void SetDate(GuiComposer composer, string prefix, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                Clear(composer, prefix);
                return;
            }

            SetAllIntModes(composer, prefix);
            SetNumberValue(composer, DayKey(prefix), dt.Day);
            SetDropDownValue(composer, MonthKey(prefix), dt.Month.ToString());
            SetNumberValue(composer, YearKey(prefix), dt.Year);
            SetNumberValue(composer, HourKey(prefix), dt.Hour);
            SetNumberValue(composer, MinuteKey(prefix), dt.Minute);
        }

        /// <summary>Gets the date/time as a string in "yyyy-MM-dd HH:mm" format, or null if no year is set.</summary>
        public static string? GetDate(GuiComposer composer, string prefix)
        {
            var yearInput = composer.GetNumberInput(YearKey(prefix));
            int year = yearInput != null ? (int)yearInput.GetValue() : 0;
            if (year <= 0) return null;

            var monthDd = composer.GetDropDown(MonthKey(prefix));
            int month = 1;
            if (monthDd != null && int.TryParse(monthDd.SelectedValue, out int parsedMonth))
                month = parsedMonth;
            month = Math.Clamp(month, 1, 12);

            int day = Math.Clamp((int)(composer.GetNumberInput(DayKey(prefix))?.GetValue() ?? 1), 1, DateTime.DaysInMonth(year, month));
            int hour = Math.Clamp((int)(composer.GetNumberInput(HourKey(prefix))?.GetValue() ?? 0), 0, 23);
            int minute = Math.Clamp((int)(composer.GetNumberInput(MinuteKey(prefix))?.GetValue() ?? 0), 0, 59);

            var dt = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
            return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static void Clear(GuiComposer composer, string prefix)
        {
            SetAllIntModes(composer, prefix);
            SetNumberValue(composer, DayKey(prefix), 0);
            SetDropDownValue(composer, MonthKey(prefix), "1");
            SetNumberValue(composer, YearKey(prefix), 0);
            SetNumberValue(composer, HourKey(prefix), 0);
            SetNumberValue(composer, MinuteKey(prefix), 0);
        }

        private static bool OnNow(GuiComposer composer, string prefix)
        {
            SetDate(composer, prefix, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            return true;
        }

        private static bool OnClear(GuiComposer composer, string prefix)
        {
            Clear(composer, prefix);
            return true;
        }

        private static void SetAllIntModes(GuiComposer composer, string prefix)
        {
            SetIntMode(composer, DayKey(prefix));
            SetIntMode(composer, YearKey(prefix));
            SetIntMode(composer, HourKey(prefix));
            SetIntMode(composer, MinuteKey(prefix));
        }

        private static void SetIntMode(GuiComposer composer, string key)
        {
            var input = composer.GetNumberInput(key);
            if (input != null) input.IntMode = true;
        }

        private static void SetNumberValue(GuiComposer composer, string key, int value)
        {
            var input = composer.GetNumberInput(key);
            if (input != null) input.SetValue(value);
        }

        private static void SetDropDownValue(GuiComposer composer, string key, string value)
        {
            var dd = composer.GetDropDown(key);
            if (dd != null) dd.SetSelectedValue(value);
        }

        private static string DayKey(string prefix) => $"{prefix}-day";
        private static string MonthKey(string prefix) => $"{prefix}-month";
        private static string YearKey(string prefix) => $"{prefix}-year";
        private static string HourKey(string prefix) => $"{prefix}-hour";
        private static string MinuteKey(string prefix) => $"{prefix}-minute";
    }
}
