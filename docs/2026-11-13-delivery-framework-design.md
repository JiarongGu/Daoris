# Delivery: an ask carried across repositories to production under one plan, built in parallel, verified together and released in dependency order (DELIVERY1)

**Decided 2026-11-13 (D290).** One feature crossed three repositories and four deployables on an installed Daoris, and the person
had to supply its order, its release targets and its coordination by hand: a sequential chain built the parts one after another,
the contract lived in channel posts, the parts were checked together only because a coordinator told each session to compose its
own stack, the release steps guessed their targets and were stopped by hand to keep the order, both asks read Done with nothing
released, and when the app was updated nothing watched the work for nearly three hours. The owner then directed that Daoris derive
such an ask's order itself from code and resource dependencies, show why each step follows the one before, enforce it, and count
the ask complete only once it is released and checked in production. Two independent proposals answered (**Codex**, on gpt-6.1-sol,
and **Fable**, Fable 5.2), and the installed Daoris's own session had drafted a cross-repository design on its quest (CROSSREPO1,
**the draft**). The case, the two proposals and the draft's branch are the steward's working notes; the case is kept with neutral
names, so each point below names where its detail comes from, *(Codex)*, *(Fable)* or *(draft)*, and cites the case's evidence in
place. The steward took the convergence of the two proposals and, where they differ, chose as §11 records. Written by Claude Opus 5.5
on 2026-11-13 from main at `305b90efad`. Read with D284 and `docs/2026-11-12-access-model-design.md` §4, D285 and
`docs/2026-11-12-teamwork-design.md` (§2.2, §6.1, §7, §9), D288, D289 and `docs/2026-11-11-review-for-every-kind-design.md` §16,
`docs/areas/workflows-review-landing.md` (rules 15-17, 22-24, 31, 31b), `docs/areas/co-working-and-messages.md` (rules 4, 12,
35-36) and `docs/areas/driving.md` (rules 43-44). Nothing here is built. Driver paths start at
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, service paths at `src/Daoris.Service/Daoris.Service.Core/`.

## 0. In short

- **No flow engine.** TEAMWORK1's `TeamPlan` becomes the ask's delivery plan across repositories. Each repository keeps its own
  workflow, runs and review; the plan adds deployables, a dependency graph with its evidence, a versioned contract, joint checks, a
  production example and the person's few questions *(both)*.
- **Planned before coded.** The intake is the lead: before any part is published it names the parts, the deployables, the edges
  between them with the evidence for each, D288's targets, the contract, the joint-check recipe and the production example with its
  accounts. The person answers only what evidence cannot settle, in one queue *(both)*.
- **Evidence makes an edge, never a pointer.** The registry's `uses` guides where the lead reads; it never proves a release
  prerequisite. Each edge cites its own evidence, and an order the owner imposes is labelled policy *(Codex)*.
- **Parallel against a contract, landed only together.** Every part starts at once against an acknowledged contract revision; a
  frozen joint receipt over the exact candidates gates every landing *(both)*.
- **Released in order on observed success.** The existing `wait.unit` kind waits on the upstream deployable's release receipt, no
  new kind *(Fable)*; each provider write Daoris makes is judged and journaled before it is sent *(Codex)*.
- **Durable coordination, no chat process owning it.** Plan revisions, receipts, obligations, subscriptions and invocation journals
  persist; one desktop lease reconciles on restart; events wake reads. Ask Daoris is the front desk, the lead plans, the appointed
  supervisor judges, the workflow engine performs *(Codex)*.
- **Done means delivered.** An ask is complete only when every deployable is released and the production example passed *(both)*.

## 1. The case, and what it needs

One ask added candidate versions of a report's configuration. **Repository A** (the schema owner) holds **D1**, the configuration
database's migration, and **D2**, the configuration API; **repository B** (the backend) holds **D3**, which maps that schema as a
mirror without migrating; **repository C** (the frontend) holds **D4**. Seven quests, nineteen sessions and seven workflow runs
later, all code was built, checked together on a local stack and mostly merged in 5 h 17 m; no release step reached a release; about
45 interventions came from the coordinator, the steward and the person. The needs below are the case's own, numbered as it numbers
them.

| Need | The case's evidence | Answered in |
|---|---|---|
| 1 One plan over parts in several repositories | A sequential chain started the frontend 63 minutes after the first part and 29 after the backend; two quests a session published to the other repositories stood outside the ask, with no through words and no place in its state | §2, §4.1 |
| 2 The contract as a versioned artifact | It lived in four channel posts, hand-backs and knowledge files; its list changed from one object to arrays mid-flow; the owner's design document kept the old visibility rule | §2, §4.2 |
| 3 A graph between deployables | The build order came from *owns*, *accepts* and *mirror* declarations; the release order added the migration as its own node, which only a release definition's stage condition carried | §3.2 |
| 4 Dependencies enforced at run time | A quest that builds on another's commit started the same second as it; the frontend's release was held only by stopping its pull-request step | §4.1, §5.2 |
| 5 Joint verification before landing | Sessions composed six joint runs themselves; two parts never ran jointly; nothing recorded which revisions ran together | §4.3 |
| 6 One real example after release | The owner chose one customer's report; a viewer account was recorded nowhere; releasing a version changes what the customer sees | §5.5 |
| 7 Review per part and per ask, bound to what ships | Per-part review was off, yet two readings of the running result at the ask caused about 3 hours of rework; a per-part review listed 44 files against a stale local line where 19 had changed; one Accept waited 1 h 53 m unseen | §4.4 |
| 8 Declared targets, decoys refused | One line fed a disabled legacy definition, a retired one and the real one; another repository's step found no release and failed | §3.3 |
| 9 Release in dependency order on observed success | The order was kept by stopping five steps by hand, partly on a wrong belief that a frontend merge deploys | §5.1, §5.2 |
| 10 What a release ships beyond the ask | The backend's release carried 13 other merges, the frontend's 6 (one needing the backend's), the schema owner's an unrelated configuration change | §5.1 |
| 11 Complete only when released and verified | Both asks read Done hours before anything reached production | §2.3, §5.5 |
| 12 Typed, routed recovery | An agent without Daoris's tools failed twice; a finished run rewound; a `no-release` failure and a pull request's conflict went unreported for hours; restarts ended a session; a step could not be paused before its run existed | §5.4 |
| 13 A durable coordinator | A polling script ran about 40 coordinator turns (83 M cache-read tokens); an update ended it and nothing watched for 2 h 53 m while three items stalled | §6 |
| 14 The person's few decisions, asked once | Four design points, the release go, one version or several, the go per step, the viewer account and the released edit arrived through four routes | §3.4 |
| 15 Mid-flow amendments | A follow-up ask; words typed into one session changed the contract in two other repositories; a forward migration followed a merged one | §4.2, row k |
| 16 Landing identity per part | Branches named after the chain's first quest in other repositories rewound a finished run | §4.1 |
| 17 Shared lines with other asks | A pull request conflicted with another ask's; releases from shared lines carry others' merges | §5.1, row n |
| 18 Fast, visible progression | Driver looks of 3 to 7 minutes made a 20-minute tail an hour | §6, §10 |
| 19 Agent capability parity | A role's agent without Daoris's tools ended as "exited without touching its quest", and its carry-on kept it | §5.4 |
| 20 Cost visible | About 708 k output tokens; the coordinator's share 22%, much of it polling | §10 |

## 2. The objects and their states

### 2.1 The records

Portable records live on the service beside the ask, written through TEAMWORK1's one plan door (`team_plan`), each revision appended
and never edited, carrying repository-relative references, commit ids, digests and provider resource ids, never a machine path. Trees,
ports, accounts, raw logs, subscriptions and invocation journals stay in the desktop's store *(Codex; Fable)*.

| Object | Holds | States |
|---|---|---|
| `TeamPlan` (D285, extended) | Its parts, plus a base per repository, the `deployables` read from each declaration at a blob id, the `edges`, the `contracts`, the joint `checks`, the `example`, step settings set ahead (`stepSettings`) and the `questions` | a revision; the ask's phase is read from it (§2.3) |
| `TeamPart` (D285, extended) | Its repository and lane, the deployables it changes, the contract revisions it reads, its branch and landing named by repository and part | planned → building → submitted → checked → landed; blocked, failed, dropped and superseded explicit |
| `Deployable` | An id unique in its repository, its paths, `provides` and `consumes` (§2.2), its D288 target, bound by deployable id | unresolved → declared → built (an artifact bound) → releasing → released; failed with its kind |
| `Edge` | From, to, phase (`after` and `orders` for building and landing, as D285; `release-after` for releasing), the milestone it waits for, a `why` sentence, its `evidence` (a `file:range` with its blob id, or a provider's stage id) and its `source` (declaration, stage condition, contract, code read, policy, person) | proposed → standing → satisfied; stale when its evidence moved; waived by the person; superseded |
| `ContractRevision` | Its owning part (the keel, D285 §6.1), its document's path in the owner's repository, the revision and digest, each consumer's acknowledgement | draft → published → acknowledged → moved |
| `JointReceipt` | The frozen vector: plan coverage, contract digests, each candidate's commit and prospective result tree, artifact digests, the recipe's digest, the configuration's digest, the toolchain, the assertions and their outcomes *(Codex)* | preparing → running → passed, failed (naming a part), stopped or unknown; void once an input moves |
| `DeploymentAttempt` | The deployable, the exact artifact, the target's ids, the action, its invocation key, the approvals it waits on | ready → awaiting authority → dispatched → observed success, failed or uncertain *(Codex)* |
| `ReleaseReceipt` | The deployable, the build and release ids, the stage, the approvals given and by whom, each success check's reading, `ships` (every change between production's artifact and this one, by ask), its observer *(Fable)* | succeeded, failed with its kind, outside the plan (an act Daoris did not perform, observed) |
| `ExampleReceipt` | The example's subject, the accounts by their record's name, its steps and readings, whether its edit changes what a customer sees and how it was restored | set → run → passed or failed |

Quests record requested work, sessions perform it, runs execute its workflow; a quest's Done never establishes delivery *(Codex)*.

### 2.2 The declarations

Each repository's `daoris.json` gains `deployables` beside `domain`'s `owns` and `accepts` *(Fable)*:

```json
"deployables": [
  { "id": "config-migration", "paths": ["migrations/**"],
    "provides": [{ "resource": "config-schema", "mode": "migrates" }] },
  { "id": "config-api", "paths": ["src/**"],
    "provides": [{ "resource": "config-api", "mode": "owns" }],
    "consumes": [{ "resource": "config-schema", "mode": "reads" }] }
]
```

`provides` modes are `owns` (the shape) and `migrates` (its schema's changes); `consumes` modes are `mirror` (maps a schema without
migrating), `calls` (an endpoint) and `reads` (a table, a cache key, a flag). A deployable is not a repository: repository A holds
two, ordered by a stage condition *(Fable)*. A repository that declares none is asked, as the first piece of its own part, to declare
them; the lead proposes the entries from discovery and never writes them into that repository.

The release target stays where D288 put it, the release step's options over the shared workflow, a repository's values by workflow
and node, now naming the deployable the node releases; a repository with two deployables has two release nodes *(Fable)*. The target
gains `deploysOn`, the earliest act that deploys into it (`push`, `pull-request`, `merge` or `start`), proposed by discovery from the
provider's triggers and declared like the rest; undeclared, it is read as `push`, so every exposing act waits *(draft; Codex)*.

### 2.3 The ask's phase

The ask's phase is one projection of the plan and its receipts, never a stored status *(both)*: Planning, Building, Checked together,
Landed, Releasing, Released, Complete; Blocked names the wait that holds it, and Failed and Partly released are told apart. Driving
rule 43's `Done` becomes one rule: every required node of the standing plan reached its end. An intake always writes a plan (one part
is a valid plan, D285), and a plan with no deployables ends at its landing, so there is one rule, not two *(Fable)*. An ask filed before
the plan door keeps its quest-derived standing until it closes.

## 3. When each thing is decided

### 3.1 The planning step

The lead is TEAMWORK1's `plan` work, started as the intake is (driving rule 44): for an ask the declarations route to more than one
repository the intake is the lead. Before the first quest is published it reads the declarations, the ownership rules, the code, the
migrations, the infrastructure and the provider's definitions, and writes the plan's first revision *(Codex)*:

1. the outcomes and actors, quoting the ask's requirements, with the contract's open points (who sees what, one or several, naming,
   response shapes, compatibility);
2. the parts and owners, from `owns`, `accepts` and the repositories' own rules, a keel part where several parts read one contract;
3. the deployables, from each declaration;
4. the dependency graph (§3.2);
5. the targets (§3.3), with what each release would ship and its configuration prerequisites, read now and read again before each
   release, since a shared line moves *(Fable)*;
6. the contract's first revision;
7. the joint check: its recipe, participants and assertions (§4.3);
8. the production example, its accounts and its restoration (§5.5).

Planning fixes the targets and the order early; the exact builds and the full release contents are filled in later and checked again
before each release *(Codex)*.

### 3.2 The dependency graph

A pure judge in the service, `DeliveryGraph`, beside D285's split judge, proposes edges from fixed rules; each names the evidence and
the order it forces *(Fable's judge, Codex's evidence rule)*:

| Evidence | Edge |
|---|---|
| X `provides` a resource Y `consumes` as `mirror` or `reads` | Y `release-after` X; Y's part `after` X's keel at `submitted` |
| A release definition's stage condition (stage 2 starts on stage 1) | the second stage's deployable `release-after` the first's |
| A `migrates` deployable and a deployable reading its tables, in one repository | the reader `release-after` the migration |
| A part reads a contract revision | the consumer's deployable `release-after` the contract owner's |
| The lead's code read (a caller of a new response shape, a dropped cache key) | a proposed edge citing its `file:range`, its source `code read` |
| The owner's order with no evidence above | an edge whose source is `policy`, said as such |

The registry's `uses` is not a row: it tells the lead which repositories to read, and never by itself proves a prerequisite *(Codex)*.
A model's reading alone makes no standing edge; a code-read edge is enforced with its cited range and says its source *(Fable)*. Each
edge is stale when its evidence's blob moves, and the lead is asked to confirm or drop it. A cycle is a question, settled by a
compatibility change or a staged rollout *(Codex)*. The person may add or waive an edge with a reason; a waived edge passes its wait
as `waived`, never as satisfied.

In the case the judge would have proposed: D2 `release-after` D1 (the stage condition); D3 `release-after` D1 (the backend mirrors the
schema and relies on the second migration's index); D3 `release-after` D2 (a code read, cited: the backend's release drops a cache
key the schema owner keeps; where the read finds no such reason, the owner's order makes this edge `policy`); D4 `release-after` D3
(the frontend's header calls the version list for every user and reads its arrays, the contract). The order D1, D2, D3, D4 falls out
with each edge's reason shown.

### 3.3 The targets

Discovery is a read-only plugin command (`discover-releases`, a `tool` without `writes`) that returns each line's definitions, stages,
triggers, approvals, stage conditions and enabled flags. It proposes; a declared target resolves; none, several, or a declared one
that changed is a question listing the candidates, even when only one was found (D288 point 2) *(Codex; Fable)*. An established
declaration needs no confirmation again. A target question is asked at planning and held at its release node, so the parts build
while it waits and the person sees it at the start, not at the release.

In the case discovery lists the backend line's three definitions, and the declared target names the real one; the disabled legacy
and retired definitions stay candidates nobody chose. The frontend's target declares `deploysOn: start`: a merge creates a release
whose production stage is started by hand, so the merge deploys nothing there (the coordinator's five-hour belief otherwise).

### 3.4 The person's questions

One queue per ask on the existing obligation keys (`NeedsAPerson`, `AttentionStateSlice`), so the item, the bell, *What needs you* and
a session count each once *(both)*. Each item says what it is, what yes does, what it blocks, the default and the evidence. In the
case they would have been: the contract's open points (viewers or admins, one candidate or several), the target questions, the
production example's subject, its accounts and whether its edit is kept, the go for releasing, and a `ships` list that grew. The
person's through words answer what they cover, on parts added later too, within their stated scope; they never pass a `go-ahead`, a
provider's approval, or a check that did not pass *(Codex)*. The lead derives everything else: the person never settles who owns a
migration or reconstructs the provider's topology.

## 4. The parallel build and joint verification

### 4.1 Every part at once, each with its own identity

When the plan's first revision is saved every part is published, under `(ask, part)` (D285); a build edge across repositories binds a
contract revision or a submission's receipt, never a foreign commit as the consumer's base *(draft)*. Independent parts build together.
A quest a session publishes while serving a part becomes a plan revision adding a part through `team_plan`, so its through words,
its order and its state are the ask's: the case's two session-published quests would have been parts, the second `after` the first's
submission rather than starting the same second *(Fable)*. A part's branch and landing are named by repository and part, so no
repository's landing carries another's quest name (case need 16). Step settings set ahead on the plan (`stepSettings {part, node:
pause | options}`) apply when the part's run binds, so a step can be paused before its run exists (case failure 9) *(Fable)*.

### 4.2 The contract

The lead drafts the contract's first revision on the plan; the keel part commits its document in the owning repository and publishes
it as its first hand-back, so it is acknowledged before consumers implement *(Codex)*. Each consumer's pack names the revision and
digest it read; consumers build against the contract's pinned fixtures while the provider builds. A part that needs the contract
changed proposes a revision through `team_plan`; the owning part makes it in its repository, so the owner's design document moves with
the shape (case need 2); each consumer is told at urgent, since the work in flight is now wrong (co-working rule 4); its
acknowledgement goes stale, and every joint receipt naming the old digest is void *(Fable)*.

### 4.3 One frozen combination, checked once

A **recipe** says how the parts' candidates compose into one stack, what runs against it and which assertions it reports. It is owned
by one repository or a plugin and named on the plan by repository, path, commit and digest; ports, accounts and secrets stay on the
desktop *(draft)*. When every participant's submission is ready (TEAMWORK1's observed hand-back), the driver freezes the vector, prepares
the inputs read-only, runs the recipe as one ticket of the existing check broker under the machine's admission, and writes the
`JointReceipt` *(both)*. Worker prose, a matching command and a quest's Done never stand in for it *(Codex)*; images are named by
digest, not tag; skipped, missing and stopped are not passed.

Each repository's delivery run waits on it: a `wait.unit` on the joint receipt of every group covering the repository stands before its
`review` and its `land`, so no part lands until all pass together. A red names the part whose assertions failed and goes to that
part's session with the log; the others hold. A moved candidate, contract, recipe, configuration or toolchain voids the receipt and the
offers drawn from it, and a new vector is frozen *(Codex)*. In the case, the six joint runs the sessions composed by themselves become
one recorded run over the three parts, and the two parts that never ran jointly are inside it.

### 4.4 Review per part and per ask

Review is D289's: each run's Outputs, and the Accept card bound to its run, node, attempt and revision. The joint receipt, the release
plan and each release receipt are outputs (evidence and resource plan). Per-part review follows the repository's workflow, so the
owner's choice to turn it off holds. At the ask, one card shows the integrated behaviour on the joint stack and the receipt before any
landing, and the release plan with its `ships` before the first release; its group press writes one revision-bound word per run and
says which were accepted and which moved, never one atomic Accept over differing revisions *(Codex)*. The case's two readings of the
running result belong at this card, before landing, not after it.

## 5. The ordered release and recovery

### 5.1 What a release deploys

The joint receipt names each candidate's prospective result on its line. Where a target promotes the exact artifact built from that
result, the receipt stands for its release. Where it deploys a build of the line and the line has moved past the checked result
(other asks' merges, a squash's different tree), the same recipe runs once more on the **release vector**, the exact commits whose
builds will ship, before the first release of the ask; `ships` is read from the same comparison and names every other change by its
ask *(Codex's fence, draft §5.1)*. A line that moves again before its release voids the release vector for that deployable alone. In
the case the backend's release would have shown its 13 other merges and the frontend's 6, the one needing the backend's change among
them, before anything started.

### 5.2 The order, as workflow waits

Each repository's delivery run, after its landing, has per deployable a `wait.unit` on the upstream release receipts the plan's edges
name, then the plugin's `release` step on its D288 target *(Fable)*:

`parts → wait.unit (joint) → review → land → push → pull-request → monitor → [go-ahead] → wait.unit (upstream releases) → <plugin>/release → release`

`wait.unit` gains its run-time reader: today `WaitTarget` is parsed, validated and diffed, and nothing reads a commitment at run time
*(draft)*. A typed commitment (`joint`, `release`, `example`) reaches only on its receipt: an upstream release succeeded with its
success checks read, for the exact artifact in the release vector *(Codex)*. A person's skip of it is a waiver the plan records, never a
satisfaction; a press by anyone else is that caller's request to the party (D284 point 3). A wait with no edge for its deployable
reaches at once, said. Where a target's `deploysOn` is earlier than the node that waits (a merge that deploys), the run's bind holds and
asks, through the existing workflow choice door, for a version whose wait stands before that act; it never rewrites a saved graph
*(draft)*. Where `deploysOn` is `start`, the parts land and merge before the ordered promotion. A go for releasing is a `go-ahead` the
workspace's workflow places before the first release, one press for the ordered set or one per release as its options say; Daoris
adds none (D284). Within one release definition, the second stage's node observes the stage the condition starts.

### 5.3 Guarded, journaled writes

Every provider write a Daoris step makes (creating a pull request, setting auto-complete, completing it, creating a release, starting
a stage) is judged immediately before dispatch against the current plan revision, the receipts, the release vector and the step's
attempt, and written to the invocation journal under a durable key (ask, run, node, attempt, sub-act, provider resource) before it is
sent *(Codex; draft §5.2)*. Creating a pull request and setting its auto-complete are separate sub-acts. A lost answer is `uncertain`
and is observed before any retry; nothing is sent twice. Reconciliation and watchers observe and never write, so a watcher waiting for
hours holds no permission *(Codex)*. A plugin without the guarded seam makes no write for a planned ask; its step holds, saying so.
`no-release`, a skipped stage, a partial success and nothing to release never satisfy a required deployment *(Codex)*. The provider's
own approvals belong to its named approvers: an attempt awaiting one shows who may give it, and no Daoris step gives it. A release
whose application checks are declared (migration history, the new index, health and configuration reads) succeeds only when they read
green, not when the provider's stage is green alone *(Codex)*.

An act Daoris does not perform (an agent's own CLI, a plugin tool an agent calls, the person on the provider's page) runs as D284 says
and is observed: Daoris writes its receipt as *outside the plan*, counts it for an edge only where its artifact is the release vector's
and its checks read green, and says so *(Fable)*. The order covers Daoris's own acts; it cannot promise to prevent an outside
deployment *(Codex)*.

### 5.4 Typed failures, routed

Each failure is an event with a kind, its attempt, an owner, its evidence and a recovery; each writes a durable notice in *Done and
failed*, and only one that needs a decision or a remedy enters *What needs you* *(Codex)*. Retrying a deployment repeats no landing and
no Accept of unchanged work.

| Kind | Owner | Recovery | The case |
|---|---|---|---|
| `agent-without-tools` | coordinator | the next staffing choice; a carry-on never keeps the failed agent | failed twice before a hand-over |
| `session-ended` | coordinator | carried on in its own conversation where its harness resumes | a carry-on from a 4.6 k brief of a 244 k context |
| `contract-moved`, `joint-red` | the part's session | woken with the diff or the log | — |
| `pr-conflict`, `line-moved` | the part's session | merge the line; the joint or release check runs again | a conflict undetected for hours |
| `target-unresolved`, `target-changed`, `no-release` | the person | the hold with its candidates | `not-started`, then `no-release` told to no one |
| `approval-waiting`, `accept-waiting`, `go-waiting` | the step's named party | one row; through words pass only an Accept | an Accept 1 h 53 m unseen |
| `uncertain-write` | coordinator | observed before any retry | — |
| `release-failed`, `success-red` | the person | downstream held, the declared rollback shown, the lead's recovery revision | — |
| `outside-act` | coordinator | observed, its receipt written, the evidence it touches voided | — |
| `example-failed` | the lead | a plan revision; the ask is not complete | — |

The coordinator here is the appointed supervisor where one is, else the rules (§6). Partial release is a visible outcome (D1 released,
D3 failed, D4 waiting on D3), never complete and never rolled back by itself; recovery is the lead's linked revision with the deployed
version vector *(draft §6)*. A rollback, where a target declares one, is a deployment attempt like any other.

### 5.5 The production example and completion

The ask's last node is its example, owned by the ask (case need 6): a run keyed by the ask, held by the repository the plan names (in
the case the frontend's), bound to a shipped workflow: `wait.unit` on every release receipt the plan requires → a `human` step on which
the person, or the agent the plan names, performs the steps with the declared accounts, records each reading and, where the edit
changes what a customer sees, its restoration; its Done writes the `ExampleReceipt`. A missing account keeps the ask pending with one
question. The ask is complete when every required deployable has its release receipt and the example passed (§2.3).

## 6. The coordinator and durability

**Who does what** *(Codex)*: Ask Daoris is the conversational front desk: it files the ask, relays the queue and says where the work
stands, and holds no watch and no knowledge of its own (release paths are declarations). The lead plans and re-plans, woken with
collected facts only when judgement is needed. The appointed supervisor, D285's `team` subject of `SupervisorLook`, judges failures and
recovery, rules first, and acts through `team_plan`. The workflow engine evaluates the waits and performs the deterministic
transitions and the guarded writes. No chat process owns coordination state.

**What persists**: plan revisions and receipts on the service; obligations on their existing keys; subscriptions with their event
marks, deadlines, invocation journals and the joint check's resource leases in the desktop's store *(Codex)*.

**Restart**: one desktop lease owns the effects. On start it reconciles each journal entry (an `uncertain` write is observed before
anything else), rebuilds the outstanding waits from the plan and the receipts, resumes each subscription from its mark and re-arms its
deadlines; a joint check's leftover resources are those its attempt named, and nothing else is stopped. In the case the update would
have left no gap: the waits stood in records, not in a chat's polling script.

**Events wake reads**: a submission, a receipt, a pull request's state, a provider's stage, a process's exit, an account's allowance.
A bounded reconciliation by code, at an interval each subscription declares, catches a missed event; no model turn polls *(Codex)*. The
watch's own latency is measured (§10) before more rides on it (D285 §9).

## 7. How the existing pieces fit (D228: one way each)

| Piece | What it becomes | What goes |
|---|---|---|
| `Ask` and driving rule 43 | Holds the plan; `Done` read from the plan's nodes (§2.3) | `Done` from quests, for every ask with a plan |
| The intake | The lead's planning step for an ask across repositories | Its words prescribing a `then` chain for such an ask |
| `Quest.Then`, `Parent` | Sequential, unplanned work only | — |
| A quest a session publishes serving a part | A plan revision adding a part | Quests on an ask's work outside the ask |
| `TeamPlan`, `TeamPart`, `TeamMember`, `PartPack` (D285) | Extended as §2.1 | — |
| The keel (D285 §6.1) | Owns the contract revision | The contract as channel posts; the channel carries words only |
| `wait.unit` | Its run-time reader of typed receipts; a person's skip becomes a recorded waiver | — (no new kind) |
| `request` | Unchanged: it reaches on a quest's Taken or Done, never a delivery's evidence | — |
| The check broker (TEAMWORK1a) | Runs the joint recipe as one ticket | Sessions composing their own stacks as the record of a joint check |
| The integration queue and train (D285 §7.3) | Each repository's candidate on the ask's branch | — |
| D288's release step | Its target named per deployable, with `deploysOn` | Point 3's person gate before each downstream release, never built: the receipt wait replaces it |
| The plugin's watch and reconciliation | Observation only; writes judged and journaled | A reconciliation that reports `not-started` without observing; writes from a watcher |
| The built-in `release` kind | Unchanged: it ends a run's trees | — |
| D289's Outputs and Accept card | The receipts and the release plan as outputs; the ask-scope card and its group press | — |
| Through words | Reach later parts within their scope | — |
| Ask Daoris | The front desk | A watcher script, and release paths in its own memory |
| `SupervisorLook`'s `team` subject (D285 §9) | Routes §5.4's kinds | — |
| `AskStandings` | Reads the delivery projection | Its quest-derived rule, once no ask without a plan remains open |
| The registry's `uses` | A pointer for the lead's reading | `uses` as an edge |
| The draft (CROSSREPO1) | Folded in: candidate vectors, typed waits, guarded writes, receipts, recovery | Its decision; its backlog row, replaced by DELIVERY1's |

There is no second release scheduler, approval store or team status *(Codex)*.

## 8. The first narrow build

**Its scope**: one desktop, one non-shared workspace, one provider plugin, the case's three repositories and four deployables, one
pinned stack recipe and one production example *(Codex)*. It is the path that would have run the case with no steward intervention
for the order or the release targets: plan-first dependency analysis, target declarations, stable part and run identities, frozen
joint verification, typed prerequisite waits, guarded deployment attempts, restart recovery and delivery-derived completion, shown on
the existing Outputs and obligation surfaces.

**The case, run on it.** The person's words reach Ask Daoris, which files the ask. The lead reads the three repositories and the
provider and writes the plan: three parts, D1 to D4 with their targets resolved and the decoys left as candidates, the four edges of
§3.2 with their reasons, the contract's first revision, the recipe, and the example with its author, viewer and unrelated accounts.
The queue asks the contract's open points, the viewer account and the example's edit, once each. The three parts start within one
look. A part that finds a second schema change adds a part through `team_plan`, `after` the first's submission. When the three
submissions are ready, the recipe runs once over the frozen vector; the ask's card shows the running stack before any landing. Each
repository lands, pushes and merges by its own workflow under its own names, since no production target deploys on merge. Before the
first release the release check runs on the line builds and the card shows what each release ships; the owner's go opens the order.
D1's stage starts, its migration history and index read green, its receipt is written; D2's stage, started by the condition, passes
its approval and its health reads; D3 waits on both receipts, then D4 on D3's. A restart in the middle reconciles the journal and
re-sends nothing. The example passes, and the ask is complete. The person is left the contract's intent, the go, the provider's
approvals and the example's accounts.

**TEAMWORK1 rows it needs first**: **a**'s check broker (built; its leftovers are not needed here); **d**, the team records, the `(ask,
part)` publication and the `team_plan` door, after ACCESS1h; and **f**, the ask's delivery run keyed by the ask, the `parts` kind and
the ask's candidate refs. Row f needs b only for replaying verdicts: without the store a batch runs its reached checks instead of
replaying them, so d and f go ahead of b and c. TEAMWORK1 §15's deferral of parts across repositories until its z is amended: they come
with d and f, proved by this design's row j. Not needed first: TEAMWORK1 b, c, e, g, h, i and z.

| Row (lanes; after) | What the person sees working | Proof | What it removes |
|---|---|---|---|
| **a** deployables and targets declared (service, driver, examples; RELEASETARGET1 points 1-2) | Each repository's deployables read from its declaration; each release node names its deployable and `deploysOn`; discovery lists the candidates and chooses none | The case's four deployables as fixtures; three definitions on one line give a question, never a choice; two release nodes in one repository; an undeclared `deploysOn` reads as `push` | The release step's guess |
| **b** the delivery graph at planning (service, driver; TEAMWORK1d) | The ask's plan, before its first quest, with every edge's reason and evidence, and one queue of questions | The case's fixtures give D1→D2, D1→D3, D2→D3 and D3→D4 with their sources; `uses` alone gives no edge; an unevidenced order is `policy`; a cycle is a question; a moved blob makes its edge stale; each question is counted once | The intake's chain for an ask across repositories |
| **c** parts at once, each with its identity (service, driver; b, TEAMWORK1f) | Every part starts when the plan is saved; a session's quest serving a part joins the plan; a step paused ahead applies when its run binds | Three parts started in one look; the second schema part `after` the first's submission; branches and landings named by repository and part; through words reach a part added later | Quests on an ask outside it; pre-stops refused for a run not yet bound |
| **d** typed prerequisite waits (driver; c) | A run's step waits on a receipt and says which | `wait.unit` reaches only on its typed receipt; a skip records a waiver; no edge reaches at once; a target deploying earlier than its wait holds the bind | `wait.unit` read by nothing at run time |
| **e** the contract revision (service, driver; c) | The contract's revision on the plan, acknowledged by each consumer | Each consumer's pack names its digest; a moved digest voids its joint receipts | The contract as channel posts |
| **f** frozen joint verification (driver; d, e, TEAMWORK1a) | One recorded check over the exact candidates before any landing | Three immutable inputs composed read-only, one broker ticket, the receipt; red names its part; skipped is not passed; a moved input voids; a restart cleans only its attempt's resources | Sessions' own stacks as the joint record |
| **g** guarded deployment attempts (driver, examples; d, f) | Releases in order, each with its receipt, its success checks and what it ships | The release check on a moved line; a judged and journaled write; an `uncertain` write observed before a retry; a watcher writes nothing; `no-release` and partial success satisfy nothing; an outside release observed and labelled | The person gate of D288 point 3; writes from reconciliation and watchers |
| **h** restart recovery (driver; g) | Work goes on after an update with nothing re-sent and nothing unwatched | A restart at each attempt state reconciles the journal, rebuilds the waits, resumes the subscriptions from their marks; no model turn polls | Ask Daoris's watcher |
| **i** delivery-derived completion (service, driver, web; g) | The ask reads Complete only when released and its example passed; a partial release shows as such | The ask's phase from the plan; the example's run and receipt; a missing account keeps it pending; the receipts and the release plan on Outputs, in English and Chinese | `Done` from quests for a planned ask |
| **j** the installed rehearsal (the steward; a-i) | A three-repository fixture stack delivered end to end through the installed window, then one real ask | Through a restart, a pull request's conflict and a partial release; the record ids, the input digests, the offer and effect times and the clean-up; dated shots in both languages | The steward's hand-held order and targets |

Each row lands with its tests seen failing first, filtered to its own classes, with `npm run quick`, and rewrites in the same chunk the
area sentences its change makes untrue (D228).

## 9. The rest, in order

| Row (lanes; after) | What it adds | Case need |
|---|---|---|
| **k** contract amendments and nested plans (service, driver; j) | A revision delivered at urgent, the owner's document moved through the owner's part, the affected parts re-derived and superseded work said | 2, 15 |
| **l** typed failures routed (driver, modules; j, TEAMWORK1e) | §5.4's every kind through the supervisor's `team` subject, each a durable notice | 12, 19 |
| **m** the ask-scope card and its group press (web, modules; j) | D289's later part at the ask: the joint stack before landing, the release plan before releasing | 7 |
| **n** shared lines across asks (driver; j) | `ships` by ask, a cross-ask dependency inside a release made an edge, a conflict with another ask routed to its owning part | 10, 17 |
| **o** the example run by an agent (driver; j) | The example performed with evidence, tenant refusal and the gateways it crosses | 6 |
| **p** overlapping check groups and verdict reuse (driver; j, TEAMWORK1b) | Several joint groups over subsets of parts; an exact-input joint verdict replayed from the store | 5 |
| **q** cost and latency per ask (tools, driver; j) | `dev-metrics --ask` with event-to-action latency and tokens by role and the model that ran | 18, 20 |
| **r** a second provider (examples, driver; g) | Another provider plugin through the same guarded seam | 8 |
| **s** several machines (service, driver; k-r) | A plan executed across machines on the every-known-machine pattern of `QuestClosure` | — |

## 10. Measures

Each is taken on the rehearsal (row j) and on the first real ask, against the case; a missing source says *not recorded*, never zero.

| Measure | The case | The first build's target |
|---|---|---|
| Interventions for order and targets | 5 steps stopped ahead by hand, the targets read from the provider by hand, the order given by the owner | none |
| Steps acting on, or stopped before acting on, an undeclared target | 3 pre-stopped, 1 failed unreported | none; every hold asked at planning |
| Spread of the parts' starts | 63 minutes | one look |
| Landings with a joint receipt | none recorded; two parts never jointly checked | every landing |
| Failure to its notice | hours (`no-release`, a conflict) | one event, or one reconciliation interval |
| Unwatched time after a restart | 2 h 53 m | none |
| Asks Done with nothing released | 2 | none |
| Routes the person's decisions came by | 4 | 1 queue, each asked once |
| Coordinator turns woken by its own polling | about 40 | none |
| Event to next action; the look's duration | 3 to 7 minutes a look, before its fix | measured (row q) |
| Tokens and cost by ask, part, role and model | 708 k output, coordinator 22% | reported (row q) |
| Rework after the person's reading of the result | about 3 hours | measured, with the reading before landing |

## 11. Rejected

- **A flow engine above the workflows, or one graph holding every repository's steps** *(both)*: two ways to move work (D228); per-run
  graphs, receipts and the integration queue suffice, and one graph passes the graph's bound.
- **A sequential quest chain for independent parts** *(both)*: it serialises work that can run at once and cannot express a join.
- **The contract and the dependencies as channel posts** *(both)*: no revision, no acknowledgement, no staleness, no enforcement.
- **The registry's `uses` as proof of a release prerequisite** *(Fable's judge had it as a rule)*: it names a neighbour, not an order;
  an edge stands on its own evidence, and an owner's order is labelled policy *(Codex)*.
- **An order derived by a model alone, or declared by the person alone** *(Fable)*: unrepeatable, or blind to the evidence the
  declarations and stage conditions already hold; the person's order stays as a policy edge.
- **A new `awaits` kind** *(Fable)*: `wait.unit` is that wait; it gains a reader, not a sibling.
- **A deployable equal to a repository** *(Fable)*: the case has two in one, ordered by a stage condition.
- **Order kept by holding merges or pull requests** *(Fable)*: merging and releasing are different events; `deploysOn` says which act
  deploys.
- **Choosing a target by discovery, a label, a trigger or the releases made** (D288): they describe the provider's behaviour, not the
  intent.
- **A person's gate before each downstream release** (D288 point 3's first build): the person would answer a question the upstream
  receipt answers; a go, where wanted, is the workflow's `go-ahead`.
- **A joint Accept as release coordination** (D289): consent, not order.
- **A coordinator chat holding the state, polling** *(both)*: it died with the app and spent 22% of the output watching.
- **Watchers holding write permission** *(Codex)*: a write after invalidation; watching after deployment cannot protect its
  prerequisites.
- **Refusing an agent's own release command or a plugin write tool an agent calls** *(Fable; the draft refused them)*: it narrows the
  agent (D284); the act is observed, labelled and reconciled.
- **A separate activation of a dormant plan** *(the draft)*: the planning step's own holds keep a plan whose targets or bindings are
  unresolved from releasing; a second gate is a second way.
- **Atomic landing or rollback across repositories** *(both)*: the providers offer separate effects; partial delivery stays visible and
  is recovered explicitly.
- **A quest's Done as delivery** *(Codex)*: it records requested work; delivery is read from receipts.

## 12. Open

- **The provider's fence.** Whether the case's provider can promote the exact checked artifact, or only deploy the line's build, is
  row a's to read; the release check (§5.1) covers the second.
- **Which repository holds the recipe** for a workspace whose stack no one repository owns: the lead proposes one through a quest to
  it; no default owner is named here.
- **Several machines** wait for row s; until then a plan runs on one desktop and a shared workspace's plan is not executed.
