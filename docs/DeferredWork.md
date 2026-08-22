# DeferredWork

Game-tick scheduler for one-shot, coalesced and end-of-tick work.

Useful when several systems need to react to the same event and you want to
avoid N separate immediate callbacks. Work is collected and then executed in
a controlled order.

See the source for the current API:
- `src/Performance/DeferredWorkScheduler.cs`
