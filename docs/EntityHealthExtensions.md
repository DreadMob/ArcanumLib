# EntityHealthExtensions

`ArcanumLib.Common.EntityHealthExtensions` provides health queries and scaling helpers that work through `Entity.WatchedAttributes` or `EntityBehaviorHealth`.

## Quick example

```csharp
using ArcanumLib.Common;

if (entity.TryGetHealthFraction(out float frac))
{
    // frac is 0.0..1.0
}

entity.ScaleHealth(1.5f); // +50% max/current health
```
