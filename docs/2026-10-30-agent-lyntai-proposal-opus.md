# Daoris's providers on Lyntai's agent-provider layer — the independent Opus reader's proposal

> AGENTLYNTAI1, phase 1 under D191 (its amendment of 2026-10-23 seats an independent Opus reader beside Codex). One of two sealed
> proposals, written from `docs/2026-10-30-agent-lyntai-brief.md` and `docs/2026-10-30-agent-lyntai-survey.md` alone, with the inputs they
> name, before any other proposal for this design was read. Daoris was read at `b96f93f9c` (the batch line `parent/batch-1029p`, rows a
> and b on it); Lyntai at its `master`, `9a41435c`, whose layer is the survey's `e010099f` unchanged, and at `2fb2dc7e`, the AG20 commit
> that carries its D234 and D235, read by `git show` and never written. Daoris's provider files are the survey's `e0c7b7e5b` but for the
> suite's case lists split into two classes with no case changed (`Tests/Agents/ProviderConformanceCases.cs`, `ProviderConformanceLive.cs`).
> Paths follow the survey's aliases (a bare path is under the driver library; `Tests/`, `Desktop/`, `Service/`; `Lyntai/…`, `Lyntai/Core/`
> its `src/Lyntai.Core/`, `Lyntai/Basic/` its `src/Lyntai.Providers.Basic/`); a Lyntai line marked *(AG20)* is `2fb2dc7e`'s, any other
> `master`'s. Read, not run: nothing here builds, spends, signs in or publishes. Daoris's rows are named L1 to L8 here and the requests to
> Lyntai Y1 to Y8, for the synthesis to name as it files them.

*Sealed 2026-10-30, before any other proposal was read.*

## 0. What I read, and where the survey is short

Daoris: the brief and the survey whole; D191, D22 and D213 whole (its points, the notes of rows a and b, the direction of 2026-10-29); the
provider design's §11 and §12 (`docs/2026-10-26-agent-provider-design.md:1001-1199`); the infrastructure design's §1 to §8
(`docs/2026-10-27-provider-infrastructure-design.md:1-300`); FOUNDATION1's §2.4 and Part B's table (`docs/2026-10-25-foundation-design.md:435-460`,
`:1674-1739`); `Agents/Contract/Provider.cs`, `Known.cs`, `Ports.cs`, `AgentRun.cs`, `ProviderParts.cs:100-133`; both trees'
`Directory.Packages.props` and the desktop's project references. Lyntai: its study whole, with §3.1 rows d and s-vii, §7 and §9 as
`2fb2dc7e` changes them; `docs/agent-provider-layer.md` whole; its D18, D19, D20, D70, D219, D221, D222, D225 to D228, D233, and D234 and
D235 *(AG20)*; *(AG20)* `Lyntai/Core/Agents/AgentProviderDescriptor.cs`, `IAgentProvider.cs`, `AgentSessionModels.cs`, `AgentAccountKeys.cs`,
`AgentPoolOptions.cs`, `AgentPoolModels.cs` and the declarations of `AgentPoolReducer.cs`, `AgentAccountPool.cs`, `AgentFailure.cs`,
`AgentStreamEvent.cs`, `IAgentSession.cs`, `Lyntai/Basic/Acp/AcpBuilderExtensions.cs` and `ClaudeCli/ClaudeCliBuilderExtensions.cs`;
`Lyntai/Core/Processes/IProcessRunner.cs`, `Lyntai/Basic/ClaudeCli/ClaudeAgentSession.cs:1-80`, `ClaudeAgentOptions.cs`, and the public and
internal types of `Lyntai/Basic/{ClaudeCli,CodexCli,Acp}/`. The survey holds where I lean on it; these are where it is short.

1. **D234 and D235 are written and not yet on Lyntai's line.** `2fb2dc7e` is no ancestor of Lyntai's `master` (`9a41435c`); its replay,
   `0cbd84c1`, is the one commit of `daoris/s-d553ab68` beyond `master`, its layer sources `2fb2dc7e`'s. Each decision binds on the owner's
   acceptance of that branch (its *Standing*). So AG20's ruling is not yet binding, and this design must stand under both answers (Q4). No
   row here waits for it.
2. **A built-in door's claims are reachable only through a container.** `ClaudeAgentDoor`, `CodexAgentDoor` and `AcpAgentDoor` are
   `internal static` (`Lyntai/Basic/ClaudeCli/ClaudeAgentDoor.cs:9`, `CodexCli/CodexAgentDoor.cs:9`, `Acp/AcpAgentDoor.cs:10`). The sessions'
   constructors are public (`ClaudeAgentSession.cs:41`, `CodexAgentSession.cs:54`, `AcpAgentSession.cs:64`), but the descriptor each door
   states is built only inside its registration (`ClaudeCliBuilderExtensions.cs:94-106` *(AG20)*; `AcpBuilderExtensions.cs:45-62`
   *(AG20)*). A host that constructs a session gets no claims and would have to write them, which is a copy. The driver takes no container
   (survey §11), so the adapter composes a private one (Q15) and Y8 asks for factories.
3. **A registration's environment is fixed for its life, and a turn carries none.** The ACP door's `environment` and the Claude door's
   are the registration's (`AcpBuilderExtensions.cs:45-67` *(AG20)*; `ClaudeAgentSession.cs:36-46`), and `AgentSessionOptions` holds no
   environment but the account's home (`AgentSessionModels.cs:59-64` *(AG20)*). Daoris's own variables change with every start (the role,
   the target, the session's tokens, the git wiring; D213 point 6). Until a turn can carry its own, each start composes a registration of
   its own door (Q10); Y5 asks for the general form.
4. **The runner yields lines, never a process.** Every member of `IProcessRunner` takes a command, arguments, an environment and a working
   directory and yields lines or a result (`Lyntai/Core/Processes/IProcessRunner.cs:23-95`); no process id or handle crosses it. Containment
   is therefore only what the runner itself does: Daoris's runner is its launcher (D201), never a wrapper of Lyntai's `ProcessRunner`.
5. **The pool's reducer is internal, and the pool's store optional.** `AgentPoolReducer` and its rows are `internal`
   (`AgentPoolReducer.cs:7-27` *(AG20)*); a host reaches its rules only through `AgentAccountPool`, whose store may be absent
   (`AgentAccountPool.cs:64-65` *(AG20)*). Q7's *the reducer alone* is not on offer without a request; the pool without a store is.
6. **D222's validation per version couples a host's agent updates to the library's releases.** A usage read is a version probe and then,
   for a validated version only, an exchange (`Lyntai/docs/agent-provider-layer.md:213-224`); the validated versions there are older than
   the signed-out ones captured beside them. Daoris pins and updates its agents' programs itself (row f), so a read through the layer would
   answer unknown from the first update the layer has not validated, until a release validates it (Q12, Y2).
7. **D234's one-variable rule meets a case Daoris keeps.** Every provider sharing a key must read the home through one variable, refused
   when the registry is built (`AgentAccountKeys.cs:10-31` *(AG20)*). Daoris's owner table keeps a plugin door joined to `claude-code` that
   declares its own `profileVariable` and is handed that, never `CLAUDE_CONFIG_DIR` (`docs/2026-10-26-agent-provider-design.md:1053-1054`,
   `:1141`). That is D234's own re-open condition. It binds only where the layer keys Daoris's holds, which this design defers (Q4, Q7).
8. **The two records mean different things by *external*.** D22 calls Daoris a sibling's first external consumer, outside its repository
   (`D22.md:12-15`); Lyntai's D18 relaxes its freeze while every consumer is one of the owner's own applications
   (`Lyntai/docs/DECISIONS.md:542-561`). Daoris is the owner's, so D18's disclosed break in a minor stays open while Daoris is the only
   consumer (Q16).
9. **The layer's Claude door has no place for the plugins Daoris hands, and one intent for three postures.** `ClaudeAgentOptions` carries
   a settings file, a server file, allowed tools, the skip-permissions switch, setting sources and strict servers
   (`Lyntai/Basic/ClaudeCli/ClaudeAgentOptions.cs:6-40`); Daoris's native spawn also hands the plugins composed for it
   (`Agents/Contract/Ports.cs:35-36`). The door's `Write` intent is `--permission-mode acceptEdits` (`ClaudeAgentArgs.cs:83-93`), which can
   carry at most one of Daoris's three postures (`ProviderParts.cs:119-120`). The native door's move meets both (Q11, Y4, Y7).
10. **FOUNDATION1's *j after h, i and o* names its own rows.** Part B's order (`docs/2026-10-25-foundation-design.md:1679-1680`) puts 1j
    after 1h, 1i and 1o (conversations and driven lines onto the course, words never held outside `said`), not after the provider design's
    h, i and o; survey §9's sentence reads either way. It matters for Q20, since this design builds the provider design's row i nowhere.

## 1. The shape

### 1.1 In one paragraph

**The layer is what a door is; Daoris is what is done with doors.** A door in the layer's sense, the process started for a turn, spoken
to and read, with its descriptor and claims, its events, its typed failure with restriction and delivery, its usage reading and the
account home it is handed, is Lyntai's, built and released there, and Daoris writes none of it again. What a host does with doors stays
Daoris's: its ids and records, the account rule, the walk and the cool-off, the start plan, the shell that completes a spawn with Daoris's
own variables and the launcher that contains it, the readings in Events and their sink, the live run until the layer has one, reach, the
programs' pins, the rule language, connectors, plugins and the meter. `Agents/Contract` stays Daoris's host contract and shrinks. Each
Daoris door is either **layer-run**, bound to one Lyntai provider and door and reading every fact the layer states from that provider's
descriptor, or **Daoris-run** on today's transports, unchanged. A door becomes layer-run only when a Lyntai release carries every reading
and operation Daoris uses on it today, shown by the conformance suite case for case, old against new; until then nothing of its transport
is refactored here, so nothing is built twice. The adapter is one folder, `Agents/Layer/`, and the census and `StructureTests` hold every
Lyntai type there and in the providers' folders.

### 1.2 Build what calls a door; never rebuild what is a door

The direction rejects building rows c to u here and moving them later. Read against the code, the provider design's rows are of two kinds,
and the line between them is the split:

- **Rows that change how Daoris calls a door**: d (the owner once), g (the doors as abilities), h's shell and launcher, l's sink and
  vocabulary, m's executors holding `AgentRun`, n's offers store, o's rules, p's plan, q's catalogue, r's sheet, u's reach, and every
  correction. They are needed whatever runs underneath, and they are what makes a later door move invisible to the callers. They stay
  Daoris's and build as accepted.
- **Rows that change what a door does inside**: i (the protocol transport and its dialects), j's seam, k (the native transport and Claude
  Code's dialect), l's four stream-json readers made one, and e's usage readers and stream words moved into folders. The layer has its own
  of each (survey §8), and Daoris's would be retired the day its door moves. They are not built here; Daoris's transports stay as they stand
  until their door moves, and then go.

### 1.3 Not a copy

D213's note rejects a copy of the layer's types inside Daoris, which would leave two contracts to keep equal. Three rules keep the adapter
from being one, each held by a test (Q13, Q14):

1. **No Daoris type mirrors a layer type.** Daoris declares no record that restates an ability, a failure, a restriction, a hold or an
   event of the layer's. Where a Daoris type already holds the same fact for Daoris-run doors (`KnownBy`, `Transport`, `Eligibility`,
   `DoorFailure`), the adapter maps the layer's value into it at the boundary, and nothing above the adapter sees the layer's.
2. **A layer-run door declares nothing the layer states.** Its record holds the binding and the host's facts; the eleven ability names, the
   home variable and the account key the layer files under are read from the bound descriptor. A registration that declares one of them
   beside a binding is refused by `ProviderFilesTests`, naming the fact.
3. **One home per fact, counted.** The census counts a fact a layer-run door restates as a site with no allowance, so a copy cannot land
   quietly.

### 1.4 Where each piece sits

| Piece | Home | Named by |
|---|---|---|
| A door's descriptor, claims, turn, events, failure, restriction, delivery, usage reading, account mapping, agent key | Lyntai (`Lyntai.Agents`, `Lyntai.Providers.*`, `Lyntai.Processes`) | `Agents/Layer/` and the providers' folders alone |
| The binding, the per-start composition, the runner over the launcher, the run over turns, the claims, accounts, failures and readings translated | `Agents/Layer/` (new) | Agents' registry and executors' ports |
| A layer-run provider's registration (its program, arguments, home variable, key) | `Agents/Providers/<Id>/` | `Agents/Registry/Providers.cs` |
| Ids, records, the registry, the host's parts, `Known<T>`, `AgentRun`, the census, the suite | Daoris, as today | as today |
| The account rule, the walk, the cool-off, the start plan, the shell, the launcher, the sink | Daoris (Agents, Roles, Platform, Events, Loop) | as today |

## 2. The questions, by number

### Q1. Row by row

Every layer part below is asked by a quest that states the need with the asker struck, never written into Lyntai; §3 lists them. *Sites*
are the survey's §7 counts at `e0c7b7e5b`.

| Row | Placing, and why by the generic test | The layer's part | Sites | Against the study (survey §9) |
|---|---|---|---|---|
| **c** the third stand-in | Split, built as **L2**. The folder, the program answering the scenario contract in its own vocabulary and its pending file are Daoris's meter (D213 point 12); its doors are registered through the layer, the protocol one on the generic ACP door, the native one as a host's own provider written in its folder | AG8 (built); AG15 (*an application running a third ACP agent needs to teach the door that agent's extensions, error data and limit frames without a fork*) and AG16 un-pend its own-vocabulary cases | 0 | agrees, and adds that the stand-in is the first layer-run provider, so it also proves the adapter |
| **d** ⚖ the owner once | Daoris's. Which accounts a door runs as, read once by Daoris's readers, with the one-level `accountOf` and its `Lent` part, follows this host's plugin catalogue; Lyntai's §9 files `Lent` as the host's | D234 (built, not yet binding): each layer-run door registers with `agent:` set to its Daoris account key | 3 and the tools' 4, kept behind the registry's one reader | agrees: D234 is the layer's half; the readers stay Daoris's |
| **e** the accounts' readers | Split three ways. (1) The usage question is the agent's fact: the layer's, as **L3**. (2) Limit, window, refused and error words read from a door's own stream: the layer's failure reader for a layer-run door; Daoris's, unrefactored, for a Daoris-run door until its move. (3) The floor, the sign-in and login question, `ClaudeTrust`, the key variable read once, `ToolHomeLeftovers`, `WeekFixed`, D179's weights: the host's (each a deployment's choice or Daoris's file), built as accepted | AG9 (built); Y2 (*an application that keeps an agent at a version it chooses needs the usage reading at that version, or a statement it can act on, without waiting for a library release*) | 53: the usage readers' share deleted at L3; the words' share deleted at each door's move; the rest kept or moved into the providers' folders (the split of the 53 is unmeasured) | departs: the study files nothing; §0 item 6 is the gap Y2 asks |
| **f** the program | Daoris's: release sources, a chosen version and a location are what D20 refuses (`Lyntai/docs/DECISIONS.md:576-592`) | none; a layer door is handed the pinned program's path as its command (Q12) | 52, kept, into Install's sources as accepted | agrees |
| **g** the doors as abilities | Daoris's: the callers' tests of `Wire` become reads of a door's abilities; for a layer-run door the eleven names read through the binding | none | 113, kept | agrees |
| **h** ⚖ the spawn | Split. The shell with Daoris's variables in today's order, `ISessionGit`, the tools' `PATH`, the carriers and the launcher are orchestration: Daoris's, and the launcher is also the layer's runner (Lyntai's D226) | AG17 (*an application that runs an agent on a key needs an account that is a set of variables, and the provider's own list of what it reads*); Y5 | 15, kept, into the shell and the folders | agrees on AG17; adds the runner and Y5 (§0 items 3, 4) |
| **i** the protocol transport | The layer's, not built here: refactoring `AcpSession` into a composite would build it twice | AG15; and for any protocol door to move, Y3 | 6, kept until L6, then deleted | agrees on AG15; adds Y3 as the move's precondition |
| **j** ⚖ each protocol door its dialect | Split: the seam the layer's (AG15); the set each door reads the host's choice, named in its registration when it moves | AG15 | 6, as i | agrees |
| **k** the native transport | The layer's, not built here | AG10, AG18; for a native conversation, Y3 | 5, kept until L5 and L7, then deleted | departs: the layer's Claude door is one process per turn fed its prompt (`Lyntai/Basic/CliAgentLoop.cs:23`), which serves a driven start and not a conversation held on stdin (`Ports.cs:20-21`, `:52-56`), and reads no subagent's lines |
| **l** the readings and the sink | Split. `WireReading` and `IReadingSink` in Events, the sink composed in Loop, every payload Events' own (D213 point 5): Daoris's, built as accepted; the translation of a layer-run door's events into them in `Agents/Layer/`. Not built: the four stream-json readers made one | AG10 child, AG18 task, AG19 held request, AG12 refused model | 2, kept | agrees; adds that no door loses a reading, since none moves before its release carries them |
| **m** the session's life | Split. The executors holding `AgentRun`, the native retry behind it through Course's `IResumeState`, the protocol resume once, `IConversationHome`: Daoris's | Y3 (*an application that converses with an agent needs a run kept across turns*) | 0 | departs: the study leaves it an option; Daoris's built and measured run is the trigger D233 names, so Daoris asks |
| **n** models | Split. D205's offers store, judge and walk, asked, carried, acknowledged and reported apart, the re-ask through `IChoiceReask`: Daoris's | AG11 (the ACP door carries a model or refuses it), AG12 (a refused model read with its restriction) | 3, kept | agrees |
| **o** ⚖ permissions, skills, hooks | Daoris's: D72's grammar, the lineage hook, the trust preflight and the skills are the product's | AG19; Y4 (*an application that lets a person choose how freely an agent acts needs the door to carry the agent's own permission mode, or refuse one it cannot*) | 12, kept, into Claude Code's folder | departs by Y4: the study places postures nowhere (§0 item 9) |
| **p** ⚖ connectors | Daoris's: the session's identity in every entry is this host's | none; a layer door is handed the plan's entries as `AgentMcpServer`s | 4, kept | agrees |
| **q** ⚖ plugins' doors | Split. The manifest, the catalogue, the declaration with no field for what a manifest may never claim, `Lent` and the reserved names are Daoris's; a plugin's protocol door runs at L6 on the generic ACP registration, whose claims are fixed `Declared` | none new (AG15 and AG19 grow the registration) | 3, kept | agrees |
| **r** the page, service, tools | Daoris's | none | 1 and the service's 3, kept | agrees |
| **s-i, s-iii, s-iv, s-v ⚖, s-viii, s-ix on** | Daoris's | none | 0 | agrees |
| **s-ii** | Daoris's rule on Daoris's record; it agrees with `AgentHandover.Decide`, which is not called, since it keys on a transcript's source Daoris does not record | none | 0 | agrees |
| **s-vi** ⚖ | Split: the strip Daoris's; each provider's declared read names the layer's | AG17 | 0 | agrees |
| **s-vii** ⚖ | Daoris's; the layer's pool half is D234 | none | 0 | agrees |
| **t** | Daoris's, as **L8**: it also retires each transport whose doors have all moved | none | 48, deleted | agrees |
| **u** ⚖ reach | Daoris's: admission, which two hosts answer differently | none | 0 | agrees |
| **OPENROUTER1** | Split, as **L4** by its own design. Its key where D172 point 3 keeps keys, its offers, its reach, maker independence and D125's cool-off are Daoris's; its executor is Claude Code's program at the gateway through the layer's Claude CLI door | AG17 (a key per turn), AG10 | n/a | agrees with Daoris's reading; the HTTP door is not its executor (Q19) |

### Q2. The host's line

| Fact | Holds it | Why |
|---|---|---|
| The floor of a usage read | Daoris, per provider (s-iii) | how often to pay a vendor's start is a deployment's call (D222's bound; the study's row e) |
| A default cool-off for an unstated reset | Daoris: D125's sixty minutes, unchanged | rule 7: Lyntai's D227 holds such a hold until cleared and offers `UnknownProbeAfter` as the host's option (`AgentPoolOptions.cs:24-28` *(AG20)*). Daoris takes no pool now (Q7), so nothing changes; when AGENTINFRA1's stage B takes it, the option is D125's value for a legacy unknown timed limit and never for an owner action, and the one leased attempt D227 grants where D125 frees the account is that stage's decision |
| The variables stripped from a spawn (s-vi) | Daoris decides; the layer states what each provider reads (AG17) | which to strip is policy; the read list is a fact of the provider |
| Interactive | Daoris, each door's declaration | the layer has no conversation a person types into |
| Reach | Daoris, in the plan's walk (row u) | admission |
| The rule language | Daoris (D72), handed to a layer-run Claude door as its settings file | the product's grammar |
| Release sources and pins | Daoris (row f) | D20 |
| Where holds are kept, and by how many processes | Daoris: `cooling.json`, several processes on one home | the layer's pool is one process (D219, `agent-provider-layer.md:210-212`); the home is not (`docs/2026-10-25-foundation-design.md:2731`) |

### Q3. `Agents/Contract` onto the layer

**An adapter, never a replacement.** A replacement would make the layer's descriptor the contract and drop or request Daoris's parts. It
fails three ways: Daoris's records hold door ids and account keys by kind, compared without case, and shipped builds read them
(`ProviderId.cs:62`; `RecordIds.cs:4-61`), where the layer's ids are ordinal and a second registration replaces the first
(`IAgentProvider.cs:133-146` *(AG20)*); the host's parts fail the generic test, and Lyntai's §9 files none of them, so they would be lost;
and Daoris's executors hold a live run the layer has not got. It is not the rejected copy, by §1.3's three rules.

| Daoris types | After |
|---|---|
| `ProviderId`, `DoorId`, `AccountKey` | stay. A layer-run door's Lyntai provider id is its `DoorId` in the spelling Daoris's registry resolves; its Lyntai door `cli` or `acp`; its key the descriptor's `Agent` (Q4). Never stored by the layer |
| `Provider` | stays, the host's record of an agent |
| `AgentDoor` | stays and gains the binding, a `Known<LayerDoor>` naming the Lyntai provider and door. For a layer-run door `Spawn`, `Dialect` and `Tables` are stated absent, and the layer-named abilities and the home's variable read through the binding |
| `Transport`, `TransportOf` | stay; `cli` reads as `Native` and `acp` as `Protocol`, any other kind refused at registration until a design gives it an executor; `TransportOf` goes at L8 |
| `KnownBy`, `Known<T>` | stay; a layer claim is read into a `Known<T>` (Q5) |
| `Eligibility` | stays; equal to the layer ACP door's resume, unknown and negotiated (`AcpAgentDoor.cs:26-27`) |
| `AccountPool` | stays: a key's providers. Not the layer's `AgentAccountPool`, the walk with its holds (Q21) |
| `AgentRun` | stays. For a layer-run door, `LayerRun` implements it over one `StreamAsync` per turn: open and send are a turn, resume hands the token, stop a turn cancels, close does nothing; steer, apply a choice on the same session and stop a task are refused `unsupported` before a write until Y3 |
| `Reach`, `ProviderIdentity`, `DoorIdentity`, `ProgramFacts`, `LentPart`, `VocabularyPart`, `PortRef` | stay: the host's (study §5, §9) |
| `PoolFacts`, `AccountsPart` | stay; the key variable read from the provider's declared names once AG17 is released; the login question and sign-in the host's |
| `TablesPart`, `DoorTablesPart` | stay for Daoris-run doors. For a layer-run door the failure is the layer's (Q8); a sentence Daoris recorded and the layer misses stays as a provider table entry naming its request, until a release carries it |
| `UsagePart`, `IUsageQuestion` | `UsagePart` holds the layer's `IAgentUsageSource` for an agent whose usage moved (L3) and keeps the freshness, Daoris's; `IUsageQuestion` retires there |
| `DoorHome` | stays; `Variable` read from the descriptor's account mapping for a layer-run door; `OwnHome`, `TrustFile` and `MustExist` Daoris's |
| `PosturesPart` | stays, in the agent's words; a layer-run door maps each onto the door's intent and its own options, and a posture with none mapped refuses the start, as today (D213 point 6), so a door whose postures the layer cannot carry does not move (Y4) |
| `AbilitiesPart` | stays; `Interactive` and `TakesSettings` Daoris's; `Resumes` read as the layer's `resume` |
| `ISpawn`, `IDialect`, `INativeDialect`, `IProtocolDialect` | stay for Daoris-run doors, absent on a layer-run door, retired with the last door of their kind (L8) |
| `IChildVocabulary`, `IConversationHome` | stay: the lineage hook's lines and the calls' keys, and what Daoris copies between account homes, are the host's; a layer-run door's stream signals are the layer's child readings (AG10, AG18) |

The registry's nine types stay (`ProviderRegistry`, which gains the layer view; `ResolvedId`, `AccountAgent`, `IdKind`, `RecordIdField`,
`RecordIds`; `Providers`, each layer-run provider a line; `ProviderRegistration` and `LegacyDoor` until L8). New in `Agents/Layer/`: the
composition of a start's registration (private container), `LauncherRunner` (Q10), `LayerRun`, and the translations of claims, accounts,
failures and events.

### Q4. Provider, door and account

Daoris's provider keeps its doors (D213 point 1). Each layer-run door is one Lyntai provider with one door (Lyntai's D225), registered under
the Daoris door's id, with `agent:` set to the door's Daoris account key (D234):

| Daoris door | Its key | Its registration when it moves | Moves at |
|---|---|---|---|
| `claude-code` (native) | `claude-code` | `AddClaudeCliAgentSession(id: "claude-code", agent: "claude-code", command: the pinned program)`, door `cli` | L5 driven starts; L7 conversations |
| `claude-code-acp` | `claude-code` | `AddAcpAgentSession(id: "claude-code-acp", command: the adapter's program, accountHomeVariable: CLAUDE_CONFIG_DIR, agent: "claude-code")` | L6 |
| `codex-acp` | `codex` | `AddAcpAgentSession(…, accountHomeVariable: CODEX_HOME, agent: "codex")` | L6 |
| the `codex` holder | `codex` | no door: the layer's Codex CLI provider is registered for its usage reading alone, and its session is never opened | L3 |
| `dsh` | `dsh` | `AddAcpAgentSession(id: "dsh", …)`; its home variable unmeasured | L6 |
| `stub`, `acp-stub` | `stub` | stay Daoris-run: they rehearse Claude Code's and Codex's paths on today's transports (D213's note of row b, part 3) | L8 at the earliest |
| `alt-stub`, `alt-acp-stub` | `alt-stub` | the native door a host's own provider in its folder; the protocol door `AddAcpAgentSession`, its own home variable, `agent: "alt-stub"` | L2 |
| a plugin's harness | its `accountOf` verbatim, else its own | `AddAcpAgentSession(id: the harness, …, accountHomeVariable: its profileVariable, agent: its key)`, but `agent:` its own id where its variable differs from its key's | L6 |

**Every id a shipped record holds resolves as today.** Daoris's records store Daoris's ids only; the layer's ids for doors are the same
strings, derived and never stored, and the layer's store holds nothing Daoris reads, since no pool is taken (Q7). The 22 fields of
`RecordIds` resolve through Daoris's registry by their kind, and an id a build lacks reads as an unavailable door (D213 point 8).

**Case-blind against ordinal.** Daoris's registry resolves an id without case to its registered spelling, and only that spelling reaches
the layer's registry, which compares ordinally (`IAgentProvider.cs:137` *(AG20)*). The layer never meets another spelling.

**Refused against replaced.** Daoris's registry refuses a twice-registered id or door (`ProviderRegistry.cs:30-73`) before the adapter
builds the layer's registry from the set it validated, so the layer's replacement (`IAgentProvider.cs:133-146` *(AG20)*) has nothing to
replace. A planted pair of registrations of one id through the adapter is refused by Daoris, naming both.

**Under AG20's two answers.**
- **Yes, D234 as written, once its branch is accepted.** The table above. The adapter computes one exception before it registers: a door
  whose home variable differs from its key's (§0 item 7) gets `agent:` its own id, so D234's refusal never fires, while Daoris's records
  keep that door's accounts under the shared key. Harmless while Daoris keeps its account rule; it is D234's re-open condition the day
  Daoris takes the pool, and is asked then (§3, not filed).
- **No, D225 stands.** Each layer-run door registers with no `agent:`, and the adapter hands every account Daoris's rule picked as
  `AgentAccount.Provider` equal to the door's own id, so the layer's refusal of another provider's account never fires. Nothing is lost
  now, since Daoris keeps its walk and its cool-off; what waits is only a later use of the layer's pool across one agent's doors.

### Q5. What is known

`Known<T>` stays for every fact the layer does not state, and a layer claim is read into one, never declared beside it. The mapping:
`Supported` reads as the value true with its source, `Unsupported` as false, and `Unknown` as stated absent; the source maps one to one
(`AgentAbilitySource` and `KnownBy` hold the same three, survey §1); the evidence is carried verbatim; `Applies` names the door and the
claim's `AppliesTo`, the one version the layer can state (`AgentProviderDescriptor.cs:64-65` *(AG20)*).

The two records read silence differently: Daoris reads an absent fact as today's behaviour (D53), the layer reads `Unknown` as *not
promised* (`AgentProviderDescriptor.cs:3-8` *(AG20)*). For a layer-run door the two meet at the move: a door moves only when each ability
Daoris relies on for it today reads `Supported` through the layer, or reads `Unknown` where today's behaviour does not rely on it; the
conformance suite's ability cases on the layer view show which. So D53's reading never promises something the layer does not.

What the descriptor cannot state lives on the Daoris door as today: interactive, taking settings, offers, the postures' words, reach, the
lineage hook's lines, the conversation home, and the eight-fact applicability of a measurement (COAGENT1 §3.3), which Lyntai declined to
file (study §3.1 row a; D235's list of what stays the host's, survey §13). **A run is admitted by its own evidence** (D213 point 2):
`LayerRun` admits an operation by the bound door's claim (`AgentDoor.SupportFor`, `AgentProviderDescriptor.cs:99-101` *(AG20)*) and a
negotiated one by the turn's own answer (the ACP door refuses a resume before the prompt when the agent states neither method,
`agent-provider-layer.md:118-120`); a claim read at another opening narrows and never admits.

### Q6. The run

| Daoris today | The layer today | Runs on the layer when |
|---|---|---|
| The protocol door: one process across a session's turns, a model set and a posture set on the session, words queued, a request held for the person, background tasks, a stop that keeps the conversation (`Agents/Acp.cs:299`, `:330`, `:639`, `:2077-2091`, `:2786`, `:3105`, `:3444`) | a process per turn, steering unsupported, no mode and no option set (`Lyntai/Basic/Acp/AcpAgentSession.cs:14-29`; survey §8) | **L6**, per door, once Y3 (for the ACP door), AG11, AG12, AG15, AG19 and Y4 are released |
| The native door's driven start: one process, its prompt in, its stream read (`Ports.cs:17-18`) | the Claude CLI door: one process per turn fed its prompt (`CliAgentLoop.cs:23`) | **L5**, once AG10, AG18, Y4 and Y7 are released; the door's conversations stay Daoris-run |
| The native door's conversation: stdin held open, a message and an interrupt framed (`Ports.cs:20-21`, `:52-56`) | none | **L7**, once Y3 holds for the CLI door |
| The stand-ins `stub`, `acp-stub` | — | not before L8 (Q4) |

**Y3 is asked, and its trigger is Daoris's measurement.** D233 re-opens the long-lived run on a host that needs a word taken mid-turn, with
a first measurement of which doors accept one (`Lyntai/docs/DECISIONS.md:6701-6704`). Daoris has built and measured two: Claude Code's
protocol door queues words (`Acp.cs:2077-2091`) and Claude Code's native door takes a framed message and an interrupt on held stdin
(`Ports.cs:52-56`). Codex's protocol adapter is unmeasured; the quest names it so, and its first measurement is a model-free one on frames
Daoris has recorded where it has them, else an installed one that spends, the owner's (Q22).

**Every step keeps each door's life as today.** No door moves before its release carries everything it does; between moves a Daoris-run
door is untouched. Each move shows the five (start, a turn, a resume of its own conversation, a cool-off, an end) on the stand-in through
the layer and then on the install (Q23). A door split by run kind at L5 states which kind is layer-run, and the suite holds each live case
on the kind it drives.

### Q7. Accounts, holds and the walk

**Daoris takes the restriction vocabulary only, as a layer-run door's terminal reads it; not the pool, and not the reducer**, which is
internal (§0 item 5).

Why not the pool now: it is one process, and two processes on one pool need a store capability it has not got (D219's re-open condition),
while a Daoris home runs several; it would be a second hold store beside `cooling.json`, which shipped builds read; it picks a seat per
turn, a walk inside the layer, where D213 point 3 says a provider never walks accounts and D212 §2.4 that no caller walks again after the
plan; and its released hold buys one leased attempt (D227), where D125's cool-off frees the account, which is a change of behaviour
AGENTINFRA1's stage B is the place to decide.

D197 (one pool), D205 (ordered choices), D206 (a limit resumed) and D125 (sixty minutes) hold as they stand: the walk is the start plan's in
Roles, the rule `LineAccount` (`Agents/LineAccount.cs:201`), the cool-off the roster's in `cooling.json` keyed by owner and profile
(`Agents/AccountCooling.cs:10`, `:29`). A layer-run door's limit reaches it so: the terminal's `AgentFailure` with its restriction becomes a
`DoorFailure`, `AgentErrors.Read` reads it as a limit, and the roster cools the account under Daoris's key and the profile, its end the
restriction's `ResetAt` or `RetryNotBefore` where stated (`AgentFailure.cs:66-97` *(AG20)*), else D125's sixty minutes. An owner action
(a credit refusal) is handled as today's credit case is until stage B, and is never written into `until` as a made-up date
(`docs/2026-10-27-provider-infrastructure-design.md:269-271`). A cooling entry an older build wrote is read as today: no field is added.

AGENTINFRA1's restriction against the layer's: cause and subject agree member for member (study §2); the subject key is the layer's internal
scope string (`AgentPoolReducer.cs:33-41` *(AG20)*), so Daoris keeps its own (owner and profile, model, endpoint); the owner action's reason
the layer lacks (survey §8), Y6; the id and revision sit on the layer's `AgentHold` (`AgentPoolModels.cs:109-111` *(AG20)*), which Daoris
has none of until it takes the pool; observed time, evidence and dialect version stay Daoris's, the layer's `By` naming the entry that
decided (`AgentFailure.cs:126` *(AG20)*).

**Later, in AGENTINFRA1's stage B.** In the isolated home, one process: the layer's pool over a store Daoris supplies (an `IKeyValueStore`
over a new desktop-store family), `UnknownProbeAfter` D125's value for a legacy unknown timed limit, `cooling.json` untouched. The shared
home waits for a pool across processes, which is not asked now (§3).

### Q8. Failures and words

**One reading stays `AgentErrors.Read` with D210's precedence**, the door's table before the provider's, on every path. For a layer-run door
the door's table is the layer's: `SessionEnded.Failure` (its kind, restriction, delivery and `By`) is that door's reading of its own fields
(`agent-provider-layer.md:166-171`); the adapter turns it into a `DoorFailure` (the four kinds agree one to one, survey §8;
`AgentFailure.cs:8-22`), which `AgentErrors.Read` reads first. Daoris's provider tables then apply only where the layer read nothing (a
fatal failure decided by no entry), which is a sentence Daoris has recorded for that agent and the layer's one classifier lacks.

**A recorded sentence reaches the layer as a request**, with the sentence and where it was recorded, since the classifier is held both
ways on recorded sentences (`agent-provider-layer.md:166-169`). Its Daoris table entry names the request until a release carries it, and
is then deleted. Meanwhile the four kinds stay one reading by that order, shown by a conformance case per kind on the stand-in through the
layer and on `stub` through today's transport, each read as the same kind.

**Delivery.** The layer's `AgentDelivery` is read by Daoris's retry: never sent may go again, taken resumes, uncertain holds. The layer's
`RetryingAgentSession` is never registered, so one layer, Daoris's, owns the budget (Lyntai's layer §5.3; AGENTINFRA1 §5).

### Q9. Readings

Events reads nothing of the layer. `Agents/Layer/` translates a layer-run door's events into Daoris's readings and writes them to the same
sink as today's transports; FOUNDATION1's hubs and triggers mint from what the sink keeps, as before (D213 point 5).

| The layer's event (`AgentStreamEvent.cs:7-86` *(AG20)*) | Daoris's reading |
|---|---|
| `SessionStarted` | the conversation named |
| `TextDelta`, `Thinking` | the agent's words and thinking |
| `ToolCall`, `ToolResult` by call id | a tool call and its result; a child's lines once AG10 adds the parent's id |
| `PlanUpdated` | the plan |
| `UsageLive`, `UsageFinal` with its model, `UsageReading` | usage; the model reported |
| `SessionEnded` with its failure | the turn's end; a failure through `DoorFailure` (Q8) |

What the layer lacks waits for its row: a child seen, said and ended (AG10, AG18), a background task (AG18), a held request (AG19), a
refused model (AG12). Provenance and replay are not asked: the layer emits only what its wire carries (`agent-provider-layer.md:150-151`)
and drops a load's replay (study §3.1 row l), so a layer reading is stamped *reported* and never *replayed*. Until those rows land, Daoris
keeps reading on today's transports every door that needs them, because no such door moves (Q6).

### Q10. The spawn and containment

**The runner is the launcher.** `Agents/Layer/LauncherRunner` implements `IProcessRunner`: `StreamLinesAsync` and `ExchangeLinesAsync`
(which the ACP door and the usage reading need, Lyntai's D226) start through Platform's launcher (`Platform/AgentStarts.cs:18`), which
contains the process as it contains every agent (D201); `RunAsync` serves probes; `StreamBytesAsync` keeps its refusing default;
`CommandExists` answers from Install's toolchain. Lyntai's `ProcessRunner` is never used.

**Today's order holds** (D213 point 3). The shell composes what it composes today before the layer door is built: the program resolved
against the tools' `PATH` and handed as the registration's `command`, and Daoris's variables (`Spawning.Daoris`, the session's tokens
cleared, the git wiring through `ISessionGit`) in today's order as that start's registration `environment`, a null value removing an
inherited one (`IProcessRunner.cs:8-11`). The door then adds its own carriers (its arguments, the settings and server files, the account's
home in its variable), after the program is resolved, as today. The runner hands the result to the launcher unchanged.

**One registration per start** (§0 item 3) until Y5 lets a turn carry its own environment; then the composition is made once. **s-vi's
strip** is computed from Daoris's `VocabularyPart` (the measured reads) until AG17, then from each provider's declared names, and applied as
a null for each stripped variable in the start's environment. The proof is row h's table at each door's move: the resolved program, its
argument vector and every variable equal to today's.

### Q11. Models, postures, rules, skills, connectors and plugins

- **Models.** D205's offers per door, version and account stay in Daoris's store. The model is carried on both CLI doors already
  (`AgentSessionModels.cs:33-39` *(AG20)*), on the ACP door from AG11; a refused one reads with its restriction from AG12. On a layer-run
  door the re-ask through `IChoiceReask` comes at the next turn's open, which is the native door's behaviour today (the next start) and not
  the protocol door's (the same session), so protocol doors wait for Y3.
- **Postures** stay in the agent's words, mapped onto the layer's intent (`AgentToolPolicy`, `AgentSessionModels.cs:5-28` *(AG20)*) and
  the door's own options (`SkipAllPermissions`, `ClaudeAgentOptions.cs:15-25`). Y4 asks for the agent's own modes. ⚖: the mapping hands the
  agent exactly today's posture, and a posture with none mapped refuses the start, so a move never widens.
- **D72's grammar and the lineage hook** stay Claude Code's provider's, in Daoris (row o), handed to a layer-run Claude door as its
  settings file (`ClaudeAgentOptions.SettingsPath`).
- **Connectors.** `ConnectorPlan` and CONNECTORS1 stay Daoris's; a layer-run door is handed the plan's entries as `AgentMcpServer`s, the
  session's identity in each (`AgentSessionModels.cs:53-57` *(AG20)*), and the door refuses one it cannot render before a spawn
  (`ClaudeAgentSession.cs:56-61`), which keeps D213 point 6's refusal. The Claude door renders the entries into a temporary file it deletes
  after the turn (`ClaudeAgentSession.cs:56-58`), where today's helpers' file is written under the home; L5's spawn table decides between
  handing Daoris's own file (`McpConfigPath`) and the layer's rendering. ⚖.
- **Skills and plugins** stay the host's (`ISkillLayout`, the catalogue); a native door's plugins need Y7. **A plugin's declared door**
  runs at L6 on the generic ACP registration, whose claims a host cannot widen (`AcpBuilderExtensions.cs:45-62` *(AG20)*); its manifest stays
  Daoris's.

**The ⚖ rows, placed.** d, h, o, p, q, u, s-v, s-vi and s-vii stay Daoris's and build as accepted, each after both makers' reads; j's
narrowing is named at each protocol door's registration at L6. A door's move narrows nothing, because it waits for parity; where the layer
would narrow (fewer abilities, a posture it cannot carry), the door does not move. L3, L5, L6 and L7 are ⚖ themselves, since each reroutes
how an account's home and a posture reach a process.

### Q12. The program

Row f stays Daoris's: release sources, pins and signed downloads in Install, since D20 has the layer never fetch, choose a version or pick a
location. Each agent's pin lives where it lives today and reaches a layer door as the registration's `command`, the pinned program's path,
so the layer's own command seams (an override variable, then `PATH`, `ClaudeAgentSession.cs:18-19`) are never read. The coupling of §0 item
6 is answered at L3: it does not land until Y2 is answered, since otherwise an agent's update would blank the windows a person reads.

### Q13. The census at each step

- **A site that leaves Daoris** lowers its row's count in the same commit, and a row at zero is removed (counts are exact, so an allowance
  nothing needs fails).
- **What a Daoris file may name of the layer:** its namespaces only in `Agents/Layer/` and `Agents/Providers/<Id>/`; the layer's own
  provider ids (`claude-cli`, `codex-cli`, survey §1) nowhere, since Daoris registers under its own; the vendor variables a registration
  passes as `accountHomeVariable` only in the provider's folder, as the census already confines an agent's vocabulary.
- **The layer's types are a token** of their own in the desktop's census, counted by name in the source text as the census reads today
  (D213's note of row a, part 2), its two folders permanent exceptions under this design's decision. The service's twin is untouched: the
  service's own use of Lyntai is out of scope.
- **`StructureTests` and the map.** The structure analysis compiles the driver's sources alone, so the layer's types are external symbols;
  it gains a rule that a file outside Agents naming a `Lyntai` namespace fails, and inside Agents only the two folders may. D212's map is
  kept: the layer is a library below every subdomain, named by Agents alone, so no upper subdomain gains an edge, and a disclosed break
  reaches one folder.
- **Planted:** `using Lyntai.Agents;` in a Loop file, an `AgentFailure` named in Events, an `AddAcpAgentSession` call in
  `Agents/Registry/`, `claude-cli` in a provider's folder, and a layer-run registration declaring a home variable beside its binding; each
  fails.

### Q14. The conformance suite at each step

- **L1:** unchanged, three views.
- **From L2:** a fourth view, *the layer*: every layer-run provider in `Providers.Built()`, `alt-stub` first, presented through Daoris's
  registry over `Agents/Layer/`. The 60 cases run on it as on the others: the 26 mandatory by the adapter's mapping (ids, parts, records,
  postures, the spawn table through the runner, the pool's agreement and `Lent`); the 14 abilities read through the binding; the 20 live
  ones by the stand-in's program through the layer's door and Daoris's runner.
- **Held pending** by name: the layout's `row` admits an L-row or a Lyntai row (`lyntai:AG10`, `lyntai:Y3`), so a held line names what
  makes it pass, for example a child seen and ended (`lyntai:AG10`), words queued and a steer (`lyntai:Y3`), the stand-in's own extensions on
  the protocol door (`lyntai:AG15`), a held request (`lyntai:AG19`), a model on the protocol door (`lyntai:AG11`).
- **Lyntai's theory holds** every ability stated once, an unknown id naming what exists, a malformed descriptor refused, another provider's
  account and an unhonourable option refused before a spawn, and an account's home reaching the child's variable
  (`Lyntai/tests/Lyntai.Tests/Agents/AgentProviderConformanceTests.cs:116-375`); AG16 would hold one terminal per turn, a failed terminal
  carrying its failure and a printed limit never classifying. Daoris's suite does not prove those again; on the layer view it proves the
  adapter (each claim read equal into Daoris's part, a refusal before a spawn surfacing as Daoris's `unsupported` naming the ability) and
  every case only Daoris can (ids by kind, `Lent`, records, reach, Daoris's variables, admission, interactive, applicability, and the
  scenario's live cases end to end through Daoris's executors).
- **The stand-in that proves the shape** is Daoris's `alt-stub`, its scenario contract and vocabulary its own; Lyntai's `acp-stub.mjs`
  stays Lyntai's test.
- **Working as today, shown:** at L1 and L2 the three existing views' pending files are byte for byte the commit before's and the fourth
  view's file is the only new one; at each door's move, that door's cases on the layer view equal its cases on the registry-over-adapters
  view the commit before, case for case, and its pending file does not grow; the census shrinks by exactly the sites the move deleted.

### Q15. The reference

- **Packages:** `Lyntai.Core` and `Lyntai.Providers.Basic`. Not the metapackage: it adds `Lyntai.Storage.Basic` and `Lyntai.Tools.Mcp` with
  `ModelContextProtocol.Core` (survey §11), none of which the driver uses.
- **Project:** the driver alone, and in it the Agents subdomain alone (Q13).
- **Constructed or registered:** the layer's types are constructed where their constructors are public (`AgentProvider`,
  `AgentProviderRegistry`; `IAgentProvider.cs:51`, `:143` *(AG20)*). The built-in doors, whose claims are internal (§0 item 2), are composed
  through a private `ServiceCollection` per start inside `Agents/Layer/`, which adds the container package to the driver beside the
  abstractions `Lyntai.Core` brings. No container appears anywhere else in the driver. When Y8's factories are released, the container goes.
- **Pin:** in `Desktop/Directory.Packages.props`, beside the store's pair, its comment naming D22 and this design.
- **Equal to the service's pin?** Not as a gate: the desktop references no service project (`Desktop/*/*.csproj`), so the two never share
  a process. As practice, the service follows the desktop to the same release once its knowledge search suites pass, so one changelog is
  read for both. The desktop's SQLite pair stays the service's (FOUNDATION1 z-i); a Lyntai release moves it only through a package that pins
  SQLite (`Lyntai.Storage.Sqlite`, survey §11), which neither tree takes.
- **Transitive versions** against Shenora 0.24.0 and SQLite 10.0.12 and 3.0.5: *unmeasured*. Measured before L1 is dispatched, on a pack
  of the commit the release will carry (Q17), and again at the release: the transitive list before and after, and the publish layout.

### Q16. Versioning

- **The version each row needs** is the first release that carries its needs (§3, §4).
- **Taking a release** follows D22's note of SHEN3: the changelog read at the release's tag for every version skipped, the compare read for
  the files Daoris composes (`Lyntai/Core/Agents/`, `Lyntai/Core/Processes/IProcessRunner.cs`, `Lyntai/Basic/{ClaudeCli,CodexCli,Acp}/`) and
  the two `ApiSurface` baselines; a note on D22 per release taken.
- **A disclosed break in a minor** (Lyntai's D18, open while Daoris is the only consumer, §0 item 8) is met in `Agents/Layer/` alone; the
  row taking the release says what it changed. Exact pins mean a break arrives only with a row that takes it.
- **The freeze and the order.** A release freezes what it carries (D70; D235). Daoris builds each row only on members the release carries,
  first on D235's first tier (members no open row touches); a member a row grows additively is treated as absent until its release; and
  Daoris reaches the uncertain parts (the pool) last, after it has used and released the certain ones (descriptor, events, failure, the ACP
  door), the order Lyntai's layer §4 asks. Daoris's packed-build evidence on a row not yet released (Q17) lets Lyntai change a member before
  a release freezes it.

### Q17. Building against changes not yet released

**D22 stands for what lands, with a note for what proves.** Main and every merged branch pin a released version; the merge tool's gates
and the CI beside build released versions only, from nuget.org or the machine's packages folder. The note, appended to D22: a branch may be
built against a pack of a named Lyntai commit (D235's recipe, `dotnet pack` into an untracked folder under a version of its own) to prove a
need before a release; the pack is made from a fresh clone of the named public commit under Daoris's ignored `local/`, never in a Lyntai
checkout another session works in; the version override comes from an untracked file the tracked props import only when it is present, so
no tracked file names the folder or the version (D235); such a build is evidence for the request to Lyntai and is recorded with it; and the
branch lands only when a release carries what it uses.

On a machine with no Lyntai checkout the gates never see the override, so a branch that needs an unreleased member fails its restore there,
which is the refusal. While a Lyntai row is merged and unreleased, a Daoris branch holds nothing that compiles only against it: a row is not
dispatched until the release that carries its need exists, since a branch is handed back working and merged, and a waiting one goes stale.

Under the answers foreseeable: D235 as written, the above; Lyntai publishing previews later, a published prerelease the CI could restore,
which D22's *released* would need its own note to admit, not proposed now; a shared feed for several hosts (D235's re-open), the same as
now, read only through untracked configuration.

### Q18. No feed reachable

A build restores from the machine's global packages folder when the pinned versions are there (NuGet's standard behaviour, *unmeasured*
here and in the CI), so a machine that has built once builds offline. A clean machine's first build needs nuget.org, as Shenora does today;
nothing of Lyntai is kept in the repository. The installed app restores nothing at run time: the publish copies `Lyntai.Core`,
`Lyntai.Providers.Basic` and their dependencies, and L1's proof lists them in the publish layout and starts a stand-in session with the
network off (*unmeasured* until then, survey §12). A person with no network keeps working: the layer spawns only what the host deployed and
fetches nothing (D20); a usage read starts the vendor's own server, which needs the vendor's network as today's does.

### Q19. What moves when

§3 lists the requests and §4 the rows. In order:

1. **L1**, the reference, after the release that carries AG1 to AG9 and D234 (Y1).
2. **L2**, the stand-in through the layer, after the same release.
3. **L3** ⚖, account windows through the layer, after the same release and Y2.
4. Beside them, needing no release: Daoris's host rows (d, the host's half of e, f, g, h, l, m, n, o, p, q, r, the corrections, u) on the
   accepted design, interleaved with FOUNDATION1's Part B as D213 orders them.
5. **L4**, OPENROUTER1, after the release carrying AG10 and AG17 and Daoris's d, f, h, n and u (u after FOUNDATION1's 1j); it may land before
   or after L5, whichever's needs land first.
6. **L5** ⚖, Claude Code's driven starts, after the release carrying AG18, Y4 and Y7 (AG10 already in).
7. **L6** ⚖, the protocol doors, one row per door (dsh, then `codex-acp`, `claude-code-acp`, then a plugin's), after the release carrying Y3
   for the ACP door, AG11, AG12, AG15 and AG19.
8. **L7** ⚖, Claude Code's conversations, after Y3 for the CLI door.
9. **L8**, row t: the old seam's end, after every door has moved.

**OPENROUTER1's executor is Claude Code's program at the gateway**, through the layer's Claude CLI door with a key account (AG17). The HTTP
door on Lyntai's D233 is rejected as its executor: it runs Lyntai's own tool loop with no workspace, which the host keeps (D233), so
driving a quest in a repository through it would mean Daoris building the executor that owns the repository, its tools and its
permissions, a new agent and out of scope (brief §6); AGENTINFRA1 §3 refuses an integration with no executor for agent execution. An
inference-only use (a voice whose answer is its text) may take the HTTP door later, by OPENROUTER1's own design.

**AGENTINFRA1's stages:** A is settled. **B** runs in the isolated home after L2 and Y6: the layer's restriction vocabulary and pool, one
process, over a store Daoris supplies (Q7). **C** is the layer's turn as the execution-neutral run, from L2, the alternative-dialect stand-in
being `alt-stub`, and an inference-only integration refused for execution before a send. **D**'s HTTP half after AG13 and AG14; its hosted
half is not built, since Lyntai's D233 keeps a hosted agent out and building one here would be a door Daoris writes, a direction for the
owner (Q22). **E** is OPENROUTER1 first.

### Q20. FOUNDATION1's Part B rows g, j, q and r

| Row | What it touches of the provider layer | Reads | Sequencing |
|---|---|---|---|
| **1g** failures read once on every path (`docs/2026-10-25-foundation-design.md:1717`) | a conversation's and a brainstorm's limits by the reader's code; a layer-run door's failure enters as a `DoorFailure` (Q8), so one reader covers both kinds of door | Daoris's (`DoorFailure`, `AgentErrorKind`) | Part B's order, no release; if it lands before L2, L2's translation is proven into it, and if after, its proof includes the stand-in through the layer |
| **1j** one start plan and one walk (`:1721`, §2.4 `:437-448`) | the plan's settled selection, the account walk inside each choice, the reservation | Daoris's registry, account rule and offers store; never the layer's pool | after 1h, 1i and 1o (§0 item 10), before row u, which reads `Reach` in its walk; no release. The layer's `PickAsync` is rejected as the walk: a seat per turn in one process, a second walk, no reservation across processes |
| **1q** ⚖ start admission by the work (`:1734`) | nothing of a provider: the start layer is keyed by the work | Daoris's | near Part B's end; no release |
| **1r** ⚖ the connector by work (`:1735`) | the session's identity, which every plan entry carries; a layer-run door carries it in each `AgentMcpServer` | Daoris's call layer | near the end; its measurement of the protocol doors first (FABRIC-ACP1) is made on today's transports, which those doors keep until L6 |

### Q21. The names

| Word | Daoris | Lyntai | Kept as |
|---|---|---|---|
| provider | an agent with one or more doors (`Provider.cs:28`) | a registration, one door per built-in (D225) | Daoris's sense in its code and prose; the other is *a layer provider*, named only in `Agents/Layer/` and its prose; never on the window (D213 point 11) |
| door | a way in, keeping today's adapter id | `AgentDoor` inside a provider (`cli`, `acp`) | a Daoris door is one layer provider's one door |
| agent | the product a person picks (智能体 on the window) | `AgentProviderDescriptor.Agent`, the key several providers share (D234) | the window's word unchanged; in code the layer's `Agent` is read only as the account key |
| account | a profile, keyed by owner and profile | `AgentAccount`: its key, a name, a home | one thing; the profile is the layer's name |
| pool | `AccountPool`, a key's providers | `AgentAccountPool`, the walk and its holds | Daoris's sense stays; the other is *the layer's pool* |
| hold | a cool-off, *cooling* on the window | `AgentHold`, a restriction held | the window keeps its cooling words; *hold* only for the layer's |
| run | `AgentRun`, a live run across turns | none: a turn is one `StreamAsync` | Daoris's; `LayerRun` is a run made of turns |

The glossary gains nothing: the layer never reaches the window.

### Q22. What is the owner's

1. **Each Lyntai release Daoris waits on.** What: a publication in Lyntai, started by its release workflow (its D19), freezing what it
   carries (its D70, softened by D18 while Daoris is the only consumer). Yes: the Daoris rows that wait on it can land. Where: §3's batches;
   the first carries AG1 to AG9 and D234 and lets L1 to L3 land.
2. **D234 and D235's branch in Lyntai.** What: both bind on the owner's acceptance of Lyntai's AG20 branch (§0 item 1). Yes: the layer keys
   a shared account across one agent's doors. No row here waits for it (Q4).
3. **Installed proofs on a real account.** What: L3's one usage read per agent (no model is run); L5, L6 and L7's one session and one resume
   per moved door; L8's one session per protocol door and one on Claude Code's native door, one resume each. Yes: each runs under a recorded
   authorization or the owner's bounded yes, naming its account and bound, after its row is staged. Where: Q23.
4. **OPENROUTER1's first measurement with a real key.** What: money on a gateway. Yes: L4's design measures its limit words and offers.
   Where: its own design.
5. **A hosted agent.** What: AGENTINFRA1 stage D's hosted half, which Lyntai's D233 keeps out. Yes: a change of the product's direction,
   then a design and a request. Where: Q19.
6. **Rulings Lyntai's process puts to the owner there** if Daoris asks for them later (D234's door-by-door variable, a pool across
   processes): Lyntai's own; Daoris only asks.

Nothing else here needs the owner: the note on D22, the adapter's choices and the requests' wording are the readers' and the steward's.
Nothing signs in, pushes or rewrites history, and nothing loses data: no record changes shape and `cooling.json` is never overloaded.

### Q23. Proof

| Step | Unit and census | Process | Conformance | Rehearsals | A person sees | Install, and what it spends |
|---|---|---|---|---|---|---|
| L1 | `StructureTests`' planted cases; the census's new token at zero outside its folders; a restore with the HTTP cache cleared and one offline from the packages folder; the transitive list recorded | none | three views unchanged | the deployment rehearsal, the parent's | nothing | the app starts and a stand-in session runs with the network off; nothing |
| L2 | claims read into parts; the account mapping and its exception; a duplicate refused before the layer's registry; case-blind resolution; the per-start composition; the spawn table for both stand-in doors; the four kinds mapped; events translated | the stand-in's live cases through the layer and the runner, contained by the launcher | the fourth view; `alt-stub`'s pending lines naming L-rows and Lyntai rows; its cases equal to `stub`'s same scenario cases case for case | the family rehearsal's stand-in phase runs `alt-stub`'s pair with no edit | nothing | none |
| L3 ⚖ | every recorded usage answer read alike by old and new; the floor and freshness unchanged; an unvalidated version reading unknown and shown as today's unknown; the census shorter by the usage readers' sites | a stand-in usage server through the runner's live stdin, held until it answers | the usage cases in every view | the family rehearsal whole | the Accounts screen's windows on both agents, as before, in both languages | one read per agent on one named account, no model run |
| L4 | by its own design | | | | | its first key measurement, the owner's |
| L5 ⚖ | the driven start's spawn table equal; the postures mapped; a limit on it cooling a `claude-code` account, and one on `claude-code-acp` still cooling the native door's account | `LimitWaitsProcessTests`, `ContinuationTests` and the native suites whole | `claude-code` on the layer view equal to its cases the commit before; its pending file not growing | family and deployment rehearsals | driven Claude Code sessions as before | one driven session and one resume on a named account |
| L6 ⚖ per door | as L5 for the door; its narrowing (j) at its registration | `AcpTests`, `AcpSteerTests`, `AcpResumeTests`, `ChatTurnsTests` and the tick tests that play its frames | as L5 | as L5 | that door's sessions as before, queueing and held requests among them | one session and one resume on the door |
| L7 ⚖ | the conversation's spawn table equal | `ChatGoesOnProcessTests`, `ChatTransientProcessTests` | as L5 | as L5 | Claude Code's conversations as before | one conversation and one resume |
| L8 | the census at its permanent rows only; every pending file empty; each record a build before wrote read through the registry | the brief's §9 suites whole | every view green | both, whole | nothing | as D213's row t: one session per protocol door and one on Claude Code's native door, one resume each |

## 3. The requests to Lyntai

Each travels as a quest that states the need and its evidence, never the change; its receipt is recorded on the Daoris row that waits.

| Request | The general need, the asker struck | Evidence Daoris sends | Answered by | Waits on it | Batch |
|---|---|---|---|---|---|
| **Y1** | A host that takes the agent layer as a package needs a release that carries it. | the layer under *Unreleased* (survey §0, §11); D235 | a release of AG1 to AG9 and D234 | L1, L2, L3 | first |
| **Y2** | An application that keeps an agent at a version it chooses needs the usage reading at that version, or a statement it can act on, without waiting for a library release. | Daoris's pins (row f), its recorded answers (`IUsageQuestion.Recorded`, `Ports.cs:78-79`), D222's per-version bound | a design of Lyntai's | L3 | first, or second |
| **Y3** | An application that converses with an agent needs a run kept across turns: a word taken while a turn runs, a stop that keeps the conversation, a permission request waiting for a person, background work read until it ends. | Claude Code's protocol queueing and native held stdin, built and measured (Q6); Codex's protocol adapter unmeasured | D233's option made a row | L6, L7 | fourth |
| **Y4** | An application that lets a person choose how freely an agent acts needs the door to carry the agent's own permission mode, or refuse one it cannot. | Daoris's three postures (`ProviderParts.cs:119-120`); the layer's two intents (`ClaudeAgentArgs.cs:83-93`); no mode set by the ACP door (survey §8) | new | L5, L6 | third |
| **Y5** | An application that starts each turn with variables of its own needs a turn's environment, applied as a registration's is, a null removing one. | Daoris's variables per start (D213 point 6); §0 item 3 | new, or AG17 widened | none (one registration per start meanwhile) | any |
| **Y6** | An application that tells a person what to do about a hold needs the owner action's reason: refill, sign in, grant a permission, change a configuration. | `docs/2026-10-27-provider-infrastructure-design.md:126-130`; the layer's owner action carries none (survey §8) | new | AGENTINFRA1 stage B | third |
| **Y7** | An application that composes plugins for a CLI agent's session needs the door to hand them, or refuse. | `Ports.cs:35-36`; `ClaudeAgentOptions.cs:6-40` | new, the Claude door's own option | L5 | third |
| **Y8** | An application that composes without a container needs each built-in door's provider, its claims included, constructible directly. | §0 item 2; the driver's no container (survey §11) | new | none (a private container meanwhile) | any |
| filed | AG10, AG17 | the study's | filed | L4, L5 | second |
| filed | AG18, with AG16, AG13, AG14 | the study's | filed | L5; AGENTINFRA1 D | third: Daoris reports AG18 as blocking L5, which moves it ahead of Lyntai's order (survey §13) |
| filed | AG11, AG12, AG15, AG19 | the study's | filed | L6 | second to fourth, as Lyntai builds them |

**Not asked now, each with its trigger:** D234's door-by-door variable (when Daoris takes the pool and a door of a shared key reads its own
variable); a pool across processes (D219's re-open, when shared-home activation is designed); the reducer as a public pure type (when a
Daoris reader needs the rules without the pool); a CLI door a host can teach a third agent's lines (when a third native agent is chosen);
provenance on every reading and an offered-model list (as the study has them).

## 4. The rows

Each is named by its outcome (D187), taken by one builder across every lane it needs in this repository and handed back working.

### L1. The desktop builds on Lyntai's released agent layer, and nothing changes

- **Code.** `Desktop/Directory.Packages.props` pins `Lyntai.Core` and `Lyntai.Providers.Basic` at the first release; the driver references
  both; `StructureTests` gains the layer rule and the desktop census its token (Q13); D22 its note (Q17).
- **What a person sees.** Nothing. **Why a row of its own:** the closure, the boundary and a build with and without a feed are shown before
  any reader depends on them (brief §10 step 1).
- **Proof.** Q23's L1 line. **Waits for** Y1's release.

### L2. A third stand-in runs through the layer, and the suite reads it in a fourth view

- **Code.** `Agents/Layer/` (the per-start composition, `LauncherRunner`, `LayerRun`, the translations of claims, accounts, failures and
  events); `Agents/Providers/AltStub/` with `provider.json`, `pending.json`, its `Program/` entry answering the scenario contract in its own
  vocabulary, its native door's session as a host's own provider and its protocol door's registration; one line in `Providers.Built()`; the
  suite's fourth view and the pending layout's widened `row`.
- **Two parts, one branch each, one feature:** (a) the composition, the runner, the claims and accounts, the registration and listing, the
  fourth view's mandatory and ability cases; (b) the run, the readings and failures, the live cases and the rehearsal's pair.
- **What a person sees.** Nothing: a stand-in is listed nowhere without a configured command. **Why a row of its own:** it is the adapter
  every later move stands on, and the meter that will show each door's gap before the door moves (brief §8). `alt-stub`'s own diff is its
  folder and its line; the adapter is added once, with it as its first user.
- **Proof.** Q23's L2 line. **Waits for** Y1's release.

### L3. ⚖ Claude Code's and Codex's account windows are read through the layer, as before

- **Code.** Claude Code's and Codex's providers register the layer's CLI providers for their usage reading alone (Q4), their sessions never
  opened (a test holds it); `UsagePart` holds the layer's usage source, the floor and freshness Daoris's; `ClaudeUsage`, `CodexUsage` and the
  usage switch (`HarnessRoster.cs:2295-2368`) deleted; the reading's windows mapped into today's; the read spawned through `LauncherRunner`.
- **What a person sees.** The Accounts screen's windows on both agents as before; the screenshot: that screen with a signed-in account of
  each, in English and Chinese.
- **Proof.** Q23's L3 line; ⚖ since it reroutes how an account's home reaches the vendor's server. **Waits for** Y1's release and Y2's
  answer.

### L4 to L8

**L4, OPENROUTER1** (OpenRouter runs as an agent through the layer), by its own design on the contract and the layer, after AG10, AG17 and
Daoris's d, f, h, n and u (Q19). **L5 ⚖, Claude Code's driven sessions run through the layer and read, resume, cool and end as before**, the
door bound for its driven start, its conversations Daoris-run; after AG18, Y4 and Y7; `ClaudeStreams` and `StructuredOutput` stay for the
conversation until L7. **L6 ⚖, each protocol door's sessions run through the layer as before**, one row per door; `AcpSession` goes with the
last of them. **L7 ⚖, Claude Code's conversations run through the layer as before**, after Y3 for the CLI door; the native transport goes.
**L8, the old seam ends and every record still reads**: row t, with each retired transport, the shim and `provider-vocabulary.json`.

## 5. Rejected

- **The layer's descriptor as Daoris's contract** (Q3): records by kind and case-blind, the host's parts and the live run would be lost.
- **A copy of the layer's types inside Daoris** (D213's note): two contracts to keep equal; §1.3 holds the adapter to none.
- **Building rows i, j's seam, k, l's reader made one and e's stream words here**: each would be built twice.
- **Moving every door at once**, or a door before its release carries parity: a big-bang rewrite, and a regression for no one's gain.
- **Moving `stub` and `acp-stub` onto the layer**: they rehearse Claude Code's and Codex's paths over today's transports, which the
  rehearsals assert.
- **Taking the layer's pool now** (Q7), or asking for its reducer now: one process, a second hold store, a walk in the layer, a one-attempt
  lease against D125; no Daoris reader before stage B.
- **The layer's retry decorator beside Daoris's retry**: two budgets.
- **The layer's brief composer for Daoris's hand-over**: D189's brief composes from Daoris's record in its own order, and changing what the
  next agent reads is out of scope (brief §6); D228 freezes the layer's order.
- **The HTTP door as OPENROUTER1's executor** (Q19).
- **The metapackage** (Q15), and **Lyntai's types named outside Agents** (Q13): weight for nothing, and a break reaching every subdomain.
- **An ambient per-start context the runner reads** in place of a per-start registration: hidden coupling, where the registration uses the
  seam as built.
- **A pack's version on main, a project reference, a submodule, a source copy or a committed package** (Q17).
- **Waiting for AG20 before any row** (Q4), and **one Lyntai pin forced equal across both trees as a gate** (Q15).

## 6. What stays unmeasured

- The transitive closure against Shenora 0.24.0 and SQLite 10.0.12 and 3.0.5, and the publish layout (Q15, Q18).
- Whether nuget.org lists any Lyntai release Daoris would take (survey §11), and an offline restore here and in the CI (Q18).
- How row e's 53 sites split among the usage readers, the stream words and the host's facts.
- dsh's home variable and whether it resumes; whether Codex's protocol adapter takes a word mid-turn (Q4, Q6).
- Whether any installed plugin declares a door joined to `claude-code` with its own variable (§0 item 7).
- The agent versions Daoris pins against the layer's validated usage versions (§0 item 6).
- The start-time cost of a private container per start (Q15).
- What a session is handed by the layer's rendered server file against Daoris's own (Q11).
- Whether Daoris's asking posture has any counterpart on a headless CLI turn (Q11, Y4).
