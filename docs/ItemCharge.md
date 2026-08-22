# ItemCharge

`ArcanumLib.Items.ItemCharge` provides generic helpers for item charge, drain, refuel and stat gating. It works directly on `Vintagestory.API.Common.ItemStack.Attributes` and is fully data-driven.

## When to use it

Use `ItemCharge` when your mod needs:

- A numeric charge value on an item (time-based, use-based, or generic).
- Maximum charge capacity and configurable recharge amount per material.
- Refueling by matching the source item code/path against a list of patterns.
- Draining time-based charges over in-game hours.
- Gating other item stats behind the current charge level.

## Configuration

Create an `ItemChargeConfig` to set your attribute key namespace and resolvers.

```csharp
var config = new ItemChargeConfig
{
    AttributePrefix = "mymod:attr:",
    LegacyAttributePrefixes = new List<string>(),
    MetaPrefix = "mymod:",
    LegacyMetaPrefixes = new List<string>(),
    DisplayNameResolver = shortKey => Lang.Get($"mymod:charge-{shortKey}"),
    UnitResolver = shortKey => shortKey.EndsWith("chargehours") ? "h" : ""
};
```

## Attribute keys

The following metadata keys are read in priority order: generic key first, then legacy keys.

| Concept | Generic key | Example legacy key |
|---------|-------------|-------------------|
| Charge value | `{AttributePrefix}charge` | `*chargehours`, `*chargeuses`, unprefixed `charge` |
| Maximum charge | `{MetaPrefix}chargemax` | `oldmod:chargemax` |
| Charge per material unit | `{MetaPrefix}chargeperunit` | `oldmod:chargeperunit` |
| Accepted refuel patterns (JSON) | `{MetaPrefix}chargematerials` | `oldmod:chargematerials` |
| Charge mode (`all`/`partial`) | `{MetaPrefix}chargemode` | `oldmod:chargemode` |
| Gated attributes (JSON array) | `{MetaPrefix}chargegatedattrs` | `oldmod:chargegatedattrs` |
| Depleted multiplier | `{MetaPrefix}chargedepletedmult` | `oldmod:chargedepletedmult` |

## Usage

```csharp
// Read current charge
float charge = ItemCharge.GetChargeValue(stack, config);

// Consume one "use" of charge
ItemCharge.TryConsumeCharge(stack, 1f, config);

// Refuel with a source item if it matches chargematerials
if (ItemCharge.CanRechargeWith(stack, sourceStack, config))
{
    ItemCharge.TryRecharge(stack, out int consumed, config);
}

// Drain time-based charge by elapsed hours
ItemCharge.TryDrainTimeCharge(stack, elapsedHours, config);

// Get stat multiplier if an attribute is charge-gated
if (ItemCharge.TryGetChargeGatingMultiplier(stack, "walkspeed", out float mult, config))
{
    // scale the stat by mult
}
```

## Charge gating

Only **time-based** charges (keys ending with `chargehours`) gate other stats by default.

- `chargemode = "all"` — every stat is gated.
- `chargemode = "partial"` — only attributes listed in `chargegatedattrs` are gated.
- The curve is linear from `MinActiveMultiplier` to `MaxActiveMultiplier` over `FullChargeThreshold` hours.
- When charge is below `DepletedThreshold`, the `chargedepletedmult` is used.

## Error handling

All JSON parsing is wrapped with logging. Invalid `chargematerials` or `chargegatedattrs` JSON falls back to an empty list and a warning is emitted through `config.Logger`.
