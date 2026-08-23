using ProtoBuf;
using System.Collections.Generic;

namespace ArcanumLib.Gui.Controls
{
    /// <summary>
    /// Data for a custom info tab with scrollable sections.
    /// Consumers register providers that build this data server-side;
    /// the client renders it as a scrollable tab with sections.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CustomTabData
    {
        /// <summary>Localization key (or plain text) for the tab name shown in the tab strip.</summary>
        public string? tabNameKey { get; set; }

        /// <summary>Ordered list of content sections to render in the tab body.</summary>
        public List<CustomTabSection>? sections { get; set; }

        /// <summary>
        /// Optional localization prefix for decoration strings. The client resolves these keys:
        ///   {decorPrefix}-decor-divider   — section separator line
        ///   {decorPrefix}-decor-header    — symbol prefix before section title
        ///   {decorPrefix}-decor-entry     — bullet marker for normal entries
        ///   {decorPrefix}-decor-active    — bullet marker for active entries
        ///   {decorPrefix}-decor-sub       — sub-item marker (for nested lines)
        /// If null or keys missing, client falls back to built-in defaults.
        /// Consumers provide decoration via their lang files.
        /// </summary>
        public string? decorPrefix { get; set; }
    }

    /// <summary>
    /// A single section within a custom tab.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CustomTabSection
    {
        /// <summary>Localization key (or plain text) for the section header.</summary>
        public string? titleKey { get; set; }

        /// <summary>Optional rich-text intro shown below the header, before entries.</summary>
        public string? introKey { get; set; }

        /// <summary>List of entries (name + description pairs) in this section.</summary>
        public List<CustomTabEntry>? entries { get; set; }
    }

    /// <summary>
    /// A single entry within a section.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CustomTabEntry
    {
        /// <summary>Localization key (or plain text) for the entry name.</summary>
        public string? nameKey { get; set; }

        /// <summary>Localization key (or plain text) for the entry description.</summary>
        public string? descKey { get; set; }

        /// <summary>If true, the entry is highlighted as currently active.</summary>
        public bool isActive { get; set; }
    }
}
