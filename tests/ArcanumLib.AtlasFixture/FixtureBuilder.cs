using Vintagestory.API.MathTools;

namespace ArcanumLib.AtlasTests;

/// <summary>
/// Builder scenario that generates a small, pristine world fixture.
/// This scenario is intended to be run via <c>atlas fixture</c>:
/// <code>
/// atlas fixture bin/Debug/net10.0/ArcanumLib.AtlasTests.dll \
///   --scenario BuildsFixture --out tests/ArcanumLib.AtlasTests/fixtures/world.vcdbs --force
/// </code>
/// </summary>
[AtlasWorld(Seed = 12345)]
public class FixtureBuilder : AtlasScenarioBase
{
    [AtlasScenario]
    public async Task BuildsFixture()
    {
        var pos = World.Spawn.UpCopy();

        // Place a tiny landmark platform so tests have a known reference surface.
        for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
                World.SetBlock("game:soil-medium-normal", pos.AddCopy(x, 0, z));

        await World.Ticks(10);

        // No assertions; the side effect is the generated world save.
    }
}
