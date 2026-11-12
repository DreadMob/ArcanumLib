---
layout: default
title: Player animations
nav_order: 21
---

# Player animations

`ArcanumLib.PlayerAnimations` — custom player animations without patching the seraph shape. Any mod or content pack drops JSON into `config/playeranimations/`; the client behavior injects the keyframes into whatever shape the player uses (vanilla seraph, PlayerModelLib models) when it is tesselated.

## Authoring

`assets/<domain>/config/playeranimations/*.json`:

```json
{ "animations": [
  {
    "meta": { "code": "mymod-castbolt", "animation": "mymod-castbolt", "weight": 10,
              "blendMode": "Average", "easeInSpeed": 4, "easeOutSpeed": 4, "onAnimationEnd": "EaseOut" },
    "animation": { "name": "castbolt", "code": "mymod-castbolt", "quantityframes": 30,
                   "onActivityStopped": "EaseOut", "keyframes": [ { "frame": 0, "elements": { ... } } ] }
  }
] }
```

- `meta` is what `player.json` would hold for the animation (blend modes, weights, ease speeds); `animation` is what the shape would hold (keyframes).
- An entry whose code ends with `-fp` is the first-person variant of the one without it and is picked by the game automatically.
- Codes are lowercase; keyframes must stay below `quantityframes` (loops wrap by themselves).
- Files are read from every asset domain, not per mod id.

ArcanumLib adds the behavior `alegacyplayeranimations` to the player entity itself (patch in its own assets) — nothing to patch in your mod.

## Playing

```csharp
var anims = api.ModLoader.GetModSystem<PlayerAnimationSystem>();

// Client: play on the local player at once and relay to everyone else.
anims.PlayLocal("mymod-castbolt", "mymod-chargeloop");   // start, then codes to stop first

// Server: make clients play it on this player (scripted scenes, server-driven actions).
PlayerAnimationSystem.SetServer(player.Entity, "mymod-castbolt", stop: "", includeSelf: true);
```

The server relays only codes it knows from the JSON files, so a client cannot make others play arbitrary animations. The server itself never animates. Commands travel in the watched attribute `alegacyvsquest:playeranim` with a sequence number, so the same code twice in a row restarts.

## Notes

- The attribute and channel names carry the Quest Framework prefix the system was born with; they are kept for compatibility with saved players.
- `Metas` and `Animations` expose what was loaded.
