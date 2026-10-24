# FOUNDATION1: Codex's sealed proposal (2026-10-25)

One of three sealed proposals written from `docs/2026-10-25-foundation-brief.md` and `docs/2026-10-25-foundation-survey.md` under D191, by Codex (gpt-6.1-sol, reasoning effort high), read-only, from main at `9262f23d6`, without the other two. Kept verbatim; the steward ran it from outside the install. The other two are `docs/2026-10-25-foundation-proposal-claude.md` and `docs/2026-10-25-foundation-proposal-opus.md`.

## The proposal

The Fabric should be a set of shared .NET contracts with explicit owners: **participants identify who is acting, moves own changes of execution and delivery, and events describe committed changes**. Sessions, quests, appointments, channels and workflow runs keep their existing authority; they become sources and adapters of these contracts, rather than being copied into a universal graph. One deterministic movement planner serves both the driver’s look and the chat runner, while their executors retain their different lifetimes. One event vocabulary feeds state projections, notifications and plugin observations through different, bounded delivery contracts. This makes D193’s staff, work orders and departmental operation agree without introducing new concepts into the window.

### Basis and corrections to the survey

References below use the survey’s path aliases: `Driver/`, `Modules/`, `Core/`, `Mcp/` and `Web/`. Code was inspected at `9262f23d604f68313ad8ea3c467e295b363156d7`. This is a read-only proposal; no tests, installed measurements or account probes were run.

| Survey claim or implication | Finding against this checkout | Consequence |
|---|---|---|
| Transient retry reaches driven protocol sessions only (§2.2 N, §2.4 row 6). | **Now stale.** Protocol conversations and Ask Daoris receive `ChatRetry`, using `AgentErrors.Read`, at `Driver/Conversations/Chat/ChatRunner.cs:1664–1683`. The change is recorded under AGENTERRORS1b in `docs/decisions/D210.md:348`. | Preserve this implementation and its `ChatTransientProcessTests`; do not build a second conversation retry. |
| Account cooling still distinguishes ordinary words from a limited end (§2.4 row 1). | **Confirmed.** `ResumeChoice.Waits` retains the distinction at `Driver/Accounts/Rotation/ResumeAccount.cs:78`; the roster repeats it at `Driver/Accounts/Harnesses/HarnessRoster.cs:1008`; the chat wake returns the own account’s reset at `Driver/Conversations/Chat/LimitWaits.cs:185–186`. | LIMITSWITCH1 belongs in one account disposition policy, then each reader moves onto it. |
| Pushes, invalidation and held answers represent overlapping machinery (§3.4 row 3). | **Qualified.** The page already excludes live pushed entries: `Web/bridge/state/stateHub.ts:80–88`, `Web/app/ShellSignals.tsx:94–122`, and `Web/bridge/state/slices.ts:309`. Detail queries beneath those families still pull. | Preserve D182’s protocol. Replace implicit dependency invalidation, not the working snapshot cache. |
| Words pass through one say door (§2.1, §2.3). | The seam exists, but its strongest guarantee is **not enforced everywhere**. Ordinary dispatch explicitly continues after an attempt mark fails: `Driver/Conversations/SaidDoor.cs:50–62`, `:84–90`. The service deduplicates a producer key only while its word remains on `said`: `Core/Sessions/SessionLedger.cs:955–961`. | A shared dispatcher alone cannot establish crash-safe, at-most-once attempts. Delivery receipts and mandatory attempt acknowledgement are required. |
| Held words lose their key and choice (§2.4, final paragraph). | **Confirmed, with another loss:** the shell’s fallback `Word` retains only text, files, door and display choice (`Modules/Loop/SessionWords.cs:179`); construction drops the incoming key and level (`:464`); persistence drops the display choice too (`:920–929`). The record write supplies no key (`:701`). The headless fallback also omits its request key (`Driver/Conversations/GoOn/SessionSay.cs:198`). | This is the first repair: a small change at a common loss boundary, before changing movement policy. |
| Stored `Actor` and inferred role duplicate one fact (§1.3 row 8). | The fields exist as described, but they do **not all mean the same thing**. `Actor` records execution provenance (`Core/Sessions/Sessions.cs:195–204`); `Serves`, `Piece` and `Opinion` record relationships (`:138–165`). | Preserve both meanings. Stop using actor provenance as a substitute for participation or permission. |
| Native lineage has no production reader (§1.3 row 17). | Confirmed in the inspected production paths. `ChildLineage.Read` folds the file and maps unreadability to an empty fold (`Driver/Sessions/ChildLineage.cs:125–137`). The connector records lineage without enforcing it (`Mcp/CallLineageFilter.cs:17–21`). | COWORKVIEW1 supplies the reader; C2a supplies enforcement. Neither may turn “unreadable” into “no children”. |

The survey’s 51-of-273 classification remains its historical judgement. I have not independently recounted it or established a numerical defect-reduction forecast.

### Who acts

#### Participant, actor and appointed role

**Answers 1, 4 and 5.** There should be one `Participant` **read contract**, not one new persisted session record for every entity.

```text
Participant {
  id
  subject: SessionPart | BrainstormPass | MeetingTurn | RoleHolder | NativeChild
  scope: machine, workspace?, repository?, entry?, part?
  relations: serves?, piece?, opinion?, parentParticipant?
  actor?: ActorId
  bindings[]
  visibility: opaque | observed | connected | recoverable
  provenance
}
```

The desktop-side .NET `ParticipantReader` owns this projection. It joins existing records; it does not own their lifecycle or store another copy of their state.

`RuntimeBinding` is the separate, machine-local identity contract:

```text
RuntimeBinding {
  id, participantId, generation
  adapter, door, harnessIdentity
  executionRef?, accountRef?, keptConversationRef?
  nativeIdentity?, lineageEvidence?
  visibility
}
```

The local service owns binding issuance and lookup in new local-only tables. Desktop execution code supplies process and harness facts. A shared host exposes neither these tables nor their routes. Accounts, process references and conversation identifiers have no sync-wire fields.

| Today’s participant | Target identity and owner | What remains distinct |
|---|---|---|
| Build, chat, intake, Ask Daoris, helper, partner, reviewer | Participant references the existing service session part. Existing part id remains the connector’s root subject during migration. | Work kind, relationship and permission are separate fields of the projection. Existing session state stays in `SessionStore`. |
| Brainstorm voice or synthesizer | Participant references its existing pass; a binding is registered before its connector starts. | Voices remain a frozen parallel set, not fallback choices. Today their frozen entries and synthetic connector identity are visible at `Driver/Loop/DriverBrainstorms.cs:346–360`, `:664–669`. |
| Meeting turn | References a meeting turn only when a real executor exists. | No fabricated running participant for today’s unavailable executor. |
| Co-worker | A relationship projection over helper, partner and reviewer participants. | No additional “co-worker session” or team lifecycle store. |
| Supervisor or steward holder | Participant references the desktop’s holder identity. Appointment references that participant and its generation. | Appointment authority stays in `StandingConfirmation`, outside ordinary session permissions. |
| Native child | Observed child references its part and harness identity. A **connected** child receives a locally issued binding after lineage verification. | No new session, quest or slot. Missing parent, end, model or connection remains unknown. |

For existing session roots, identity is a mapping to the existing record, not a second row containing its lifecycle. For subjects without a session, the binding directory stores only identity and references. C1a’s lineage remains the evidence store until its readers have moved through the binding interface.

An appointed role is **held by a participant**. That participant may eventually run through an actor’s execution choices, after SUPERVISOR1c’s measurements. Holding an appointment does not turn the participant into the owner. Actor choice answers *which execution to use*; appointment admission answers *which exact act this holder may perform*.

The existing proof boundary survives: generation, live holder, nonce, HMAC, caller placement, exact form and owner overrule are checked at `Modules/Platform/StandingConfirmation.cs:1213–1289`. Both roles remain dark while their current flags are false (`:330`, `:336`). This refactor does not enable them or move their key into the pool.

#### Rules and admission

**Answer 2.** Unify the **resolution contract**, while retaining typed policy variants:

```text
ParticipationPolicy {
  sourceScope, revision
  enabled, consentMode
  permittedAgents
  concurrency
  executionBounds
  capabilityProfile
  requirements: typed requirements for this work kind
}
```

The variants retain reviewer independence and rechecks, partner confinement evidence, helper bounds, brainstorm fan-out and hand-over consent. Flattening these into `{ mode, agents }` would discard rules rather than consolidate them.

Existing configuration remains authoritative. `helpers`, `opinions`, `handOver`, `brainstorms`, `intakeAdapter`, `helperAdapter` and actors become inputs to this resolver. Defaults remain their current defaults. In particular:

- An absent partner policy does not become permission to start partners.
- A parsed brainstorm mode that currently changes no start does not acquire new behaviour inside an extraction.
- Ask Daoris’s existing enabling key becomes an adapter to the resolved policy, rather than a second check beside actor resolution.

Admission becomes a shared, staged judgement:

1. Resolve the subject and scope.
2. Resolve policy and the proposed act.
3. Verify the binding and lineage required by that act.
4. Check kind-specific requirements: maker independence, measurement, appointment proof.
5. Reserve concurrency.
6. At execution, verify the reservation, current grounds and containment outcome.

This is one rule composition, **not one distant approval whose result every executor trusts forever**. The connector enforces tool admission; the launcher enforces execution containment; role doors verify their own cryptographic proof. Each consumes the shared classification and returns closed refusal codes.

The current connector’s missing-record path is open (`Mcp/RoleFilter.cs:110–114`), while its unread-record fallback uses the partner allow-list (`:138–140`). Replace both for **named Daoris subjects** with explicit unknown-subject admission: registered reads only; restricted acts require positive evidence. An unnamed outside connector retains D173 C2a’s separately declared scope.

A production partner still requires its measured row. That table is empty today (`Driver/Conversations/Domain/PartnerMeasurements.cs:44`); this proposal does not populate it by inference.

#### Agent, model and account choice

**Answer 3.** Every Daoris-controlled start goes through one `ExecutionResolver`, built from `ActorChoices.Effective` and D197’s account walk. Its output is a settled `ExecutionSelection`, not an invitation for the caller to walk again:

```text
ExecutionSelection {
  actor, policyRevision
  choice, agent, askedModel, askedEffort
  account, harnessIdentity
  passed[]
  reservation
}
```

Evaluation is uncounted. Only the committed reservation counts. The launcher consumes that reservation once, and failure releases it once.

| Start path | Target treatment |
|---|---|
| New Build, intake, chat, Ask Daoris | Common choice-major walk, preserving each start’s posture and policy fences. |
| Driven carry-on and retry | Resolve from the previous execution’s agent and asked choice, then apply the permitted movement policy. Retire the `config.Adapter` plus previous model combination at `Driver/Loop/Driver.cs:1917`, `:2020`. |
| Same-conversation go-on | Reapply the recorded execution. It is not a fresh actor walk. |
| Terminal start-from | Pass Chat’s whole effective order, as the window does. It currently passes only `FirstAgent` at `Modules/Bridge/Routes/SessionsVerbs.cs:32–33`. |
| Reviewer, helper, partner | Typed candidate fences and independence requirements feed the common walk. Retire the helper’s dependency on an opinion-shaped result and wording (`Driver/Conversations/Helpers/HelperChoice.cs:66–70`). |
| Brainstorm | Resolve each frozen voice once with its workspace. It currently supplies no workspace to account selection (`Driver/Loop/DriverBrainstorms.cs:682–683`). No replacement of an unavailable voice. |
| Hand-over | Named agent/account constraints become explicit selection inputs; no fallback outside them without the existing consent rule. |
| Role holder | Enters the resolver only when its approved actor-holder row is built and measured. Until then, its existing pinned execution remains an explicitly named exception. |
| Native child | Observed, never selected or started by Daoris. |
| Meeting | No execution bypass is invented while no real start exists. |

D205 remains the model-choice policy. The actor’s requested tier, acknowledged configuration and reported tier remain separate; absent evidence is not displayed as account default or zero usage.

### How work moves

#### One judgement, separate executors

**Answer 6.** Introduce a pure `MovementPlanner.Decide(snapshot, intent)` used by both loops.

| Input | Required facts |
|---|---|
| Identity | Entry, source part, current binding, source execution and selected constraints. |
| Intent | Person’s words, solicited answer, go-on, limit, curve, hand-over, briefed continuation, workflow send-back, transient retry or explicit stop. |
| Work ownership | Quest/run revision, question generation, appointment grounds where relevant, tree ownership and current move generation. |
| Availability | Eligible accounts and choices, cool-offs, offered models, harness capabilities and kept conversation identity. |
| Delivery | Kept word ids, producer keys, authors, order and durable dispositions. |
| Boundary | Turn running, between turns, process exited, press reservation or winding up. |

Its result is one of:

```text
KeepRunning | ResumeExisting | RetryExisting
Wait(reason, eligibleReset?)
BeginMove(target, contextMode)
NeedsPerson(reason) | Finish
```

The result includes required preconditions and intended effects. It performs no file copy, model call, spawn or ledger write.

The look and chat runner remain adapters and executors. A transient retry stays inside the live protocol run, where its conversation still exists; it does not round-trip through the look. A workflow press retains its turn and tip reservation. The shared planner decides disposition without becoming a scheduler for all Fabric activity.

#### Account policy

**Answer 7.** State the rule once:

1. A resumable conversation uses its recorded account when that account is eligible and ready.
2. For an ended record with words, a cooling account does not pin the words while another **permitted** execution can run: LIMITSWITCH1.
3. A new part takes one settled selection, carried through to its actual open.
4. When all permitted candidates cool, wait until their earliest eligible reset. Signed-out, refused, unoffered or otherwise ineligible candidates do not contribute a fictitious reset.
5. An explicit account constraint is distinct from an agent constraint and from the mere fact that the previous part used an account.

The planner must receive selection provenance. `Profile` alone cannot prove an explicit account pick: `ResumeAccount` documents that the record does not keep whether it was picked (`Driver/Accounts/Rotation/ResumeAccount.cs:15–17`). Likewise, today’s `Choice == "pick"` check prevents a move to another **agent**, after same-agent account selection has already happened (`Driver/Conversations/Chat/LimitMove.cs:85–104`, `:447`).

This is a real policy discrepancy, not an extraction detail. Append a dated clarification to D206 and D137:

- Newly recorded explicit account constraints remain pinned.
- Agent picks constrain the agent.
- Historical records with no account-choice provenance follow LIMITSWITCH1’s latest owner-directed rule when ended; Daoris does not invent a historical pin.
- Their recorded account still wins when ready.

This explicitly accepts a residual: the original intent of an old account selection cannot be reconstructed. The compatibility cases must cover it.

A selection also owns its reservation. The current live limit move selects once at `Driver/Conversations/Chat/LimitMove.cs:85–86`, then starts with `profile: null` at `:141–144`. That second walk disappears.

#### Durable movement and context

**Answer 8.** Every accepted move has one `MoveRecord`, including a same-conversation resumption that owns delivery across a restart:

```text
MoveRecord {
  id, version, generation, intentKey
  entry, sourcePart, sourceBinding
  trigger, requestedBy, groundsRevision
  settledSelection
  context: Resume | Carry | Brief
  contextRecipe, sourceBounds
  destinationPart?, destinationBinding?
  phase
  deliveryIds[], deliveryDisposition
  refusalOrFallbackCode?
}
```

The driver owns movement semantics. The local service owns persistence and conditional updates in new local-only move tables, so move claims, destination allocation and word ownership can be committed with session records. Its routes store and validate the driver’s protocol; they do not select accounts or inspect harness homes.

Both executors claim a move by entry and generation. This matters because conversations can continue while the shell waits for another loop’s home lease (`Modules/Loop/DriverLoop.cs:839–842`). A home lease alone is not the exclusion mechanism for every conversation.

| Phase | Durable effect and recovery |
|---|---|
| `prepared` | Claim source generation, record grounds, selection and delivery references. Competing intent receives the existing result or a superseded refusal. |
| `quiescing` | Await the required natural boundary. Preserve the existing difference between a person’s explicit cut and an agent’s hand-over request. |
| `opening` | Allocate the destination idempotently under `moveId`. A lost open answer finds that exact destination; it never searches approximately by time and opens another. |
| `context-ready` | Resume the kept id, verify a carry, or compose the bounded deterministic brief. No delivery prompt precedes this phase. |
| `delivering` | Transfer eligible word ownership transactionally, record attempts, then let the one dispatcher write. |
| `settled` | Publish the destination and outcome; retain the receipt needed for deduplication and recovery. |
| `held` / `uncertain` | Keep the reason and retained references. Unknown external effects do not cause an automatic second spawn or prompt. |

Existing hand-over and limit boxes are first read through this interface. After all their readers use it, import each active box idempotently and retire its executable old representation. New movement meaning belongs in new tables; old `from`, `entry`, state and limit values retain their meanings.

Context selection is a separate capability judgement:

- **Resume** only the kept conversation id on its recorded execution.
- **Carry** only a measured, compatible same-agent/account-to-account case, after exit, create-only copy, load and read-back.
- **Brief** through `ContextBriefs`, using the source bounds recorded by the move.

SWITCHCTX1’s carry becomes a context strategy and phases of this record, not a second delivery owner. Its replay, model, compatibility, source-exit and first-turn checks remain. Its `.carry.json` and the limit box do not both own pending delivery.

A fallback before a destination names a conversation can use that destination’s first open. A fallback after it has named one creates the specified further part. It never silently gives one record a second conversation.

#### Words and failure outcomes

**Answers 9 and 10.** Introduce one immutable `WordEnvelope` and one delivery contract:

```text
WordEnvelope {
  producerKey, wordId?, sourcePart, entry
  author, textRef, attachmentRefs
  level, order
  displayChoice?
  supervisorDecisionRef?
}
```

Display choice is not `SaidWord.Choice`, whose current service meaning is reserved for a supervisor’s verified release (`Core/Sessions/SessionLedger.cs:896–906`). An ordinary word never gains role attribution because it carries a selected label.

The construction rule is:

1. A producer assigns its key once.
2. The service keeps the word on `said` before a dispatcher or move holds it.
3. A move keeps references and recipes, not another text queue.
4. The service records an acknowledged attempt before the wire.
5. A written word receives a durable disposition; its producer receipt remains after it leaves the pending list.
6. Recovery sends only words proven never attempted. Attempted words with no conclusive receipt become visibly uncertain.

`DrivenInbox` and `ChatTurns` keep their scheduling behaviour but implement this same contract. The shell and headless doors use the same keep-and-route operation; a winding-up record is no longer a shell-only durable queue versus a headless `running` refusal (`Driver/Conversations/GoOn/SessionSay.cs:206–210`).

The brief’s “never twice nor lost” needs a sharper question: **what can be guaranteed across a non-idempotent harness write?** Without a harness receipt or idempotency key, Daoris cannot atomically commit its database and the harness’s acceptance. The enforceable guarantee is no automatic second attempt after an uncertain write, durable retained evidence, and a visible way for the person to say it again. Uncertainty must not be described as successful delivery or silently discarded.

The four error kinds feed the planner as typed outcomes:

| Outcome | Shared disposition |
|---|---|
| Limit | Preserve the limit reading and cool-off; no strike. Resume, move or wait through the account policy. |
| Access | Retain delivery and report the required person action. No transient retry and no implicit grant. |
| Transient | Same conversation, account, choice and slot; D210’s five waits and owner check. No movement, park, strike or cool-off. |
| Fatal | End with the reason. Subsequent recovery is a separately admitted intent. |

Stops and model refusal before a prompt remain separate outcomes. The reader’s source discipline stays at the interface: `AgentErrors.Read` already distinguishes harness-side access evidence from provider failures (`Driver/Agents/Errors/AgentErrors.cs:144–164`).

AGENTERRORS1b’s new chat retry stays built. Its open follow-up about words carried by a limit-refused try must be resolved through dispositions, not another scan that guesses whether a word can be resent. `LimitHeld` currently clears prompt candidates on transient events (`Driver/Conversations/Domain/LimitHeld.cs:35–41`); the replacement consumes the recorded disposition.

### What is said

#### Event, source and ordering

**Answer 11.** A Fabric event is an immutable description of a committed transition:

```text
FabricEvent {
  id, schema, kind
  sourceStore, sourceSequence
  subject, scope
  operationId?, causeId?
  occurredAt
  facts: closed payload for this kind
}
```

The owner of the transition mints its identity at commit. Database-backed changes write their event outbox entry in the same transaction. File-backed transitions either move to their owning journal transaction or retain an explicitly weaker observation contract; a watcher must not manufacture a complete history from its latest snapshot.

A bounded local outbox is a **delivery record**, not a second domain store and not an event-sourced database. Sessions, appointments, quests and moves remain authoritative. Startup rebuilds state from those stores.

There are three different orders:

- **Source sequence:** committed order within a source store.
- **Delivery sequence:** the relay’s `run, seq`, ordering what it tells.
- **Projection version:** D182’s slice `epoch, seq`, ordering snapshots and deltas.

These identifiers are related, not interchangeable. Timestamps do not create a total causal order across processes.

Move phase events are committed with their move transitions. Legacy database writers are covered by store-level transition capture or reconciliation explicitly reported as incomplete. The current observer-created `store_identity` moves into schema initialization; observers stop writing schema (`Core/Platform/ChangeStream.cs:483–493`).

#### Closed vocabulary and sentences

**Answer 12.** Use subject-specific codes. A part ending, a line finishing and work reaching the line are different facts.

| Canonical kind | Closed facts | Existing projections |
|---|---|---|
| `part.parked` | Part, question generation, wait reason | Park row, attention, eligible external `session.parked`. |
| `part.ended` | State, cause, limited, requestedBy | Session state, log, eligible end notice and legacy end adapters. |
| `account.limited` | Local binding/account reference, reset, stated/unstated reading | Account state, wait wording and movement wake. |
| `move.prepared`, `move.settled`, `move.held` | Move id, source/destination references, context mode, reason | Parts strip, move note, log. |
| `work.landed` | Landing outcome and work reference | Landing observation; does not claim the line holds the work. |
| `work.integrated` | Proof that the line holds the work | Fabric end-cycle judgement. |
| `person.waiting`, `person.answered` | Wait identity, generation, exact outcome | Confirmation lists, attention and notice receipts. |
| `turn.answered`, `channel.replied`, `question.answered` | Their own correlation references | The different meanings currently called “answered”. |
| `delivery.uncertain` | Word and attempt references | Lost/uncertain wording and explicit resend action. |

The internal `part.ended` event covers all part ends. Notification policy decides which warrant an *Ended* announcement. EVENTHOOK1’s existing external `session.ended` keeps its narrower published meaning through an adapter; its grammar is not silently redefined.

One code registry declares payload fields, privacy class and rendering family. The page composes chrome from English and Chinese catalogues. Authored content remains verbatim. Terminal and relay renderers produce their fixed sentences from the same coded facts; internal enum names never become fallback prose.

#### Channel disposition

| Today’s channel | Target |
|---|---|
| Pushed slices | Stay authoritative .NET projections with their existing epoch, base-sequence, manifest and resync rules (`Modules/Platform/StateSlices.cs:22–57`). Events wake their owners; they are not reconstructed in the page. |
| Held answers | Remain demand-driven detail projections, with explicit input dependencies and versions. A stale completion cannot become current merely because it finished last. |
| `STATE_CHANGED` | Compatibility wake for unmigrated readers and plugins. Migrated readers declare exactly which source changes invalidate them. |
| `DRIVER_TICK` | Compatibility projection of the look’s report. Its data and broad invalidation responsibilities retire separately. |
| `UNDER_WAY`, confirmations, command answers | Correlated operation state. Command answers remain private to their requester. |
| Hook observations | Project canonical events through each hook’s contract. |
| Hook waterfall and work commands | Stay commands or decisions; they are not converted into broadcast facts. |
| Chain events | Typed internal dependencies over the same vocabulary. Do not bind unused ranks merely because they exist (`Driver/Platform/Chain.cs:170–172`). |
| Attention watch | Becomes an attention policy projection. It no longer establishes the occurrence or identity of a park by comparing snapshots. |
| Notices | One durable notification projection and receipt owner. The look, slice and headless host submit facts rather than independently editing notice state. |
| Machine log | Diagnostic projection plus operational diagnostics that are not domain events. It is not the recovery ledger. |
| Session event files | Remain authored transcript and protocol evidence, including tools, thoughts and receipts. Lifecycle notes become projections; transcript frames do not all become Fabric events. |
| Lineage files | Evidence store for child bindings and COWORKVIEW1. Several append producers do not imply several authorities for child identity. |
| Ask channel | Remains the durable message queue and its own acknowledgements. Channel messages are not replaced by event wakes or copied into `said`. |
| EVENTHOOK1 relay | External observation adapter over eligible events, preserving its grammar, bounds, watermark and loss reporting. |

Held answers particularly need this dependency treatment: today broad `STATE_CHANGED` and presses forget the entire cache (`Modules/Bridge/DriverModule.cs:109–113`, `:245–248`), while `HeldAnswers.Land` fences only competing work completions (`Modules/Bridge/HeldAnswers.cs:147–160`).

#### Readers and disclosure

**Answer 13.**

| Reader | May read | Excluded |
|---|---|---|
| Desktop page | Allow-listed desktop projections, scoped to its workspace; transcript/detail through their existing doors. | Credentials, role keys and arbitrary event payloads. Machine paths only in explicitly desktop-only projections. |
| OS notifications and taskbar | Notification projection with localized chrome and navigation references. | Raw provider payloads, transcript bodies and arbitrary log fields. |
| Plugin | Its approved observation projection. EVENTHOOK1 retains generated ids and closed codes. | Names, paths, account references, authored text and secrets in external event frames. |
| Outside agent session | EVENTHOOK1’s validated fixed sentences and watermark. | An event is no authority to act, publish or write elsewhere. |
| Terminal | Same .NET judgements through the running instance, or the same implementation alone under its lease. | Role keys and broadcast command results. |
| Teammate’s mirror | Existing permitted session, quest, ask, channel and opinion records through their existing sync contracts. | Runtime bindings, moves, word receipts, streams, accounts, appointments, lineage and the local outbox. |

The existing session feed already demonstrates the structural boundary: `FedSessionRecord` has no account, `said`, actor or conversation-id field (`Core/Sessions/SessionFeed.cs:22–26`). New local information stays absent from that wire.

#### Durability and loss

**Answer 14.**

| Information | Kept and recovered |
|---|---|
| Domain records, pending words, move phases, delivery receipts | Durable. Recovery reads their owner and resumes only an idempotent or proven-unattempted step. |
| Event outbox | Bounded durable delivery history with an earliest retained cursor. A cursor before it receives an explicit gap, then a snapshot/reconciliation instruction. |
| Slice snapshots and held-answer caches | Memory only. New epoch after restart; baseline and resync preserve “unknown” versus “empty”. |
| Plugin queues | Bounded memory, separate per plugin. Overflow, invalid frame, reconnect and changed run report loss. |
| Native lineage | Durable evidence, possibly partial. Missing, unreadable and successfully read-empty are distinct results. |
| Active transient episode | Live-run state. Restart settles its attempted words; it does not silently reset five tries and replay the failed prompt. |
| Diagnostic log | Best-effort retained diagnostics, never proof that a domain effect committed. |

A notification baseline is not an event baseline. Reopening the app must reconstruct current waits and unread receipts without replaying every old balloon. A park immediately before restart remains a visible wait even when no announcement is repeated.

### Record ownership and compatibility

**Answer 15.** A concept can have an input, execution state and result under different owners without having three authoritative copies of one fact. The ownership boundary must name the fact.

| Survey §4 records, including its referenced area tables | One owner after migration | Sync and older-build treatment |
|---|---|---|
| `sessions`: lifecycle, marks, `from`, `entry`, limit, answer, note parts; actor/asked/override/ran | Service `SessionStore`; driver supplies execution provenance. | Existing permitted fields travel. Existing state and field meanings stay unchanged. |
| `said`, attempts and take-back | Service word ledger. New delivery receipts and movement ownership are local-only. | No new wire fields. Old words keep their old reading; receipts go in new tables. |
| Quests and `quest_log` | Service quest store. | Existing sync and replay remain. Events describe operations rather than replacing them. |
| Asks, channel messages, marks, acknowledgements and cursors | Service ask/channel stores. | Existing wire contracts remain. |
| Opinions table | Service opinion store. | Existing feed treatment remains. Driver gate and due records do not become another opinion result store. |
| Family revisions and `store_identity` | Owning service stores and schema initializer. | Existing change-stream compatibility; no observer schema mutation. |
| Helper ask/answer; partner answer; question; agent hand-over ask; brainstorm request/answer | Existing service domain stores. | Local-only shapes stay local. Their driver readers use typed adapters. |
| Helper state/capture/back; partner state/facts/delivery and piece ref | Driver execution/delivery owners. | Local-only. Delivery references converge on the word/move receipt contract; payloads and git work are not copied into participants. |
| Opinion gates, dues and work packets; brainstorm passes, meetings and rooms | Driver’s respective execution owners. | Existing records remain canonical until every reader uses the concept adapter. |
| `.harness.json` | Binding store after import; desktop alone resolves its local execution details. | No wire field. Retain its old reader until all resume paths use the binding interface. |
| `.cannot.json`, `.reopened.json` | Driver continuation state; become projections/receipts of the movement contract where applicable. | Old notes retain their meaning. |
| `.new-session.json`, handed-quest records, hand-over boxes and ask-state, `.limit-move.json` | Requests remain requests; accepted execution and recovery belong to `MoveStore`. | Import once with a migration receipt. Never run an imported request again beside its move. |
| `held-words.json` | Initially shell-owned compatibility queue; ultimately replaced by service-kept words plus movement routing. | First repair preserves metadata additively. Retirement occurs only after all producers keep first. |
| Ask Daoris `.help.json` | Help-history owner; its hand-off link becomes a move projection. | Existing name/pin/history values remain unchanged. |
| `cooling.json`, `driver.json` rules and actors, `agent-offers.json` | Driver account, configuration and offers owners. | Local-only; the Fabric does not duplicate or sync them. |
| Appointments, supervisor decisions and `.states`, supervisor release state, steward effects/receipts, holder rooms | Desktop authority subsystem. | No sync. Exact forms, generations and proof rules remain; new authority meanings are never inserted where older builds could execute them. |
| Lineage call/hook/stream/end lines | Lineage evidence store; binding issuer owns issued identity, reader owns the fold. | Local-only. Preserve provenance; do not replace old evidence with guessed lifecycle. |
| Session `.events.jsonl` | Session’s execution writer. | Local-only transcript. Any writer hand-off must preserve sequencing; current sequence locking is instance-local (`Driver/Sessions/Events/SessionEventLog.cs:43–63`). |
| Notices and unreadable notice backups | One notification store owner. | Preserve existing ids, receipt meanings, retention and unreadable evidence. |
| Machine logs | Existing diagnostic owners using a shared event schema. | Local-only; twin implementations and parser remain tested until replaced together. |
| `pipe-waiting`, terminal `.told`, `running.json` | Confirmation store; request adapter; instance-address owner, respectively. | Local-only. Events describe them but are not their recovery state. |
| Slice snapshots, held answers, presses underway, confirmation lists, watch baselines, announcement sets, stream cursor, hook queues, plugin health | Their single projection or execution owner. | Rebuildable memory; restart and loss are explicit. No accidental promotion into sync records. |

Compatibility is a landing condition, not a version flag added at the end. Each new store tests the older build’s actual reads and writes. Legacy session fields are not repurposed to hide new movement or authority semantics. Importing a box must also prevent its old executable representation from remaining independently actionable.

A downgrade’s mid-move safety is therefore something each store row must prove. If an older reader could still start or deliver from the legacy representation, that migration row does not land until the mapping supplies a safe old reading.

### Names

**Answer 16.** The brief’s use of *line* needs correction: the glossary’s `line` is the repository branch, **主线**, at `Web/locales/glossary.json:530`; the work spanning parts is the existing **session / 会话** at `:482`.

| English / Chinese | Meaning and code treatment |
|---|---|
| Fabric / 协作底座 | Technical name for the shared contracts. No new screen label. Add a glossary explanation before any visible use. |
| actor / 角色 | D205’s work kind and execution choices. Preserve `ActorChoices`; use typed `ActorId`. Existing glossary: `:475`. |
| appointed role / 任命职责 | Authority granted by an appointment. Qualifier distinguishes it from actor. The page keeps supervisor / 主管 and steward / 管家 (`:547–548`). |
| participant / 参与方 | Identity of an entity acting or observed in the Fabric. Internal term; no universal participant screen. |
| session / 会话 | One repository’s line of work; preserve its entry identity. |
| part / 段 | Existing ledger record within that session (`:483`). |
| line / 主线 | Repository branch. Do not rename it. |
| move / 转接 | Internal execution transition; replaces the several *move-in-flight* implementation records after migration. Existing user-facing hand-over and go-on wording remains. |
| event / 事件 | A committed fact, distinct from a command, state snapshot and transcript frame. |
| channel / 频道 | Existing ask conversation and queue (`:494`); unchanged. |

Rename the connector’s `SessionRole` to a participation classification when its readers migrate. Its current `Serves/Piece` shape (`Mcp/RoleFilter.cs:13–19`) is neither a D205 actor nor an appointed role.

### In-flight designs and decision amendments

| Input | Disposition |
|---|---|
| **MODELROLE1 / D205** | **Keep built; extract its shared execution walk.** Preserve choice order, fences, model carriers, provenance and person-only spending changes. |
| **AGENTERRORS1 / D210** | **Keep built rows 1a, 1a2 and now 1b.** Remaining door and kind rows normalize into the same outcomes and delivery dispositions. No new outer retry loop. |
| **LIMITSWITCH1** | **Build early through the movement/account contract.** Driven, repository-chat and Ask Daoris readers migrate separately; all use the same eligible-reset calculation. |
| **COWORKVIEW1** | **Build settled row a as designed, then adapt its participant reads.** Preserve versioned dependencies, partial-line handling, unknown/unreadable states and bounded views. Re-cut the routing half against participation policy; do not smuggle it into row a. |
| **SWITCHCTX1 carry** | **Re-cut its durable ownership into `MoveRecord`; preserve its conservative carry protocol.** The accepted line view remains built. Carry stays behind its outstanding reads and capability evidence. |
| **EVENTHOOK1** | **Keep the relay boundary and grammar; re-cut internal production.** External v1 filtering, generated ids, fixed sentences, job checks, bounds and `events.missed` stay. Canonical source facts replace attention-watch differences as the eventual source. |

Append dated amendments to:

- **D137/D188:** complete word identity, durable dispositions, acknowledged attempts and explicit uncertainty.
- **D137/D206:** LIMITSWITCH’s unified account policy and explicit account-versus-agent selection provenance.
- **D173/D203:** participant projection, issued bindings and the distinction between actor provenance, collaboration relationships and appointed authority.
- **D189/D131:** carry only under SWITCHCTX1’s approved conditions; one conversation identity per record remains.
- **D119/D173:** canonical event sources and adapters, without silently changing existing hook meanings.
- **D182:** dependency-versioned held detail and durable event delivery bookkeeping; retain its rejection of an event-sourced domain database.

D208/D209’s authority bounds need no widening.

### Migration in build order

**Answers 17 and 19.** Each numbered row is its own branch. Before production adoption, its new type is read beside the old result and a census names every migrated and explicitly remaining path. Intentional disagreements have a decision amendment and a failing regression case.

Every row retains the brief’s complete baseline gates. During development, run the reached suites whole; at landing, require the complete gate set, including both Process halves, modules, web types/rules/unit/e2e/measured, deployment and family rehearsal. Follow the repository’s serial Process/rehearsal rules in `docs/development.md:88–103`. Proofs below are requirements, not claims of completed testing.

| Row | Standalone landing | Its own proof |
|---|---|---|
| **1 — one day: preserve a word’s identity at the shell boundary** | Add `WordEnvelope`; move all held-word readers through it before extending persistence. Preserve key, level and display choice through `SessionWords`, its pump/restore, and the headless record fallback. Preserve the distinction from supervisor proof choice. No movement-policy change. | Unit: same producer retry keeps one pending word; restart retains key, level, attachments and display choice; ordinary choice never becomes supervisor attribution. Census: every shell/headless keep supplies identity or names an explicit legacy case. Run `SessionWordsTerminalTests`, session-word waiting/message suites and service say/store tests. Process: restart while winding up, then one delivery. Family/deployment regression unchanged. |
| **2 — movement contract, no new store** | Add `MovementIntent`, `MovementSnapshot`, `MovementDecision` and settled-selection types. Adapt A–N from survey §2.2 as a census. Compare current dispositions, naming deliberate disagreements. Existing boxes remain authoritative. | Unit table covers ready/cooling/missing/refused/unoffered, explicit constraints, ownership changes and every natural boundary. Differential tests reproduce current paths. Run the whole continuation, hand-over, limit, rotation, planner and chat suites. No installed performance claim: production behaviour is unchanged. |
| **3 — LIMITSWITCH on driven ended records** | First production reader of the account disposition. Replace the cooling hold at `Driver/Loop/Driver.cs:2031–2039`; carry one settled selection to the existing briefed start. Append the account-policy amendment. | Unit: own ready wins; own cooling plus another eligible account opens one briefed part; all cooling uses the eligible earliest reset; access is not a reset. Process: real ledger, second account, one part and each word once; stop/supersession wins. Run full `ContinuationTests`, `SessionMessagesTickTests`, `LimitResumeTickTests`, `AccountRotationTickTests`, `PlannerTests`. Install: observe its next qualifying move and time-to-go-on. |
| **4 — LIMITSWITCH for repository chats** | Move chat go-on and wake onto the same policy; remove the second selection at the live limit move. Keep chat execution separate. | Full `ChatGoOnTests`, `ChatGoesOnProcessTests`, limit-wait/goes-on/elsewhere Process suites and `HandOverTests`. Add words arriving between selection and open, all-cooling earliest reset, and explicit constraint cases. Install: record judged versus opened account and reservation count. |
| **5 — Ask Daoris movement reader** | Adapt its placement, go-on, replacement and limit wake. Use the same disposition with its workspace and room constraints. | Process: New chat racing finishing/opening; words during replacement; limit wake retains room and words. Help coverage and existing chat suites whole. Install: next natural replacement/limit case, with no blank history. |
| **6 — all failure exits use typed outcomes** | Preserve built retries; normalize remaining opens, pipe failures and one-turn kinds in separate subrows, one door/kind per branch. | `AgentErrors`/`AcpTransientTests`, full `TransientRetryTickTests`, `ChatTransientProcessTests` and affected one-turn suites. Process: code-only limit, access at open, transient then success, exhausted transient, transient then limit/fatal, stop during delay. No transient walk or strike. Real provider-outage proof remains pending until one occurs. |
| **7 — every movement reader uses the contract** | Move curve, hand-over, briefed move, driven carry-on and workflow send-back adapters one at a time. No new movement persistence yet. | Census has no unnamed A–N path. Full hand-over, curve, continuation, inbox, chat-turn and workflow suites. Process: turn-boundary races; a person’s cut versus an agent request; tip and ownership reservations remain valid. |
| **8 — durable move store and word ownership** | Introduce local-only tables after readers use the interface. Import each old active box once. Add unique operation/destination allocation and conditional word transfer. Migrate one box family per branch. | Service transaction tests and old-build statement tests; wire-absence census. Process: kill after every phase, lose each response, restart with concurrent words, two competing intents, service restart, no second destination. Family rehearsal exercises real service and both executors. |
| **9 — mandatory attempts and receipts** | Replace fail-open attempt callbacks and residual process-only holding with the acknowledged delivery contract. Add producer receipts after pending removal. | Full word, inbox, chat, continuation and transient suites. Process: attempt commit fails; commit succeeds but reply is lost; crash before/after wire; duplicate producer request after removal. No automatic resend of an uncertain attempt; uncertainty visible in en/zh. Install: delivery latency and service-write cost before/after. |
| **10 — SWITCHCTX1 strategy** | Carry through move phases, first its measured conversation case, then each other eligible path separately. Remove separate carry delivery ownership. | Source-exit, create-only copy, replay mismatch, model mismatch, stopped load, first-turn rejection, fallback and crash cases. No prompt before context readiness. Install’s next natural move supplies a narrowly scoped receipt; unobserved cases stay unmeasured. |
| **11 — shared execution resolver** | Extract each existing actor walk and model re-ask into one resolver; fix terminal start-from and carry-on provenance; retain policy-specific candidate planning. | `ActorChoicesTests`, actor-run, helper/opinion/partner/brainstorm suites and start census. Process: later choice, model refusal before prompt, no fallback after prompt, workspace pin, exactly one reservation. Family/deployment cover packaged starts. |
| **12 — participant projection and COWORKVIEW1 row a** | Build its settled read, then adapt identity reads and cap/classification consumers to `ParticipantReader`; retain partial visibility. | Session/store, helper, child-lineage and hook Process suites; no new service session per child; unreadable differs from empty. Window/pipe/help censuses. Install’s specified child runs in both languages, widths and themes. |
| **13 — participation policy and admission** | Move one kind and one door at a time. Introduce binding issuance for non-session subjects. Keep appointment proof protocols and dark flags. Stage C2a only after real-door lineage evidence. | Full `McpToolsTests`, role-filter tests, `HelperLookTests`, pipe/standing censuses and confirmations; partner evidence digest cases. Real root/child/grandchild calls on each exact supported door; unknown lineage refuses restricted acts. |
| **14 — event contract and source adapters** | Add code/payload/privacy registry and source-event identity. Compare current end/park/limit/landing channels before replacing a reader. Add transactional outbox capture after source readers use the contract. | `NoteCodesTests`, log/schema and event censuses; direct connector writes captured; source event and state commit together; rollback emits nothing; older writer compatibility. Process: park then answer between looks, end around restart, final event before crash. |
| **15 — projections, one reader per branch** | Move notices, attention announcements, usage wake, dependency-versioned held answers and remaining invalidations separately. Preserve the slice protocol. | Full state/attention/notices/look suites and all `bridge/state` tests. Lost final push, reversed delta, stale completion, failure over retained answer, second window and headless lease holder. Install: judges, reads, git starts, push bytes, CPU and latency with one and two windows. |
| **16 — EVENTHOOK1 external adapter** | Attach eligible canonical events through its unchanged v1 projection; preserve its measurement-first relay work. Retire duplicate legacy producers only after parity proof. | `PluginKitTests`, grammar/privacy census, queue overflow, late plugin, reconnect, invalid required id, dropped final frame and run change. Install: actual outside session wake, fixed output, watermark and explicit loss; no new authority. |

Row 1 is my first choice for expected defect reduction per edit: it repairs three concrete losses at a common boundary and establishes the value passed through later moves. That is an engineering judgement, not a measured optimum. LIMITSWITCH starts immediately after the pure planner makes its disagreement explicit.

### What needs the owner

**Answer 18.** The proposal requests no new product direction, spending default, sign-in, publication, push, history rewrite or deletion of owner data.

The following remain the owner’s decisions when execution reaches them:

- Enabling a plugin that sends observations to outside programs, through its existing person door.
- Appointing a role or applying different actor choices, through their existing person doors.
- A real sign-in.
- Spending beyond development’s authorized runs.
- Publishing, pushing, rewriting history or authorizing loss of retained data.

No foundation row enables these by migration. Carry measurements use the next natural move as SWITCHCTX1 specifies; a separately funded probe would need its own authorization.

### Rejected alternatives and remaining measurements

| Rejected | Reason |
|---|---|
| One universal participant/session table | It would duplicate session state or manufacture sessions for children and holders. Identity, execution and authority have different owners. |
| Making every current kind an appointed role | A helper’s read-only work kind is not an owner-granted management authority. |
| Deleting relationship marks because `Actor` exists | Execution provenance cannot replace `serves`, `piece`, `opinion` or entry relationships. |
| One undifferentiated admission boolean | It hides evidence, expiry, exact forms and executor enforcement. |
| A universal scheduler or execution graph | The Fabric is the substrate; workflow graphs remain optional patterns and retain bound-version semantics. |
| Re-running account selection at every layer | It changes the selected account, double-counts reservations and makes recovery depend on new availability. |
| Extending only the limit box | Curve, hand-over, carry-on and fallback delivery would retain separate recovery rules. |
| Copying transcripts for every move | Cross-agent, own-home, unsupported-version and unverified replay cases keep the deterministic brief. |
| Treating a lost harness acknowledgement as permission to resend | The effect may already have happened. Retain uncertainty and require an explicit new intent. |
| Replacing all IPC, transcript frames and commands with one event bus | Commands require correlated answers; transcripts contain authored material; state needs snapshots. A common fact vocabulary does not erase delivery contracts. |
| Event sourcing the application | Existing stores already own state. A bounded outbox supports delivery without becoming a competing truth. |
| Replacing quests with messages or events | Observation and conversation grant no cross-repository ownership. |
| Renaming the page around Fabric terminology | The code needs these boundaries; a person who configures nothing should still recognize the app. |
| A record-format cutover before reader migration | Older builds ignore new flags. New executable meaning must be isolated and its old reading proven. |

Still unmeasured are the installed cost of additional durable word receipts and move transactions; actual defect reduction; the frequency and duration of uncertain delivery; retained outbox size and useful retention bounds; real ACP lineage completeness under load; production partner confinement; role-holder execution through actors; real carry acceptance, replay agreement, model continuity, compaction behaviour and cache cost; pipe-door recovery capabilities; external relay wake latency; and real provider-outage recovery on each remaining door.

Those measurements gate their respective capabilities. They do not justify delaying the small identity repair, the shared account disposition or the removal of independently owned move delivery.
