using ArcanumLib.Validation;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ValidationResultTests
{
    [Fact]
    public void Success_HasNoErrorsOrWarnings()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void Error_HasErrors()
    {
        var result = ValidationResult.Error("boom");

        Assert.False(result.IsSuccess);
        Assert.True(result.HasErrors);
        Assert.Single(result.Errors);
        Assert.Equal("boom", result.Errors[0]);
    }

    [Fact]
    public void Warning_HasWarnings()
    {
        var result = ValidationResult.Warning("careful");

        Assert.True(result.IsSuccess);
        Assert.True(result.HasWarnings);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void WithError_AddsError()
    {
        var result = ValidationResult.Success().WithError("e1").WithError("e2");

        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void WithWarning_AddsWarning()
    {
        var result = ValidationResult.Success().WithWarning("w1");

        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Combine_MergesErrorsAndWarnings()
    {
        var a = ValidationResult.Error("e1").WithWarning("w1");
        var b = ValidationResult.Error("e2").WithWarning("w2");

        var combined = a.Combine(b);

        Assert.Equal(2, combined.Errors.Count);
        Assert.Equal(2, combined.Warnings.Count);
    }

    [Fact]
    public void OperatorPlus_Combines()
    {
        var a = ValidationResult.Error("e1");
        var b = ValidationResult.Error("e2");

        var c = a + b;

        Assert.Equal(2, c.Errors.Count);
    }

    [Fact]
    public void FromErrors_CollectsMessages()
    {
        var result = ValidationResult.FromErrors(new[] { "a", "b", "c" });

        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void ToString_Success()
    {
        Assert.Equal("Success", ValidationResult.Success().ToString());
    }

    [Fact]
    public void ToString_WithWarning()
    {
        Assert.Equal("Warnings: 1", ValidationResult.Warning("w").ToString());
    }

    [Fact]
    public void ToString_WithError()
    {
        Assert.Equal("Errors: 1 (Warnings: 0)", ValidationResult.Error("e").ToString());
    }
}
