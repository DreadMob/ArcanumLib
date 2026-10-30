using ProtoBuf;

namespace ArcanumLib.Blocks.Clock;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClockSettingsData
{
    public int EnabledHours { get; set; } = 0xFFFFFF;
}
