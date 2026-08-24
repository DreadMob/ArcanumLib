using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// Condition type for declarative action gating.
/// </summary>
public enum ActionConditionType
{
    /// <summary>Always true. Useful for debugging.</summary>
    Always,
    /// <summary>Checks a numeric value from <see cref="ActionContext.Extra" /> against a minimum.</summary>
    MinValue,
    /// <summary>Checks a numeric value from <see cref="ActionContext.Extra" /> against a maximum.</summary>
    MaxValue,
    /// <summary>Checks that a key exists in <see cref="ActionContext.Extra" />.</summary>
    HasKey,
    /// <summary>Checks that a string value in <see cref="ActionContext.Extra" /> equals the expected value.</summary>
    Equals,
    /// <summary>Checks the player has the given privilege.</summary>
    Permission,
    /// <summary>Logical AND of nested conditions.</summary>
    All,
    /// <summary>Logical OR of nested conditions.</summary>
    Any,
    /// <summary>Logical NOT of a nested condition.</summary>
    Not
}

/// <summary>
/// A declarative condition attached to an <see cref="ActionDescriptor" />.
/// Evaluated before the action handler runs.
/// </summary>
public class ActionCondition
{
    /// <summary>The condition type.</summary>
    [JsonProperty("type")]
    public ActionConditionType Type { get; set; } = ActionConditionType.Always;

    /// <summary>The key in <see cref="ActionContext.Extra" /> to check.</summary>
    [JsonProperty("key")]
    public string? Key { get; set; }

    /// <summary>The expected or threshold value.</summary>
    [JsonProperty("value")]
    public string? Value { get; set; }

    /// <summary>Nested conditions for All/Any/Not.</summary>
    [JsonProperty("conditions")]
    public List<ActionCondition> Conditions { get; set; } = new();

    /// <summary>
    /// Evaluates the condition against the given context.
    /// </summary>
    /// <param name="context">The operation context.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public bool Evaluate(ActionContext context)
    {
        if (context == null) return false;

        switch (Type)
        {
            case ActionConditionType.Always:
                return true;

            case ActionConditionType.MinValue:
                if (string.IsNullOrEmpty(Key) || !context.Extra.TryGetValue(Key, out var minObj)) return false;
                if (!TryToDouble(minObj, out var minVal) || !TryToDouble(Value, out var threshold)) return false;
                return minVal >= threshold;

            case ActionConditionType.MaxValue:
                if (string.IsNullOrEmpty(Key) || !context.Extra.TryGetValue(Key, out var maxObj)) return false;
                if (!TryToDouble(maxObj, out var maxVal) || !TryToDouble(Value, out var thresholdMax)) return false;
                return maxVal <= thresholdMax;

            case ActionConditionType.HasKey:
                if (string.IsNullOrEmpty(Key)) return false;
                return context.Extra.ContainsKey(Key);

            case ActionConditionType.Equals:
                if (string.IsNullOrEmpty(Key) || !context.Extra.TryGetValue(Key, out var eqObj)) return false;
                return string.Equals(Convert.ToString(eqObj), Value, StringComparison.OrdinalIgnoreCase);

            case ActionConditionType.Permission:
                if (string.IsNullOrEmpty(Value) || context.Player == null) return false;
                return context.Player.HasPrivilege(Value);

            case ActionConditionType.All:
                foreach (var sub in Conditions)
                {
                    if (!sub.Evaluate(context)) return false;
                }
                return true;

            case ActionConditionType.Any:
                foreach (var sub in Conditions)
                {
                    if (sub.Evaluate(context)) return true;
                }
                return false;

            case ActionConditionType.Not:
                if (Conditions.Count == 0) return true;
                return !Conditions[0].Evaluate(context);

            default:
                return true;
        }
    }

    private static bool TryToDouble(object? obj, out double result)
    {
        result = 0;
        if (obj == null) return false;
        return double.TryParse(Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
    }
}
