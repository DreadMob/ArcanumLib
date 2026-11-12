---
layout: default
title: Chat announcements
parent: "Misc Helpers"
nav_order: 30
---

# Chat announcements

`ChatAnnounce` (`ArcanumLib.Text`) — one entry point for server-wide announcements from any mod, with enough context for a per-player filter.

```csharp
ChatAnnounce.Send(sapi, new ChatAnnouncement
{
    Event = "mymod-jackpot",                 // what the players' notification settings match
    Message = $"{player.PlayerName} hit the jackpot!",
    Position = pos.ToVec3d(), Dimension = pos.dimension,
    ParticipantUids = new[] { player.PlayerUID },   // always notified
});
```

Without a filter the message goes to the general chat group as a notification. A mod that has one (the Quest Framework's chat bell) installs `ChatAnnounce.Handler` and decides per player; announcements still arrive as plain broadcasts when that mod is absent.
