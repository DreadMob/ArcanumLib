using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ArcanumLib.Actions;
using ArcanumLib.Core;
using ArcanumLib.Definitions;
using ArcanumLib.Diagnostics;
using ArcanumLib.Effects;
using ArcanumLib.Events;
using ArcanumLib.Logging;
using ArcanumLib.Progression;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ValidatableDefinitionTests
{
    private class ValidDefinition : IValidatableDefinition
    {
        public bool IsValid() => true;
    }

    private class InvalidDefinition : IValidatableDefinition
    {
        public bool IsValid() => false;
    }

    [Fact]
    public void IsValid_True_ForValidDefinition()
    {
        IValidatableDefinition def = new ValidDefinition();
        Assert.True(def.IsValid());
    }

    [Fact]
    public void IsValid_False_ForInvalidDefinition()
    {
        IValidatableDefinition def = new InvalidDefinition();
        Assert.False(def.IsValid());
    }
}

public class DiagnosticsModSystemTests
{
    [Fact]
    public void ExecuteOrder_IsHighPriority()
    {
        var system = new DiagnosticsModSystem();
        Assert.Equal(1000, system.ExecuteOrder());
    }

    [Fact]
    public void ShouldLoad_ServerSide_ReturnsTrue()
    {
        var system = new DiagnosticsModSystem();
        Assert.True(system.ShouldLoad(EnumAppSide.Server));
    }

    [Fact]
    public void ShouldLoad_ClientSide_ReturnsFalse()
    {
        var system = new DiagnosticsModSystem();
        Assert.False(system.ShouldLoad(EnumAppSide.Client));
    }

    [Fact]
    public void GetMonitorReport_NoSamples_ContainsNoSamplesMessage()
    {
        var monitor = new RuntimeMonitor();
        var report = monitor.GetMonitorReport();

        Assert.Contains("no samples yet", report);
        Assert.Contains("=== ArcanumLib Runtime Monitor ===", report);
    }

    [Fact]
    public void ExtractSummary_NoSummarySection_ReturnsDefaultMessage()
    {
        // ExtractSummary is internal static on DiagnosticsReporter
        var method = typeof(DiagnosticsReporter).GetMethod("ExtractSummary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string)method!.Invoke(null, new object[] { "no summary here" })!;

        Assert.Equal("Diagnostics completed. See server log for details.", result);
    }

    [Fact]
    public void ExtractSummary_WithSummarySection_ReturnsSummary()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("ExtractSummary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var report = "-- Summary --\n  Errors:   0\n  Warnings: 1\n=== End Diagnostics ===";
        var result = (string)method!.Invoke(null, new object[] { report })!;

        Assert.Contains("-- Summary --", result);
        Assert.Contains("Errors:   0", result);
        Assert.Contains("Warnings: 1", result);
    }

    [Fact]
    public void IsVersionSatisfied_EqualVersions_ReturnsTrue()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = (bool)method!.Invoke(null, new object[] { "1.0.0", "1.0.0" })!;

        Assert.True(result);
    }

    [Fact]
    public void IsVersionSatisfied_HigherInstalled_ReturnsTrue()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "2.0.0", "1.0.0" })!;

        Assert.True(result);
    }

    [Fact]
    public void IsVersionSatisfied_LowerInstalled_ReturnsFalse()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "0.9.0", "1.0.0" })!;

        Assert.False(result);
    }

    [Fact]
    public void IsVersionSatisfied_InstalledPreRelease_RequiredRelease_ReturnsFalse()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "1.0.0-rc1", "1.0.0" })!;

        Assert.False(result);
    }

    [Fact]
    public void IsVersionSatisfied_InstalledRelease_RequiredPreRelease_ReturnsTrue()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "1.0.0", "1.0.0-rc1" })!;

        Assert.True(result);
    }

    [Fact]
    public void IsVersionSatisfied_EmptyRequired_ReturnsTrue()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "1.0.0", "" })!;

        Assert.True(result);
    }

    [Fact]
    public void IsVersionSatisfied_EmptyInstalled_ReturnsFalse()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "", "1.0.0" })!;

        Assert.False(result);
    }

    [Fact]
    public void IsVersionSatisfied_BothPreRelease_SameBase_HigherPreRelease_ReturnsTrue()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "1.0.0-rc2", "1.0.0-rc1" })!;

        Assert.True(result);
    }

    [Fact]
    public void IsVersionSatisfied_InvalidInstalled_ReturnsFalse()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("IsVersionSatisfied",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { "not-a-version", "1.0.0" })!;

        Assert.False(result);
    }

    [Fact]
    public void TryParseVersion_ValidVersion_ReturnsTrue()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("TryParseVersion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var args = new object[] { "1.2.3", 0, 0, 0 };
        var result = (bool)method!.Invoke(null, args)!;

        Assert.True(result);
        Assert.Equal(1, args[1]);
        Assert.Equal(2, args[2]);
        Assert.Equal(3, args[3]);
    }

    [Fact]
    public void TryParseVersion_TwoPartVersion_ReturnsTrue()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("TryParseVersion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var args = new object[] { "1.2", 0, 0, 0 };
        var result = (bool)method!.Invoke(null, args)!;

        Assert.True(result);
        Assert.Equal(1, args[1]);
        Assert.Equal(2, args[2]);
        Assert.Equal(0, args[3]);
    }

    [Fact]
    public void TryParseVersion_InvalidVersion_ReturnsFalse()
    {
        var method = typeof(DiagnosticsReporter).GetMethod("TryParseVersion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var args = new object[] { "abc", 0, 0, 0 };
        var result = (bool)method!.Invoke(null, args)!;

        Assert.False(result);
    }
}
