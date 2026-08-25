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
    /// <summary>True when there are no errors.</summary>
    public bool IsSuccess => Errors == null || Errors.Count == 0;
    /// <summary>True when at least one warning was recorded.</summary>
    public bool HasWarnings => Warnings != null && Warnings.Count > 0;
    /// <summary>True when at least one error was recorded.</summary>
    public bool HasErrors => Errors != null && Errors.Count > 0;

    /// <summary>Recorded error messages, if any.</summary>
    public IReadOnlyList<string> Errors { get; }
    /// <summary>Recorded warning messages, if any.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Creates an empty successful result.</summary>
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

    /// <summary>Returns a successful result with no errors or warnings.</summary>
    /// <returns>A successful <see cref="ValidationResult" />.</returns>
    public static ValidationResult Success() => new();

    /// <summary>Returns a failed result with a single error message.</summary>
    /// <param name="message">The error message.</param>
    /// <returns>A failed <see cref="ValidationResult" />.</returns>
    public static ValidationResult Error(string message) =>
        new(new[] { message }, null);

    /// <summary>Returns a successful result with a single warning message.</summary>
    /// <param name="message">The warning message.</param>
    /// <returns>A <see cref="ValidationResult" /> with one warning.</returns>
    public static ValidationResult Warning(string message) =>
        new(null, new[] { message });

    /// <summary>Returns a failed result from a collection of error messages.</summary>
    /// <param name="messages">The error messages.</param>
    /// <returns>A failed <see cref="ValidationResult" />.</returns>
    public static ValidationResult FromErrors(IEnumerable<string> messages) =>
        new(messages.ToList(), null);

    /// <summary>Returns a successful result from a collection of warning messages.</summary>
    /// <param name="messages">The warning messages.</param>
    /// <returns>A <see cref="ValidationResult" /> with warnings.</returns>
    public static ValidationResult FromWarnings(IEnumerable<string> messages) =>
        new(null, messages.ToList());

    /// <summary>
    /// Combines this result with another, merging errors and warnings.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The combine.</returns>
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
    /// <param name="message">The message.</param>
    /// <returns>The with error.</returns>
    public ValidationResult WithError(string message)
    {
        var errors = new List<string>(Errors) { message };
        return new ValidationResult(errors, Warnings);
    }

    /// <summary>
    /// Adds a warning to this result.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The with warning.</returns>
    public ValidationResult WithWarning(string message)
    {
        var warnings = new List<string>(Warnings) { message };
        return new ValidationResult(Errors, warnings);
    }

    /// <summary>
    /// Logs errors and warnings through the given logger.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="context">The operation context.</param>
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

    /// <summary>Returns a string that represents the current object.</summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        if (HasErrors)
            return $"Errors: {Errors.Count} (Warnings: {Warnings.Count})";
        if (HasWarnings)
            return $"Warnings: {Warnings.Count}";
        return "Success";
    }

    /// <summary>Combines two results, merging their errors and warnings.</summary>
    /// <param name="a">The first result.</param>
    /// <param name="b">The second result.</param>
    /// <returns>A combined <see cref="ValidationResult" />.</returns>
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
