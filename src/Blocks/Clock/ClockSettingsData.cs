using ProtoBuf;

namespace ArcanumLib.Blocks.Clock;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClockSettingsData
{
    public int EnabledHours { get; set; } = 0xFFFFFF;

    /// <summary>Zero is not written on the wire: without this "no hours at all" would arrive as "every hour".</summary>
    [ProtoBeforeDeserialization] public void ZeroDefaults() { EnabledHours = 0; }
}
