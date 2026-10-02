# An answer continues the session (ANSWER1)

> The owner, 2026-10-02: *"whenever I input anything say the session was waiting for my input and after I input it
> starts a new session? isn't this should be continue"*. This is the contract for what an answer does. Its decision is
> **D131**. Status: **designed; the driver's half built (ANSWER1a); the service's half (ANSWER1b) and the page's
> (ANSWER1c) are rows.** Read with **D46 §4**, **D51**, **D79**, **D80**, **D83**, **D90**, **D104**, **D125** and
> **D130**.

## 0. What happened, and what is true today

Seen on the install by the parent session, on `claude-code-acp` 0.84.0 and an account of Daoris's own: a driven
session parked to ask the person and ended its turn (`end_turn`). Its record was `awaiting-person`. The person answered
on the screen. The driver started a **new** session, noted *carries `#<quest>` on with your answer to session `<id>`,
in the tree it worked in*. The agent's own conversation was gone, and the screen showed a second session.

Read from the code at `32cbf03`:

- **The answer ends the record.** `SessionLedger.AnswerAsync` keeps the words in `answer` and moves the record
  `awaiting-person` → `completed`, its note gaining *Answered: …* (D83). A finished record does not move (the ledger's
  `Terminal` refusal).
- **The carry-on is a new record.** The planner carries on a taken quest whose last record here is `completed` with an
  answer (`Planner.CarryOn`), and the ledger opens a session on it (`OpenAsync`, the STANDDOWN2 clause). The new
  session is handed the answer, the last plan and the last words (TOOL4f) in the same tree.
- **Nothing resumes an agent's own conversation.** Every start sends `session/new` (`AcpSession.OpenAsync`) or runs
  `claude -p` (`ClaudeCodeAdapter.Prepare`). D46 held resume as an adapter capability "until real driven runs ask for
  it"; this is that run.
- **The ledger already allows `awaiting-person` → `working`** (`SessionLedger.Allowed`), and a park holds its tree the
  whole time it waits (D51).

What the adapter offers, read from `@agentclientprotocol/claude-agent-acp@0.84.0` (`dist/acp-agent.js`) and the ACP
SDK it pins (`@agentclientprotocol/sdk@1.5.1`, `dist/schema`):

- `initialize` answers `agentCapabilities.loadSession: true` and `sessionCapabilities.resume: {}`.
- `session/resume` takes `sessionId`, `cwd`, `mcpServers` and `_meta`, and resumes **without replaying** the history.
  `session/load` takes the same and **replays** the whole history as `session/update` before it answers.
- Both rebuild the Agent SDK's query with `resume: <id>`, which reads the transcript under the account's configuration
  home (`CLAUDE_CONFIG_DIR/projects/*/<id>.jsonl`). A conversation that is not there is refused with
  `resource_not_found` (`-32002`).
- On the native door the Agent SDK's own `system`/`init` line carries `session_id` (`SDKSystemMessage`,
  `@anthropic-ai/claude-agent-sdk@0.3.284`), and `claude --resume <id>` continues that conversation.

## 1. Resume when it can work

**An answered park is continued by its own record.** The record moves `awaiting-person` → `working`, the harness's
own conversation is resumed in the same tree, and **the answer is its next prompt**, verbatim. Nothing is composed
around it: the conversation already holds the quest, the instruction and the question.

- **The protocol door**: `session/resume` where the agent advertises `sessionCapabilities.resume`, since the record
  already holds the conversation; else `session/load` where it advertises `loadSession`, and the replay it sends is
  not kept a second time; else the agent cannot resume. The request carries what `session/new` would: the tree as
  `cwd`, the servers (the connector among them), and the rules in `_meta`. The posture is set on its answer as on a
  new session's.
- **The native door**: `claude -p <answer> --resume <id>`, with the posture, the settings and the servers a start
  carries.
- **The conversation's id is kept the moment the wire says it**: `session/new`'s `sessionId`, or the native door's
  `init` line's `session_id`, in `<home>/sessions/<session>.harness.json` beside the transcript, with the adapter
  that opened it. It is never read from the agent's own home (D66 §3; D125 rejected reads inside an account's
  directory).

**It needs** the record still parked with an answer (§5's ANSWER1b), the same adapter, **the same account** (the
conversation lives in that account's configuration home, so a different one would not find it, and a record names
one account), the same tree still standing, a kept id, and an adapter that can resume. The harness's version may
differ (the native `claude` updates itself): the resumed run's first line says so, since the record names the version
it opened on.

## 2. Otherwise, today's carry-on, said why

Where any condition fails, the park ends `completed` with the answer kept and the reason added to its note, as the
answer ended it before, and a **new** record carries the quest on in the same tree, handed the answer, the last plan
and the last words (D80, TOOL4f). Its note names the reason in one line. The machine log writes `session.answered`
once per answer taken up: `{session, adapter, resumed, why}`, `why` being the code below.

| Code | When | The note's line |
|---|---|---|
| `account` | the start runs on another account than the park did: a limit cools it, a rotation or D130's list chose another | *its conversation stays with the account it ran on, and this start runs on another* |
| `adapter` | the person changed the adapter since | *it ran on `X`, and starts here now run on `Y`* |
| `unkept` | no id was kept (a park from before this build, or a wire that never said one) | *Daoris kept no id for its conversation* |
| `tree` | its tree is gone | *its tree is gone* |
| `unable` | the adapter has no resume on its door | *`X` cannot resume a conversation* |
| `offered` | the agent advertised neither `resume` nor `loadSession` | *the agent offers no way to resume a conversation* |
| `gone` | the agent answered `resource_not_found`, or the native door ended before opening the conversation | *the agent no longer has its conversation* |
| `refused` | the agent refused the resume otherwise | *the agent refused to resume it* |
| `ended` | the record had already ended: a service from before ANSWER1b | *its record had already ended* |

The first five are known before anything is spawned. The last four are the wire's: the resumed run then ends with
nothing done, the park is closed as above, and the carry-on starts in the same look. A note travels, so it never names
an account (TOOL4f) and never quotes the agent's refusal; the agent's own words go to the record's conversation, which
stays on this machine.

## 3. How it is recorded and shown: the same record reopens

**Chosen: one Daoris record for one harness conversation.** A resume reopens the record that parked; a fallback is a
new record because it is a new conversation. Weighed:

| | The same record reopens | A linked record the page folds |
|---|---|---|
| Strikes (DRV6) | unchanged: they count `failed` records, and an answered park never failed | unchanged |
| Verdicts | the planner continues an answered park, and that park is not busy with itself; the ledger opens nothing | unchanged |
| Machine log | `session.started` once, `session.parked` per park, `session.ended` once; `session.answered` says each answer | a second `session.started` per answer, which the log reads as another session |
| A derived timeline | derived from one record's events, as it is | derived across two records, by a link every reader must follow |
| The stream's one home | one conversation, one home: the transcript appends and the events continue | one conversation split over two homes |
| A teammate | sees one record move forward: `awaiting-person` → `working` | sees two rows, unless the link travels as a new field |
| The review (SURF6) | one base commit, so one range over all the work | two ranges, one per record |

**Rejected: a linked record the page folds into one thread.** It needs no ledger change, but every reader of records
(the page, `SessionGroups`, `daoris-driver sessions`, the review, Ask Daoris's facts, a teammate's page) would learn
the fold, the stream would have two homes for one conversation, and the log would count two sessions where the person
sees one. With the record reopened, the page shows one row and one conversation continuing as it is built today.

**Not chosen either: reopening a finished record.** A finished record is a record (the ledger's `Terminal` rule), it
has already travelled, and the strikes are counted from it. So the answer must leave the record parked, which is
ANSWER1b's change.

## 4. The other entries

- **Try again after a person's stop (SESSUX1b): a new session, as today.** The stop ended that session, and the record
  says so and has travelled. A fresh conversation also drops the context the person stopped; the carry-on is handed
  the last plan and last words.
- **A carry-on after a time-out, a crash or a refused turn (D80): a new session, as today.** The `failed` record is
  what the strikes count, and a finished record does not move. A conversation that timed out is also the one that
  ran out of time.
- **A take the sweep or a shutdown cut off (D104): the same.** `stopped` is finished.
- **A resume after another repository answers (D79): the same shape, held.** The waiting session concludes
  `completed`, so its record cannot reopen; continuing it needs the record to wait without finishing, a ledger change
  no real run has asked for yet.

Resuming a finished session's conversation in a new record is not done for any of them: that is the linked shape §3
rejects.

## 5. What the service and the page owe

- **ANSWER1b, the service: the answer keeps the park.** `AnswerAsync` keeps the record `awaiting-person`, sets
  `answer` and adds *Answered: …* to its note, and says it carries on at the driver's next look. A second answer before
  that replaces the first. **A move into `awaiting-person` clears `answer`**, so a session that parks again asks anew.
  A move to `completed` keeps it, which the fallback's open needs. Proof: `SessionLedgerTests`, and the family
  rehearsal's answer checks, which today read `completed` after an answer.
- **ANSWER1c, the page and the groups: an answered park reads as carrying on.** Until the driver takes it up, the
  parked card shows the answer and *carries on at the next look*, and `SessionGroups` lists it under *Working*, never
  *Waiting on you*. The attention watch's count follows. Proof: `SessionGroupsTests`, the card's story.

Until ANSWER1b lands, the driver sees every answered record already `completed`, and carries it on as today with the
`ended` line.

## 6. The build (ANSWER1a) and what its gates do not cover

Built in the driver: the kept id (`HarnessConversations`), the judgement (`Continuation.Judge`), the planner's
continue verdict, `AcpSession`'s resume, the native door's `--resume` and its `init` id, the continuation in the
driver (`Driver.Continue.cs`), the line, and a real-process tick on a protocol stub that speaks `session/resume`
(`AnswerContinuesTickTests`, the parent's to run). D131's notes say what each settled.

Not covered: no real agent was resumed by this branch, so whether `claude-agent-acp` resumes a driven conversation
across a process, with Daoris's rules and servers handed again, is ANSWER1d's canary on the install. The native door's
`--resume` is held by its arguments and the `init` line's shape, not by a real run. The stand-in service models
ANSWER1b; the real service still ends the record (§5).
