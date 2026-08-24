using System;
using System.Collections.Generic;
using ArcanumLib.Actions;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ActionRegistryServiceTests
{
    [Fact]
    public void Register_Null_Throws()
    {
        var registry = new ActionRegistryService();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void Register_EmptyId_Throws()
    {
        var registry = new ActionRegistryService();
        var handler = Substitute.For<IActionHandler>();
        handler.Id.Returns("");

        Assert.Throws<ArgumentException>(() => registry.Register(handler));
    }

    [Fact]
    public void Register_AndGetHandler_ReturnsSame()
    {
        var registry = new ActionRegistryService();
        var handler = CreateHandler("heal");

        registry.Register(handler);

        Assert.Same(handler, registry.GetHandler("heal"));
        Assert.True(registry.IsRegistered("HEAL"));
    }

    [Fact]
    public void Unregister_RemovesHandler()
    {
        var registry = new ActionRegistryService();
        var handler = CreateHandler("jump");

        registry.Register(handler);
        Assert.True(registry.Unregister("jump"));
        Assert.Null(registry.GetHandler("jump"));
    }

    [Fact]
    public void Unregister_Unknown_ReturnsFalse()
    {
        var registry = new ActionRegistryService();
        Assert.False(registry.Unregister("missing"));
    }

    [Fact]
    public void GetRegisteredIds_Snapshot()
    {
        var registry = new ActionRegistryService();
        registry.Register(CreateHandler("a"));
        registry.Register(CreateHandler("b"));

        var ids = registry.GetRegisteredIds();

        Assert.Equal(2, ids.Count);
        Assert.Contains("a", ids);
        Assert.Contains("b", ids);
    }

    [Fact]
    public void Register_ReplacesSameId()
    {
        var registry = new ActionRegistryService();
        var first = CreateHandler("same");
        var second = CreateHandler("same");

        registry.Register(first);
        registry.Register(second);

        Assert.Same(second, registry.GetHandler("same"));
    }

    [Fact]
    public void Validate_NullDescriptor_ReturnsInvalid()
    {
        var registry = new ActionRegistryService();
        var result = registry.Validate(null!, CreateContext());

        Assert.Equal(ActionOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Validate_MissingId_ReturnsInvalid()
    {
        var registry = new ActionRegistryService();
        var result = registry.Validate(new ActionDescriptor { Id = "" }, CreateContext());

        Assert.Equal(ActionOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Validate_HandlerNotFound_ReturnsHandlerNotFound()
    {
        var registry = new ActionRegistryService();
        var result = registry.Validate(new ActionDescriptor { Id = "nope" }, CreateContext());

        Assert.Equal(ActionOutcome.HandlerNotFound, result.Outcome);
    }

    [Fact]
    public void Validate_HandlerNotAvailable_ReturnsNotAvailable()
    {
        var registry = new ActionRegistryService();
        var handler = CreateHandler("closed");
        handler.IsAvailable(Arg.Any<ActionContext>()).Returns(false);
        registry.Register(handler);

        var result = registry.Validate(new ActionDescriptor { Id = "closed" }, CreateContext());

        Assert.Equal(ActionOutcome.NotAvailable, result.Outcome);
    }

    [Fact]
    public void Validate_HandlerAvailable_ReturnsSuccess()
    {
        var registry = new ActionRegistryService();
        var handler = CreateHandler("open");
        handler.IsAvailable(Arg.Any<ActionContext>()).Returns(true);
        registry.Register(handler);

        var result = registry.Validate(new ActionDescriptor { Id = "open" }, CreateContext());

        Assert.Equal(ActionOutcome.Success, result.Outcome);
    }

    [Fact]
    public void Execute_Success_ReturnsHandlerResult()
    {
        var registry = new ActionRegistryService();
        var handler = CreateHandler("cast");
        handler.IsAvailable(Arg.Any<ActionContext>()).Returns(true);
        handler.Execute(Arg.Any<ActionContext>()).Returns(ActionResult.Success("casted"));
        registry.Register(handler);

        var result = registry.Execute(new ActionDescriptor { Id = "cast" }, CreateContext());

        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("casted", result.Message);
    }

    [Fact]
    public void Execute_HandlerThrows_ReturnsFailed()
    {
        var registry = new ActionRegistryService();
        var handler = CreateHandler("fail");
        handler.IsAvailable(Arg.Any<ActionContext>()).Returns(true);
        handler.Execute(Arg.Any<ActionContext>()).Returns(_ => throw new InvalidOperationException("boom"));
        registry.Register(handler);

        var result = registry.Execute(new ActionDescriptor { Id = "fail" }, CreateContext());

        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("boom", result.Message);
    }

    [Fact]
    public void ExecuteAll_StopsOnError()
    {
        var registry = new ActionRegistryService();
        var good = CreateHandler("good");
        good.IsAvailable(Arg.Any<ActionContext>()).Returns(true);
        good.Execute(Arg.Any<ActionContext>()).Returns(ActionResult.Success());

        var bad = CreateHandler("bad");
        bad.IsAvailable(Arg.Any<ActionContext>()).Returns(false);

        registry.Register(good);
        registry.Register(bad);

        var results = registry.ExecuteAll(new[]
        {
            new ActionDescriptor { Id = "good" },
            new ActionDescriptor { Id = "bad" },
            new ActionDescriptor { Id = "good" }
        }, CreateContext());

        Assert.Equal(2, results.Count);
        Assert.Equal(ActionOutcome.Success, results[0].Outcome);
        Assert.Equal(ActionOutcome.NotAvailable, results[1].Outcome);
    }

    [Fact]
    public void ExecuteAll_ContinueOnError_ExecutesAll()
    {
        var registry = new ActionRegistryService();
        var good = CreateHandler("good");
        good.IsAvailable(Arg.Any<ActionContext>()).Returns(true);
        good.Execute(Arg.Any<ActionContext>()).Returns(ActionResult.Success());

        var bad = CreateHandler("bad");
        bad.IsAvailable(Arg.Any<ActionContext>()).Returns(false);

        registry.Register(good);
        registry.Register(bad);

        var results = registry.ExecuteAll(new[]
        {
            new ActionDescriptor { Id = "good" },
            new ActionDescriptor { Id = "bad" },
            new ActionDescriptor { Id = "good" }
        }, CreateContext(), continueOnError: true);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Clear_RemovesAllHandlers()
    {
        var registry = new ActionRegistryService();
        registry.Register(CreateHandler("x"));
        registry.Clear();

        Assert.Empty(registry.GetRegisteredIds());
    }

    private static IActionHandler CreateHandler(string id)
    {
        var handler = Substitute.For<IActionHandler>();
        handler.Id.Returns(id);
        return handler;
    }

    private static ActionContext CreateContext()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        return new ActionContext(sapi);
    }
}
