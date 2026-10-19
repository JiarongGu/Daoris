# Verification lanes: a step's checks run beside its work and report back (2026-10-19)

**Standing: proposed** (FABRIC-VERIFY1; D180 point 7). The owner, 2026-10-19, after this repository's own development moved
to a local CI (D180): the same shape *"will also help Daoris's own process when large quest sets come and some quests need
multi-repository or multi-agent work together, and it fits the fabric idea: a more flexible and adjustable in-flight
system."* A read-only opinion from Codex was taken on where verification blocks a quest step today, what may move, what a
failure does, and a first slice; this design adopts it. Nothing here is built until D173 carries its note and a slice's row
is taken.

## 1. The rule: accepting work and verifying it are two facts

A step's result can be **accepted** (the next step starts, or it lands under the person's rule) while its **verification is
pending**. Pending is never recorded as passed, unknown is never success, and a verdict always names the exact candidate it
judged. Acceptance and landing never retire a verification: a lane that is owed stays owed until it reports.

## 2. Where verification blocks today

| Point | Where | What it holds back |
|---|---|---|
| `ready` | `QuestExchange` (the bound and taken state, set-up, requirement answers); `QuestStore`'s ownership, seal and source-reopen checks | the submission itself |
| Submission holds | `QuestStore` publishes a successor only from an unheld submission | the chain's next step, the landing, the close |
| The second opinion | `TreeGates` (landing); `OpinionGateLook` (the next step under `steps`) | the landing; the next step |
| Landing | `AutoLander`: workflow, opinion, review, result and tree, judged again under the tree's hold | the branch made or advanced |
| Push | `AutoLander.BeforePushAsync` (CLOSE1h12) | the push (`push-held`) |
| Performed workflow nodes | `WorkPerformer` through the graph's prerequisites | dependent steps |
| Close and go-on | `NodeRuntime`'s fences; `QuestStore.CloseAsync` (binding, claim, submission, holds, seal); the reopen's hold (D173 §E5) | the close; a started successor |

## 3. What stays blocking, and what may become a lane

**Stays blocking, always** (D180's rule: what must never be undone or must never leave the machine): the sensitive scan
before prohibited content enters history or leaves the machine; a repository's own declared blocking set; the ownership,
binding and freshness fences (a claim, a seal, a stale submission); a person's explicit authorization; and an exact-commit
green proof before an install. Pending or unknown never satisfies any of them. The structural checks at `ready` stay
immediate.

**May become a lane, where the person chooses**: a repository's suites and rehearsals, declared evidence checks, and the
second opinion (D155). Moving one deliberately removes its serial dependency, including D157's protected opinion order,
and never fabricates a `reached` receipt to do it.

**The close waits at first.** The successor's work and an authorized landing may go on while a lane runs, but the step's
final close waits until its lanes settle: that keeps E5's reopen open, since a closed successor can make a reopen
impossible today. A provisional close is a later, explicit policy.

## 4. A lane's record

A lane is a durable obligation bound to the repository, the take, the submission's identity, the immutable candidate
commit, the graph and policy versions, and its attempt. It runs in an isolated checkout of that commit and reports
`pending`, `passed`, `failed` or `unknown`. `AutoLander.Owe` is the scheduling precedent, but a lane never depends on a
gate continuing to hold; `OpinionLook`'s retirement on landing changes so that a landing does not retire an owed opinion
that became a lane.

## 5. When a lane fails

- **Hold what has not begun**: an unsent push and any other protected effect not yet started. Completed landings and
  pushes are kept; history is never rewritten.
- **Ask the person to reopen** the named submission (D173 §E5). An automatic reopen would need its own authorization
  design: today a reopen is the person's alone.
- **Route one fix** to the owning repository under the authority it has (a quest where the rule allows, else proposed).
- **Where the work already closed**, append the failure and link a fresh fix quest; never rewrite the completion.
- **An opinion's findings are claims to answer**, not a proven failure.

## 6. Chains and sets across repositories

Independent quests go on. A step known to have consumed the failed result stops further promotion and dependent
dispatch, its session told at a safe turn boundary; E5's go-on then brings its branch up to date and clears its old
result (a source merely unheld does not release it).

Across repositories, the record says **what actually stood on what**: the producer's submission and commit, the
commitment or receipt it was permitted, and the consumer's acceptance or attempt; chain ancestry alone is not enough. It
uses the fabric's commitment boundary (`docs/2026-10-16-fabric-events-design.md` §E6), never a foreign node's wire or file
system. A failure in repository A marks the work in B that stood on it and reaches B through B's own authority; nothing
rolls back across repositories. D178's bring-forward stays a person's fresh take, never an automatic recovery.

## 7. The person's control

Per workspace defaults and per repository overrides say, for each check, whether it blocks or runs as a lane and what a
failure does; a repository's declared blocking set is a floor nothing lowers. Existing workflows stay as they are. A new
*in flight* preset makes suites, rehearsals and enabled opinions lanes, keeps requested human looks, and keeps the
install blocking. The page says *accepted; verification running*, what stands on it, and the next decision. An agent may
propose weaker participation; it never chooses it.

## 8. The first slice

One declared deterministic check, one repository, two chained steps, durable scheduling and reporting, an authorized
landing while the check is pending, and the person's reopen after it fails. Proven with real processes and git: the chain
advances while the check runs; a restart keeps the obligation; the check fails; an unsent push is withheld; the person
reopens; the go-on brings the branch up to date; the step submits anew. Deferred: moving the opinion to a lane, an
automatic reopen, distributed scheduling, graph editing, any rollback.

## 9. The traps, each a test

- **A landing cancels verification**: land before the lane ends; its failure still arrives and holds what had not begun.
- **A stale verdict reaches fresh work**: submit twice at one commit; the first verdict moves neither the second nor its
  consumers.
- **A race before the push or the close**: a failure arriving behind each hold; no newly forbidden effect starts.
- **A doubled recovery**: a restart around each durable write and a replayed completion; one attempt, one consequence.
- **An overbroad invalidation**: A fails after B consumed it; B holds, an independent C goes on.
- **Ownership or unknown read as success**: racing takes, a disconnect and a reconnect; D178's loss and unconfirmed states
  kept.
