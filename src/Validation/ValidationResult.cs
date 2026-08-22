using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ArcanumLib.Validation;

/// <summary>
/// A lightweight, immutable result object for validation and parse pipelines.
/// Collects errors and warnings so they can be accumulated across many steps and
/// logged together at the end of an operation.
/// </summary>
public readonly struct ValidationResult
{
    public bool IsSuccess => Errors == null || Errors.Count == 0;
    public bool HasWarnings => Warnings != null && Warnings.Count > 0;
    public bool HasErrors => Errors != null && Errors.Count > 0;

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }

    public ValidationResult()
    {
        Errors = [];
        Warnings = [];
    }

    private ValidationResult(IReadOnlyList<string>? errors, IReadOnlyList<string>? warnings)
    {
        Errors = errors ?? [];
        Warnings = warnings ?? [];
    }

    public static ValidationResult Success() => new();

    public static ValidationResult Error(string message) =>
        new(new[] { message }, null);

    public static ValidationResult Warning(string message) =>
        new(null, new[] { message });

    public static ValidationResult FromErrors(IEnumerable<string> messages) =>
        new(messages.ToList(), null);

    public static ValidationResult FromWarnings(IEnumerable<string> messages) =>
        new(null, messages.ToList());

    /// <summary>
    /// Combines this result with another, merging errors and warnings.
    /// </summary>
    public ValidationResult Combine(ValidationResult other)
    {
        if (IsSuccess && !HasWarnings && other.IsSuccess && !other.HasWarnings)
            return this;

        var errors = Merge(Errors, other.Errors);
        var warnings = Merge(Warnings, other.Warnings);

        return new ValidationResult(errors, warnings);
    }

    /// <summary>
    /// Adds an error to this result.
    /// </summary>
    public ValidationResult WithError(string message)
    {
        var errors = new List<string>(Errors) { message };
        return new ValidationResult(errors, Warnings);
    }

    /// <summary>
    /// Adds a warning to this result.
    /// </summary>
    public ValidationResult WithWarning(string message)
    {
        var warnings = new List<string>(Warnings) { message };
        return new ValidationResult(Errors, warnings);
    }

    /// <summary>
    /// Logs errors and warnings through the given logger.
    /// </summary>
    public void Log(ILogger? logger, string context)
    {
        if (logger == null) return;

        foreach (var error in Errors)
        {
            logger.Error("[{0}] {1}", context, error);
        }

        foreach (var warning in Warnings)
        {
            logger.Warning("[{0}] {1}", context, warning);
        }
    }

    public override string ToString()
    {
        if (HasErrors)
            return $"Errors: {Errors.Count} (Warnings: {Warnings.Count})";
        if (HasWarnings)
            return $"Warnings: {Warnings.Count}";
        return "Success";
    }

    public static ValidationResult operator +(ValidationResult a, ValidationResult b) => a.Combine(b);

    private static IReadOnlyList<string>? Merge(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0) return b.Count == 0 ? null : b;
        if (b.Count == 0) return a;

        var merged = new List<string>(a.Count + b.Count);
        merged.AddRange(a);
        merged.AddRange(b);
        return merged;
    }
}
