# Agents and harnesses — how it works now

## What it does

Daoris runs other makers' coding agents as processes on this machine: Claude Code, Codex, dsh, two pairs of stand-ins with no
model, and any harness a plugin declares, each through a door: the native pipe (one process per run, read line by line) or the
protocol door (the Agent Client Protocol on stdio). A start chooses the binary and one of Daoris's accounts, composes the agent's
settings, rules, servers, skills and first prompt in its own vocabulary, and holds the run (rule 8). Daoris reads usage, limits
and failures from the door, never the agent's words, and decides from them: cool an account, walk to another, retry or stop.
Model and effort come from the role's choices; Daoris runs no model loop of its own.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/`, each type in a file of its name; `docs/index/README.md` says
where a seam, route, verb or fixture is. Which folder owns which job:

| Where | Its job |
|---|---|
| `Contract/` | The provider record (identity, doors, account pool, word tables, usage question, reach, each part known or stated absent), `DoorAbilities`, `AbilityReading`, `AgentRun`, the `SpawnRequest` a start makes and the `ProcessRecipe` a spawn answers, the ports a provider fills (`Ports.cs`), a plugin's `DeclaredDoor` |
| `Registry/` | The one registry, of providers alone: `Providers.cs`'s built list, the plugins' doors joined, names resolved by kind, whose accounts a door runs as (`OwnerOfAgent`, `AgentNamed`), which agent a door is (`AgentOfDoor`), where it may work (`ProviderReach`) |
| `Providers/<Name>/` | Each provider's facts, spawns, word tables, usage question and own-home reads: `ClaudeCode`, `Codex`, `Dsh`, `Stub` (`stub`, `acp-stub`), `AltStub` (`alt-stub`, `alt-acp-stub`, with words and a home variable of their own) |
| `Transports/` | `Shell`, which completes every session's process from a recipe, and the session's git port |
| The root | The run (`TransportRun`, `DoorInput`, `ProtocolWords`, `HarnessCapture`, `NativeFrame`; `SessionProcesses`, never sent to the service); the protocol door (`Acp.cs`, a root wiring `Acp*` units); the roster (`HarnessRoster` and its `Roster*`, `Account*`, `Usage*` and `Binary*` units, `AgentErrors`, `HarnessSettings`, `AgentOffers`); what a session is handed (`ConnectorPlans`, `SessionServers`, `ServerKeeper`, `PermissionRules`, `RuleProposals`, `SpawnSkills`, `TargetPrompt`, `Spawning`); the terminal's `daoris agent …` verbs (`AgentsCommand`), each refusing a flag it does not take (`AgentVerbFlags`) |
| `Turn/` | The protocol turn's registered stages (`docs/index/registered.md`), its wiring interfaces and the open prompts' gate (`OpenPrompts`) |
| `Layer/` | Lyntai's agent layer, compiled only under `DaorisLyntaiLayer` (rule 47) |
| Beside Agents | `Events/Readings/` (a run's `WireReading`s), `Knowledge/` (the provider sheet, `IAgentFacts`), `Loop/Session/` (a spawn's assembly), `Plugins/` |

## Its rules

**Agents and doors**

1. An adapter is added deliberately. An unknown agent or door name is an error naming what exists (`UnknownDoorException`),
   never a fallback to another harness. (D23, D213)
2. The built set is `claude-code` (pipe), `claude-code-acp`, `codex-acp` and `dsh` (protocol), and the stand-ins; the machine's
   default is `claude-code`. A plugin may add harnesses, never replace a built name: its door is a `DeclaredDoor`, a protocol door
   with its own identity, home and program and no rules, placed by its `accountOf`; a manifest naming steering, resume, rules,
   words, lineage or confinement reaches nothing. (D53, D57, D64)
3. Every harness reached over the protocol is a configuration of the one protocol door, not a new seam. (D53)
4. A provider is an agent with one or more doors. A door's account key is its harness's `accountOf`, else its provider's id,
   followed one level only: `claude-code-acp` runs as `claude-code`'s accounts, `codex-acp` as `codex`'s. One agent's two doors
   are one agent; two providers sharing a pool are two (`AgentOfDoor`). (D213)
5. Ids are typed and opaque: carried, stored, compared and shown, never read for behaviour. (D213)
6. Silence preserves: an adapter that declares nothing, or a part stated `Absent`, behaves as before the feature existed. Every
   claim about another program is read from its binary, source or wire first, never guessed. `Reach` is never absent: the plan
   (`StartPlan.AccountsAsync`) passes a choice whose provider is not opened to the start's place as `not-opened`, and a role
   holder's or a meeting's start refuses it; no place is opened to an `Opened` provider yet. (D53, D57, D212, D213)
7. Each `AgentRun` operation is admitted by the run's own evidence; one it lacks is refused as unsupported before anything is
   written. (D213)
8. Every executor holds its run through `TransportRun`, or `LayerRun` for a driven native start where the driver carries
   Lyntai's layer (both a `HeldRun`, rule 47): the driven start, the continuations, the go-on, the resume's one retry (only where
   the harness exited nonzero before naming the conversation) and the conversation's runner; none prepares a process itself. A
   run's words are two ports: a native door's lines on `AgentRun.Input` (`DoorInput`, keeping each word until the harness names
   the conversation, so a retry replays what the ended process never acknowledged) and a protocol door's requests on
   `AgentRun.Words` (`ProtocolWords`); nothing above the run writes to a process's stdin. (D213, D243, D246)
9. A provider translates; Daoris selects. A provider never walks accounts, picks a model, starts another participant or decides
   whether a line goes on. (D213, D67)
10. The protocol door's wire enriches the console and transcript, but a record concludes from the exit code and the quest's
    state, because the wire's stop reason flattens failures. (D46, D53)
11. A driven session takes no person's line on its stdin; the protocol door's stdin carries Daoris's frames, and a driven native
    prompt goes on stdin under a bare `-p` as BOM-less UTF-8, never as an argument. (D213, D49)

**The spawn**

12. One shell completes every session's process from its door's recipe, in one order: the role variables cleared and every other
    provider's registered credential and home dropped (a plugin's door keeps all); the repository and service, the tools, the
    session's git, the identity variables, the recipe's own; the account's key, the binary and pin, the program on the tools'
    `PATH`, Claude Code's seam on a door whose program runs it (never Codex's, dsh's or a plugin's), the home; the
    tools-at-the-start switch (`ENABLE_TOOL_SEARCH=false` for Claude Code); the carriers last. A provider declaring a name of Daoris's list is refused at registration, but
    for the session's tokens. (D49, D230, D172)
13. A start's connectors are one `ConnectorPlan` per workspace (`ConnectorPlans.For`): Daoris's connector, the plugins' and the
    browser's for a session, Daoris's alone for a reviewer, helper, partner or brainstorm pass; a source not permitted, a name
    twice or a missing required server is refused by name. The protocol door's open carries the plan; Claude Code's native door
    writes it to a `.mcp.json` under the home (`--mcp-config`), including the located Daoris connector with the session's identity
    on fresh quest starts and carry-ons, without relying on a repository's `.mcp.json`. Only a supervisor's turn leaves the agent's
    own servers out, held to the person's tier, its handed file and Daoris's servers (`HandSourcesAlone`), its shell tools denied.
    A native door is admitted only for an adopted repository whose servers inherit its environment (`Adoption.Drives`). An `npx`
    server starts as node and its package's script, installed once into `cache/toolchain/<package>/<version>`; one that cannot
    start is not handed (`server.refused`). A server driving Daoris's browser runs under `keeper/server-keeper.mjs`, restarted
    past 512 MB with no call in flight, since Playwright MCP's memory grows with every request. The session's tabs are in a browser
    context of its own that closes with the server, so the new one is sent to the page the session's newest answer named, and a
    call naming an element reference from before the restart (`target`, `ref`) gets a fresh snapshot. The keeper lists and calls only the tools the server's plugin declares (its reads and
    writes), answering a call to another without reaching the server, and ends a call past two minutes, said in the conversation
    and cancelled at the server; every mode alike, full access included.
14. Plugin skills are `SkillSource` values laid out by the door's provider (`ISkillLayout`; Claude Code copies them per session
    under the home's spawn folder; a provider stating none is handed none) and handed by the harness's session-only door
    (`--plugin-dir`, or `_meta` beside the rules). A harness that loads no tree skills (Codex) is told their folders. (D170, D230)
15. A Claude session keeps the account's auto-memory, synced skills, plugins and connectors, and Daoris writes no account home,
    vendor folder or the person's tool home. It hands what the person's own run reads from that home (`CLAUDE_CONFIG_DIR`, else
    `.claude`), each family on unless switched off (`OwnHomeDefaults`): skills, subagents and commands as plugin `own`
    (`own-skills`); user-scope servers but Daoris's connector and any naming a `DAORIS_` variable (`own-servers`); enabled plugins
    (`own-plugins`); the Chrome switch (`own-chrome`); `settings.json` beneath Daoris's rules where nothing else sets a value,
    never what signs in, sets the mode, names plugins, switches hooks off, sets a held variable or widens a one-job session
    (`own-settings`); `CLAUDE.md` appended to the system prompt (`own-memory`). A Codex session gets the person's Codex home as
    `CODEX_CONFIG`: `config.toml` where the account's sets nothing (never provider, sign-in, approval, sandbox or trusted
    projects), `AGENTS.md`, skills and prompts in `developer_instructions`, and `mcp_servers` on the open, filtered alike
    (`CodexOwnHome`). A pass held to Daoris's connector gets no own servers, and Ask Daoris no own servers, plugins or Chrome
    switch; nothing is handed in the person's own home, on a plugin's door or under a scratch home. (D230, D253, D262, D266)
16. The standing block is composed by the driver for every kind of session, handed at a start and at a move, never on a
    resume. The whole first prompt stays under `StandingBlock.Budget` (30,000 characters); past it the block goes in a file the
    prompt names. (D230)
17. Every session runs in a configuration home Daoris owns. With no Daoris account signed in, a start waits for a sign-in. It
    never runs in the person's own tool home, but a conversation begun there goes on there. (D175, D197)

**Permissions and posture**

18. What an agent may do is Daoris's rules in three scopes (machine, workspace, repository) in `permissions.json`, over the
    defaults `connector`, `commit` and the six own-home families (rule 15), each switched off by the person, and `no-push`,
    `no-publish` and `tree-guard`, which are off until the person switches them on (`defaultsOn`). The union is the harness's
    command-line settings tier; its precedence decides and `deny` wins. `tree-guard`, where on, refuses a write outside the
    session's tree and declared targets, but a subagent's in the worktree its harness made (`.claude/worktrees/<name>`). (D72, D253)
19. Only a harness whose provider states a rule language is handed rules (`IRuleLanguage`: Claude Code's `ClaudeRules`, with the
    tree guard, the lineage hook and the hard denials); nothing is translated until measured. Codex, which states none, holds
    none of the person's denies: its standing block and access setup say so, the git doors judge its push, force and delete, and
    a packet's reader on it is `not-enforced` and passed (roles rule 42). (D72, D213, D233, D272)
20. A session may propose a rule change with `permission_propose`. A narrowing applies at the next look; a widening waits for
    the person's yes, or an allow supervisor's at its rule's reach, but one that moves the person's deny, switches off a denying
    default or lets a publish through under `no-publish` stays the person's (`WideningAllows`). A proposal settles once
    (`ProposalSettlement.Settle`), marked `settling` before its save and never saved twice. Every writer of the rule file reads,
    decides and saves under its one lock (`PermissionChange.Change`), recording who made the change. (D74)
21. A driven session runs in its harness's judged mode: `auto`, else `acceptEdits`, on Claude's protocol door, the posture held
    (`PostureAsked.Held`); `acceptEdits` on its pipe; `agent` on Codex; `workspace-write` through dsh's environment.
    (D81, D53, D253)
22. Full access (`bypassPermissions`, `agent-full-access`) and the asking mode (`default`, `workspace-write`) come only from the
    person's setting: Ask Daoris's full access or a repository's access setup. A harness with no mapped mode refuses the start,
    never running a narrower mode under the label. (D183, D184)
23. A permission request reaching the driver, on every door and mode below full access, is held for the person to allow once or
    refuse, or for the allow supervisor at the session's repository; only the hold's bound (`HeldPermissions.Bound`, 15 minutes)
    refuses it, said as such. The protocol door holds on its wire (`ChatHolds`); the native door, which refuses under `-p` what
    its rules do not allow, is handed a `PreToolUse` hook (`permission-ask.mjs`, `ClaudeAskHook`) that writes a `permit` request
    for a call its mode and rules would ask about and answers from the loop's hold (`NativePermits`). A session shaped for one job
    (review, helper, intake, brainstorm, partner, supervise) is handed none. (D81, D184, D253)
24. Folder trust is written only on the person's act naming the folder, one flag in the harness's file: with no account named,
    for every account of the set, never the person's own home; a named account must exist here. Trust gates a start only where
    the handed rules would not let the connector through. (D73, D197)
25. A session reads the workspace's registered checkouts by default and writes another only where the person declared it;
    reading is the read checkout's setting. (D107)

**Accounts**

26. An account is made by signing in into a fresh folder named by a new id (`acct-` and eight hex characters; an `account-N`
    keeps its name as its id); removing it deletes the folder. An API key is an account too: kept in `keys.json`, sealed with
    DPAPI for the Windows user (`KeySeal`; an unsealed key is sealed at the file's next write), handed only to a start's process in
    the tool's variable, shown by its last four characters. An agent held by a key alone (dsh) reads an account by its key: none
    reads signed out, as *no key*; a key never read is checked once by the provider's cheap read (`IKeyCheck`), a refused key
    reading signed out. Every place that tells the person how to add or mend such an account says the key's way (`agent key`,
    *Set key…*), never a sign-in. (D66, D67)
27. Each agent's accounts are one set serving every workspace, with one order and one set of use settings. The person's pick at
    a start is the one exception; a pick whose folder is gone is refused, never moved; a handed quest's named account that is gone
    walks on. (D197)
28. The person's own sign-in is outside the set: Daoris never signs it in or out, asks it anything or starts work in it. On the
    agent's page it is the list's last row; *Ways in* says each way in by what it gives, and one not installed says what to do.
    (D197, D175)
29. The walk, under `goal` (the default): a near account last, then most room in the tightest window the provider reported
    (rooms within five points tie; no number ranks as no room; not under `switch: off`), then fewest of Daoris's sessions running,
    then a week about to lapse, then most room under the curve, then least recently started, then the order. Under `order` the
    list is the walk, a near account last. The cap is one number per machine. (D130, D165, D197)
30. A cooling account is held first, by a file read, before any probe. A model family's cool-off holds a start whose model names
    that family (`opus` in `claude-opus-4-5`) or runs at account default, and passes one naming another (`held.cooling-model`);
    every reader that knows a line's model reads it so (`CoolingOf` over `AccountCooling.Holding`). A refused account is passed
    until a person acts, a signed-out one until a sign-in or an hour after its last reading. With no account ready the start
    waits, saying the soonest way out. (D125, D130, D205)
31. A running conversation moves at its turn's end to another account of the same agent with room (10 points under its curve on
    every window), never mid-turn, and not again within 30 minutes. Where none has room, the `handOver` rule decides another
    agent. (D165, D163)
32. A resume asks for its record's own account, because a harness keeps a conversation in that account's home; a start on another
    agent is on another account. A resume judges that account as the walk does: a whole window with its reset ahead cools it
    until then, unless the person picked it. (D131, D137, D206)
33. One status question per configuration home runs at a time. No real sign-in runs under a scratch home. (D125, D150)

**Usage, limits and errors**

34. Usage is machine-local: it names accounts, lives under the home and has no HTTP route. Each account's use is read at no model
    spend (Claude's `get_usage`, Codex's app server), with its source and age, by its provider's `UsageQuestion` or the layer
    (rule 47). Opening Agents starts no process: an account is read anew only at a press (*Read now*, *Read again*) or where a
    start's walk needs it, never within the question's floor. A week opened with no reset and no use has a made-up reset and is no
    account week (`UsageCurves.ResetMadeUp`). Each usage file is edited under its keep lock; one that does not read is never saved
    over. (D57, D165)
35. Capacity is estimated from Daoris's own turns and the readings, with weights that are dated ratios, never prices. Acting on
    the estimate waits. (D179)
36. A door's failure is read by one reader (`AgentErrors`) into four kinds, in order: limit, access, transient, fatal, from the
    door's own tables, then its owner's. The agent's message is never read but for a driven protocol turn's last one with no step
    after it (`TurnEndLimits`), since Claude Code's adapter says its account's limit that way and ends `end_turn`; read as a limit
    it ends the run as the turn's failure (`AcpTurnLimited`). Anything unread is fatal. An access failure carries how it was read.
    (D210, D125)
37. A limit cools that account until the reset the agent named plus 2 minutes, with 15 minutes' grace for a dated reset alone
    and nothing believed past 8 days, else the machine's `cooloff` (60 minutes); `ResetBounds` holds these bounds. A limit is never
    a strike. A limit naming a model family the agent offers, or a reading putting that family's week at 100% with its
    reset ahead, cools that family alone (a family is a word that tells one offered model from another, read from the agent's opens
    and its settings aliases, `ModelFamilies`; no list of Daoris's names one) (`models` in `cooling.json`); every other limit cools the whole account. *Try now* and a
    sign-in end both. (D125, D205)
38. A limited line resumes by itself at the look: its own conversation on its own account when ready. Else a driven line, and a
    repository's chat at its limit move, goes on on another account of the same agent in a new part carrying its conversation
    (`LimitCarry`), its files copied once the part it leaves has ended: whole at or under the provider's `WholeBound`, compacted
    first past it where the harness can be asked, else briefed. It is briefed on another agent, from or to the tool's own home,
    where the door declares no carry, the copy fails or the door refuses the copy's resume (the copy taken out,
    `IConversationCarry.Remove`); `session.moved` says which. A restart cutting a carried move off before its new part runs leaves
    it in the same conversation. With no account to move to it waits, showing the earliest reset. A driven line whose limits rule
    refers holds as an ask for the limit supervisor (roles rule 48). The bound is D206's measurement. (D206)
39. A transient failure is tried again in the same conversation after 1, 2, 4, 8 and 16 minutes, up to the line's tries rule:
    never a strike, park, cool-off or walk. Past the bound the quest is held until the person's word or *Try again*. Every
    protocol-door run takes that retry (`TransientRetries.For`, `ChatRetry`), checked by its kind's own authority
    (`TransientOwners`); a pass with minutes of its own waits none past them and fails with its kind's code (`helper.transient`
    and its kin), never *timed out*. (D210, D229)
40. Access goes to the person: the account is marked refused or signed out, with no retry. A fatal error fails with its reason
    and is a strike. (D210)

**Toolchain and models**

41. The binary is the explicit command, else the managed pin (workspace, then machine), else `PATH`; a pin with nothing installed
    refuses, never falls back, and Daoris never installs an agent unasked. A binary's version is kept in `reads.json` with the size
    and last write of the files its question starts, asked again only where they changed, at a press or `agent list --again`,
    after an install, update or pin, or by a start held on an absent agent; a door with no version question is present by a look
    at its file (`BinaryPresence`). A look, the page, the list and a restart start no process, and Daoris starts no `reg.exe` or
    `docker`. (D57, D121)
42. Agents install from their makers' channels into the home's toolchain folder. An ACP adapter is its own package and pin; the
    managed `claude` it runs reads `claude-code`'s pin. On Windows an npm-installed agent starts as the node and script its `.cmd`
    shim would run (`PackageStarts`). A pin names an exact version (`PinInstall.Plan` refuses a tag or a range). (D57, D121)
43. A start's model and effort come from the role's choices, in the tool's own words: on the protocol door by
    `session/set_config_option` before the posture and the first prompt, on the pipe by `--model` and `--effort`. An absent value
    is the account default. (D205)
44. A model the agent does not offer stops the process before any prompt, with no strike, and the walk goes on (`IChoiceReask`,
    the refusal kept in the offers store). A running session never moves to another choice. The record keeps what was asked,
    acknowledged and ran, each read apart (`ActorAsked.ForSpawn`). (D205)
45. An account's own model and effort live in the tool's own settings file under that account, for Claude Code only, changed
    under its keep lock (`AgentSettings.Write`); Daoris offers nothing for a tool whose settings it has not read, and `max` is
    refused as an account default. What an agent offers is read by `AgentOffers.Offers`, per session and per account default.
    A Claude door's session models are the newest open's list on either of its doors; the settings alias list answers only where no
    open has listed, stamped with the program version it was read at (`AgentSettings.ModelsReadAt`). A role's model the agent's last
    open did not list is said as a hint by `daoris role set` (`ModelHints`), never refused. (D98)
46. A conversation's model and effort options are those the agent offered on its wire. The session's mode is never offered as an
    option. (D98)
47. Lyntai's agent layer (`Lyntai.Core`, `Lyntai.Providers.Basic`, never the metapackage) is taken by the driver alone from
    Lyntai's local build, referenced only under `DaorisLyntaiLayer` (the desktop's props import the service's ignored
    `local/lyntai-local.props`); without it the driver has no Lyntai assembly. Only `Agents/Layer/`, each provider folder's
    `Layer/` and their tests name it (`LayerCensusTests`). There both usage questions are read through the layer
    (`LayerUsageRead`), and a native door's driven start is held on `LayerRun` (`DrivenRuns`, `LayerHold`), one turn per process
    through the same host steps; a supervisor's turn and a partner's piece stay on `TransportRun`. A layer door's failure enters as
    a `NormalizedFailure` (`LayerFailures`), answered by `AgentErrors.OfNormalized` with no table read, which only tests call yet: its
    access readings stay apart (a field's and the words'), a limit's window, scope and stated reset reach D125's bounds as stated
    (`LayerStatedReset`), and an allowance report is put in the rate-limit frame's shape for the one reader of an agent's windows
    (`LayerAllowance`). `LauncherRunner` arms no inactivity clock and states it can pause one, so a request the layer holds for the
    person waits with no clock; a door's private container stays Daoris's, the layer declining a door without one (Lyntai D249).
    The parity oracle (`Tests/Agents/Parity/`) names each difference of the two paths a `ParityGap`. (D213, D248)
48. An old adapter-seam name is read only where it has its end: `driver.json`'s `adapter`, `intakeAdapter` and `helperAdapter`
    and the service stores' renames (`SessionColumnNames` and its kin), deleted with nocompat1's steps; a plugin's `session.ended`
    frame's `adapter`, renamed with the work-event grammar's next version; a machine-log line's `adapter`, until its file ages out.
    An older remote host's `adapter` reads as the door `unknown`. `StoredOldNamesCensusTests` holds the rest. (D232)
49. What a door can do is one reading (`AbilityReading`): the ability its provider states as a `Known` with its source and, where
    measured, the versions it was taken at (`MeasuredAt`), else its transport's value said as a default that was not measured, else
    absent. `DoorAbilities` and the provider sheet both read it, and `agent list` names each measurement and offers reading taken at
    a version other than the pinned one (`VersionCurrency`). A start records the pinned install's version where that install runs,
    never the machine's own look. A held-pending case of the conformance suite names an open backlog row (`TASKS.md`), and each real
    door's recorded frames, with their program and adapter versions, are `fixtures/providers/<id>/corpus.jsonl`.

## Where a change goes

Agents sits after Knowledge and before Workplace, Roles, Participants, Context, Course and Loop (`Driver.Tests/StructureTests.cs`).
It uses what is below it; what sits above reaches it through ports installed when the driver loads (`IAgentContext`,
`IAgentWorkplace`, `IAgentWorkWords`, `IAgentPlugins`).

- A new agent or door: a line in `Registry/Providers.cs` and a folder `Providers/<id>/` on the contract; a door is a record of its
  provider, never a class (in tests `TestDoors.Door`, a shipped one `BuiltDoors.Of(id)`).
- Code naming Lyntai's layer: `Agents/Layer/` or a provider folder's `Layer/` (rule 47).
- A new fact about a real agent (a flag, a posture, a word): its provider's or door's part, measured first, a word table's entry
  with its recorded sentence.
- Protocol-door behaviour on a frame: a stage marked `[TurnStage(name, phase, after)]`, reaching the session through
  `ITurnWiring` and sharing state through a state type (`RefusedCalls`); `AcpPartsTests` holds the order. Other door behaviour is
  an `Acp*` unit behind a wiring interface. The open prompts change only through `OpenPrompts`, under its gate, calling nothing
  out while it is held. `Acp.cs` only wires.
- How a run starts, waits or goes on: `TransportRun`, `TransportStart`. A new fact a door says is a `WireReading` in
  `Events/Readings/` on the run's sink, kept by its owner's part where the executor composes it (`SessionHold`, `ChatWatch`),
  never a callback.
- Which account or binary: `HarnessRoster`, the pure ranking in `AccountRotation` or `Domain/`. Which choice or model: Roles.
- A failure's kind: `AgentErrors` and the door's tables; what follows it: Loop and Course.
- What a session is handed: `SessionEquipment`, `SessionRules`, `SpawnEquipment.Equip`; a door's arguments and carriers on its
  spawn; Daoris's variables in `Shell`, never a recipe.
- A branch on what a door can do asks `door.Can`, never its wire; driving an unadopted repository is `Adoption`'s, read once
  (`BuildDoor.Drives`) and carried as a bool.

Tests are in `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Agents/`, named for their unit; the censuses holding the area's
shape are `ProviderCensusTests`, `ProviderVocabularyTests`, `ProviderConformanceTests`, `LayerCensusTests` and the old names'
two. Gates: `driver`, `censuses`, `driver-process`, `cli` for the `*TwinTests`' other half, `web-unit` for the Agents page.

## Contradictions

An older word the code departs from, then what reads right.

1. D23 (one harness): only its unknown-name error lives (D53, D57, D213).
2. D24 and D98 (Daoris names no model): D205, whose suggestions name `sonnet` and `opus`.
3. D205 point 7 (Build led by `opus` · high): its 2026-10-25 note and the code, `sonnet` · high then `opus` · high.
4. D81 (never `bypassPermissions`): D184's bypass by an unrestricted setup; D81 holds for the auto setup.
5. D184 point 5 (the person's denies hold everywhere): Codex states no rule language (D233 choice 3).
6. D125 point 4, D130 points 3-4 (workspace and machine lists): D197's one set per agent.
7. D125 point 5 (the own home as an account): D175 and D197, a start with no Daoris account waits.
8. D73 (trust with no account names the default's or the own home): D197 point 3, every account of the set.
9. D57 (everything under `~/.daoris/`): D63 and D172, entries of Daoris's home.
10. D213 point 2 (no reader asks "own, else owner"): `AgentErrors` reads its own table then its owner's, as D210 point 2
    prescribes; D213 is the target.
11. `HarnessAccountFolders` (a pin with nothing installed falls back to `PATH`) vs `RosterWalk`: the refusal.
12. `ClaudeCodeSpawn` (the repository's `.mcp.json` wires the connector) vs `ConnectorPlans.For` at every spawn: the spawn.

## Open

- **AGENTPROVIDER1**: h's containment on the installed launcher; r's Stub Two partner exchange (COAGENT1e); the rest of t, after
  every door has moved; j, each protocol door reading its own agent's extensions, after 1e. All Daoris's own.
- **AGENTLYNTAI1**: the owner's, d's acceptance and installed proof and s-ii's spend (the three tools a headless turn cannot answer).
  Lyntai's, held for a third request: steering, the kept runs of `codex-acp` and dsh, Codex's refused open, the Claude CLI kept
  run (1f-i), and a release (every member is on its `master`, none released, so Daoris builds on its local build). Daoris's own: the
  protocol doors' moves in order (1e), each after its records (STEER3, MSG1h, AUTONOMY1b3d), and a decision on `nativeSubagentSessions`.
- **AGENTERRORS1** c and e: the native door's failure reads, the measured `--resume`, its live and one-turn tries.
- **LIMITRESUME1**: one native session seen ending on a limit, and the owner's yes.
- **CODEXTOOLS1**: the installed proof that a first Codex turn uses the connector.
- **STEER3**, **MSG1h**, **MSG1i**: steering and next-step words measured on Codex and the native door.
- **BGWAIT2b**, **HELPMODE1a3**: woken-turn words, resume and fresh part per door, full access on the installed pins.
- **MODELROLE1c4**: the installed proofs of a refused model passing an account and of a chat's chosen model.
- **LIMITRESUME1**, too: no family's limit sentence is recorded (`ClaudeLimits.Words.Recorded`).
- Posture words are still strings with marks (`PosturesPart`, `PostureAsked.Wire`), to become a typed record with ACCESS1's row l; the
  Agents page and the role editor do not yet show the currency report and the model hint the terminal says.
