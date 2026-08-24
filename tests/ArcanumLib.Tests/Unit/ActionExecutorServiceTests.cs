using System;
using ArcanumLib.Actions;
using ArcanumLib.Core;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

[Collection("ArcanumServices")]
public class ActionExecutorServiceTests : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly IServerWorldAccessor _world;

    public ActionExecutorServiceTests()
    {
        ArcanumServices.Shutdown();
        ArcanumServices.Register(new ActionRegistryService());

        _world = Substitute.For<IServerWorldAccessor>();
        _sapi = Substitute.For<ICoreServerAPI>();
        _sapi.World.Returns(_world);
    }

    public void Dispose()
    {
        ArcanumServices.Shutdown();
    }

    [Fact]
    public void Execute_NullDescriptor_ReturnsInvalid()
    {
        var service = new ActionExecutorService(_sapi);
        var result = service.Execute(null!, CreateContext());

        Assert.Equal(ActionOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Execute_RequiredPermissionMissing_ReturnsNotAvailable()
    {
        var service = new ActionExecutorService(_sapi);
        var player = Substitute.For<IServerPlayer>();
        player.HasPrivilege("build").Returns(false);

        var result = service.Execute(new ActionDescriptor
        {
            Id = "dig",
            RequiredPermission = "build"
        }, CreateContext(player));

        Assert.Equal(ActionOutcome.NotAvailable, result.Outcome);
    }

    [Fact]
    public void Execute_ConditionFalse_ReturnsNotAvailable()
    {
        var service = new ActionExecutorService(_sapi);
        var result = service.Execute(new ActionDescriptor
        {
            Id = "greet",
            Condition = new ActionCondition { Type = ActionConditionType.HasKey, Key = "flag" }
        }, CreateContext());

        Assert.Equal(ActionOutcome.NotAvailable, result.Outcome);
    }

    [Fact]
    public void Execute_HandlerNotFound_ReturnsHandlerNotFound()
    {
        var service = new ActionExecutorService(_sapi);
        var result = service.Execute(new ActionDescriptor { Id = "unknown" }, CreateContext());

        Assert.Equal(ActionOutcome.HandlerNotFound, result.Outcome);
    }

    [Fact]
    public void Execute_Success_ReturnsSuccess()
    {
        var service = new ActionExecutorService(_sapi);
        var handler = CreateHandler("wave");
        ActionRegistry.Register(handler);

        var result = service.Execute(new ActionDescriptor { Id = "wave" }, CreateContext());

        Assert.Equal(ActionOutcome.Success, result.Outcome);
    }

    [Fact]
    public void Execute_Success_PassesDescriptorArgsToContext()
    {
        var service = new ActionExecutorService(_sapi);
        IReadOnlyList<string>? capturedArgs = null;

        var handler = CreateHandler("say");
        handler.Execute(Arg.Do<ActionContext>(ctx => capturedArgs = ctx.Args))
            .Returns(ActionResult.Success());
        ActionRegistry.Register(handler);

        service.Execute(new ActionDescriptor { Id = "say", Args = new[] { "hello", "world" } }, CreateContext());

        Assert.NotNull(capturedArgs);
        Assert.Equal(new[] { "hello", "world" }, capturedArgs);
    }

    [Fact]
    public void GetRemainingCooldown_NoCooldown_ReturnsZero()
    {
        var service = new ActionExecutorService(_sapi);

        Assert.Equal(0, service.GetRemainingCooldown(1, "action"));
    }

    [Fact]
    public void GetRemainingCooldown_Active_ReturnsRemaining()
    {
        _world.ElapsedMilliseconds.Returns(1000L);

        var service = new ActionExecutorService(_sapi);
        var player = CreatePlayerWithEntity(7);
        var handler = CreateHandler("spell");
        ActionRegistry.Register(handler);

        service.Execute(new ActionDescriptor
        {
            Id = "spell",
            CooldownMs = 5000
        }, CreateContext(player));

        _world.ElapsedMilliseconds.Returns(2500L);

        Assert.Equal(3500, service.GetRemainingCooldown(7, "spell", _sapi));
    }

    [Fact]
    public void GetRemainingCooldown_Expired_ReturnsZero()
    {
        _world.ElapsedMilliseconds.Returns(0L);

        var service = new ActionExecutorService(_sapi);
        var player = CreatePlayerWithEntity(8);
        var handler = CreateHandler("shout");
        ActionRegistry.Register(handler);

        service.Execute(new ActionDescriptor
        {
            Id = "shout",
            CooldownMs = 1000
        }, CreateContext(player));

        _world.ElapsedMilliseconds.Returns(2000L);

        Assert.Equal(0, service.GetRemainingCooldown(8, "shout", _sapi));
    }

    [Fact]
    public void GetRemainingCooldown_WhitespaceAction_ReturnsZero()
    {
        var service = new ActionExecutorService(_sapi);
        Assert.Equal(0, service.GetRemainingCooldown(1, " ", _sapi));
    }

    [Fact]
    public void ClearCooldowns_RemovesPlayerCooldowns()
    {
        _world.ElapsedMilliseconds.Returns(0L);

        var service = new ActionExecutorService(_sapi);
        var player = CreatePlayerWithEntity(9);
        var handler = CreateHandler("jump");
        ActionRegistry.Register(handler);

        service.Execute(new ActionDescriptor { Id = "jump", CooldownMs = 10000 }, CreateContext(player));
        service.ClearCooldowns(9);

        Assert.Equal(0, service.GetRemainingCooldown(9, "jump", _sapi));
    }

    [Fact]
    public void ClearAllCooldowns_RemovesEverything()
    {
        _world.ElapsedMilliseconds.Returns(0L);

        var service = new ActionExecutorService(_sapi);
        var player = CreatePlayerWithEntity(10);
        var handler = CreateHandler("roll");
        ActionRegistry.Register(handler);

        service.Execute(new ActionDescriptor { Id = "roll", CooldownMs = 10000 }, CreateContext(player));
        service.ClearAllCooldowns();

        Assert.Equal(0, service.GetRemainingCooldown(10, "roll", _sapi));
    }

    private static IActionHandler CreateHandler(string id)
    {
        var handler = Substitute.For<IActionHandler>();
        handler.Id.Returns(id);
        handler.IsAvailable(Arg.Any<ActionContext>()).Returns(true);
        handler.Execute(Arg.Any<ActionContext>()).Returns(ActionResult.Success());
        return handler;
    }

    private ActionContext CreateContext(IServerPlayer? player = null)
    {
        return new ActionContext(_sapi, player, null, null, null);
    }

    private static IServerPlayer CreatePlayerWithEntity(long entityId)
    {
        var player = Substitute.For<IServerPlayer>();
        var entity = Substitute.For<EntityPlayer>();
        entity.EntityId = entityId;
        player.Entity.Returns(entity);
        return player;
    }
}
