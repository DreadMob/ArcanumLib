using System.Collections.Generic;
using System.Globalization;
using ArcanumLib.Actions;
using NSubstitute;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ActionConditionTests
{
    [Fact]
    public void Evaluate_NullContext_ReturnsFalse()
    {
        var condition = new ActionCondition { Type = ActionConditionType.Always };

        Assert.False(condition.Evaluate(null!));
    }

    [Fact]
    public void Evaluate_Always_ReturnsTrue()
    {
        var condition = new ActionCondition { Type = ActionConditionType.Always };

        Assert.True(condition.Evaluate(CreateContext()));
    }

    [Fact]
    public void Evaluate_MinValue_MissingKey_ReturnsFalse()
    {
        var condition = new ActionCondition { Type = ActionConditionType.MinValue, Key = "gold", Value = "10" };
        var context = CreateContext(extra: new());

        Assert.False(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_MinValue_AboveThreshold_ReturnsTrue()
    {
        var condition = new ActionCondition { Type = ActionConditionType.MinValue, Key = "gold", Value = "10" };
        var context = CreateContext(extra: new() { ["gold"] = "15" });

        Assert.True(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_MinValue_BelowThreshold_ReturnsFalse()
    {
        var condition = new ActionCondition { Type = ActionConditionType.MinValue, Key = "gold", Value = "10.5" };
        var context = CreateContext(extra: new() { ["gold"] = "10.4" });

        Assert.False(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_MaxValue_AboveThreshold_ReturnsFalse()
    {
        var condition = new ActionCondition { Type = ActionConditionType.MaxValue, Key = "heat", Value = "100" };
        var context = CreateContext(extra: new() { ["heat"] = 101 });

        Assert.False(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_MaxValue_AtThreshold_ReturnsTrue()
    {
        var condition = new ActionCondition { Type = ActionConditionType.MaxValue, Key = "heat", Value = "100" };
        var context = CreateContext(extra: new() { ["heat"] = 100 });

        Assert.True(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_MinValue_DoubleInNonInvariantCulture_Works()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

            var condition = new ActionCondition { Type = ActionConditionType.MinValue, Key = "gold", Value = "1" };
            var context = CreateContext(extra: new() { ["gold"] = 1.5 });

            Assert.True(condition.Evaluate(context));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void Evaluate_HasKey_Present_ReturnsTrue()
    {
        var condition = new ActionCondition { Type = ActionConditionType.HasKey, Key = "flag" };
        var context = CreateContext(extra: new() { ["flag"] = true });

        Assert.True(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_HasKey_Missing_ReturnsFalse()
    {
        var condition = new ActionCondition { Type = ActionConditionType.HasKey, Key = "flag" };
        var context = CreateContext(extra: new());

        Assert.False(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Equals_CaseInsensitive_ReturnsTrue()
    {
        var condition = new ActionCondition { Type = ActionConditionType.Equals, Key = "role", Value = "Admin" };
        var context = CreateContext(extra: new() { ["role"] = "admin" });

        Assert.True(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Equals_Different_ReturnsFalse()
    {
        var condition = new ActionCondition { Type = ActionConditionType.Equals, Key = "role", Value = "admin" };
        var context = CreateContext(extra: new() { ["role"] = "user" });

        Assert.False(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Permission_PlayerHasPrivilege_ReturnsTrue()
    {
        var player = Substitute.For<IServerPlayer>();
        player.HasPrivilege("build").Returns(true);
        var condition = new ActionCondition { Type = ActionConditionType.Permission, Value = "build" };

        Assert.True(condition.Evaluate(CreateContext(player)));
    }

    [Fact]
    public void Evaluate_Permission_MissingPrivilege_ReturnsFalse()
    {
        var player = Substitute.For<IServerPlayer>();
        player.HasPrivilege("build").Returns(false);
        var condition = new ActionCondition { Type = ActionConditionType.Permission, Value = "build" };

        Assert.False(condition.Evaluate(CreateContext(player)));
    }

    [Fact]
    public void Evaluate_Permission_NoPlayer_ReturnsFalse()
    {
        var condition = new ActionCondition { Type = ActionConditionType.Permission, Value = "build" };

        Assert.False(condition.Evaluate(CreateContext(player: null)));
    }

    [Fact]
    public void Evaluate_All_AllTrue_ReturnsTrue()
    {
        var condition = new ActionCondition
        {
            Type = ActionConditionType.All,
            Conditions = new()
            {
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "a" },
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "b" }
            }
        };
        var context = CreateContext(extra: new() { ["a"] = 1, ["b"] = 2 });

        Assert.True(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_All_OneFalse_ReturnsFalse()
    {
        var condition = new ActionCondition
        {
            Type = ActionConditionType.All,
            Conditions = new()
            {
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "a" },
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "b" }
            }
        };
        var context = CreateContext(extra: new() { ["a"] = 1 });

        Assert.False(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Any_OneTrue_ReturnsTrue()
    {
        var condition = new ActionCondition
        {
            Type = ActionConditionType.Any,
            Conditions = new()
            {
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "a" },
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "b" }
            }
        };
        var context = CreateContext(extra: new() { ["b"] = 2 });

        Assert.True(condition.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Any_NoneTrue_ReturnsFalse()
    {
        var condition = new ActionCondition
        {
            Type = ActionConditionType.Any,
            Conditions = new()
            {
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "a" },
                new ActionCondition { Type = ActionConditionType.HasKey, Key = "b" }
            }
        };

        Assert.False(condition.Evaluate(CreateContext(extra: new())));
    }

    [Fact]
    public void Evaluate_Not_EmptyConditions_ReturnsTrue()
    {
        var condition = new ActionCondition { Type = ActionConditionType.Not };

        Assert.True(condition.Evaluate(CreateContext()));
    }

    [Fact]
    public void Evaluate_Not_InvertsNested()
    {
        var condition = new ActionCondition
        {
            Type = ActionConditionType.Not,
            Conditions = new() { new ActionCondition { Type = ActionConditionType.HasKey, Key = "a" } }
        };

        Assert.True(condition.Evaluate(CreateContext(extra: new())));
        Assert.False(condition.Evaluate(CreateContext(extra: new() { ["a"] = 1 })));
    }

    private static ActionContext CreateContext(
        IServerPlayer? player = null,
        Dictionary<string, object>? extra = null)
    {
        var sapi = Substitute.For<ICoreServerAPI>();
        var context = new ActionContext(sapi, player, null, null, null);
        if (extra != null)
        {
            foreach (var kv in extra)
                context.Extra[kv.Key] = kv.Value;
        }
        return context;
    }
}
