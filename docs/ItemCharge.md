---
layout: default
title: ItemCharge
---

# ItemCharge

Generic helpers for item charge, drain, refuel, and stat gating.

## What is it for?

Use `ItemCharge` when an item has a limited resource that is consumed over time or per use:

- A sword that loses charge with each hit.
- A ring that consumes hours of magical energy.
- Armor whose protection drops as its charge depletes.
- A lantern that can be refueled with specific materials.

It supports generic `charge` attributes and legacy time/use suffixes, with configurable metadata for max charge, refuel materials, charge-per-unit, and charge-gated stats.

## Configuration

Most consumers only need `ItemChargeConfig` once:

```csharp
var config = new ItemChargeConfig
{
    AttributePrefix = "mymod:",
    MetaPrefix = "mymod:",
    Logger = api.Logger,
    DisplayNameResolver = shortKey => $"{shortKey} charge"
};
```

| Property | What it controls |
|----------|-----------------|
| `AttributePrefix` | Namespace for the current charge value (`mymod:charge`). |
| `MetaPrefix` | Namespace for max-charge, material, and gating metadata. |
| `LegacyAttributePrefixes` | Optional old namespaces to still read from. |
| `DisplayNameResolver` | How the charge name appears to the player. |
| `Logger` | Where parse warnings go. |

## Attribute keys

| Concept | Generic key | Legacy example |
|---------|-------------|----------------|
| Current charge | `{AttributePrefix}charge` | `chargehours`, `chargeuses` |
| Maximum charge | `{MetaPrefix}chargemax` | `oldmod:chargemax` |
| Charge per material unit | `{MetaPrefix}chargeperunit` | `oldmod:chargeperunit` |
| Accepted refuel patterns (JSON) | `{MetaPrefix}chargematerials` | `oldmod:chargematerials` |
| Charge mode (`all` / `partial`) | `{MetaPrefix}chargemode` | `oldmod:chargemode` |
| Gated attributes (JSON) | `{MetaPrefix}chargegatedattrs` | `oldmod:chargegatedattrs` |
| Depleted multiplier | `{MetaPrefix}chargedepletedmult` | `oldmod:chargedepletedmult` |

## Quick example

```csharp
using ArcanumLib.Items;

float charge = ItemCharge.GetChargeValue(stack, config);

// Consume one "use"
if (ItemCharge.TryConsumeCharge(stack, 1f, config))
{
    // do the charged action
}
```

## Usage

### Read current charge

```csharp
float charge = ItemCharge.GetChargeValue(stack, config);
float max    = ItemCharge.GetChargeMax(stack, config);
float pct    = ItemCharge.GetChargePercentage(stack, config);
```

### Consume charge

```csharp
if (ItemCharge.TryConsumeCharge(stack, 1f, config))
{
    // one use consumed
}
```

### Refuel

```csharp
if (ItemCharge.CanRechargeWith(stack, fuelStack, config))
{
    ItemCharge.TryRecharge(stack, out int consumedUnits, config);
    slot.MarkDirty();
}
```

### Drain time-based charge

```csharp
// elapsedHours is the number of in-game hours since the last tick
ItemCharge.TryDrainTimeCharge(stack, elapsedHours, config);
```

### Gate a stat by charge

Only time-based charges gate stats by default.

```csharp
if (ItemCharge.TryGetChargeGatingMultiplier(stack, "walkspeed", out float mult, config))
{
    entity.Stats.Set("walkspeed", "mymodChargeGate", mult, false);
}
```

## Charge gating

- `chargemode = "all"` gates every stat.
- `chargemode = "partial"` only gates attributes listed in `chargegatedattrs`.
- The multiplier is linear from `MinActiveMultiplier` to `MaxActiveMultiplier` over `FullChargeThreshold` hours.
- Below `DepletedThreshold`, `chargedepletedmult` is used.

## Error handling

Malformed `chargematerials` or `chargegatedattrs` JSON does not throw. The helper returns a safe default and logs a warning through `config.Logger`.
