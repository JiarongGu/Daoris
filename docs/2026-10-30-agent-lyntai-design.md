# Daoris's providers on Lyntai's agent-provider layer (AGENTLYNTAI1, 2026-10-30: round 1, proposed)

**Round 1's standing (proposed under D191, as an amendment to D213):** the synthesis of the two sealed proposals, Codex's
(`docs/2026-10-30-agent-lyntai-proposal-codex.md`, *X*) and the independent Opus reader's (`docs/2026-10-30-agent-lyntai-proposal-opus.md`,
*O*), each written from the brief (`docs/2026-10-30-agent-lyntai-brief.md`) and the survey (`docs/2026-10-30-agent-lyntai-survey.md`) alone and
sealed before its author read the other; this synthesis is written by Claude Opus 5.5 from both. Every one of the brief's twenty-three
questions is answered once, in §1's table and the section it names; where the two agree it is said once, and where they differ the choice
names whose it follows, why, and the case the other gets wrong (§1.1). It changes a foundation in the senses D191's amendment of 2026-10-20
names (the seam every agent's process is started, spoken to and read through moves into another repository's package; D213's rows, D22 and
D212's map are re-cut), so **both makers read this round before any of it is accepted** (D191's note of 2026-10-24; the brief §2 points 3
and 4), and the rows marked ⚖ (§9) are built only after both makers' reads find nothing blocking in them. **For what stays Daoris's, D213's
accepted text stands**; what this design re-cuts of it is named row by row (§3.1) and becomes a note on D213 only at acceptance. Nothing here
is built, spends an account, signs in, publishes, sends a request, writes into Lyntai or deletes.

**Read at** Daoris `23cc27254` (the batch line `parent/batch-1029p`: AGENTPROVIDER1's rows a and b, FOUNDATION1j's part i and both proposals;
main at `a3e226e58` carries none of them) and at Lyntai's `master` `9a41435c`, whose agent layer is the survey's `e010099f`, with its AG20
commit `2fb2dc7e` (its D234 and D235), which is no ancestor of `master` and whose layer sources its replay `0cbd84c1`, the one commit of its
session branch beyond `master`, carries unchanged. Both proposals read Daoris at `b96f93f9c`; since then FOUNDATION1j part i landed
(`Roles/StartPlan.cs`; D212's note of 2026-10-30), which changes no provider file, so their Daoris lines stand. Lyntai was only read. Paths
follow the survey's aliases (a bare path is under the driver library; `Tests/`, `Desktop/`, `Service/`; `Lyntai/Core/` is its
`src/Lyntai.Core/`, `Lyntai/Basic/` its `src/Lyntai.Providers.Basic/`); a Lyntai line marked *(AG20)* is `2fb2dc7e`'s, any other `master`'s.
*X §n* is a section of Codex's proposal, *O Qn* the Opus reader's answer to question n; X's rows are M0 to M16 and its requests Q-F, Q-L, Q-O,
Q-R, Q-K, Q-C, Q-M and Q-U; O's rows are L1 to L8 and its requests Y1 to Y8. This design's requests to Lyntai are **LQ1 to LQ12** (§8) and
its new rows **AGENTLYNTAI1a to 1f** (§9); D213's rows keep their letters (*row d* is AGENTPROVIDER1d). Read for it: the brief, the survey and
both proposals whole; D191; D213 with every note; D22; the provider design's §1 to §1.2, §11 and §12; FOUNDATION1's §2.4, Part B's order and
rows (`docs/2026-10-25-foundation-design.md:435-460`, `:1674-1739`) and D212's note on row j part i; the infrastructure design's §7 and §8;
in Lyntai its D18, D233, D234 and D235, the layer's §5, `TASKS.md`'s AG10 to AG19 and the layer's sources each line below cites.

## 0. What this rests on

### 0.1 The direction

The owner's direction of 2026-10-29 (D213's note of that date), as the brief §1 carries it: Daoris's provider layer is Lyntai's agent-provider
layer, consumed as a package; what an agent is gets built once, in the library, and Daoris keeps what is a host's; rows a and b stand as the
seam and the meter; rows c onward wait for this design; building them here and moving them later, a copy of the layer's types inside Daoris,
and a dependency taken before this design are rejected. In D193's picture it serves the equipment and vendors (part 14) and the departments
(part 3): the layer is another department's work, which Daoris asks for and never edits.

What the two proposals share, and this design keeps without argument: Daoris's contract stays and becomes **an adapter over the layer's real
objects, never replaced by the layer's descriptor** (X §3, O Q3); a door moves onto the layer **only when a release carries everything Daoris
does on it today, shown by the conformance suite case for case**, and until then its transport is untouched (X §6, O Q6); **the transports are
not rebuilt here**: rows i, k, j's seam and l's four readers made one (X §1, O §1.2); **a long-lived run is asked of Lyntai now**, Lyntai's
D233 re-open met by Daoris's measured protocol queueing and held native stdin (X Q-L, O Y3); Daoris keeps its account rule, its walk and
`cooling.json`, and takes **the layer's restriction vocabulary, never its pool as the walk** (X §7, O Q7); **one failure reading**, D210's
(X §8, O Q8); **Events names no Lyntai type**, the adapter translating the layer's events into Daoris's readings (X §9, O Q9); **Daoris's
runner is its launcher**, behind Lyntai's `IProcessRunner`, Lyntai's own runner never used (X §10, O Q10); row f, D72's grammar, reach, the
connector plan, the plugin catalogue and the meter stay Daoris's; **`Lyntai.Core` and `Lyntai.Providers.Basic`, taken by the driver alone**,
the metapackage refused (X §15, O Q15); **nothing merges against an unreleased layer**, D22 standing for what lands (X §17, O Q17);
OPENROUTER1's executor **Claude Code's program at the gateway**, the HTTP door not its executor (X §19, O Q19); FOUNDATION1's g, j, q and r
reading Daoris's values, the layer's `PickAsync` never the walk (X §20, O Q20); and the window's words unchanged (X §21, O Q21).

### 0.2 The Opus reader's gaps, checked

O's §0 names ten places the survey is short. Each was checked by a line in either repository: **every one holds**, three wider than O says.

| # | O's claim | At the commits above | Holds |
|---|---|---|---|
| 1 | D234 and D235 bind only once the owner accepts Lyntai's AG20 branch | `2fb2dc7e` is no ancestor of `master` `9a41435c`; its replay `0cbd84c1` is its session branch's one commit beyond `master`; D234's and D235's *Standing* make the owner's acceptance of the branch binding (`Lyntai/docs/DECISIONS.md:6711-6712`, `:6749` *(AG20)*); `master`'s descriptor has no `Agent`, which AG20 adds (`Lyntai/Core/Agents/AgentProviderDescriptor.cs:134`, `:137` *(AG20)*) | yes |
| 2 | A built-in door's descriptor is internal, reachable only by a registration | `ClaudeAgentDoor`, `CodexAgentDoor`, `AcpAgentDoor` are `internal static` (`Lyntai/Basic/ClaudeCli/ClaudeAgentDoor.cs:9`, `CodexCli/CodexAgentDoor.cs:9`, `Acp/AcpAgentDoor.cs:10`); the descriptor is built inside the registration (`Acp/AcpBuilderExtensions.cs:59-61`, `ClaudeCli/ClaudeCliBuilderExtensions.cs:100-104` *(AG20)*), whose summary says a consumer never constructs its types by hand (`AcpBuilderExtensions.cs:10-11`). **Wider:** the usage source is internal too (`Lyntai/Basic/CliUsageSource.cs:48`, `UsageAgentProvider.cs:8`), so a door's usage reading is reached only through its session's registration | yes, wider |
| 3 | A registration's environment is fixed for its life; a turn carries none | the environment is a registration parameter passed to a keyed singleton (`AcpBuilderExtensions.cs:50`, `:62-69`; `ClaudeCliBuilderExtensions.cs:95`, `:105-116` *(AG20)*); `AgentSessionOptions` holds no environment, the account's home its one per-turn value (`Lyntai/Core/Agents/AgentSessionModels.cs:10-65`, `:59-64` *(AG20)*) | yes |
| 4 | The runner yields lines, never a process | every member of `IProcessRunner` takes a command, arguments, an environment and a working directory and yields a result, lines or bytes (`Lyntai/Core/Processes/IProcessRunner.cs:23-107`); Daoris's launcher answers a `Process` (`Platform/AgentStarts.cs:30`), which a runner over it keeps | yes |
| 5 | The pool's reducer is internal; its store optional | `HoldRow`, `PoolRules`, `SeatAdmission` and `AgentPoolReducer` are internal (`Lyntai/Core/Agents/AgentPoolReducer.cs:7-27` *(AG20)*); the pool's store may be null, and two processes on one pool are out of scope (`AgentAccountPool.cs:32-34`, `:51`, `:64-67` *(AG20)*) | yes |
| 6 | D222's validation ties a usage reading to Lyntai's releases | a read probes the version and asks only a validated one (`Lyntai/docs/agent-provider-layer.md:217-224`); the set is matched exactly (`CliUsageSource.cs:28-29`, `:77-81`), claude 2.1.296 and codex-cli 0.160.0, older than the signed-out 2.1.312 and 0.163.0 captured beside them; `requireValidatedVersion` is a parameter of the internal type, left true by the registration (`CliUsageSource.cs:48-57`; `ClaudeCliBuilderExtensions.cs:102-104` *(AG20)*). An update Daoris pins reads unknown until a Lyntai release validates it | yes |
| 7 | D234's one-variable rule meets a case Daoris keeps | providers sharing a key must read the home through one variable (`Lyntai/Core/Agents/AgentAccountKeys.cs:10-31` *(AG20)*), D234's re-open (`DECISIONS.md:6744-6745` *(AG20)*); Daoris keeps a plugin door joined to `claude-code` handed its own `profileVariable` (`docs/2026-10-26-agent-provider-design.md:1141`). Whether an installed plugin declares one is unmeasured | yes |
| 8 | D22's *external* and Lyntai's D18 *first-party* mean different things | D22 calls Daoris either sibling's first external consumer, outside its repository (`D22.md:12-15`); D18 relaxes the freeze while every consumer is one of the owner's own applications and expires with an external one (`Lyntai/docs/DECISIONS.md:543-544`, `:560-561`). Daoris is first-party in D18's sense, so a disclosed break may still come in a minor | yes |
| 9 | The Claude door has no plugins option and one intent for three postures | `ClaudeAgentOptions` carries a settings file, a server file, allowed tools, skip-permissions, setting sources and strict servers, no plugins (`Lyntai/Basic/ClaudeCli/ClaudeAgentOptions.cs:6-40`), where Daoris hands `--plugin-dir` (`Agents/Contract/Ports.cs:35-36`; `Agents/Adapters.cs:2332-2340`); `ReadOnly` denies write tools, `Write` is `--permission-mode acceptEdits`, skip-all `--dangerously-skip-permissions` (`ClaudeAgentArgs.cs:67-73`, `:85-94`), where Daoris's driven run names `default`, `bypassPermissions` or `acceptEdits` (`Agents/Adapters.cs:2199-2204`). **Wider:** the door always denies `AskUserQuestion`, `ExitPlanMode` and `EnterPlanMode` (`ClaudeAgentArgs.cs:10-12`), which Daoris's native arguments do not (`Agents/Adapters.cs:2212`), and carries no effort (none in `AgentSessionModels.cs:10-65`; `--resume` and `--model` alone, `ClaudeAgentArgs.cs:138-144`), where Daoris passes `--effort` (`Roles/Domain/ActorRun.cs:59-60`) | yes, wider |
| 10 | FOUNDATION1's *j after h, i and o* names its own rows | Part B's order is `g, h, i, o, j` (`docs/2026-10-25-foundation-design.md:1679-1680`), FOUNDATION1's 1h, 1i and 1o (`:1718-1720`), not the provider design's | yes; and j's part i is now built (D212's note of 2026-10-30) |

### 0.3 Corrections to the brief, the survey and the proposals, checked

| Claim | At the commits above | Who is right |
|---|---|---|
| X §4: D234 *is a real answer*, so the shared-account ruling *is not asked again* (X §22) | written, and binding only at the owner's acceptance of the AG20 branch (§0.2 row 1); X's citations of D234 at `DECISIONS.md:6650` and D235 at `:6688` are `:6709` and `:6747` *(AG20)* | O |
| X §15: the desktop's Lyntai pin equal to the service's, *the desktop process can bring both dependency graphs together* | the desktop references no service project and reaches the service over its doors (`Desktop/Directory.Packages.props:6-11`; `Desktop/Daoris.Desktop.App/Daoris.Desktop.App.csproj:52-53`; the host located as a program of its own, `Desktop/Daoris.Desktop.Driver/Service/ServiceHostLocator.cs:87`): no desktop process loads the service's graph | O |
| X §4: the `codex` holder *no executable provider fabricated*, while its M3b reads Codex's allowance through the library | Codex's usage source is reached only through `AddCodexCliAgentSession` (§0.2 row 2) | O |
| X §8: a process-wide `AddErrorTextMatcher` *could change the service's unrelated classification* | it is process-wide (`Lyntai/Core/Inference/ProviderVerdictClassifier.cs:16`, `:32`), in the desktop's process alone; the service is another process. X's conclusion stands: a string-only matcher cannot keep a door's or a source's distinctions | X, in kind |
| O Q4: dsh's home variable *unmeasured* | Daoris declares `DSH_HOME` (`Agents/Adapters.cs:2032`); whether dsh's program reads it is what is unmeasured | — |
| O Q9, Q19: AG10 and AG18 give a child's readings | AG10 is a `ParentCallId` on `ToolCall` and `ToolResult` alone (`Lyntai/TASKS.md:301-303` *(AG20)*) and AG18 a task's start and end (`:373-375`): a child's words, its seen and ended, and the turn a task wakes are filed nowhere | X (its Q-O) |
| Both: AG19 answers Daoris's held permission request | AG19's contract ends a request unanswered within the turn's inactivity clock as a refusal (`Lyntai/TASKS.md:381-383` *(AG20)*), where Daoris holds one for the person until they answer (the brief §9); X lists the waiting as unmeasured, O not at all | neither; LQ3 states it |
| Survey §13: D234 and D235 *landing on its line after a replay* | the replay `0cbd84c1` is on its session branch, not on `master` | — |
| Survey §9 and both proposals: none of FOUNDATION1's g, j, q, r built | j's part i is built on the batch line: one start plan, `StartPasses`' seven codes, one re-ask comparison (`Roles/StartPlan.cs`; D212's note of 2026-10-30) | — |
| D213 §11 and survey §14 item 6: rows h, l, m, n, o and q land before mod-xxi is opened | spent: mod-xxi landed with the structure census at no family (`D212.md:1458`); each such row now keeps `StructureTests` at no family instead of cutting an edge | — |
| Survey §14 item 3: *rows c to w* | rows a to u with the corrections; w-s, w and x are FOUNDATION1's | the survey |

### 0.4 The inputs, folded

| Input (the brief §3) | Folded as |
|---|---|
| AGENTPROVIDER1, D213 | stands for what stays Daoris's; rows i and k not built, rows c, e, j, l and t re-cut, OPENROUTER1 re-placed (§3.1), the rest as accepted; its points 1 to 14 bind every row that stays |
| AGENTINFRA1 | its stages placed (§9.4): B in its isolated home on LQ11 and LQ12, C the layer's turn for a layer-run door, D's HTTP half on Lyntai's AG13 and AG14 and its hosted half not built, E OPENROUTER1 first; `cooling.json` never overloaded |
| FOUNDATION1, D212 | the map holds, Lyntai a library below every subdomain named by Agents alone (§6.1); g, j, q and r read Daoris's values (§9.5) |
| Lyntai's layer, its D219 | taken at a release: the descriptor and registry, the events, the failure with restriction and delivery, the usage source, the CLI doors and the generic ACP door; not its pool as the walk, its retry decorator or its brief composer (§13) |
| Lyntai's study, D233, AG10 to AG20 | each AG row Daoris needs named on the row that waits (§8); its verdicts kept where both proposals agree, each departure said in §3.1 |
| Lyntai's D18, D19, D20, D25 with D221, D70, D222, D225 to D228 | designed within; D222's validation (LQ2) and D233's long-lived run (LQ3) asked to move by requests with evidence |
| D22 | stands for what lands; a dated note at row 1a says how a pack serves as evidence (§7.3) |
| OPENROUTER1, CONNECTORS1, COAGENT1e | OPENROUTER1 placed (§9.4); CONNECTORS1's plan Daoris's, handed as the layer's server entries; COAGENT1e's sheet a read of Daoris's contract, the layer's claims read into it |
| Lyntai's D234 and D235 | the design holds under either answer of AG20 (§4.2); D235's pack is evidence, never what lands (§7.3) |

## 1. The two proposals, question by question

| Q | X | O | Chosen |
|---|---|---|---|
| 1 **Row by row** | split, the machinery Lyntai's and policy Daoris's; asks Q-F, Q-L, Q-O, Q-R, Q-K, Q-C, Q-M, Q-U | build what calls a door, never rebuild what a door does; asks Y1 to Y8 | **O's line; X's Q-L, Q-O, Q-M and Q-R beside O's requests; j a correction of its own (X)** (§3.1, §8) |
| 2 **The host's line** | floor, D125, the strip, interactive, reach, rule language, release sources Daoris's | the same, and where holds are kept, by how many processes | **Both, said once** (§3.2) |
| 3 **The contract** | an adapter, catalogue entries over library objects, all 34 placed | an adapter in `Agents/Layer/`, three rules against a copy | **Both: an adapter; O's rules with X's deletion rule** (§2.3, §4.1) |
| 4 **Provider, door, account** | the layer's provider id the door's; D234 binding, `agent:` the key; Q-K now | the same mapping; D234 not yet binding, the design under both answers; a door of a differing variable under its own id | **O's** (§4.2) |
| 5 **What is known** | `AgentAbility` for a claim, Daoris's eight-fact envelope, `Unknown` never upgraded | `Unknown` read as stated absent; a door moves where what it relies on reads `Supported` | **Both** (§4.3) |
| 6 **The run** | whole doors after Q-L | the native door split by run kind; protocol doors after Y3 | **O's split** (§5.1) |
| 7 **Accounts** | the vocabulary now; Q-R for stage B over host-owned state | the vocabulary only; stage B the layer's pool, one process | **X's** (§5.2) |
| 8 **Failures** | one classification; a missed sentence keeps the door on its old reader; Q-F | `AgentErrors.Read`, the layer's failure the door's table, Daoris's tables behind it | **O's one reading with X's door-waits rule; Q-F not asked** (§5.3) |
| 9 **Readings** | the adapter supplies Events' values; Q-O for what the AG rows lack | the same, on AG10, AG18, AG19, AG12 | **X's Q-O** (§5.4) |
| 10 **The spawn** | Daoris's runner over the launcher, eight steps, parity cases | the runner the launcher, one registration per start, Y5 | **O's per-start registration with X's steps and cases** (§5.5) |
| 11 **Models and the rest** | AG11, AG12, Q-M; Q-C for the carriers | AG11, AG12; Y4, Y7; the rendered server file decided at the move ⚖ | **O's split requests, X's Q-M, and the effort carrier both missed** (§5.6) |
| 12 **The program** | Daoris's; no library command over the pin | Daoris's; the pin handed as the command | **Both** (§5.7) |
| 13 **The census** | `Agents/Integration/`, the providers, handles in the registry; every symbol form checked | `Agents/Layer/` and the providers' folders; a token of its own | **O's places, X's check** (§6.1) |
| 14 **The suite** | a fourth view; lines keep their row; both stand-ins | a fourth view; `row` admits a Lyntai row; `alt-stub` the shape | **The fourth view; lines re-pointed to the row that now makes them pass** (§6.2) |
| 15 **The reference** | Core and Basic; a private scope; pins equal to the service's; lock files | Core and Basic; a private container per start; not equal as a gate | **O's** (§7.1) |
| 16 **Versioning** | release labels, the published version recorded at dispatch | the first release carrying the need; D18 open while first-party | **X's labels, O's reading of D18** (§7.2) |
| 17 **Unreleased** | D22 unchanged; a pack in scratch for comparison | a note on D22; a pack from a fresh clone, evidence only | **O's note within X's limits** (§7.3) |
| 18 **No feed** | a warm cache or a seeded bundle; an isolated-folder proof | the packages folder; the install started offline | **Both** (§7.4) |
| 19 **The order** | M0 to M16; OPENROUTER1 at M15 | L1 to L8; OPENROUTER1 at L4 on the layer | **O's first three rows, the second split as X's; OPENROUTER1 neither's** (§9) |
| 20 **FOUNDATION1** | g, j, q, r stand in Part B's order | the same; j after its own h, i, o | **Both** (§9.5) |
| 21 **The names** | each qualified | Daoris's sense kept, the layer's qualified | **Both** (§10) |
| 22 **The owner's** | releases, a publication, installed proofs, a sign-in, OpenRouter, the hosted direction | the same, and the AG20 branch | **O's list** (§11) |
| 23 **Proof** | per milestone, three records a landing | per row | **Both** (§12) |

### 1.1 Where they differ, and which way

| # | The difference | Chosen, and why | The case the other gets wrong |
|---|---|---|---|
| 1 | The third row: Codex's failures read through the layer, its transport kept (X's M2, on Q-F), or the account windows read through the layer (O's L3, on Y2) | **O's** (row 1c): the usage source is public (`IAgentUsageSource`, AG9) and needs only a version policy (LQ2); it proves the per-start composition, the runner's live stdin and the account mapping before any session moves. Q-F asks Lyntai to make its doors' internal readers (`Acp/AcpFailure.cs:41`, `ClaudeCli/StreamJsonFailure.cs:18`) a public surface frozen at release for a host's own transport, which D235's tiers count among what is never a promise and which no host needs once its doors run on the layer | X's: M2 waits on a seam Lyntai has no row for, then keeps it frozen after every door has moved |
| 2 | A sentence Daoris reads that the layer does not: a Daoris table behind a layer-run door (O Q8) or the door kept on its old reader until a release reads it (X §8) | **X's rule inside O's one reading**: a door moves when the layer reads every sentence Daoris has recorded for it; a sentence it lacks travels as a request and the move waits | O's: two readers of one failure on a layer-run door, against O's own §1.3 rule 2, and a table a later release must be noticed to retire |
| 3 | Claude Code's native door moved whole after Q-L (X's M6) or its driven starts first and its conversations after Y3 (O's L5, L7) | **O's** (rows 1d, 1f): the layer's Claude CLI door is one process per turn fed its prompt (`Lyntai/Basic/CliAgentLoop.cs:23`), a driven start's shape (`Ports.cs:17-18`), not a conversation's held stdin (`:20-21`) | X's: driven starts wait for the largest and least certain request |
| 4 | The protocol doors' order: Codex's first (X's M5) or dsh first (O's L6) | **O's** (row 1e): dsh is driven only, reads the spec's frames alone and uses the fewest abilities, so the long-lived run's adapter is proven where a regression reaches fewest sessions | X's: the first protocol move is on the door one of the two makers runs every day |
| 5 | D234 binding now (X §4) or written and binding at the branch's acceptance (O §0 item 1) | **O's** (§0.2 row 1) | X's: its mapping names `agent:` against a release that may not carry the member |
| 6 | A plugin door joined to a key, with a variable of its own: Q-K asked and the door kept Daoris-run (X §4) or registered under its own id with `agent:` its own (O Q4) | **O's**: the layer needs a shared key only for its pool and usage source, which Daoris takes for no such door; D234's refusal never fires, no variable or hold moves; Q-K is asked when Daoris takes a layer pool on such a key | X's: a door stays on the old seam for a case no installed plugin is known to hold, waiting on a change to D234 |
| 7 | AGENTINFRA1's stage B on the layer's pool over a Daoris store, one process (O Q7), or on Q-R, the rules over host-owned atomic state (X §7) | **X's** (LQ12): the pool picks a seat per turn (`AgentAccountPool.cs:17-20` *(AG20)*), a second walk beside D212 §2.4's one; AGENTINFRA1 §7's isolated home excludes legacy clients, not a newer build's own processes | O's: two processes on the isolated home's pool, the last writer winning (`AgentAccountPool.cs:32-34` *(AG20)*) |
| 8 | A child's and a task's readings on AG10 and AG18 (O) or Q-O beside them (X) | **X's** (LQ7; §0.3) | O's: row 1d finds a child's words, seen and ended still missing when AG10 and AG18 land |
| 9 | The open's offered models and options: not asked (O §3) or asked apart from usage (X's Q-M) | **X's** (LQ8): Daoris's offers store keeps what a protocol open negotiates (D213 point 5's `Negotiated`; row n), and AG11 reads the offered models only to refuse one (`Lyntai/TASKS.md:310-316` *(AG20)*) | O's: a protocol door's move empties the offers a person's choices read |
| 10 | The carriers: one request for posture, settings, plugins and connectors (X's Q-C) or posture and plugins apart (O's Y4, Y7) | **O's split, with two carriers neither asked**: the effort (LQ5; §0.2 row 9) and the tools the door always denies (LQ4) | X's: one request answered piecemeal, no row able to name its release; both: the driven move drops the asked effort and denies three tools today's spawn does not |
| 11 | A start's own variables: one registration per start and Y5 (O) or a runner converting the library's request (X §10) | **O's** (§0.2 row 3) | X's: the library's request carries no start's variables, so a runner shared by starts reads an ambient per-start context |
| 12 | The desktop's pin equal to the service's (X) or not a gate (O) | **O's** (§0.3) | X's: every desktop row moves the service's 3.5.3 pin and runs its knowledge suites for a graph no desktop process loads |
| 13 | Lock files committed (X §15) or none (O) | **O's**: no tree here uses them; the direct versions are exact central pins, the resolved graph recorded in row 1a's proof | X's: a tree-wide restore policy changed inside a reference row, Shenora's restore with it |
| 14 | D22 unchanged (X §17) or a dated note (O Q17) | **O's note, X's limits** (§7.3) | X's: D22 silent beside Lyntai's D235, which rejects waiting with nothing to build on (`DECISIONS.md:6765-6767` *(AG20)*), so a later row guesses which governs |
| 15 | The `codex` holder: no executable provider (X) or the layer's Codex CLI provider registered for its usage alone, never opened (O) | **O's** (§0.2 row 2) | X's: its own M3b cannot read Codex's allowance through the layer |
| 16 | The third stand-in with its adapter in one row of two parts (O's L2) or the fourth view first and `alt-stub` by its folder alone (X's M1) | **X's split** (rows 1b-i, 1b-ii): row c's proof is its diff (D213 §11 row c) | O's: the adapter's lines sit in the diff meant to show the folder was enough |
| 17 | A pending line naming a Lyntai row (O's `lyntai:AG10`) or keeping its row, the request in its why (X §14) | **Neither**: a line whose row this design re-cuts (i, j's seam, k, l's readers, e's usage readers) is re-pointed, in the re-cutting row, to the row that now makes it pass, its why naming the request | O's: the suite's row set admits ids it cannot check; X's: a line naming row k, never built, never un-pends |
| 18 | j's narrowing at each protocol door's registration (O Q11) or a correction of its own (X's M10) | **X's**: the layer's ACP door reads both vendors' extensions for every agent (`Acp/AcpAgentReader.cs:251`), today's composite, so a move keeps it and j narrows on AG15 alone | O's: what Codex is sent changes inside a move (D213 point 10) |
| 19 | Today's stand-ins kept as they are until the old seam ends (both) | **Neither**: a moved door's model-free proofs (the Process suites' and rehearsals' programs speaking its vocabulary; `acp-stub`, whose frames are the composite the layer's ACP door reads) are driven through the door's new path in its move; the native `stub`'s plain lines, which no layer door reads, stay Daoris-run until row t | both: a moved door's suites go on proving a path that no longer runs |
| 20 | OPENROUTER1 after AG10 and AG17 on the layer (O's L4) or after the door moves and stage B (X's M15) | **Neither** (§9.4): after its Daoris rows, on whichever transport Claude Code's native door runs when it lands, moving with that door, its key on the layer by AG17 | O's: a Lyntai release it does not need while Claude Code's door is Daoris-run; X's: a daily cap read as a limit with D125's cool-off (the provider design §12) waits for new restriction semantics it does not use |
| 21 | Lyntai's types in `Agents/Integration/`, the folders and the registry's handles (X §13) or in `Agents/Layer/` and the folders (O Q13) | **O's**: the registry hands its validated set to the adapter and names no layer type | X's: the one registration list's folder depends on an external package |
| 22 | The structure check by namespace import (O) or every symbol form (X) | **X's**: the structure analysis binds names to symbols (FOUNDATION1 mod-0) | O's: `global::Lyntai.Agents.AgentFailure` written in Events passes a check by imports |
| 23 | `NeverSent` read as the door states it (O) or uncertain until the write is proven (X §8) | **X's**: it means nothing came back (`Lyntai/Core/Agents/AgentFailure.cs:115-116` *(AG20)*), not that no byte left; Daoris's runner writes the stdin and knows | O's: a prompt an agent read before dying sent again, against *a word once on the wire never sent again* (the brief §9) |
| 24 | AG19's inactivity refusal (neither names it, §0.3) | **LQ3 states it**: a request waits as long as the host holds it | both: a protocol door's move refuses the person's held requests after the turn's inactivity clock |

### 1.2 What each got that the other missed

**Codex's.** A door kept on its old reader until the layer reads every sentence recorded for it (§5.3). `NeverSent` uncertain until the
write is proven (§5.3). What AG10, AG18 and AG19 leave unread, asked as Q-O (LQ7), and the open's negotiation, Q-M (LQ8). Restriction
reduction over host-owned state without the walk, Q-R (LQ12). j a correction of its own. The structure check over every symbol form. The
spawn's eight steps and their cases (a `.cmd` shim, `&` in a carrier's path, an empty against a removed variable, a plugin's home-variable
override). The third stand-in's diff its folder alone. A fake HTTP executor for AGENTINFRA1's stage D. Release labels, the published version
recorded at dispatch. A process-wide error matcher refused.

**The Opus reader's.** Ten gaps of the survey, every one holding (§0.2): the descriptors reachable only by a registration, a registration's
environment fixed, the runner yielding lines, the reducer internal, D222's coupling, D234's variable rule, D18's *first-party*, the Claude
door's postures and plugins, FOUNDATION1's own h, i and o, D234 not yet binding. The line between building what calls a door and rebuilding
what a door does (§2.2). Three rules against a copy (§2.3). The native door split by run kind. One registration per start. The `codex` holder
registered for its usage alone. The usage reading as the first part moved. The desktop's pin apart from the service's. dsh first.

## 2. The shape

### 2.1 In one paragraph

**The layer is what a door is; Daoris is what is done with doors** (O §1.1). A door in the layer's sense (a process started for a turn,
spoken to and read, with its descriptor and claims, its events, its typed failure with restriction and delivery, its usage reading and the
account home it is handed) is Lyntai's, built and released there, and Daoris writes none of it again. What a host does with doors stays
Daoris's: ids and records, the account rule, the walk and the cool-off, the start plan, the shell that completes a spawn with Daoris's
variables and the launcher that contains it, the readings and their sink, reach, the programs' pins, the rule language, connectors, plugins
and the meter. `Agents/Contract` stays Daoris's host contract and shrinks as doors move. Each door is **Daoris-run**, on today's transports
and unchanged, or **layer-run**, bound to one Lyntai provider and door and reading every fact the layer states from that provider's
descriptor. A door, or one run kind of it, becomes layer-run in one row, when a Lyntai release carries every reading and operation Daoris
uses on it today and the suite shows it case for case; a transport retires when its last door has left. The adapter is one folder,
`Agents/Layer/`; the census and `StructureTests` hold every Lyntai type there and in the providers' folders.

### 2.2 The line, and when a door moves

Rows that change **how Daoris calls a door** (d, e's host half, f, g, h's shell, l's sink, m's executors over `AgentRun`, n, o, p, q, r, u
and every correction) are needed whatever runs beneath, and they make a later move invisible to the callers: they stay Daoris's and build as
accepted (O §1.2). Rows that change **what a door does inside** (i, j's seam, k, l's four readers made one, e's usage readers and stream
words) have their counterpart in the layer and would be retired the day their door moves: they are not built (both). **A door, or a run kind
of it, moves when** (the brief §10 point 2): its release carries each reading and operation the door uses today; the suite's cases for it on
the layer's view equal its cases on the old view the commit before, its pending file not growing (§6.2); its spawn table (the resolved
program, the argument vector, every variable) equals today's but for each difference the row states as a decision note or a request (the
brief §10 point 3); and its model-free real-process proofs run through the new path (§1.1 row 19). A door that would lose a reading, an
operation or a posture does not move.

### 2.3 Not a copy

Four rules keep the adapter from being the copy D213's note rejects, each held by a test (O §1.3, X §3):

1. **No Daoris type mirrors a layer type.** Where a Daoris type already holds the fact for Daoris-run doors (`KnownBy`, `Transport`,
   `Eligibility`, `DoorFailure`), the adapter maps the layer's value into it at the boundary; nothing above `Agents/Layer/` sees the layer's.
2. **A layer-run door declares nothing the layer states**: its ability names, home variable, account key and failure tables are read from
   the bound descriptor and reader. A registration declaring one beside a binding is refused by `ProviderFilesTests`, naming the fact.
3. **One home per fact, counted**: the census counts a fact a layer-run door restates as a site with no allowance.
4. **What the layer replaces is deleted** (X §3): a door's transport readers go in the row that moves its last run kind, never kept as a
   second implementation, and a library upgrade edits `Agents/Layer/` and the providers' folders alone.

### 2.4 Where each piece sits

| Piece | Home | Named by |
|---|---|---|
| A door's descriptor, claims, turn, events, failure, restriction, delivery, usage reading, account mapping, agent key | Lyntai (`Lyntai.Agents`, `Lyntai.Processes`, `Lyntai.Providers.*`) | `Agents/Layer/` and the providers' folders alone |
| The binding, the per-start composition (a private container), `LauncherRunner`, `LayerRun`, the translations of claims, accounts, failures and events | `Agents/Layer/` (new) | Agents' registry and the executors' ports |
| A layer-run door's vendor registration (`AddClaudeCliAgentSession`, `ClaudeAgentOptions`) | `Agents/Providers/<Id>/` | `Agents/Registry/Providers.cs`, by its registration alone |
| The generic ACP registration a plugin's or dsh's door takes | `Agents/Layer/` | the plugin factory and dsh's folder |
| Ids, records, the registry, the host's parts, `Known<T>`, `AgentRun`, the census, the suite | Daoris, as today | as today |
| The account rule, the walk, the cool-off, the start plan, the shell, the launcher, the sink | Agents, Roles, Platform, Events, Loop, as today | as today |

## 3. The split (Q1, Q2)

### 3.1 Row by row

Each layer part below is asked by a quest that states the general need with the asker struck, never written into Lyntai (§8). *Sites* are the
survey's §7 behaviour counts at `e0c7b7e5b`.

| Row | Placing, and why by the generic test | The layer's part | Sites | Against the study (survey §9) |
|---|---|---|---|---|
| **c** the third stand-in | **Re-cut as row 1b.** Its folder (`provider.json`, `pending.json`, a `Program/` answering the scenario contract in its own vocabulary) is Daoris's meter; its protocol door registers on the generic ACP door, its native door is a host's own `IAgentProvider` in its folder, since no layer door can be taught a third agent's lines | AG8, built; AG15 and AG16 un-pend its own-vocabulary and stream cases | 0 | agrees; it also proves the adapter |
| **d** ⚖ the owner once | Daoris's, as accepted: which accounts a door runs as, the one-level `accountOf` and its `Lent` follow this host's catalogue | D234's `agent:` on each layer registration (§4.2) | 3, tools' 4 | agrees |
| **e** the accounts' readers | **Re-cut.** The usage question is the agent's: the layer's, at row 1c. The words a door's own stream says (limit, window, refused, error) are the layer's at that door's move, Daoris's on a Daoris-run door until then. The floor, sign-in and login question, `ClaudeTrust`, the key variable read once, `ToolHomeLeftovers`, `WeekFixed` and D179's weights are a deployment's or Daoris's files: the host's, as accepted | AG9, built; LQ2 | 53: the usage readers' deleted at 1c, the stream words' at each move, the rest into the folders (the split measured before dispatch) | departs: D222's coupling (§0.2 row 6) is LQ2 |
| **f** the program | Daoris's: a release source, a version and a place are what Lyntai's D20 refuses | none; the pinned path handed as the registration's command | 52 | agrees |
| **g** the doors as abilities | Daoris's: the callers' `Wire` tests read abilities; a layer-run door's through its binding | none | 113 | agrees |
| **h** ⚖ the spawn | Split: the shell with Daoris's variables in today's order, `ISessionGit`, the tools' `PATH`, the carriers' order and the launcher are Daoris's, the launcher also the layer's runner (Lyntai's D226); `ISpawn`'s recipe serves Daoris-run doors alone | AG17; LQ9 | 15 | agrees on AG17; adds the runner and one registration per start |
| **i** the protocol transport | **Not built**: the layer's ACP door is it; `AcpSession` stays until its last door moves | LQ3 | 6, deleted at 1e | agrees; adds LQ3 |
| **j** ⚖ each protocol door its dialect | **Re-cut as a correction after row 1e**, each protocol door registered with its own dialect set | AG15 | 6, at j | agrees |
| **k** the native transport | **Not built**: the layer's Claude CLI door; Daoris's native transport stays until 1f | AG10, AG18, LQ3, LQ7 | 5, deleted at 1f | departs: one process per turn serves a driven start, not a conversation |
| **l** the readings and the sink | **Re-cut**: `WireReading` and `IReadingSink` in Events, the sink in Loop, every payload Events' own, `ChildBindings` into `IChildVocabulary`, as accepted; the layer's events translated in `Agents/Layer/`; the four stream-json readers made one not built | AG10, AG12, AG18, AG19, LQ7 | 2 | agrees, with LQ7 |
| **m** the session's life | Daoris's, as accepted: the executors hold `AgentRun`, the native retry behind it, the protocol resume once, `IConversationHome`, `DoorInput`; at a move `LayerRun` implements `AgentRun` | LQ3 | 0 | departs: Daoris's run is D233's trigger, so it asks |
| **n** models | Daoris's, as accepted | AG11, AG12, LQ5, LQ8 | 3 | agrees, with LQ5 and LQ8 |
| **o** ⚖ permissions, skills, hooks | Daoris's, as accepted: D72's grammar handed as the settings file, the lineage hook, the trust preflight, the skills | AG19, LQ4 | 12 | departs: the study places postures nowhere |
| **p** ⚖ connectors | Daoris's, as accepted; a layer-run door handed the plan's entries as `AgentMcpServer`s | none | 4 | agrees |
| **q** ⚖ plugins' doors | Daoris's, as accepted; a plugin's protocol door on the generic ACP registration at 1e-iv, its claims fixed `Declared` | AG15, AG19 grow the registration | 3 | agrees |
| **r** the page, service, tools | Daoris's, as accepted; the layer's claims read into COAGENT1e's sheet | none | 1, service's 3 | agrees |
| **s-i, s-iii, s-iv, s-v ⚖, s-viii, s-ix on** | Daoris's corrections, as accepted | none | 0 | agrees |
| **s-ii** | Daoris's rule on Daoris's record, agreeing with Lyntai's `AgentHandover.Decide`, never called | none | 0 | agrees |
| **s-vi** ⚖ | Split: the strip Daoris's; the reads each provider declares the layer's from AG17 | AG17 | 0 | agrees |
| **s-vii** ⚖ | Daoris's; the layer's half is D234's key | none | 0 | agrees |
| **t** | **Re-cut**: the old seam's end, each transport whose doors all moved retired, the native `stub` onto a host's own provider or retired | none | 48 | agrees |
| **u** ⚖ reach | Daoris's: admission, read in the plan's walk | none | 0 | agrees |
| **OPENROUTER1** | **Re-placed** (§9.4): its key, offers, reach, maker independence and D125 Daoris's; its executor Claude Code's program at the gateway | AG17 once it runs on the layer | n/a | agrees; the HTTP door not its executor |

### 3.2 The host's line

| Fact | Holds it | Why |
|---|---|---|
| The floor of a usage read | Daoris, per provider (s-iii) | how often to pay a vendor's start is a deployment's call |
| A default cool-off for an unstated reset | Daoris: D125's sixty minutes, unchanged; never an owner action's | Lyntai's D227 holds such a hold until cleared and offers `UnknownProbeAfter` as the host's option (`AgentPoolOptions.cs:28` *(AG20)*); Daoris takes no pool as its walk |
| The variables stripped from a spawn (s-vi) | Daoris decides; the layer states what each provider reads (AG17) | which to strip is policy, what a program reads a fact |
| Interactive | Daoris, each door's declaration; dsh refused a conversation as today | the layer has no conversation a person types into |
| Reach | Daoris, in the plan's walk (row u) | admission; a working directory is not consent |
| The rule language | Daoris (D72), handed to a layer-run Claude door as its settings file | the product's grammar, no universal translation |
| Release sources and pins | Daoris (row f) | Lyntai's D20 |
| Where holds are kept, by how many processes | Daoris: `cooling.json`, several processes on one home | the layer's pool is one process (`AgentAccountPool.cs:32-34` *(AG20)*); a Daoris home is not (`docs/2026-10-25-foundation-design.md:2731`) |

## 4. The mapping (Q3, Q4, Q5)

### 4.1 `Agents/Contract` onto the layer

**An adapter, never a replacement** (both). A replacement makes the layer's descriptor the contract and drops or requests Daoris's parts; it
fails three ways: Daoris's records hold door ids and account keys by kind, compared without case, and shipped builds read them
(`Agents/Contract/ProviderId.cs:62`; `Agents/Registry/RecordIds.cs:4-61`), where the layer's ids are ordinal and a second registration
replaces the first (`Lyntai/Core/Agents/IAgentProvider.cs:133-146` *(AG20)*); the host's parts fail the generic test and Lyntai files none of
them (survey §13); and Daoris's executors hold a live run the layer has not got. §2.3 keeps it from being the rejected copy.

| Daoris types (34) | After |
|---|---|
| `ProviderId`, `DoorId`, `AccountKey` | stay. A layer-run door's Lyntai provider id is its `DoorId` as Daoris's registry spells it; its Lyntai door `cli` or `acp`; its key the descriptor's `Agent` where §4.2 sets one. The layer stores none of them |
| `Provider`, `AgentDoor` | stay, the host's records: a provider groups doors, each door bound or not. `AgentDoor` gains the binding, a `Known<LayerDoor>` naming the Lyntai provider and door; a bound door states `Spawn`, `Dialect` and its tables absent and reads the layer-named abilities and its home variable through the binding |
| `Transport`, `TransportOf` | stay: `cli` reads `Native`, `acp` `Protocol`, any other kind refused at registration until a design gives it an executor; after row g no caller decides on it; `TransportOf` goes at row t |
| `KnownBy`, `Known<T>` | stay; a layer claim read into a `Known<T>` (§4.3) |
| `Eligibility` | stays: on a layer-run protocol door it is the door's `resume`, `Unknown` and `Negotiated` (`Acp/AcpAgentDoor.cs:26-27`), settled by the turn's own answer |
| `AccountPool` | stays: a key's providers. Never Lyntai's `AgentAccountPool`, the walk and its holds (§10) |
| `AgentRun` | stays, the executors' one face. A door's run is today's until its move; then `LayerRun` implements it: over one turn per call for a driven native start (1d), over the long-lived run of LQ3 for the protocol doors (1e) and conversations (1f). An operation the layer's run cannot do is refused before a write, which a moved door never meets where today's works, by §2.2 |
| `PortRef`, `Reach`, `ProviderIdentity`, `DoorIdentity`, `ProgramFacts`, `LentPart`, `VocabularyPart` | stay, the host's (survey §13's list of what stays the host's); `PortRef` placeholders go as their rows land |
| `PoolFacts`, `AccountsPart` | stay; the key variable read from the provider's declared names once AG17 is released; the login question and sign-in the host's |
| `TablesPart`, `DoorTablesPart` | stay for Daoris-run doors alone; a layer-run door's failure is the layer's (§5.3) |
| `UsagePart`, `IUsageQuestion` | `UsagePart` holds the layer's `IAgentUsageSource` for an agent whose usage moved (1c), the floor and freshness Daoris's; `IUsageQuestion` retires there |
| `DoorHome` | stays; its variable read from the descriptor's account mapping on a layer-run door; `OwnHome`, `TrustFile` and `MustExist` Daoris's |
| `PosturesPart` | stays, in the agent's words; a layer-run door maps each onto the layer's intent and the door's options and, with LQ4, the agent's own mode; a posture with none mapped refuses the start (D213 point 6) |
| `AbilitiesPart` | stays; `Interactive` and `TakesSettings` Daoris's; `Resumes` read as the layer's `resume` |
| `ISpawn`, `IDialect`, `INativeDialect`, `IProtocolDialect` | stay for Daoris-run doors, absent on a layer-run one, retired with the last door of their kind (row t) |
| `IChildVocabulary`, `IConversationHome` | stay: the lineage hook's lines, the calls' keys and what Daoris copies between account homes are the host's; a resume token neither replaces the conversation home nor authorizes a carry (X §3) |

The registry's nine types stay: `ProviderRegistry` gains the layer view; `ResolvedId`, `AccountAgent`, `IdKind`, `RecordIdField`,
`RecordIds`; `Providers`, each layer-run provider a line; `ProviderRegistration` and `LegacyDoor` until row t.

### 4.2 Provider, door and account

Daoris's provider keeps its doors (D213 point 1). Each layer-run door is one Lyntai provider with one door (Lyntai's D225), registered under
the Daoris door's id:

| Daoris door | Its key | Its registration when it moves | Moves at |
|---|---|---|---|
| `claude-code` (native) | `claude-code` | `AddClaudeCliAgentSession(id: "claude-code", agent: "claude-code", command: the pinned program)`, door `cli`; at 1c for its usage source alone, never opened | 1c usage; 1d driven; 1f conversations |
| `claude-code-acp` | `claude-code` | `AddAcpAgentSession(id: "claude-code-acp", command: the adapter's program, accountHomeVariable: "CLAUDE_CONFIG_DIR", agent: "claude-code")` | 1e-iii |
| `codex-acp` | `codex` | `AddAcpAgentSession(id: "codex-acp", …, accountHomeVariable: "CODEX_HOME", agent: "codex")` | 1e-ii |
| the `codex` holder | `codex` | `AddCodexCliAgentSession(id: "codex", agent: "codex")` for its usage source alone, a test holding its session never opened; still no runnable door (D213 point 1) | 1c |
| `dsh` | `dsh` | `AddAcpAgentSession(id: "dsh", …, accountHomeVariable: "DSH_HOME")` (`Agents/Adapters.cs:2032`; its program's read unmeasured) | 1e-i |
| `stub`, `acp-stub` | `stub` | `acp-stub` moves with 1e-ii, its frames the composite the layer's ACP door reads; `stub`'s plain lines stay Daoris-run until row t (§1.1 row 19) | 1e-ii; t |
| `alt-stub`, `alt-acp-stub` | `alt-stub` | the native door a host's own provider in its folder; the protocol door `AddAcpAgentSession`, its own home variable, `agent: "alt-stub"` | 1b-ii |
| a plugin's harness | its `accountOf` verbatim, else its own | `AddAcpAgentSession(id: the harness, …, accountHomeVariable: its profileVariable, agent: its key)`, but `agent:` its own id where its variable differs from its key's other doors | 1e-iv |

All three built-in pairs read one variable per key (`Agents/Adapters.cs:1932`, `:2370` `CLAUDE_CONFIG_DIR`; `:2115` and
`Agents/CodexAccounts.cs:42` `CODEX_HOME`; the stand-in's `:1714`, `:1797`), so D234's rule holds for them.

**Every id a shipped record holds resolves as today.** Records store Daoris's ids alone; the layer's provider ids are the same strings,
derived and never stored, and the layer's store holds nothing Daoris reads, since no pool is taken as the walk. The 22 fields of `RecordIds`
resolve through Daoris's registry by their kind; an id a build lacks reads as an unavailable door (D213 point 8). **Case-blind against
ordinal:** Daoris resolves an id without case to its registered spelling, and only that spelling reaches the layer's registry; a stored
spelling stays as stored. **Refused against replaced:** Daoris's registry refuses a twice-registered id or door (`ProviderRegistry.cs:30-73`)
before the adapter builds the layer's registry from the set it validated, so the layer's replacement has nothing to replace; a planted pair
through the adapter is refused by Daoris, naming both.

**Under AG20's two answers.** *Yes, D234 accepted and released*: the table above; every account is handed with `Provider` the Daoris key,
and the computed exception (a door whose variable differs gets `agent:` its own id and is handed `Provider` its own id) keeps D234's refusal
from firing while Daoris's records keep that door's accounts under the shared key. *No, D225 stands*: no registration names `agent:`, and
every account is handed with `Provider` the door's own id, so the layer's refusal of another provider's account (`IAgentProvider.cs:103-114`
*(AG20)*) never fires. Either way nothing Daoris keeps changes, since Daoris keeps its walk and its cool-off, and **rows 1a to 1c need neither
answer**: the usage source accepts an account whose `Provider` is its registration's id (`CliUsageSource.cs:70-73` *(AG20)*). What waits is
only a later use of a layer pool across one agent's doors, which asks Q-K then (§8).

### 4.3 What is known

`Known<T>` stays for every fact the layer does not state, and a layer claim is read into one, never declared beside it: `Supported` reads
true with its source, `Unsupported` false, `Unknown` stated absent, whose fallback is the door's stated fallback, never an upgrade (X §5); the
source maps one to one (`AgentAbilitySource` and `KnownBy` hold the same three, survey §1), the evidence is carried verbatim, and `Applies`
names the door and the claim's `AppliesTo`, the one version the layer states. Daoris reads silence as today's behaviour (D53), the layer as
*not promised*; the two meet at the move, since a door moves only where each ability Daoris relies on reads `Supported`, or `Unknown` where
today does not rely on it (§2.2). What the descriptor cannot state stays on the Daoris door: interactive, taking settings, offers, the
postures' words, reach, the lineage hook's lines, the conversation home, and the eight-fact applicability of a measurement (COAGENT1 §3.3:
the door, the agent's and adapter's versions, the posture, the writable roots by their kind, network and loopback reach, the operating
system, the settings' digest), which Lyntai declined to file (survey §13). **A run is admitted by its own evidence** (D213 point 2):
`LayerRun` admits an operation by the bound door's claim, a negotiated one by the turn's own answer; a claim read at another opening narrows
and never admits.

## 5. The run and its parts (Q6 to Q12)

### 5.1 The run

| Daoris today | The layer today | Layer-run when |
|---|---|---|
| The native door's driven start: one process, its prompt, its stream read (`Ports.cs:17-18`) | the Claude CLI door: one process per turn, the prompt on stdin (`CliAgentLoop.cs:23`; `AgentSessionModels.cs:12-13`) | **1d**, on AG10, AG18, LQ4, LQ5, LQ6 and LQ7's CLI half |
| The protocol door: one process across a session's turns, a model and a posture set on the session, words queued, a request held for the person, background tasks, a stop that keeps the conversation (`Agents/Acp.cs:299`, `:330`, `:639`, `:2077-2091`, `:2786`, `:3105`, `:3444`) | a process per turn, steering unsupported, no mode or option set (`Acp/AcpAgentSession.cs:14-29`) | **1e**, per door, on LQ3's ACP half, AG11, AG12, AG19 with LQ3's waiting, LQ4, LQ7, LQ8 |
| The native door's conversation: stdin held open, a message and an interrupt framed (`Ports.cs:20-21`, `:52-56`) | none | **1f**, on LQ3's CLI half |
| The stand-ins | — | §4.2's table |

**LQ3 is asked now, and its trigger is Daoris's measurement** (both). Lyntai's D233 re-opens a long-lived run on a host that needs a word
taken mid-turn, with a first measurement of which doors accept one (`Lyntai/docs/DECISIONS.md:6704-6707` *(AG20)*). Daoris has built and
recorded two: Claude Code's protocol door queues words (`Acp.cs:2077-2091`) and its native door takes a framed message and an interrupt on
held stdin (`Ports.cs:52-56`). Codex's protocol adapter and dsh are unmeasured; the quest says so, and their first measurement is model-free
on frames Daoris has recorded where it has them, else an installed proof that spends, the owner's (§11). Restarting a process each turn is
rejected for a conversation (X §6): it loses measured behaviour. **Every step keeps each door's life as today**: between moves a Daoris-run
door is untouched, and each move shows start, a turn, a resume of its own conversation, a cool-off and an end through the new path, first
model-free and then on the install (§12). A door split by run kind at 1d states which kind is layer-run, and the suite holds each live case on
the kind it drives.

### 5.2 Accounts, holds and the walk

**Daoris takes the restriction vocabulary as a layer-run door's terminal reads it; not the pool as its walk, and not the reducer, which is
internal** (both, §0.2 row 5). The pool picks a seat per turn, a walk inside the layer, where D213 point 3 says a provider never walks
accounts and D212 §2.4 that no caller walks again after the plan; it is one process, a Daoris home several; it would be a second hold store
beside `cooling.json`, which shipped builds read; and its released hold buys one leased attempt (Lyntai's D227) where D125 frees the account.
D197, D205, D206 and D125 hold: the walk is the start plan's in Roles (`Roles/StartPlan.cs`), the rule `LineAccount`
(`Agents/LineAccount.cs:201`), the cool-off the roster's in `cooling.json` keyed by owner and profile (`Agents/AccountCooling.cs:10`, `:29`).
A layer-run door's limit reaches it so: the terminal's `AgentFailure` with its restriction becomes a `DoorFailure`, `AgentErrors.Read` reads
it a limit, and the roster cools the account under Daoris's key and the profile, its end the restriction's `ResetAt` or `RetryNotBefore`
where stated (`AgentFailure.cs:72-97` *(AG20)*), else D125's sixty minutes; a limit on `claude-code-acp` cools the `claude-code` account its
native door runs as, as today. An owner action (a credit refusal) is handled as today's credit case until stage B, never written into
`until` as a made-up date. A cooling entry an older build wrote reads as today: no field is added.

AGENTINFRA1's restriction against the layer's (X §7's table, member for member): the cause is `RestrictionCause` directly; account, shared
pool, model and endpoint are `RestrictionSubject` with an explicit host binding to the evidenced subject key, the layer's display scope never
parsed; `model-on-binding` is the layer's model restriction with the host's binding identity, never widened to every run of that model;
reset, retry-not-before and unknown are `RestrictionRecovery`, their provenance kept; the owner action's reason the layer lacks (LQ11), and a
clear is no proof the action happened; the id and revision of the layer's `AgentHold` (`AgentPoolModels.cs:109-111` *(AG20)*) and the host
record's revision kept apart; observed time, evidence and dialect version stay Daoris's envelope.

**Stage B** (§9.4), in AGENTINFRA1's isolated home: the layer's restriction rules over Daoris's own atomic state by LQ12, Roles still
walking, the roster's order and use policy D197's, the chosen reservation and any probe lease committed by Daoris in one step, a legacy
unknown timed limit probed after D125's sixty minutes and an owner action held; `cooling.json` untouched and a legacy entry projected as
legacy evidence. No cross-process atomicity is claimed until its two-process proof passes.

### 5.3 Failures and words

**One reading stays `AgentErrors.Read` with D210's precedence**, the door's table before the provider's, on every path (both). For a
layer-run door the door's table is the layer's: `SessionEnded.Failure` (kind, restriction, delivery, `By`) is that door's reading of its own
fields (`Lyntai/docs/agent-provider-layer.md:166-171`); the adapter turns it into a `DoorFailure` (the four kinds agree one to one,
`AgentFailure.cs:8-22`), which `AgentErrors.Read` reads, and **no Daoris table stands behind a layer-run door** (§1.1 row 2). A door moves
only when the layer reads every sentence Daoris has recorded for it; a recorded sentence the layer lacks travels as a request with the
sanitized trusted frame, the agent's version, the phase, the expected kind and a near-miss that must not classify (X §8), and the move waits
for the release that carries it. Meanwhile the four kinds stay one reading on every path, shown by a case per kind on the stand-in through
the layer and on `stub` through today's transport, each read as the same kind. No process-wide error matcher is installed.

**Delivery.** Three facts stay apart (X §8): the failure's kind decides retry, cooling or conclusion; the delivery constrains repetition;
Course's durable word receipt records what Daoris attempted and the agent acknowledged. Daoris's retry reads the layer's `AgentDelivery`:
`Taken` resumes, `Uncertain` holds, and `NeverSent` may go again only where Daoris's runner shows no line of the prompt was written to the
child's stdin; otherwise it reads as uncertain (§1.1 row 23). The layer's `RetryingAgentSession` is never registered (`LyntaiBuilder.cs:559`),
so one budget, D210's, is Daoris's.

### 5.4 Readings

Events reads nothing of the layer. `Agents/Layer/` translates a layer-run door's events into Daoris's readings and writes them to the same
sink as today's transports; FOUNDATION1's hubs and triggers mint from what the sink keeps (D213 point 5). For a given input one decoder owns
each fact, and no frame is parsed again above the adapter (X §9).

| The layer's event (`Lyntai/Core/Agents/AgentStreamEvent.cs:7-86` *(AG20)*) | Daoris's reading, and its owner |
|---|---|
| `SessionStarted` | the conversation named; Participants keeps the binding, Course is told; never a second `part.opened` |
| `TextDelta`, `Thinking` | the agent's words and thinking, Context's transcript |
| `ToolCall`, `ToolResult` by call id | a tool call and its result; a child's once LQ7 carries its identity |
| `PlanUpdated` | the session's plan, never replaced by a child's |
| `UsageLive`, `UsageFinal` with its model, nullable `UsageReading` | usage with its unit, scope and time, an absent count unknown; the model reported, never filled from the request |
| `SessionEnded` with its failure | the turn's end, the executor deciding whether the part ended; a failure through `DoorFailure` (§5.3) |

What the layer lacks waits for its rows: a child seen, said and ended (AG10 with LQ7), a background task and the turn it wakes (AG18 with
LQ7), a held request (AG19 with LQ3), a refused model (AG12), an unread frame kept bounded (LQ7), replay told from live (LQ7). Until each
lands, Daoris keeps reading every door that needs it on today's transports, because no such door moves (§2.2). A replay stays history, an
inferred child closure stays inferred, computed context stays computed.

### 5.5 The spawn and containment

**The runner is the launcher** (both). `Agents/Layer/LauncherRunner` implements every `IProcessRunner` member a layer door uses:
`StreamLinesAsync` and `ExchangeLinesAsync` (the ACP door's and the usage reading's live stdin, Lyntai's D226) start through Platform's
launcher (`Platform/AgentStarts.cs:18`), which contains the process as it contains every agent (D201); `RunAsync` serves probes;
`StreamBytesAsync` keeps its refusing default; `CommandExists` answers from Install's toolchain, never the optimistic default. No fallback to
Lyntai's `ProcessRunner` exists. The runner keeps the `Process` the launcher answers and records each stdin write (§5.3). Platform stays
unaware of Lyntai: the Lyntai-facing type sits in Agents, above Platform's.

**One registration per start** (O; §0.2 row 3): the shell composes what it composes today before the layer door is registered, and the
private container serving that start is built from the result, until LQ9 lets a turn carry its own environment. **Today's order** (D213
point 3; X §10's steps): the common shell, inherited role and target identity removed; the start's Daoris identity and composed target (a
chat clearing an inherited target and setting none); the tools and their `PATH` before the program is resolved; the git wiring and the
tokens' clear through `IChildGit` and `ISessionGit`, the numbered `GIT_CONFIG_*` in today's append order; the selected account's permitted
key or home and the door's overrides in their order; the program resolved and handed as the registration's `command`; the door's carriers
(its settings, servers and plugins) after it; the launch through Platform's gate and containment. A null environment value removes an
inherited variable (`IProcessRunner.cs:8-11`), an empty one does not. **s-vi's strip** reads Daoris's `VocabularyPart` until AG17, then each
provider's declared reads with the measured ones, and strips only a variable another provider registers that the selected provider neither
declares nor is measured to read, the selected provider's own key kept after the tokens' clear. **The proof** at each move is a table of the
resolved program, its argument vector and its complete environment against today's, with a `.cmd` shim, a carrier path holding `&`, an empty
against a removed variable and a plugin's home-variable override among its cases.

### 5.6 Models, postures, rules, skills, connectors and plugins

- **Models.** D205's offers per door, version and account stay in Agents' offers store, asked, carried, acknowledged and reported apart.
  The model is carried on both CLI doors (`AgentSessionModels.cs:33-39` *(AG20)*), on the ACP door from AG11; a refused one reads with its
  restriction from AG12; the offers the open negotiates reach the store by LQ8. The native door's refusal goes on to the next start as today;
  the protocol door's re-ask on the same session before any prompt, its refused attempt's changes set back, needs LQ3 with LQ8, so protocol
  doors wait for both. **The effort** a start asks (`--effort`, `Roles/Domain/ActorRun.cs:59-60`) has no carrier in the layer's options
  (§0.2 row 9): LQ5, and 1d waits for it.
- **Postures** stay in the agent's words, mapped onto the layer's intent and the door's options and, with LQ4, the agent's own permission
  mode; the tools the Claude door always denies (`ClaudeAgentArgs.cs:10-12`) are the host's to state under LQ4. ⚖: the mapping hands the agent
  exactly today's posture and today's tools, a posture with none mapped refusing the start, so a move never widens; where Lyntai declines LQ4,
  the narrowing is a correction row of its own, never decided inside 1d.
- **D72's grammar and the lineage hook** stay Claude Code's provider's, in Daoris (row o), handed to a layer-run Claude door as its
  settings file (`ClaudeAgentOptions.SettingsPath`).
- **Connectors.** `ConnectorPlan` and CONNECTORS1 stay Daoris's: Knowledge supplies neutral entries, the plan holds the resolved servers,
  each entry's identity, environment and exclusions; a layer-run door is handed them as `AgentMcpServer`s, the session's identity in each,
  and refuses one it cannot render before a spawn, which keeps D213 point 6's refusal. The Claude door renders the entries into a file it
  deletes after the turn, where today's helpers write theirs under the home; 1d's spawn table decides between handing Daoris's own file
  (`McpConfigPath`) and the layer's rendering, ⚖. D70's native adopted-repository path, its servers inheriting the environment, is kept.
- **Skills and plugins** stay the host's (`ISkillLayout`, the catalogue); a native door's plugins need LQ6. A plugin's declared door runs at
  1e-iv on the generic ACP registration, whose claims a host cannot widen; its manifest stays Daoris's, never passed whole.
- **The ⚖ rows.** D213's d, h, j, o, p, q, u, s-v, s-vi and s-vii stay ⚖ and build after both makers' reads; rows 1c to 1f are ⚖ (§9);
  FOUNDATION1's q and r stay ⚖.

### 5.7 The program

Row f stays Daoris's: release sources, pins, signed downloads and updates in Install. Each agent's pin reaches a layer door as the
registration's `command`, the pinned program's path, which outranks the layer's own command seams (an override variable, then `PATH`;
`ClaudeAgentSession.cs:18`), so they are never read. A native program stays pinned by its provider's id and an ACP adapter by its door's
(`RecordIds.cs:49`). D222's coupling (§0.2 row 6) is met at 1c: it does not land until LQ2 is answered, or an agent's update would blank the
windows a person reads.

## 6. The meter (Q13, Q14)

### 6.1 The census and the boundary

- **A site that leaves Daoris** lowers its row's count in the same commit, with its destination recorded; a row at zero is removed; a
  partly moved row keeps its exact remainder (X §13). A site the layer now implements is deleted, never exempted.
- **What a Daoris file may name of the layer:** its neutral namespaces (`Lyntai.Agents`, `Lyntai.Processes`) and the generic ACP registration
  in `Agents/Layer/` and the providers' folders alone; a vendor's registration and options (`AddClaudeCliAgentSession`, `ClaudeAgentOptions`)
  in that vendor's folder alone; the layer's own default provider ids (`claude-cli`, `codex-cli`) nowhere, since Daoris registers under its
  own; a vendor variable a registration passes in its provider's folder, as the census confines an agent's vocabulary today.
- **The layer's types are a token of their own** in the desktop's section of `provider-census.json`, counted by name in the source text as
  the census reads today (D213's note of row a, part 2), the two places permanent exceptions under this design's decision. The service's
  section does not count it, since the service's own use of Lyntai is out of scope (the brief §6); a twin the change reaches changes in the
  same commit (`.claude/knowledge/twins.md`).
- **`StructureTests` and the map.** D212's map is kept: Lyntai is a library below every subdomain, named by Agents alone, so no subdomain
  gains an edge and a disclosed break reaches two places. The structure analysis already binds every name to its symbol; it gains a rule that
  a symbol of a `Lyntai` namespace in a signature, an alias, a generic argument, a qualified name or an import outside `Agents/Layer/` and the
  providers' folders fails, and a vendor type outside its folder fails (X §13). **Planted**, each failing: `using Lyntai.Agents;` in a Loop
  file; `global::Lyntai.Agents.AgentFailure` in Events; `AddAcpAgentSession` in `Agents/Registry/`; `ClaudeAgentOptions` in Codex's folder;
  `claude-cli` in a folder; a layer-run registration declaring a home variable beside its binding.
- **Each row of D213's that was to land before mod-xxi** keeps `StructureTests` at no family (§0.3).

### 6.2 The conformance suite

- **Row 1a:** unchanged, three views, every pending file byte for byte the commit before's.
- **From 1b-i:** a fourth view, *the layer*: every layer-run provider in `Providers.Built()`, presented through Daoris's registry over
  `Agents/Layer/`, the tests' own provider first and `alt-stub` from 1b-ii. The 60 cases run on it as on the others: the 26 mandatory by the
  adapter's mapping (ids, parts, records, postures, the spawn table through the runner, the pool's agreement and `Lent`), the 14 abilities read
  through the binding, the 20 live by the stand-in's program through the layer's door and Daoris's runner. X §14's table accounts for each of
  the 60 by name; read its milestones as this design's rows (M1 1b, M3 d and 1c, M4 f, M5 to M9 1d to 1f, M10 j, s-v and s-vi, M11 r, M12 u,
  M13 stage B, M14 AGENTINFRA1's D, M15 OPENROUTER1, M16 t), and its M2 lines as each door's move.
- **A provider joins the fourth view in the row that moves it.** Since a pending file has no view and a case must read alike in every view of
  one provider (D213's note of row b, part 2), joining proves parity by the suite's own rule: a case that passes on the old view and fails on
  the layer's cannot be held pending, since the held line would then pass on the old view and fail its row. The row also compares the door's
  layer-view cases with its old-view cases the commit before; its pending file does not grow.
- **Held pending, by name:** a line's `row` is a row of D213's §11 or of this design; its why names the Lyntai request or row that the row
  waits on. A line whose row this design re-cuts (i, j's seam, k, l's readers, e's usage readers) is re-pointed, in the re-cutting row, to
  the row that now makes it pass (§1.1 row 17).
- **Lyntai's theory holds** every ability stated once, an unknown id naming what exists, a malformed descriptor refused, another
  provider's account and an unhonourable option refused before a spawn, an account's home reaching the child's variable
  (`Lyntai/tests/Lyntai.Tests/Agents/AgentProviderConformanceTests.cs:116-375`), and AG16 would hold the stream's rules. Daoris's suite does not
  prove those again; on the layer view it proves the adapter (each claim read equal into Daoris's part, a refusal before a spawn surfacing as
  Daoris's `unsupported` naming the ability) and every case only Daoris can (ids by kind, `Lent`, records, reach, Daoris's variables,
  admission, interactive, applicability, the live cases end to end through Daoris's executors).
- **The stand-in that proves the shape** is Daoris's `alt-stub`, its scenario contract and vocabulary its own; Lyntai's `acp-stub.mjs` stays
  Lyntai's test of its own seam. Neither substitutes for the other (X §14).

## 7. The package (Q15 to Q18)

### 7.1 The reference

`Lyntai.Core` and `Lyntai.Providers.Basic`, not the metapackage, which brings `Lyntai.Storage.Basic` and `Lyntai.Tools.Mcp` with
`ModelContextProtocol.Core` (survey §11), none of which the driver uses (both). The driver alone takes them, and within it the Agents subdomain
alone (§6.1). The layer's types are constructed where their constructors are public (`AgentProvider`, `AgentProviderRegistry`); the built-in
doors and their usage sources, internal (§0.2 row 2), are composed through a private `ServiceCollection` per start inside `Agents/Layer/`,
Daoris's runner registered first, which adds the container package to the driver beside the abstractions `Lyntai.Core` brings; no container
appears anywhere else in the driver, and none crosses to a higher subdomain. When LQ10's factories are released the container goes. The pin
lives in `Desktop/Directory.Packages.props`, its comment naming D22 and this design, `Lyntai.Core` and `Lyntai.Providers.Basic` at one exact
release. **Not kept equal to the service's pin as a gate**: no desktop process loads the service's graph (§0.3); the service follows a release
on its own when its knowledge-search suites pass, so one changelog is read for both. The desktop's SQLite pair stays the service's (FOUNDATION1
z-i); a Lyntai release moves it only through a package that pins SQLite (`Lyntai.Storage.Sqlite`), which neither tree takes. **Transitive
versions** against Shenora 0.24.0 and SQLite 10.0.12 and 3.0.5: unmeasured, measured on a pack of the named commit before LQ1 is sent (§7.3),
and again at the release in row 1a: the resolved graph before and after, duplicate and downgrade warnings, the publish layout.

### 7.2 Versioning

Each row waits on a release label, never a guessed version (X §16): **R-a** (AG1 to AG9, with D234 if accepted: rows 1a, 1b), **R-c** (LQ2:
1c), **R-d** (AG10, AG18, LQ4, LQ5, LQ6, LQ7's CLI half: 1d), **R-e** (LQ3's ACP half with its waiting, AG11, AG12, AG19, LQ4's ACP half,
LQ7's ACP half, LQ8: 1e), **R-f** (LQ3's CLI half: 1f), **R-j** (AG15: j), **R-OR** (AG17: OPENROUTER1 once on the layer), **R-B** (LQ11,
LQ12: stage B). Fewer releases may carry them; at dispatch each row records the published version satisfying its label, and until one exists
the row is not dispatched. **Taking a release** follows D22's note of SHEN3: the changelog read at the release's tag for every version skipped,
the compare read for the files Daoris composes (`Lyntai/Core/Agents/`, `Lyntai/Core/Processes/IProcessRunner.cs`,
`Lyntai/Basic/{ClaudeCli,CodexCli,Acp}/`) and the two `ApiSurface` baselines, a note on D22 per release taken. **A disclosed break in a minor**
(Lyntai's D18, open while every consumer is first-party, §0.2 row 8) is met in `Agents/Layer/` and the folders alone, the taking row saying
what changed; exact pins mean a break arrives only with a row that takes it; a storage break never gets D18's relaxation. **The freeze and the
order:** a release freezes what it carries (Lyntai's D70; D235's tiers); Daoris builds each row only on released public members, first on the
tier no open row touches, a member a row grows additively read as absent until its release, and reaches the uncertain parts last, after it has
used the certain ones (descriptor, events, failure, usage, the ACP door), as Lyntai's layer §4 asks; packed-build evidence (§7.3) lets Lyntai
change a member before a release freezes it.

### 7.3 Building against changes not yet released

**D22 stands for what lands** (both). Main, every merged branch, the merge tool's gates and the CI beside build released versions alone, from
nuget.org or the machine's packages folder, with no Lyntai checkout, pack override or local feed. **Row 1a appends a dated note to D22**
(O's note within X's limits): a pack of a named public Lyntai commit (Lyntai's D235 recipe: `dotnet pack` into an untracked folder under a
version of its own, the global cache's copy evicted) may serve as **evidence for a request** before a release, made from a fresh clone under
this repository's ignored `local/`, never in a Lyntai checkout another session works in, its version override read from an untracked file
the tracked props import only when present, so no tracked file names the folder or the version; **no merged branch, no dispatched row and no
gate builds on one**, and a build that falls back from a missing release to a pack is refused. While a Lyntai row is merged and unreleased, a
Daoris branch holds nothing that compiles only against it: a row is handed back working and merged (D187), so it is not dispatched before
its release. Making such a pack publishes nothing and is not the owner's. **Under the answers foreseeable:** a release carrying the need: pin
it; a pack only: evidence, the row waits; a shared prerelease feed (D235's re-open): a separate amendment of D22, never adopted by default;
a capability declined: the working path kept, the unmet request recorded, the boundary re-opened, the mechanism never rebuilt here.

### 7.4 No feed reachable

A build restores from the machine's global packages folder when the pinned versions are there, so a machine that has built once builds
offline; a clean machine's first build needs nuget.org, as Shenora's does today, and nothing of Lyntai is kept in the repository. **Row 1a
proves it** (X's proof, O's install): a restore into an isolated empty packages folder from a local feed holding exactly the resolved closure,
remote sources cleared; and the published app started with package feeds unreachable, running a stand-in session. The installed app restores
nothing at run time: the publish copies `Lyntai.Core`, `Lyntai.Providers.Basic` and their dependencies, listed in the publish layout. A
person with no network keeps working: the layer spawns only what the host deployed and fetches nothing (Lyntai's D20); a usage read starts
the vendor's own server, which needs the vendor's network as today's does.

## 8. The requests to Lyntai

Each travels as a quest stating the need and its evidence, never the change, published once this design is accepted, its receipt recorded on
the Daoris row that waits; nothing is written into Lyntai. A sentence Daoris has recorded and the layer does not read travels as its own
request (§5.3).

| Request | The general need, the asker struck | Evidence Daoris sends | Answered by | Waits on it |
|---|---|---|---|---|
| **LQ1** | A host that takes the agent layer as a package needs a release that carries it. | the layer under *Unreleased* (survey §0, §11); the resolved closure of a pack (§7.1) | a release of AG1 to AG9, D234 with it if accepted | 1a, 1b, 1c |
| **LQ2** | An application that keeps an agent at a version it chooses needs the usage reading at that version, or a statement it can act on, without waiting for a library release; an unknown stays unknown on any failure. | §0.2 row 6; Daoris's pins (row f) and its recorded answers (`Ports.cs:78-79`) | a design of Lyntai's | 1c |
| **LQ3** | An application that converses with an agent needs a run kept across turns: a word taken while a turn runs, a stop that keeps the conversation, a model re-asked on the same session, background work read until it ends, and a permission request that waits for a person as long as the host holds it, the wait never read as the agent's inactivity. | Claude Code's protocol queueing and native held stdin, built and recorded (§5.1); AG19's inactivity refusal (`Lyntai/TASKS.md:381-383` *(AG20)*); Codex's adapter and dsh unmeasured | Lyntai's D233 option made rows | 1e (ACP half), 1f (CLI half) |
| **LQ4** | An application that lets a person choose how freely an agent acts needs the door to carry the agent's own permission mode and the tools it denies, or refuse what it cannot. | Daoris's three postures (`ProviderParts.cs:119-120`) and driven modes (`Adapters.cs:2199-2204`); the door's two intents and three always-denied tools (`ClaudeAgentArgs.cs:10-12`, `:85-94`); no mode set by the ACP door (survey §8) | new | 1d, 1e |
| **LQ5** | An application that sets how hard an agent thinks needs the door to carry the effort a turn asks, or refuse it. | `Roles/Domain/ActorRun.cs:59-60`; `AgentChoice.Effort` carried by no door (`AgentPoolModels.cs:17` *(AG20)*) | new | 1d |
| **LQ6** | An application that composes plugins for a CLI agent's session needs the door to hand them, or refuse. | `Ports.cs:35-36`; `ClaudeAgentOptions.cs:6-40` | new | 1d |
| **LQ7** | A consumer keeping a transcript and a lineage needs, without re-reading vendor frames, a child's identity, words, start and end, a task's life and the turn it wakes, a replay told from live, and an unread frame kept as a bounded diagnostic. | `Agents/ClaudeStreams.cs:73`, `:99`, `:137`; AG10's and AG18's contracts (`Lyntai/TASKS.md:301-303`, `:373-375` *(AG20)*) | AG10, AG18 widened, or new | 1d, 1e |
| **LQ8** | A consumer offering models needs the open's offered models and options, what it accepted and what it refused, apart from reported usage. | D213 point 5's `Negotiated`; `Agents/AgentOffers.cs:41`, `:86`; AG11 (`Lyntai/TASKS.md:310-316` *(AG20)*) | AG11 widened, or new | 1e |
| **LQ9** | An application that starts each turn with variables of its own needs a turn's environment, applied as a registration's is, a null removing one. | D213 point 6; §0.2 row 3 | new, or AG17 widened | none (one registration per start meanwhile) |
| **LQ10** | An application that composes without a container needs each built-in door's provider, its claims and usage source included, constructible directly. | §0.2 row 2; the driver takes no container (survey §11) | new | none (a private container meanwhile) |
| **LQ11** | An application that tells a person what to do about a hold needs the owner action's reason: refill, sign in, grant a permission, change a configuration. | `docs/2026-10-27-provider-infrastructure-design.md:126-130` | new | stage B |
| **LQ12** | An application whose own planner selects, and which runs several processes on one store, needs the restriction rules without the walk: applicable holds evaluated, a revision-checked release and one probe lease committed through its own atomic state. | §0.2 row 5; D212 §2.4's one walk; several processes on one home (`docs/2026-10-25-foundation-design.md:2731`); the pool's last writer winning (`AgentAccountPool.cs:32-34` *(AG20)*) | new | stage B |
| filed | AG15, AG16 | the study's | filed | 1b's own-vocabulary cases; j |
| filed | AG17 | the study's | filed | s-vi's declared reads; OPENROUTER1 on the layer |
| filed | AG13, AG14 | the study's | filed | AGENTINFRA1's stage D |

**Not asked now, each with its trigger** (§13 gives why): Q-F, a door's failure reader usable without its transport (no trigger: failures
move with their door); Q-K, a variable per door on one account (when Daoris takes a layer pool on a key one of whose doors reads another
variable); a pool across processes (D219's re-open, when shared-home activation is designed); a CLI door a host can teach a third agent's lines
(when a third native agent is chosen); provenance on every reading (as the study has it). Daoris reports LQ4, LQ5, LQ6, AG10 and AG18 as
blocking 1d, which moves them in Lyntai's build order (D235; survey §13).

## 9. The order and the rows (Q19, Q20)

### 9.1 In order

1. **1a**, the reference, on R-a; **1b-i** and **1b-ii**, the adapter and the third stand-in, on R-a.
2. **1c** ⚖, the account windows, on R-a and R-c.
3. **Beside them, needing no release:** D213's host rows (d, e's host half, f, g, h, l's sink, m, n, o, p, q, r, the corrections, u) in
   D213 §11's order by part, interleaved with FOUNDATION1's Part B as each names (§9.5), each on files no open branch holds; u after
   FOUNDATION1j's parts and d.
4. **OPENROUTER1**, by its own design, after d, e's host half, f, h, l's sink, n and u (§9.4).
5. **1d** ⚖, Claude Code's driven sessions, on R-d (and R-OR if OPENROUTER1 runs on Claude Code's native door by then).
6. **1e** ⚖, the protocol doors, on R-e: 1e-i dsh, 1e-ii `codex-acp` with `acp-stub`, 1e-iii `claude-code-acp`, 1e-iv a plugin's declared door.
7. **1f** ⚖, Claude Code's conversations, on R-f.
8. **j** ⚖, each protocol door its own dialect, on R-j after 1e.
9. **t**, the old seam's end, after every door has moved.

AGENTINFRA1's stage B lands on R-B whenever its own row is ready (§9.4); the corrections s-i to s-ix each after the move that makes it one
place, as D213 §11 orders them.

### 9.2 The first three rows

**AGENTLYNTAI1a. The desktop builds on Lyntai's released agent layer, and nothing changes.** *Not ⚖*: nothing an agent is handed changes.
- **Code.** `Desktop/Directory.Packages.props` pins `Lyntai.Core` and `Lyntai.Providers.Basic` at R-a's release; the driver references both;
  `StructureTests` gains the symbol rule and the desktop census its token at zero (§6.1); D22 its note (§7.3).
- **What a person sees.** Nothing. **Why a row of its own:** the closure, the boundary and a build with and without a feed are shown before
  any reader depends on them (the brief §10 step 1).
- **Proof.** §12's 1a line. **Waits for** LQ1's release (R-a).

**AGENTLYNTAI1b. A third stand-in runs through the layer, and the suite reads it in a fourth view.** *Not ⚖*: no real agent's handing
changes. D213's row c, re-cut. One feature, two branches:
- **1b-i, the adapter and the fourth view.** `Agents/Layer/` (the per-start composition, `LauncherRunner`, `LayerRun` over one turn per call,
  the translations of claims, accounts, failures and events), the registry's layer view, the suite's fourth view and the re-pointed pending
  rule, proven on a provider the tests define, never production.
- **1b-ii, `alt-stub` by its folder.** `Agents/Providers/AltStub/` (`provider.json`, `pending.json`, a `Program/` entry answering the scenario
  contract in its own vocabulary, its native door's host provider, its protocol door's registration) and one line in `Providers.Built()`;
  **the diff its proof**, a change outside the folder a gap named.
- **What a person sees.** Nothing: a stand-in is listed nowhere without a configured command. **Why a row of its own:** it is the adapter
  every move stands on and the meter that shows each door's gap before the door moves (the brief §8).
- **Proof.** §12's 1b lines. **Waits for** R-a; its own-vocabulary cases on the protocol door pend on AG15, its stream rules on AG16.

**AGENTLYNTAI1c ⚖. Claude Code's and Codex's account windows are read through the layer, as before.** ⚖ since it reroutes how an
account's home reaches the vendor's server.
- **Code.** Claude Code's and Codex's providers register the layer's CLI providers for their usage source (§4.2), a test holding their
  sessions never opened; `UsagePart` holds the layer's `IAgentUsageSource`, the floor and freshness Daoris's; `ClaudeUsage`, `CodexUsage` and
  the usage switch (`HarnessRoster.cs:2295-2368`) deleted; the reading's windows mapped into today's; the read spawned through
  `LauncherRunner`'s live stdin; row e's pending lines for the usage readers re-pointed here.
- **What a person sees.** The Accounts screen's windows on both agents as before; the screenshot is that screen with a signed-in account of
  each, in English and Chinese.
- **Proof.** §12's 1c line. **Waits for** R-a and LQ2's answer (R-c); neither of AG20's answers.

### 9.3 The rest

- **AGENTLYNTAI1d ⚖. Claude Code's driven sessions run through the layer and read, resume, cool and end as before.** The `claude-code`
  door bound for its driven start, its conversations Daoris-run; `stub`'s plain-lines door untouched; the Process suites' and rehearsals'
  Claude-vocabulary programs driven through the layer's door; the spawn table's differences stated (the prompt on stdin, the layer's rule;
  the denied tools by LQ4 or a correction of its own; the server file); `ClaudeStreams` and `StructuredOutput` stay for the conversation
  until 1f. ⚖ since it reroutes how an account's home, a posture, the rules and the servers reach Claude Code. On R-d.
- **AGENTLYNTAI1e ⚖. Each protocol door's sessions run through the layer as before**, one row per door: **1e-i** dsh; **1e-ii** `codex-acp`,
  with `acp-stub`; **1e-iii** `claude-code-acp`, its limits still cooling the native door's account; **1e-iv** a plugin's declared door on the
  generic ACP registration. `AcpSession` goes with the last of them, row i's six sites with it. ⚖ since each reroutes an account's home, a
  posture, the servers and a held request's answer. On R-e.
- **AGENTLYNTAI1f ⚖. Claude Code's conversations run through the layer as before**; the native transport goes, row k's five sites with it.
  ⚖ as 1d. On R-f.
- **j ⚖ (D213's, re-cut). Each protocol door reads its own agent's extensions**, registered with its dialect set on AG15, after 1e; its
  accepted proof, Codex sent nothing it does not read among it.
- **t (D213's, re-cut). The old seam ends and every record still reads**: `ISessionAdapter`, `HarnessToolchain`, `SessionWire`, `AdapterSet`,
  the shim, each retired transport and `provider-vocabulary.json` gone; the native `stub` moved onto a host's own provider as `alt-stub`'s
  native door is, or retired with the rehearsal phases that read it, said in the row.

### 9.4 OPENROUTER1 and AGENTINFRA1

**OPENROUTER1** is its own design (the brief §6), placed here. **Its executor is Claude Code's program at the gateway** (both), on whichever
transport Claude Code's native door runs when it lands: on Daoris's, if before 1d, its door moving with Claude Code's at 1d as one more
registration of the same program, the key on the layer then by AG17; on the layer's, if after. It waits for its Daoris rows (d, e's host
half, f, h, l's sink, n and u; D213's i and k re-cut away), never for a Lyntai release by itself (§1.1 row 20). Its key is kept where D172
point 3 keeps keys and handed to its own spawn alone; its offers its own; its reach the repositories opened to it, read in the plan's walk;
its gateway no proof of another maker; a daily cap a limit with D125's cool-off and a credit refusal handled as today's credit case. **The HTTP
door on Lyntai's D233 is not its executor** (both): it runs Lyntai's tool loop with no workspace, which the host keeps, so driving a quest in a
repository through it would make Daoris build the executor that owns the repository, its tools and its permissions; an inference-only use
(a voice whose answer is its text) may take the HTTP door later, by OPENROUTER1's own design.

**AGENTINFRA1's stages:** **A** is settled. **B**, availability normalized, in the isolated home: the layer's restriction vocabulary at each
layer-run door's terminal, the rules over Daoris's own atomic state by LQ12, the owner action's reason by LQ11, Daoris's walk unchanged,
`cooling.json` untouched (§5.2); its proof the infrastructure design's §8 B line, one probe lease under two processes and an old client's
access deliberately attempted among it. **C**, execution decoupled: the layer's turn is the execution-neutral run for a layer-run door, its
alternative-dialect stand-in `alt-stub` (1b), an inference-only integration refused for execution before a send. **D**, its HTTP half: a fake
HTTP executor through the layer's AG14 door with fake tools and no keys, proving request disposition, a lost acknowledgement, a duplicate
receipt, cancellation and restart reconciliation (X's M14); its hosted half not built, since Lyntai's D233 keeps a hosted agent out and
building one here would be a door Daoris writes, a direction for the owner (§11). **E**, each real integration, OPENROUTER1 first.
Shared-home remote activation stays unavailable.

### 9.5 FOUNDATION1's rows g, j, q and r

| Row | What it touches of the provider layer | Reads | Sequencing |
|---|---|---|---|
| **1g** failures read once on every path (`docs/2026-10-25-foundation-design.md:1717`) | a conversation's and a brainstorm's limits by the reader's code; a layer-run door's failure enters as a `DoorFailure` (§5.3), so one reader covers both kinds of door | Daoris's (`DoorFailure`, `AgentErrorKind`) | Part B's order, no release; before 1b-i, 1b-i's translation proven into it; after, its proof includes the stand-in through the layer |
| **1j** one start plan and one walk (`:1721`, §2.4) | the settled selection, the account walk inside each choice, the reservation | Daoris's registry, account rule and offers store, never the layer's pool | part i built (`Roles/StartPlan.cs`); parts ii (the reviewer's, helper's and partner's walks) and iii (the chat's) in Part B's order after FOUNDATION1's own 1h, 1i, 1o; row u after it, adding `not-opened` to `StartPasses`; `PickAsync` rejected as the walk: a seat per turn in one process, a second walk, no reservation across processes |
| **1q** ⚖ start admission by the work (`:1734`) | nothing of a provider: the start layer is keyed by the work; reach is Roles' walk's, never an upward call | Daoris's | near Part B's end, before r; no release |
| **1r** ⚖ the connector by work (`:1735`) | the session's identity every plan entry carries, a layer-run door carrying it in each `AgentMcpServer`; a descriptor is no caller authority | Daoris's call layer | near the end; its ACP child measurement (FABRIC-ACP1) made on today's transports while those doors are Daoris-run, through the layer after 1e |

Part B's own order stands (`docs/2026-10-25-foundation-design.md:1679-1680`); the provider migration neither makes j wait on the role
catalogue nor pulls q and r forward.

## 10. The names (Q21)

| Word | Daoris | Lyntai | Kept as |
|---|---|---|---|
| provider | an agent with one or more doors (`Provider.cs:28`) | a registration, one door per built-in (D225) | Daoris's sense in its code and prose; the other *a layer provider*, named only in `Agents/Layer/`, the folders and their prose; never on the window (D213 point 11) |
| door | a way in, keeping today's adapter id | `AgentDoor` inside a provider (`cli`, `acp`) | a Daoris door is one layer provider's one door |
| agent | the product a person picks (智能体 on the window) | `AgentProviderDescriptor.Agent`, the key several providers share (D234) | the window's word unchanged; in code the layer's `Agent` read only as the account key, never as a maker or a review's independence |
| account | a profile, keyed by owner and profile | `AgentAccount`: a key, a name, a home | one thing; the profile is the layer's name |
| pool | `AccountPool`, a key's providers | `AgentAccountPool`, the walk and its holds | Daoris's sense stays; the other *the layer's pool* |
| hold | a cool-off, *cooling* on the window; a person's hold on a repository or session | `AgentHold`, a restriction held with its revision | the window keeps its cooling words; *hold* in code only for the layer's |
| run | `AgentRun`, a live run across turns | none: a turn is one `StreamAsync`, whose end is not the conversation's | Daoris's; `LayerRun` a run made of the layer's turns |

The window keeps *agent* (智能体) and *way in* (接入方式), the product names as written; the glossary gains nothing, since the layer never
reaches the window.

## 11. What is the owner's (Q22)

1. **Each Lyntai release a row waits on.** What: a publication in Lyntai, started by its release workflow (Lyntai's D19), freezing what it
   carries (Lyntai's D70, softened by D18 while every consumer is first-party). Yes: the rows that wait on it can land. Where: §7.2's labels
   and §8; the first, R-a, lets 1a and 1b land.
2. **The acceptance of Lyntai's AG20 branch**, which makes D234 and D235 binding there. What: Lyntai's own ruling, in Lyntai. Yes: the layer
   keys one account across an agent's doors and a release may carry `Agent`. Where: §0.2 row 1, §4.2; no Daoris row waits for it.
3. **Installed proofs that spend.** What: 1c's one usage read per agent on a named account (the vendor's server under the account's home, no
   model run); 1d's one driven session and one resume; 1e's one session and one resume per door; 1f's one conversation and one resume; j's
   and t's as D213's rows have them; LQ3's measurement of Codex's adapter where no recorded frame answers it. Yes: each runs under a recorded
   authorization or the owner's bounded yes, naming its account and bound, after its row is staged. Where: §12.
4. **OPENROUTER1's first measurement with a real key.** What: a key and its spend on a gateway. Yes: its design measures its limit words
   and offers. Where: its own design.
5. **A hosted agent.** What: AGENTINFRA1's stage D hosted half, which Lyntai's D233 keeps out. Yes: a change of the product's direction,
   then a design and a request. Where: §9.4.

Nothing else here needs the owner: the note on D22, the adapter's choices, the requests' wording and a pack made as evidence are the readers'
and the steward's. Nothing signs in, pushes or rewrites history, and nothing loses data: no record changes shape and `cooling.json` is
never overloaded.

## 12. Proof (Q23)

Every landing carries the meter (the census exact before and after with each moved site's destination, the 60 cases in every view, each
pending line naming its row), the behaviour (the row's suites whole through the path that runs, real-process cases serially), and, where a
production door moves, the parent's installed proof, recorded apart from mocked or source-built evidence (X §23).

| Row | Unit and census | Process | Conformance | Rehearsals | A person sees | Install, and what it spends |
|---|---|---|---|---|---|---|
| 1a | `StructureTests`' planted cases; the token at zero outside its places; a restore with the HTTP cache cleared; the isolated-folder restore from the closure, remote sources cleared; the resolved graph and publish layout recorded | none | three views unchanged byte for byte | the deployment rehearsal, the parent's | nothing | the published app starts with package feeds unreachable and a stand-in session runs; nothing spent |
| 1b-i | claims read into parts; the account mapping and its exception; a duplicate refused before the layer's registry; case-blind resolution; the per-start composition; the runner's members through the launcher, `CommandExists` from Install; the four kinds mapped; events translated; `NeverSent` with a written prompt read uncertain; on the tests' provider | the tests' provider's program through the runner, contained (`containment.not-widened`) | the fourth view on it; the re-pointing rule | none | nothing | none |
| 1b-ii | the diff its folder and one line; no `alt-stub` site outside it | its live cases through the layer and the runner, contained | `alt-stub`'s lines naming their rows, its cases equal to `stub`'s same scenario cases case for case | the family rehearsal's stand-in phase runs `alt-stub`'s pair with no edit | nothing | none |
| 1c ⚖ | every recorded usage answer read alike by old and new; the floor and freshness unchanged; a version not yet validated read as LQ2's answer has it and shown as today's unknown is; the sessions never opened; the census shorter by the usage readers | a stand-in usage server through the runner's live stdin, held until it answers | the usage cases in every view, the two providers in the fourth | the family rehearsal whole | the Accounts screen's windows on both agents as before, in both languages | one read per agent on one named account, no model run |
| 1d ⚖ | the driven spawn table equal to today's but the stated differences; postures mapped, a full or asking start with none mapped refused; the effort carried; a limit cooling a `claude-code` account, one on `claude-code-acp` still cooling the native door's | `LimitWaitsProcessTests`, `ContinuationTests` and the native suites whole through the layer's door | `claude-code`'s driven cases in the fourth view equal to its old view's the commit before | family and deployment | driven Claude Code sessions as before | one driven session and one resume on a named account |
| 1e ⚖ per door | as 1d for the door; its open's handshake and offers kept; a held request waiting past the turn's inactivity clock | `AcpTests`, `AcpSteerTests`, `AcpResumeTests`, `AcpBackgroundTests`, `AcpTransientTests`, `ChatTurnsTests` and the tick tests that play its frames, through the layer | as 1d, `acp-stub` with 1e-ii | as 1d | that door's sessions as before, queueing and held requests among them | one session and one resume on the door |
| 1f ⚖ | the conversation's spawn table equal | `ChatGoesOnProcessTests`, `ChatTransientProcessTests`, `ChatGoOnTests` | as 1d | as 1d | Claude Code's conversations as before | one conversation and one resume |
| j ⚖ | the cross-fed dialect cases | row i's suites | every protocol door | as 1d | none on the built-in agents | D213 row j's |
| t | the census at its permanent rows only; every pending file empty; each record a build before wrote read through the registry | the brief §9 suites whole | every view green | both, whole | nothing | D213 row t's: one session per protocol door and one on Claude Code's native door, one resume each |

D213's host rows keep the proofs their rows name, each with `StructureTests` at no family (§0.3).

## 13. Rejected

- **The layer's descriptor as Daoris's contract** (§4.1): records by kind and case-blind, the host's parts and the live run lost.
- **A copy of the layer's types inside Daoris** (D213's note): two contracts to keep equal; §2.3 holds the adapter to none.
- **Building rows i, j's seam, k, l's readers made one and e's stream words here**: each built twice.
- **Moving every door at once, or a door before its release carries parity**: a big-bang rewrite, a regression for no gain.
- **Restarting a process each turn for a conversation** (§5.1): it loses measured behaviour.
- **The layer's pool as Daoris's walk, or its store beside `cooling.json`** (§5.2): a second walk, one process, a second hold store, a
  leased attempt against D125.
- **Q-F, a failure reader without its transport** (§1.1 row 1), and **a Daoris table behind a layer-run door** (row 2).
- **Q-K now** (row 6), and **D234's mapping as binding before Lyntai's branch is accepted** (row 5).
- **The layer's retry decorator beside Daoris's retry**: two budgets.
- **The layer's brief composer for Daoris's hand-over**: D189's brief composes from Daoris's record in its own order, and Lyntai's D228
  freezes the layer's.
- **The HTTP door as OPENROUTER1's executor** (§9.4).
- **The metapackage; Lyntai's types named outside Agents, or in its registry** (§6.1, §7.1).
- **An ambient per-start context the runner reads**, in place of a registration per start (row 11).
- **The desktop's pin forced equal to the service's as a gate; lock files committed inside the reference row** (rows 12, 13).
- **A pack's version on main, a project reference, a submodule, a source copy or a committed package; a build falling back to a pack**
  (§7.3).
- **A pending line naming a Lyntai row, or one kept on a row this design does not build** (row 17).
- **j's narrowing inside a protocol door's move** (row 18).
- **Waiting for AG20's answer before any row** (§4.2).

## 14. What stays unmeasured, and what the reads should test first

**Unmeasured:** the transitive closure against Shenora 0.24.0 and SQLite 10.0.12 and 3.0.5, whether the container package is already in it,
and the publish layout (§7.1); whether nuget.org lists a release Daoris would take, and an offline restore here and in the CI beside (§7.4);
how row e's 53 sites split (§3.1); whether dsh's program reads `DSH_HOME` and resumes, and whether Codex's adapter takes a word mid-turn
(§4.2, §5.1); whether any installed plugin declares a door joined to `claude-code` with its own variable (§0.2 row 7); the agent versions
Daoris pins against the layer's validated ones (§0.2 row 6); the start-time cost of a private container per start (§7.1); what a session is
handed by the layer's rendered server file against Daoris's own (§5.6); whether Daoris's asking posture has any counterpart on a headless CLI
turn (LQ4); what Daoris reads today of an agent's `Process` beside its lines (its id for a record or a sweep, its tree for a stop), which the
runner must keep on Daoris's side (§5.5); which fake programs each Process suite and rehearsal phase runs for each door (§1.1 row 19); and
live-run parity and latency, child and replay provenance, cross-process restriction state and a held request's real waiting, each a landing
condition of its row, never a finished experiment.

**What this design's checks do not cover.** It is proposed and a document: the documentation gates prove its routing and shape, not its
claims, which it read at the commits its standing names for every line §0.2 and §0.3 cite and otherwise takes from the survey and the
proposals.

**What round 1's reads should test first**, where the synthesis is least sure:
1. **The third row** (§1.1 row 1): whether the usage reading is the right first part, against Codex's failure decoding, and whether LQ2 is a
   need Lyntai can meet by the generic test.
2. **The native door split by run kind** (row 3, 1d): whether a driven native start uses anything of a held stdin or a live run today
   (`Ports.cs:17-21`; the driven session's later words), and whether the spawn table's stated differences (the prompt on stdin, the denied
   tools, the server file) are each a decision or a request.
3. **One registration per start** (row 11): its cost, and whether Lyntai's keyed singletons and a private container per start hold under
   several concurrent starts.
4. **The stand-ins at a move** (row 19): which programs prove each door's path today, and whether `stub`'s fate at row t is right.
5. **OPENROUTER1's placement** (row 20), on Daoris's transport before 1d.
6. **LQ12's shape** (row 7) for stage B, and whether a newer build's isolated home truly runs several processes.
7. **The parity rule's mechanism** (§6.2): whether joining the fourth view in the moving row proves parity by the suite's own rule as
   claimed, given how the old view's registry over adapters reads a door that is now bound.

## 15. Rounds

Round 1, this text, is the synthesis; both makers read it next, each finding naming the file and line it rests on in either repository, and
the steward checks each against the code at the commits a round names before it is taken (the brief §2 point 3). Rounds follow until both
makers' blocking findings are taken; the ⚖ rows need both makers' reads whatever the round.
