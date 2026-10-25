# AGENTPROVIDER1: the reads of rounds 1 and 2 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-26-agent-provider-design.md` and D213, round 1, both at `ed455c6dd`, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one level down. Neither carries a link, so none is rewritten; the paths both cite are code spans, in each read's own aliases. Round 2 answers every finding of both in the design's §17, one row each, and each changed passage of the design and D213 is marked *(C1-n.m)* for Codex's §n item m or *(O1-Fn)* for the Opus reader's finding n.

Both makers' reads of round 2, at `e69b58872`, follow in the same way: run from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one level down (Codex's section labels, bold in its output, set as headings, as round 1's are). Round 3 answers every finding of both in the design's §18, one row each, and each changed passage of the design and D213 is marked *(C2-n.m)* for Codex's §n item m or *(O2-Fn)* for the Opus reader's finding n.

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

## Codex, round 2

Codex's read of round 2 at `e69b58872`: another round. Its §0 checks its own round-1 findings against round 2 (each taken but 4.1, answered with a reason that holds, and 1.2, open on the collision its 2.1 names); it then finds seven, three blocking (its 1.1, 2.1 and 2.2: dsh's door is not interactive, an account owner that names another provider's door breaks the registry's identity rule, and the spawn boundary needs a downward type), its 2.3 and 4.3 confirming what holds. The design's §18.1 answers each finding.

Reviewed HEAD `e69b58872`. No files changed or tests run.

References: **P** = `docs/2026-10-26-agent-provider-design.md`; **D** = `src/Daoris.Desktop/Daoris.Desktop.Driver/`; **S** = `src/Daoris.Service/Daoris.Service.Core/`.

### 0. Your round-1 findings

| Finding | Severity | Disposition |
|---|---|---|
| 1.1 Native retry guards | Blocking | **Taken** — P:307–311 and :848 preserve the guards and require negative cases. |
| 1.2 Owners without registrations | Blocking | **Open** — P:196–206 preserves `acme` and disabled identities, but the door-name collision below remains. |
| 2.1 Readings’ ownership and binding writer | Blocking | **Taken** — P:328 and :354–360 put payloads below Agents and the binding writer in Participants. |
| 2.2 Plugin provenance | Blocking | **Taken** — P:238 and :567–572 retain each door’s declarations independently. |
| 3.1 Mischaracterizing Codex’s facets | Non-blocking | **Taken** — P:135 now argues construction and evolution without attributing defaults to Codex. |
| 3.2 Applicability incomplete | Blocking | **Taken** — P:249–261 restores the effective configuration and mismatch cases. |
| 4.1 Early sign-in plumbing | Blocking | **Answered with a reason that holds** — P:803–820 and :831–834 preserve terminal refusal and prove sign-in through existing window callers. |
| 4.2 Environment proof | Blocking | **Taken** — P:526–542 names protected variables and inspects the spawned child’s environment. |
| 4.3 Native model-refusal exception | Non-blocking | **Taken** — P:472–473 and :851 distinguish protocol refusal from the native silent-turn exception. |
| 4.4 Corrections inside moves | Blocking | **Taken** — P:563–565 and :863–864 separate manifest, card-kind and owner-cell changes. |
| 4.5 Registration census exception | Non-blocking | **Taken** — P:699–706 and :821 restrict registration and require sabotage coverage. |
| 5.1 Installed-proof spending | Non-blocking | **Taken** — P:758–761 requires recorded authorization or a bounded owner authorization. |

### 1. Wrong claims about the existing code

1. **Blocking — interactivity is not universal to protocol doors.** P:298–303 calls it transport-provided to every protocol door, repeated at :573–574; HEAD defaults `Interactive` to false (`D/Agents/Adapters/Adapters.cs:1395`), and `DshAdapter` supplies neither interactivity nor `PrepareChat` (`:1970–2037`). `D/Conversations/Chat/ChatRunner.cs:568` and :974 consequently refuse or skip dsh before account selection. Keep interactivity door-declared, preserve dsh’s refusal, and require that case in rows g/q and conformance.

### 2. The contract

1. **Blocking — an accepted account owner can violate the registry’s identity invariant.** P:202 forbids a door id equalling another provider’s id, while :206 and row a (:782–786) require preserving `accountOf` naming another provider’s door. A plugin door with `accountOf: "claude-code-acp"` is accepted by `D/Plugins/Domain/PluginCatalog.cs:397–405`; its owner remains that exact string (`D/Accounts/Harnesses/Harnesses.cs:281`). Grouping it under provider `claude-code-acp` violates the invariant; grouping it under `claude-code` changes its account identity—specify a compatible representation before row a, rather than deferring the conflict until s-ix.

2. **Blocking — the spawn boundary still needs a downward type contract.** P:330 places `SpawnRequest` in Agents, but :314 assigns its application to Platform; Platform cannot consume an Agents-owned request under FOUNDATION1’s `docs/2026-10-25-foundation-design.md:2363`. Today’s shell also invokes Install’s `Tools` and Workplace’s git helpers (`D/Agents/Adapters/Adapters.cs:1642–1650`; `Driver.Tests/fixtures/structure.json:108`, :306–323). Name a Platform-owned launch/environment value, have the higher owners compute its wiring, and require row h’s structure proof to exclude Platform-to-Agents/Install/Workplace edges.

3. **Non-blocking — the remaining contract addresses the named counterexamples, with an explicit service exception.** P:249–261 prevents another opening’s negotiation from admitting a run; :328 and :354–360 repair readings and sink ownership; :556–576 limits manifest claims. The service deliberately still tries both lineage vocabularies (`S/Sessions/CallLineage.cs:65–79`), now accurately retained as a precedence-tested exception at P:438–442, rather than claimed as provider-selected decoding.

### 3. Where it chose between the proposals

None beyond the identity conflict in 2.1. The corrected reasons retain the consequential contributions of both proposals; I found no further dropped requirement that should return.

### 4. Rows, proofs, census, conformance and OPENROUTER1

1. **Non-blocking — row l’s no-duplication proof is stated too strongly.** P:849 requires “one input frame never two readings,” but one native result legitimately supplies usage, turn and failure facts (`D/Agents/Streams/StructuredOutput.cs:329–369`). Require each semantic fact to be emitted once, allowing several distinct readings from one frame; retain the byte-for-byte output and no-duplicate-lineage checks.

2. **Non-blocking — make wrong-door dialect rejection an explicit conformance case.** Row j’s proof at P:847 covers valid vendor sessions, while P:723 chiefly specifies each dialect’s own recorded frames. Add cross-fed fixtures: Claude extensions on Codex/dsh/plugin doors, Codex refusal metadata on Claude’s door, and both on `acp-stub`; assert the exact allowed readings and absence of unintended abilities or account effects. HEAD’s shared decoding at `D/Agents/Acp/Acp.cs:1714–1734` shows why positive vendor fixtures alone cannot prove this boundary.

3. **Non-blocking — otherwise the revised sequence and proof responsibilities are coherent, subject to findings 1–2.** P:868–870 places corrections after their moves, u before t, and the required cuts before mod-xxi; :755–761 identifies the ⚖ rows and bounded live proofs. The census’s narrow exceptions, row-b discovery plumbing and provider-local pending cases support the folder-plus-registration proof, while OPENROUTER1 now depends on both dialects and the reach gate (P:878–894).

### 5. What is the owner’s

None missing. P:898–924 now separates architectural settlement from live-account measurements, key entry and departures from the stated product direction, consistently with `docs/decisions/D191.md:19–21`.

**Verdict: another round.** Resolve the account-owner/door collision, specify the downward spawn boundary, and preserve dsh’s non-interactive behavior before acceptance.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `e69b58872`, which also carries FOUNDATION1's mod-iv: 19 findings, 2 blocking (F1, where Daoris's own variables are applied, and F2, what a joined door reads as its own); accept after named changes the steward can check without another read, F3 and F4 now and the rest before the row each names (F5 before b, F17 before k, F6 and F9 before h, F16 before j, F10 before l, F11 before n, F19 before q, F8 before u, F7 before s-vi and OPENROUTER1). It finds no blocking finding of round 1 open. The design's §18.2 answers each finding.

### 0. Both reads of round 1

I read everything at `e69b58872`. That commit also carries FOUNDATION1's mod-iv (the Service move), which landed after round 2's read commit `5fdf17962`. Paths use the survey's aliases, and `structure.json` means `Driver.Tests/fixtures/structure.json`.

The statuses mean:
- **Taken**: round 2 made the change and it holds against the code.
- **Answered**: the change was not made, and the reason given holds.
- **Open**: not dealt with.
- **Wrong**: the answer is mistaken.

"→ Fn" points to the finding in §4 that a leftover gap opens.

**Codex, round 1 (12 findings, 8 blocking)**

| Finding | Blocking? | Round 2 | Status | Against the code |
|---|---|---|---|---|
| 1.1 native retry too broad | yes | guards stated; row k gets negative cases | taken | `Loop/Driver.cs:3872-3878` and `ChatRunner.cs:2912-2921` hold. Moving the retry into the transport makes Agents name Course's `ResumeAsk` → F17 |
| 1.2 owners with no door | yes | a provider made from declarations; a disabled plugin's ids kept | taken | `Adapters.cs:2475-2482`, `:2532-2536`, `AccountSetTests.cs:430-446` and `HarnessRoster.cs:813-823` hold → F15 |
| 2.1 downward graph incomplete | yes | payloads moved to Events; sink composed in Loop; Participants' part keeps the binding | taken | `AgentErrors.cs:22`, `:47`, `:50-51` and `structure.json:89`, `:185` hold. The F7 change opens a new upward edge → F1. The sink's parts are incomplete → F10 |
| 2.2 plugin provenance only on the provider | yes | doors get their own `Identity` | taken | `DeclaredAcpAdapter.cs:25-26`, `:46-59`, `ReviewerChoice.cs:24-38` and `Adapters.cs:386-399` hold → F9, F2 |
| 3.1 rejection of Codex's facets inaccurate | no | §1.1 row 1 rewritten | taken | — |
| 3.2 applicability narrowed | yes | COAGENT1 §3.3 taken whole | taken | → F13 |
| 4.1 row c's plumbing | yes | row b names the listing's and the operands' readers and the pending layout | taken | `AgentsCommand.cs:653-658`, `AgentLoginCommand.cs:33-35`, `HarnessRoster.cs:863-867`, `Modules/Bridge/AgentActions.cs:468`, `:534` and `HarnessActions.cs:378-384` hold |
| 4.2 environment proof | yes | a list instead of a prefix; s-vi reads the child's own environment | taken | `Adapters.cs:1688` now passes → F6, F7, F1 |
| 4.3 row n against the native exception | no | row n states each door's case | taken | `StructuredOutput.cs:365-367` holds → F11 |
| 4.4 behaviour changes inside moves | yes | manifest read kept; card kinds kept until s-viii; s-ix; s-v made ⚖ | taken | `PluginCatalog.cs:397-411`, `AcpStreams.cs:37` and `StructuredOutput.cs:490-500` hold. The F14 change opens a new one → F2 |
| 4.5 census registration exception | no | narrow classification, planted cases | taken | → F14 |
| 5.1 installed proofs and spending | no | spending proofs told apart from model-free checks | taken | → F18 |

**The Opus reader, round 1 (28 findings, 3 blocking)**

| Finding | Blocking? | Status | Against the code |
|---|---|---|---|
| F1 Driver.cs lines 5 short | no | taken | mod-iv did not touch `Loop/Driver.cs`. `:3811-3830`, `:4402-4446`, `:2287-2308` and `:2299-2305` hold |
| F2 three places read unknown as the pipe; the scan test | no | taken | `Loop/Driver.cs:684-690`, `Modules/Bridge/DriverContext.cs:162-172`, `Setup/SetupWorld.cs:20-30` and `Loop/DriverCommandTests.cs:372` hold |
| F3 older-build behaviour | no | taken | `DriverContinuations.cs:30-45` and `Adapters.cs:2539-2546` hold. `SessionGroups.cs:980-983` is now `:759-762` → F4 |
| F4 offers kept per account | **yes** | taken | `AgentOffers.cs:33-39`, `:86-97` hold. The key's agent is the door (`HarnessRoster.cs:1339`, `Loop/Driver.cs:3774`) |
| F5 stub's tables named exactly | no | taken for the tables | `Adapters.cs:1728-1738` and `AgentErrors.cs:322` hold. The stub's dialects are not named exactly → F5 |
| F6 the `DAORIS_` rule fails the stub | no (before h) | taken | → F6 |
| F7 the shell; PATH order | no (before h) | taken for the order | Placing the application in Platform → F1 |
| F8 readings carry Agents types | **yes** | taken | `Acp.cs:34`, `:74` and `AgentErrors.cs:22`, `:47` hold |
| F9 binding writer | no | taken | — |
| F10 the judge walks Roles' order | no | taken | `ActorChoices` is placed in Roles (`structure.json:285`) |
| F11 `Applies` narrower than COAGENT1 | **yes** | taken | → F13 |
| F12 `Reach` absent; no reader | no | taken | → F8 |
| F13 abilities the transport gives | no (before q) | taken | `DeclaredAcpAdapter.cs:32` holds. The limit resume also reads `Resumes` → F12 |
| F14 a joined door's identity and what it reads | no (before q) | taken for identity; **wrong** for "only those" | → F2 |
| F15–F16 offers part; run's own evidence | no | taken | — |
| F17 service reads every key | no | answered: the door-table form is rejected because the service holds no plugin's door; the reason holds | `CallLineage.cs:65-79` holds |
| F18 census vocabulary; service default | no | taken | The lines are actually `Http/Program.cs:1293`, `:1324`, `:1967` → F4 |
| F19 D70 path relies on inheritance | no (before p) | taken | `Adapters.cs:1572-1574` holds |
| F20–F22 | no | taken | `SessionEvents.cs:46-47` holds |
| F23 stand-ins on the contract in b | no | taken | → F5 |
| F24 scenario contract and glob | no | taken | `family-rehearsal.mjs:4528-4531`, `:4808`, `:4840-4841` and `Daoris.Desktop.Driver.csproj:25-26` hold |
| F25 row k needs l and m | no | answered (first form) | → F17 |
| F26–F27 | no | taken | `ToolHomeLeftovers.cs:41` and `TASKS.md:406` hold |
| F28 sequencing with FOUNDATION1 | no | taken | `ChildLineage.cs:9` is placed in Participants (`structure.json:548`). Row n's Roles cut has nothing left to cut since mod-iv → F3. Row q misses `SpawnSkills` → F19 |

No blocking finding of either read is still open or answered wrongly. The one wrong answer is the non-blocking F14's "and only those" (F2).

### 1. Claims about the code

These claims are wrong at `e69b58872`:

1. **The stand-ins speak "Claude Code's dialects"** (design:194, :432, row b :797). `stub` has no `StructuredOutput()`: it gets the interface's null default (`Adapters.cs:1487`), and only `ClaudeCodeAdapter` maps `stream-json` (`:2212`). So `stub` is read by the plain capture (`Loop/Driver.cs:3827-3830`, `HarnessCapture.cs:55-78`). (F5)
2. **`LimitResumes` admits "by the wire alone"** (design:302-303; §17.2 F13). It also passes `adapter.Resumes` to `Continuations.Judge` (`DriverContinuations.cs:61-62`), which refuses where that is false (`Conversations/Domain/Continuation.cs:238`). (F12)
3. **A joined door reads its provider's home variable, own home and trust file** (design:567-569; D213 point 7). Today all three come from the door's own toolchain:
   - home variable: `Loop/Driver.cs:3010-3014`, `ChatRunner.cs:1493-1497`, `HarnessSelection.cs:569-580`
   - own home: `HarnessRoster.cs:1201-1212`
   - trust file: `Loop/Driver.cs:2293-2299`
   - `claude-code-acp` repeats the first and third for exactly this reason (`Adapters.cs:1930`, `:1938-1939`). (F2)
4. ***Agents names Roles* holds `Acp.cs` and `Adapters.cs` for row n's `ModelAsked`** (design:347, :369, :851). mod-iv moved `ActorAsked` and `ActorValue` to `Service/Domain/ActorAsked.cs:4`, `:11` (placed in Service, `structure.json:495`). The family now lists only `AccountRotation.cs` and `HarnessCapture.cs` (`:1241-1247`). (F3)
5. **On the native door, a refused model "passes to the next choice before any second prompt"** (row n :851; :873). Today the part ends with the account's line, the refusal is kept, and the *next* start's walk passes that account (`Loop/Driver.cs:3361-3380`). On both doors a refusal passes the account first, then the choice (`AgentOffers.cs:86-97`). (F11)
6. **`ModelRefused` is "kept in the start plan's re-ask"** (design:404). It is also kept in the offers store on both doors (`Loop/Driver.cs:3375`, `:3787`; `ChatRunner.cs:1869`). (F10)
7. **Citations moved by mod-iv** (design §0.2, §2.7, §5, §7, §17.3; "mod-iv not landed"). (F4)
   - `structure.json`: the families are now at `:896`, `:927`, `:984`, `:1056`, `:1175-1264`, `:1266-1273`; placements at `:185`, `:285`, `:548`; the *Service names Agents* family and `SessionLog`'s `AccountLine` edge are gone.
   - `Trees/SessionGroups.cs:975-983` / `:980-983` → `:754-762` / `:759-762`.
   - `Quests/Domain/Planner.cs:1938-1947` → `:1266-1275`.
   - `Sessions/Domain/Observation.cs:148-157`, `:564` → `:117-126`, `:533`.
   - `Sessions/Domain/ActorRun.cs:29-50`, `:57-58`, `:82-97` → `:16-37`, `:59-60`, `:89-100`.
   - Older off-by-ones: `Http/Program.cs:1293`, `:1324`, `:1967`; `AgentErrors.cs:281-323` and `:329-344`.

Everything else §17.3 lists holds as stated. The census baseline also holds at `e69b58872`: `SessionWire.Acp|Pipe` on 55 lines in 23 files, `.Wire` on 56 lines in 26 files.

### 2. The contract as round 2 states it

**Events-owned readings, and the sink composed by Loop.**
- Holds:
  - Every payload is Events' own or lower:
    - `DoorFailure` and `DoorWhere` move down from Agents (`AgentErrors.cs:22`, `:47`; placed in Agents at `structure.json:89`).
    - The builder `OfPrompt(AcpRefusal)` (`:50-51`) correctly stays with the protocol transport.
    - `OfferedOption` and `ContextUsed` replace `AcpConfigOption` and `AcpUsage`, and the windows travel as raw JSON.
  - `IReadingSink` is Events' (7) and is implemented by Agents (9), Participants (12) and Context (13); Loop (21) composes it. No upward edge.
  - The mapping onto D212 §4.2 (foundation:1148-1162) is right:
    - `part.opened` is never the handshake.
    - `turn.answered` comes only from the answer judge.
    - a limit gives `part.limited` and `account.waits`.
    - `child.seen` comes from the hub after Participants has placed the child.
    - a held permission request gives `call.held`.
    - `part.quiet` stays the watch's.
- Gap: the sink's list of parts misses Review, Course and the offers store (F10).

**`Applies` as COAGENT1 §3.3's effective configuration.**
- Holds: eight facts, the door added on top of coagent:339-344's list. That answers "a confinement measured on another OS or set of roots", and the conformance case moves each of the eight facts in turn.
- Gap: the roots must be compared as COAGENT1's *kind* ("the copy alone"), not as a literal path (F13).

**A door's own `Identity`, and what a joined door reads.**
- Holds for `DeclaredBy`, product and maker:
  - The family reads the door's toolchain (`ReviewerChoice.cs:24-33`), and `Same` matches by owner or by maker (`:36-38`). So a joined door that declares maker `Acme` stays in Claude Code's family by owner.
  - The trailer is chosen by the door's declared maker (`Adapters.cs:386-399`).
  - Two plugins' doors on one provider each keep their own word.
- Fails:
  - "Only those" is wrong for the home variable, own home and trust file (F2).
  - The commit attribution must stay a table keyed by maker, not a provider part (F9).
  - An `accountOf` that names a door breaks the identity rules (F15).

**`Spawning.Daoris` as a list; the tools' `PATH` before the program resolves.**
- Holds against the stub (`DAORIS_STUB_CONFIG_DIR`, `Adapters.cs:1688`) and against today's order (`Adapters.cs:1638-1642`, then `HarnessSelection.cs:553-560`). s-vi's child-environment proof answers inherited leaks.
- Fails:
  - The map: Platform cannot apply it (F1).
  - The list collides with a door's own declared variables (F6).
  - "Another provider's" variables fail OPENROUTER1's own reading (F7).

**`Reach` required, and row u.**
- Holds:
  - Required removes absent-as-everywhere.
  - The start layer is Participants' (foundation:2344), so its read of Agents' part points down.
  - A joined door is bound by its provider's `Reach`.
- Open: whether `Reach` is a pass in the walk or a refusal of the start, and what it reads for a start with no repository (F8).

**Offers per door, version and account.**
- Holds:
  - The store is keyed `<door>|<account>` with the version inside the reading (`AgentOffers.cs:33-39`), the door being `resolved.Name`.
  - It is judged per account (`:86-97`).
  - Native refusals are kept per account too (`Loop/Driver.cs:3375`), and the same read serves them.
  - Row n's case covers "one account refused, another of the same door tried".
- Gap: row n words the native pass wrongly (F11).

**Fit with the map.** These all point down:
- the readings;
- `Adoption` (door ids only; the walk stays in Roles);
- `IAgentFacts` (Knowledge);
- `IReleaseSource` and `ToolSpec` (Install);
- `ServerEntry` (Knowledge);
- the ports Course and Help implement.

Three places point up: F1 (Platform), F3 (`ModelAsked` over Service's `ActorAsked`) and F17 (row k's retry naming Course's `ResumeAsk`).

### 3. The rows

**Order and waits.**
- Every row lands after mod-ix.
- These rows name the lower cut they wait for: f (Install), l (Events), p and r (Knowledge), and u (D212 row 1q). The rows that cut *Agents names …* edges (h, l, m, n, o, q) land before mod-xxi.
- Three ordering points:
  - Row n's "cutting §2.7's Roles edges" has nothing left to cut since mod-iv (F3).
  - Row k moves the retry before m supplies the port it needs (F17).
  - Row q cuts all four *Agents names Plugins* files but has no mechanism for `SpawnSkills` (F19).

**Each row against "changes what it says and nothing more", and its proof.**
- a: The owner table lacks three cells (F2), and the door-named `accountOf` needs a held exception (F15).
- b: Its old-versus-new proof is right, but the stub's native dialect is misnamed (F5).
- c: The diff proof holds now that row b names the plumbing. The census cannot yet prove that the stand-in borrows nothing from another provider (F14).
- d and q: they would change a joined door's home variable, own home and trust hold (F2).
- h ⚖: Its table of the resolved program, the arguments and every variable is the right proof. Its placement is F1 and its ordering F6.
- j ⚖: Its installed proof misses a Codex background task (F16).
- l: The sink's parts (F10).
- n: F3 and F11.
- u ⚖: F8.
- s-vi ⚖: F7.
- t: Its older-build proof (the build before row a, a record naming `alt-stub`) is right. It spends an account without listing one (F18).
- e, f, g, i, m, o, p, r, s-i to s-v, s-vii to s-ix: each lands alone and proves what it moves.

**The ⚖ set** (h, j, o, p, q, u, s-v, s-vi) is right by §11's own criterion.

**The census.**
- Well specified:
  - The desktop is read semantically and the service by syntax trees.
  - The registration list is classified narrowly.
  - The permanent exceptions carry their decisions.
- Two gaps: it never checks a provider's folder naming another provider's types, and §6 misses a third service site (F14).

**The conformance suite.**
- The mandatory cases are right: applicability for each of the eight facts, refusal before a write, unread frames, and the pending files.
- The "no other provider's credential or home" case fails OPENROUTER1 as written (F7).
- The roots comparison needs F13.

**OPENROUTER1's prerequisites** (rows d, e, f, h, i, k, l, n, u) are enough for its stated reading: Claude Code's program, its dialects by reference, a key account, its own offers, limit words and reach. Through row u it also waits on D212's row 1q. Two things block it as written:
- Its own variables fail the conformance case (F7).
- Its first measurement, a brainstorm voice, has no repository for `Reach` to read (F8).

### 4. Findings

**F1 — blocking.** Daoris's variables are applied by Platform, which makes upward edges.

The design has Platform (subdomain 1) apply `Spawning.Daoris` from a `SpawnRequest` declared in `Agents/Contract/` (design:273-275, :312-316, :526-533, :330; D213 points 3 and 6). That application would name:
- `SpawnRequest` (Agents, 9);
- `Tools.Hand` (`Agents/Tools.cs`, placed in Install, `structure.json:109`);
- `GitWiring.Untoken`, `GitAuthor.Hand` and `GitLongPaths.Hand` (Workplace, `:323`, `:306`, `:318`);
- the identity variables' names that `Spawning.Roles` lists (`Adapters.cs:1659-1665`): `IntakeRoom.*` (Work orders, `Quests/IntakeRoom.cs:26`, `structure.json:442`) and `BrainstormVariables.*` (`Brainstorms/BrainstormTurns.cs:30`).

Also, "the `PATH` before the recipe's program is resolved" needs the resolver, `HarnessActions.WindowsShim` (Agents, `:38`, called from `HarnessSelection.cs:553-560`), to run in stage 3, after stage 2's recipe has been made.

Round 1 and the Opus reader's proposal kept the shell in Agents' transports. Codex's proposal had Platform apply values that their owners supply.

Smallest change — pick one and say it in §2.5, §2.7, §4.6 and D213 points 3 and 6:
- (a) Keep the two-step application in the transports' one shell in Agents. The executor in Loop composes the values from their owners, and the launcher only starts and contains. Or:
- (b) Make a Platform-owned request of plain name/value layers and removals, beside `ProcessRecipe`. Move the list's names and the shim's resolution down with it.

**F2 — blocking.** What a joined door reads (design:567-572, :854; D213 point 7).
- Today a door onto another agent's accounts reads the owner's accounts directory, key and login question (`HarnessRoster.cs:1204-1209`, `:1401`; `AgentsCommand.cs:641`).
- It reads **its own** declaration for three facts:
  - the home variable its spawn sets (`Loop/Driver.cs:3010-3014`, `ChatRunner.cs:1493-1497`, then `HarnessSelection.cs:569-580`);
  - own home (`HarnessRoster.cs:1201-1212`);
  - the trust preflight (`Loop/Driver.cs:2293-2299`).
- A manifest declares the first two (`PluginCatalog.cs:401`, `:410-411`).
- Under "`Accounts` (… home and key variables, … trust file …) and only those", take a plugin door joined to `claude-code` that declares its own `profileVariable`, or none (today a named account is refused for it, `HarnessSelection.cs:569-574`). It would be handed `CLAUDE_CONFIG_DIR` and Claude Code's trust hold. That is a behaviour change inside rows d and q, and §4.8's own declaration fields (design:559-560) would go unread. Row a's owner facts (design:782-784) include none of the three.
- Smallest change:
  - Add the three facts to `provider-owner.json`.
  - In §4.8 and D213 point 7, keep them as the door's own, as today, or make each differing cell an s-ix correction.
  - Add to row q a plugin door joined to `claude-code` with its own `profileVariable`, and one with none.

**F3 — not blocking, before row n.** mod-iv already moved `ActorAsked` to Service (`Service/Domain/ActorAsked.cs:11`, held on `SessionRecord.cs:18`). So row n's "cutting §2.7's Roles edges" cuts nothing, and §2.8 row j's "moves `ActorAsked` to `ModelAsked`" would put an Agents type on Service's record. Change:
- Let `ModelAsked` be Service's `ActorAsked`, which `AgentRun.ApplyChoice` takes from below.
- Drop the Roles cut and the rename.

**F4 — not blocking.** The citations mod-iv moved, and the older off-by-ones (§1 item 7), are stale. Change: one note in "Read at" and §17.3 saying mod-iv is in the merge, with the lines re-cited.

**F5 — not blocking, before row b.** `stub`'s native door is plain capture (`Adapters.cs:1487`, `:2212`; `Loop/Driver.cs:3827-3830`). Declaring Claude Code's native dialect on it would switch it to the structured capture and give it the native retry (§2.6), which changes behaviour in row b. Change:
- State `stub`'s native dialect as absent (plain lines).
- Name the by-reference dialects as `acp-stub`'s protocol ones.
- Add `WeekFixed` (`Adapters.cs:1730`) to the facts the stub mirrors.

**F6 — not blocking, before row h.** Two rules conflict whenever a declared variable is in the list: "No recipe names a variable of `Spawning.Daoris`" and "a door's own declared variables pass whatever their spelling".
- The wiring removes `GH_TOKEN` and `GITHUB_TOKEN` (`Platform/GitWiring.cs:47`, `:96-99`), which is a plausible agent key variable.
- Applied after the recipe, `Untoken` would strip that provider's own key. Today the key survives, because `Apply` sets the account's variables (`HarnessSelection.cs:535-538`) after the shell's `Untoken` (`Adapters.cs:1644`).
- Change: either refuse a declared variable that is in the list at registration (D23's error naming it), or apply the removals before the recipe and only the sets after it.

**F7 — not blocking, before rows s-vi and OPENROUTER1.** OPENROUTER1's own row runs Claude Code's program with `ANTHROPIC_API_KEY` empty and its key in `ANTHROPIC_AUTH_TOKEN` (`TASKS.md:188`).
- Its recipe therefore names Claude Code's registered key variable (`Adapters.cs:2395`).
- It needs `CLAUDE_CONFIG_DIR` (`:2368`) as its own home.
- So it fails the conformance case "no other provider's credential or home" (design:716), and s-vi's strip would remove its home.
- Change: define "another provider's" as registered by another provider *and not* declared by the selected one, and add a case where two providers share a variable.

**F8 — not blocking, before row u.** Row u (design:866) leaves two things open:
- Whether `Opened` is a pass in the plan's walk (D212 §2.4's one set of pass codes, foundation:439) or a refusal of the whole start. If Build's first choice is an `Opened` provider on a repository not opened to it, refusing the start loses a later choice that could have run.
- What `Opened` reads for a start with no repository (a brainstorm voice, `Adapters.cs:1555-1564`, which is OPENROUTER1's first measurement), or for one across repositories (Ask Daoris's room).

Change: name both in row u and its proof.

**F9 — not blocking, before row h.** The commit trailer switches on the declared maker (`Adapters.cs:388-398`, `:414-420`), a per-maker table (survey §1). A manifest cannot declare an attribution, so row q's case "a plugin's own provider with maker `Anthropic`" keeps today's trailer only if the attribution is read from that table by the door's maker. Change:
- Keep it a maker-keyed table, not a provider part (design:220, :845).
- Add a plugin door that declares `OpenAI` to the cases.

**F10 — not blocking, before row l.** The sink's parts (design:386-390) miss three owners:
- Review, which keeps `TaskStarted` and `TaskEnded`: `LongCalls` is placed in Review (`structure.json:588`), and row l cuts *Agents names Review*'s `RunningTask` edges (`:1232-1238`).
- Course, which `ConversationNamed` tells that held words went (`ChatRunner.cs:2896-2897`).
- The offers store, which keeps `ModelRefused` (§1 item 6).

Change: name these parts.

**F11 — not blocking, before row n.** State the native refusal as it is today (§1 item 5) in row n's proof and in "for a person's eye", and say that on both doors a refusal passes the account before the choice.

**F12 — not blocking.** Correct design:302-303 (§1 item 2): planning reads the transport-given eligibility to resume, and the open negotiates. Make the conformance fallback match.

**F13 — not blocking.** COAGENT1 says "the writable roots Daoris hands it (the copy alone)", and design:250 drops "(the copy alone)". Compared as literal paths, every session's copy would be a different configuration, so no measurement would ever admit. Change: compare each fact as COAGENT1 defines it, the roots by their kind.

**F14 — not blocking, before row c.**
- §9 fails only on sites outside the providers' folders. So `alt-stub` borrowing Claude Code's types would pass, even though §12 says such an edge needs a declaration.
- §6's "two places" in the service miss the doctrine layout's literal `claude-code` (`Core/Registry/RepositoryLayout.cs:39`).

Change: add a rule, with a planted case, that a provider's folder names another provider's types only where its registration declares the reference; and name that third site under the doctrine's exception.

**F15 — not blocking, before row a.** Today an `accountOf` that names a door (say `claude-code-acp`) keeps its accounts under that id. That can only be represented as a provider of that id sitting beside another provider's door of the same id, which breaks §2.2's identity rule that the conformance suite holds. Change: row a names it as a held exception until its s-ix correction.

**F16 — not blocking, before row j.** `initialize`'s `jetbrains.air` asks for `nativeSubagentSessions` and `asyncTasks` (`Acp.cs:1783-1796`). §15 measures whether Codex or dsh *emit* Claude Code's keys, but not whether they *read* this. Change: add it to §15, and add a Codex background task to row j's installed proof.

**F17 — not blocking, before row k.** The retry's guards and mark are `ResumeAsk`'s (`Loop/DriverContinuations.cs:1291-1297`; `ChatRunner.cs:2912-2917`), and `ResumeAsk` is Course's (`Conversations/Domain/ResumeAsk.cs:17`, `structure.json:176`). Moving the retry into Agents' native transport at row k, before m's port, adds an Agents → Course edge. Change: either give row k an Agents-owned port for the resume's state, or move the retry to row m.

**F18 — not blocking.** §11 (design:756-761) and §13 (:902-906) list the spending installed proofs as rows i, j, l, m and s-v. They leave out row t's installed proof and row l's latency measurement. Change: list both, with their account and bound.

**F19 — not blocking, before row q.** `SpawnSkills.cs` names `PluginCatalog` and `PluginFolders` (`structure.json:1228`) to copy plugins' skills. Row q's declarations source does not cover it. Change: name the port (or row o's `ISkillLayout` input) that cuts it.

19 findings, 2 blocking (F1, F2).

**Verdict: accept after named changes the steward can check without another read.**
- Take F1 (where Daoris's variables are applied) and F2 (a joined door's home variable, own home and trust file kept as today, three cells added to row a's table) in the design and in D213 points 3, 6 and 7.
- Take F3 and F4 now.
- Take the rest before the row each names: F5 before b, F17 before k, F6 and F9 before h, F16 before j, F10 before l, F11 before n, F19 before q, F8 before u, F7 before s-vi and OPENROUTER1.
