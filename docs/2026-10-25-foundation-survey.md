# The foundation as it is: who acts, how work moves, what is said — a survey

> FOUNDATION1, phase 1 of a foundational co-design under D191 (2026-10-25). The factual ground the three sealed proposals
> stand on; the direction, the constraints and the questions are `docs/2026-10-25-foundation-brief.md`. Read from the code at
> `0e195ee61`, every line number that commit's; nothing was run. Where a fact was inferred from the code rather than seen in a
> test or a record, it says *unverified*. `Dnn` is `docs/decisions/Dnn.md`. Paths: `Driver/` is
> `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Host/` is
> `src/Daoris.Desktop/Daoris.Desktop.Driver.Host/`, `Core/` is `src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` is
> `src/Daoris.Service/Daoris.Service.Mcp/`, `Http/` is `src/Daoris.Service/Daoris.Service.Http/`, `Web/` is
> `src/Daoris.Web/src/`. It proposes nothing: §5 states the seams as questions.

## 0. In one page

Each of the three areas grew several mechanisms side by side, one per design that needed it, and the places where they answer the
same question differently are where the defects come from. Of the 273 `docs/FIX-LOG.md` entries dated 2026-10-15 to 2026-10-25,
51 came from such a disagreement: 8 in who acts, 26 in how work moves, 17 in what is said (§1.6, §2.6, §3.6; the reading is a
judgement, said in §6).

| Area | What there is | Its strongest duplications |
|---|---|---|
| **Who acts** (§1) | thirteen kinds of participant; two spawn seams and three kinds outside them; eight actors (D205); no Fabric `Participant` type, a participant's identity its session record's id | a role both stored (`actor`) and read from five marks, by 27 lines and more (§1.3 row 8); five rule shapes with three defaults (row 1); four walks of an actor's order and five re-asks of a refused model (rows 3, 5); seven predicates for which kinds a tool refuses, with a reviewer and a brainstorm voice passing the server's role filter (rows 7, 15, 16); two doors for the two roles (row 22) |
| **How work moves** (§2) | fourteen paths, in two loops (the look, the chat runner), through one brief composer and one say door | *the own account cools: wait or move?* as one flag at four sites, the quest's example (§2.4 row 1); seven answers to *which account* (row 2); one durable record of a move in flight, the ended limit move's, of five kinds of move (§2.5); a limit read three ways (row 8); the transient retry on one door and one kind (row 6) |
| **What is said** (§3) | fifteen channels: fourteen pushed slices beside two invalidating events and a third shape of held answers; two dozen `DAORIS.*` bus events; the log; event and lineage files; hook points; the watches and the chain; notices; the channel | four names for four sets of *a session ended* (§3.4 row 1); a park told five ways with two baselines and two explanations (rows 2, 6); push, invalidate and hold side by side (row 3); a lineage written by four and read by none, two chain ranks bound nowhere (row 9); six ordering rules (row 7) |

**Where the code is already one place**, which a foundation can stand on: the launcher every start goes through
(`Driver/Platform/AgentStarts.cs:30`); the brief composer (`Driver/Sessions/Events/ContextBriefs.cs:59`); the service's say door and
`said` (`Core/Sessions/SessionLedger.cs:891`); the error reader (`Driver/Agents/Errors/AgentErrors.cs:133`); the actors' judge
(`Driver/Platform/ActorChoices.cs:49`); the account set's projection (`Driver/Accounts/Harnesses/AccountSet.cs:29`); the slice owner
(`Modules/Platform/StateSlices.cs:76`); the home's table of entries (`Core/Platform/DaorisHome.cs:317`). Each is read beside a
mechanism that does part of its job another way (the rows cited above).

The prior maps this survey extends rather than repeats: D173's C0 audit (`docs/2026-10-16-fabric-contract-audit.md` §1, one store per
concept, 2026-10-16), the actors brief (`docs/2026-10-23-actors-brief.md` §3-§4, every start), and the §1 of each design in flight:
EVENTHOOK1 (the hook set and the attention watch), COWORKVIEW1 (who works beside a session), SWITCHCTX1 (every move a line makes),
AGENTERRORS1 (what each door hands Daoris on a failure).

## 1. Who acts

Every kind of participant Daoris starts or admits. **Two spawn seams take the actor as their first argument**:
`Driver.HoldAsync(string actor, …)` (`Driver/Loop/Driver.cs:3631-3640`) for Build, Intake, Helper, Second opinion, Partner and
Brainstorm, and `ChatRunner.RunAsync(string actor, …)` (`Driver/Conversations/Chat/ChatRunner.cs:1381-1383`) for Chat and Ask
Daoris. Every harness start goes through the launcher's containment (D201), `AgentStarts.StartAsync` with kind `Agent` or `Piece`
(`Driver.cs:3650`, `ChatRunner.cs:1556`, `Loop/DriverContinuations.cs:1242`, `:1299`). Three participants stand outside both seams:
the role holders, in a terminal tab outside the jobs (`Modules/Windows/TerminalModule.cs:263-317`); native subagents, which Daoris
never starts; and meeting turns, which nothing starts.

**No Fabric `Participant` or `RuntimeBinding` type exists** (D173 ADR-B): `src/` holds neither. The nearest projection is
`WorkUnit`, whose kinds are branch, quest, ask and run, with sessions as its executors (`Driver/Trees/Inventory/WorkUnit.cs:4-20`,
`:140-144`). A participant's identity is its session record's id, handed to the connector as `DAORIS_SESSION_ID`
(`Core/Asks/IntakeScope.cs:28-33`).

### 1.1 The participants

| Kind | Starts at | Its rule | Its admission | Its actor and account | Its role on the record | Its record and identity |
|---|---|---|---|---|---|---|
| **Driven session** (Build; a workflow's `claimed` and `look` nodes) | `Driver.RunAsync` (`Driver.cs:1906-2976`) → `HoldAsync("build")` (`:2727-2728`); a park or words through `DriverContinuations.ContinueAsync` (`:175-188`, `:431-432`); the nodes are projections onto the quest's session (`Driver/Workflows/Domain/WorkflowGraphRun.cs:312-314`) | `drivable`, `trees` (`Driver/Platform/DriverConfig.cs:1610`, `:1616`); `access` at three scopes (`:1676-1678`); `handOver` (`:1666-1667`); `actors.build`, else `adapter` (`Driver/Platform/ActorChoices.cs:131-144`) | a clean root unless trees are on (`Driver.cs:1949-1956`); the walk's passes (`:735-747`); the ledger's busy tree (`Core/Sessions/SessionLedger.cs:1597-1601`); containment `Agent`; watched, never timed out (`Driver.cs:3674-3683`) | a first start walks Build's order (`Driver.cs:2005-2012`, `WalkBuildPassesAsync` `:710-779`); a handed quest `Effective("build", handed)` (`:2019`); **a carry-on runs on `handed ?? config.Adapter` with the last record's model** (`:1917`, `:2020`) | `Kind = Driven` with a `Quest` (`Core/Sessions/Sessions.cs:44-47`, `:101-103`); `actor: "build"` (`Driver/Service/ServiceClient.cs:648`); `From`, `FromWhy`, `FromBy` (`Sessions.cs:175-181`); `Entry` (`:237`) | the service's session record (`SessionLedger.cs:1613-1616`); `DAORIS_SESSION_ID`; a channel member only where its entry's origin is an ask (`Core/Asks/Channel.cs:1166-1173`) |
| **Repository chat** | `START_CHAT` (`Modules/Bridge/Routes/ConversationRoutes.cs:38-80`) → `ChatRunner.StartAsync` (`ChatRunner.cs:477-687`) → `RunAsync("chat")`; start-from, go-on (`Chat/ChatGoOn.cs:644-645`), hand-over, limit and briefed moves | `trees`; Chat's actor (legacy `adapter`); `handOver` | an interactive adapter, a registered checkout (`ChatRunner.cs:526-547`); the walk; the ledger (`SessionLedger.cs:652-698`); `question_ask` refused (`Core/Sessions/QuestionBox.cs:181-182`) | `Effective("chat", pick)` else `config.Adapter` (`ConversationRoutes.cs:50-51`); `SelectAsync(Conversation)` (`ChatRunner.cs:564`, `:932`); go-on keeps the record's | `Kind = Chat`, no quest, ask, opinion or serves (`Sessions.cs:54`); `actor: "chat"` (`ServiceClient.cs:701`); its own entry | its record; `DAORIS_SESSION_ID` (`ChatRunner.cs:2968-2969`); not a channel member (`Channel.cs:1169-1173`) |
| **Ask Daoris** | `START_HELP` (`Modules/Bridge/Routes/HelpRoutes.cs:46-104`) → `RunAsync("ask-daoris")` (`ChatRunner.cs:1032-1034`) | `helperAdapter` as its on/off (`HelpRoutes.cs:52-57`); `actors.ask-daoris`; `helpMode` (`DriverConfig.cs:1635`, `:1640`) | one per workspace (`SessionLedger.cs:612-618`); restricted mode's `HelpRoom.Allowed` (`Driver/Help/HelpRoom.cs:65-76`); workspace-bound (`Mcp/WorkspaceScope.cs:31-56`); refused `question_ask` and `handover_propose`, **not `helper_ask`** (`Core/Sessions/HelperBox.cs:131-138`) | `Effective("ask-daoris")` (`HelpRoutes.cs:100`), walked (`ChatRunner.cs:790`) | `Repository == "daoris:help"` (`SessionLedger.cs:432`, `:438`); `HelpMode` (`Sessions.cs:193`); `actor` | its record; `DAORIS_SESSION_ID` and `DAORIS_SESSION_WORKSPACE` (`Core/Registry/AmbientWorkspace.cs:35`, `:44-45`) |
| **Intake** | `DriverIntakes.IntakesDueAsync` (`Loop/DriverIntakes.cs:55-109`) → `HoldAsync("intake")` (`:348-349`) | Intake's actor, else `intakeAdapter` (`DriverConfig.cs:1633`); off by `ActorChoices.FirstAgent("intake")` (`DriverIntakes.cs:63-71`) | a proposed ask with no intake, the oldest of its circle (`:91-95`); slots (`:98-105`); one per workspace (`SessionLedger.cs:405`); `IntakeRoom.Allowed` (`DriverIntakes.cs:344-346`) | `Effective("intake")` (`:198`), walked (`:121-180`) | `Kind = Chat` with `Ask` (`Sessions.cs:127-135`); entry `ask:<id>/intake` (`SessionLedger.cs:412-415`) | its record; `DAORIS_SESSION_ID` and `DAORIS_ASK_ID` (`IntakeScope.cs:25-36`); publishes as its ask (`Mcp/QuestTools.cs:114-130`); the channel admits it (`Channel.cs:1164`) |
| **Helper** (D163) | `DriverHelpers.HelpersAsync` (`Loop/DriverHelpers.cs:40`) → `HoldAsync("helper")` (`:1013-1014`); asked by `helper_ask`, `SESSION_HELPER_ASK` (`Modules/Bridge/Routes/SessionsRoutes.cs:139-187`), `sessions helper` | `helpers`, `workspaceHelpers` {mode, agents} (`Driver/Conversations/Domain/HelperRules.cs:11-28`), default `on` over the set-up agents (`:18`) | `HelperJudge.Judge` (`HelperJudge.cs:61-85`), binding agents' asks only (`:67`), two per asker (`:54`); `HelperDesk.OpenAsync` (`Core/Sessions/HelperDesk.cs:69-111`); the client's `OpinionPermissions.Shape` (`Driver.cs:3108-3109`); the server's `ServingRefused` (`Mcp/RoleFilter.cs:58`) | `HelperChoice.Order` (`Driver/Conversations/Helpers/HelperChoice.cs:43-51`) → **`ReviewerChoice.ChooseAsync`** (`:69-70`); 15 minutes (`HelperInstruction.cs:34`) | `Serves` (`Sessions.cs:158`); its own entry; `actor: "helper"` (`HelperDesk.cs:99-102`) | its record and `data/helpers/<ask>.{ask,state,answer,capture,back}.json` (`HelperBox.cs:43-44`; `HelperFiles.cs:10-16`); its own `DAORIS_SESSION_ID`; its answer a `SaidWord.Helper` (`Sessions.cs:358-362`) |
| **Partner** (D199) | `Driver.PiecesAsync` (`Driver.cs:323-324`) → `HoldAsync("partner", piece)` (`Loop/DriverPartners.cs:641-647`); asked by the person alone (`SESSION_PARTNER_ASK`, `SessionsRoutes.cs:223-244`; `PartnerPersonAsk.cs:70-71`) | **none**: `DriverConfig.Parse` reads no `partners` key (`DriverConfig.cs:1609-1688`); the ask names the agent (`PartnerChoice.cs:25-29`) | `PartnerJudge.Refusal` (`Domain/PartnerJudge.cs:15-28`); two pieces a chain (`PartnerAdmission.cs:34`); a measured row, checked twice (`PartnerChoice.cs:36`, `:43-47`; `DriverPartners.cs:546-551`), **and `PartnerMeasurements.Rows` is empty** (`PartnerMeasurements.cs:44`); the piece's job; the server's `PartnerAllowed` (`RoleFilter.cs:37-40`) | `ReviewerChoice` over the named agent (`PartnerChoice.cs:32-35`); 45 minutes (`PartnerInstruction.cs:28`) | `Serves` and `Piece`, `actor: "partner"` (`Core/Sessions/PartnerDesk.cs:79-82`) | its record, `data/partners/<id>.*` (`PartnerFiles.cs:6-14`), the ref `refs/daoris/pieces/<id>` (`Driver/Trees/Pieces/PieceKeeper.cs:14`); its own id |
| **Reviewer** (D155; its recheck; a workflow's `opinion` node) | `OpinionGateLook.AskPassAsync` (`Loop/OpinionGateLook.cs:356-421`) → `HoldAsync("opinion")` (`DriverOpinions.cs:226-227`); recheck `OpinionDeliverer.cs:355-385`; the person's ask (`TreesRoutes.cs:845`) | `opinions`, `workspaceOpinions` (`Review/Domain/OpinionRules.cs:19-27`), or a workflow's (`OpinionLook.cs:29`) | the occasions (`OpinionLook.cs:68`); the maker passes (`ReviewerChoice.cs:196-201`); `OpinionDesk` (`Core/Review/OpinionDesk.cs:128-131`); the client's `OpinionPermissions`; **the server's role filter: nothing** (`RoleFilter.cs:113`) | `Effective("opinion", fence: reviewers)` (`ReviewerChoice.cs:228`); the same-agent press `record?.Adapter ?? Config.Adapter` (`OpinionGateLook.cs:393`) | `Opinion`, no `Serves` (`Sessions.cs:148`); `actor: "opinion"` | its record, the `opinions` table (`Core/Review/OpinionStore.cs:34`), `data/opinions/*`; its findings a `SaidWord.By` (`Sessions.cs:353-357`) |
| **Brainstorm voice** (generators, the synthesizer) | `DriverBrainstorms.RunAsync` (`Loop/DriverBrainstorms.cs:372`) → `HoldAsync("brainstorm")` (`:734-735`); the person's request (`Core/Sessions/BrainstormDesk.cs:85`) | `brainstorms`, `workspaceBrainstorms` {mode, agents ≤ 4, minutes}, never a repository's (`Brainstorms/Domain/BrainstormRules.cs:22-26`, `:66-90`); **its mode is read by no start** | the plan, at most four (`DriverBrainstorms.cs:346-365`); one restart (`:621-631`); the client's `BrainstormPermissions`; the server: nothing, since a pass has no record (`RoleFilter.cs:112-113`) | `Effective("brainstorm", fence)` (`:352-354`); `SelectHandedAsync(Driven)` with **no workspace** (`:682-683`); the synthesizer the first that answered (`:422-434`) | **no session record** (`:658-660`); a pass record with the actor's fields (`:701-709`) | `data/brainstorms/<id>/passes/<pass>.json` (`BrainstormFiles.cs:36-39`); a synthetic id `brainstorm-<id>-<turn>` and `DAORIS_BRAINSTORM_*` (`DriverBrainstorms.cs:664-668`; `Core/Sessions/BrainstormBox.cs:15-26`) |
| **Meeting participant** (D177) | the brainstorm's pass with `MeetingTurns ?? Unavailable` (`Loop/DriverMeetings.cs:244-249`) | the brainstorm's policy | **no real agent starts**: `MeetingTurns` has no production value (`Driver.cs:447-452`), so every turn is refused (`DriverMeetings.cs:320-322`) | no actor (`ActorChoices.cs:37`) | — | `data/brainstorms/meetings/` (`MeetingFiles.cs:38`) |
| **Co-worker** (D203) | built: `serves` on helpers and partners, `piece`, HELPERWAIT1's `ended.awaits-helper` (`SessionLedger.cs:1864`) | — | — | — | not built: a reviewer's `serves` (`OpinionDesk.cs:128-131`), `after`, `sessions team`; the page reads any `serves` as a helper (`Web/work/identity.ts:79`) | no team record; derived |
| **Supervisor holder** (D208) | `SET_APPOINTMENT` (`Modules/Bridge/Routes/PersonRoutes.cs:134-161`) → `StandingConfirmation.AppointAsync` (`Modules/Platform/StandingConfirmation.cs:822-1013`) → a terminal tab (`TerminalModule.cs:263-317`); **not offered** (`StandingConfirmation.cs:330`) | a repository's or workspace's appointment (`:831-836`, `:1134-1170`) | the caller look, the generation, the live holder, an HMAC proof over the hello's nonce, `HolderWalk.Check` (`:1213-1264`; `Driver/Sessions/Questions/SupervisorProof.cs:313-328`; `Modules/Platform/HolderWalk.cs:36-76`); the image check (`Driver/Platform/InstanceDoor.cs:157-169`) | **none**: an `actor` holder is refused (`StandingConfirmation.cs:829-830`); a pinned managed `claude-code` on its own sign-in, outside the roster, no model (`HolderTerminal.cs:42-49`, `:195`, `:216-241`) | the appointment in `data/standing-confirmation.json` (`:771-806`); a word `From: supervisor` (`Sessions.cs:420`) through `SuperviseAsync` (`SessionLedger.cs:991-1068`) | no session record, no connector (an empty MCP configuration, `HolderTerminal.cs:44`); a holder id, the generation and `DAORIS_ROLE_KEY`'s proof |
| **Steward holder** (D209) | the same path, sharing a holder or its own (`StandingConfirmation.cs:867-894`); **not offered** (`:336`) | scope `machine` (`:831-836`) | the same judge; `ROLE_ACT` against the table, its `acts`, form and `owner-set` (`:1273-1290`); rows drive to desk-decline (`StewardActRows.cs:152-162`) | none; as the supervisor's | the same file; its acts in memory alone (`StandingConfirmation.cs:1412-1445`); `*-steward` notes (`NoteCodes.cs:500`, `:568`, `:791-792`) | as the supervisor's |
| **Native subagent** (D173 C1a, C2a) | the harness's own; Daoris starts none | — | **`CallLineageFilter` records and decides nothing** (`Mcp/CallLineageFilter.cs:10-22`); C2a is held off main (`TASKS.md` FABRIC-C2a); a child takes its parent's role at the server (`RoleFilter.cs:28-30`) | the parent's | none (`Driver/Sessions/Domain/ChildBindings.cs:9`) | `data/sessions/<id>/lineage.jsonl` (`Core/Sessions/LineageFile.cs:13-14`), folded by `ChildBindings.Fold` (`:128-266`) and **read by nothing in production** (§3.1); the parent's `DAORIS_SESSION_ID` |

### 1.2 Which starts resolve an actor

D205's eight actors resolve at their starts (MODELROLE1e built), with these exceptions:

| Start | Actor | Account |
|---|---|---|
| a quest's first start | walked (`Driver.cs:715`) | `SelectHandedAsync(Driven)` per entry (`:721`, `:752`) |
| a handed quest | `handed` (`Driver.cs:2019`) | the named account first (`:2021-2023`) |
| **a carry-on or retry** | **none**: `handed ?? config.Adapter`, the last record's model (`Driver.cs:1917`, `:2020`) | `CarriedOnAsync` (`:2412-2413`) |
| a resume, a park's go-on | the record's (`DriverContinuations.cs:232`, `:432`) | `ResumeAsync` |
| intake | walked (`DriverIntakes.cs:198`) | `SelectAsync(Driven)` (`:141`, `:227`) |
| a new chat | the pick or the walk, else `config.Adapter` (`ConversationRoutes.cs:50-51`) | `SelectAsync(Conversation)` |
| **a terminal's start-from** | **the first agent only, not walked** (`Modules/Bridge/Routes/SessionsVerbs.cs:33`) | as a chat |
| a chat's go-on | the record's (`ChatGoOn.cs:526`, `:644-645`) | `ResumeAsync(walks: false)` (`:571`) |
| Ask Daoris | walked, gated on `helperAdapter` (`HelpRoutes.cs:52`, `:100`) | `SelectAsync(Conversation)` (`ChatRunner.cs:813-814`) |
| a reviewer; its recheck; the same-agent press | walked with the fence (`ReviewerChoice.cs:228`); the first pass's choice (`OpinionDeliverer.cs:366-369`); `record.Adapter ?? Config.Adapter` (`OpinionGateLook.cs:393`) | `SelectAsync(Driven)` (`ReviewerChoice.cs:394-395`) |
| a helper; a partner | walked (`HelperChoice.cs:43-51`); the model only, the agent the ask's (`PartnerChoice.cs:32-35`) | as a reviewer |
| a brainstorm voice | frozen per request (`DriverBrainstorms.cs:352-354`) | `SelectHandedAsync`, no workspace (`:682-683`) |
| a meeting; a role holder | none | none; the holder's own sign-in |

### 1.3 Duplications and disagreements

| # | The question | Where it is decided, and how the places differ |
|---|---|---|
| 1 | **The rule that says who may run a kind** | five shapes, three sets of scopes, three defaults: `helpers` {mode, agents}, repository or workspace, default `on` (`HelperRules.cs:11-28`, `:167-172`); `handOver` {mode}, default `ask` (`HandOverRules.cs:13-16`, `:122`); `opinions` {reviewers, on, required, verify, minutes, recheck}, default none (`OpinionRules.cs:19-27`, `:294`); `brainstorms` {mode, agents, minutes}, machine or workspace (`BrainstormRules.cs:22-26`, `:247-252`), its mode read by no start; `partners` absent (`DriverConfig.cs:1609-1688`); Intake and Ask Daoris a key's presence (`:1633`, `:1635`). D163 point 3 still reads `helpers` as `ask` by default (`D163.md:15-17`); only its HELPERDEFAULT1 note moves it |
| 2 | **Choosing a co-worker's agent** | `HelperChoice` and `PartnerChoice` both go through `ReviewerChoice.ChooseAsync` by an `OpinionRule` (`HelperChoice.cs:69-70`; `PartnerChoice.cs:32-33`), borrowing the opinion's rule type, so a helper that cannot be chosen is told *No second opinion: …* (`HelperChoice.cs:66`) |
| 3 | **Walking an actor's order** | four walks: `Driver.WalkBuildPassesAsync` (`Driver.cs:710-779`), `ChatRunner.WalkAsync` (`ChatRunner.cs:906-955`), `DriverIntakes.WalkAsync` (`DriverIntakes.cs:121-180`), `ReviewerChoice.WalkAsync` (`ReviewerChoice.cs:250-290`); a brainstorm none. Two sets of pass codes (`unavailable`, `no-posture`, `no-connector`, … against `not-installed`, `pin-not-installed`, … `ReviewerChoice.cs:89-111`); the reviewer's walk orders by maker and holds an agent's later entries once one is held (`:196-201`, `:270-279`) |
| 4 | **The account walk's call** | `SelectHandedAsync(Driven)` for Build and a brainstorm (the latter with no workspace, so no workspace's pinned version, `HarnessRoster.cs:899`); `SelectAsync(Driven)` for intake, reviewer, helper, partner; `SelectAsync(Conversation)` for a chat and Ask Daoris; `ResumeAsync` for a resume |
| 5 | **A model the open refuses** (D205 §3.5) | five mechanisms: `Driver.ReaskAsync` (`Driver.cs:787-807`), the intake's (`DriverIntakes.cs:171-179`), `OneTurnReask` for opinion, helper and partner (`Loop/OneTurnReask.cs:11`, `:49`, `:70`), `ChatPending` (`Chat/ChatPending.cs:19`), the brainstorm's one restart (`DriverBrainstorms.cs:621-631`); the continuations hand none (`DriverContinuations.cs:456`; `DriverBrainstorms.cs:760`) |
| 6 | **May this session ask a helper?** | five copies checking `serves`, `opinion` and `ask`: the page (`SessionsRoutes.cs:168-175`), the terminal (`SessionsHelperCommand.cs:77-79`), the driver (`HelperJudge.cs:64-65`), the service (`HelperDesk.cs:85-91`), the connector (`HelperBox.cs:131-138`); **none refuses Ask Daoris**, while the partner's twins do (`PartnerPersonAsk.cs:71`; `PartnerJudge.cs:18`) and its desk does not (`PartnerDesk.cs:65-71`) |
| 7 | **Which kinds each tool refuses** | seven predicates, each its own set: `QuestionBox.cs:172-182`, `HandOverAskBox.cs:102-111` (Ask Daoris refused too), `Channel.cs:1164` (helper and reviewer only, so an intake posts), `HelperBox.cs:131-138`, `OpinionDesk.cs:107-112`, `RoleFilter.cs:68-84` (`serves` and `piece` only) |
| 8 | **A record's role: stored, or read from marks?** | stored as `Actor` (`Sessions.cs:196`), by the driver for build, chat, intake and Ask Daoris (`ServiceClient.cs:648`, `:701`, `:779`, `:810`) and by the desks for helper, partner and opinion (`HelperDesk.cs:102`; `PartnerDesk.cs:82`; `OpinionDesk.cs:131`); read from `Ask`, `Opinion`, `Serves`, `Piece`, `HelpMode` and `Repository == "daoris:help"` (`Sessions.cs:135-193`; `SessionLedger.cs:438`) by at least 27 lines outside tests, the page (`identity.ts:70-79`) and the cap's `ActiveSessions.HoldsSlot`, which tells the driver's own sessions by their kind, place and marks (`Driver/Quests/Domain/Planner.cs:872-873`; §1.6, CAPCOUNT1). The role filter reads neither `Actor` nor `Opinion` (`RoleFilter.cs:113`) |
| 9 | **The eight actors' ids** | two hand-kept lists: `ActorChoices.Ids` (`ActorChoices.cs:37`) and `ActorRun`'s constructor (`Driver/Sessions/Domain/ActorRun.cs:176-177`) |
| 10 | **Is Intake, is Ask Daoris off?** | Intake by its actor's first agent (`DriverIntakes.cs:63`); Ask Daoris by `helperAdapter` (`HelpRoutes.cs:52`), then its actor for the order (`:100`); aligned only by the write-through (`ActorChoices.cs:171-177`) |
| 11 | **Falling back to the machine's `adapter`** | `Effective`'s *as before* (`ActorChoices.cs:137-144`), and seven more sites that read `config.Adapter` beside the actor: `ConversationRoutes.cs:51`, `:173`; `OpinionGateLook.cs:393`; `Driver.cs:1917`; `Driver.cs:1107` (the look's report names `handed ?? config.Adapter` before the start walks Build, `:2009-2011`); `Trees/SessionGroups.cs:882`; `BriefedMove.cs:113`; `ChatGoOn.cs:526`. Where Build's walk takes a later entry, the report and the resume's judgement name an agent that did not run |
| 12 | **A carry-on's agent against its record's choice** | the carry-on's agent is `handed ?? config.Adapter` (`Driver.cs:1917`), its model the last record's asked one (`:2020`), while a resume requires the record's own adapter (`DriverContinuations.cs:40-45`): the model can come from another agent's entry (*unverified*, read from the code) |
| 13 | **One start, two doors** | the route's start-from passes Chat's order (`ConversationRoutes.cs:171-174`); the terminal's passes its first agent (`SessionsVerbs.cs:32-33`) |
| 14 | **What a partner is handed** | `HandRules`' flags per kind (`Driver.cs:3080-3128`) and `PartnerHanded`'s recomputation for the admission digest (`:3150-3155`), which must agree for a measured row to match (`DriverPartners.cs:546-548`) |
| 15 | **Where a kind's tools are held** | the server's `RoleFilter` for a partner and for anything with `serves` (`RoleFilter.cs:37-58`); the client's rules for a reviewer (`OpinionPermissions.cs:27-45`), an intake (`IntakeRoom.cs:49-58`), Ask Daoris (`HelpRoom.cs:65-76`) and a brainstorm (`BrainstormPermissions.cs:34-67`), handed only where the adapter takes settings (`Driver.cs:3087`). Five lists; the partner's and the brainstorm's held by a census; on Codex only the per-tool checks bind the others |
| 16 | **A caller the filter cannot place** | `RoleFilter`: no record means nothing refused (`RoleFilter.cs:109-113`), so a brainstorm's synthetic id or a forged one passes, and a ledger read that fails holds every call to the partner's read list (`:136-141`, `ConnectorRoles.Unread` `:90-93`); C2a's rule is the opposite, fail closed (`D173.md:140-145`) |
| 17 | **Seeing native children** | the lineage file, folded and read by nothing in production; the console's streams (`Driver/Agents/Acp/AcpStreams.cs:81-84`; `Driver/Sessions/Domain/SessionOutput.cs:25-51`), the only one shown; the protocol door writes no stream lines, Codex gets no hook |
| 18 | **Who said a word** | three author fields: `SaidWord.By` (an opinion), `.Helper` (a helper **or** a partner), `.From` (`daoris`, `agent`, `supervisor`) (`Sessions.cs:392-424`); a partner is written as a helper (`PartnerDesk.cs:94-96`) |
| 19 | **A one-turn kind's time** | a constant each: helper 15 minutes, partner 45, a reviewer's rule minutes (`OpinionPass.cs:127`), a brainstorm's (`BrainstormRules.cs:8-15`); a driven session watched, never timed |
| 20 | **Concurrency** | separate counters, each counted its own way: two helpers an asker (`HelperJudge.cs:54`), two pieces a chain (`PartnerAdmission.cs:34`), one Ask Daoris a workspace (`SessionLedger.cs:612-618`), one intake a circle (`DriverIntakes.cs:91-95`), four voices (`BrainstormRules.cs:78`), the cap's slots (`ActiveSessions.HoldsSlot`) |
| 21 | **A role holder's agent and account** | a pinned `claude-code` on its own sign-in, no model (`HolderTerminal.cs:42-49`, `:195`, `:216-241`), outside the pool, the cool-offs, the usage reading and D205, where every other start goes through `HarnessRoster` and the actors |
| 22 | **The supervisor's door against the steward's** | `SUPERVISOR_ANSWER`/`DEFER` with `SupervisorProof.Canonical` (`SupervisorProof.cs:284-310`) against `ROLE_ACT` with `RoleActForm.Bytes` (`Driver/Sessions/Questions/RoleActForm.cs:48-63`): two verb families, two proof formats with two whitespace rules (`SupervisorProof.cs:254-256`; `RoleActForm.cs:100-102`), two readings of *a driven repository* (`Drivable`, `DriverLoop.cs:857-858`, against the registry, `StewardActRows.cs:64-65`), two ways the owner takes over (per question, `SessionLedger.cs:838-845`; `owner-set`, `StandingConfirmation.cs:1327-1359`), decisions in files against acts in memory; the refusal sentences say *the supervisor* for both (`Modules/Platform/InstancePipe.cs:1365-1381`) |

### 1.4 Where the code and a decision disagree

| Decision | It says | The code |
|---|---|---|
| D173 ADR-B | `Participant` and `RuntimeBinding` identity (`D173.md:11-12`) | no such types; `WorkUnit` alone (`WorkUnit.cs:140-144`) |
| D173's guard and C2a | a child's identity is issued, never inherited; a child's calls read-only by lineage, failing closed (`D173.md:35-38`, `:135-166`) | children call with the parent's id and role (`Core/Sessions/CallLineage.cs:11-14`; `RoleFilter.cs:28-30`); the filter decides nothing; C2a held off main |
| D173 C1a | the lineage binds a child to its part, for the view | written, read by nothing in production (`ChildLineage.cs:125-139`) |
| D203 points 1, 2, 6 and 10 | the role read, never stored; a reviewer's `serves`; server allow-lists for helper and reviewer; Ask Daoris refused at both helper judges; `after`, `sessions team` (`D203.md:18-24`, `:42-44`, `:64-65`) | `Actor` is stored beside the marks (row 8); none of the rest is built (`OpinionDesk.cs:128-131`; `RoleFilter.cs:37-58`; `HelperJudge.cs:65`); COWORKUX1 open |
| D199 point 2; D205 point 6 | a `partners` rule and the lead's `partner_ask`; the rules *stay where they are* (`D199.md:20-21`; `D205.md:44-45`) | no `partners` key, no `partner_ask`; the person's ask only; COAGENT1d open |
| D199 point 4 | admitted only where measured | held, but with no row no production partner starts |
| D208 point 2 | a holder may be `actor` | refused (`StandingConfirmation.cs:829-830`); SUPERVISOR1c open |
| D209 point 3 | the steward's acts include the hand-over, the yes to an agent's ask, the go-on and a set-up | no such rows (`StewardActRows.cs:152-162`; `RoleActs.cs:47-63`); `HandOverBy` has no steward (`Driver/Conversations/Domain/HandOverRecords.cs:91-103`) |
| D208's SUPERVISOR1e (a workspace's scope) | built | the brief still says *the repository it was appointed for* (`Driver/Sessions/Questions/RolesBrief.cs:31`); a route's remark calls the steward's table empty, and it has rows (`SessionsRoutes.cs:623-627`) |

### 1.5 The records this area writes

| Record | Where | Writer | Process |
|---|---|---|---|
| Session records of every kind with one | the service's `sessions` table (`Sessions.cs:476`) | `SessionStore.CreateAsync` (`Sessions.cs:788-858`) through the ledger's opens (`SessionLedger.cs:360`, `:582`, `:652`, `:1613`) and the desks (`HelperDesk.cs:99`; `PartnerDesk.cs:79`; `OpinionDesk.cs:128`) | service |
| The actor's provenance (`actor`, `choice`, `asked`, `override`, `ran`) | the same record (`Sessions.cs:196-204`) | `SessionStore.KeepActorAsync` (`:1408-1423`), driven by `ActorRun` (`ActorRun.cs:162-216`) | service, from the driver |
| Opinions | the `opinions` table; `data/opinions/gates`, `data/opinions/due.json`, `work/opinions/*` | `OpinionDesk`; the driver's gates and dues | service; driver |
| A helper's box | `data/helpers/<ask>.*.json` | `HelperBox` (ask, answer); `HelperFiles` (state, capture, back) | service; driver; modules |
| A partner's box and pieces | `data/partners/<id>.*`; `refs/daoris/pieces/<id>` in the repository | `PartnerFiles`, `PartnerBox`, `PieceKeeper` | driver; service |
| Questions | `data/sessions/<s>.question.json` (`QuestionBox.cs:41`, `:61`) | `QuestionBox` writes; `QuestionFiles` reads, clears, restores | service; driver |
| Supervisor decisions | `sessions/<s>.decisions/<id>.json`, `.states`, `sessions/<s>.supervisor.json` (`QuestionSupervisorDecisions.cs:17-22`, `:43-44`) | `QuestionSupervisorDecisions` | desktop |
| Appointments | `data/standing-confirmation.json`'s `appointments` (`StandingConfirmation.cs:771-806`) | `StandingConfirmation` | modules |
| Holder rooms | `work/rooms/roles/holders/<id>/`, `work/rooms/roles/config/` (`HolderTerminal.cs:15`, `:69-124`) | `HolderTerminal` | modules |
| The steward's effects | `driver.json`'s `drivable`, `trees`, `holds`, `roleReleases` (`StewardActRows.cs:246-251`, `:383`) | the act handlers | modules, into the driver's configuration |
| Brainstorms and meetings | `data/brainstorms/<id>/*`, `data/brainstorms/meetings/<id>/*`; rooms `work/brainstorm-rooms/<id>` | `BrainstormDesk`; the driver's `BrainstormFiles`, `MeetingFiles` | service; driver |
| Rooms | `work/rooms/intake/*`, `work/rooms/help-*` | `IntakeRoom`, `HelpRoom` | driver |
| Actors and rules | `driver.json`'s `actors`, `intakeAdapter`, `helperAdapter`, `helpers`, `opinions`, `handOver`, `brainstorms`, `access` (`DriverConfig.cs:1607-1688`) | `ActorChoices.With`, each `*Rules.Apply` | driver, through the modules' routes and verbs |
| What each agent offered | `data/agent-offers.json` | the driver's offers keeper | driver |
| The native lineage | `data/sessions/<id>/lineage.jsonl` | §3.5 | service; harness; driver |

### 1.6 Defects of the last ten days that came from these disagreements

8 of the 273 entries (2026-10-15 to 2026-10-25); the last three are borderline, a reviewer's look and the landing each judging
the same hold.

| Date | Entry | The two that disagreed |
|---|---|---|
| 2026-10-24 | An intake, an opinion and a helper a limit cut off never ran again by themselves (LIMITRESUME1h) | each kind's end handling a limit its own way, and the gate's and look's re-ask rules |
| 2026-10-24 | A second opinion judged its independence against the line's last maker only (LIMITRESUME1j) | the opinion look and its recheck naming the newest part's maker, and D155 point 4's every family that wrote the candidate |
| 2026-10-23 | A session waiting on its own helper was shown waiting on the person (HELPERWAIT1) | `Observation.Conclude` and the helper's delivery, each reading a clean end that holds its quest as a park |
| 2026-10-20 | Idle conversations held the driver's cap, so quests started two at a time (CAPCOUNT1) | the service's active list, a person's chats included, and the driver's own kinds, which the record's kind alone could not tell apart (row 8) |
| 2026-10-20 | Ask Daoris's conversations were listed among the chat sessions (ASKRAIL1) | the rail's filter of the widest slice, and Ask Daoris's own panel reading `daoris:help` apart |
| 2026-10-19 | A session that went on after its landing had its later done's opinion closed before the loop asked it (FABRIC-CLOSE1h18) | `OpinionLook.ClosesAsync` and `AutoLander.JudgeAsync` on a landing's acceptance against its entry's done |
| 2026-10-19 | A second opinion holding an ordinary automatic landing was never asked where no due was open (FABRIC-CLOSE1h17) | the auto-lander keeping the opinion's hold with no due, and the opinion look asking only what is owed |
| 2026-10-19 | A second opinion holding a push held back was never asked, and the person's ask was dropped (FABRIC-CLOSE1h15) | the opinion look not reading the landing's `push-held` entry, which exists only there |

## 2. How work moves

Every path that decides where a line's next words or work go (D167: a line is one entry of records, its parts), and with what
context. The newest commit, `0e195ee61`, is documents only (`TASKS.md`, D137's note of 2026-10-25, the index): LIMITSWITCH1's
rule is decided and not built, so every place §2.4 row 1 names still holds the older rule.

### 2.1 What every path goes through

| Piece | Where | What it decides |
|---|---|---|
| The resume's own-account judge | `Driver/Accounts/Rotation/ResumeAccount.cs:25-45` (`Judge`), `:66-100` (`ResumeChoice`) | In order: missing, outside the scope or kept (driven only), refused, cooling, signed out, else ready. `Waits` (`:78`) and `Elsewhere` (`:90-92`) say what the caller does |
| `HarnessRoster.ResumeAsync` | `Driver/Accounts/Harnesses/HarnessRoster.cs:962-1042` | The record's own account, asked as a pick (`:1023`). Cooling with neither `newSession` nor `limited` returns a hold carrying the cool-off (`:1008-1016`); any other hold walks where `walks`, else refuses (`:1029-1041`); `limited` first freshens the maker's reading (`:1001-1006`) |
| The walks | `SelectAsync` `HarnessRoster.cs:908-912`, `SelectHandedAsync` `:923-934`, `CarriedOnAsync` `:1052-1058` | A start's walk; a named account first then the walk within the same agent; a carry-on keeps the resume's account where it had one (`:1056-1058`) |
| A pick | `HarnessRoster.cs:1370-1374`, `:1171-1179` | A picked account that cools is refused, never rotated |
| The earliest reset | driven `HarnessRoster.cs:1732` (`Unready`, over the accounts the start may use); conversations `:251-256` (`CoolingsOf`, every cooling entry of the agent's owner) | two computations of one wait |
| Can it resume? | `Driver/Conversations/Domain/Continuation.cs:207-239` (pure), `Driver/Conversations/GoOn/ContinuationJudge.cs:13-15` (disk) | never, ended, adapter, account, profile, tree, unkept, door; kinds `Never` (`:30`), `Holds` (`:38`), `Lost` (`:46`) |
| The one brief composer | `Driver/Sessions/Events/ContextBriefs.cs:59-95` over `Driver/Conversations/Domain/ContextBrief.cs:162` | called by the limit move (`Chat/LimitMove.cs:127-128`, `:303-305`), the hand-over's take (`HandOver/HandOver.cs:250-252`), the briefed move (`Chat/BriefedMove.cs:126-128`, `:222-223`) and the driven carry-on (`Loop/Driver.cs:2551-2555`) |
| The words' doors at the service | `Core/Sessions/SessionLedger.cs:891` (say), `:1221` (attempt), `:1267` (take back) | `said` on the record; a key already waiting answers with that word |

### 2.2 The paths

Each row: the trigger and who may pull it; (1) the agent; (2) the account and what a cooling account does; (3) the same
conversation or a new part; (4) the context; (5) the words; (6) what it writes. *Chat* below is `Driver/Conversations/Chat/`.

| Path | Trigger, who | (1) Agent | (2) Account; cooling | (3) Conversation | (4) Context | (5) Words | (6) Writes |
|---|---|---|---|---|---|---|---|
| **A. Go-on, driven** (MSG1b; D137 points 2-7) | words kept on a parked or ended record (`Modules/Loop/SessionWords.cs:380-468`, `:681-730`), planned by `Planner.WordsOn` (`Driver/Quests/Domain/Planner.cs:1597-1615`) as a `GoOn` (`:1621-1649`), run at `Loop/Driver.cs:1906`. The person (`SESSION_INPUT`, `Modules/Bridge/Routes/ConversationRoutes.cs:93-94`; `sessions say`, `Driver/Sessions/SessionsCommand.cs:307`), Daoris, a helper's answer, findings, the supervisor's word (`Loop/DriverContinuations.cs:241-261`) | `handedQuest?.Agent ?? config.Adapter` (`Driver.cs:1916-1917`); resumes only on the record's agent (`DriverContinuations.cs:40-45`) | `ResumeAsync(own, newSession, limited)` (`Driver.cs:1985-1991`). **Cooling: held**, with *Go on in a new session* or, for a closed quest, *Start a conversation* (`Driver.cs:2031-2039`; `DriverContinuations.cs:118-149`). Limited or the person's new session: walks. Gone, outside, kept, refused, signed out, not offered: walks at once | same record and conversation where the judge passes (`DriverContinuations.cs:175-551`); else a new part, why `retry` (`Driver.cs:2346-2434`, `:4631-4632`); a closed quest's lost conversation goes to the chat runner (`Driver.cs:2377-2381`) | none on a resume (`DriverContinuations.cs:373-383`); a new part the brief (`Driver.cs:2551-2556`) | read again after the ledger's move (`DriverContinuations.cs:346-355`); only the person's go to a new part (`Driver.cs:2390-2394`); words during the run to `DrivenInbox`; the native door resumes again with held words (`DriverContinuations.cs:1184-1281`) | ledger `working` (`:272-274`), words taken off (`:799`), marks, `session.reopened`/`session.answered` (`:1133-1138`); a new part's `from`, `moved.on`/`moved.from`, `session.moved` (`Driver.cs:4589-4602`, `:4622-4633`) |
| **B. Go-on, conversation and Ask Daoris** (MSG1c) | `Chat/ChatGoOn.cs`: a heard word (`:269-304`), the sweep (`:136-168`), the come-up (`:191-262`), an answer (`:311-326`), findings (`:358-369`), the limit wake (`:429-448`) | `record.Adapter ?? config.Adapter` (`:526-535`) | `ResumeAsync(…, walks: false)` (`:570-572`; Ask Daoris `:671-673`), never `limited`. **Cooling: waits**, handed to the limit waits (`:573-579`, `:674-680`); elsewhere: `CannotGoOn`, to the person (`:581`) | the same conversation (`:644-656`); proven lost: the limit move's ended path or the briefed move (`:860-907`); a model not offered: a fresh start of its actor (`:518-523`) | none on a resume; the brief where lost | read again after the move (`:608-620`); the refused turns' words first (`:589-631`); taken off as the go-on goes (`:757-782`) | ledger `working`, `.cannot.json`, notes, `session.reopened` |
| **C. Go on in a new session; start from these words** | `SESSION_GO_ON_NEW` (`Modules/Bridge/Routes/SessionsRoutes.cs:118`), `sessions go-on-new` (`SessionsCommand.cs:322`); `SESSION_START_FROM` (`ConversationRoutes.cs:160-178`). The person | the record's; Chat's order for start-from | GoOnNew writes `<s>.new-session.json` (`GoOn/GoOnNew.cs:158-165`) and the next look walks (`HarnessRoster.cs:1038`); **a conversation is refused and pointed to start-from** (`GoOnNew.cs:127-133`) | new part; start-from a fresh conversation with no `from` (`Chat/ChatRunner.cs:319-344`) | the brief; start-from a preface only | the person's | `.new-session.json` |
| **D. Limit move, a live conversation** (LIMITGOON1, LIMITGOON2) | a turn's end the door refused as a limit (`ChatRunner.cs:1629`, `:1644-1649`), then `LimitMove.Due` (`:31-45`). Automatic | the same agent; where none of its accounts has room, the actor's other choices (LIMITGOON2, `LimitMove.cs:444-469`), never for a pick (`:447`) | a walk (`:85-86`); the same account or none: elsewhere or it waits (`:88-104`); then `runner.StartAsync(profile: null)` **walks again** (`:141-145`) | always a new part (`:12-13`), the old one ended first (`:133`) | the brief before the refused turns (`:127-129`) | the refused turns' words, then those that waited, after the brief (`:157-166`); the turns are held with no box written (§2.4 row 3) | new record `from` (`limit`, Daoris), `moved.on`, `session.moved`, `chat.limit-not-moved` (`:148`), `chat.limit-waits` (`:98`) |
| **E. Limit wait and wake, conversation** (LIMITRESUME1c) | the look's `limits` stage (`Loop/DriverWatch.cs:789` → `Modules/Loop/DriverLoop.cs:929` → `ChatRunner.cs:194-196` → `Chat/LimitWaits.cs:65-117`). Automatic | the record's | own account first, read from the cooling file alone (`LimitWaits.cs:151-183`); **a limited chat moves** (`:188-196`); **a chat a limit never ended waits for its own account only** (`:185-186`) | a wake: the same conversation (live `:206-258`, ended `ChatGoOn.WakeAsync`); a move: a briefed new part | the brief on a move | the refused turns' words, those that waited, then Daoris's limit word, keyed by the wake's clock (`:168`, `:212-213`) | `<s>.limit-move.json` (`Chat/LimitBox.cs:22-88`), the limit word on `said`, `limit.resumed`/`limit.resume-refused`, `session.reopened` |
| **F. Limit resume, driven** (LIMITRESUME1b) | the look: an open quest's `Planner.LimitedFrom` (`Planner.cs:1415-1418`, `:1464-1469`), a taken one's `CarriedOnFrom`; `LimitResume.Line` (`Loop/LimitResume.cs:28-34`). Automatic, or *Try again* after a transient end | step 0: a hand-over made from it wins (`:29`) | step 1: own account ready, protocol door, judge passes: `ResumeAsync(limited: true, walks: false)` (`Driver.cs:1993-2004`); step 2: `SelectHandedAsync`, **within the same agent** (`Driver.cs:2013-2024`); step 3: held, `held.limit-waits` at the walk's earliest (`Driver.cs:2041-2050`) | step 1 the same conversation; step 2 a new part, why `limit` only on another account, else `carry-on` (`Driver.cs:4627-4628`) | step 2 the brief | after a limited end the words always wait (`Driver/Sessions/Domain/Observation.cs:525-527`); the limit word keyed by the cooling line's `until` (`LimitResume.cs:49-54`; `Driver.cs:2309-2321`) | the limit word, `LimitResumed`, look news `limit-waits`/`limit-went-on`/`limit-resumed` |
| **G. Curve move** (D165) | a turn's end with nothing taking, opening or queued (`ChatRunner.cs:1624-1631`; `Chat/CurveMove.cs:36-37`). Automatic, **conversations only**; a driven line has `curve` only as its carry-on's reason (`Driver.cs:4629-4630`) | the same; Ask Daoris and a record with no account passed (`CurveMove.cs:70-77`) | `SwitchDecision.Decide` names it (`:128-134`), asked as a hand-over (`curve`, Daoris) (`:114-117`) | as H | as H | none wait, by construction | as H |
| **H. Hand-over, conversation** (D163) | the person (`SESSION_HAND_OVER`, `ConversationRoutes.cs:223-269`; `sessions hand-over`, `SessionsCommand.cs:332-341`), Daoris's offer as the person's press (`ConversationRoutes.cs:220-221`), the agent (`handover_propose`, `Mcp/HandOverTools.cs:17` → `Loop/DriverHandOvers.cs:246-252`), the curve | the named; Ask Daoris refused (`HandOver/HandOver.cs:190-193`) | the named account as a pick, else the walk (`Chat/ChatHandOver.cs:156-157`); a named account cooling: refused after the old part ended (`HandOver.cs:254-262`, `:292-295`) | always a new part, at the turn's end (`ChatHandOver.cs:67-68`, `:76-91`), a closing turn asked first where the person pressed or the agent asked (`:145-150`, `:175-184`) | the brief before the closing note (`HandOver.cs:250-252`) | **the brief only, none of the person's words** (`HandOver.cs:271`); words said meanwhile held by the shell while the box stands (§2.3) | `<s>.hand-over.json`, `chat.handed-over`/`chat.taken-over`, `from` |
| **I. Hand-over, a quest's session** | the page or terminal (`ConversationRoutes.cs:241-258` → `HandOver/HandOverQuest.cs:94-117`); the agent's ask (`DriverHandOvers.cs:142-205`, `:279-303`) | the handed agent (`Driver.cs:1916-1917`) | `SelectHandedAsync(handed.Account)`: the named account, then the walk within that agent (`Driver.cs:2021-2023`) | a new part; **the person's box stops a live session at once** (`DriverHandOvers.cs:102-131`), the agent's ask waits for the turn's end (`:227-235`) | the brief and the handed note (`Driver.cs:2550-2555`) | the person's | `data/handed/<quest>.json` (`DriverHandOvers.cs:133`, `:291-292`), `from` (why, asker) (`Driver.cs:4625-4626`) |
| **J. Briefed move** (D189) | the person (`SESSION_GO_ON_BRIEF`, `ConversationRoutes.cs:186-198`), Daoris on a lost conversation (`ChatGoOn.cs:888-904`), a closed quest's lost session (`ChatRunner.cs:238-240`); refused for an open or taken quest's session and with no person's words (`BriefedMove.cs:92-103`) | the record's | **its own account as a pick** (`BriefedMove.cs:132-135`), so refused exactly while that account cools | a new part, why `resume` | the brief (`:126-128`) | the person's, taken off naming the new part (`:141-163`) | `from`, notes |
| **K. Driven carry-on** (D80, D131 point 3) | the planner's carry-ons: cut off, stop released, retried, limited, brought forward (`Planner.cs:1400-1442`). Automatic | the record's, or the handed | the walk (`Driver.cs:2013-2024`); after a refused resume, the resume's account kept (`Driver.cs:2407-2434`; `HarnessRoster.cs:1056-1058`) | always a new part; the native door's one retry of a resume that exited before naming its conversation (`DriverContinuations.cs:1291-1316`) | the brief (`Driver.cs:2551-2556`) | the person's | `from` in order hand-over, limit, curve, retry, carry-on (`Driver.cs:4622-4633`); `CutOff`, `Retried`, `InFlight`, `LeftBy`, `AccountChanged` (`:2618-2678`) |
| **L. A chat's workflow press** (CHATFLOW1; D157's notes) | the bind at open and at each turn's end (`Chat/ChatRunBinds.cs:27-33`, `:40-65`); *Accept* (`Driver/Trees/Landing/ChatPresses.cs:90-151`). The person | — | — | the press reserves the turns (`Chat/ChatTurns.cs:568-586`), reads `HEAD` once, judges the gate at that tip, asks the opinion or lands that tip | — | words said meanwhile kept and waiting; a send-back reaches a live conversation only between turns (`ChatTurns.cs:652-718`, three reads, `:659-676`), an ended one through path B with **B's own-account rule** (`ChatRunner.cs:2347-2363`) | the opinion's due (`ChatPresses.cs:125-132`) |
| **M. SWITCHCTX1's carry** (proposed) | a move of one agent between two of Daoris's accounts | the same | the move's | the conversation's own files carried, loaded and read back before a prompt (`docs/2026-10-24-switch-context-design.md` §2.1-§2.4), falling back to the brief (§2.5) | the agent's own; else the brief | — | one carry record per move (§2.3). **No code**: no `Carries` or `moved.carried*` in `src/`; only the line view (SWITCHCTX1c-i) is built, in `Web/work/conversation/line.ts`, `EarlierParts.tsx`, `SessionConversation.tsx:120-157` |
| **N. AGENTERRORS1's retry** (D210) | a prompt's failure the reader calls transient (`Driver/Agents/Errors/AgentErrors.cs:133-205`; Claude's table `:281-316`) | the same | the same account, choice and slot | the same conversation: five tries after 1, 2, 4, 8 and 16 minutes (`Driver/Agents/Acp/TransientRetry.cs:24-28`), **handed to driven quest sessions on the protocol door only** (`Driver.cs:3789-3794`; `TransientRetry.cs:12-13`) | — | held words only; the go-on keyed `transient-retry/<s>/<episode>` (`Loop/TransientRetries.cs:34-44`), taken off at the conclusion (`:134-147`); the owner's check `WantedAsync` (`:98-114`) | past the bound `ended.transient` and a hold (`DriverContinuations.cs:739-742`; `Planner.cs:1525-1532`), released into path F's step 1 (`LimitResume.cs:40-42`) |

`Loop/OneTurnReask.cs:11-93` is not an error retry: it answers a model refused at the open for the one-turn kinds (opinion,
helper, partner).

### 2.3 The words' own path

The contract is `.claude/knowledge/session-words.md`: every word that may make a turn happen is kept on `said` through the
session's one dispatcher first.

- **The dispatchers.** Driven: `DrivenInbox.KeepAsync`/`KeepOrHoldAsync` (`Driver/Conversations/DrivenInbox.cs:423-480`,
  `:488-513`); its `Close` keeps the kept words on the record unless the person stopped it (`:840-890`). A conversation:
  `ChatTurns.KeepAsync` (`Chat/ChatTurns.cs:173-226`). **Under `Hold()` a word is neither kept nor taken** (`:182`, `:458-466`);
  under a limit's wait and a press it is kept and queued (`:492-500`, `:568-586`).
- **The door.** `Driver/Conversations/SaidDoor.cs:29-44` keeps with `running: true`; `:69-92` marks the attempt, then *went*.
- **The shell's judge** (`Modules/Loop/SessionWords.cs:380-468`): the inbox (`:395-409`), a live chat (`:420-433`), then the
  record. **While a hand-over's box stands, words are held for the take** (`:455-467`, `:563-582`) and passed on by `ContinuedIn`
  (`:838-855`) once `TakeEnded` fires (`DriverLoop.cs:1212`); a winding-up record's `running` refusal is held and pumped
  (`:714-720`, `:752-812`); held words persist in `held-words.json` (`:901-973`).
- **The headless twin** `LoopWords` (`Driver/Conversations/GoOn/SessionSay.cs:148-212`) answers `running` and `unreached` instead of
  holding (`:206-211`).
- **The nevers** are `WordsNever` (`SessionSay.cs:40-46`), written again in `Continuation.Judge` (`Continuation.cs:213-215`),
  `HandOver.Never` (`HandOver.cs:182-199`) and `GoOnNew` (`:98-115`).
- **The come-up** settles uncertain words (`GoOn/UncertainWords.cs:25-94`, at `DriverWatch.cs:739`, and per record at
  `ChatGoOn.cs:152-156`, `:241-245`); a failed turn's words by `WordsLeft.KeepAsync` (`GoOn/WordsLeft.cs:25-75`).
- **The queue's state** `ChatQueue` (`Driver/Conversations/Domain/ChatQueue.cs:45-46`) is *taking*, *queued*, *opening* or
  *pressing*, with no flag for a limit's wait or a take's hold; `WordsHeld` is the terminal's answer (`Domain/WordsHeld.cs:17-43`).

### 2.4 Duplications and disagreements

| # | The question | Where it is decided, and how the places differ |
|---|---|---|
| 1 | **The record's own account cools: wait or move?** (the quest's example) | D137 point 7 (`D137.md:29-30`) holds the words for the own account's reset. The code: `ResumeChoice.Waits => Own == NextHold.Cooling && !NewSession && !Limited` (`ResumeAccount.cs:78`) and its twin in the roster, `if (own == NextHold.Cooling && !newSession && !limited)` (`HarnessRoster.cs:1008`). D206 taught **the limited case** to move (`ResumeChoice.Limited`, `ResumeAccount.cs:72-75`; `D206.md:25-31`; the limit move `LimitMove.cs:83-104`; the wake `LimitWaits.cs:188-191`) and kept point 7 for words no limit ended (`LimitWaits.cs:185-186`, `if (limited is null) return (null, own.Until);`). LIMITSWITCH1 (`D137.md:791-799`) reverses that on paper; the places that still wait are the driven go-on (`Driver.cs:2031-2039`), the chat's (`ChatGoOn.cs:573-579`), Ask Daoris's (`:674-680`) and the wake (`LimitWaits.cs:185-186`). One question, one flag, four sites |
| 2 | **Which account does a resumed or continued part take?** | Own as a pick: the driven go-on (`HarnessRoster.cs:1023`), the driven limit's step 1 (`Driver.cs:1993-1997`), the chat's go-on (`ChatGoOn.cs:570-572`), the briefed move (`BriefedMove.cs:133`). The same process: a live limit wake (`LimitWaits.cs:225`). The walk: the limit move (`LimitMove.cs:141-145`), the driven step 2 and carry-on (`Driver.cs:2021-2023`). The named then the walk: a handed quest (`HarnessRoster.cs:923-934`). A named pick or the walk: a chat's hand-over (`ChatHandOver.cs:156-157`). The decision's: the curve (`CurveMove.cs:114-117`). The resume's own where it had one: the fallback after a refused resume (`HarnessRoster.cs:1056-1058`). Seven answers |
| 3 | **Are words held during a move?** | Hand-over and curve: the shell holds them while the box stands and passes them on (`SessionWords.cs:455-467`). The live limit move holds the turns too (`LimitMove.cs:119`) but writes **no box**, so a word relies on the `running` refusal and its pump, then `ContinuedIn` (`SessionWords.cs:714-720`, `:818-830`); only the ended path re-reads for a word kept in the gap (LIMITWORD1's `Carried`, `LimitMove.cs:266-270`), *unverified* that the live path can lose one. A limit's wait and a press keep and queue (`ChatTurns.cs:492-500`, `:568-586`). A driven inbox's `Closed` hands the word back to its caller (`DrivenInbox.cs:448-454`) |
| 4 | **Resume the agent's own conversation, or brief a new part?** | Resume: the driven go-on and the limit's step 1 (protocol door only, `DriverContinuations.cs:58`), the chat's go-on and wake. Always briefed: the limit move (`LimitMove.cs:12-13`), the curve and the hand-over (`HandOver.cs:250-271`), the briefed move, the driven carry-on, and a pipe-door limited line, whose briefed carry-on can land on the same account (`Driver.cs:4629-4633`), which D206 rejected (`D206.md:49`) until its row 1f. SWITCHCTX1 would carry, unbuilt |
| 5 | **Does a limit move a conversation on the person's pick?** | D206 point 5 (`D206.md:36`): a pick waits for its own account. The limit move refuses a pick only **to another agent** (`LimitMove.cs:447`); its same-agent move (`:85-104`) and the wake's move (`LimitWaits.cs:188-191`) read no pick |
| 6 | **How many tries?** | transient five, driven protocol door only (`TransientRetry.cs:24-28`); a native resume that exited, once, in two places (`DriverContinuations.cs:1291-1316`; `ChatRunner.cs:2844`); a failed turn's words once, then held (`Observation.cs:525-530`); a send-back's reads three (`ChatTurns.cs:674`); strikes three (`Planner.cs:1985`) |
| 7 | **The limit's word** | Its key: the cooling line's `until` when driven (`LimitResume.cs:49-54`), the wake's clock in a chat (`LimitWaits.cs:168`, `:213`). Its English twice, the same sentence: `LimitResetWord.Text` (`LimitMove.cs:565`) and `LimitResume.Words` (`LimitResume.cs:19`) |
| 8 | **Which failure is a limit?** | Driven: the sentence **or the reader's code** (`byCode`, `Driver.cs:3237`; `DriverContinuations.cs:762`). A chat: the sentence alone (`ChatRunner.Limited`, `ChatRunner.cs:2670-2675`, no code parameter), on the protocol door alone (`:1644`). A brainstorm: the sentence (`Loop/DriverBrainstorms.cs:778`). A *limited record*: the ledger's flag when driven (`Planner.cs:1464-1469`; `LimitResume.cs:28-34`), a scan of the events in a chat (`Domain/LimitHeld.cs:17-56`) |
| 9 | **Read the maker's usage before an own-account resume after a limit?** | driven yes (`HarnessRoster.cs:1001-1006`); a chat's wake no, the cooling file alone (`LimitWaits.cs:155`) |
| 10 | **Move to another agent at a limit?** | a chat yes (LIMITGOON2, `LimitMove.cs:444-469`); a driven line no, its step 2 walks the same agent (`Driver.cs:2021-2023`; D206's row 1e open) |
| 11 | **The earliest reset** | driven over the walk's accounts (`HarnessRoster.cs:1732`); a chat over every cooling entry of the owner (`:251-256`; `LimitWaits.cs:199`) |
| 12 | **A running session's hand-over: wait or stop?** | a chat waits for its turn's end (`ChatHandOver.cs:67-68`); a quest's session stops at once for the person's box (`DriverHandOvers.cs:102-131`) and waits for an agent's ask (`:227-235`) |
| 13 | **The door out of an account's wait** | a driven line: *Go on in a new session* (`DriverContinuations.cs:122-123`). A chat: *Start a conversation with these words*, fresh and unbriefed (`ChatGoOn.cs:575`), since go-on-new refuses a conversation (`GoOnNew.cs:127-133`) and *Go on with Daoris's brief* picks the same cooling account (`BriefedMove.cs:133`) |
| 14 | **Only the person's words go to a new part** | one rule written five times: `BriefedMove.cs:99-103`, `DriverContinuations.cs:1093-1104`, `Driver.cs:2390-2394`, `LimitMove.cs:249` and `:322`, `LimitHeld.cs:27` |
| 15 | **The say door's client** | the shell holds a winding-up session's words; the headless loop refuses them `running` (`SessionSay.cs:206-211`). The shell's record path keeps a word **with no key** (`SessionWords.cs:701`, `:782`), against the contract's producer key (`session-words.md`), while its inbox and chat paths pass one (`:400`, `:427`) |

Two more seen while reading. The limit move's first `SelectAsync` (`LimitMove.cs:85-86`) counts as a chosen start
(`HarnessRoster.cs:1149`, `:1530`) and is never released, and the start then walks again (`:141-145`), so the account judged and
the account opened can differ (*unverified* in a test). And `SessionWords`' `held-words.json` drops a word's `Choice` and `Key`
(`SessionWords.cs:901-942`), so a held park's choice (PARKVIEW1) does not survive a restart.

### 2.5 The records this area writes

| Record | Where | Writer | Process |
|---|---|---|---|
| `said`: id, text, at, files, reopens, by, helper, channel, from, key, level, attempt, decision, question, choice | the service's `sessions` table (`Core/Sessions/Sessions.cs:392-402`, `:564`, `:1717-1738`) | `SessionLedger.SayAsync` `:891`, `MarkAttemptAsync` `:1221`, `TakeBackAsync` `:1267`; called from `SaidDoor.cs:31-44`, `SessionWords.cs:701`, `:782`, `WordsLeft.cs:40`, `DriverContinuations.cs:81`, `TransientRetries.cs:37`, `LimitWaits.cs:212`, `ChatGoOn.cs:436`, `LimitMove.cs:377` | service |
| The ledger's `limited`, `interrupted`, `from_session`, `from_why`, `from_by`, `entry`, `answer`, `note_parts`, the actor's fields | `Sessions.cs:572`, `:568`, `:596-598`, `:602`, `:559`, `:576`, `:621-622` | `AdvanceAsync`, the open's `from` (`Driver.cs:2465-2468`; `ChatRunner.cs:606-607`) | service |
| `<s>.harness.json` (the conversation's id) | `data/sessions/` | `GoOn/HarnessConversations.cs:31-44` | driver library |
| `<s>.cannot.json` (go-on marks) | `data/sessions/` | `GoOn/GoOnMarks.cs:19-24` | driver library |
| `<s>.new-session.json` | `data/sessions/` | `GoOnNew.cs:29-33`, `:160` | modules route or terminal; read by the look |
| `<s>.reopened.json` | `data/sessions/` | `GoOn/ReopenMarks.cs:17-22` | driver library |
| `<s>.hand-over.json` (the box) | `data/sessions/` | `HandOver.cs:22-26`, `:121`; `HandOverQuest.cs:109` | the chat runner (shell) or the look |
| `<s>.hand-over-ask.json`, `<s>.hand-over-state.json` | `data/sessions/` | the ask `Core/Sessions/HandOverAskBox.cs:28`, `:81` (through `Mcp/HandOverTools.cs:17`); the state `HandOver/HandOverAskFiles.cs:71` | service; driver |
| `<s>.limit-move.json` | `data/sessions/` | `LimitBox.cs:31-46`; `LimitMove.cs:251`, `:270` | the chat runner (shell only) |
| `held-words.json` | `data/sessions/` (`Driver/Sessions/History/SessionHomeFiles.cs:322-326`) | `SessionWords.Save` (`:901-942`) | shell modules |
| `<quest>.json` (a handed quest) | `data/handed/` | `HandOverQuest.cs:23-27`; `DriverHandOvers.cs:133`, `:291` | the look |
| `cooling.json` | `data/` | `HarnessRoster.Limited` (`:272-304`; `AccountCooling.Keep`, an in-memory fallback, `:285`) | driver library |
| Event notes: `moved.on`/`moved.from`, `chat.limit-waits`, `held.limit-waits`, `limit.*`, `lost`, receipts, transient | the session's event file | `Driver.cs:4589-4602`, `LimitMove.cs:98`, `:170`, `UncertainWords.cs:87-90`, `TransientRetries.cs:58-61` | driver, shell |
| Machine-log lines: `session.moved`, `session.reopened`, `account.limited`, `agent.error`, `agent.transient`, `conversation.unkept` | the machine log | `ServiceClient.AccountSaid` (`Driver/Service/ServiceClient.cs:105`), `TransientRetries.cs:28-30`, `:53-56`, `HarnessConversations.cs:41-42` | driver |
| A transient episode | memory, one run (`TransientRetry.cs:77-120`), and its go-on on `said` | — | driver |
| Ask Daoris's link `handed: brief` | `<s>.help.json` (`Driver/Help/HelpConversations.cs:115`) | `LimitMove.cs:424`; `BriefedMove.cs:276` | shell |
| A carry record (SWITCHCTX1) | none; its design's §2.3 | — | — |

So a move in flight is durable for one path alone, the ended limit move's box; the hand-over's box is the take's ask, and the
curve, the live limit move and a driven carry-on keep none (`docs/2026-10-24-switch-context-design.md` §1.2 says the same of
the code it read).

### 2.6 Defects of the last ten days that came from these disagreements

From `docs/FIX-LOG.md`, 2026-10-15 to 2026-10-25, each entry's root cause read: those where two paths decided one question
differently, or one path had not learned what another already did. 26 of the 273 entries.

| Date | Entry | The two that disagreed |
|---|---|---|
| 2026-10-24 | A word kept while a limit's move was under way reached neither part (LIMITWORD1) | the ended limit move's box, read once, and the say door's `WordsNever.ContinuedIn`, on which record a word in the gap belongs to (row 3) |
| 2026-10-24 | A conversation continuing a quest's worker read its line's origin, not its quest's run (CHATFLOW1a, Codex's finding 6) | the workflow view classing a record by its origin, and the run's records chosen by `session.Quest`, null on a continuing conversation |
| 2026-10-24 | A try handed to a queued word reset the transient budget (AGENTERRORS1, Codex's C4-1) | the episode taking a try's answer as recovery, which the settling turn's rule already refused |
| 2026-10-24 | A try wrote a kept word its record had not marked attempted (AGENTERRORS1, Codex's C4-4) | the retry's try and the dispatcher's attempt mark (`SaidDoor.Dispatched`), on a word once on the wire |
| 2026-10-24 | The transient owner's check read a quest's history as the run's end (AGENTERRORS1, Codex's C4-5) | `TransientRetries.WantedAsync` reading `Awaits` as a wait, and `Observation`'s reading of the question named at the run's start |
| 2026-10-24 | A limit or access refusal at a resume's open skipped its account (AGENTERRORS1, Codex's C4-6) | the retry's open path returning before the limit and access readers that every other end runs (row 8) |
| 2026-10-24 | A new Ask Daoris chat's first words went to the one New chat finished, and its open hung (HELPNEWCHAT1) | `START_HELP`'s carry-on of a running conversation and *New chat*'s finish, on whether a finishing process still runs |
| 2026-10-24 | A line a limit cut off came back briefed on the account its conversation was on (LIMITRESUME1b) | the carry-on walking like a first start and the agent's own resume taken only where the person's words waited (rows 2, 4) |
| 2026-10-24 | A conversation at its limit was refused turn by turn, and its waiting words never went on at the reset (LIMITRESUME1c) | the look's limit handling for driven lines and no stage for a conversation's reset (rows 9, 11) |
| 2026-10-23 | A conversation whose account met its limit stayed on it, and Ask Daoris reopened with nothing carried (LIMITGOON1) | the curve move (windows only, Ask Daoris passed) and the briefed move (a proven loss only), neither taking a limit-refused turn |
| 2026-10-23 | A hand read back after its answer was lost told nothing on this machine (HELPERWAIT1b2) | the read-back marking a word handed without the dispatch the hand path runs |
| 2026-10-21 | The person's words to a quest's sessions around an ask-and-wait never reached a run (SAYHELD1) | `Planner.WordsOn` and the start reading `awaits` as a wait, and D79's resume opening a new session without the words |
| 2026-10-21 | A session that committed its work and failed before its take was built again from nothing (RETRYWORK1) | D80's carry-on covering a cut-off take only, and a failed open quest getting a first start |
| 2026-10-20 | A lost done's follow-up stood everywhere, and a step whose session ran across the go-on never got its source's work (CLOSEREOPEN1) | the session's end judging a reopen by the step held as it ended, and the later go-on |
| 2026-10-20 | The real ledger refused the carry-on of a step the person's reopen stopped (FABRIC-CLOSE1c26) | the planner's `CarriesOn` and the ledger's own list of records that may be carried on |
| 2026-10-19 | A resumed protocol conversation skipped a required posture (HELPMODE1a4) | the resume path and the start path, on a required posture |
| 2026-10-19 | A finished driven session waited 48 minutes on its own go-on and was recorded failed (BGWAIT2) | Daoris's go-on prompt and the adapter's own woken turn |
| 2026-10-18 | A step's next submission after the person's go-on was never sealed (FABRIC-CLOSE1c14) | the go-on's write and `PublishNextAsync`'s rebind |
| 2026-10-18 | A step gone on with after its session ended normally sat taken with nothing submitted (FABRIC-CLOSE1c13) | the planner's go-on and the start's rebase reading only a session the reopen stopped |
| 2026-10-18 | The person's go-on kept a held step's result made on the reopened work (FABRIC-CLOSE1c10) | the go-on's rebound step and the reopen's and submission's clearing |
| 2026-10-16 | A word taken back from a driven session could still go to its resumed run (SAIDBACK1e) | the continuation building its prompt before the ledger's move, and the take-back door refusing only a running record |
| 2026-10-15 | A message to a session vanished behind the error when its send timed out (SENDLOST1b) | the composer's and every other box's press on a bridge timeout, where Ask Daoris already kept the words |
| 2026-10-15 | Ask Daoris lost the person's first words on a loaded machine (HELPSTART1, SENDLOST1) | `START_HELP` answering after the room's read, and the page giving the words back to a box the new conversation no longer drew |
| 2026-10-15 | Words that could not go on left a fresh tree and branch at every look (GOONTREE1) | the go-on's early returns and every other early return after the open in `Driver.RunAsync` |
| 2026-10-15 | Words to a conversation handed over or moved stayed on its record and could restart it (HANDWORDS1-i) | the say doors, the go-on and the come-up judging the old record alone, and the take's `from` link |
| 2026-10-15 | The person's words to a session whose closed quest had once waited on a question never went on | the planner's `WrittenTo` and the start's continuation guard, each reading a closed quest's stale `awaits` |

And the case that opened this design, LIMITSWITCH1 (`TASKS.md`; `D137.md:791-799`), is row 1: not a FIX-LOG entry yet, since it is
not built.

## 3. What is said

Every channel by which a change is told: to the page, the person, an agent, a plugin, the terminal or another process. *The bus*
is the shell's `IEventBus`, whose events reach this machine's windows and nothing else (`Modules/Platform/StateSlices.cs:73-74`,
`Modules/Platform/StatePublisher.cs:149`). `App/` below is `src/Daoris.Desktop/Daoris.Desktop.App/`.

### 3.1 The channels

| Channel | What it carries | Who writes | Who reads | Kept | Order |
|---|---|---|---|---|---|
| **Pushed slices** (D182) | `DAORIS.STATE_PUSH` {slice, epoch, partition, seq, status, data, baseSeq?, removed?} (`StateSlices.cs:35-38`, `:82`); `STATE_MANIFEST` every 15 s (`:138`, `:304-309`); fourteen slices (§3.2) | one `StateSlice` per slice, judging one wake at a time, a wake only asking (`StateSlices.cs:76-394`, `:101-102`); registry `DriverLoop.Slices` (`Modules/Loop/DriverLoop.cs:124-129`) | every window's query cache: `PUSHED` (`Web/app/ShellSignals.tsx:20-23`), `useStateSlices` (`Web/bridge/state/slices.ts:342-355`); the snapshot by `STATE_SLICE` (`Modules/Bridge/Routes/DriverRoutes.cs:40-42`) | memory; the epoch a fresh id per owner's life (`StateSlices.cs:99`, `:128`) | per partition `seq` and epoch, a delta never coalesced (`:245-247`), a resync on a missing base or a manifest ahead (`slices.ts:179-189`, `:248-268`) |
| **Invalidations: `STATE_CHANGED`** (D169 point 3) | {scopes[], sessions?}, *what moved, never what it is now* (`StatePublisher.cs:99-106`), 21 scopes (`:14-88`) | the middleware after a declared route answers (`:185-221`); a terminal's `.told` note (`:253-256`; `DriverLoop.cs:801`); the driver's `Publish` (`:263-264`) at a service move and elsewhere (`DriverLoop.cs:773`, `:813-817`, `:1154-1161`) | the page's hub (`Web/bridge/state/stateHub.ts:27-65`, `:222`); slices' wakes; `DriverModule.cs:109-114` (forgets held work); plugins (`StatePublisher.cs:229-234`) | none | none; not awaited (`:146-147`) |
| **Invalidations: `DRIVER_TICK`** | {events, considered, untrusted, waits} (`DriverLoop.cs:1045-1063`): data and an invalidation at once | the shell's look, only when something planned or a signature moved (`:958-1043`) | `ShellSignals.tsx:67-122` (writes three keys, invalidates nine families with `notPushed`); the groups slice's wake (`GroupsStateSlice.cs:103`) | none | none |
| **`UNDER_WAY`** | {seq, presses[{route, session?, subject?, step?, behind?}]} (`StatePublisher.cs:117-130`, `:300-352`) | the publisher, under a lock | the page keeps the newest (`stateHub.ts:133-140`); re-read on mount (`DriverRoutes.cs:31-32`) | memory | whole-list `seq` |
| **Held answers** (NOBLOCK1) | the last answer of each `[ByEvent]` read, by route and payload (`Modules/Bridge/HeldAnswers.cs:28-203`; `DriverModule.cs:277`): `HEAVY_SLOT`, `HELP_PROPOSALS`, `SUPERVISOR_WAITING`, `SESSION_OPENINGS`, `SESSION_WHERE`, `TOOLS_LIST` | the read's work; `ANSWERED` {held, answer or failed} only when the answer moved (`HeldAnswers.cs:150-161`, `:176`) | every window whose cache holds the key's hash (`Web/bridge/state/held.ts:24-52`); the terminal's `sessions supervise` polls past *pending* (`Driver/Sessions/Questions/SessionsSuperviseCommand.cs:143-162`) | memory, 128 by least recent ask (`HeldAnswers.cs:40`, `:181-188`) | an older work landing after a newer moves nothing (`:149`) |
| **`DAORIS.*` bus events** | `SESSION_ATTENTION` (five kinds from the look, `Holding`, `Confirming`, `ConfirmingRestored`), `NOTICE`, `MESSAGE`, `SESSION_ENDED`, `SESSION_QUEUED`, `SESSION_EVENTS`, `SESSION_OUTPUT`, `SESSION_STREAMS`, `SESSION_OPTIONS_CHANGED`, `CHAT_REPLACED`, `CONFIRMATIONS`, `SERVICE_CONFIRMATIONS`, `DRIVER_ERROR`, `DRIVER_READY`, `STANDING_USED`, `HARNESS_ENDED`, `TOOLS_ENDED`, `UPDATE_STATE`, `TERMINAL_*`, `ATTEND_SESSION` (§3.3) | `DriverLoop` (`:350-356`, `:698-1237`), `HeldCallAnnouncements.cs:54`, `PipeConfirmations.cs:598-717`, `NoticesStateSlice.cs:306`, `:347`, `AskMessages.cs:35`, `ServiceConfirmations.cs:66-78`, `App/MainForm.cs:156-157` and others | the page (`ShellSignals.tsx:138-227`, `Web/bridge/sessions/*.ts`, `Web/bridge/person.ts:81`, `:124-131`); the OS balloon (`App/SessionNotifier.cs:99-103`); the taskbar (`App/MainForm.cs:170-180`) | none | per kind: `SESSION_EVENTS` by the file's `seq`, the confirmations by a whole-list `seq`, the rest none |
| **The machine log** (D94) | {time, source, level, event, data} (`Driver/Platform/MachineLog.cs:390-413`): `session.*`, `turn.*`, `account.limited`, `landing.auto`, `look.said`, `change.*`, `chain.*`, `changes.*`, `state.slice`, `person.confirmation`, `plugin.*`, `pipe.*`, `role.*`, `supervisor.*`, `session.quiet`, the page's six events | two twin writers: `Driver/Platform/MachineLog.cs:34` (desktop, driver, browser, `:65`) and `Core/Platform/MachineLog.cs:29` (host, connector) | `MachineLogReader` (`Driver/Platform/MachineLogReader.cs:37-68`): `daoris-driver logs` (`Host/LogsConsole.cs:22-37`), Settings → Logs (`Modules/Platform/LogModule.cs:146-205`), plugin health and activity (`Driver/Plugins/PluginHealth.cs:45`, `PluginActivity.cs:34`), `tools/usage-report.mjs` (a twin parser) | `logs/<date>.<source>.jsonl`, 20 MB a file, 30 days (`MachineLog.cs:12-40`); a failed write dropped (`:139-142`) | appended at the end under a named mutex |
| **The session's event file** | user, message, thought, tool, plan, usage, turn, note, transient, raw, receipt (`Driver/Sessions/Domain/SessionEvents.cs:13-98`) | `SessionEvents` (`Driver/Sessions/Events/SessionEventLog.cs:25`), written before anyone is told (`:39-76`) | the page by `SESSION_EVENTS` and `SESSION_HISTORY` (`Web/bridge/sessions/conversation.ts:47`, `:83`); `SessionLog` (`Driver/Sessions/Events/SessionLog.cs:97`); `RunningCalls` and the watch (`DriverLoop.cs:100`); the brief; the answers-alone hold (`Driver/Conversations/GoOn/AnswersAloneHold.cs:97-123`); a park card's words (`SessionLastWords.cs:18-22`) | `data/sessions/<id>.events.jsonl` (`SessionEventLog.cs:599-607`) | `seq` last + 1 from a cache per process (`:45-63`) |
| **The lineage file** (D173's C1a) | ids only, `by` call, hook, stream or end | four writers: the connector (`Mcp/CallLineageFilter.cs:17-22` → `Core/Sessions/LineageFile.cs:59-93`), Claude Code's hook (`Driver/Sessions/hooks/lineage-hook.mjs:25`, `:31-46`), the driver's stream (`Driver/Sessions/ChildLineage.cs:145-152`; `ChildBindings.cs:330`), the part's end (`ChildLineage.cs:118-122`; `Driver.cs:3899`, `ChatRunner.cs:2916`) | **no production reader**: `ChildLineage.Read` (`:125-139`) is called by tests alone | `data/sessions/<id>/lineage.jsonl` | appends retried |
| **Hook points** (D119, D148) | `quest/consider`, `session/ended`, `work/land`, `work/state`, `state/changed`, `work/watch`, `work/step`, `work/perform` (`Driver/Plugins/Domain/HookPoints.cs:24-88`); `session/ended`'s frame {session, quest, repository, state, adapter, account, byPerson, note} (`:115-125`) | the loop: `quest/consider` a waterfall failing closed (`Driver/Plugins/Hooks.cs:1028-1059`); `session/ended` for runs the look drained (`Driver.cs:1624-1651`); `state/changed` a queue of 64 dropping the oldest (`Hooks.cs:654-666`, `:1096-1152`); the work wire the lease's loop alone | plugins the loop started; none while another loop holds the lease (`DriverLoop.cs:832-835`); none on a headless loop's `state/changed` (`HookPoints.cs:61-64`) | memory | `state/changed` first in, first out; the rest per call |
| **Fabric events** (D173) | the watches' wakes and the end judgement's facts (`Driver/Platform/Chain.cs:5-61`): `ChangeWatch` topics (`Driver/Platform/ChangeWatch.cs:48-113`), `HomeWatch` (`Driver/Platform/HomeWatch.cs:196-254`), `RefWatch` links 1 and 2 (`Driver/Trees/RefWatch.cs:104-151`); the service's change stream, five families each with its revision (`Core/Platform/ChangeStream.cs:12-61`, `:95`) | the watches; the service | the look's topics and the slices, through one wait (`Driver/Service/LedgerChanges.cs:44-99` → `DriverWatch.cs:491` → `DriverLoop.HeardMove`, `:150-154`); the `words` family only by `HelpFacts.cs:157` | memory; the stream's cursor `epoch.n` (`ChangeStream.cs:80-113`) | the chain's ranks (`Chain.cs:93-122`); **ranks 3 (`ended`) and 4 (`publish`) are declared in the fixed table (`:170-172`) and bound nowhere** |
| **Notifications** (D196) | the notices record: ended, expired, withdrawn, closed, lost, newer, answered, look (`Driver/Platform/Notices.cs:37-72`); *What needs you*'s rows (`Modules/Loop/NeedsAPerson.cs:135-616`, about two dozen kinds) | `NoticesStateSlice` (`:172-248`), `LookSaid.News` (`Driver/Platform/LookSaid.cs:224-236`), the headless host (`Host/Program.cs:1006`); only *ended* and *answered* come from the watch (`Notices.cs:108-113`) | the bell, *Done and failed*, *Messages* (`Web/bridge/notices.ts:202-227`); the balloon (`App/SessionNotifier.cs`); toasts (`ShellSignals.tsx`); the taskbar's count from the page (`Web/bridge/windows.ts:113-124`) | `data/notices.json`, a week, at most 200 (`Notices.cs:32-35`) | none |
| **The ask's channel** (D167, D188) | messages {id, ask, at, session?, to?, re?, question, text, urgent?} (`Core/Asks/Channel.cs:87-104`), lines published, taken, done, declined, awaiting (`:110-116`), acks and the person's receipts (`:152-205`) | a session through the connector (ordinary by default, ten a part, three urgent, `:9-25`, `:727-744`); the person (urgent by default, no cap, `:751-768`) | a session by `channel_read`, which acks `read` (`:840-868`); a running part by `Driver/Loop/ChannelReach.cs:44-60`; a waiting line on every tool answer but `channel_read` (`Mcp/WaitingFilter.cs:13-75`) | the service's database, mirrored to teammates | per ask |
| **The quest log** | operations published to chosen (`Core/Quests/QuestLog.cs:4-109`) | `quest_log` (`Core/Quests/Quests.cs:837-848`, appended `:1364`) | replay, the change stream (`ChangeStream.cs:56`), the sync | the database, synced | the log's |
| **Presses told** | {route, session?, at}, ten routes (`Driver/Sessions/Requests/PressesTold.cs:38-72`) | a terminal verb's `run/requests/*.told` (`:90-103`) | the shell's loop, each once (`:112-139`), as `STATE_CHANGED` (`DriverLoop.cs:801`) | a file for one minute (`:78`) | — |
| **Others** | `ReopenTelling` (Daoris's words under a key, `Driver/Loop/ReopenTelling.cs:109`, `:246-291`); `HeavyTold` (a driven start's instruction and deny rules, `Driver/Sessions/Heavy/HeavyTold.cs:43-114`); `AskMessages` (`Modules/Loop/AskMessages.cs:38-41`); the knowledge server's one notification, `tools/list_changed` on a newer build (`tools/knowledge-server.mjs:384`); the headless terminal's report lines (`Host/Program.cs:998-1007`) | | | | |
| **EVENTHOOK1's relay** (proposed) | a point `work/event`, kinds `session.parked`, `quest.parked`, `session.ended`, `work.landed` (`docs/2026-10-24-event-hook-design.md:436-475`), a frame of ids and codes (`:503-533`), 256 a plugin, dropping the oldest with a `missed` count (`:571-572`), relayed by `daoris-driver events relay --once` (§6.1) | — | an outside agent's session | — | run and `seq` |

No code of EVENTHOOK1 exists: `work/event`, `event-relay` and `events relay` occur only in `TASKS.md` and the design.

### 3.2 The fourteen pushed slices

| Slice | Partitions | Keyed | Its bus wakes | What else wakes it |
|---|---|---|---|---|
| driver (`Modules/Loop/DriverStateSlice.cs:28`) | shell, choices, running (`:31-45`) | no | `STATE_CHANGED` driver, sessions, harnesses; `SESSION_ENDED`; `DRIVER_READY`; `STANDING_USED` (`:58-66`) | every look (`DriverLoop.cs:1005`) |
| lines (`LinesStateSlice.cs:37`) | per repository (`:40`), `notReadYet` (`:91`) | no | driver, lines, harnesses; `DRIVER_READY` (`:50-55`) | the machine's facts (`:62`, `:99-115`) |
| sessions (`SessionsStateSlice.cs:33`) | per workspace by session (`:39-47`); `said` and `transcript` pull-only (`:55-56`) | yes | sessions; `SESSION_ENDED`; `DRIVER_READY` (`:62-67`) | the ledger's family (`:109-122`) |
| calls (`CallsStateSlice.cs:32`) | `calling`, `quiet` (`:37-82`) | yes | none | `RunningCalls.Changed`, `SessionWatch.Changed` (`:51`) |
| work (`WorkStateSlice.cs:45`) | per repository and `<held>` (`:51`, `:57-73`) | no | landing, sweep, sessions (`:99-103`) | the facts (`:133`, `:218-226`) |
| groups (`GroupsStateSlice.cs:44`) | rows, entries (`:47-50`) | yes | sessions; `SESSION_ENDED`; `DRIVER_READY`; `DRIVER_TICK`; `SESSION_QUEUED` (`:98-107`) | the ledger; the trees' facts (`:115`, `:198-210`) |
| sweep (`SweepStateSlice.cs:41`) | per repository (`:47-57`) | no | none | the facts (`:87`, `:184-192`) |
| sync (`SyncStateSlice.cs:39`) | per wired circle (`:46`) | no | sync; `DRIVER_READY` (`:55-59`) | the loop's pass; the ledger's quests (`:106-130`) |
| quests (`QuestsStateSlice.cs:29`) | per workspace by quest (`:35-43`) | yes | quests; `DRIVER_READY` (`:61-65`) | the ledger's quests and sessions (`:58`, `:131-144`) |
| asks (`AsksStateSlice.cs:22`) | per workspace by ask; `words` pull-only (`:35`) | yes | asks, quests; `DRIVER_READY` (`:41-45`) | the ledger's asks, quests, sessions (`:38`, `:53`) |
| usage (`UsageStateSlice.cs:32`) | accounts, sessions (`:35-44`) | yes | `SESSION_ENDED`; `DRIVER_READY` (`:47`) | its period |
| accounts (`AccountsStateSlice.cs:36`) | per agent (`:42-48`) | no | harnesses; `HARNESS_ENDED`; `SESSION_ENDED`; `DRIVER_READY` (`:54-60`) | the sessions slice's push, filtered (`:133-140`) |
| attention (`AttentionStateSlice.cs:45`) | rows, sessions, working (`:48-60`), a deep allow-list (`:70-124`) | yes | rules, sessions, driver, harnesses; `HARNESS_ENDED`; `DRIVER_READY`; `CONFIRMATIONS` (`:165-174`) | its input slices' pushes and six other sources (`:183-185`); every look (`DriverLoop.cs:1008`) |
| notices (`NoticesStateSlice.cs:42`) | news, messages (`:45-64`) | yes | none | the ledger's channel and asks (`:122-135`), `HomeWatch.ForNotices` (`DriverLoop.cs:822`), its own writes (`:263-273`), the look's news (`DriverLoop.cs:900`) |

Two judges tell beside their push: the attention slice's calls `HeldCallAnnouncements.Judged` (`AttentionStateSlice.cs:205`),
and the notices slice's emits `NOTICE` and `MESSAGE` (`NoticesStateSlice.cs:306`, `:347`). A shell waiting on the home's lease, a
headless loop driving, builds no watch (`DriverLoop.cs:841-842`, `:895`), so it hears no change stream and sends no
`DRIVER_TICK`; its ledger-fed slices reconcile only at the manifest's period (*unverified*).

### 3.3 One change, every channel

**A driven session ends** (the driver concludes it).

| Channel | Where | What it says |
|---|---|---|
| the ledger, then the sessions, groups and attention slices | `SessionsStateSlice.cs:109-122` | every end, at once |
| `STATE_CHANGED` {sessions, [id]} | `DriverLoop.cs:1154` | only where this process's client moved it |
| the log's `session.ended` | `SessionLog.cs:273-283` | at the move, any closed state |
| `SESSION_ATTENTION` *Ended* and the notice `ended-<id>` | `Driver/Quests/Domain/Attention.cs:246-258`; `DriverLoop.cs:947-951`; `Notices.cs:110` | at the look's end, the first look a baseline (`Attention.cs:224`); not for the person's stop, a limit, a refused choice or an end its quest's park says |
| the plugin point `session/ended` | `Driver.cs:1638-1651` | the person's stops and limits included, the frame with no limit flag (`HookPoints.cs:115-125`) |
| `SESSION_ENDED` | — | **not sent**: its one writer says *a conversation's end* (`DriverLoop.cs:1228-1237`), every caller a chat runner's `onEnded` (`:920-929`, `:1207`) |
| the usage slice | `UsageStateSlice.cs:47` | wakes on `SESSION_ENDED` alone, so a driven end reaches it at its period |
| the lineage's `end` | `Driver.cs:3899` | |
| the headless host | `Host/Program.cs:1006-1007` | a console line and a notice; the window then says `NOTICE` in the focused window only (`ShellSignals.tsx:191`), with no balloon, where a shell's end gets a balloon and a toast in every window |

`SESSION_ENDED`, `session/ended`, `session.ended` and *Ended* each name a different set of ends.

**A conversation ends.** `SESSION_ENDED` (`DriverLoop.cs:1233`) and the log's `session.ended`. The attention watch leaves it out
by design (`Attention.cs:143-150`); no *ended* notice, no plugin point (*unverified* that none reaches `session/ended`). The
*answered* notice and `SESSION_ATTENTION` tell a turn that answered the person (`Notices.cs:111`; `DriverLoop.cs:1083-1090`).

**A session parks** (waits on the person).

| Channel | Where | Note |
|---|---|---|
| the log's `session.parked` | at the move (`SessionLog.cs:267-302`) | |
| `SESSION_ATTENTION` *Parked* | the next look (`Attention.cs:230-237`) | the baseline drops a park made around a restart (`SessionLog.cs:38-41`) |
| the `parked` row and its count | `NeedsAPerson.cs:266-273`, `:550-556` | not Ask Daoris, not an answered park (`:770-774`) |
| the `active` signature | `DRIVER_TICK` (`DriverLoop.cs:1026-1031`) | |
| a notice, a plugin point | none | |

The two comments that explain it disagree on who parks a record: *only the driver moves a record into* a parked state, *always
through this client* (`SessionLog.cs:34-37`), against *a state change on the record that nothing local performs — the session
asks, through its own connector* (`Attention.cs:119-122`).

**An account's limit is met.**

| Channel | Where | Note |
|---|---|---|
| *Ended* | suppressed for a limited end (`Attention.cs:246-252`) | |
| the look's news `limit-waits`, `limit-went-on`, `limit-resumed` | `LookSaid.cs:160-173` | a `look` notice, listed and never said (`NoticesStateSlice.cs:364-365`) |
| the record's `limit` | pushed (`SessionsStateSlice.cs:43-44`) | |
| the log's `account.limited` | `SessionLog.cs:132-146` | |
| `SESSION_ATTENTION` *Waiting* | once per cool-off (`Attention.cs:220-228`, `:297-306`) | the page's toast drops its detail (`Web/notifications/noticeWords.ts:77`) |
| the waits | `DRIVER_TICK` (`DriverLoop.cs:1062`); the account rows (`NeedsAPerson.cs:540`) | |
| `session/ended` | | a limited end reads as any other |

**A terminal's press waits for Confirm.** `CONFIRMATIONS`, numbered (`Modules/Platform/PipeConfirmations.cs:338`, `:697`);
`SESSION_ATTENTION` *Confirming*, one per press (`PipeConfirmations.cs:340`, `:708-717`), **not gated by the notify switch**, where the
look's attention (`DriverLoop.cs:948`), *Holding* (`HeldCallAnnouncements.cs:53`), `NOTICE` and `MESSAGE` are; the `confirmation` row
(`NeedsAPerson.cs:480-483`); the record `data/pipe-waiting/<id>.json` (`PipeWaiting.cs:39-62`); the log's `person.confirmation`
(`PipeConfirmations.cs:727-740`); the taskbar's flash (`App/MainForm.cs:170-180`). After a restart *ConfirmingRestored*
(`PipeConfirmations.cs:598-605`) has no words on the page, which toasts the driver's English (`noticeWords.ts:56-75`). A press at the **service's** person door gets `SERVICE_CONFIRMATIONS` alone: no balloon and no
row, since the attention slice reads only the pipe's (`AttentionStateSlice.cs:288-289`, `:422-432`).

**A word is said to a session.** `SESSION_QUEUED` in three shapes (`DriverLoop.cs:1163`, `:1171-1180`, `:1192-1204`); the record's
`said`, heard by the service's `words` family (`ChangeStream.cs:43-48`) and pushed by no slice (`SessionsStateSlice.cs:55-56`); the
event file's `user` with its reach and its `receipt`s; `held-words.json` (`SessionHomeFiles.cs:322-326`); the log's `message.sent`
from the page (`LogModule.cs:74-75`) and `session.reopened` (`SessionLog.cs:28-30`). *Answered* names four facts: a turn that took
the word ended `end_turn` (`SessionEvents.cs:63-64`), a repository's reply on the channel (`Channel.cs:201`), a conversation that
answered the person (`Notices.cs:64-69`; `Attention.cs:32-36`), and the person answering a park (`Attention.cs:187`).

**A quest is published or moves.** The quest log's append (`Quests.cs:1364`) wakes the quests, sync and asks slices and the plan;
the channel's lines use their own codes (`Channel.cs:110`), not the log's operation names; `DRIVER_TICK` where the considered
signature moved (`DriverLoop.cs:958-962`); `quest/consider` only at a planned start; the `unanswerable` row
(`NeedsAPerson.cs:298-304`). No notice; no desktop log line (the service's *unverified*).

**A tool call is held.** The calls slice's `waiting` (`CallsStateSlice.cs:57-66`), the `permission` row (`NeedsAPerson.cs:402`) and
*Holding* (`HeldCallAnnouncements.cs:36-55`), which has no first-read baseline (`:22-23`), where the attention watch, the notices
slice and `AskMessages` each take their first read as one.

**A session goes quiet.** The calls slice's `quiet`, the `quiet` row (`NeedsAPerson.cs:364`), a note, a console line and
`session.quiet` (`Driver/Sessions/Watch/SessionWatch.cs:50-61`); no attention event, notice or balloon.

### 3.4 Duplications and disagreements

| # | The question | How the channels differ |
|---|---|---|
| 1 | **What is a session's end?** | four names for four sets: `SESSION_ENDED` (conversations only), `session/ended` (runs the look drained, limits and the person's stops included), `session.ended` (every closed state at the move), *Ended* (the look's, less the person's, a limit's, a refused choice's and a park's) (§3.3) |
| 2 | **Who tells a park, and when?** | the log at the move, the attention watch a look later with a baseline that drops parks around a restart, the row from the records, the tick by a signature; two comments disagree on who performs it (`SessionLog.cs:34-37`; `Attention.cs:119-122`) |
| 3 | **Pushed with data, or invalidated?** | D182 pushes judged state; `STATE_CHANGED` and `DRIVER_TICK` (D169) still invalidate, and both refetch what lies beneath a pushed entry under the same keys (`ShellSignals.tsx:67-122`; `stateHub.ts:27-65`); `DRIVER_TICK` is data and invalidation in one; the held answers are a third shape, forgotten by presses and `STATE_CHANGED` but not by a ledger move, so the first ask after one answers stale and `ANSWERED` corrects it (`DriverModule.cs:109-114`, `:238-250`) |
| 4 | **The same look's facts on two channels** | `DRIVER_TICK` (considered, untrusted, waits, only when a signature moved) and the attention slice (every look, `AttentionStateSlice.cs:275-287`) |
| 5 | **The notify switch** | gates the look's attention, *Holding*, `NOTICE` and `MESSAGE`; not *Confirming* or *ConfirmingRestored* (§3.3) |
| 6 | **A first read: a baseline or news?** | a baseline for the attention watch (`Attention.cs:224`), the notices slice (`NoticesStateSlice.cs:361`) and `AskMessages` (`:86-103`); news for *Holding* (`HeldCallAnnouncements.cs:22-23`) |
| 7 | **Ordering** | per partition `seq` and epoch (pushes); none (`STATE_CHANGED`, `DRIVER_TICK`); a whole-list `seq` (`UNDER_WAY`, the two confirmations); a file's `seq` from a cache per process (the event file, `SessionEventLog.cs:45-63`, two processes appending to one file *unverified*); first in, first out dropping the oldest (`state/changed`); EVENTHOOK1's proposed run and `seq` |
| 8 | **One fact, several codes** | *answered* four ways (§3.3); the channel's quest lines against the quest log's operations (`Channel.cs:110`; `QuestLog.cs:4-109`); `SESSION_QUEUED` in three shapes; the person's default level urgent and a session's ordinary on the channel (`Channel.cs:727-729`, `:751-752`) |
| 9 | **Recorded, and read by nothing or only when pulled** | the lineage file (four writers, no production reader); the chain's ranks 3 and 4, declared and bound nowhere (`Chain.cs:170-172`); the record's `said`, which the service's `words` family hears and no slice pushes (`ChangeStream.cs:43-48`; `SessionsStateSlice.cs:55-56`) |
| 10 | **Two writers of one line** | the machine log's twin writers (`Driver/Platform/MachineLog.cs`, `Core/Platform/MachineLog.cs`) and its twin parser in `tools/usage-report.mjs`; the notices record written by the slice, the look and the headless host (`Notices.cs:182-218`) |
| 11 | **Where the shell is not the driver** | a shell waiting on another loop's lease runs no plugin (`DriverLoop.cs:832-835`), hears no change stream and sends no tick (§3.2); a headless loop tells `state/changed` nothing (`HookPoints.cs:61-64`) and its ends reach the window only as a focused window's `NOTICE` |

Also seen: a pipe ground's read of a held route adds a fresh id to its payload, so each read is a new entry against the 128
(`Modules/Platform/InstancePipe.cs:1056-1074`); and the change stream's observer writes a table, `store_identity`, into the
service's database (`ChangeStream.cs:483-494`).

### 3.5 The records this area writes

| Record | Writer (process) |
|---|---|
| `data/notices.json` (and `notices.unreadable-*.json`) | `Notices`, through the notices slice, `LookSaid` in whichever process looks, and the headless host |
| `logs/<date>.<source>.jsonl` | `Driver/Platform/MachineLog` (desktop, driver, browser); `Core/Platform/MachineLog` (host, connector) |
| `data/sessions/<id>.events.jsonl` | `SessionEvents`, in the process running the session |
| `data/sessions/<id>/lineage.jsonl` | `LineageFile` (connector), `lineage-hook.mjs` (Node, under Claude Code), `ChildLineage` (driver or shell) |
| `data/sessions/held-words.json` | `SessionWords` (shell; `SessionWords.cs:897-903`) |
| `data/sessions/<id>.cannot.json` | `GoOnMarks`, `AnswersAloneHold` (the loop) |
| `data/pipe-waiting/<id>.json` | `PipeWaiting` (the shell holding the lease) |
| `run/requests/*.told` | `PressesTold` (a terminal verb) |
| `run/running.json` | `RunningAddress` through `DriverLoop.Announce` (`:602-609`) |
| the service's database: `quest_log`, each family's revision, the channel's messages, marks and acks, `store_identity` | the HTTP host and the connector |
| memory only | slice snapshots; held answers; presses under way; both confirmations' lists; the attention watch's last look (`Attention.cs:162-181`); the notices slice's known and said ids; `AskMessages`; `HeldCallAnnouncements`; the waiting line's told set per connector; the change stream's cursor; `PluginHealth` |

### 3.6 Defects of the last ten days that came from these disagreements

17 of the 273 entries (2026-10-15 to 2026-10-25).

| Date | Entry | The two that disagreed |
|---|---|---|
| 2026-10-24 | Each part a limit cut off read as a failure, each with an *Ended* notice (LIMITRESUME1a) | the ledger's limit flag, read only by the strikes, and `SessionEnded`, the attention watch, the page's state word and the sessions slice, none carrying it (row 1) |
| 2026-10-23 | An approved hand-over that could not be taken read as refused (HANDOVERSAID1) | the take's own reason, sent to the look's line alone, and the ask's note with one refusal wording |
| 2026-10-23 | A repository set up from its page stayed not adopted (SETUPSTATE1) | the registration following only `lines-moved.json`, while the ref watch heard the outside move, and no `registry` change told the window |
| 2026-10-23 | A refused move back to the person was said to no one (HELPERWAIT1c) | a look line and the driver's memory, and *What needs you* judged from records |
| 2026-10-22 | A folder held on the own sign-in's file would have reached *What needs you* without its `own` (POOL1c) | the judged row carrying `own`, and the slice's allow-list dropping it |
| 2026-10-22 | Ask Daoris's *Go there* answered that its plan changed once its quest was taken (ASKQUESTS1) | the card's proposals re-asked on a press or a poll, and the pushes that moved the quest |
| 2026-10-22 | The modules suite's account tests on `main` still asked for the scopes the fold retires, and the push missed `pooled` (POOL1a1) | the `ACCOUNTS` pull answering `pooled`, and the accounts slice's allow-list not naming it |
| 2026-10-21 | A window on one workspace counted another workspace's sessions to review (NEEDSSCOPE1) | rows judged from the service's records carrying a workspace, and rows from the driver's lists carrying none |
| 2026-10-21 | What needs you called a session's tree unowned, titled it by its path, and said a kept branch's reason twice in English (OVERVIEWNOISE1b) | the inventory joining a tree to its unit through a branch record, and the session record naming the tree |
| 2026-10-20 | The window's counts disagreed: the title kept an older count, the status bar counted the person's conversations (SESSBADGE1) | the page's badge to the window thread, unordered, and the status bar's own count beside the cap's |
| 2026-10-20 | A sign-in panel mounted after Codex printed its code never showed the code (SIGNIN1) | the pushed output relay keeping no lines, and the pull reading only session buffers |
| 2026-10-20 | A pushed window asked the held permissions every 4 s, which a second window's snapshot test counted (AUTONOMY1b3b) | a 4 s poll beside the pushed calls slice that carried the same mark |
| 2026-10-19 | A tool call the rules refused read as still running in a session's head (WAITSAY1b) | the page's running call, ended only on completed, failed or cut, and both doors recording `refused` |
| 2026-10-19 | A park answered and asking again could go unsaid, and an answered one was said (AUTONOMY1e) | the attention watch comparing states alone, and the record holding an answered park as `awaiting-person` with words |
| 2026-10-19 | An idle Daoris told every window a tick at every look, and each tick asked git of every checkout (IDLECPU1) | the shell's `DRIVER_TICK` rule (any report line) and the page's own (changed lines only) |
| 2026-10-16 | A conversation whose turn ended with work offered Accept only in the review's side bar (ACCEPTFOOT1) | the head and foot reading the groups' `lands`, and the side bar its own plan from the diff |
| 2026-10-15 | An accepted session's plan read stale after a tab switch, and never in another window (STATEHUB1b) | a press refreshing only its own window's hook, and `DRIVER_TICK` only on the loop's own change |

## 4. The records, and who owns them

Each area's own table is §1.5, §2.5 and §3.5; this is the same records by their owner. The home's places are D172's table,
`HomeEntry` (`Core/Platform/DaorisHome.cs:317-426`), whose twins are the CLI's and the driver's.

| Owner | What it holds | Travels on the sync? |
|---|---|---|
| **The service** (its database) | `sessions` with `said`, the marks (`ask`, `opinion`, `serves`, `piece`, `help_mode`), `from`, `entry`, `limited`, the actor's fields (`Core/Sessions/Sessions.cs:476`); `quests` and `quest_log` (`Core/Quests/Quests.cs:837-972`); `asks`, `channel`, its marks, acks and cursors (`Core/Asks/*`); `opinions` (`Core/Review/OpinionStore.cs:34`); each family's revision for the change stream, and `store_identity`, which the stream's observer writes (`Core/Platform/ChangeStream.cs:483-494`) | the records and their keys, cleaned of machine paths; `said` and the actor's fields have no field on the wire (`.claude/knowledge/session-record-fields.md`) |
| **The service** (files it writes in the home) | a helper's ask and answer (`Core/Sessions/HelperBox.cs:43-44`); a question (`QuestionBox.cs:41`); an agent's hand-over ask (`HandOverAskBox.cs:28`); a brainstorm's request; a partner's answer; the lineage's call lines (`LineageFile.cs:59-93`) | no |
| **The driver library** (the look, in the shell or the headless host) | `data/sessions/<s>.*`: the conversation's id, go-on marks, reopen marks, the event file; `data/handed/`; `cooling.json`; `data/helpers/*.state`, `.capture`, `.back`; `data/partners/*`; `data/opinions/*`; `data/brainstorms/*`; `data/notices.json` (written also by the slice and the headless host); `data/agent-offers.json`; `driver.json`'s rules and actors (through the modules' doors); the lineage's stream and end lines; the machine log's `desktop`, `driver` and `browser` files | no |
| **The chat runner** (the shell only) | `<s>.limit-move.json` (`Chat/LimitBox.cs:31-46`); the hand-over's box for a conversation; Ask Daoris's `.help.json` link | no |
| **The desktop modules** (the shell) | `held-words.json` (`Modules/Loop/SessionWords.cs:901-942`); `data/standing-confirmation.json` with its appointments; `data/pipe-waiting/`; `work/rooms/roles/`; `run/running.json`; supervisor decisions (`QuestionSupervisorDecisions.cs:17-22`) | no |
| **The terminal** | `run/requests/*.told`; `<s>.new-session.json` (`GoOnNew.cs:29-33`) | no |
| **A harness** | the lineage's hook lines (`lineage-hook.mjs`); its own conversation in the account's home, which Daoris never reads (D189 point 5) | no |
| **Memory only** | the slices' snapshots, the held answers, the presses under way, both confirmations' lists, the attention watch's last look, the notices slice's known ids, a transient episode, the hook queue, `PluginHealth` | — |

Three things follow from the table without a judgement. One concept may have records under several owners: a helper's ask is
written by the service (`ask`, `answer`) and the driver (`state`, `capture`, `back`) and read by the modules; a hand-over's ask by the
service and its state by the driver. Two writers share some files (`data/notices.json`; the lineage file, with four). And what a
move in flight owns is written by the chat runner for one path alone (§2.5).

## 5. The seams a foundation would cut along

Stated as questions, for the proposals to answer (`docs/2026-10-25-foundation-brief.md` §5 asks each proposal to). Each names the
rows that raise it.

**Who acts.**
- Is a participant one record, with its role a field that every reader reads, or a session record plus the marks and boxes each
  kind adds today (§1.3 rows 8, 18)? If one, which process owns it, and how does a kind with no session record today (a brainstorm
  voice, a role holder, a native child, a meeting's turn) appear in it?
- Is the rule that says whether a kind may run one shape across kinds and scopes, or one per kind (row 1)? Does a rule parsed and
  read by nothing (the brainstorm's mode) stay a rule?
- Is admission one judge, from the person's rule to the connector's filter, or a judge per door (rows 6, 7, 15, 16)? Which way does
  it fail on a caller it cannot place: open, as the role filter does, or closed, as C2a says?
- Is *the driver's own session*, which the cap counts, a property of the participant or a judgement over its marks (§1.6, CAPCOUNT1)?
- Is an actor's walk one walk with the kind as a parameter (rows 3, 4, 5), and which starts may still bypass it (§1.2)?
- How do an appointed role (D208, D209) and an actor relate, and is a role holder a participant, given today's holder runs outside
  the pool and the actors (rows 21, 22)?

**How work moves.**
- Is *where a line's next words go* one decision, with the go-on, the limit, the curve, the hand-over, the briefed move, the carry-on,
  the retry and the workflow's send-back as its triggers, or a decision per path (§2.2)? Where would it run, given that driven lines
  are planned by the look and conversations by the chat runner?
- Is *the own account cools: wait or move* one rule, and *which account a part takes* one function, read by every path (§2.4 rows 1,
  2, 9, 11)?
- Is a move in flight a record of its own, owning its delivery and recovery, for every kind of move (§2.5; SWITCHCTX1's carry
  record), and who owns it: the service, the driver, the shell?
- Do words across a move hold by construction, one place deciding where a word said during a move goes (§2.4 rows 3, 14, 15)?
- Does every path read a failure through the one reader and its four kinds, conversations included (rows 6, 8)?

**What is said.**
- What is one event: who mints it, its id, its kind's code, its order and its scope? Which of today's channels are projections of
  it, and which stay channels of their own (§3.1)?
- Is the page's state pushed with its data alone, or do invalidation and held answers remain beside the pushes, and on what
  rule (§3.4 row 3)?
- Is there one vocabulary of kinds of change, one code each, with the sentence composed per reader and language (rows 1, 8)?
- Which reader may read what: the page, the person's notifications, a plugin, an outside session (EVENTHOOK1), the terminal, a
  teammate's mirror? What never leaves the machine?
- What is durable, what is memory, and how does a reader learn it missed something (rows 6, 7, 11)? Is a record written by four and
  read by none (the lineage) a channel, or a store waiting for its reader?

**Across the three.**
- For each record of §4, is there one owner? Where a concept's record is split across processes today, which part moves?
- Which in-flight design (EVENTHOOK1's event, COWORKVIEW1's read, SWITCHCTX1's carry record, AGENTERRORS1's reader) is the first
  piece of the foundation, and which would it re-cut?
- What is the smallest step that proves the shape on today's tests?

## 6. How this survey was read, and what it does not cover

- **Read, not run.** The steward read the decisions and the in-flight designs named in the brief, and three read-only reading agents
  read the code of each area at `0e195ee61`, each claim with its line. The steward checked a sample of each area's claims against the
  lines before taking them (among them `ResumeAccount.cs:78`, `HarnessRoster.cs:1008`, `LimitWaits.cs:185-186`, `ChatRunner.cs:2670-2675`,
  `LimitMove.cs:447`, `:565`, `LimitResume.cs:19`, `TransientRetry.cs:12-28`, `DriverLoop.cs:1228-1237`, `UsageStateSlice.cs:47`,
  `Chain.cs:170-172`, `PipeConfirmations.cs:708-717`, `HeldAnswers.cs:40`, `SessionLog.cs:34-37`, `Attention.cs:119-122`,
  `RoleFilter.cs:105-141`, `HelperChoice.cs:66`, `PartnerMeasurements.cs:44`, `ActorRun.cs:176-177`, `StandingConfirmation.cs:330-339`,
  `CallLineageFilter.cs:8-24`, `identity.ts:79`, `Driver.cs:1917`). Line numbers move with every merge; a proposal cites the code
  again at its own commit.
- **The defects' classes are a judgement.** Each entry of the ten days was read for its root cause and counted only where two
  mechanisms decided one question differently, or one had not learned what another had. Borderline cases are marked (§1.6); the
  workflow close's entries (FABRIC-CLOSE1c10, c13, c14, c26) are counted under how work moves because a go-on and a step's carry-on
  disagreed. A looser reading would add STEER2a (a press against a next-step door) and CLOSELAND1 (a record's state against its
  quest's).
- **Not covered.** The git and landing code (D147, D198, GITAPP1), the workflow graph's own runtime (D157, D190), the knowledge index,
  the CLI's doctrine, the browser and the terminal panel, except where they tell a change or move a line. The page's rendering is
  surveyed only where it reads a channel.
- **Nothing here is a proposal.** Where a row reads like a defect, it is a fact about the code at this commit; whether it is one is
  for the proposals and the steward.
