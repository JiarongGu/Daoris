# AGENTPROVIDER1: the reads of round 1 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-26-agent-provider-design.md` and D213, round 1, both at `ed455c6dd`, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one level down. Neither carries a link, so none is rewritten; the paths both cite are code spans, in each read's own aliases. Round 2 answers every finding of both in the design's §17, one row each, and each changed passage of the design and D213 is marked *(C1-n.m)* for Codex's §n item m or *(O1-Fn)* for the Opus reader's finding n.

## Codex, round 1

Codex's read of round 1 at `ed455c6dd`: 12 findings, 8 blocking (its 1.1, 1.2, 2.1, 2.2, 3.2, 4.1, 4.2 and 4.4); another round. The design's §17.1 answers each finding.

Reviewed HEAD `ed455c6`. No files changed or tests run.

### 1. Wrong claims about the existing code

1. **Blocking — the native retry is described too broadly.** The design at `docs/2026-10-26-agent-provider-design.md:258` and D213 at `docs/decisions/D213.md:35` say an exit before naming the conversation retries once; HEAD requires a structured **resume**, a nonzero exit, no prior refusal or retry, and no requested stop (`src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs:3869`; `Conversations/Chat/ChatRunner.cs:2902`). State those guards explicitly and require negative cases for clean exit, fresh start, refusal and stop.

2. **Blocking — folding the Codex holder does not eliminate every owner without a registered door.** The claim at `docs/2026-10-26-agent-provider-design.md:112` misses accepted plugin declarations such as `accountOf: "acme"` without an `acme` registration; HEAD also retains those identities when the plugin is disabled (`src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Accounts/AccountSetTests.cs:430`). Define how registration and unavailable descriptors preserve this case, including its account wiring, before asserting that row d changes nothing.

### 2. The contract

1. **Blocking — the downward graph is incomplete.** `WireReading.Failed` contains `DoorFailure` (`docs/2026-10-26-agent-provider-design.md:337`), currently declared in Agents (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Errors/AgentErrors.cs:47`), but §2.7 does not explicitly move that payload below Events; negotiation payload ownership is similarly unspecified. Assign every readings payload a lower owner, and make Participants implement the binding writer through an injected port: the design currently assigns that write to Agents at line 320, whereas FOUNDATION1 assigns bindings to Participants (`docs/2026-10-25-foundation-design.md:2344`).

2. **Blocking — plugin provenance cannot live only on the provider.** The parts table puts `DeclaredBy`, product and maker on provider identity (`docs/2026-10-26-agent-provider-design.md:189`), although one built-in provider can acquire doors from several plugins. HEAD preserves each door’s plugin and declared maker independently (`src/Daoris.Desktop/Daoris.Desktop.Driver/Plugins/DeclaredAcpAdapter.cs:25`; `Review/Domain/ReviewerChoice.cs:24`); retain those declarations and define their relationship to provider identity, with family-comparison cases that preserve today’s answers.

### 3. Choices between the proposals

1. **Non-blocking — the record choice is reasonable, but its rejection of Codex’s alternative is inaccurate.** The comparison at `docs/2026-10-26-agent-provider-design.md:109` argues against facets with defaults, while the sealed Codex proposal explicitly rejects ineffective defaults and specifies closed unsupported-operation refusals (`docs/2026-10-26-agent-provider-proposal-codex.md:10`, `:68`). Explain the record’s construction and evolution advantage without attributing defaults to that proposal.

2. **Blocking — the synthesis drops part of applicability that protects measured admission.** Codex explicitly retained operating system, writable roots and network conditions (`docs/2026-10-26-agent-provider-proposal-codex.md:95`); the synthesis’s `Applies` list at `docs/2026-10-26-agent-provider-design.md:215` omits them, although COAGENT1 requires them (`docs/2026-10-22-coagent-design.md:339`). Restore the complete effective configuration and require conformance cases rejecting mismatched measurements and negotiations from another opening, including identical versions and posture.

### 4. Rows, proofs, census and OPENROUTER1

1. **Blocking — row c’s early sign-in proof needs plumbing that row b does not name.** Row c promises sign-in while remaining hidden without a configured command (`docs/2026-10-26-agent-provider-design.md:666`), but HEAD hides empty-binary stand-ins from agent-management operands even when configured (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/AgentsCommand.cs:653`; `Agents/AgentLoginCommand.cs:33`). Put generic management-operand discovery and the third provider’s pending-case expectations in row b, then demonstrate row c’s folder-plus-registration diff against those callers.

2. **Blocking — the environment proof both rejects today’s stand-in and misses inherited leaks.** Row h forbids every `DAORIS_` recipe variable (`docs/2026-10-26-agent-provider-design.md:681`), but the existing provider home variable is `DAORIS_STUB_CONFIG_DIR` (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Adapters/Adapters.cs:1688`). Define the protected Daoris identity variables separately, and make s-vi inspect the final spawned child’s environment with sentinel credentials: checking recipe entries alone cannot detect credentials inherited outside the recipe.

3. **Non-blocking — row n’s proof contradicts the preserved native exception.** Row n requires refusal before any prompt on every door (`docs/2026-10-26-agent-provider-design.md:687`), while §4.2 correctly retains native refusal after a silent turn at line 391. Specify protocol refusal before prompting and native refusal with no model answer or tool execution, matching `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Streams/StructuredOutput.cs:365`.

4. **Blocking — two behavior changes still ride inside move rows.** Row q requires rejecting forbidden manifest fields (`docs/2026-10-26-agent-provider-design.md:690`), whereas HEAD reads recognized fields and ignores extras (`src/Daoris.Desktop/Daoris.Desktop.Driver/Plugins/Domain/PluginCatalog.cs:397`); unifying the child card kind at design line 352 also changes one door’s output. Preserve both behaviors during their moves, or give each a separate correction row with its chosen answer, decision amendment and paired cases.

5. **Non-blocking — the census needs an explicit registration exception.** Its boundary rejects concrete provider references outside provider folders (`docs/2026-10-26-agent-provider-design.md:560`), but row c requires such a reference in `Agents/Registry/Providers.cs` at line 665. Classify that construction site narrowly, test that behavioral branches there still fail, and demonstrate equivalent sabotage coverage in the host, service, page and tools.

### 5. What is the owner’s

1. **Non-blocking — assigning installed proofs to the parent does not establish spending authorization.** The blanket statement at `docs/2026-10-26-agent-provider-design.md:728` should distinguish model-free development checks from live installed-account measurements; D191 reserves account spending beyond development runs (`docs/decisions/D191.md:19`), and COAGENT1 explicitly marks installed measurements as requiring the owner’s yes (`docs/2026-10-22-coagent-design.md:687`). Each live proof should name applicable existing authorization or obtain a bounded authorization when its concrete measurement is ready.

**Verdict: another round.**

## Opus (foundation), round 1

The independent Opus 5.5 reader's foundation read of round 1 at `ed455c6dd`: 28 findings, 3 blocking (F4, F8 and F11); accept after named changes, F6 and F7 to be taken before row h is built, F13 and F14 before row q and F19 before row p. The design's §17.2 answers each finding.

### 1. Claims about the code

I read everything at `ed455c6dd`. Its `src/` and `tools/` are identical to `0f4d2a8a7`.

**What holds.** Every row of §0.2 holds against the code:
- `max` is refused only as an account default, and the session carrier takes it (`Accounts/AgentSettings.cs:185-193`; `Sessions/Domain/ActorRun.cs:44`, `:55-58`; `Web/settings/ai/Actors.tsx:26-27`).
- The D179 interim is at `D179.md:207-211`.
- The holder's terminal runs on no server (`Modules/Platform/HolderTerminal.cs:44`).
- The census families sit at `structure.json:862`, `:1034`, `:1065`, `:1122`, `:1211` and `:1332-1433`.
- The cited lines in `Settings/DriverConfig.cs:72`, `Platform/ActorVerbs.cs:74-75`, `Settings/AccessKey.cs:74` and `Participants/BindingStore.cs:86-94`, `:236-242` are as stated.
- The wire extensions appear on 63 lines in 24 files.
- `Agents/AgentsCommand.cs:653-658` and `BrainstormSynthesis.cs:12` are as stated.

The census baseline also holds at both commits: 55 lines in 23 files for `SessionWire.Acp|Pipe`, 56 in 26 for `.Wire`.

The adapter seam's lines hold:
- the `Resumes` default (`Adapters.cs:1506`), the built set (`:2548-2571`) and `WithPlugins` (`:2521-2537`);
- `Spawning.Roles` (`:1659-1665`), the shell (`:1606-1653`) and the postures (`:1348-1376`);
- the trailer (`:383-431`).

So do these:
- `ClaudeErrors` (`AgentErrors.cs:281-323`), `ClaudeAcpErrors` (`:329-344`) and `DoorFailure` (`:47`);
- the `WeekFixed` route (`Modules/Bridge/Routes/AccountsRoutes.cs:62`) and `Accounts/Usage.cs:69-76`;
- `Setup/SetupCommand.cs:116-118` and `ChildBindings.cs:11`, `:81-82`.

**What is wrong:**
- **F1**: `Loop/Driver.cs` citations past line 852 are 5 lines short.
- **F2**: set-up is not the only door that reads an unknown agent as the pipe, and one test does name `SetupWorld.DoorOf`.
- **F3**: the older-build behaviour in §5 is not what the code does.
- **F4**: offers are kept per account; the design keys them per door and version only.
- **F5**: the stub's tables are not quite "Claude Code's by reference".
- **F6**: the `DAORIS_` rule fails today's stub.
- **F7**: the shell is not unchanged, and the order the tools' PATH is applied in matters.
- **F22**: §0.2 misstates what O said about cutting the *Agents names …* families.

### 2. The contract

**A provider as an agent with one or more doors.** This holds:
- Codex's holder folds into Codex's provider, and `codex` is refused where a door is asked.
- A door's id is opaque outside the providers.
- Typed ids stop a provider's id being passed where a door's is wanted.
- The "provider per agent-and-door pair" counterexample is answered.

**Required parts, with `Absent` meaning today's behaviour.** This answers omission: a part added later breaks every provider's construction, where today a plugin's door got `Resumes` true by default. "A new provider inheriting a wrong default" still survives in two places:
- `Reach` stated absent reads as *everywhere*, the widest answer, on the one part that confines (F12).
- Abilities that the protocol transport gives every protocol door today would read absent on a plugin's declared door, which has no field to state them (F13).

Joining a provider by `accountOf` is the one claim by which a manifest gains what it may not declare, and the joined door's own identity has nowhere to live (F14). The native door's session-scope offers have no part (F15).

**`Known<T>` with source, evidence and applicability.** This is right, and §2.4's rule that a negotiation describes only the opening that answered it handles "a negotiation answered by another opening". Two gaps:
- `AgentRun` must admit operations by its own opening's answers, and kept readings may only narrow, never admit (F16).
- `Applies` is narrower than COAGENT1 §3.3's effective configuration, which is a decided rule (F11, blocking).

**`AgentRun` refusing before any write.** Sound, and needed to remove the callers' three-capture branches.

**Two shared transports, each door handing its dialect.** Sound:
- Row i keeps today's composite reading.
- Row j narrows each door to its own dialect, so in the driver "a dialect reading another agent's key" is closed.
- The service still tries every provider's call keys on every call (F17).

**Readings in Events, one sink per run.** The vocabulary and its mapping onto FOUNDATION1's codes are right:
- `turn.answered` is minted only for an answer, never for a stopped or failed turn.
- `part.quiet` stays the watch's.
- A child ended by its parent's end is inferred, its outcome unknown.
- The provider mints nothing.

But:
- The payload types of `Failed`, `Negotiated`, `WindowsSaid` and the turn's context are Agents types, which puts an upward edge into Events (F8, blocking).
- The sink's binding writer is placed in Agents, but `bindings` is Participants' (F9).
- Some facts have two homes (F21).

**Records keeping their strings, resolved by one registry.** This holds for every string. The offers' account dimension is missing (F4), and the older-build claim is wrong (F3).

**Fit with FOUNDATION1's map.** Three upward edges: F8, F9 and F10. The lineage hook is moved out of Participants without saying so (F28).

**Also:**
- The census vocabulary has no declaring part (F18).
- §4.4's "never trusted to an inherited environment" contradicts D70's native path, which the design keeps (F19).
- §7 row 3 applies the "compatible recipe" rule to a resume across versions; it belongs to a carry (F20).

### 3. The choices between the proposals

The reasons in §1.1 hold for these rows:
- row 1, the required record over facets with defaults;
- row 2, `AgentRun`;
- row 4, no doorless provider;
- rows 5 and 6, shared transports outside the providers' boundary;
- row 7, the stand-in split from its plumbing;
- row 11, rows cut by part;
- row 12, the plugin factory in Agents;
- rows 14 to 19.

Each comes with a case the other proposal gets wrong.

Rows that are right but need a change:
- Row 3 is right, but it narrows X's applicability (F11).
- Row 8, alt-stub's program in its own folder, needs row b to make the driver's project pick up providers' programs (F24).
- Row 9, the stubs by reference, needs the referenced tables named exactly (F5).
- Row 10, moves apart from corrections, is the right principle but rows d and f break it (F26).
- Row 13 has the right shape, but its judge sits in Agents and walks Roles' order (F10).

Dropped, and should stay:
- X's account applicability on offers (F4).
- X's operating system, writable roots and network conditions for safety measurements (F11).
- X's word-reach row among the records (F22).
- X's scoping of the compatible-recipe rule to a carry (F20).
- O's fold of D212's row 1g (F22).

### 4. The rows

**a.** Lands alone and changes nothing, but "a day" is optimistic: a semantic census across the desktop, the service, the page and the tools, plus the owner table. Its census also needs a vocabulary before any registration exists (F18).

**b and c.** The stand-in is the contract's meter, and its proof needs two named changes:
- Row b's registration list is empty, yet its proof runs the stand-ins through the shim (F23).
- Row c's "folder and one line, no rehearsal edit" cannot hold without a stand-in scenario contract and a project-file glob that row b adds (F24).

**d and f.** Both carry decisions inside moves (F26).

**e.** Holds. Its "`ToolHomeLeftovers`' homes from the providers" must not newly list Codex's home (F26).

**g.** Holds. The source-scan test at `DriverCommandTests.cs:372` moves with it (F2).

**⚖ h.** Needs F6 and F7 before it is built.

**i and ⚖ j.** Hold, and their proofs are right.

**k.** Depends on l and m (F25).

**l.** Its byte-for-byte proof is good. Needs F8 and F9.

**m.** Holds, apart from F20.

**n.** Needs F4 and F15.

**⚖ o.** Moves the lineage hook (F28).

**⚖ p.** Needs F19.

**⚖ q.** Needs F13 and F14.

**r.** Is not all of COAGENT1e (F27).

**The corrections.**
- s-i: holds; it updates the test named in F2.
- s-ii: holds, with F20 applied.
- s-iii, s-iv and s-vii: hold.
- s-v: should be ⚖ by the design's own rule (F26).
- ⚖ s-vi: holds. Its read should say that a plugin's own provider is stripped too.

**t.** Its older-build proof cannot pass against a shipped build as written (F3).

**The ⚖ set** is otherwise right: h, j, o, p, q and s-vi each move or narrow what reaches an agent.

**The census** is well specified: semantic for the desktop, exact sites, permanent exceptions carrying their decisions. The service's `claude-code` default has no removing row; name it as permanent or give it its row (F18).

**The conformance suite** is right in its mandatory cases and its pending table. Its `DAORIS_` case fails today's stub (F6). Its absent-ability fallbacks must equal today's behaviour on the protocol transport (F13). Its live stand-in cases belong in the Process half.

**OPENROUTER1** also needs i and k, whose dialects it reuses by reference, and a row that reads `Reach` at the start layer (F12).

**Sequencing with FOUNDATION1** needs stating (F28).

### 5. Findings

**F1 (not blocking).** z-iii added `Context.Conversations.ImportFiles()` at `Loop/Driver.cs:853-857`. Every `Loop/Driver.cs` line past 852 that the design cites "as the survey cites it" is therefore 5 short:
- §1.1 row 2's `:3806-3825` is now `:3811-3830`;
- §4.3's `:4397-4441` is now `:4402-4446`;
- row o's `:2282-2303` is now about `:2287-2308` (the trust check is at `:2299-2305`).

The "Read at" claim, that only §0.2's files moved, is wrong. *Change:* re-cite these lines, or note in "Read at" that `Loop/Driver.cs` lines past 852 are +5.

**F2 (not blocking).** §7 row 1 singles out set-up for reading an unknown agent as the pipe. The loop does the same (`Loop/Driver.cs:685`, `:689`), and so does the window (`Modules/Bridge/DriverContext.cs:164-171`). Also, "none tests `SetupWorld.DoorOf`" overlooks `Loop/DriverCommandTests.cs:372`, which holds that call's text by a source scan. *Change:* say all three read unknown as the pipe, and name the test as one that g or s-i updates.

**F3 (not blocking).** §5 says an older build meeting a record that names a door it lacks "refuses to continue it … as it does for a removed plugin's door". It does not. A driven record that ran on another door has its words carried on by the walk's pick (`Loop/DriverContinuations.cs:30-45`; `Trees/SessionGroups.cs:980-983`). Only a chat refuses, through `AdapterSet.Resolve` (`Adapters.cs:2539-2546`). Row t's proof, "an older build's own reader refusing … rather than substituting its default", cannot pass against a shipped build. *Change:* state what an older build does: a driven record is carried on by its walk with the brief, never resumed on another door; a chat is refused, naming what exists. Prove that instead.

**F4 (BLOCKING).** Offers are kept per door, account and version: the key is `<agent>|<account>`, and an account's refusal at an open passes that account for a day (`Accounts/AgentOffers.cs:7-8`, `:26-39`, `:86-97`). §4.2, §5's offers row, §2.8 row j and §6 key them per door and version only, and give D212's start plan `ProviderRegistry.Offers(door, version)`. That signature cannot answer D205's per-account judge. It also drops X's account applicability. *Change:* make it `Offers(door, version, account)`, read from Agents' offers store rather than the registry, and key §5's row per door, account and version.

**F5 (not blocking).** The stub references Claude Code's limit, window, sign-in and refused words. Its error words are only `ClaudeErrors.Codes` (kind words and statuses, without the transient sentences: `Adapters.cs:1734-1738`; `AgentErrors.cs:318-322`). Its login question and home are its own. Referencing Claude Code's whole `Tables` would make a stand-in's "529 Overloaded" text read as transient where today it ends. *Change:* §2.2, §1.1 row 9 and row d should name the referenced tables exactly: `Codes`, not `Words`; and `acp-stub` takes no `AuthRequiredCode`.

**F6 (not blocking; row h ⚖).** The rule "a recipe naming … any `DAORIS_` variable fails the conformance suite" fails today's stub: its account home variable is `DAORIS_STUB_CONFIG_DIR` (`Adapters.cs:1688`), and it belongs in its door's recipe. §4.6 also has every door carrying `DAORIS_TARGET`. *Change:* name Daoris's own variables by list (`Spawning.Roles`, `Adapters.cs:1659-1665`, plus the shell's repository, service, tools and git variables) instead of by prefix, or make the stub's variable a named exception. Have Platform, not the door, apply `DAORIS_TARGET`.

**F7 (not blocking; row h ⚖).** §2.6 says the shell is "unchanged", but §4.6 moves Daoris's variables into Platform after the recipe. Today the shell sets them: `Unrole`, the repository and service variables, `Tools.Hand`, `GitWiring`, `GitAuthor` and `GitLongPaths` (`Adapters.cs:1635-1650`, which is the census's *Agents names Workplace* row). The tools' PATH is set "before the harness's binary is resolved on that PATH" (`:1638-1642`), and `HarnessSelection.cs:553-560` resolves a `.cmd` shim against it. If the PATH is applied after the recipe, an npm-installed adapter may resolve to a different program. *Change:* say the shell's Daoris half becomes Platform's application. Apply the tools' PATH before the program is resolved. Have row h's table compare the resolved program and its arguments, not only the variables.

**F8 (BLOCKING).** `WireReading` is declared in Events (7), but its readings carry Agents (9) types:
- `Failed` carries `DoorFailure` (`Agents/Errors/AgentErrors.cs:47`, with `DoorWhere` at `:22`; the file is placed in Agents, `structure.json:89`);
- `Negotiated` carries abilities, which are `Known<T>` in `Agents/Contract`, and offers, which are `AcpConfigOption` (`Agents/Acp/Acp.cs:74`);
- the turn's context is `AcpUsage` (`Acp.cs:34`);
- `WindowsSaid` carries the window readings.

§2.7 moves down only the end, refusal, transient and turn facts that FOUNDATION1's cut takes. As written, Events names Agents permanently. *Change:* §2.7's Events row should add Events-owned values for the failure (moving `DoorFailure` and `DoorWhere` down), the windows, the context and the negotiated reading (plain ability keys and offered values), with Agents building `Known<T>` from them. Also list `Negotiated` and `Outside` in D213 point 5.

**F9 (not blocking).** §3.1 has the sink keep "in Agents the binding's conversation (the store's `bindings`)". `bindings` belongs to Participants (D212 §14.2 row 12; `HarnessConversations` is placed there at `structure.json:186`), so Agents writing it is an upward edge. *Change:* Participants' writer keeps `ConversationNamed`.

**F10 (not blocking).** `Reach.Drives(order, adopted)` sits in `Agents/Domain/` but walks Build's order. That order is `ActorChoices.Effective`/`ActorEffective` (`Platform/ActorChoices.cs`, placed in Roles at `structure.json:286`), and the walk goes "through the start's judge", which is Roles' or Participants' (D212 §2.3, §2.4). Both are upward edges. `Reach` also names two things: a provider part (§2.3) and this connector judge. *Change:* the judge in Agents takes door ids and the per-door rule only; the walk over Build's order stays in Roles. Rename the judge apart from the part.

**F11 (BLOCKING).** §2.4 and D213 point 2 define `Applies` as the door, the agent's and adapter's versions, the posture and the configuration digest. COAGENT1 §3.3 (accepted, D199) defines a measurement's effective configuration to also include the writable roots, the network and loopback reach, and the operating system (`docs/2026-10-22-coagent-design.md` §3.3). X kept these for safety measurements. As written, a decision others rest on would let a confinement measurement taken on one OS or set of roots admit on another. *Change:* say `Applies` is COAGENT1 §3.3's effective configuration, whole, in both §2.4 and D213 point 2.

**F12 (not blocking).** `Reach` stated absent reads as "everywhere", the widest answer, on the one part that confines. A plugin's own provider cannot state it, because its manifest has no field. No row builds the start-layer reader of `Provider.Reach`: §12 leans on D212 §2.3, whose admission is keyed by work. OPENROUTER1's prerequisites (d, e, f, h, l, n) also omit i and k, which create the protocol and native dialects it reuses by reference. *Change:* make `Reach` a required value (`Everywhere` or `Opened`), never `Absent`. Name the row that reads it at the start layer, and list i, k and that row among OPENROUTER1's prerequisites.

**F13 (not blocking; row q ⚖).** Some abilities are given by the protocol transport to every protocol door today, plugin doors included:
- the limit resume (`DriverContinuations.cs:54-63`, which checks the wire alone);
- a stop by `session/cancel`;
- servers on the wire;
- interactive (`Plugins/DeclaredAcpAdapter.cs:29`).

A `DeclaredDoor` has no field for these, so as written they read absent and the plugin door loses them. *Change:* §2.6 lists the abilities the protocol transport gives every protocol door, from the transport's own source, and the factory sets them.

**F14 (not blocking; row q ⚖).** A plugin harness declares its own product and maker (`Plugins/Domain/PluginManifest.cs:33-35`; `DeclaredAcpAdapter.cs:44-58`). Today those feed D155's family and its "as its plugin declares" wording (`Review/Domain/ReviewerChoice.cs:24-46`) and the commit trailer (`Adapters.cs:383-388`). §2.3 has no identity at the door level, and §4.8 gives the declaration "its identity" with nowhere to put it once the door joins a provider. Nor does §4.8 say which provider parts a joined door reads. This is the "claiming too much" counterexample: joining hands over another provider's credentials and vocabulary. *Change:* §4.8 and row q list what a joined door reads (Accounts, Tables and Usage, and only those) and keep its declared product and maker as the door's own. Records should keep the plugin's maker on new writes.

**F15 (not blocking).** §4.2 adopts two scopes of offers, but §2.3 has a home only for the account-default scope (`Models` = `ISettingsFile`). A native door's declared session offers, such as `max` on Claude Code's pipe door, have no part, and row s-iv reads them. *Change:* add a door part `Offers`, declared for a native door and negotiated for a protocol one.

**F16 (not blocking).** §2.5 says "each operation admitted by the door's abilities". *Change:* say the run admits by its own: declared values, this opening's negotiation, and a measurement that applies to the run's configuration. A kept reading may narrow (as `AgentOffers.Judge` passes over an account) but never admit.

**F17 (not blocking).** The service's call reader tries Claude Code's key, then Codex's, on every call, whoever made it (`Core/Sessions/CallLineage.cs:65-69`). Splitting it into one file per provider still needs an ordered list of readers in the service, which knows no door-to-provider mapping. That is a dialect reading another agent's key. *Change:* name the service's ordered reader list and its precedence as a census exception with a twin case, or select a reader by the record's door through a twin table.

**F18 (not blocking).** The census reads "the list from the providers' registrations", but wire keys and files (`_claude/…`, `thread_active_writer`, `x-codex-turn-metadata`, `.mcp.json`) sit in no part of §2.3, and row a has no registrations yet. The service's `claude-code` default (`Http/Program.cs:1291-1293`) has no removing row. *Change:* add a declared vocabulary part, seed row a's list from a fixture that row t empties, and name the service default as permanent or give it a row.

**F19 (not blocking; row p ⚖).** §4.4 says the session's identity is "never trusted to an inherited environment". But the native driven path D70 keeps reaches the connector through the repository's own `.mcp.json`, and the identity arrives by inheritance (`Adapters.cs:1574`). *Change:* state that this path relies on "its servers inherit its environment", declared true for Claude Code's native door, and that a door stating it absent cannot drive by a repository's own servers.

**F20 (not blocking).** §7 row 3 attaches the "compatible recipe across doors or versions" rule to a resume. Taken literally, that would refuse a resume on the same door after every adapter update. X's rule was about a carry. *Change:* a resume on the record's own door at any version goes as today, with the wire's `gone` and `exited`. The recipe rule applies to a carry only.

**F21 (not blocking).** Some facts have two homes:
- `raw` appears both in `Said` and as `Unread` ("the raw stream, local"), while today an unknown frame is kept in the event file as `raw` (`SessionEvents.cs:46-47`);
- the context appears both in `Said`'s `usage` and in `TurnEnded`.

Row l's byte-for-byte proof needs one answer for each. *Change:* `Unread` is written as today's `raw` event, and the context has one home.

**F22 (not blocking).** Two items were dropped:
- X's word-reach record (`turn-end`, `next-step`, `now`; survey §14) is missing from §5's table.
- D212's Part B rows 1g, 1q and 1r are missing from §2.8, though O had 1g.

§0.2's "Neither proposal: who cuts the *Agents names …* families" misstates O, whose §1.5 and "Beside FOUNDATION1's rows" assign the cuts to its own rows. *Change:* add these to §5 and §2.8, and correct §0.2.

**F23 (not blocking).** Row b says `Providers.Built()` is "empty of providers here", yet its proof has "both of today's stand-ins run through the shim, old against new". The shim presents contract doors, so the proof proves nothing of it, and row c becomes the shim's first user: any shim gap found there is a change outside alt-stub's folder. *Change:* row b writes `stub` and `acp-stub` on the contract, by reference, through `LegacyDoor`, with their behaviour compared.

**F24 (not blocking).** Row c's "its folder and one line, no edit to the rehearsal" cannot hold as written:
- The family rehearsal names `stub` and `acp-stub` 235 times, and its phases assert the stub program's scripted content (`tools/family-rehearsal.mjs:4528-4531`, `:4808`, `:4840-4841`).
- The driver picks up programs by explicit project entries (`Daoris.Desktop.Driver.csproj:26`).

*Change:* row b defines the stand-in scenario contract that the phases drive, names which phases a third pair runs, and adds a project-file glob for providers' programs and fixtures.

**F25 (not blocking).** Row k unifies "the four `stream-json` readers … the other three reading readings", puts native framing "behind `AgentRun`", and proves "exact receipt order". But the readings and the sink land in l, and the executors' `AgentRun` with `DoorInput`'s attempt port lands in m. *Change:* k moves the native dialect and the retry once; the readers' unification goes to l and the run's framing to m. Or order k after l and m.

**F26 (not blocking).** Rows d and f carry decisions inside moves, against §1.1 row 10:
- Row a lists "each cell where two copies disagree as row d's decision".
- Row f says "each difference listed as a correction before it lands", with no correction row for it.

Two more:
- Row e's "homes from the providers" must not newly list Codex's home (`Platform/ToolHomeLeftovers.cs`: "Codex is not listed").
- s-v changes what reaches Codex's and dsh's processes, as j and s-vi do, yet it is not ⚖.

*Change:* each disagreeing cell or difference becomes a correction row of its own, or the row proves there is none. Row e lists only providers that declare a conversation home. Mark s-v ⚖, or say why it is exempt.

**F27 (not blocking).** Row r "is COAGENT1e", but COAGENT1e also holds the manifest's two postures, which row q leaves to "COAGENT1e's own reviewed row", and the family rehearsal's Stub Two phase (`TASKS.md:406`; coagent design §8.1, §8.4). *Change:* row r builds the sheet half and the measurements table; COAGENT1e keeps its postures and its Stub Two phase, after row q.

**F28 (not blocking).** The *Agents names …* edges assigned to rows h, l, m, n, o and q are mod-xxi's precondition (D212 §14.6: mod-xxi lands only when no upward exception is left). Yet §11 has rows waiting while mod-xxi holds Loop's files, so each could wait on the other. Rows f, l, p and r also wait on FOUNDATION1's Install, Events and Knowledge cuts, which no row names. And moving the lineage hook into Claude Code's provider folder (§3.3, row o) changes where D212 §14.2 row 12 places it: `LineageHook` is in `Sessions/ChildLineage.cs:9`, placed in Participants at `structure.json:517`. *Change:* state that these rows land before mod-xxi is opened, and name the lower cut each row waits for. Have row o take the D212 note for the hook's new place.

28 findings, 3 blocking (F4, F8, F11).

**Verdict:** accept after named changes the steward can check without another read. The three blocking findings are each a sentence or a signature: `Offers(door, version, account)`; Events-owned payloads in §2.7's Events row; `Applies` as COAGENT1 §3.3's effective configuration whole. Before the ⚖ rows they touch are built, take F6 and F7 into row h, F13 and F14 into row q, and F19 into row p.
