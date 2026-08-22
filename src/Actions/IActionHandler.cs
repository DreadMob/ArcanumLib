using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ArcanumLib.Actions;

/// <summary>
/// Result of an action execution.
/// </summary>
public enum ActionOutcome
{
    /// <summary>The action executed successfully.</summary>
    Success,
    /// <summary>The action was not executed because its preconditions failed.</summary>
    NotAvailable,
    /// <summary>The action was rejected by validation (e.g. wrong arguments).</summary>
    Invalid,
    /// <summary>The action handler was not found in the registry.</summary>
    HandlerNotFound,
    /// <summary>The action threw an exception during execution.</summary>
    Failed
}

/// <summary>
/// Outcome of an action execution, including an optional message.
/// </summary>
public readonly struct ActionResult
{
    /// <summary>
    /// Whether the action succeeded.
    /// </summary>
    public bool IsSuccess => Outcome == ActionOutcome.Success;

    /// <summary>
    /// The outcome kind.
    /// </summary>
    public ActionOutcome Outcome { get; }

    /// <summary>
    /// Optional human-readable message describing the outcome.
    /// </summary>
    public string? Message { get; }

    public ActionResult(ActionOutcome outcome, string? message = null)
    {
        Outcome = outcome;
        Message = message;
    }

    public static ActionResult Success(string? msg = null) => new(ActionOutcome.Success, msg);
    public static ActionResult NotAvailable(string? msg = null) => new(ActionOutcome.NotAvailable, msg);
    public static ActionResult Invalid(string? msg = null) => new(ActionOutcome.Invalid, msg);
    public static ActionResult HandlerNotFound(string? msg = null) => new(ActionOutcome.HandlerNotFound, msg);
    public static ActionResult Failed(string? msg = null) => new(ActionOutcome.Failed, msg);
}

/// <summary>
/// Context passed to an action handler during validation and execution.
/// Contains the acting player, the source item stack (if any), and the raw
/// string arguments from the action descriptor.
/// </summary>
public sealed class ActionContext
{
    /// <summary>
    /// The server API. Always non-null when the action is executed on the server.
    /// </summary>
    public ICoreServerAPI Sapi { get; }

    /// <summary>
    /// The player initiating the action. May be null for server-initiated actions.
    /// </summary>
    public IServerPlayer? Player { get; }

    /// <summary>
    /// The entity of the initiating player, if available.
    /// </summary>
    public EntityPlayer? PlayerEntity => Player?.Entity;

    /// <summary>
    /// The item stack from which the action was triggered, if any.
    /// </summary>
    public ItemStack? ItemSlot { get; }

    /// <summary>
    /// The block position targeted by the action, if any.
    /// </summary>
    public Vintagestory.API.MathTools.BlockPos? TargetPos { get; }

    /// <summary>
    /// The raw string arguments from the action descriptor.
    /// </summary>
    public IReadOnlyList<string> Args { get; }

    /// <summary>
    /// Optional extra data bag for mod-specific context.
    /// </summary>
    public Dictionary<string, object> Extra { get; } = new();

    public ActionContext(
        ICoreServerAPI sapi,
        IServerPlayer? player = null,
        ItemStack? itemSlot = null,
        Vintagestory.API.MathTools.BlockPos? targetPos = null,
        IReadOnlyList<string>? args = null)
    {
        Sapi = sapi;
        Player = player;
        ItemSlot = itemSlot;
        TargetPos = targetPos;
        Args = args ?? System.Array.Empty<string>();
    }
}

/// <summary>
/// Handles a single action type identified by its <see cref="Id"/>.
/// Implementations are registered in <see cref="ActionRegistry"/> and invoked
/// when an <see cref="ActionDescriptor"/> with a matching id is executed.
/// </summary>
public interface IActionHandler
{
    /// <summary>
    /// Unique action identifier. Must match the <c>id</c> field in JSON action descriptors.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Returns true if the action can currently be executed in the given context.
    /// Called before <see cref="Execute"/>. Use this for cooldown, permission, and
    /// resource checks. Server-side only.
    /// </summary>
    bool IsAvailable(ActionContext context);

    /// <summary>
    /// Executes the action. Called only when <see cref="IsAvailable"/> returned true.
    /// </summary>
    ActionResult Execute(ActionContext context);
}
