using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Text;

/// <summary>A server-wide chat announcement with enough context for a per-player filter.</summary>
public sealed class ChatAnnouncement
{
    /// <summary>Event code the player's notification settings match against (e.g. "casino-jackpot").</summary>
    public string Event = "";
    public string Message = "";
    /// <summary>Where it happened, or null.</summary>
    public Vec3d? Position;
    public int Dimension;
    /// <summary>Players directly involved (always notified).</summary>
    public string[] ParticipantUids = System.Array.Empty<string>();
}

/// <summary>
/// One entry point for server-wide announcements shared by all mods. A mod with a
/// notification filter (Quest Framework's chat bell) installs <see cref="Handler"/>;
/// without one the message goes to the general chat group as is.
/// </summary>
public static class ChatAnnounce
{
    /// <summary>Delivers an announcement; null = plain broadcast.</summary>
    public static System.Action<ICoreServerAPI, ChatAnnouncement>? Handler { get; set; }

    public static void Send(ICoreServerAPI sapi, ChatAnnouncement announcement)
    {
        if (sapi == null || announcement == null || string.IsNullOrWhiteSpace(announcement.Message)) return;
        if (Handler != null) Handler(sapi, announcement);
        else sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, announcement.Message, EnumChatType.Notification);
    }
}
