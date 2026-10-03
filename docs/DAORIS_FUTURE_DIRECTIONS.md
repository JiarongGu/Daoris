# Daoris — Future Directions & Improvement Proposal

> Status: Proposal  
> Scope: Post-current-roadmap evolution  
> Goal: Evolve Daoris from an **AI-native engineering control plane that can drive work** into one that can **prove, learn from, recover, optimize, and safely scale that work**.

---

## 0. Current Position

Daoris already has a surprisingly complete control-plane foundation:

- canonical engineering doctrine and packs
- repository adoption, sync, drift detection, and upstreaming
- workspace-based repository families
- repository ownership/domain declarations
- cross-repository quests
- local and shared knowledge services
- desktop driver and session execution
- Claude Code / Codex / ACP-based harness support
- multiple agent accounts and profiles
- toolchain pinning
- permissions and trust boundaries
- session conversations and structured events
- plugins
- workspace topology / Map
- intake → quest → session → verify/report workflow
- local-first + remote synchronization

That means the next major opportunity is **not simply adding more agent adapters**.

The next phase should strengthen five properties:

```text
Can Daoris drive work?
        ↓ already largely solved

Can Daoris prove that the work is correct?
Can Daoris understand why work succeeded or failed?
Can Daoris recover safely when execution goes wrong?
Can Daoris improve future execution from previous work?
Can Daoris coordinate larger engineering programs without becoming opaque?
```

A useful target identity is:

> **Daoris = Engineering Control Plane + Evidence System + Learning Loop**

---

# 1. Evidence-First Completion

## Problem

An agent saying:

> "Done"

is not the same as the work being complete.

Even if repository gates pass, completion may require evidence that exists outside the repository:

- browser behavior
- screenshots
- API responses
- performance results
- database migrations
- deployment state
- user acceptance criteria
- cross-repository compatibility
- issue/ticket expectations

Daoris already owns the lifecycle of a Quest, so it is the correct layer to own the definition of **what evidence is required before a Quest can become complete**.

## Proposal: Evidence Contracts

Extend a Quest with machine-readable acceptance evidence:

```json
{
  "acceptance": [
    {
      "type": "gate",
      "name": "tests"
    },
    {
      "type": "browser",
      "assert": "login flow reaches dashboard"
    },
    {
      "type": "artifact",
      "path": "docs/api-diff.md"
    },
    {
      "type": "human",
      "prompt": "Confirm the new interaction feels correct"
    }
  ]
}
```

Possible evidence types:

- `gate`
- `test`
- `browser`
- `command`
- `artifact`
- `diff`
- `api-contract`
- `performance`
- `deployment`
- `human`

## Desired lifecycle

```text
Quest
  ↓
Agent work
  ↓
Candidate completion
  ↓
Evidence collection
  ↓
Verification
  ├── pass → Done
  └── fail → Resume / New Quest / Human review
```

## Important rule

**The model should not be the authority that declares its own success.**

The session may propose completion.

Daoris should determine whether the declared evidence exists.

## Priority

**P0 / Very High**

This makes every later feature more trustworthy.

---

# 2. Execution Provenance / Engineering Ledger

## Problem

Daoris already records sessions, accounts, agent versions, quests, and conversations.

The natural next step is to make every engineering change reconstructable.

For any commit or resulting diff, Daoris should be able to answer:

```text
Why did this change exist?
Which Quest requested it?
Which intake created the Quest?
Which repository owned it?
Which agent performed it?
Which account/profile was used?
Which agent/tool version was used?
Which doctrine version governed the session?
Which permissions were granted?
Which gates ran?
What evidence caused it to be accepted?
Which human decisions changed the run?
```

## Proposal: Engineering Ledger

Introduce an immutable logical record:

```text
Intent
  ↓
Intake
  ↓
Quest
  ↓
Session
  ↓
Tool calls / actions
  ↓
Diff
  ↓
Verification evidence
  ↓
Commit / PR / release
```

Example:

```json
{
  "questId": "Q-1042",
  "sessionId": "S-992",
  "repository": "Shenora",
  "agent": "claude-code",
  "agentVersion": "2.x",
  "profile": "work",
  "doctrine": "daoris@0.0.x",
  "baseCommit": "abc123",
  "resultCommit": "def456",
  "gates": [...],
  "evidence": [...],
  "decisions": [...]
}
```

This should be queryable from both directions:

```text
Quest → code

code → why was this changed?
```

## Future integration

Eventually:

```bash
daoris trace <commit>
daoris trace <file>
daoris trace <quest>
```

## Priority

**P0 / Very High**

This could become one of Daoris's strongest differentiators.

---

# 3. Replayable Sessions

## Problem

Agent systems are difficult to debug because their runs are partially nondeterministic.

When a task fails, it is often difficult to distinguish:

- bad prompt/context
- wrong repository routing
- wrong permissions
- tool failure
- environment difference
- changed agent version
- model behavior
- stale code state
- missing knowledge

Daoris already pins many of these variables.

That puts it in an unusually good position to implement **engineering replay**.

## Proposal

A session record should contain enough information to reconstruct its execution envelope:

```text
base commit
repository
quest
agent
agent version
profile identity reference
tool versions
doctrine version
permissions
environment declaration
MCP/plugin declarations
input context references
human interventions
```

Command:

```bash
daoris replay <session>
```

Modes:

```text
--inspect
    reconstruct without running

--fork
    create a new session from the old starting state

--same-tools
    require original tool versions

--latest-tools
    reuse intent but run against current tools
```

## Why this matters

This turns failures into reproducible engineering incidents rather than chat transcripts.

## Priority

**P1 / High**

---

# 4. Session Checkpoints and Recovery

## Problem

Long-running agent work will eventually fail because of:

- process crash
- laptop restart
- rate limits
- corrupted worktree
- network failure
- tool crashes
- permissions requiring human intervention
- agent context exhaustion

Restarting from zero wastes both tokens and reasoning.

## Proposal: Execution Checkpoints

Daoris should define checkpoints at meaningful state transitions:

```text
Session started
↓
Repository inspected
↓
Plan accepted
↓
First change landed
↓
Tests passing
↓
Verification started
↓
Candidate complete
```

Checkpoint data should not attempt to serialize hidden model state.

Instead preserve **engineering state**:

```text
git tree
session events
current quest state
known findings
remaining acceptance criteria
tool state references
pending questions
```

Recovery:

```bash
daoris session resume <id>
```

or:

```text
Original session unavailable
       ↓
New agent session
       ↓
Receives Recovery Brief
       ↓
Continues from checkpoint
```

## Recovery Brief

Generated deterministically from recorded state:

```markdown
# Recovery Brief

Quest:
...

Completed:
- ...

Evidence:
- ...

Remaining:
- ...

Current tree:
...

Known constraints:
...

Do not repeat:
- ...
```

## Priority

**P1 / High**

---

# 5. Agent Outcome Evaluation

## Problem

Usage measurement tells Daoris:

```text
how much an agent consumed
```

but not:

```text
whether it was effective
```

Daoris should eventually know:

- completion rate
- retry rate
- gate failure rate
- human intervention rate
- time/token cost
- regression frequency
- average quests reopened
- repository-specific success
- task-type-specific success

## Proposal: Outcome Metrics

Per completed Quest:

```text
agent
agent version
profile
repository
task category
duration
usage
number of retries
human interventions
verification failures
final acceptance
reopen within N days
```

Then calculate:

```text
Success / Cost
Success / Token
Success / Session
First-pass verification rate
Mean retries
Human intervention rate
```

## Important boundary

Do **not** reduce this to a universal "agent score".

An agent may perform differently by domain:

```text
Claude
  architecture      strong
  browser workflow  medium
  repetitive edits  expensive

Codex
  repetitive edits  strong
  repo discovery    different profile
```

Selection should be contextual.

## Priority

**P1 / High**

---

# 6. Evidence-Based Agent Routing

Once outcome measurements exist, Daoris can improve routing.

Today:

```text
Quest
  ↓
configured agent
```

Future:

```text
Quest
  ↓
Task characteristics
  ↓
Historical outcomes
  ↓
Available accounts
  ↓
Cost / usage constraints
  ↓
Agent selection
```

Example routing facts:

```json
{
  "repository": "Daoris.Service",
  "taskType": "dotnet-refactor",
  "preferredAgent": "codex",
  "reason": {
    "sampleSize": 23,
    "firstPassRate": 0.87
  }
}
```

## Critical design principle

Daoris should select from **measured engineering outcomes**, not from model-brand assumptions.

It should also remain explainable:

> Selected Codex because the workspace policy allows it and its last 20 similar quests had fewer verification retries.

## Priority

**P2 / Medium-High**

Depends on outcome measurement first.

---

# 7. Repository Intelligence 2.0

Repository intelligence is already on the roadmap.

This area can become much more important than a visual graph.

The goal should not be:

> draw the repository.

The goal should be:

> make structural facts available to routing, impact analysis, verification, and agents.

## Suggested layers

### 7.1 Symbol Graph

```text
class
function
interface
module
project
package
```

Relations:

```text
calls
implements
inherits
imports
publishes
consumes
```

### 7.2 Contract Graph

Extract externally meaningful contracts:

```text
HTTP routes
events
messages
database schemas
public interfaces
CLI commands
configuration schemas
```

### 7.3 Change Impact Graph

Given:

```text
change X
```

answer:

```text
What may break?
Which repositories depend on this?
Which tests are relevant?
Which quests may need to be generated?
```

### 7.4 Ownership Graph

Combine code structure with repository declarations:

```text
symbol → module → repo → domain owner
```

### 7.5 Semantic Architecture Layer

Allow repositories to annotate structural nodes:

```text
"This service owns recipe aggregation."
"This endpoint is legacy."
"This module may not depend on UI."
```

## Priority

**P1 → P2**

Repository intelligence should be treated as infrastructure, not UI decoration.

---

# 8. Cross-Repository Change Plans

## Problem

Quest is a good ownership boundary.

But some features intentionally require several repositories.

Example:

```text
Backend API change
↓
SDK update
↓
Frontend update
↓
Desktop integration
```

Independent quests are not enough; the overall change has ordering and compatibility constraints.

## Proposal: Change Plan / Mission

Introduce an orchestration layer above Quest.

Possible terminology:

```text
Goal
Mission
ChangeSet
Program
```

Suggested model:

```text
Mission
  ├── Quest A: API
  ├── Quest B: SDK
  │      dependsOn A
  ├── Quest C: Web
  │      dependsOn B
  └── Quest D: Integration verification
         dependsOn A,B,C
```

Example:

```yaml
mission: Add live inspection
quests:
  - id: api
    repo: backend

  - id: client
    repo: sdk
    after: api

  - id: ui
    repo: web
    after: client

  - id: verify
    type: verification
    after: [api, client, ui]
```

## Important boundary

A Mission coordinates ownership.

It must **not** erase repository ownership.

Every modification should still happen inside the owning repository's Quest/session.

## Priority

**P1 / High**

---

# 9. Contract-Driven Cross-Repo Changes

For multi-repository work, use explicit interface contracts.

Example:

```text
Producer repository
     ↓
Contract change
     ↓
Consumers
```

Daoris can detect:

```text
OpenAPI diff
protobuf diff
event schema diff
NuGet/npm package API diff
public .NET API diff
database migration contract
```

Then:

```text
Breaking contract detected
        ↓
Find dependent repositories
        ↓
Create / propose dependent Quests
```

Example:

```text
Backend removes field X
        ↓
Daoris knows:
  web consumes X
  desktop consumes X
        ↓
2 follow-up quests proposed
```

## Priority

**P2 / Medium-High**

High strategic value after repository intelligence exists.

---

# 10. Workspace-Level Invariants

Today repository gates protect repositories.

Some rules, however, belong to the entire workspace.

Examples:

```text
all public APIs must remain backward compatible

frontend cannot directly call internal-service-x

all repositories must use the same protocol schema version

a release cannot contain mixed package versions

service A and service B must pass compatibility tests together
```

## Proposal

```text
workspace gates
```

Example:

```json
{
  "workspaceGates": [
    {
      "name": "api-compatibility",
      "runner": "..."
    }
  ]
}
```

Run at Mission verification / integration stage.

## Priority

**P2**

---

# 11. Human Decision Objects

Humans currently appear mainly as interruptions or approvals.

Daoris should make important human decisions explicit artifacts.

Examples:

```text
"Use SQLite, not Postgres."
"Do not modify the public API."
"Ship without migration support."
"Accept this known limitation."
```

## Proposal

A decision object:

```json
{
  "decisionId": "D...",
  "scope": "quest|repository|workspace",
  "statement": "...",
  "reason": "...",
  "madeBy": "human",
  "expires": null
}
```

Agent sessions receive relevant active decisions automatically.

This prevents the classic failure:

```text
Person tells Agent A something
Agent A finishes
Agent B starts tomorrow
Decision is gone
```

## Useful extension: expiry

Some decisions are temporary:

```text
"Do not update React until release X."
```

Allow:

```text
expiresAt
supersededBy
```

## Priority

**P1**

---

# 12. Knowledge Provenance and Confidence

Daoris already indexes repository knowledge and detects convergence.

The next step is to distinguish different kinds of "knowledge".

Not all knowledge is equally authoritative.

Example:

```text
Architecture decision
Production incident finding
Agent observation
README statement
Old task note
Human instruction
Generated hypothesis
```

## Proposal

Every knowledge item should carry provenance:

```json
{
  "sourceType": "decision",
  "source": "docs/decisions/D73.md",
  "commit": "...",
  "observedAt": "...",
  "authority": "repository",
  "status": "active"
}
```

Possible states:

```text
observed
proposed
verified
accepted
deprecated
superseded
contradicted
```

## Why this matters

Without provenance, a knowledge system eventually becomes a confident pile of stale statements.

## Priority

**P1 / High**

---

# 13. Knowledge Contradiction Detection

Convergence answers:

> Two repositories discovered the same thing.

The opposite problem is equally valuable:

> Two authoritative sources disagree.

Examples:

```text
Repo A:
"All IDs are UUID."

Repo B:
"Customer IDs are numeric."
```

or:

```text
Decision D14:
"Use X."

New architecture document:
"Use Y."
```

Daoris should detect potential contradictions and present them for review.

```text
Knowledge
  ├── convergence
  ├── duplication
  └── contradiction
```

Do not auto-resolve.

Human disposition:

```text
different scope
one stale
both valid
supersede
needs investigation
```

## Priority

**P2**

---

# 14. Knowledge Decay / Freshness

Knowledge should not live forever without challenge.

Add a freshness system based on evidence, not arbitrary TTL.

Potential decay signals:

```text
source file removed
owning code changed heavily
API contract changed
decision superseded
repository no longer exists
knowledge has not been observed since major refactor
verification repeatedly contradicts it
```

Daoris may mark:

```text
Fresh
Possibly stale
Stale
Superseded
```

This should remain advisory unless tied to a deterministic invariant.

## Priority

**P2**

---

# 15. Context Compiler

## Problem

As Daoris accumulates:

- doctrine
- repository knowledge
- workspace knowledge
- Quest details
- decisions
- code maps
- previous sessions

blindly injecting everything will destroy context quality.

## Proposal

Treat context preparation as a first-class compiler.

```text
Available knowledge
       ↓
Relevance selection
       ↓
Authority filtering
       ↓
Deduplication
       ↓
Budgeting
       ↓
Structured context package
```

Output:

```text
Session Context Package

1. Quest
2. Active repository rules
3. Relevant human decisions
4. Relevant architecture knowledge
5. Relevant prior incidents
6. Relevant code map
7. Explicit exclusions
```

## Critical principle

The context compiler should produce an inspectable artifact.

```bash
daoris context explain <session>
```

should answer:

```text
Why was document X included?
Why was Y excluded?
How many tokens/bytes did each category consume?
```

## Priority

**P1 / Very High**

This is likely more valuable than adding increasingly large memories.

---

# 16. Context Experiments

Once context packages are explicit, Daoris can test them.

Example:

```text
Context A:
full architecture docs

Context B:
targeted symbol + decisions only
```

Compare:

```text
completion rate
usage
verification failures
session duration
```

This enables evidence-driven doctrine/context optimization.

Eventually Daoris can answer:

> This rule is always loaded but has not influenced any measured successful task.

or:

> Sessions that receive document X make fewer architecture mistakes.

This is potentially a unique Daoris capability.

## Priority

**P3 / Experimental**

---

# 17. Doctrine Effectiveness Measurement

Daoris currently solves doctrine propagation.

A future question is:

> Does this doctrine actually help?

Possible measurement:

```text
Rule introduced
↓
Affected sessions
↓
Compare:
- recurrence of targeted failure
- gate failures
- human corrections
- relevant incident rate
```

Example:

```text
Rule:
"Never modify generated migrations manually"

Before:
8 corrections / 50 sessions

After:
1 correction / 60 sessions
```

No automatic deletion should happen from this metric.

But it gives humans real feedback about whether doctrine is useful.

## Priority

**P3**

---

# 18. Failure Taxonomy

A system cannot learn from failure if every failure is just:

```text
failed
```

Create a structured taxonomy.

Example:

```text
routing_failure
context_failure
permission_failure
tool_failure
environment_failure
implementation_failure
verification_failure
contract_failure
agent_hallucination
human_requirement_change
external_dependency
rate_limit
```

A failed/retried Quest should record one or more causes.

Initially human/agent suggested, human-confirmed where important.

Later this drives:

```text
analytics
agent routing
context improvements
doctrine improvements
tool reliability work
```

## Priority

**P1**

---

# 19. Incident → Doctrine Loop

This fits Daoris extremely well.

When repeated failures occur:

```text
Failure
↓
Incident pattern
↓
Candidate lesson
↓
Doctrine / knowledge proposal
↓
Human review
↓
Canon
↓
Future repositories
```

Daoris already has upstreaming.

Extend it beyond manually edited doctrine.

Example:

```text
5 sessions hit the same EF Core migration issue
            ↓
Daoris detects repeated failure cluster
            ↓
"Candidate lesson"
            ↓
Human turns it into knowledge / rule / skill
            ↓
upstream
```

This makes "cultivation" concrete.

## Priority

**P2 / Strategically Very High**

---

# 20. Safe Parallelism

A single session per repository is a clean initial ownership model.

Eventually larger tasks will want parallel work.

Do not start with arbitrary multi-agent swarms.

Use git/tree isolation.

Possible model:

```text
Repository
  ├── worktree/session-A
  ├── worktree/session-B
  └── worktree/session-C
```

Daoris knows:

```text
changed file sets
symbol ownership
dependencies
merge order
```

Before starting parallel sessions:

```text
Conflict estimator
```

Possible result:

```text
A and B: likely independent
A and C: both touch module X → serialize
```

## Priority

**P3**

Only after replay, evidence, and provenance are strong.

---

# 21. Speculative Execution

Much later, Daoris could run two implementations when the cost is justified.

Example:

```text
Quest
 ├── Agent A implementation
 └── Agent B implementation
          ↓
same acceptance contract
          ↓
evidence comparison
          ↓
human selection
```

Useful for:

```text
high-risk refactors
architecture choices
performance work
difficult bug fixes
```

This should be explicit and rare, not the default.

## Priority

**P4 / Experimental**

---

# 22. Budget and Cost Policies

Account rotation should eventually be constrained by engineering budgets.

Possible scopes:

```text
session
quest
mission
workspace
month
```

Example:

```json
{
  "budget": {
    "maxUsd": 10,
    "maxSessions": 4,
    "maxRetries": 2
  }
}
```

Behavior:

```text
budget healthy → proceed

budget warning → cheaper eligible strategy

budget exceeded → hold for human
```

Important:

Do not optimize purely for token cost.

Optimize against outcome metrics:

```text
cost per accepted Quest
```

## Priority

**P2**

---

# 23. Environment Contracts

Tool versions alone do not fully define reproducibility.

Sessions may depend on:

```text
.NET SDK
Node
Java
OS
browser
database
environment variables
services
containers
```

Define a lightweight environment contract:

```json
{
  "requires": {
    "dotnet": ">=10",
    "node": "24",
    "os": "windows"
  }
}
```

Daoris can check before spawning.

This should not become a replacement for Nix/Docker/devcontainers.

Its responsibility:

```text
detect
explain
select compatible runner
```

not:

```text
rebuild the whole environment ecosystem
```

## Priority

**P2**

---

# 24. Remote Workers

Desktop currently drives the local machine.

Eventually the driver abstraction can support remote workers:

```text
Daoris Control Plane
     ↓
Worker Registry
 ├── desktop-windows
 ├── linux-build-box
 ├── gpu-machine
 └── ephemeral-cloud-worker
```

Worker declares capabilities:

```text
OS
tools
repositories
trust
resource limits
browser availability
GPU
network access
```

Quest execution chooses an eligible worker.

## Important

This should reuse the existing driver/session semantics.

Do **not** create a second execution model.

## Priority

**P3**

---

# 25. Secrets as Capabilities

Daoris correctly avoids owning agent login secrets where possible.

For broader execution, sessions will need access to sensitive capabilities:

```text
deploy staging
read Azure logs
query database
publish package
access GitHub
```

Do not hand raw secrets to the model unless unavoidable.

Represent access as capabilities:

```text
azure.logs.read
github.pr.create
npm.publish:package-x
database.staging.read
```

A tool/plugin resolves the capability to credentials outside the model-visible context.

```text
Agent
 ↓ requests capability
Daoris policy
 ↓
Tool/Plugin
 ↓ uses secret
External system
```

## Priority

**P1/P2 depending on deployment ambitions**

---

# 26. Policy Simulation

Permission changes are dangerous.

Before applying a workspace/repository rule:

```bash
daoris policy simulate
```

show:

```text
Which current sessions would be blocked?
Which tools become available?
Which repositories gain write access?
Which plugins receive additional capabilities?
```

This is infrastructure-as-code style planning for agent permissions.

## Priority

**P2**

---

# 27. Agent Supply-Chain Security

Once Daoris manages:

```text
agent binaries
plugins
tools
MCP servers
browser extensions
```

it becomes part of the software supply chain.

Recommended future features:

```text
checksums
publisher identity
signed manifests
SBOM where available
version provenance
plugin permission declaration
tool update diff
quarantine
```

Example:

```text
Plugin requests:
- filesystem.read
- browser.control
- network: github.com

New version additionally requests:
- process.execute

→ require explicit approval
```

## Priority

**P1 / High before ecosystem expansion**

---

# 28. Plugin Capability Model

Current fail-closed plugin boundaries are a strong base.

Next step:

Every plugin should declare capabilities.

Example:

```json
{
  "capabilities": [
    "browser.control",
    "filesystem.read:workspace",
    "network:github.com"
  ]
}
```

Daoris should distinguish:

```text
declared
approved
active
used
```

The UI can show:

```text
Plugin X has 4 capabilities.
This session used 2.
```

## Priority

**P1**

---

# 29. Plugin Protocol Versioning

As plugins grow, compatibility will become important.

Introduce:

```text
apiVersion
minimumDaorisVersion
capabilityVersion
```

and explicit compatibility errors.

Avoid silently attempting compatibility.

Potential future command:

```bash
daoris plugin doctor
```

## Priority

**P1**

---

# 30. Quest Templates

Many workflows repeat:

```text
bug fix
dependency upgrade
API change
security patch
UI feature
production incident
release
```

Add templates that define:

```text
recommended acceptance evidence
default gates
expected phases
risk level
```

Example:

```yaml
kind: dependency-upgrade

acceptance:
  - tests
  - api-compatibility
  - dependency-audit
```

Templates should guide, not become heavyweight workflow DSL.

## Priority

**P2**

---

# 31. Risk Classification

Not every Quest deserves the same autonomy.

Introduce deterministic / declared risk factors:

```text
production deployment
database migration
authentication
billing
security
public API
large deletion
infrastructure
secrets
```

Then policies:

```text
Low
  autonomous

Medium
  autonomous + evidence

High
  human approval before write/deploy

Critical
  planning only unless explicitly unlocked
```

Avoid asking the LLM alone to determine risk.

Use repository declarations + changed surface + explicit task metadata.

## Priority

**P1 / High**

---

# 32. Change Blast Radius

Combine repository intelligence + risk.

Before work begins:

```text
Quest
  ↓
Expected change surface
  ↓
Dependency graph
  ↓
Risk estimate
```

Example:

```text
Likely touches:
- public API
- 3 dependent repos
- authentication code

Recommended execution:
- plan review
- contract diff
- integration Mission
```

This can improve both routing and human attention.

## Priority

**P2**

---

# 33. Review Agent as a Separate Role

Do not let the same session be the only reviewer of its own work.

A verification session can receive:

```text
Quest
acceptance contract
diff
relevant rules
```

but not necessarily the implementation conversation.

```text
Implementer
    ↓
Diff
    ↓
Independent verifier
```

Possible modes:

```text
same agent / fresh context
different agent
human only
hybrid
```

Daoris should record which verification topology was used.

## Priority

**P1**

---

# 34. Adversarial Review Mode

For high-risk changes:

```text
review objective:
find reasons this change should NOT be accepted
```

Useful for:

```text
security
migration
public API
data loss risk
concurrency
```

Again, this should produce evidence/findings, not an opaque score.

## Priority

**P2**

---

# 35. Repository Health Surface

Daoris can become the place where a developer sees:

```text
Repository
 ├── doctrine drift
 ├── stale knowledge
 ├── open quests
 ├── failed sessions
 ├── dependency risks
 ├── toolchain health
 ├── gate reliability
 ├── unresolved contradictions
 └── recent incidents
```

The key is not to invent another generic dashboard.

Every health item should be actionable:

```text
Why?
Evidence?
Owner?
Action?
```

## Priority

**P2**

---

# 36. Workspace Engineering Timeline

Create one timeline that merges:

```text
human decisions
quests
sessions
commits
releases
incidents
doctrine changes
knowledge changes
```

Example:

```text
09:10 Ticket imported
09:12 Intake created 3 quests
09:14 Backend session started
09:31 Contract changed
09:32 Web follow-up quest proposed
09:48 Backend verified
10:03 Web completed
10:10 Mission accepted
10:12 New knowledge promoted
```

This would make Daoris feel like a true engineering operating surface rather than a collection of screens.

## Priority

**P2**

---

# 37. "Why?" Queries

Daoris should become extremely good at answering engineering questions.

Examples:

```text
Why is this repository using SQLite?

Why can't this agent write to that folder?

Why did Daoris choose this repository?

Why did this Quest reopen?

Why is this document in the session context?

Why did this plugin receive browser access?

Why did this change create another Quest?

Why is this agent profile currently unavailable?
```

Most control-plane systems expose state.

Great control-plane systems expose **causality**.

Every automated decision should preserve its explanation.

## Priority

**P0/P1 as a design principle**

---

# 38. Event-Sourced Control Plane

Daoris already has structured session events.

Long-term, consider using events as the conceptual source for workflow state:

```text
QuestCreated
QuestClaimed
SessionStarted
PermissionGranted
ChangeProposed
GateCompleted
EvidenceAdded
VerificationFailed
QuestCompleted
DecisionRecorded
```

Benefits:

```text
auditability
replay
debugging
timeline
state reconstruction
sync
analytics
```

This does not necessarily require adopting a heavyweight event-sourcing framework.

SQLite tables + append-only event semantics may be enough.

## Priority

**P2 architecture direction**

---

# 39. Offline-First Conflict Semantics

Remote sync is already modeled similarly to Git.

As teams grow, Daoris needs explicit rules for simultaneous control-plane changes:

```text
Quest edited on machine A
Quest completed on machine B
Decision changed on machine C
```

Define merge semantics per entity.

Good candidates:

```text
append-only events → merge naturally

mutable configuration → explicit conflict

completion state → monotonic where possible

human decisions → versioned/superseded, not overwritten
```

## Priority

**P1 before serious multi-user adoption**

---

# 40. Multi-User Roles Without Enterprise Bloat

Team use will eventually need identity and authority.

Keep it minimal:

```text
Viewer
Developer
Maintainer
Workspace Owner
```

Permissions can apply to actions:

```text
create quest
approve high-risk execution
change workspace policy
manage agent profiles
publish doctrine
register plugin
manage remote
```

Avoid building a large corporate IAM system.

Integrate external identity when needed.

## Priority

**P3 unless team adoption arrives earlier**

---

# 41. GitHub / Issue Tracker as Intake Adapters

The current intake model already expects tickets/links.

Make external systems adapters rather than core concepts.

Example:

```text
GitHub Issue
Linear
Jira
Azure DevOps
Plain URL
Local markdown
```

normalize to:

```text
IntakeSource
```

Daoris then owns everything after ingestion.

## Priority

**P2**

---

# 42. Pull Request / Commit Feedback Loop

When a human reviews a PR produced by an agent:

```text
review comments
requested changes
rejected design choices
```

are extremely valuable learning signals.

Daoris can convert them into:

```text
session feedback
failure classification
candidate knowledge
candidate doctrine
```

Example:

```text
Reviewer repeatedly says:
"Do not introduce service abstractions for simple handlers."

After repeated cases:
→ candidate local engineering principle
```

## Priority

**P2**

---

# 43. Release Missions

A release naturally crosses repositories and gates.

Represent release as a special Mission:

```text
Version readiness
↓
Repository gates
↓
Dependency compatibility
↓
Build artifacts
↓
Deployment
↓
Smoke verification
↓
Release record
```

This is likely a better fit for Daoris than building a generic CI/CD replacement.

Daoris coordinates existing tools.

It should not try to replace:

```text
GitHub Actions
Azure DevOps
Argo
Terraform
```

## Priority

**P3**

---

# 44. Production Feedback Ingestion

Eventually engineering knowledge should include runtime reality.

Adapters may ingest:

```text
Application Insights
Sentry
Datadog
OpenTelemetry
GitHub incidents
Azure alerts
```

Flow:

```text
runtime incident
↓
Daoris intake
↓
repository routing
↓
Quest
↓
fix
↓
verification
↓
incident lesson
↓
knowledge/doctrine candidate
```

This creates:

> Production → Engineering → Knowledge → Future Engineering

## Priority

**P3 / Strategically strong**

---

# 45. Architecture Fitness Functions

Beyond tests, repositories can define architectural invariants:

```text
Domain must not reference Infrastructure

UI cannot reference database project

Public API compatibility must not regress

module A may only depend on B and C
```

Repository intelligence can evaluate them.

This moves architecture from documentation into verifiable rules.

## Priority

**P2**

---

# 46. Architecture Drift Detection

Combine:

```text
declared architecture
repository graph
commit history
```

Detect:

```text
unexpected dependency
new public surface
new cross-domain coupling
layer violation
ownership ambiguity
```

Present as:

```text
Architecture drift candidate
```

not automatically a failure unless explicitly configured.

## Priority

**P2/P3**

---

# 47. Auto-Generated Repository Brief

Before a repository joins Daoris, generate an inspectable brief:

```markdown
# Repository Brief

Purpose:
...

Likely owns:
...

Likely accepts:
...

Stack:
...

Important entry points:
...

External contracts:
...

Dependencies:
...

Existing agent instructions:
...

Potential risks:
...
```

A human approves/corrects it.

Then use it to bootstrap:

```text
domain declaration
knowledge index
code map
initial gates
```

## Priority

**P1**

This would dramatically improve onboarding.

---

# 48. Workspace Onboarding Assistant

For a folder containing many repositories:

```bash
daoris workspace adopt ./src
```

Possible flow:

```text
discover repositories
↓
generate briefs
↓
detect dependency relationships
↓
suggest workspace topology
↓
suggest domain declarations
↓
show collisions
↓
human approves
↓
connect/adopt
```

Daoris should provide facts and suggestions; humans decide ownership.

## Priority

**P2**

---

# 49. Test Selection

Repository intelligence can help determine:

```text
Which tests need to run for this change?
```

Start advisory:

```text
Changed:
A.cs
B.cs

Likely affected:
Project X
Project Y

Recommended gates:
unit-X
integration-Y
```

Later allow deterministic selectors where the dependency graph is reliable.

## Priority

**P3**

---

# 50. Cached Verification

Agent workflows repeatedly execute expensive gates.

Daoris can cache evidence keyed by:

```text
commit/tree hash
environment
gate version
dependencies
```

If nothing relevant changed:

```text
reuse verified evidence
```

Careful:

```text
unit tests → often cacheable
browser state → maybe not
external API check → usually time-sensitive
deployment check → not reusable
```

## Priority

**P3**

---

# 51. Semantic Diff Summaries

A raw git diff is not enough for higher-level workflow.

Generate structured facts:

```text
Added endpoint
Changed public type
Modified database schema
Changed permission declaration
Added dependency
Changed config contract
```

These facts can power:

```text
risk
review
routing
cross-repo impact
release notes
```

Implementation should use AST/contract tooling where possible rather than asking an LLM to infer everything.

## Priority

**P2**

---

# 52. Acceptance Criteria Generator

When an Intake becomes Quests, Daoris can ask the intake agent to propose acceptance criteria.

But generated criteria should be inspectable and editable before execution.

Example:

```text
Request:
"Add dark mode."

Proposed acceptance:
- preference persists
- startup uses stored preference
- system theme works
- no flash of incorrect theme
- existing settings tests pass
```

Then those criteria become part of the evidence contract.

## Priority

**P1**

---

# 53. Requirement Ambiguity Detection

Before spending a full agent session, intake should identify unresolved questions.

Classify:

```text
blocking ambiguity
safe assumption
implementation detail
```

Example:

```text
"Delete project"

Unknown:
Does delete mean soft delete or permanent?

→ blocking
```

This can reduce expensive false starts.

## Priority

**P1**

---

# 54. Assumption Ledger

Agents inevitably make assumptions.

Require important assumptions to be surfaced:

```json
{
  "assumption": "existing API must remain backward compatible",
  "source": "inferred",
  "impact": "high"
}
```

High-impact inferred assumptions can pause for human review.

Accepted assumptions become part of session provenance.

## Priority

**P1**

---

# 55. Work Decomposition Quality

Intake currently routes work.

Future evaluation can track whether decomposition itself was good.

Signals:

```text
Quest frequently spawns surprise cross-repo work

Quest repeatedly blocked by another Quest

multiple Quests edit same ownership surface

Quest is repeatedly reopened because acceptance was split badly
```

Daoris can then improve decomposition strategies.

## Priority

**P3**

---

# 56. "Do Not Automate" Registry

A mature automation platform should explicitly know where autonomy is unwanted.

Examples:

```text
production database destructive operations

legal text

billing changes

certificate rotation

customer data exports
```

Workspace config:

```json
{
  "manualOnly": [
    "production.database.destructive",
    "billing.publish"
  ]
}
```

Agents can prepare a plan but not execute.

## Priority

**P1**

---

# 57. Time-Scoped Permissions

Instead of:

```text
Agent may deploy staging
```

support:

```text
Agent may deploy staging
for this Quest
until this session ends
```

Capability lease:

```text
scope
quest/session
expiry
reason
approvedBy
```

## Priority

**P2**

---

# 58. Network Policy

For high-trust execution, Daoris may declare network boundaries:

```text
none
workspace services only
allowlisted domains
full network
```

Plugins/tools enforce where technically possible.

Session provenance records effective policy.

## Priority

**P3**

---

# 59. Redaction Boundary

Conversation/session data may contain:

```text
tokens
secrets
customer data
internal URLs
PII
```

Before remote synchronization:

```text
local event stream
↓
redaction policy
↓
shared event stream
```

Repositories/workspaces should be able to mark fields as:

```text
local-only
share-metadata
share-full
```

## Priority

**P1 before broader remote/team usage**

---

# 60. Knowledge Export / Portability

Daoris should avoid becoming the only place engineering knowledge can be understood.

Provide exports:

```text
Markdown
JSON
graph format
static bundle
```

Example:

```bash
daoris export knowledge
daoris export workspace-map
daoris export ledger
```

This protects the project's local-first philosophy.

## Priority

**P2**

---

# 61. Headless Driver

Desktop is an excellent user-facing driver.

A future server/headless mode would allow:

```text
CI worker
home server
build machine
remote VM
```

to run the same driver semantics.

Important:

```text
same quests
same permissions
same evidence
same event model
same plugins
```

Only the shell changes.

## Priority

**P2/P3**

---

# 62. Daoris Protocol Surface

As Daoris becomes infrastructure, its most valuable product may eventually be its contracts.

Potential stable protocols:

```text
Quest protocol
Session event protocol
Evidence protocol
Plugin protocol
Worker protocol
Repository intelligence protocol
```

Keep implementation replaceable.

This lets:

```text
third-party UI
third-party driver
new harness
new analysis engine
```

participate without embedding Daoris code.

## Priority

**P2 long-term architecture**

---

# 63. Compatibility Test Kit

If protocols become public, provide a conformance harness.

Example:

```bash
daoris compat test plugin ./my-plugin
daoris compat test harness my-agent
```

Outputs:

```text
PASS handshake
PASS capability declaration
PASS interruption
FAIL resume semantics
```

This is important if Daoris ever develops an ecosystem.

## Priority

**P3**

---

# 64. Local Simulation / Sandbox Workspace

Provide a tiny synthetic workspace used to test:

```text
quests
plugins
permissions
agents
routing
missions
sync
```

Something like:

```text
examples/sandbox/
  api/
  client/
  web/
```

with known expected workflows.

Useful for:

```text
regression
plugin development
new agent adapters
demo
documentation
```

## Priority

**P1**

---

# 65. Daoris Doctor 2.0

Current `doctor` focuses on doctrine duplication.

Eventually add independent diagnostic domains:

```bash
daoris doctor doctrine
daoris doctor workspace
daoris doctor agent
daoris doctor plugin
daoris doctor sync
daoris doctor knowledge
daoris doctor execution
```

Each remains mostly advisory.

A top-level:

```bash
daoris doctor
```

summarizes only actionable problems.

## Priority

**P2**

---

# 66. Explainable Automation as a Non-Negotiable Principle

Every autonomous action should be able to answer:

```text
Why now?
Why this repository?
Why this agent?
Why this account?
Why this permission?
Why this context?
Why this Quest dependency?
Why was this considered complete?
```

Store the facts used to make the decision.

Avoid storing only:

```text
selected = X
```

Prefer:

```json
{
  "selected": "X",
  "because": [...]
}
```

This should be treated as a core architectural rule.

---

# 67. Deterministic Core, Probabilistic Edge

A strong long-term rule for Daoris:

## Deterministic responsibilities

Daoris should own:

```text
ownership
locks
permissions
state transitions
versioning
sync
evidence existence
gate execution
budgets
capabilities
dependency constraints
provenance
```

## Probabilistic responsibilities

Agents may propose:

```text
task decomposition
repository selection when ambiguous
knowledge similarity
contradiction candidates
risk candidates
acceptance criteria
implementation strategy
```

## Human responsibilities

Humans decide:

```text
ambiguous ownership
high-risk execution
doctrine promotion
contested knowledge
important architectural choices
exceptions
```

This separation will prevent Daoris from becoming an opaque autonomous-agent framework.

---

# 68. Things Daoris Probably Should NOT Become

Protecting scope is as important as adding features.

## 68.1 Not another LLM SDK

Do not absorb:

```text
provider abstraction
prompt chains
memory algorithms
model registries
embeddings framework
```

That is closer to Lyntai's domain.

---

## 68.2 Not another CI/CD product

Daoris should coordinate:

```text
GitHub Actions
Azure DevOps
Terraform
deployment tools
```

rather than replace them.

---

## 68.3 Not an IDE

The working surface should show:

```text
tasks
sessions
diffs
evidence
decisions
```

but avoid rebuilding VS Code.

---

## 68.4 Not a generic project manager

Do not compete with:

```text
Jira
Linear
GitHub Projects
```

Daoris should manage the part where:

> intent becomes engineering execution.

---

## 68.5 Not a "multi-agent swarm" framework

Avoid agents talking endlessly to agents.

Prefer:

```text
explicit ownership
explicit quests
fresh sessions
deterministic state
evidence
```

Only introduce parallel/multi-agent patterns where measured benefits justify them.

---

## 68.6 Not an enterprise platform too early

Avoid premature:

```text
large RBAC hierarchy
organization billing
marketplace
complex tenancy
workflow designer
custom scripting runtime
```

Daoris's strongest property today is that its architecture still has understandable boundaries.

---

# 69. Suggested Priority Roadmap

## Phase A — Trust

These make existing automation much safer.

### A1. Evidence Contracts
Quest completion requires explicit evidence.

### A2. Engineering Ledger
Trace intent → quest → session → diff → evidence.

### A3. Failure Taxonomy
Failures become structured learning inputs.

### A4. Human Decision Objects
Important human decisions persist across sessions.

### A5. Assumption Ledger
High-impact inferred assumptions become visible.

### A6. Independent Verification Session
Implementer is not its only reviewer.

### A7. Plugin / Agent Supply-Chain Hardening
Capabilities, versions, checksums, provenance.

---

# Phase B — Reliability

### B1. Session Checkpoints
Recover engineering state.

### B2. Replay / Fork Session
Recreate or retry from recorded execution envelopes.

### B3. Offline Sync Conflict Semantics
Make multi-machine state convergence explicit.

### B4. Environment Contracts
Detect incompatible execution machines before spawning.

### B5. Context Compiler
Build precise, inspectable context packages.

---

# Phase C — Intelligence

### C1. Repository Brief
Generate structured repository understanding.

### C2. Repository Intelligence Graph
Symbols, dependencies, contracts, ownership.

### C3. Semantic Diff
Turn code changes into architecture-level facts.

### C4. Blast Radius
Estimate affected contracts and repositories.

### C5. Knowledge Provenance
Track authority and lifecycle.

### C6. Contradiction Detection
Find conflicting engineering knowledge.

---

# Phase D — Multi-Repo Engineering

### D1. Mission
Coordinate multiple Quests without breaking ownership.

### D2. Contract Change Detection
Detect API/event/schema changes.

### D3. Dependent Quest Proposal
Create work for affected consumers.

### D4. Workspace Gates
Verify cross-repository invariants.

### D5. Release Missions
Coordinate existing CI/CD tools around release evidence.

---

# Phase E — Learning

### E1. Outcome Metrics
Measure accepted results, not just usage.

### E2. Evidence-Based Agent Routing
Choose agents using historical outcomes.

### E3. Incident → Doctrine Loop
Turn repeated failures into candidate rules/knowledge.

### E4. Doctrine Effectiveness
Measure whether rules reduce targeted failures.

### E5. Context Experiments
Measure which context actually improves outcomes.

---

# Phase F — Scale

Only after the earlier phases are proven.

### F1. Headless Driver
Same driver semantics outside Desktop.

### F2. Remote Workers
Capability-aware execution machines.

### F3. Safe Parallelism
Git worktree based concurrent sessions.

### F4. Cost Policies
Budget per Quest/Mission/workspace.

### F5. Protocol Conformance Kit
Support external ecosystem implementations.

---

# 70. Recommended Near-Term Top 10

If choosing only ten additions, I would prioritize:

1. **Evidence Contracts**
2. **Engineering Ledger**
3. **Context Compiler**
4. **Session Recovery / Checkpoints**
5. **Independent Verification Sessions**
6. **Human Decision + Assumption Ledger**
7. **Repository Brief / Onboarding Intelligence**
8. **Repository Intelligence as structured facts**
9. **Mission — multi-Quest orchestration**
10. **Outcome Metrics → evidence-based routing**

These ten reinforce each other:

```text
Intent
  ↓
Repository Intelligence
  ↓
Mission / Quest
  ↓
Context Compiler
  ↓
Agent Session
  ↓
Checkpoint
  ↓
Diff
  ↓
Independent Verification
  ↓
Evidence
  ↓
Engineering Ledger
  ↓
Outcome Metrics
  ↓
Better future routing
```

That is a real learning loop without giving control to an opaque autonomous agent.

---

# 71. Potential Long-Term Architecture

```text
                          Human
                            │
                    Decision / Intent
                            │
                            ▼
                    ┌──────────────┐
                    │    Daoris    │
                    │ Control Plane│
                    └──────┬───────┘
                           │
          ┌────────────────┼─────────────────┐
          │                │                 │
          ▼                ▼                 ▼
      Knowledge        Repository         Policy
       System          Intelligence       / Trust
          │                │                 │
          └──────────┬─────┴──────┬─────────┘
                     │            │
                     ▼            ▼
                   Intake       Mission
                                  │
                      ┌───────────┼───────────┐
                      ▼           ▼           ▼
                    Quest       Quest       Quest
                      │           │           │
                      ▼           ▼           ▼
                   Session     Session     Session
                      │           │           │
                      └──────┬────┴────┬──────┘
                             │         │
                             ▼         ▼
                          Changes    Evidence
                             │         │
                             └────┬────┘
                                  ▼
                            Verification
                                  │
                                  ▼
                           Engineering Ledger
                                  │
                    ┌─────────────┼─────────────┐
                    ▼             ▼             ▼
                 Outcome       Incident       Knowledge
                 Metrics        Learning       Evolution
                    │             │             │
                    └─────────────┴─────────────┘
                                  │
                                  ▼
                         Better future work
```

---

# 72. The Strategic Moat

Many products can launch coding agents.

Many products can show sessions.

Many products can wrap Claude/Codex.

Those are unlikely to remain durable differentiators.

Daoris can build a stronger moat around:

```text
Engineering provenance
+
Repository ownership
+
Cross-repository coordination
+
Evidence-based completion
+
Knowledge evolution
+
Measured execution outcomes
```

The valuable dataset is not:

> chat history.

It is:

```text
Intent
→ engineering context
→ decisions
→ implementation
→ evidence
→ outcome
→ lesson
```

That structure is far harder to reproduce than an agent launcher.

---

# 73. Final Direction

The most natural evolution for Daoris is:

```text
v0.x
Engineering doctrine
        ↓
Cross-repository knowledge
        ↓
Quest routing
        ↓
Agent driver
        ↓
Engineering control plane
        ↓
Evidence + provenance
        ↓
Recovery + reproducibility
        ↓
Repository intelligence
        ↓
Multi-repository missions
        ↓
Measured learning loop
```

The key principle should remain:

> **Daoris manages engineering reality; agents supply engineering judgement.**

Do not make Daoris smarter by making more things probabilistic.

Make it smarter by giving agents better facts, stronger boundaries, better evidence, and a system that remembers what actually happened.

That would make Daoris less like another "AI coding platform" and more like an actual **operating layer for AI-assisted software engineering**.
