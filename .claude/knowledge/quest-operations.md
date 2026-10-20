---
name: quest-operations
applies_when: adding a kind of quest operation, or changing what one carries, when it applies, or what a rebase does with it
enforces: a new kind goes into every one of its places in the same change, the nine every kind needs and those its fields and its verb bring; the census test runs over every kind and fails on the place it misses
---

# Quest operations — every place a new kind goes

**A quest is its operations, replayed (D68). A kind of operation is one enum value, placed in a dozen places across
the store, the wire and both hosts, and the code ties them together nowhere. This list and the census test do. Put a
new kind in every row below in the same change, and give it its sample in the census first.**

## Why

EVID1a's `evidenced` (D144) went into eight places, and none of them was written down. A missed place fails quietly,
usually on another machine:

- **No replay rule.** The kind applies to no quest. A remote refuses it and a rebase rewrites it, while the machine
  that made it shows the quest moved.
- **A field the payload or the wire does not carry** is lost at the first read from the store or the first sync, and
  the next machine replays a different quest.
- **No rebase rule.** `waited` (D79) had none. A wait on a quest that another machine closed first became a conflict
  naming no attempted status. The wire refused it, and every later push of that circle with it (the fix log,
  2026-10-07, found while this list was written).
- **State that no door answers** never reaches the page, the driver or a terminal.

An older build reads an unknown kind as an operation that is not whole. Its remote refuses a push that carries one, and
its machine cannot read a page that holds one, so a remote and its machines take a new kind together (D144 on D68).

## How to apply

**Work test first.** Add the value and its sample in the census, watch it fail, then place the kind one row at a time
until the census is green. The verb's own tests come after.

**A field an existing kind gains** goes through rows 3, 4, 6, 7, 9 and 10 and the verb's parameter, and its census
sample is the kind's own sample with the field set. Check too the places that carry a note or a payload without being a
row: `KeepAsync` and `RewriteAsync` (a lost move rewritten as a conflict), and any note rewrite on the way (QUESTCLOSE1b1's
`noteParts` is the worked example, D126's note). An existing field a further kind carries is named by one predicate the payload, the
log's reader and the wire all ask: `QuestStore.JudgedOpen` (a rebind's `whileOpen`, FABRIC-CLOSE1c15) and `QuestStore.Carries` (a take's
`submission`, its step's source as found, FABRIC-CLOSE1c18), and the rebase's conflict drops it as the log does. A field that gives the
quest nothing new skips rows 6, 7 and 10: a dismissal's `forward` (SYNCMERGE1a) removes the conflict as a plain dismissal does.
A field can also change whose an operation is. `forward` makes a dismissal the taker's own, so `QuestLog.TakersOwn(QuestOperation)`
names it, and `OnALostTake` and the rebase ask about the operation, never its kind alone. Where the field ties two operations of one
write together (the mark and the fresh take it made), the rebase judges the pair at the first. A pending take whose marked dismissal
would land nowhere loses with it (`RebaseAsync`'s `unbrought`, `QuestLog.Brings`). Prove an older build reading the field as absent
with `OlderRemote`, which strips the field it is given, on the push or the fetch.

**An existing kind's effect changing** touches row 2 alone, and no shape changes, so an older build reads the same log as a
different quest without refusing anything. The remote and every machine take the change together, as for a new kind, and a row
an older build cached reads as it was until the quest's next operation replays it. Prove the change on two machines and the
remote, as `QuestReopenSyncTests` does for FABRIC-CLOSE1c10's `rebound`, which now clears a held step's result. The orders a plain
pass never meets have fakes there (FABRIC-CLOSE1c21): `RacedRemote` lands another machine's push between a fetch and the push, and
`InterruptedRemote` stops a push before it reaches the remote or before its answer; a trigger on `quest_cursor` stops the integration
before its commit, and a restart is the store opened again on its connection.

### The places

Rows 1 to 8 are the eight `evidenced` needed, and every kind needs them. Rows 9 to 11 come with what the kind carries and who
makes it. Row 12, the moves read, every kind needs too (FLOWCHART1a2). Paths are under `src/Daoris.Service/`.

| # | Place | Where | Why it is there |
|---|---|---|---|
| 1 | The kind | `QuestOperationKind`, `Daoris.Service.Core/Quests/QuestLog.cs` | Its lowercased name is the log's `kind` column (`QuestStore.KindText`) and the wire's `kind`, and both read it back by name. The number is never stored. |
| 2 | The replay's rule | `QuestLog.Applies` and `QuestLog.Step`, the same file; `QuestTransitions` for a kind that moves the status | One judgement on every machine: the store's verbs, a remote taking a push (`QuestStore.ReceiveAsync`) and a rebase all ask `Applies`, so no order of operations reaches a state the table forbids. A kind with no `Applies` rule applies nowhere. One with no `Step` rule is stepped as a move to a status it does not have, and throws. A kind that moves no status has no `QuestTransitions.Target`. |
| 3 | The log payload | `QuestStore.PayloadJson` and `QuestStore.ReadOperation`, `Daoris.Service.Core/Quests/Quests.cs`; `AppendAsync` (this machine's) and `KeepAsync` (another machine's, fetched or received) pass the field | The history is the truth, and the payload is all of an operation that it keeps. Write a field only when it is set, so a row from before reads as it did. |
| 4 | The wire and its shape sentence | `QuestWire.Write`, `QuestWire.Read` and `QuestWire.Shape`, `Daoris.Service.Core/Quests/QuestWire.cs` | The remote's doors and the machine's client speak one wire (sync design §8). `Read` answers null for an operation that is not whole, and one such operation makes the whole push not whole: the remote's push door then answers 400 with `Shape` alone. So `Shape` names whatever the kind requires. |
| 5 | The rebase's rule | `QuestLog.Lost`, which `QuestStore.RebaseAsync` reads | What a pending operation of the kind becomes once a fetch means it no longer applies: forgotten, a conflict, or kept. Only a move becomes a conflict, because a conflict names the status it attempted and the wire refuses one that names none. A kind with no rule throws. When losing the kind should also take the chain step it published, as a lost done and a lost yes do, `RebaseAsync` calls `ForgetFollowUpsAsync` for it, which takes this machine's unpushed work on the step too, a take rewritten as a conflict naming the close (FABRIC-CLOSE1e2); a remote answers behind for a pushed step naming no source while it holds the parent not done, so a follow-up is never numbered on a close that has not landed there. A lost submission calls `ForgetStepsAsync` instead, which takes a step this machine had already taken too, and rewrites that take as a conflict on a quest no push carries, so its session's claim reads lost (FABRIC-CLOSE1e); where the parent's take lost to the person's reopen, the conflict carries `lostToTheReopen`, since the step's own history no longer shows the reopen (FABRIC-CLOSE1c22). Only a pending step goes so: a remote answers behind for a step whose source it holds without the submission it grew from (`QuestStore.AwaitsItsSourceAsync`), so a step is never numbered while its source's push is behind (FABRIC-CLOSE1c21). Once this machine's own take loses, `RebaseAsync` loses its later pending moves and waits on the quest too, even where they would apply: they were made on that take (D69). A move becomes a conflict. A wait is forgotten, and the take's conflict names its question, so whoever asked learns the quest no longer waits on it (WAITCLAIM1). A wait on a quest this machine did not take is no part of a take and is unaffected. A move or a wait made after the pass that rewrote the take never reaches the rebase: `QuestStore.MoveAsync` and `WaitAsync` refuse it inside their write where `QuestLog.OnALostTake` reads, by `QuestLog.Claim`, which `ClaimAsync` answers from, this machine's claim lost on a taken quest, and nothing is written (WAITCLAIM2). A remote refuses one that a machine on an older build still pushes, by the same rule in `QuestStore.ReceiveAsync`, answered in the lost take's words (WAITCLAIM3). One that older build left pending, `RebaseAsync` loses by the same rule, reading the claim from a take-conflict already written: a move becomes a conflict and a wait is forgotten, named on no conflict, since the take's may be numbered. `IntegrateAsync` rebases each quest of the circle that can hold one, which no fetch touches (WAITCLAIM4). A take lost to the person's reopen leaves its step open, and `OnALostTake` judges that open step too, for every kind a taker makes but a take (FABRIC-CLOSE1c18, `QuestLog.LostToTheReopen`); so it does wherever the take's conflict landed on the step open, as one whose submission went does once the winner's chain publishes the step anew (FABRIC-CLOSE1c24, `QuestLog.LostOnTheOpenStep`); a refusal's words read which from the history (`QuestMove.LostToTheReopen`, `QuestExchange.LostTake`), never from the step being open. A new kind that a taker makes joins that list and those refusals (`OnALostTake` names the kinds), or the comment there says why it does not. |
| 6 | The cache column | `QuestStore.EnsureSchemaAsync` (the `CREATE TABLE`, and the `SchemaColumns` list that gives an older store the column), `WriteCacheAsync` and `QuestStore.Read` | The `quests` row is the replay, written whole, and every list reads it. A property that the kind's step sets and the row does not keep is missing from every list, though the history holds it. |
| 7 | The HTTP answer | `QuestResponse`, `Daoris.Service.Http/ApiContracts.cs`; `ToQuest`, `Daoris.Service.Http/Program.cs` | Every quest route answers through `ToQuest`, and the page, the driver and a terminal read nothing else. Give a new field its default, so an answer from an older host reads as none. |
| 8 | The MCP listing | `ListQuestsAsync` (`quest_list`), `Daoris.Service.Mcp/QuestTools.cs` | A session reads quests only here. Say the new state in words a session can act on, and what it waits for. |
| 9 | What the operation carries | A parameter of `QuestOperation`, `QuestLog.cs`, null or false by default | Every other kind is unchanged by it. Rows 3 and 4 write it only for this kind. |
| 10 | What it gives the quest | A property of `Quest`, `Quests.cs`, set by its step, and any reading made from it | A state that holds a quest for a person goes through `Quest.Hold`. The outstanding list (`ListAsync` reads `held`), an ask's done (`Asks.cs`) and the history clear (`History.cs`) then follow it unchanged, as they did for EVID1a. |
| 11 | The verb | A `QuestStore` method, a `QuestExchange` method, and a door on each host that may make it | In one transaction the store replays, judges with row 2, appends, writes the cache, and publishes any held step it releases (`PublishNextAsync`). The exchange judges what the store cannot, once for both hosts. A person's act has a local host's door and no MCP tool, as a delete, a yes and a verdict do. A new request shape joins the serializer context in `ApiContracts.cs`. |
| 12 | The moves read | `QuestMoves.Read` and its row for the kind (`Own`), `Daoris.Service.Core/Quests/QuestMoves.cs`; `QuestMovesResponse`, `ApiContracts.cs`, which `GET /api/quests/{id}/moves` answers on both hosts | A quest's page draws its path from it and a trace says it (FLOWCHART1a2, D186). A kind with no row throws, so neither draws it. What a move answers is an allow-list: a field the kind carries reaches a page only once `QuestMoveReading` and the answer name it, and never the payload, a note's body, the answers or a path. Whether it applied is the replay's, judged against the quest the moves before it replay to, never stored. |

### Beyond the service

These are other lanes' files, so a service branch names them in its hand-back and leaves them alone:

- the driver, which reads the quest answer field by field (`src/Daoris.Desktop/Daoris.Desktop.Driver/Service/ServiceClient.cs`);
- the page's quest type (`src/Daoris.Web/src/data/api.ts`);
- the family rehearsal, which compares some answers whole (`tools/family-rehearsal.mjs`). A new field fails checks a
  service branch cannot run, as EVID1a's `evidence: []` did;
- the words a quest's flowchart says each kind in (`quests.flow.kind.<kind>` in `src/Daoris.Web/src/locales/{en,zh}/quests.json`,
  drawn by `quests/questLife.ts`), which `questLife.test.ts` holds to the enum, and the trace's (`TraceWords.MoveSaid`, the
  driver's `Trace.Words.cs`); each shows a kind it does not know as recorded, and a new kind's own field, once row 12 answers
  it, is worded in both;
- the records: the decision that adds a kind amends D68. The sync design's list of kinds (§2) is history and is not
  kept current: the enum is the list.

### The census

`QuestOperationKindsTests` (`Daoris.Service.Tests`) runs each theory over every value of `QuestOperationKind`. Each
theory starts from a sample of the kind, made as its verb makes it. A new kind fails first for want of a sample, and the
failure names this document. Then each theory checks one or two rows:

- **row 2:** the kind applies to some quest, steps it, and moves its status only as `QuestTransitions` says;
- **row 4:** it crosses the wire both ways as it was made, and if the wire refuses it with only the fields every
  operation names, `QuestWire.Shape` names it;
- **rows 3 and 6:** it is kept in the log and read back as made, and the cached row is its replay, property by property;
- **row 5:** it has a rebase rule, and only a move becomes a conflict;
- **row 12:** it is read as a move, applied where it applies, from the status it reached the quest at, carrying its own fields
  and no others (`MoveFields`). `QuestMovesTests` holds the reading's properties to the allow-list, the replay's order, what
  did not apply, and a dismissal's reference.

`QuestResponseTests` (`Daoris.Service.Http.Tests`) checks row 7: every property of `Quest` is answered by
`QuestResponse`, or left out with its reason.

No test checks rows 8 and 11 or the other lanes. Each check was seen failing: an enum value added with nothing else
failed four theories. Given a sample, a field the wire did not read failed the wire's. Given an `Applies` rule and no
`Step` rule, it failed the replay's. Dropping a decline's flag from the payload failed the log's theory, dropping a
wait from the cache failed the cache's, and a property added to `Quest` failed the HTTP check.
