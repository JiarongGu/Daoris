---
name: pushed-slices
applies_when: moving a family of the page's queries from pulls to the shell's pushes (D182's PUSH1 rows), adding a partition to a pushed slice, or changing what a pushed slice's owner judges, wakes on or answers
enforces: a pushed slice has one owner in .NET whose partitions are allow-listed, a wake that only asks, its snapshot read from memory through `STATE_SLICE`, and on the page a spec in its domain's bridge file read from memory once live, with every broad invalidation and every press's answer passing over its entry; an older host keeps the pull
---

# Pushed slices — every place a slice goes

**A slice is pushed by one owner and held by each window's query cache (D182, its PUSH1a note). The host half and the page
half share no code, so a slice is wired in every row below in the same change.** The driver's state is the worked example:
`Loop/DriverStateSlice.cs` on the host, `driverSlice` in `bridge/driver.ts` on the page. `Loop/LinesStateSlice.cs` is the one
judged from the machine's facts, a partition per repository (PUSH1b1).

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
