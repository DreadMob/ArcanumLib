namespace ArcanumLib.Definitions;

/// <summary>
/// Marks a data-driven definition that can validate itself after loading.
/// Used by generic loaders to skip invalid JSON entries without crashing the whole pass.
/// </summary>
public interface IValidatableDefinition
{
    /// <summary>Returns true when the definition is valid and can be used.</summary>
    /// <returns>true if valid; otherwise, false.</returns>
    bool IsValid();
}
