---
layout: default
title: ValidationResult
---

# ValidationResult

A lightweight, immutable result object for validation and parse pipelines.
Collects errors and warnings so they can be accumulated across many steps and
logged together at the end of an operation.

## Usage

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

## API

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
