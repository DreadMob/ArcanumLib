using System;
using ArcanumLib.Core;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ArcanumServiceRegistryTests
{
    [Fact]
    public void Dispose_SameInstanceRegisteredUnderTwoInterfaces_DisposesOnce()
    {
        var registry = new ArcanumServiceRegistry();
        var service = new MultiInterfaceService();
        registry.Register<IServiceA>(service);
        registry.Register<IServiceB>(service);

        registry.Dispose();

        Assert.Equal(1, service.DisposeCount);
    }

    private interface IServiceA { }

    private interface IServiceB { }

    private sealed class MultiInterfaceService : IServiceA, IServiceB, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
