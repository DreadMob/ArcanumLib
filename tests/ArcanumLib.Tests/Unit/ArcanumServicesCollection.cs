using Xunit;

namespace ArcanumLib.Tests.Unit;

/// <summary>
/// Groups tests that touch the <see cref="ArcanumLib.Core.ArcanumRuntime" /> and its service registry.
/// Tests in this collection run sequentially to avoid parallel static-state conflicts.
/// Each test class is responsible for activating and disposing its own runtime.
/// This collection replaces the former "EffectState" collection — all tests that need
/// a runtime or any service registered in it should use "ArcanumServices".
/// </summary>
[CollectionDefinition("ArcanumServices")]
public class ArcanumServicesCollection
{
}
