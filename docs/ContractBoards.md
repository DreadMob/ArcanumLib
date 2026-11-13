---
layout: default
title: Contract boards
nav_order: 44
---

# Contract boards

`ArcanumLib.Boards` — boards of contracts ("bring 64 iron ingots — get 200"): players post them with a reward that is taken into escrow at once, others take them, hand the goal in and collect. The core runs every step and keeps the escrow; what a contract asks for (its **goal**) and what it pays besides items (**reward sources**) are extensions other mods register.

## Life of a contract

| State | How it gets there | What happens |
|---|---|---|
| `open` | `Post` (player) or `AddServerContract` | Escrowed stacks are held in the board state. |
| `claimed` | `Accept` | The goal starts tracking for the claimer (`IBoardGoal.OnAccepted`). `Abandon` makes it open again. |
| `fulfilled` | `TurnIn` / `Complete` on a **player** contract | The goal handed in; the claimer collects the escrow, the author collects what the goal delivered (`PendingItemStackBytes`). Leaves the board when both are done. |
| `done` | `TurnIn` / `Complete` on a player contract whose goal has `ConfirmDays > 0` | The taker says the work is done; the author has `ConfirmDays` to answer (`Confirm` → `fulfilled`, `Dispute` with a reason → `disputed`). `SweepExpiry` accepts it when the term passes. Counts as an active post, never expires. |
| `disputed` | `Dispute` (author) | The reward stays in escrow until an admin runs `Resolve` (pay → `fulfilled`, refund → like an admin cancel). |
| `completed` | `TurnIn` / `Complete` on a **server** contract, or the last `Collect` | Server contracts pay on the spot. |
| `cancelled` | `Cancel` (author, open only), `ForceCancel` (admin) | Escrow back to the author, reward sources release. |
| `expired` | `SweepExpiry` once `ExpireTotalDays` passed | Like cancel; a claimer's tracking is dropped even when offline. |

A refund for an offline author goes to `BoardOptions.OfflineRefund`; boards created by the registry keep it and hand it out when the author joins.

## Creating a board

```csharp
using ArcanumLib.Boards;

public override void StartServerSide(ICoreServerAPI api)
{
    var board = api.ModLoader.GetModSystem<BoardRegistry>().Create("mymod:orders", new BoardOptions
    {
        LangPrefix = "mymod:orders-",            // "mymod:orders-error-not-open", "…-completed", …
        PostRules = { MaxActivePostsPerPlayer = 3, PostCooldownHours = 2, RewardSlotCount = 4 },
    }, titleKey: "mymod:orders-title");

    board.RegisterGoal(new DeliveryGoal());
    board.RewardSources.Add(new AccountPayment());
    board.Completed += e => api.Logger.Notification("{0} done by {1}", e.Contract.Id, e.Player?.PlayerName);
}
```

`Create` stores the board in the world save (`arcanumlib:board:<key>`), sweeps expiry every 30 s and moves authors and claimers with [`/arcanum transfer`]({{ site.baseurl }}{% link PlayerTransfer.md %}). A mod with its own save object makes `new ContractBoard(sapi, key, options)`, assigns `State` (anything implementing `IBoardState`) after loading, calls `SweepExpiry` itself and `Register`s it — the Quest Framework bounty board works this way.

Every action returns an error lang key (`LangPrefix + "error-…"`) or null. ArcanumLib ships English and Russian texts for the default prefix `arcanumlib:board-`.

## Goals

```csharp
public class DeliveryGoal : BoardGoalBase, IPostableBoardGoal
{
    public override string Kind => "delivery";
    public override bool DeliversToAuthor => true;          // the author has to collect the goods
    public string NameKey => "mymod:goal-delivery";
    public bool HasDemand => true;
    public IReadOnlyList<string> TargetOptions(ContractBoard b, IServerPlayer p) => Array.Empty<string>();   // free text

    public override string? ValidatePost(ContractBoard b, IServerPlayer author, BoardContract draft) => /* known item? */ null;
    public override bool IsCompletable(ContractBoard b, IServerPlayer p, BoardContract c) => /* has the items */ true;
    public override bool TryComplete(ContractBoard b, IServerPlayer p, BoardContract c, bool force) => /* take them */ true;
    public override void OnFulfilled(ContractBoard b, IServerPlayer? claimer, BoardContract c) { /* c.PendingItemStackBytes = … */ }
    public bool TargetIsItem => true;                      // the post window offers the held item and shows its name
}
```

Other hooks: `ValidateAccept`, `OnAccepted`, `OnLoaded` (re-attach tracking of claimed contracts after a restart), `OnReleased` (abandon / expiry / admin cancel; `player` is null when offline), `OnPosted`, `CountsForPostCooldown`. A goal done outside the board (a kill seen by an event) calls `board.Complete(player, contract, force: true)`; a tracked task finished by an admin calls `OnGoalCompletedElsewhere`. Goals keep their own parameters in `BoardContract.Data`.

**Work the author accepts.** A goal nothing can check (a service: "roof the tavern") returns `true` from `IsCompletable`/`TryComplete` and sets `ConfirmDays` (e.g. 3). Handing it in only marks it `done`; the author then confirms or disputes, and silence counts as accepted. `OnFulfilled` gets `claimer == null` when the term ran out with the taker offline. The deadline and the dispute reason are in `Data` (`ContractBoard.ConfirmDueKey`, `ContractBoard.DisputeKey`).

## Reward sources

Escrowed items are built in. Anything else implements `IBoardRewardSource`: `Pay` when the claimer gets the reward (server contracts: on completion; player contracts: on collect), `Release` on cancel / expiry, `Holds` while it still owes something. Reserve at post time in the goal's `ValidatePost`/`OnPosted` or on the `Posted` event, and list the reward on the contract as a `BoardRewardEntry { Kind, Code, Amount }` so windows can show it (lang key `<Kind>-reward`, args amount and code).

## Block and windows

Block class `arcanumlib.ContractBoard` (block entity of the same name) opens the board named by `attributes.boardKey`. A board with an `Opener` shows its own window; otherwise the ArcanumLib window lists the contracts (filter by goal when there are 2–3 goals, and "mine"; each row shows the goal, title, author · time, reward and state) with take / hand in / give up / take back / collect / accept / dispute buttons, and goals with `ConfirmDays` get a steps strip and a note of what happens next. The post window (`IPostableBoardGoal`s only) picks the goal with buttons (a list past four goals), then target, count, run-out (with an ∞ button for none), title, description and reward slots. The target label is the lang key `<NameKey>-target` when it exists. The core's chat lines for the confirmation step use `LangPrefix` (`marked-done`, `your-contract-done`, `your-work-accepted`, `your-work-disputed`, `error-not-done`, `error-dispute-reason`): a board with its own prefix needs them too. The window talks over the `arcanumlib:boards` channel; the escrow slots are a server inventory, and items left in them return when the window closes or the player leaves.

## Admin command

`/arcanum boards list` — boards and their counts; `/arcanum boards show <board>` — contracts with ids; `/arcanum boards cancel <board> <contract>` — removes one, escrow and delivered items go back to their owners; `/arcanum boards resolve <board> <contract> pay|refund` — decides a done or disputed contract (privilege `controlserver`).

## Saved data

`BoardContract` keeps the proto numbers of the Quest Framework bounty contract (1–28), so boards saved before the move read unchanged; new members (`Data`, reward `Kind/Code/Amount`) come after them.
