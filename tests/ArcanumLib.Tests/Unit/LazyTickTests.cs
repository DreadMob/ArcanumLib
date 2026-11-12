#nullable enable
using System;
using System.Reflection;
using ArcanumLib.Core;
using ArcanumLib.Effects;
using ArcanumLib.PlayerTransfer;
using ArcanumLib.Spatial;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

/// <summary>Subsystems with no user cost nothing: their tick listeners start with the first user and stop with the last.</summary>
[Collection("ArcanumServices")]
public class LazyTickTests : IDisposable
{
    private Action<float>? _tick;

    public LazyTickTests()
    {
        ArcanumRuntime.Activate();
        var resistance = new EffectResistanceService();
        ArcanumServices.Register<IEffectResistanceService>(resistance);
    }

    public void Dispose() => ArcanumRuntime.Current?.Dispose();

    private ICoreServerAPI Sapi()
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        sapi.Event.RegisterGameTickListener(Arg.Any<Action<float>>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(ci => { _tick = ci.Arg<Action<float>>(); return 7L; });
        sapi.World.AllOnlinePlayers.Returns(Array.Empty<IPlayer>());
        return sapi;
    }

    [Fact]
    public void ZoneTracker_TicksOnlyWhileZonesExist()
    {
        var sapi = Sapi();
        var tracker = new PlayerZoneTracker();
        tracker.StartServerSide(sapi);
        try
        {
            Assert.False(tracker.IsTicking);
            sapi.Event.DidNotReceiveWithAnyArgs().RegisterGameTickListener(default(Action<float>)!, 0, 0);

            PlayerZoneTracker.Register("z", new SphereZoneShape { Center = new Vec3d(0, 0, 0), Radius = 4 });
            Assert.True(tracker.IsTicking);

            PlayerZoneTracker.Unregister("z");
            Assert.False(tracker.IsTicking);
            sapi.Event.Received(1).UnregisterGameTickListener(7L);
        }
        finally { tracker.Dispose(); }
    }

    [Fact]
    public void InLandTracker_TicksOnlyWhileSubscribed()
    {
        var sapi = Sapi();
        var tracker = new PlayerInLandTracker();
        tracker.StartServerSide(sapi);
        Action<string, string?, string?> onChange = (_, _, _) => { };
        try
        {
            Assert.False(tracker.IsTicking);

            PlayerInLandTracker.PlayerClaimChanged += onChange;
            Assert.True(tracker.IsTicking);

            PlayerInLandTracker.PlayerClaimChanged -= onChange;
            Assert.False(tracker.IsTicking);
            sapi.Event.Received(1).UnregisterGameTickListener(7L);
        }
        finally
        {
            PlayerInLandTracker.PlayerClaimChanged -= onChange;
            tracker.Dispose();
        }
    }

    [Fact]
    public void StatusEffects_TickFromFirstApplyUntilEmpty()
    {
        var sapi = Sapi();
        var system = new StatusEffectModSystem();
        system.StartServerSide(sapi);
        try
        {
            Assert.False(system.IsTicking);

            var service = ArcanumServices.Get<IStatusEffectService>(ArcanumServiceScope.Server)!;
            var entity = new DummyEntity(11);
            service.Apply(entity, Effect("short"), 500);
            Assert.True(system.IsTicking);

            _tick!(1f);   // the effect runs out; the service is empty again
            Assert.False(system.IsTicking);
            sapi.Event.Received(1).UnregisterGameTickListener(7L);
        }
        finally { system.Dispose(); }
    }

    [Fact]
    public void PlayerTransfer_RegistryEmptiedWhenServerStops()
    {
        // a started system (StartServerSide also wires chat commands, which need the real parsers)
        var system = new PlayerTransferModSystem();
        typeof(PlayerTransferModSystem).GetField("sapi", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(system, Sapi());

        PlayerDataTransfer.Register("lazytest", _ => { });
        PlayerDataTransfer.RegisterEntityAttributePrefix("lazytest:");
        PlayerDataTransfer.RegisterAfterTransfer("lazytest-site", (_, _) => { });
        Assert.NotEmpty(PlayerDataTransfer.All);

        system.Dispose();

        Assert.Empty(PlayerDataTransfer.All);
        Assert.Empty(PlayerDataTransfer.EntityAttributePrefixes);
        Assert.Empty(PlayerDataTransfer.AfterTransferSteps);
    }

    private static IStatusEffect Effect(string code)
    {
        var effect = Substitute.For<IStatusEffect>();
        effect.Code.Returns(code);
        effect.StackMode.Returns(EnumStackMode.Refresh);
        effect.MaxStacks.Returns(1);
        effect.Tags.Returns(new[] { code });
        effect.HasTick.Returns(false);
        return effect;
    }

    private sealed class DummyEntity : Entity
    {
        public DummyEntity(long id) { EntityId = id; Class = "dummy"; }
    }
}
