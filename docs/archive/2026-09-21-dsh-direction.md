# The dsh direction — the plan, before the evaluation

> Written 2026-09-21, from the owner's direction the same day: **deepseek-harness is a good example to
> use — maybe fully adopt its function too, to improve our system — and the direction starts next
> session, planned properly here.** This document is that plan. It is not the decision: the decision
> is DSH1's to produce, D-numbered, with running-code evidence — the way D47 priced git-as-store
> before any host existed. What this plan fixes is the option space, the boundaries at stake, the
> probes the evaluation must run, and what holds still while it runs.

## 1. What dsh is, functionally — the grounded facts

[deepseek-harness](https://github.com/deepseek-ai/deepseek-harness) (`dsh`, MIT, developer preview) is
an **agent harness**: the layer that runs the agent loop itself. Facts established from its own tree
(a checkout is available locally; the survey is recorded here so the evaluation starts ahead):

- **It is the loop, not a wrapper.** Model adapters (`anthropic-messages` — claude models included —
  `openai-completions`/`-responses`, DeepSeek, custom gateways), a scoped tool registry, a durable
  session-event log (`turn/step/tool` records with embedded streams), approval policy, sandbox seams,
  subagents, jobs, compaction. Everything is a Cordis plugin; profiles compose a deployment (`web`,
  `headless`, `sdk`, `acp`).
- **Its Claude Code/Codex "bridges" are configuration compatibility, not delegation**: they run a
  repository's *existing* `hooks.json`/settings hooks inside dsh's own loop. dsh substitutes for those
  harnesses while honoring what the repository already declared.
- **It can also genuinely delegate to them**: `subagent-claude-code` and `subagent-codex` are one-shot
  delegation providers — a dsh session can hand work to the real claude-code or codex product.
- **It is drivable over a protocol.** The `acp` profile is an automation-only **ACP (Agent Client
  Protocol) server** over JSON-RPC stdio, and `subagent-acp` makes dsh a *client* of any ACP agent.
  There is also a TypeScript/Python SDK profile. Structured session events over a contract — not
  stdout to be parsed.
- **Its web console is the working surface we already adopted structurally** (D52 as amended,
  `2026-09-21-working-surface-components.md` §3a) — three-column frame, per-session dock, trajectory.
- **Its docs system is a standard with gates** (DOCS1) — much of it independently converging with this
  repository's own doctrine.

## 2. The seam, stated once

**dsh owns the session; Daoris owns the family.** On the family layer they do not overlap at all: dsh
has no canon, no cross-repository quests, no registry/workspaces, no convergence, no coexistence
guarantee, no zero-dep connector — it is single-workspace, session-centric. On the session layer the
overlap is total by ambition: dsh's loop, records, approvals and console are a strictly richer version
of what Daoris observes from outside a harness's stdio.

Every option below is an answer to one question: **how much of the session layer does Daoris hand to
dsh?**

## 3. The options

| | What it is | What it gives | What it costs / touches |
|---|---|---|---|
| **A · dsh as a third harness** | An `ISessionAdapter` + toolchain entry, HARNESS2-shaped: the driver spawns `dsh headless` sessions; chat via its stdio | A third agent option; multi-provider models through one tool; cheap and purely additive | Little; D23's "explicit second" rule as written. Improves nothing structural |
| **B · ACP as the driver's session protocol** | The adapter seam grows a *protocol* door: the driver holds sessions over ACP (dsh natively; claude-code through its ACP adapter where one exists) instead of raw pipes | **Structured session events by contract** — turn/step/tool boundaries the working surface's timeline could render without the screen-scraping D52 rejected (rejecting stdout-parsing was right; a protocol is not parsing); turn-taking that is a real API; one wire format across harnesses | The adapter seam is re-cut (D23 evolves, not breaks); the driver's observed-lifecycle model gains an event source it must still not *trust* over observation (D46 — records stay observed); per-harness ACP maturity varies and must be probed, not assumed |
| **C · the working surface AS a dsh deployment** | Daoris's Work view (SURF4+) is not built on `Daoris.Web` — the desktop hosts a dsh web deployment, and quests/registry/trees/records land as **Daoris plugins** in its tree | The whole console — trajectory, approvals, docking, session management — for the cost of plugins instead of a UI build; upstream keeps improving it | D38 strains (two UIs in fact if the ops console stays); D41/D42 stack is displaced for the surface; Daoris takes a Cordis dependency and a developer-preview product on its critical path; the D31/D47 boundaries (doctrine unwritable; streams never leave) must be re-proven inside someone else's host |
| **D · full re-platform** | Daoris's service/desktop rebuilt as dsh plugins; the family layer becomes a plugin family | One runtime, one plugin model, maximal reuse | Reopens **D1** (Daoris is process tooling, not an LLM product) — explicitly the owner's call and nobody else's; dissolves the D36/D43 deployment story; the CLI stays untouchable regardless (zero-dep, offline — non-negotiable under every option) |

These are not exclusive: **A is a strict subset of B, and C can sit on either.** The likely honest
outcome is a composite (e.g. B for the driver + structural adoption for the UI, revisiting C when dsh
leaves preview) — but that is DSH1's to decide with evidence, not this plan's to presuppose.

## 4. The boundary inventory

What each standing decision demands of the evaluation:

- **D1 / D24 — no model calls in Daoris; no model named.** dsh *configures* models; Daoris still must
  not. Under every option the model stays the harness's configuration — dsh's own `settings.yaml` in
  that repository — and the record reports what ran. Option D is the only one that reopens D1, and it
  says so out loud.
- **D8 / D33 — the CLI.** Untouchable under every option: zero runtime deps, offline doctrine
  commands, npx-pinned. Nothing dsh-shaped enters it.
- **D23 — adapters arrive deliberately, on proof.** Option A is this rule as written. Option B *re-cuts
  the seam itself* — that is a design change with a decision, not an adapter addition.
- **D37 — the approval boundary.** dsh has its own approval policy and permission surfaces. The
  evaluation must map them onto D37's line: reversible in-repository work proceeds; destructive,
  irreversible, outward-facing stays human. A richer approval surface must not widen autonomy (D52's
  words) — dsh's *progressive* patterns, if any, are declined the same way.
- **D46 / D47 — observed lifecycle; records sync, processes and streams never.** dsh's session events
  are self-reports from inside the loop. They may *enrich* a record; they must not replace the two
  observed signals (process lifetime, quest transitions) that outside work also produces. And a dsh
  web deployment binds a port — the disclosure rules (no machine path off-machine, streams local-only)
  must be re-proven against its surface if option C is ever taken.
- **D49 §3's rejection — a Daoris-owned chat loop.** Not reopened by A/B/C: dsh owns the loop, not
  Daoris; Daoris still pipes and observes. The rejection was about Daoris calling models with its own
  prompt, and it stands.
- **D51 — the tree is the unit of exclusion.** Unaffected by every option, which is why SURF2/SURF3
  were safe to land first: trees, the lock, and the registry are family substrate underneath whichever
  session layer sits on top.

## 5. What DSH1 must prove hands-on — the probe list

Run against the local checkout (read-only) and an installed `dsh`, over a scratch repository — the
same discipline as every rehearsal: **driven, not read about.** Each probe has a pass shape; a probe
that cannot pass is evidence, not failure.

**Session prerequisites, so nothing is discovered mid-probe:**

- **A model API key, supplied by the person at session time.** Probes 1, 4, 5, 6 and 8 run a real
  agent loop, and a real loop calls a real model — this is an *evaluation*, not a gate, so the
  no-model rule for gates does not apply, but the key never lands in a file and the session says which
  provider answered (D24's reporting instinct). Without a key, probes 2, 3 and 7 still run — protocol
  handshake, event vocabulary and version history need no completion — and the session downgrades
  honestly rather than stalling.
- **Hermetic dsh state.** dsh keeps a Harness home (`$DSH_HOME`); every probe sets it to a scratch
  directory so nothing touches the person's real profiles, credentials or sessions — the same
  discipline the rehearsals apply to `~/.daoris`. Probes run over a scratch repository, never the
  tracked examples and never a real one.
- **Scope guard.** Probes 1–2 are the core and decide A and most of B; 3–8 follow as evidence permits,
  and DOCS1 rides along. If the session runs long, it splits at the evidence note — a half-evaluated
  option is recorded as exactly that, never rounded up to a decision.

1. **The headless run** (`dsh headless` / one-shot): give it a quest-shaped target in a scratch repo;
   observe exit code, what landed in git, and what the session log records. *Pass: DRV4's shape — a
   commit, a readable durable record.* This is option A's whole requirement.
2. **The ACP session**: drive an agent over the `acp` profile's JSON-RPC stdio — start, send a turn,
   stream events, stop. Map the event vocabulary onto the driver's needs (spawn/observe/turn/stop) and
   onto the timeline's (turn/step/tool boundaries). *Pass: the working surface's timeline could render
   from these events with no stdout parsing.*
3. **Claude-code over ACP**: establish whether the *other* harness speaks ACP today (its own adapter,
   or via dsh's `subagent-acp`). *This decides whether B is "one protocol" or "one protocol plus the
   old pipes", which halves or doubles its value.*
4. **The hook-config bridge**: point dsh at a repository carrying real claude-code hooks; verify they
   fire and can block. *Decides whether a dsh session honors the repository's existing D37 wiring.*
5. **The subagent delegation**: a dsh session delegating one task to real claude-code. *Establishes
   whether dsh-as-orchestrator composes with the products we already support rather than displacing
   them.*
6. **The approval map**: trigger dsh's approval flow; write down which of its policy knobs correspond
   to acceptEdits-style postures and where D37's hard line lands. *Pass: a configuration exists in
   which nothing outward-facing is auto-approved.*
7. **Stability and pinning**: record the version probed, what `THERE WILL BE COMPATIBILITY-BREAKING
   CHANGES` has actually broken across its recent tags, and what a pin + upgrade discipline costs.
   *This prices the developer-preview risk instead of fearing it.*
8. **The plugin cost** (only if C stays live after 1–7): write one trivial Daoris plugin (read-only
   quest list in its web UI) and measure what it took — Cordis surface area, build integration, i18n,
   the D31 boundary. *Pass/fail is the honest build-cost number, not the demo.*

## 6. Risks, stated before they are discovered

- **Developer preview.** Breaking changes are promised by the README. Anything adopted is pinned to an
  exact version; nothing is vendored initially; probe 7 prices the churn.
- **Scale asymmetry.** dsh is a large system with its own framework (Cordis) and its own doctrine.
  Daoris's discipline is small owned surfaces with gates. Options C/D trade owned surface for leverage
  — the evaluation must weigh *maintenance* leverage, not demo leverage.
- **MIT mechanics.** Structure and learning carry no obligation; the first ported file brings a
  third-party notice in the same commit (already recorded in D52's amendment).
- **The two-door rule** (D50). Whatever is adopted must be manageable from a terminal as well as a
  screen — dsh's own headless/ACP profiles make this natural, but it must be checked, not assumed.

## 7. Sequencing — what holds and what does not

- **DSH1 is the next session**, displacing SURF4a: the decision could change what SURF4 is built *on*,
  and building the view first would be building on ground the owner has offered to move.
- **SURF4a–d, SURF5's UI half and SURF6's UI half hold** until DSH1's decision. Their *design* stands
  either way — the components inventory, the identity rules, the attention model and the D52 geometry
  transfer to a plugin implementation almost unchanged if C is chosen.
- **SURF2/SURF3 stand under every option** (the lock, the trees, the registry are family substrate),
  as do SURF5's driver half (notifications on park/end) and SURF6's mechanics (`SessionTrees` already
  refuses to destroy work; only the surface that calls it is open).
- **DOCS1 feeds DSH1** rather than running separately: the docs-system study is one strand of the same
  evaluation, cheapest done while the checkout is warm.
- Everything held elsewhere (REH1, TEST1, CANON5/7, WSP5, HARNESS2) is untouched — though **HARNESS2
  (a codex adapter) should wait for DSH1 too**: if B is chosen, the codex adapter may be an ACP
  configuration rather than a hand-built `ISessionAdapter`.

## 8. What DSH1 produces

The way DRV1, DRV3 and SURF1 did: **an evidence note** (the probe results, verbatim where they
surprise), **a numbered decision** naming the chosen option and the rejected ones with reasons, and
**a build order of session-sized items**. The owner decides the option — this is a direction with
D1-sized stakes at its far end, and the evaluation's job is to make that decision cheap and
well-lit, not to make it.
