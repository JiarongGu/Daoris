# STAFFPOLICY1 — Codex's proposal: staffing policies with explicit selection and bounded switching

Status: sealed proposal, 2026-11-02, from `docs/2026-11-02-staff-policy-brief.md`; Codex, read-only, kept as it came.

Proposed under STAFFPOLICY1. Evidence is from repository revision `0031fb038ad090496343c58d621f75e81ce1a269`. This is a design proposal; its proofs describe implementation requirements, not checks run here.

**1. The decision**

A working role owns one staffing policy: its choices, how new work is distributed among them, and what observable conditions permit a replacement. The person sets that policy through `SET_ROLE`. The driver chooses within it and records why.

An absent policy means today’s behaviour, including today’s differences between starts, continuations and conversation limits. Installing this feature changes no staffing or spending.

The existing foundation already separates the necessary responsibilities:

- `RoleFor` resolves the role at a place, choosing the most specific scope and falling back from Design to Build: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleCatalog.cs:167`.
- `StartPlan` resolves that role’s choices and walks accounts inside each choice: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StartPlan.cs:153`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StartPlan.cs:283`.
- D197’s account walk reads the agent owner’s single account set: `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/HarnessRoster.cs:1207`.
- `CourseDecision` decides whether and when a line may continue; it expressly does not walk accounts: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/LineCourse.Decide.cs:5`.

Extend these responsibilities. Do not add a second router beside them.

**2. Policy shape and defaults**

Keep existing `choices` arrays valid and unchanged. Add an optional complete `staffing` value to a role set. A role has one authoritative staffing representation: the old array, interpreted as legacy ordered staffing, or the new value.

Example of an explicitly enabled policy:

```json
{
  "version": 1,
  "selection": {
    "rule": "weighted"
  },
  "choices": [
    {
      "id": "primary",
      "agent": "claude-code",
      "model": "sonnet",
      "effort": "high",
      "weight": 3
    },
    {
      "id": "alternative",
      "agent": "codex",
      "weight": 1,
      "limits": {
        "works": ["build"],
        "dailyAdmissions": 20
      }
    }
  ],
  "on": {
    "limit": {
      "action": "next",
      "selection": "policy",
      "exhausted": "wait-reset"
    },
    "access": {
      "action": "next-account",
      "exhausted": "next"
    },
    "model-refused": {
      "action": "next",
      "selection": "policy"
    },
    "transient-exhausted": {
      "action": "ask"
    },
    "quiet": {
      "action": "ask"
    }
  },
  "bounds": {
    "automaticMoves": 3,
    "failedOpens": 8
  },
  "exhausted": "ask"
}
```

The agent, model and effort above are illustrative authored values, not shipped recommendations.

| Field | Contract |
|---|---|
| `version` | Required on a new policy. Unknown versions hold affected starts with an explanation. |
| `choices` | One to four, retaining the existing limit. Stable ids distinguish alternatives across reordering and history. |
| `agent`, `model`, `effort` | The tool’s own names. Omitted model or effort retains account default. No model catalogue or alias equivalence is introduced. |
| `weight` | Positive integer, 1–100; default 1. Used only by weighted selection. Zero is invalid; removal is explicit. |
| Array order | Priority under ordered selection and the final tie-break under other rules. |
| `limits.works` | Optional subset of the role’s declared work codes. No model interprets a task’s prose to decide eligibility. |
| `limits.dailyAdmissions` | Optional maximum prompt admissions per UTC day, including pending reservations. |
| `limits.dailyTokens` | Optional admission threshold based on reported token usage, with its measurement limitations stated below. |
| `on` | Closed condition and action vocabulary. No authored regexes over arbitrary conversation text. |
| `bounds` | Required finite bounds for policies enabling new automatic moves. |
| `exhausted` | `wait-reset`, `ask` or `stop`; waiting requires an actual known reset. |

Omission of `staffing` on a role edit preserves its staffing. Explicit `staffing: null` removes the new policy and restores ordered staffing over its current choice tuples, after the review states which limits and conditions disappear. A request containing both a legacy `choices` edit and new `staffing` is refused as ambiguous.

For a policy-bearing role, a legacy choices-only writer must receive `policy-required`, rather than silently discarding the policy.

Stable ids survive weight, order and condition changes. Changing the agent/model/effort tuple creates a new choice identity. Daily usage already charged to the role remains charged; editing weights does not replenish a budget.

Today’s editor holds four choices, including typed model and effort alternatives: `src/Daoris.Web/src/agents/roles/RoleEditor.tsx:7`, `src/Daoris.Web/src/agents/roles/RoleEditor.tsx:108`.

**Inheritance follows the role, whole.** Repository overrides workspace; workspace overrides machine. A child role replaces the parent’s complete policy. There is no field-by-field merging of weights, budgets or conditions. *Give this place its own* copies the effective policy into a draft; *Back to workspace’s* removes the override and names the policy that will take over.

This follows the implemented scope ranking and the existing place editor’s inheritance actions: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleRecords.cs:27`, `src/Daoris.Web/src/agents/setup/PlaceRoles.tsx:45`.

D208 appointments remain separate. Staffing a working role grants no appointed authority, and editing staffing neither appoints nor transfers a holder. Working and appointed records are already distinct shapes: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleRecords.cs:57`; appointment authority is the owner’s window press: `docs/decisions/D208.md:42`.

**3. Selection: what a weight means**

A weight distributes **prompt admissions for new work**, not tokens, elapsed time, simultaneous sessions or successful outcomes. A 3:1 policy aims for three admissions to one choice for each admission to the other while both are eligible. The screen calls this a target, never a guaranteed spending share.

Ordinary resumes retain their recorded choice. A turn ending successfully does not trigger rebalancing.

| Rule | Deterministic selection |
|---|---|
| **Ordered — First that can run** | Existing array order, each choice’s D197 account walk before the next. |
| **Weighted — Spread work** | Smooth weighted round-robin among eligible choices. Maintain integer credits, add eligible weights, select the greatest credit, then subtract their total from the winner. Array order breaks ties. |
| **Round-robin — Take turns** | Next eligible choice after the last admitted choice, wrapping once. Equivalent to equal weights, with simpler explanation. |
| **Cost order — Cheapest first** | The person supplies each choice’s relative `costRank`; lowest first. This is explicitly *your cost order*, not a price Daoris calculated. Missing ranks refuse this preset. |
| **Measured success** | Rank comparable, attributed outcomes for the same work code, using `(accepted + 1) / (decided + 2)` over 28 days, after at least 20 decided outcomes per choice. Show the numerator, denominator and period. Unmeasured choices follow an explicitly selected ordered fallback. |

The first release can offer ordered, weighted and round-robin together. Cost order requires the person’s rankings. Measured success becomes selectable only after its attribution and outcome row is built; an inactive selector is not presented as functioning routing.

Current usage records explicitly contain no prices, and missing measurement is not zero: `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Usage.cs:28`. Current role-use reporting counts starts and pass reasons from machine logs; it is not success evidence: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleUse.cs:18`. Therefore this proposal introduces neither inferred prices nor “success” inferred from an agent saying it finished.

For success selection, an outcome must be a recorded workflow acceptance or explicit person verdict attributable to that choice. Pending, cancelled and provider-failed attempts are not rejection verdicts. Chats and other work without comparable outcomes use the disclosed fallback. No automatic exploration is added.

Unavailable choices accumulate no future entitlement. Their credit is cleared when excluded; returning choices rejoin without a burst of catch-up work. Changing the selection rule starts a fresh credit schedule. Historical admissions and budget use remain.

An optional percentage ceiling needs a denominator. Support `maxDailyShare` only alongside an explicit role-wide daily admission allowance: 40% of 100 permits at most 40 admissions that day. Do not claim that it guarantees 40% of actual traffic or cost.

**4. One judge, with account selection inside it**

Add a pure `StaffingJudge` under `Roles/`, composed into `StartPlan`. Its input is a value snapshot:

- Work, place, resolved role, role revision and policy digest.
- Trigger: start, limit, refused model, exhausted transient episode, quiet observation, hand-over or person continuation.
- Current choice and account; resume feasibility; explicit agent/account picks.
- Permission, maker-independence and confinement fences supplied by the caller.
- Available doors and versions, model offers and refusals.
- Account allowance, cooling and sign-in facts supplied through D197’s existing seam.
- Machine cap and pending reservations.
- Committed and pending admissions, measured usage and optional outcome data.
- Classified health observations, their source and expiry.
- Logical run’s move count, failed-open count and passed pairs.
- Injected clock and stable decision sequence.

Its output is a typed plan: **resume**, **try choice**, **wait until**, **ask**, **stop** or **hold**, with reason codes, passed candidates and the facts used.

The judge ranks choices. D197 chooses accounts. `CourseDecision` retains lifecycle vetoes: stopped or superseded lines, standing moves, shutdown, ownership and delivery preconditions. These are not policy preferences.

The existing `StartPlan` already reads the effective order once and carries role revision: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StartPlan.cs:159`. Its common loop already retains the soonest cooling entry when no choice runs: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StartPlan.cs:288`.

The execution sequence is:

1. Resolve `RoleFor`, or validate the workflow’s explicitly named role.
2. Apply non-negotiable fences and policy eligibility.
3. Rank choices using one snapshot. Read-only previews neither probe accounts nor advance scheduling state.
4. Ask the existing account walk for the highest candidate. Exhaust that choice’s accounts before considering another choice.
5. If live validation refuses it, record the reason, exclude the appropriate pair or choice, and rank again without advancing the schedule.
6. Reserve one admission and one account load count. Revalidate the cap and policy revision before opening.
7. Charge the admission when the first prompt write is attempted. A write whose outcome is uncertain remains charged. A pre-prompt refusal releases its provisional admission.
8. End and release an opened process before choosing a replacement that would use its slot.

Pending admissions participate in selection, so concurrent starts do not all choose the same apparent winner. Reservation and scheduler updates are serialized; no database lock spans an agent probe or network call.

The current account walk already distinguishes counted starts from non-counting questions, and its release clears both pending and observed load after a refused process stops: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StartPlan.cs:310`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/HarnessRoster.cs:1972`.

**5. Conditions and actions**

A condition records what was observed, where, when and by which classifier. It never treats an agent’s ordinary answer as a transport failure.

The implemented classifier has four kinds—limit, access, transient and fatal—and reads the failure’s own channel, codes and measured words. Unknown failures remain fatal: `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/AgentErrors.cs:5`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/AgentErrors.cs:149`.

| Condition | Observation | Permitted policy actions and boundary |
|---|---|---|
| **Limit reached** | Existing classified limit and recorded cooling/reset. | Preserve own-conversation resume when ready. Otherwise exhaust usable accounts of the current choice, then ordered or policy-ranked choices; wait for earliest usable reset, ask or stop. No strike where today’s limit path gives none. |
| **Account cooling or allowance exhausted** | Existing cool-off or allowance reading; not a new probe from a screen. | Exclude that account. D197 walks the rest. If the choice has none, consider another choice or wait. Never clear cooling to satisfy a weight. |
| **Account access refused** | Classified authentication, credential or permission failure. | Mark the account as needing the person, then walk other permitted accounts and choices. Ask if none remain. Never substitute a more permissive posture. |
| **Door unavailable or inadmissible** | Missing install/pin, unsupported posture, missing connector or failed confinement admission. | Pass the choice or hold with its repair action. Policy cannot authorize installation, access widening or unmeasured confinement. |
| **Model refused** | Existing offer/refusal reading; refusal before prompting, or the specifically measured silent-turn exception. | Pass the `(choice, account)` pair; try its other accounts, then another choice under the selected rule. No limit cooling or strike. An ordinary refusal after productive output is not this condition. |
| **Provider/service unavailable** | Classified provider connection/service failure or exhausted bounded open/prompt retry. | Keep existing same-conversation retries by default. An explicit policy may switch after a specified retry threshold, wait for a recorded retry time, ask or stop. A failure of Daoris’s own ledger/service holds all replacement starts; it does not justify changing agents. |
| **Repeated errors** | N classified transport failures in a declared interval, reset by an answered turn. Default for an enabled rule: three in ten minutes. | Open a temporary health exclusion, then choose another eligible choice, ask or stop. Unknown/fatal errors ask or stop by default. A failed test or declined task is not provider health. |
| **Quiet past the inactivity window** | Existing watcher’s state, episode and sleep-aware quiet clock. | Ask or leave running by default. Automatic end-and-switch is available only for a door/state with the required measured quiet evidence, known idle status and a confirmed stop. Otherwise that action is refused. |
| **Daily token threshold reached** | Reported usage plus pending allowances, with measurement completeness. | Hold the next admission, choose a permitted alternative whose applicable budgets allow it, ask or stop. It does not cut off a productive turn. Unknown usage holds a budget-controlled admission. |
| **Switch/open bound reached** | Durable logical-run counters. | Hold for the person or stop. Waiting, another look, restart and helper answers do not replenish the count. |

`next` means *the next eligible choice under the condition’s selected rule*. The editor offers **in listed order** and **using this policy**, so a person need not infer what “next” means under weights.

An account-level condition remains account-level. A provider health exclusion is scoped to the observed door/endpoint and version; it affects a model alone only when the failure identifies that model. Limits and sign-in failures never make an entire maker “unhealthy”. Expiry means *eligible to try again*, not *known healthy*.

Keep the existing transient schedule—five retries after 1, 2, 4, 8 and 16 minutes—unless the person explicitly changes the switching threshold: `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/TransientRetry.cs:20`.

Silence deserves particular restraint. Today the watcher says its signs cannot prove absence of work, brings quiet work to the person, and ends nothing automatically except its stated narrow cases: `docs/decisions/D200.md:18`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Events/SessionWatch.cs:63`. A staffing checkbox must not silently overturn that contract.

Likewise, a reported-token threshold is an admission guard, not a hard monetary cap. A turn can overshoot before its usage arrives. The review says this explicitly; a hard provider spend ceiling requires a provider-enforced mechanism.

**Bounds.** New automatic-switch presets propose three replacement parts and eight failed opens per logical run, editable within finite validated ranges. Account changes that create replacement parts count too. Ordinary resumes and eligibility checks do not. The person can allow another bounded batch; a policy cannot renew itself.

Legacy staffing retains its existing bounds and behaviour. Merely reading an old role does not introduce a new three-move hold.

**6. Starts, limits, hand-over and participants**

| Decision point | Policy application |
|---|---|
| **New driven start** | Resolve the role for the actual work and place, then allocate one admission. |
| **Ordinary continuation** | Keep the recorded choice and native conversation where valid. No weighted redraw. |
| **Limit** | Preserve D206’s own-account resume preference; otherwise apply the allowed replacement rule and compute the earliest reset across eligible choices. |
| **Model refusal** | Reuse the claimed start and reservation; pass the refusal without recursion or resetting attempt bounds. |
| **Person’s hand-over** | An explicit target remains their target. Show its model, budget consequence and policy exceptions before the press. |
| **Agent’s hand-over request** | Existing `handOver` consent still decides whether it may proceed. The staffing judge selects only within the permitted candidates. |
| **Helper** | Apply the helper rule and named ask first, including maker partitions. Weight only the candidates those rules permit. |
| **Partner** | The named agent remains fixed. Policy may select its model/effort alternative and D197 account; it cannot substitute another agent or relax confinement. |
| **Second opinion** | Independence partitions outrank weights. A model change does not create another maker. |
| **Brainstorm** | Freeze disclosed voices once. Within each permitted voice, select an alternative at planning time; never redraw or introduce another maker during a pass. Preserve the generator’s chosen tier for synthesis. |

These restrictions are already load-bearing. Helpers derive a fence from the ask and rule; partners expressly substitute no agent for a name; reviewer walking retains maker partitions: `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Helpers/HelperChoice.cs:48`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Partners/PartnerChoice.cs:26`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/ReviewerChoice.cs:331`.

The current brainstorm implementation freezes its plan and takes distinct agents: `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/DriverBrainstorms.cs:401`. A weight must not quietly turn that fan-out into a lottery over voices.

**A switch can continue the same logical turn, through a new part.** It cannot run two writers in the same tree. End or cancel the outgoing process, confirm it has stopped, record that boundary, then open the replacement. If stopping is uncertain, hold.

Use D189’s deterministic brief and the existing move delivery mechanism. Preserve the request, attributed words, last answers, work evidence, omissions and bounded outgoing note. Do not copy a harness store or invent a model summary. Existing moves already share one durable holder and distinguish unopened, undelivered and uncertain effects: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/ConversationMoves.cs:6`.

A forced limit, exhausted error or measured stall asks no extra closing turn of the failing agent. Person-initiated and agent-requested hand-overs retain their existing closing-note rule: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/HandOverAsks.cs:187`.

Move held words by their existing ids through the keyed open. Attempted-but-uncertain words remain uncertain rather than being replayed as new instructions. The existing hand-over records the old end before asking the new open: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Chat/ChatHandOver.cs:230`; the briefed path already moves words at the open: `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Chat/BriefedMove.cs:36`.

**7. What the person sees**

Use the existing `RoleEditor` everywhere: All roles, a place’s Roles fold and Taken by. It already separates a hook-free editor from bridge ownership: `src/Daoris.Web/src/agents/roles/RoleEditor.tsx:50`.

Presets fill a draft; they never save it:

- **First that can run:** current ordered behaviour.
- **Spread evenly:** equal weights.
- **Spread by weight:** editable weights and target admission shares.
- **Take turns:** round-robin.
- **Cheapest first:** asks for the person’s cost order.
- **By measured acceptance:** offered only when the measurement capability exists.

Conditions and bounds sit below choices in one fold. Advanced limits belong on each choice. Keep account configuration on the agent’s page.

The following are schematic window wireframes. At 1546 px, the activity rail is 48 px, list pane 260 px and main area 1238 px. At 680 px, the list collapses, leaving 632 px beside the rail; the editor stacks by its container width.

```text
1546 px — Agents / place / Roles

┌─────────────────────────────────────────────────────────────────────────────┐
│ Daoris                       Agents · Repository                            │
├────┬─────────────────┬───────────────────────────────────────────────────────┤
│    │ Places          │ Repository                                           │
│ A  │ This machine    │ ▸ Sessions                                           │
│    │ Workspace       │ ▸ Workflow                                           │
│    │   Repository ●  │ ▾ Roles                                              │
│    │                 │ Build · from Workspace · Spread 3:1           [Edit] │
│    │                 │                                                      │
│    │                 │ Build                                                │
│    │                 │ Use [Spread by weight ▾]                             │
│    │                 │ Choice   Agent          Model       Effort   Weight  │
│    │                 │ 1        Claude Code    sonnet      high       [3]   │
│    │                 │ 2        Codex          Default     Default    [1]   │
│    │                 │ [+ Choice]   Target: 75% / 25% of new admissions    │
│    │                 │                                                      │
│    │                 │ ▾ Switching conditions                              │
│    │                 │ Limit       Next using policy; else earliest reset   │
│    │                 │ Provider    Retry here; then ask                     │
│    │                 │ Quiet       Ask                                      │
│    │                 │ Bounds      [3] moves · [8] failed opens              │
│    │                 │ ▸ Choice limits                                      │
│    │                 │ ▸ Edit role                                          │
│    │                 │                                                      │
│    │                 │ Changes Build for this workspace's repositories.     │
│    │                 │ Existing runs keep their accepted policy.            │
│    │                 │ [Save] [Cancel]                                      │
└────┴─────────────────┴───────────────────────────────────────────────────────┘

1546 px — session head

Build · Codex · model reported by the agent
Spread 3:1 · choice 2 · Claude Code's usable accounts reached their limits
Move 1 of 3 · continued with Daoris's brief                         [Why?]

Why?
Policy: Build, revision 12, from Workspace
Selection: weighted · target 3:1 · candidate 1 excluded by cooling
Asked: account default    Acknowledged: …    Reported: …
[Parts and decisions]
```

```text
680 px — same editor, stacked

┌──────────────────────────────────────────────┐
│ Daoris                   Agents · Repository │
├───┬──────────────────────────────────────────┤
│ A │ Repository                               │
│   │ ▾ Roles                                  │
│   │ Build · from Workspace                    │
│   │ Spread 3:1                         [Edit] │
│   │                                          │
│   │ Build                                    │
│   │ Use [Spread by weight                 ▾] │
│   │                                          │
│   │ Choice 1                                 │
│   │ Agent   [Claude Code                  ▾] │
│   │ Model   [sonnet                       ▾] │
│   │ Effort  [high                         ▾] │
│   │ Weight  [3]                [Up][Down][×] │
│   │                                          │
│   │ Choice 2                                 │
│   │ Agent   [Codex                        ▾] │
│   │ Model   [Account default              ▾] │
│   │ Effort  [Account default              ▾] │
│   │ Weight  [1]                [Up][Down][×] │
│   │ [+ Choice]                               │
│   │ Target: 75% / 25% of new admissions       │
│   │                                          │
│   │ ▾ Switching conditions                   │
│   │ Limit                                    │
│   │ [Next using policy                    ▾] │
│   │ Then wait for the earliest reset.        │
│   │ Provider: retry here; then ask           │
│   │ Quiet: ask                               │
│   │ Automatic moves [3]                      │
│   │ Failed opens    [8]                      │
│   │ ▸ Choice limits                          │
│   │ ▸ Edit role                              │
│   │                                          │
│   │ Changes Build throughout Workspace.      │
│   │ [Save] [Cancel]                          │
└───┴──────────────────────────────────────────┘

680 px — session head

Build · Codex
Reported model: …
Spread 3:1 · choice 2
Claude Code's usable accounts reached their limits.
Move 1 of 3 · Daoris's brief carried.
[Why?] [Parts]
```

Weights remain numeric inputs with keyboard controls; no drag-only editing. Refusals stay under Save with the draft intact. Use container queries, existing spacing and both themes. These follow the UI contracts at `docs/2026-10-20-ui-ux-design.md:156`, `docs/2026-10-20-ui-ux-design.md:223`, `docs/2026-10-20-ui-ux-design.md:281`.

The Roles fold summarizes **rule, proportions, switching consequence and source**, rather than only its first choice. A health or budget restriction opens the relevant explanation. It reads last observations without probing, preserving today’s `ROLE_STRUCTURE` contract: `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs:402`.

**Explanation records.** Every admitted selection and replacement records:

```text
decision id; logical run; session/part; trigger
role id + revision; policy digest; stable choice id + displayed position
selected agent; asked model/effort; selection rule and decisive values
excluded choices and coded reasons; observation times
reservation; move/open counters; next wake or person action
```

Keep these machine-local in the desktop store, attached to session history. The head, terminal and role summary read the same decision. Machine logs describe it but are not scheduling state.

Always distinguish **asked**, **acknowledged** and **reported** tier. An absent reported model stays *not reported*. The existing head already preserves that distinction: `src/Daoris.Web/src/work/actors.ts:8`, `src/Daoris.Web/src/work/actors.ts:50`.

Automatic reset waits remain outside *What needs you*. A policy hold requiring a person becomes one typed wait, counted once and shared by the head, foot and attention view. Its acts are a bounded continuation, an explicit choice, editing the role, or stopping. A continuation grants no role-setting authority.

**Terminal twin.** Extend `daoris driver role`, preserving its existing grammar, with:

```text
daoris driver role build --json
daoris driver role explain build --work build --repository <repository> --json
daoris driver role set build --staffing-file policy.json --revision 12 --plan
daoris driver role set build --staffing-file policy.json --revision 12
```

The plan writes nothing and reports scope, spending consequence, limits and unsupported observations. Explain neither probes nor advances selection. The actual set remains `SET_ROLE`, with its existing authority. Today’s terminal already plans role changes through the role judge: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleVerbs.cs:72`.

**Ask Daoris.** Extend `role_propose`; do not add an applying `settings_*` door. A proposal carries the complete staffing value, revision and scope, shows the same review, and applies through `RoleDoor`. Today’s role proposal already checks the revision and uses that door: `src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpRoleProposals.cs:53`.

D222 applies throughout: authored names and tool values remain as written; shipped preset names, labels and reason codes translate through every shipped catalogue. No configuration asks for English and Chinese fields: `docs/decisions/D222.md:5`.

**8. Storage, compatibility and changes to the decisions**

Store new policy definitions, scheduling state, reservations and decisions in new desktop-store tables. Keep legacy staffing authoritative until an explicit policy set. Do not hide new state inside machine logs or create another transcript store.

A role set must compare its expected revision and commit the role record, staffing policy and revision as one transaction. Today `RoleDoor.Apply` writes staffing and catalogue separately: `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleDoor.cs:50`. The new policy must not acquire that partial-update window.

Older records without policy metadata remain historical facts: show their recorded choice and tier, and *selection policy not recorded*. Do not reinterpret them using today’s weights.

A logical run binds its accepted policy snapshot. Edits affect new runs; an explicit reviewed continuation can adopt a newer revision. Restart restores the bound snapshot, counters and pending effects. Permission revocation and stopping still take effect immediately.

**Older executable safety needs an executable boundary.** Older readers ignore extra fields on a valid choice, so putting weights beside `agent` would silently turn a budgeted policy into an ordered list: `src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/ActorKey.cs:112`.

For policy-bearing staffing:

- Keep the authoritative definition in new tables.
- Project an explicit unsupported/unreadable guard into staffing read by supported older builds, rather than a runnable approximation.
- Raise the writer stamp so those builds cannot overwrite the guard.
- Test the older reader’s actual start paths, including legacy fallback keys. A stamp alone blocks writes, not execution.
- If an older build cannot be held at that boundary, it is outside supported rollback. State that before enabling the policy.

The current store checks its writer stamp inside every write transaction, while staffing can already represent unreadable entries: `src/Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStore.cs:713`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleStaffing.cs:196`.

| Record | Amendment |
|---|---|
| **D205** | Ordered choices become the default policy. Add stable ids, explicit distribution rules, conditions, bounded replacement and decision explanations. Withdraw its rejection of routing only for the person-enabled forms specified here. Preserve tool-native model values, tier disclosure, permissions and the person’s role-setting press. |
| **D206** | Preserve limited ends, no-strike handling, own-account resume preference and look-based waking. Replace “walk the ordered choices” with “apply the bound policy’s permitted replacement rule”. Earliest reset considers only eligible choices. |
| **D197** | Account ownership, set, allowance ranking, cooling, own-sign-in exclusion and account picks remain. Add no role/account assignment. The role judge consumes account facts and uses the existing walk. |
| **D189 / hand-over** | Preserve native resume and deterministic briefed parts. Add staffing decision identity to the move; no new transcript-copy mechanism. Error and stall replacements require a confirmed end before another writer starts. |
| **D200 / D210** | Keep quiet evidence and classified error sources. New automatic actions require explicit policy consent and the stated observation capability. Unknown silence or failure does not become an automatic fallback. |
| **D208** | Staffing remains separate from appointment and authority. No holder key, grant or appointment follows a staffing choice. |

**9. Code changes, file by file**

All paths below name existing files read for this proposal; new files are identified as additions.

| Existing file and evidence | Proposed change |
|---|---|
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleRecords.cs:62` | Keep role identity, authored name, scope and authority separation. Bind staffing through the role’s revisioned transaction. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleCatalog.cs:167` | Retain `RoleFor`; return the effective policy source with the resolved role. Preserve ambiguity and unreadable holds. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/ActorChoices.cs:59` | Preserve legacy normalization, picks and fences. Add normalization into stable policy choices without introducing implicit candidates. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleStaffing.cs:174` | Read legacy or policy-backed staffing. Add the new policy store and compatibility guards. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StaffingProjection.cs:182` | Project legacy staffing unchanged; project guarded staffing for a policy an older build must not execute. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleDoor.cs:50` | Accept and validate the complete policy; atomically compare revision and write role plus staffing. Extend batch validation too. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StartPlan.cs:240` | Compose the new pure `StaffingJudge`, reservation owner and deterministic candidate order into the existing walk. Preserve D197 inside each choice. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/BuildWalk.cs:24` | Extend pass records with policy eligibility reasons and decision identity; retain deduplicated logging. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/HarnessRoster.cs:1196` | Expose cached account facts and claim/revalidation seams. Keep owner accounts, allowance reading and count release here. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/AgentErrors.cs:138` | Export classified observations and classifier provenance to the judge; retain classification precedence. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/TransientRetry.cs:18` | Report episode progress to policy evaluation; preserve current retries when no policy changes them. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs:1457` | Replace separate bounded reselection branches with one attempt owner, carrying reservations and passed pairs across replacements. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs:2262` | Preserve own-resume precedence, then invoke the same policy used at a start. Remove local choice-count inference from policy decisions. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/LimitResume.cs:28` | Keep limit-line and reset-word semantics; attach the staffing decision and policy-bound wait. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Chat/LimitMove.cs:139` | Replace the separate same-agent/other-agent candidate assembly with the shared policy plan, preserving picks and delivery. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/HandOverAsks.cs:141` | Obtain automatic target ordering from the driver’s policy result. Retain explicit person targets and agent-request consent. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Chat/ChatHandOver.cs:184` | Carry the selected choice and decision through the durable move; preserve turn holds, closing notes and stop-before-open. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Course/ConversationMoves.cs:6` | Associate decisions with existing move recovery; scheduler recovery must not independently resend words. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/ReviewerChoice.cs:259` | Pass maker partitions and admission fences into the common judge; remove independent ranking within a partition. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Helpers/HelperChoice.cs:48` | Retain ask/rule candidate construction; supply its fence and actual place to the judge. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Partners/PartnerChoice.cs:26` | Restrict policy selection to the named agent and existing measured admission. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/DriverBrainstorms.cs:394` | Select alternatives while freezing voices; preserve disclosed participants throughout the brainstorm. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Events/SessionWatch.cs:63` | Supply typed quiet observations; keep unsupported automatic actions unavailable. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleUse.cs:28` | Report policy admissions from durable decisions, with rule, revision and coverage; retain legacy log-derived history as such. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleVerbs.cs:72` | Add policy parsing, planning and explanation through the same judge and role door. |
| `src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpRoleProposals.cs:89` | Carry policy drafts, revision and review through existing role proposals. |
| `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs:379` | Extend `ROLES` and `ROLE_STRUCTURE` with policy summaries/capabilities; extend `SET_ROLE` and `SET_ROLES` payloads without changing their authority. |
| `src/Daoris.Web/src/agents/roles/RoleEditor.tsx:57` | Add presets, weights, conditions and bounds to the one editor. Keep it hook-free. |
| `src/Daoris.Web/src/agents/roles/RolesView.tsx:75` | Own policy drafts and revisioned saves; preserve inline refusal handling. |
| `src/Daoris.Web/src/agents/setup/PlaceRoles.tsx:31` | Replace first-choice-only summaries with policy and restriction summaries; copy complete policies when creating overrides. |
| `src/Daoris.Web/src/work/actors.ts:50` | Add decision explanations beside asked, acknowledged and reported tier. |

Add small, separately tested policy types, judge and scheduling-store files under `Roles/`; avoid growing `StartPlan` into the storage implementation. Extend `Store/DesktopStore.cs` with the new tables and writer boundary, following its transaction contract at `src/Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStore.cs:48`.

**10. Rows in build order**

| Row | Contract | Proof required |
|---|---|---|
| **STAFFPOLICY1a — Types and legacy interpretation** | Versioned shape, stable ids, closed conditions, complete inheritance; absent policy reproduces current behaviour. | Table tests for every seeded work, scoped roles, Design fallback, off, picks, fences, malformed and unknown versions. |
| **1b — Durable policy and compatibility** | Atomic revisioned role/staffing write; new tables; older-reader guard; budgets survive edits. | Stale concurrent saves, crash before commit, batch refusal, old reader/start statements, older writer rejection and raw projection recovery. |
| **1c — Deterministic selection** | Ordered, weighted and round-robin allocation; pending reservations count; previews have no effects. | Exact equal and 3:1 schedules, eligibility changes, tie order, cancellation, concurrent starts, restart and cap races. |
| **1d — Common start and refusal owner** | One reservation across account and model refusal; finite passed pairs and failed-open bound. | Extend start-plan and role-start suites; real-process silent model refusal and access refusal; no double count, strike or cooled model refusal. |
| **1e — Limits and durable replacement** | Own resume first; otherwise policy replacement or earliest valid reset; bound counters persist. | Driven and conversation paths, held words, removed choices, explicit picks, refused resume and crashes at every move phase. Assert context and word ids, not just replacement agent. |
| **1f — Participant fences** | Helpers, opinions, partners and brainstorms use the same selection without widening candidates or authority. | Other-maker precedence, named partner confinement, role not listed, policy work exclusions and frozen brainstorm voices. |
| **1g — Error and quiet conditions** | Existing classifiers and retry defaults; explicit thresholds; unsupported stall actions refused. | Recorded wire failures versus identical answer text, fatal unknowns, retry exhaustion, quiet with busy/unknown CPU, sleep and failed cancellation. |
| **1h — Usage limits and optional rankings** | Admission/token limits disclose measurement gaps; cost order is authored; success ranking uses attributed outcomes. | Missing usage is unknown, no budget refill by reweighting, concurrent budget claims, UTC rollover, turn overshoot wording, score attribution and unmeasured fallback. |
| **1i — Decisions, waits and terminal** | One durable explanation, machine-local; a person hold counts once; role sets remain the person’s. | Head/terminal/role agreement, absent historical policy, no remote leakage, wait actions and badge count, route census, publishing and Ask Daoris coverage. |
| **1j — One editor and proposals** | Presets remain drafts; complete inheritance; typed tool values; D222 words; stale saves preserve drafts. | Editor and proposal tests, all shipped catalogue parity, keyboard/IME, both themes, stories at actual container widths and rendered geometry. |
| **1k — Integration and installed proof** | Every decision point reaches the same judge; upgrade changes no existing spending; enabled policy explains actual replacements. | Declared component gates and baseline verification; serial process/recovery suites; installed windows at **1546 and 680 px**, including a real limit replacement, a policy hold and its terminal explanation. |

Acceptance requires evidence that legacy runs remain unchanged, explicitly enabled distribution works under concurrent starts, and a replacement preserves the line’s context and words while naming the tier that actually ran.