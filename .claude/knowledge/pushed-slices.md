---
name: pushed-slices
applies_when: moving a family of the page's queries from pulls to the shell's pushes (D182's PUSH1 rows), adding a partition to a pushed slice, changing what a pushed slice's owner judges, wakes on or answers, judging a machine fact per repository (FACTSPLIT rows), or moving a part of the driver's look beside it as its topic's judge (PARALLEL rows)
enforces: a pushed slice has one owner in .NET whose partitions are allow-listed, a wake that only asks, its snapshot read from memory through `STATE_SLICE`, and on the page a spec in its domain's bridge file read from memory once live, with every broad invalidation and every press's answer passing over its entry; an older host keeps the pull
---

# Pushed slices — every place a slice goes

**A slice is pushed by one owner and held by each window's query cache (D182, its PUSH1a note). The host half and the page
half share no code, so a slice is wired in every row below in the same change.** The driver's state is the worked example:
`Loop/DriverStateSlice.cs` on the host, `driverSlice` in `bridge/driver.ts` on the page. `Loop/LinesStateSlice.cs` is the one
judged from the machine's facts, a partition per repository (PUSH1b1); `linesSlice` in `bridge/lines.ts` is its page half
(PUSH1b2), and the one whose rows carry a flag (`notReadYet`) that the page reads before the field it qualifies.

| Place | What goes there | What holds it |
|---|---|---|
| The owner | A `StateSlice` over a judge returning named partitions, each an allow-listed DTO; never a credential or an arbitrary payload, a machine path only in a desktop-only slice. A judge reading what lands later (the machine's facts start after the slices) answers null until it has, which keeps the slice `notJudged` | `StateSlicesTests`; the slice's own tests: a hostile fixture field never pushed, a second snapshot judging nothing, `notJudged` before its truth lands |
| Its wakes | `WakesOn`: the bus events whose payloads move it, each only asking; `Hears` where a move reaches it with no event (`LinesStateSlice.HearFacts`: the facts it reads landing, by their values' references); and a direct `Wake()` where the loop learns of a move with no event | the slice's wake test; a fact of another topic landing wakes nothing |
| The registry | The slice added to `DriverLoop.Slices`, which `Start` runs and `STATE_SLICE` finds | the route answering its snapshot |
| Its allow-list's twin | Where the projection is a route's own answer picked by field (as the driver's state is `STATE`'s), every field the route answers is pushed or named pull-only | `DriverStateSliceTests`' classing test, the pattern to copy |
| The page's spec | A `SliceSpec` in the domain's bridge file: its name, the exact key it feeds, `read: () => readSlice(name)` (so `STATE_SLICE` keeps one bridge file), `compose`, and `merge` only where the owner pushes deltas | `DriverModuleRoutesTests` (the route's bridge file), `slices.test.ts` |
| Mounting | The spec in `ShellSignals`' `PUSHED` | `pushedState.test.tsx`-style counts |
| The query | `queryFn: () => readPushed(client, name, pull)`, and while `usePushedLive(name)`, no focus refetch and never stale | a tick and a focus counted at the bridge: zero reads of the slice |
| Invalidations | Every broad invalidation of a family holding the entry passes `predicate: notPushed(client)`; the hub's scopes already do | `ShellSignals.test.tsx`, `stateHub.test.tsx` |
| Answers | A press answering the slice's data writes it only where the slice is pulled (`writeAnswered` is the driver's) | the press's own test |
| The records | The README's `STATE_SLICE` row names the slice and its partitions; D182 gets the row's note | `DriverModuleRoutesTests`' README check |

**Two traps.** A window's views whose effects run before `ShellSignals` pull beside the snapshot, so a root mounts it first
(`SecondaryWindowRoot`). And a delta is never coalesced: only a whole replacement carries the pump's coalesce key, which
`StateSlice` sets itself.

## A fact judged per repository, before its slice

A slice partitioned per repository is only as cheap as the fact beneath it: one repository's `refs` wake must judge that
repository alone (D181 points 2-3). `Driver/Help/HelpFacts.cs` holds three such topics, the lines (PUSH1b1), the work
inventory (FACTSPLIT1a) and the sessions' trees (FACTSPLIT1b, both D181's notes), and PUSH1c's slices follow their shape.

| Part | The shape | Where it is |
|---|---|---|
| Subjects | Made from the machine fact (the service's answers), not from configuration: each registry repository with a checkout here, made and forgotten in `Known` as that fact lands, read under a lock so a late answer never undoes a newer one. What belongs to no checkout gets a part of its own under a name no folder can hold (`<no checkout>`) | `HelpFacts.Known`, `HelpFacts.NoCheckout` |
| Wakes | `Woken(topic, subject, snapshot)`: a `refs` move wakes its repository's subject only; a family move that names no repository wakes every subject the machine fact names, none before it lands. A part whose subjects are read from another fact (the trees from the sessions' look) is not woken with that fact's wake, which would judge the fact it is about to replace | `HelpFacts.Woken` |
| The judge | Reads its inputs from the machine fact (`PartOf`: the root, the checkouts, the trees in use from the snapshot's active sessions; `TreesPartOf`: the look and whose records are the part's), never a service call of its own, and git only under its repository | `HelpFacts.JudgeWorkAsync`, `WorkInventories.ReadPartAsync`, `HelpFacts.ReadTreesAsync` |
| Re-wake | When the machine fact lands with a part's inputs changed (its root, its trees in use; for the trees, the look with the part's trees to judge moved), that part alone is judged again, the trees' at once rather than after their rest | `HelpFacts.Keep` |
| Rules | A deadline, a bound, how many parts at once, and a `Rest` where a burst of one repository's moves should be one judgement | `LinesRules`, `WorkRules`, `TreesRules` |
| The merge | `HelpFacts.Read` merges the parts in name order, the leftover part last (the trees into the look, by path), and reads null while any part was never judged, so *not read yet* keeps its meaning | `HelpFacts.Read`, `HelpNotReadYet` |
| Its lines | Each part's lines carry their repository as data, so a workspace's room filters them by it, never by the words | `InventoryUnread`, `HelpRoomScope.Project` |

**What `MachineFacts` does underneath.** A judge returning null drops the subject's fact (`Land`); one that throws keeps the
last fact. Judgements run in the execution context captured at `Start()`, so a test counting git starts wraps `facts.Start()`
in `ProcessStarts.Within`, not the wake. The tests to copy: `HelpFactsTests`' per-repository count (a `refs` wake judges only
its part), its merge, a gone repository dropping its part, and the git count under the seam.

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
