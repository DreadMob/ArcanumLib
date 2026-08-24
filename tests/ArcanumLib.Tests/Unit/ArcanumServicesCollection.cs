using Xunit;

namespace ArcanumLib.Tests.Unit;

[CollectionDefinition("ArcanumServices")]
public class ArcanumServicesCollection : ICollectionFixture<object>
{
    // Empty fixture used to group tests that touch the static ArcanumServices registry.
    // This prevents them from running in parallel and stepping on each other's global state.
}
