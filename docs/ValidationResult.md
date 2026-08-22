---
layout: default
title: ValidationResult
parent: "ModAssetLoader"
nav_order: 3
---

# ValidationResult

## What is it for?

`ArcanumLib.Validation.ValidationResult` is a lightweight, immutable result object for validation and parse pipelines. It collects errors and warnings so they can be accumulated across many steps and logged together at the end of an operation.

## When to use it

- Validating JSON or other input through several checks.
- Collecting errors and warnings to report at once.
- Returning a validation status from a parse pipeline.
- Combining validation results from multiple sources.

## Quick example

```csharp
using ArcanumLib.Validation;

ValidationResult Validate(JsonObject json)
{
    if (json == null)
        return ValidationResult.Error("JSON is null");

    var result = ValidationResult.Success();

    if (json["name"].AsString() == null)
        result = result.WithError("Missing 'name'");

    if (json["weight"].AsFloat() < 0)
        result = result.WithWarning("Negative weight treated as zero");

    return result;
}

var final = Validate(a) + Validate(b) + Validate(c);

if (final.HasErrors)
    final.Log(api.Logger, "MySystem");
```

## Usage

```csharp
public readonly struct ValidationResult
{
    public bool IsSuccess { get; }
    public bool HasWarnings { get; }
    public bool HasErrors { get; }

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }

    public static ValidationResult Success();
    public static ValidationResult Error(string message);
    public static ValidationResult Warning(string message);

    public ValidationResult Combine(ValidationResult other);
    public ValidationResult WithError(string message);
    public ValidationResult WithWarning(string message);

    public void Log(ILogger? logger, string context);

    public static ValidationResult operator +(ValidationResult a, ValidationResult b);
}
```

| Member | Description |
| --- | --- |
| `Success()` | A result with no errors or warnings. |
| `Error(message)` | A result with a single error. |
| `Warning(message)` | A result with a single warning. |
| `WithError` / `WithWarning` | Returns a new result with the additional message. |
| `Combine` / `+` operator | Merges two results into one. |
| `Log` | Writes warnings and errors to the logger with a context string. |

## Notes

- `ValidationResult` is a `readonly struct`; each mutation returns a new instance.
- `Log` writes all warnings and errors to the supplied logger.
- A result with only warnings is still `IsSuccess == true` but `HasWarnings == true`.