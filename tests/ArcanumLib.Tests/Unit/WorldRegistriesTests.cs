#nullable enable
using ArcanumLib.Common;
using ArcanumLib.Core;
using ArcanumLib.Gui.Controls;
using ArcanumLib.Network;
using ArcanumLib.Text;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

/// <summary>Static registries holding one world's state are emptied when the world unloads.</summary>
[Collection("ArcanumServices")]
public class WorldRegistriesTests
{
    public WorldRegistriesTests() => ArcanumLifecycle.DisposeAll();

    [Fact]
    public void LifecycleDispose_ClearsPerWorldRegistries()
    {
        GuiItemFit.RememberNoOutline(EnumItemClass.Item, 4242, false);
        GuiOccluders.Add(ElementBounds.Fixed(0, 0, 10, 10));
        ChatAnnounce.Handler = (_, _) => { };
        var sapi = Substitute.For<ICoreServerAPI>();
        BlockEntityAccessGate.LogDenial(sapi, null, null, BlockEntityAction.InsertExtract, "test");
        var api = Substitute.For<ICoreAPI>();
        api.Side.Returns(EnumAppSide.Server);
        ArcanumChannelHub.GetOrCreate(api, "worldregistries-test");

        Assert.True(GuiItemFit.CachedCount > 0);
        Assert.True(GuiOccluders.Count > 0);
        Assert.True(BlockEntityAccessGate.DenyLogCount > 0);
        Assert.True(ArcanumChannelHub.TryGet(api, "worldregistries-test", out _));

        ArcanumLibModSystem.RegisterWorldRegistries();
        ArcanumLifecycle.DisposeAll();

        Assert.Equal(0, GuiItemFit.CachedCount);
        Assert.Equal(0, GuiOccluders.Count);
        Assert.Null(ChatAnnounce.Handler);
        Assert.Equal(0, BlockEntityAccessGate.DenyLogCount);
        Assert.False(ArcanumChannelHub.TryGet(api, "worldregistries-test", out _));
    }
}
