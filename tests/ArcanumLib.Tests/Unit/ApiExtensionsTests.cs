using ArcanumLib.Common;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ApiExtensionsTests
{
    [Fact]
    public void IsClient_ICoreAPI_ClientSide_ReturnsTrue()
    {
        var api = Substitute.For<ICoreAPI>();
        api.Side.Returns(EnumAppSide.Client);

        Assert.True(api.IsClient());
        Assert.False(api.IsServer());
    }

    [Fact]
    public void IsServer_ICoreAPI_ServerSide_ReturnsTrue()
    {
        var api = Substitute.For<ICoreAPI>();
        api.Side.Returns(EnumAppSide.Server);

        Assert.True(api.IsServer());
        Assert.False(api.IsClient());
    }

    [Fact]
    public void IsClient_ICoreClientAPI_ClientSide_ReturnsTrue()
    {
        var capi = Substitute.For<ICoreClientAPI>();
        capi.Side.Returns(EnumAppSide.Client);

        Assert.True(capi.IsClient());
        Assert.False(capi.IsServer());
    }

    [Fact]
    public void IsServer_IWorldAccessor_ServerSide_ReturnsTrue()
    {
        var world = Substitute.For<IWorldAccessor>();
        world.Side.Returns(EnumAppSide.Server);

        Assert.True(world.IsServer());
        Assert.False(world.IsClient());
    }

    [Fact]
    public void IsClient_NullApi_ReturnsFalse()
    {
        ICoreAPI? api = null;
        Assert.False(api!.IsClient());
        Assert.False(api!.IsServer());
    }

    [Fact]
    public void IsServer_NullWorld_ReturnsFalse()
    {
        IWorldAccessor? world = null;
        Assert.False(world!.IsServer());
        Assert.False(world!.IsClient());
    }
}
