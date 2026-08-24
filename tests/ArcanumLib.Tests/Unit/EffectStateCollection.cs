using Xunit;

namespace ArcanumLib.Tests.Unit;

[CollectionDefinition("EffectState")]
public class EffectStateCollection : ICollectionFixture<object>
{
    // Groups tests that touch the static EffectResistanceStore registry.
}
