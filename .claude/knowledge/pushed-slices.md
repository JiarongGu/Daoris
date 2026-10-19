---
name: pushed-slices
applies_when: moving a family of the page's queries from pulls to the shell's pushes (D182's PUSH1 rows), adding a partition to a pushed slice, changing what a pushed slice's owner judges, wakes on or answers, judging a machine fact per repository (FACTSPLIT rows), or moving a part of the driver's look beside it as its topic's judge (PARALLEL rows)
enforces: a pushed slice has one owner in .NET whose partitions are allow-listed, a wake that only asks, its snapshot read from memory through `STATE_SLICE`, and on the page a spec in its domain's bridge file read from memory once live, with every broad invalidation and every press's answer passing over its entry; an older host keeps the pull
---

# Pushed slices — every place a slice goes

**A slice is pushed by one owner and held by each window's query cache (D182, its PUSH1a note). The host half and the page
half share no code, so a slice is wired in every row below in the same change.** The driver's state is the worked example:
`Loop/DriverStateSlice.cs` on the host, `driverSlice` in `bridge/driver.ts` on the page. `Loop/LinesStateSlice.cs` is the one
judged from the machine's facts, a partition per repository (PUSH1b1); `linesSlice` in `bridge/repositories/lines.ts` is its page half
(PUSH1b2), and the one whose rows carry a flag (`notReadYet`) that the page reads before the field it qualifies.

| Place | What goes there | What holds it |
|---|---|---|
| The owner | A `StateSlice` over a judge returning named partitions, each an allow-listed DTO; never a credential or an arbitrary payload, a machine path only in a desktop-only slice. A judge reading what lands later (the machine's facts start after the slices) answers null until it has, which keeps the slice `notJudged` | `StateSlicesTests`; the slice's own tests: a hostile fixture field never pushed, a second snapshot judging nothing, `notJudged` before its truth lands |
| Its wakes | `WakesOn`: the bus events whose payloads move it, each only asking; `Hears` where a move reaches it with no event (`LinesStateSlice.HearFacts`: the facts it reads landing, by their values' references; `CallsStateSlice.HearCalls`: a session's running call moving in the loop's own record, `RunningCalls.Changed`, raised outside its lock); and a direct `Wake()` where the loop learns of a move with no event | the slice's wake test; a fact of another topic landing wakes nothing |
| The registry | The slice added to `DriverLoop.Slices`, which `Start` runs and `STATE_SLICE` finds | the route answering its snapshot |
| Its allow-list's twin | Where the projection is a route's own answer picked by field (as the driver's state is `STATE`'s), every field the route answers is pushed or named pull-only | `DriverStateSliceTests`' classing test, the pattern to copy |
| The page's spec | A `SliceSpec` in the domain's bridge file: its name, the exact key it feeds, `read: () => readSlice(name)` (so `STATE_SLICE` keeps one bridge file), `compose`, and `merge` only where the owner pushes deltas | `DriverModuleRoutesTests` (the route's bridge file), `slices.test.ts` |
| Mounting | The spec in `ShellSignals`' `PUSHED` | `pushedState.test.tsx`-style counts |
| The query | `queryFn: () => readPushed(client, name, pull)`, and while `usePushedLive(name)`, no focus refetch and never stale | a tick and a focus counted at the bridge: zero reads of the slice |
| Invalidations | Every broad invalidation of a family holding the entry passes `predicate: notPushed(client)`; the hub's scopes already do | `ShellSignals.test.tsx`, `stateHub.test.tsx` |
| Answers | A press answering the slice's data writes it only where the slice is pulled (`writeAnswered` is the driver's), or where the pushes compose nothing yet (`pushedOf(client).data(name) === undefined`), so a press made before the first judgement is not lost | the press's own test; `useSweepLook` (PUSH1c4b) |
| The records | The README's `STATE_SLICE` row names the slice and its partitions; D182 gets the row's note | `DriverModuleRoutesTests`' README check |

**Two traps.** A window's views whose effects run before `ShellSignals` pull beside the snapshot, so a root mounts it first
(`SecondaryWindowRoot`). And a delta is never coalesced: only a whole replacement carries the pump's coalesce key, which
`StateSlice` sets itself.

## A family read under many keys, and a large one

The sessions (PUSH1d2, `Loop/SessionsStateSlice.cs`, `sessionsSlice` in `bridge/sessions/sessions.ts`) are the worked example; D182's PUSH1d2
note has the measure that chose it.

| Part | The shape | Where it is |
|---|---|---|
| One entry | The spec feeds the widest variant's key (every repository, closed included, every workspace); each hook reads it once live and derives its variant with a stable `select` that twins the service's filter and order. Not live, each variant is its own pull under its own key, its `queryFn` first waiting for a snapshot on its way (`readPushed` with a null pull) so a window opening reads none beside it | `useSessionsRead`, `sessionsIn` in `data/queries.ts`; `pushedList` for any list (the quests' and the asks', PUSH1d3) |
| Keyed deltas | A partition whose data is an object of records by key (`StateSlice.Keyed`) pushes the keys that moved and names the keys gone; the page's `merge` is `mergeKeyed` | `StateSlice.Moved`, `slices.ts` |
| Judged and empty | `compose` is told whether the owner judged (`judged`): a list judged empty composes `[]`, so a last partition gone empties the list instead of leaving it as it was | `Pushed.install`, `Pushed.apply` |
| A write that bypasses the host | A `Hears` on `DriverLoop.MoveHeard`, which the watch tells each move it heard (`HeardMove`): the ledger's family wakes the slice, so a connector's write is pushed with no desktop dispatch | `SessionsStateSlice.HearLedger` |
| A driver fact beside the records | What the driver alone knows of a session (its running call, WAITSAY1c1) is a slice of its own, keyed by session and judged from memory, never a field of the records' slice, whose judge reads the service; one partition judged empty is pushed empty, never gone | `CallsStateSlice` |
| Its twins' test | The allow-list held against the page's type (`data/api.ts`) and the service's answer record (`ApiContracts.cs`), read from the sources | `SessionsStateSliceTests` |
| An ordered list | A place in the list is never a field (one move would push every row after it): each row carries, pushed-only, the sort inputs the reader orders its group by, and no more, so a write the order does not read pushes nothing; the page's compose twins the reader's order, written in the decision's note | `GroupsStateSlice.OrderedBy` (PUSH1c3a) |
| A route's own look | Where a pull's freshness is the settled bar, the judge takes the route's look once per wake (and keeps what the route kept of it), rather than a fact that may lag it; the route's builders write each record | `GroupsStateSlice.LookAsync`, `SessionsRoutes.Grouped` |
| A route that answers one subject at a time | No pull answers the widest, so the spec feeds a key no variant uses (`keys.syncEvery`), a subject with no partition reads as the route answers it (`wired: false`), and live but not yet judged leaves the read unanswered rather than pulling a partial map | `SyncStateSlice`, `standingOf` (PUSH1a2) |
| A machine path the page reads only as present | The path is never pushed: a flag says what its presence said (a file's `kept`), the page's type gains it beside the path, and one helper reads either, so a pulled answer and a pushed one render alike | `QuestsStateSlice.Kept`, `fileKept` (PUSH1d3) |
| An answer judged from other families | The service's answer reads more than its own table (an open quest's `deletable` reads the sessions, an ask's standing its quests): the slice hears each family its answer reads, not only its own | `QuestsStateSlice.Families`, `AsksStateSlice.Families` (PUSH1d3) |
| A verdict the page computed | Judged in .NET and added to the route's answer beside the data it judges (D182 point 6), so the pull and the push carry it alike, and the page words it: a number that moves with the clock is pushed rounded as the page says it (a whole percent), so the period pushes only where the words would change, and the flag beside it is judged on the exact value | `AccountsRoutes.SaidShown`, `ScopeShown` (`curve`, `over`, `curves`, `measured`, `overCurve`, PUSH1e) |
| A fact another slice already holds | Read from that slice's snapshot in memory (`StateSlice.Snapshot`), never a second read of the source; `StateSlice.Pushed`, told after a judgement that pushed, is its wake, filtered to what the judge reads (a count that moved), so the other slice's every move judges nothing here | `AccountsStateSlice.Running`, `HearRunning` (PUSH1e) |
## A fact judged per repository, before its slice

A slice partitioned per repository is only as cheap as the fact beneath it: one repository's `refs` wake must judge that
repository alone (D181 points 2-3). `Driver/Help/HelpFacts.cs` holds four such topics, the lines (PUSH1b1), the work
inventory (FACTSPLIT1a), the sessions' trees (FACTSPLIT1b, both D181's notes) and the clean-up's list (`HelpFacts.Sweep`,
PUSH1c4a), and PUSH1c's slices follow their shape.

| Part | The shape | Where it is |
|---|---|---|
| Subjects | Made from the machine fact (the service's answers), not from configuration: each registry repository with a checkout here, made and forgotten in `Known` as that fact lands, read under a lock so a late answer never undoes a newer one. What belongs to no checkout gets a part of its own under a name no folder can hold (`<no checkout>`) | `HelpFacts.Known`, `HelpFacts.NoCheckout` |
| Wakes | `Woken(topic, subject, snapshot)`: a `refs` move wakes its repository's subject only; a family move that names no repository wakes every subject the machine fact names, none before it lands. A part whose subjects are read from another fact (the trees from the sessions' look) is not woken with that fact's wake, which would judge the fact it is about to replace | `HelpFacts.Woken` |
| The judge | Reads its inputs from the machine fact (`PartOf`: the root, the checkouts, the trees in use from the snapshot's active sessions; `TreesPartOf`: the look and whose records are the part's), never a service call of its own, and git only under its repository | `HelpFacts.JudgeWorkAsync`, `WorkInventories.ReadPartAsync`, `HelpFacts.ReadTreesAsync` |
| Re-wake | When the machine fact lands with a part's inputs changed (its root, its trees in use; for the trees, the look with the part's trees to judge moved), that part alone is judged again, the trees' at once rather than after their rest | `HelpFacts.Keep` |
| Rules | A deadline, a bound, how many parts at once, and a `Rest` where a burst of one repository's moves should be one judgement | `LinesRules`, `WorkRules`, `TreesRules` |
| The merge | `HelpFacts.Read` merges the parts in name order, the leftover part last (the trees into the look, by path), and reads null while any part was never judged, so *not read yet* keeps its meaning | `HelpFacts.Read`, `HelpNotReadYet` |
| Its lines | Each part's lines carry their repository as data, so a workspace's room filters them by it, never by the words | `InventoryUnread`, `HelpRoomScope.Project` |
| A person's occasion beside a fact | Where a person's press asks what the fact must never ask on a timer (D148 point 2's pull-request asks), the fact judges from what was kept and the press's door wakes only the parts whose kept record it moved | `SweepStateSlice.Asked`, `LandedCleanups.SweepPartAsync` (PUSH1c4a) |
| A door reading the parts | A route that reads the cheap half itself, as fresh as before, takes the git half from the parts: each part says what it asked (`HelpTrees.Asked`), so asked-and-empty and never-judged differ; what no part asked is walked by the door alone, after one wait in the facts' life for the parts' first judgement, and the facts' look is judged afresh where it is behind the door's (PUSH1c1) | `HelpFacts.WithTreesAsync`, `HelpFacts.TreesJudged` |

**What `MachineFacts` does underneath.** A judge returning null drops the subject's fact (`Land`); one that throws keeps the
last fact. Judgements run in the execution context captured at `Start()`, so a test counting git starts wraps `facts.Start()`
in `ProcessStarts.Within`, not the wake. The tests to copy: `HelpFactsTests`' per-repository count (a `refs` wake judges only
its part), its merge, a gone repository dropping its part, and the git count under the seam; for a door, the four `A_door…` cases
and `A_tree_no_part_asked_yet…`, and the route's `DriverModuleSessionsTests.The_groups_take_each_trees_judgement_from_the_facts…`.

## A part of the look judged beside it

The look's own judges (the ending hooks and the packets, PARALLEL1b; the opinions, the helpers and the landing, PARALLEL1g) are not
`MachineFacts`: they run on the running set (`RunningSessions.Judge`: one judgement per subject, a bound per topic, waited for at
the loop's close) and keep in `LookFacts` only what the next look's plan reads.

| Part | The shape | Where it is |
|---|---|---|
| Not judged yet | A subject no judgment has answered for holds what it could hold (a chain's next steps, an ask that begins nothing), never read as a let-go | `OpinionGateLook.NotJudged` |
| A slot | What takes a place under the cap is begun by the look before its plan, from an act the judgment kept and one look takes; the next judgment is asked after it, so it reads the claim held | `LookFacts.TakeAct`, `OpinionGateLook.Begin` |
| The wake | A judgment wakes the loop only where it moved what the plan reads, so a hold never spins it | `RunningSessions.FactsMoved`, `LookFacts.KeepOpinion` |
| A failure | Said once until its subject is judged again: its line wakes the next look, whose judgment fails again | `LookFacts.Failed` |
| One look whole | A one-look run and a gate's run-until-idle judge in the look (`LoopContext.Beside` false), so the rehearsals read each look whole | `Driver.Wanted` |

The tests to copy: `LookJudgesTests`, each judgment held on a door only it asks, so no git runs.
