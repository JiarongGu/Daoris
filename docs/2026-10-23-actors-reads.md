# MODELROLE1: the two reads of round 1 (D191)

Codex's read and an independent Opus 5.5 reader's foundation read (in Fable's place, D191's amendment of 2026-10-23) of
`docs/2026-10-23-actors-design.md` and D205 at `45288ece5`, verbatim but for their headings set one level down. The steward ran
both from outside the install, read-only; neither saw the other's read. Round 2 answers each finding in the design's reads table.

## Codex

Codex's read, read-only.

### 1. Claims about today’s code

1. **Non-blocking — one correction is itself wrong.** The design’s correction table says there is no “No model is named” statement at `Adapters.cs:2074` (`docs/2026-10-23-actors-design.md:31`). That line says **“No model is ever named”** for the native adapter; `:2164` repeats it for chat. **Smallest fix:** retain both references and remove “none at `:2074`.”

2. **Non-blocking — model-scoped windows are already read into history.** The design calls them unread (`docs/2026-10-23-actors-design.md:40`, `:80`, `:265`, `:674`). `Driver/Accounts/Usage/ClaudeUsage.cs:106` calls `Seen`; `:113–129` enumerates the windows and retains their scope, utilization and reset. Only the account-walk projection at `:78` excludes them. **Smallest fix:** say “recorded in usage history, unused by rotation and cooling,” and scope row k to that missing behavior.

3. **Non-blocking — D205 overstates what determines today’s model.** “Every session runs its account’s own model” does not follow from Daoris adding no model flags (`docs/decisions/D205.md:12–15`). Custom commands already supply arbitrary arguments (`Driver/Agents/Adapters/Adapters.cs:2090–2093`, `:2180–2183`); repository settings and `ANTHROPIC_MODEL` outrank account settings (`Driver/Agents/AgentsCommand.cs:821–822`). **Smallest fix:** say today’s starts leave model selection to the tool’s existing cascade, including custom commands.

### 2. The shape

Eight actors for existing starts, with orientation and exact work waiting for their own start or signal, is a sound choice. Session-level carriers improve on Claude’s shared settings approach. Keeping permission lists separate from model choices, retaining maker-based independence, using one inline editor, and measuring the doors first also serve the direction. The partner digest’s present contents support the synthesis’s correction: `Driver/Conversations/Domain/PartnerConfiguration.cs:56–72` hashes confinement-related inputs, not the settings file’s bytes. Adding model names to that hash merely to distinguish models would not strengthen the confinement proof.

4. **Blocking — “no reviewers” currently means off, not inheritance.** The design makes an empty reviewer list read the actor (`docs/2026-10-23-actors-design.md:297–300`, `:615`; D205 `:43–45`). Today `OpinionRule.None` has an empty list, and `IsNone` is exactly `Reviewers.Count == 0` (`Driver/Review/Domain/OpinionRules.cs:24–27`). JSON `false` reads that way, while an object with no reviewers is refused (`:159–179`). The proposed meaning either enables an explicitly disabled opinion or remains unreachable behind existing off checks. **Smallest fix:** distinguish disabled, inherited reviewers and explicit reviewers in the rule’s representation and every reader. Require migration fixtures proving existing `false`, absent and malformed rules retain their behavior.

5. **Blocking — rule-supplied account defaults are missing from the effective choice model.** A listed agent absent from the actor receives account default (`docs/2026-10-23-actors-design.md:295–296`), although the actor’s editor presents its explicit list as the choices that can run (`:422–445`). That execution has no corresponding index in `choice: 1–4`, nor is it necessarily `pick`, `before` or `command` (`:555–556`). For example, a rule naming Claude and an actor containing only Codex still runs Claude’s account default. `ReviewerChoice` selects from the rule’s entries today (`Driver/Review/Domain/ReviewerChoice.cs:215–226`). **Smallest fix:** define this derived choice explicitly, give it durable provenance such as `rule-default`, and show that scoped rules can supply account-default entries. Alternatively, require an explicit actor choice before such an agent runs. The preview and opening explanation must identify the actual effective order.

6. **Blocking — Brainstorm conflates alternatives with participants.** The design uses the actor list as voices, and lets a passed choice surrender its voice to the next unused choice (`docs/2026-10-23-actors-design.md:304–306`). Elsewhere that same list means alternatives, tried only when an earlier choice cannot start. Thus accepting two choices can start two generators rather than one generator with a fallback; repeated models of one agent can also become supposedly separate voices. Today participant names are deduplicated and planned separately (`Driver/Loop/DriverBrainstorms.cs:345–355`), then launched as distinct generators (`:385–394`). Substituting another agent for a named participant also changes the maker receiving the packet; D176 requires disclosure before that transfer (`docs/decisions/D176.md`, point 2). **Smallest fix:** keep a frozen participant plan separate from each participant’s alternatives, retain named-agent and policy fences, and recheck disclosure before a different maker receives the packet. Codex’s proposal preserved this distinction better.

7. **Blocking — scalar write-through can change other actors without their acceptance.** Build and an unset Chat both inherit `adapter` (`docs/2026-10-23-actors-design.md:352`), but saving Build writes its first agent into that key (`:403–405`). Changing Build can therefore change Chat, and the compatibility Brainstorm fallback, despite accepting only Build. Those dependencies exist today in `Modules/Bridge/Routes/ConversationRoutes.cs:48` and `Driver/Loop/DriverBrainstorms.cs:348`. The legacy `driver adapter` writer also edits the scalar directly (`Driver/Platform/DriverChoicesCommand.cs:528–534`), unlike the two verbs explicitly migrated by the design. **Smallest fix:** preserve dependent actors’ effective choices before writing through, or stop using that shared scalar as their live fallback after migration. Specify how every legacy writer behaves once explicit actors exist, and test that changing Build leaves Chat and Brainstorm unchanged unless the reviewed change names them.

### 3. What is missing or unsafe

8. **Blocking — cached offers and refusals can skip an account that supports the preferred model.** Offers are cached by agent and version (`docs/2026-10-23-actors-design.md:544–545`), then their absence excludes the whole choice before a spawn (`:218`). An open-time refusal likewise advances to the next choice (`:219–223`), and missing entitlement passes the choice (`:264`). None of those rules ensures another eligible account gets the preferred model first. Today account selection supplies a distinct configuration home and possibly account-specific environment (`Driver/Accounts/Harnesses/HarnessRoster.cs:1244–1254`); the offered controls are explicitly session-scoped (`Driver/Agents/Acp/Acp.cs:938–944`). A negative cache can also prevent the very opening that would refresh it. **Smallest fix:** treat cached offers as editor hints unless their applicability is established; make entitlement and negotiation failures account-specific, exhaust eligible accounts before advancing choices, and define invalidation and refresh. Test two accounts with different offers and an entitlement change without a version change.

9. **Blocking — “unchanged walk” lacks a release mechanism for failed negotiation.** The design promises only the successful choice counts and cites the panel’s `counts` distinction (`docs/2026-10-23-actors-design.md:254–262`). But public `SelectAsync` always counts (`Driver/Accounts/Harnesses/HarnessRoster.cs:883–886`), and `Pass` reserves the chosen start before returning (`:1411–1415`). Its private rollback is used for probe-lock failures (`:1424–1440`), not a later model refusal. Conversely, `counts: false` also skips start-specific usage refresh and reservation behavior (`:1076–1079`, `:1191–1197`). **Smallest fix:** extend the selection seam with a reservation that can be committed or released, plus exclusions local to the attempted start. Test refusal, cancellation and repeated alternatives without leaked reservations, distorted rotation or premature account reuse.

10. **Blocking — the existing ACP setter does not establish the acknowledgement claimed.** The design reuses `SetConfigOptionAsync` unchanged and describes acknowledgement as the agent’s answer (`docs/2026-10-23-actors-design.md:184–188`, `:335–341`). When the response has no option list, that method substitutes the requested value locally (`Driver/Agents/Acp/Acp.cs:533–536`). When a list is returned, it does not check that its current value matches the requested value. A successful RPC is therefore insufficient evidence that the named model was selected. **Smallest fix:** distinguish returned state from locally assumed state, verify the requested offered value before releasing work, and hold or pass an unconfirmed selection. Test success with no list and success returning a different current value; neither should be labelled agent-reported acknowledgement.

11. **Blocking — the native effort fallback restores the precedence problem it rejects.** The design correctly says spawn-file `effortLevel` can lose to `modelSettings.<model>.effortLevel`, then permits that same fallback when `--effort` is unavailable (`docs/2026-10-23-actors-design.md:203–206`). The existing contract explicitly gives per-model effort precedence (`Driver/Accounts/AgentSettings.cs:7`, `:25–30`; `Driver/Agents/AgentsCommand.cs:815–818`). A saved high effort can therefore outrank an actor’s low effort while the session is presented as having taken that choice. **Smallest fix:** offer named effort only through a measured mechanism that honors it, or explicitly make unsupported effort an account-default execution before dispatch. Row a must test conflicting saved per-model effort, not merely that a settings key reaches the process.

12. **Blocking — composer changes cannot be restored reliably from today’s notes.** The design promises that the last composer change wins on go-on (`docs/2026-10-23-actors-design.md:278–280`, `:662`), but `asked` is set only at the open (`:556`). Today the change note retains the option’s display name and the selected value’s display name, not its category and actual value (`Driver/Conversations/Chat/ChatRunner.cs:1723–1728`). Display names need not be unique or reversible. An effort-only change also needs the previously selected model preserved. **Smallest fix:** persist the effective conversation override as structured model/effort values when each change succeeds, separately from immutable opening provenance. Restore it on resume and briefed continuation. Test duplicate display labels, effort-only changes, restart and subsequent actor edits.

13. **Blocking — the proposed reported-tier record loses valid information.** The design requires every reported model in first-use order, but caps the record at three and updates it only when a new model appears (`docs/2026-10-23-actors-design.md:337–339`, `:557`). A conversation can select more than three models, and effort or acknowledgement can change without adding a model. Existing `TurnModels` also sorts names and collapses an unsplit multi-model turn to `mixed`, discarding the individual names (`Driver/Accounts/Usage/TurnModels.cs:69–80`); accepted splits are sorted too (`:181–185`). **Smallest fix:** retain reported identities independently of whether token attribution reconciles, define explicit overflow rather than silent truncation, and persist configuration changes independently of new model names. Row b needs unsplit mixed, fourth-model and same-model effort-change cases.

14. **Blocking — row b has no provenance home for Brainstorm.** The actor table includes every brainstorm generator and synthesizer (`docs/2026-10-23-actors-design.md:136`), while row b promises the session record’s fields and session Details for every start (`:660`). Brainstorm deliberately opens **no service session record** (`Driver/Loop/DriverBrainstorms.cs:634–639`). Its local pass record currently holds role, agent/account provenance, state, codes and turn count (`Driver/Brainstorms/Domain/BrainstormDocument.cs:7–13`; reader at `Driver/Brainstorms/BrainstormFiles.cs:195–196`). **Smallest fix:** add actor selection and reported-tier provenance to the existing local brainstorm records and their reader/display/terminal chain, including repair attempts. Put that work explicitly in row b; a thirteen-start actor table alone cannot prove it.

15. **Blocking — person confirmation is guaranteed only on the managed door, not for full-access setting changes generally.** The design says both modes require the person’s press, and claims the code already enforces that (`docs/2026-10-23-actors-design.md:90`, `:505–513`; D205 `:57–60`). `Person` classification protects requests entering the pipe (`Modules/Platform/InstancePipe.cs:557–562`). Full-access Ask Daoris nevertheless has unrestricted file access and no tree guard (`Driver/Conversations/Chat/ChatRunner.cs:758–774`), so editing `driver.json` bypasses that route. Section 15 acknowledges exactly this hole (`docs/2026-10-23-actors-design.md:678–680`). **Smallest fix:** settle the distinction explicitly: either scope the guarantee to supported setting operations and describe raw edits as unenforced under D183, or introduce an authority boundary that rejects unconfirmed raw changes too. The proof must exercise both the managed command and direct-file path; a pipe confirmation test proves only the former.

**15 findings; 12 blocking.**

**Verdict: another round.** Keep the eight-actor layout, session-level carriers, maker independence and confinement-based partner digest. The next round needs a consistent effective-choice model covering disabled and inherited rules, brainstorm participants, migration dependencies, account-specific negotiation, durable conversation overrides and reported-tier provenance. It must also settle what person confirmation actually guarantees under full access. These affect the central selection and spending contract, so they warrant another read after revision.

## Opus (foundation)

The independent Opus reader's foundation read, read-only.

### Opus's foundation read of MODELROLE1's design (D205, round 1)

*An independent Opus 5.5 reader, in Fable's place under D191's amendment of 2026-10-23. Read at `45288ece5` (the design's
branch; its code is `656acf6de`'s, the design commit adds documents only). Read whole: `docs/2026-10-23-actors-design.md`,
`docs/decisions/D205.md`, `docs/2026-10-23-actors-brief.md`, `docs/2026-10-23-actors-proposals.md`. Loaded for it:
`.claude/knowledge/session-record-fields.md`, `model-decoupling.md`, `translation-parity.md`, `compatible-builds.md`;
D163, D183 (HELPMODE1c3's note), D201, D203 at the points cited; `docs/model-roles.md`; the glossary and
`Web/scripts/names-check.mjs`. No network: the design's reading of Claude Code's model-configuration page is not checked here.
Paths use the design's abbreviations (`Driver/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/`, `Core/`, `Mcp/`,
`Web/` = `src/Daoris.Web/src/`).*

The shape is right and I would build on it: actors as kinds of start, an ordered list of choices in the tool's own words,
choice-major over D197's walk, the session-level carriers, the rules kept as fences, the upgrade read from today's keys, every
change the person's press, and *asked* beside *ran* never judged. What follows is where the mechanism under that shape does not
yet hold against the code.

#### 1. Facts the design states, checked

**§0.2's corrections**

| Claim | Rests on | Holds? |
|---|---|---|
| "No model is named" sits at `Adapters.cs:1733`, `:1870`, `:1962`, `:2164`, and **none at `:2074`** | `Driver/Agents/Adapters/Adapters.cs` | **Partly.** The four hold; `:2074` *does* carry it: "**No model is ever named** (D24)", the pipe door's class remark. Cosmetic |
| `WalkAsync` runs `:1047`–`:1283` | `Driver/Accounts/Harnesses/HarnessRoster.cs` | Holds |
| `DriverMeetings.cs:26-27` is the remark; code `:249`; `Unavailable` `:320` | `Driver/Loop/DriverMeetings.cs` | Holds |
| Verb list `:154-155`; `helper` `:540`/`:545`; `intake` `:556`/`:562` | `Driver/Platform/DriverChoicesCommand.cs` | Holds (within a line) |
| Parse error `:866`; `Save` `:914`, lock `:918`, `JsonKeeping.Kept` `:923` | `Driver/Platform/DriverConfig.cs` | Holds |
| Every `SET_*` is `Person` (`:234-245`); `SET_STANDING_CONFIRMATION` is `Window` (`:269`) | `Modules/Platform/PipeCensus.cs` | Holds |
| The ACP door's `options.settings` is a file path (`Adapters.cs:1794-1795`, `:1809`); the pipe's `--settings` (`:2222-2226`); `SpawnSettings.Write` names no model (`Permissions.cs:507-588`) | the same | Holds; the adapter's own remark calls it the SDK's programmatic tier, "the one `--settings` fills" |
| `PartnerConfiguration.Digest` hashes `posture=`, `guard=` and the sorted rule lines only | `Driver/Conversations/Domain/PartnerConfiguration.cs:56-72` | Holds. The configuration also carries `Agent` and `HarnessVersion` (`:28-29`), so two choices of one agent read one row |
| Codex and dsh are refused by a missing `SettingsFile`, not by name | `Driver/Accounts/AgentSettings.cs:242-245` | Holds |
| The usage answer holds `seven_day_opus` and `seven_day_sonnet`; `:78` reads `five_hour` and `seven_day` only | `Driver/Accounts/Usage/ClaudeUsage.cs:41`, `:78` | Holds; in the one recorded answer both model windows are `null`, so row k may find nothing to read |
| `--effort` (with `max`) is documented, and `/model` > `--model` > `ANTHROPIC_MODEL` > settings `model` | Claude Code's docs, read 2026-10-23 | **Not checked here** (no network). The model order matches my own reading of those docs; see N1 for effort |

**The rest of what it cites** — `Acp.cs:82`, `:516-517`, `:527-539`, `:918-923`, `:940-944`, the resume's posture near
`:2626`, `:1379-1394`, `:2216-2220`; `Driver.cs:1671-1672`, `:2583`, `:2674` (`HandRules`), `:3161-3180`, `:3775-3778` (no
`onOptions`); `ChatRunner.cs:2185-2190`, `:424-426`; `HarnessRoster.cs:883-886`, `:924`, `:991-995`, `:1003-1007`, `:1786`;
`ReviewerChoice.cs:175-237`; `HelperChoice.cs:15-37`; `PartnerChoice.cs:17-31`; `DriverBrainstorms.cs:345-359`, `:418`,
`:603-609`; `BrainstormDesk.cs:63` (`MostAgents = 4`); `TurnModels.cs:9-25`; `StructuredOutput.cs:212`, `:332`;
`UsageHistory.cs:134`, `:606-640`; `InstancePipe.cs:557-562`; `HelpSettingProposals.cs:31-38`; `HelpRoomDoors.cs:160-161`;
`HelpAccountTool.cs:10-31`; `HelpAccountProposals.cs:7-20`; `Sessions.cs:63`, `:95-99`, `:158`, `:190-193`; the three home
twins and `home.test.ts:323-328`; `DriverIntakes.cs:111`; `HelpRoutes.cs:52`; `ConversationRoutes.cs:48`;
`WorkflowGraphRun.cs:314-316`; `DriverConfig.cs:411-434`, `:1452-1455`; `headFacts.ts:172`; `PartsStrip.tsx:18`;
`glossary.json:477-478`, `:482`, `:535`, `:537`, `:539`, `:581`, `:628`, `:651`; `notifications.json:55`; zh `note.json:53` —
**all hold**, with drift of one to three lines.

**What the design rests on without citing a line, checked**

| Claim | Where it lives | Holds? |
|---|---|---|
| The account walk runs "unchanged" inside each choice, and "choice 1 … was refused by account-1 and account-2" in one start | `WalkAsync(adapter, config, workspace, chosen, kind, counts, own, ct)`, `HarnessRoster.cs:1047`; the count is taken under the gate (`:1176-1214`) | **No.** The walk takes no per-start exclusion, so an account that refuses a model cannot be passed for one choice without cooling it; and a selection is counted as soon as it is made. See B2 |
| A model not offered is caught "before the first prompt" and the walk "continues at the next choice within the same start" | The record opens at `Driver.cs:2108` (agent, version, profile) **before** `Prepare` (`:2320`) and `HoldAsync` (`:2362`), where `session/new`'s options are first read (`Acp.cs:940-942`); the pipe door puts the prompt in argv (`Adapters.cs:2092`) | **No.** See B1 |
| `no-full-access` is the only posture pass a choice needs | `DrivenAccess.For` refuses a repository's `asking`/`unrestricted` permission prompts on an agent with no such mode (`Driver/Sessions/Domain/DrivenAccess.cs:106-112`), and the start **holds** (`Driver.cs:1775`) | **No.** See B3 |
| Only Build and Chat read `adapter` | `config.Adapter` is read at `DriverBrainstorms.cs:348`, `ConversationRoutes.cs:164`, `Modules/Bridge/Routes/SessionsVerbs.cs:33`, `BriefedMove.cs:111`, `ChatGoOn.cs:372`, `OpinionGateLook.cs:368`, `SessionGroups.cs:837`, `HelpRoomMachineNow.cs:15` and more | **No.** See B4 |
| "Every start names its actor through a required parameter of `HoldAsync` and the chat runner's start" covers every run | A native session's later runs start outside both: `GoOnStart` (`DriverContinuations.cs:899-906`) and `AgainAsync` (`:1031-1039`) | **No.** See B5 |
| "An empty `reviewers` reads the actor's choices" | `OpinionRules.Read` refuses a rule naming no reviewer (`Driver/Review/Domain/OpinionRules.cs:179`, `NoReviewers` `:140`) | **No: no such rule exists.** See B6 |
| `handOver` (`ask`/`on`/`off`) governs Daoris's own offer | `HandOverRules.ModeShape`: "whether **an agent's ask** to hand its session over …" (`Driver/Conversations/Domain/HandOverRules.cs:57-58`); the page's offer is "taken only by the person's press" (`Web/work/start/handOverOffer.ts:24`) | **No.** See B8 |
| A `Person` route waits for the window's card whoever calls; D201 covers no standing choice (point 3) and no agent Daoris runs (point 4) | `InstancePipe.cs:556-562`, `PipeConfirmations.FieldsOf` (nested payloads shown, 256 values); D201 points 3-5 | Holds |
| No `settings_*` grant reaches actors | HELPMODE1c3's five tools: `notify`, `language`, `timeout`, `cap`, `cooloff` (D183's note of 2026-10-20) | Holds |
| dsh has no full posture | `ISessionAdapter.FullPosture => null` (`Adapters.cs:1275`), not overridden by `DshAdapter` | Holds |

#### 2. Blocking findings

**B1. The in-start move happens after the record is open, and on the pipe door after the prompt is sent.**
- *The design:* §3.3 "a model not among them … stops the process before the first prompt … and the walk continues at the next
  choice within the same start (C)"; §3.3 the pipe door's unknown `--model` "read as `refused`"; §4.3 "after a prompt is sent,
  … never retried on another model or agent"; row c's install proof.
- *The code:* a driven start selects its account (`Driver.cs:1734`), then **opens the session record** naming its agent,
  version and profile (`:2108`), then prepares and spawns (`:2320`, `:2362`); the options are first seen at `session/new`
  (`Acp.cs:940-942`). `Session.Adapter` travels and cannot change; the account was counted when chosen. A conversation opens
  its record before its spawn the same way. On the pipe door the target is the `-p` argument (`Adapters.cs:2092`), so a model
  refusal can only arrive after the prompt was handed.
- *Smallest change:* say it in §3.3: (a) a refused or unoffered **model on the same agent** is replaced on the open session by
  the next choice of that agent (`set_config_option` again, no respawn), the record's `choice`/`asked` updated through their
  later door (N3); (b) a next choice on **another agent** ends the opened record (`choice-not-offered`, no strike, the reading
  kept, its count returned) and the next look takes it, a conversation keeping the person's words and saying so; (c) on the pipe
  door, a model refusal is recognised only by row a's measured words on a turn with no output and no tool use, is the one named
  exception to §4.3, is kept as a reading (B2) and ends the part with no strike. Row c's proof follows.

**B2. Offers and refusals are per account; the design keeps them per agent and version and passes the whole choice.**
- *The design:* §3.3 "judged before a spawn: a choice whose model the agent's last reading lacks is passed"; §9.2
  `agent-offers` keyed by "each agent and version"; §4.1 "a missing model entitlement … passes the choice for this start, coded
  `refused`", beside §4.6's note "refused by account-1 **and** account-2" and the choice-major rule itself.
- *The code:* both Claude doors run as `claude-code`'s accounts (`AccountOf`, `Adapters.cs:1837`; the account seam
  `CLAUDE_CONFIG_DIR`), so what `session/new` offers and what a turn accepts are that account's. `WalkAsync` has no exclusion to
  pass one account for one choice in one start.
- *Consequence:* one account's reading passes the choice for every account (a Pro-like account read last starves `opus` on the
  others), or one account's refusal skips the rest of that choice's accounts, against §4.1's own order; and on the pipe door,
  with no reading kept, every look spawns choice 1 again on each refusing account.
- *Smallest change:* key `agent-offers` by agent, version **and account**; judge `not-offered` and `refused` per account inside
  the choice, through a per-start set of passed accounts handed to the walk (not a cool-off, not counted); pass the choice only
  when every account of it is passed; keep a pipe-door model refusal in the same reading so the next start passes that account
  without spawning. Say in §4.1 that the walk gains that one parameter.

**B3. A repository's permission prompts can starve an actor.**
- *The design:* §4.1's pass codes have `no-full-access` for Ask Daoris alone.
- *The code:* a driven start whose repository's prompts are `asking` or `unrestricted` on an agent with no `AskingPosture` /
  `FullPosture` is refused by `DrivenAccess.For` (`DrivenAccess.cs:106-112`) and **held** (`Driver.cs:1775`), after the walk
  chose and counted an account. A Build whose choice 1 is such an agent (dsh; a plugin harness) holds every look in that
  repository and never reaches choice 2. Ask Daoris's restricted mode requires the asking posture the same way (D183 point 3).
- *Smallest change:* one pass code, `no-posture` (covering `no-full-access`), judged per choice **before** the walk from
  `DrivenAccess.For` and Ask Daoris's mode, for every actor whose start resolves an access.

**B4. The migration's write-through moves actors the person did not touch, and the old doors are not migrated.**
- *The design:* §6.1 Build **and** Chat read `[adapter]`; §6.4 setting Build writes its first agent through to `adapter`;
  §6.4 names `driver intake|helper` only; §8.2 "in both modes the change is the person's press".
- *The code:* (a) `adapter` is read by Chat's start (`ConversationRoutes.cs:48`, `:164`, `SessionsVerbs.cs:33`), the
  brainstorm's fallback (`DriverBrainstorms.cs:348`, an *as before* actor), `BriefedMove.cs:111`, `ChatGoOn.cs:372`,
  `OpinionGateLook.cs:368`. So the person's Save on Build silently changes Chat and the brainstorm's agent — exactly what
  `IntakeAdapter`'s remark forbids ("the second changing must not quietly move the first", `DriverConfig.cs:419-421`), and in
  row c, before Chat's row d exists. (b) `daoris driver adapter` writes `adapter` with **no route** (`Change(null, null, …)`,
  `DriverChoicesCommand.cs:528-532`), so a terminal, or Ask Daoris under full access, changes an *as before* Build or Chat with
  no card. (c) `setting_propose` still has `adapter`, `intake` and `helper` doors (`HelpSettingProposals.cs:37`), the room's
  doors table still lists all three (`HelpRoomDoors.cs:159-163`), and `SET_INTAKE`/`SET_HELPER` remain in the census.
- *Smallest change:* (a) the write that first sets any actor read from `adapter` writes every other actor still read from it as
  its explicit `before` list in the same write, and the screen says which; (b) `driver adapter <agent>` becomes Build's
  one-choice list through `SET_ACTOR` (`Person`), saying what it replaced; (c) name each old door's fate in §6.4 (retired into
  `actor_propose`, or writing a one-choice list through `ActorChoices.Judge`), with their census and coverage rows.

**B5. A native session's later runs can drop the carried model.**
- *The design:* §3.2 "`--model` … appended after the person's command as the other flags are (`Adapters.cs:2088-2096`)"; §13's
  census on `HoldAsync`'s and the chat runner's start.
- *The code:* a native driven session's later runs (the person's words, a background go-on) start through `GoOnStart`, which
  re-hands the settings and servers by hand because callers append them (`DriverContinuations.cs:899-906`), and `AgainAsync`
  (`:1031-1039`) — neither through `HoldAsync`. `PrepareResume` (`Adapters.cs:2122-2131`) and `PrepareChat` (`:2172-2184`) are
  the other two native prepares. A flag added at the first spawn only, or at a caller, is lost on the next run, which then runs
  the account default; the record's *ran* would show a second model and nothing would say why.
- *Smallest change:* the asked choice rides `SessionTarget`/`ChatTarget`, and every native prepare (`Prepare`, `PrepareResume`,
  `PrepareChat`) and every protocol open and resume appends or sets it; the census holds the target and the three prepares, and
  the pipe stub proves a go-on run carries `--model`.

**B6. A second opinion's empty `reviewers` does not exist, and its actor orders nothing.**
- *The design:* D205 point 6, §4.4 ("an empty `reviewers` reads the actor's choices"), §12's D155 note, row e's proof
  ("empty `reviewers` reading the actor"), and the screen's *Second opinion · Codex · account default +1 · another maker first*.
- *The code:* a rule naming no reviewer is refused (`OpinionRules.cs:179`); the walk's entries are always the rule's named
  agents (`ReviewerChoice.cs:211-214`), or the one the ask names (`OpinionGateLook.cs:366-370`); a partner is always named by the
  ask (`PartnerChoice.cs:21-25`).
- *Consequence:* for Second opinion and Partner the actor can only supply a model per named agent; the order and the agents its
  row shows decide nothing, and the suggestion's "Codex first" there is decoration.
- *Smallest change:* drop the empty-reviewers branch from point 6, §4.4, §12 and row e (or name the change to `OpinionRules`
  and every door that judges it, which amends D155 point 3's "named"); say that Second opinion's and Partner's choices are read
  as *a model for each agent*, and their rows and card say so (*Models for the agents a rule or an ask names*).

**B7. Brainstorm's choices are voices, not an order, and the acceptance words are false for it.**
- *The design:* §4.4 voices "then the actor's choices in place of `config.Adapter` … up to `MostAgents`"; §6.2 Brainstorm
  suggested *Claude Code · opus · high, Codex · account default* ("different makers' voices"); §6.3's card "Daoris tries a
  later choice … only when an earlier one cannot start"; §7's editor "Choice 2 runs only when no account of choice 1 can".
- *The code:* with no named agents and no policy, a brainstorm runs one voice, `config.Adapter`
  (`DriverBrainstorms.cs:348`); `Generators` makes one pass per distinct agent name (`:347`), so two choices of one agent are one
  voice.
- *Consequence:* pressing *Use these* turns one voice into two on every such brainstorm — more spending, on another maker —
  under words that say the later choice runs only when the first cannot. D191 point 3's yes would be given on a false sentence.
- *Smallest change:* either Brainstorm's actor gives **one** voice in place of `config.Adapter` (the first choice that can run,
  the rest fallbacks, like every other actor), or its list is named as voices on its row, its editor and the card ("each choice
  is a voice; at most four"), the card counting the voices it adds. The first is simpler and keeps one meaning of *choice*.

**B8. `handOver: on` is widened to take Daoris's own offer.**
- *The design:* §4.2 "when every account of that agent is cooling, D163's offer names the actor's next choice …; under `on`
  takes it"; D205 point 5.
- *The code:* `handOver`'s mode says only "whether an agent's ask to hand its session over waits for your yes, is taken when its
  turn ends, or is refused" (`HandOverRules.cs:57-58`); Daoris's offer is "taken only by the person's press" (`handOverOffer.ts`
  `:24`); D163 rejected automatic hand-overs by default.
- *Consequence:* a repository the person set to `on` for its agents' asks would now have a running line moved to another maker
  by Daoris with no press — a meaning the person never chose.
- *Smallest change:* Daoris's offer of the next choice is taken by the person's press under `ask` **and** `on` (not offered under
  `off`), as today's offer is; or §12's D163 note states the widening and the card that turns `on` says it. The first keeps D163.

#### 3. Not blocking, in order of weight

1. **An effort can be outranked and never said.** §3.2 holds the *model* against the environment; for effort it claims only
   "above every saved effort". My reading of Claude Code's docs (unchecked here) is that an environment effort variable
   (`CLAUDE_CODE_EFFORT_LEVEL`) ranks above every other way, `--effort` included; nothing in this repository names it. And no door
   reports the effort that ran, so *ran* is always *not reported*. Row a should measure the variable's rank; *Details* should say
   *asked high · not reported* plainly, and §0.2 should not read as if effort holds like the model.
2. **Row a's list should grow** by: whether `set_config_option` on `claude-agent-acp` writes the account's `settings.json`
   (`/model` can save; that would break D98's boundary and move every *account default* on the account); whether the offered
   models differ by account (B2); whether `--model` holds on a `--resume` run; when a pipe model refusal arrives (B1); and
   whether `claude-agent-acp` offers the SDK's aliases. If it offers ids, the suggestion's `opus`/`sonnet` are `not-offered` on
   that door: the card must judge each suggested choice by `ActorChoices.Judge` against the offers and leave out what it cannot
   run, so a press cannot leave Build with no choice that ever runs.
3. **`ran` (and B1's `choice`/`asked` update) are set after the open.** `session-record-fields.md` gives such a field its own
   service door (as `help_mode` and `resolved` have), a `PersonDoors.Table` row and its stand-ins; §9.3 says "exactly as
   `profile`", which is set at the open. Name the door in row b. `acknowledged` also arrives after the open.
4. **The opening note's actor line names models and other accounts.** Say which carrier it rides. If any of it reaches the
   record's `Note`/`NoteParts`, `SessionSync` feeds them cleaned only of paths and this record's own profile
   (`Core/Remote/SessionSync.cs:61-67`, `Core/Sessions/SessionNote.cs:28-51`); keep the line on the local conversation, with a
   no-sync test like the four fields'.
5. **Which choice a continuation takes after the actor changed.** Carry-ons, retries, a second opinion's recheck (which walks
   `[first.Adapter]`, `OpinionDeliverer.cs:213`) and the brainstorm's synthesizer should take the record's (or the generator's)
   asked choice, and say so; the hand-over offer's "next choice" is read from the actor as it stands now. `choice` needs a value
   for a handed quest's named agent (`handed`), which is neither 1-4, `pick`, `before` nor `command`.
6. **Helper with no rule list.** `HelperChoice.Candidates` never offers the asker's own family by default (`:24-27`); an actor
   whose choices include it would. Fine as the person's list, but say it, with the *same agent, fresh* label.
7. **`agent-offers` is written by every protocol open, from more than one process** (the loop, conversations, the host
   consoles): name its writer (atomic, merged per key under a lock) and that an older version's reading never replaces a newer
   one. Add `actors` to `driver-config-keeps.json` and the CLI writer's keep table.
8. **`actor_propose`'s revision is undefined.** Say it is a digest of each named actor's list as read, judged per actor at
   *Apply*, so an unrelated actor's change does not stale the card.
9. **The combined model-and-effort option** (O §2.1: a `codex-acp` that offers one option is offered as models with no effort)
   was dropped; keep it for row a's outcome.
10. **A plugin-declared pipe harness has no carrier.** Offer it *account default* only, without the free field.
11. **A person's command that sets `--model` inside a wrapper** cannot be seen; Daoris's appended flag then wins. Say so beside
    *your command*.
12. **The use line counts from `actor.chose` in the daily machine log**; where the log keeps fewer than seven days, the line
    must say the span it read (absent never zero).

#### 4. The names

Against the glossary (`Web/locales/glossary.json`), `names-check.mjs` and `translation-parity.md`:

- **actor / 角色.** Right in both. One care: D203 point 7 shows each co-worker's *role* on the head; were that ever worded, its
  Chinese would also be 角色. Record in the entry that a co-worker's role is named by its actor (第二意见, 助手, 搭档), so 角色 names
  one thing.
- **choice / 选择 — the glossary entry as written would fail `names:check --strict`.** Its `avoid.en` list is checked against
  every label (`names-check.mjs:199-201`): *option* hits *History options* (`projects.git.history.all.more`) and *change a
  conversation's option* (`person.route.SET_SESSION_OPTION`), which are legitimate (the design itself says *option* is the
  agent's config option); *range* hits *No range to read* (`work.review.failed.headline.SESSION_NO_BASE`). Put those reasons in
  `why`, not `avoid`. Its `match` must be narrow (or `pair: false`): 27 English strings say *choice* in its ordinary sense.
- **your pick / 你的选择** collides with *choice* / 选择: a Chinese reader sees "your choice" and cannot tell the person's pick
  outside the order from a choice in it. Use 你指定的 (or 你选定的).
- **choice {{n}} / 第 {{n}} 选择** lacks its measure word; 第 {{n}} 个选择 is five units, at the `status` edge, or 选择 {{n}}.
- **Build / 开发** must not enter as a term with a `\bbuild\b` match: English chrome says *build* 45 times, mostly 构建's build
  output, which would each be held to 开发. Enter it as the actor's name only (an accepted name, or a match on the label).
- **Off.** Intake and Ask Daoris already say what off does — *Off — declarations only* / 关闭——仅按声明, *Off — starters only* /
  关闭——仅入门提示 (`settings.ai.json:11`, `:31`). Keep those on the folded rows rather than a bare *Off* / 关闭.
- **account default / 账户默认, as before / 沿用原设置, Chat / 聊天, Intake / 受理, Second opinion / 第二意见, Helper / 助手,
  Partner / 搭档, Ask Daoris / 问道衍, Brainstorm / 头脑风暴**: right, within their budgets (沿用原设置 at the edge, as §7.1 says).
- **Missing from §7.1:** the opening note's actor line, *Details*' *Actor* / *Model* / *Effort* rows and *asked · acknowledged ·
  ran* and *your command*, and the pass reasons' sentences, in both languages; each is chrome a row must carry with its key kind.

#### 5. Verdict

**Accept after named changes**: B1 to B8, each a change of the design's text the steward can check at the lines named, with
row c's proof, row e's proof and the card's words following them. The shape needs no other round. If the steward settles B1 or
B2 otherwise than the change named (for instance a different way to pass one account for one choice), those two sections are
the walk's contract and want another foundation read.

## Codex, round 2

Codex's read of round 2 at `8b41f7b08`, read-only. Round 3 answers it.

### 1. Each round-1 finding

Verdicts assess round 2’s treatment, not implementation. `Design` means `docs/2026-10-23-actors-design.md`; shortened code paths follow its abbreviations.

| Finding | Verdict | Where | Why |
|---|---|---|---|---|
| Codex 1 — native adapter’s “No model” remark | taken | Design §0.2; `Adapters.cs:2074` | The correction now retains the actual remark. |
| Codex 2 — model windows already recorded | taken | Design §§0.2, 4.1, row k; `ClaudeUsage.cs:106` | History recording is distinguished correctly from rotation and cooling. |
| Codex 3 — today’s model cascade | taken | Design §0.2; D205’s round-2 note | Custom commands, repository settings and environment are included. |
| Codex 4 — empty reviewers versus off | taken | Design §§4.0, 4.4; `OpinionRules.cs:24`, `:179` | Empty inheritance is withdrawn; `false` remains off, with migration cases. |
| Codex 5 — rule-supplied defaults lack provenance | taken | Design §§4.0, 9.3 | `rule-default` supplies a durable source and appears in the preview and explanation. |
| Codex 6 — brainstorm alternatives versus participants | taken | Design §4.4; `DriverBrainstorms.cs:345` | Distinct, frozen voices replace substitution; acceptance wording still needs finding 10 below. |
| Codex 7 — scalar migration changes untouched actors | open | Design §§4.0, 6.4, rows c–e | The first-write projection helps, but clearing an actor reopens inheritance, and readers land too late; findings 7–8. |
| Codex 8 — account-specific offers and invalidation | taken | Design §§3.3, 9.2 | Readings are per account and version, with bounded negative validity. |
| Codex 9 — negotiation reservation and release | open | Design §4.1; `HarnessRoster.cs:1662`, `:1681` | A release exists on paper, but ownership during reselection and an intervening look remain undefined; finding 1. |
| Codex 10 — acknowledgement inferred by setter | answered | Design §3.2; `Acp.cs:527` | Returned and assumed state are separated. Proceeding after an empty successful answer is a defensible availability choice when labelled *not reported*. |
| Codex 11 — effort settings fallback loses precedence | taken | Design §3.2, row a | The file fallback is withdrawn; unsupported effort is disclosed before dispatch. |
| Codex 12 — composer override cannot be reconstructed | open | Design §4.2; `ChatRunner.cs:1699` | Structured values fix display-name reconstruction, but a model change can invalidate the retained effort; finding 5. |
| Codex 13 — reported-tier information lost | taken | Design §§5, 9.3 | Names are collected before token attribution; overflow is explicit; configuration changes have separate entries. |
| Codex 14 — brainstorm provenance has no home | taken | Design §5, row b; `DriverBrainstorms.cs:634` | Local pass records, including retries, receive the provenance and their reader/display chain. |
| Codex 15 — confirmation protects only managed requests | open | Design §8.2 | The acknowledged full-access limitation is honest, but the new confirmed-copy mechanism needs an exact approval and write contract; finding 6. |
| Opus B1 — refusal occurs after opening the record | open | Design §3.3 | Ending incompatible records is sound; reservation transfer, default restoration and conversation retry remain incomplete; findings 1–3. |
| Opus B2 — offers and refusals are account-specific | taken | Design §§3.3, 4.1, 9.2 | The scope and choice-major account exhaustion are corrected. |
| Opus B3 — unsupported permission posture starves choices | wrong | Design §4.1; `Help/HelpMode.cs:83` | The driven pre-pass is right; the added claim that restricted Ask Daoris requires an asking mapping contradicts the code; finding 9. |
| Opus B4 — migration and old writers | open | Design §§4.0, 6.4, 8.2 | Old doors are named, but preservation after clearing, rollout order and confirmed writes remain open. |
| Opus B5 — later native runs lose the model | taken | Design §3.2; `DriverContinuations.cs:899`, `:1031` | Targets carry the choice through all three prepares; retries reuse the prepared start. |
| Opus B6 — nonexistent empty-reviewer inheritance | taken | Design §§4.0, 4.4 | Reviewers remain named; their actor supplies model alternatives without enabling opinions. |
| Opus B7 — voices contradict fallback acceptance words | open | Design §§4.4, 6.3, 7.1 | Voice semantics are explicit, but the actual acceptance paragraph still promises fallback-only execution; finding 10. |
| Opus B8 — widening `handOver: on` | taken | Design §4.2; `handOverOffer.ts:24` | Daoris’s offer remains the person’s press under every mode. |

### 2. Findings introduced by round 2

1. **Blocking — reselection has no defined ownership of the existing reservation.**  
   `Design:279` re-asks the counting walk while the opened process remains alive; `Design:382` releases on negotiation failure. Keeping the original reservation biases that walk: `HarnessRoster.cs:1687` includes provisional starts in `Running`, and `AccountRotation.cs:541` ranks by that count. Releasing it first removes the protection against a concurrent account probe while the process still holds the account (`HarnessRoster.cs:729`). A second selection also creates another reservation, while `Unchose` only removes the original provisional number; a look can already have moved that number into record-based accounting (`:1651`).  
   **Smallest change:** define reselection as an operation on the existing reservation: exclude its own load when judging alternatives, retain it during same-account negotiation, and stop the old process before transferring or releasing it. Define its handoff to record-based accounting. Add cases with goal ordering, concurrent probes and a look during negotiation.

2. **Blocking — reusing an ACP session does not restore account default.**  
   `Design:279` permits setting again on the same open session, while §3.1 defines account default as passing nothing. Suppose `session/new` starts on `m0`; setting `m1` returns contradictory current state `m2`, which round 2 correctly treats as refusal; the next choice names no model. Sending nothing leaves `m2`, not `m0`. `Acp.cs:533` keeps the returned current state, and there is no reset operation in this mechanism.  
   **Smallest change:** reuse the session only when the next choice can establish its complete effective configuration. For account-default values after a potentially mutating attempt, restore a measured opening default or reopen the session. Test the contradictory-answer-to-default case.

3. **Blocking — “the next look” is not a conversation fallback mechanism.**  
   `Design:282` ends the record and promises that a conversation keeps its words and proceeds at the next look. Conversations start in `ChatRunner.StartAsync` (`ChatRunner.cs:390`), independently of driven planning. Their existing go-on requires an ended chat with waiting words and resumes its recorded adapter (`ChatGoOn.cs:340`, `:372`); it does not take the next actor choice. Saying the words are kept does not identify who schedules the replacement or distinguishes negotiation from an ordinary continuation.  
   **Smallest change:** give the chat runner an explicit pending-start retry path, carrying the effective order, passed accounts and queued words until dispatch or exhaustion. Define recovery after interruption and separation from ordinary go-on. Prove that one press survives refusal on account 1 and starts on account 2 without another word or press.

4. **Blocking — `asked` uses a real model name as its account-default sentinel.**  
   `Design:780` allows either a model value or the string `default`. But `AgentSettings.Models` explicitly includes the tool’s model alias `"default"` (`Accounts/AgentSettings.cs:58`), also present in the ACP fixture (`AcpTests.cs:2379`). An explicit request for that alias and an omitted model therefore have indistinguishable provenance, although one must carry a flag/setter and the other must leave the cascade alone. Replacing requested effort with `not-carried` also loses the `high` needed for the promised *asked high · not carried* display.  
   **Smallest change:** use tagged values, separating requested value, source and carrier status. Keep an explicit `"default"` model distinct from an absent model, and retain unsupported requested values. Test both through recording and resume.

5. **Blocking — merging composer overrides can resurrect an obsolete effort.**  
   `Design:416` stores each changed category and merges it into the previous override. An effort-only change should preserve the model, but the inverse is unsafe: changing the model can change both the effort menu and its current value, which `Acp.cs:516` explicitly documents. An earlier `{model: A, effort: high}` followed by a model change whose returned state is `B/low` can be persisted as `B/high`; resume then changes what the person left selected.  
   **Smallest change:** after a successful model change, reconcile both categories from the returned configuration, clearing or replacing dependent effort overrides. Preserve the model on effort-only changes. Add a model-change/reset-effort case alongside the current effort-only case.

6. **Blocking — the confirmed copy lacks an exact approval and commit contract.**  
   `Design:723` introduces two files and says *Apply confirms the file’s* choices. It does not bind that press to a displayed snapshot, specify what happens if the file changes before Apply, or say whether saving one actor confirms unrelated pending edits. This matters with the existing writer: `DriverConfig.Save` rereads disk and preserves concurrent changes (`DriverConfig.cs:914`, `:923`). Copying that merged configuration into `actors-confirmed` could confirm an unrelated edit. The hand-edit card also has no specified before/after choices, unlike `actor_propose`.  
   **Smallest change:** make the confirmed snapshot authoritative. Bind the hand-edit card to its displayed actor changes and revision; reject stale Apply/Put back. Apply an ordinary Save only as its reviewed patch over confirmed state, preserving unrelated pending edits as unconfirmed. Define the shared lock, write ordering and recovery for both files, and ensure all start-time fallback readers receive the confirmed view. Prove concurrent edits and failure between writes.

### 3. Remaining findings and proof gaps

7. **Blocking — clearing an actor defeats first-write preservation.**  
   `Design:347` freezes dependent actors only on the write that first sets one of Build, Chat or Brainstorm. Yet `Design:698` and `:806` support clearing an actor back to absence. After migration, clear Chat; then change an already-set Build. Chat again reads the written-through `adapter` and moves with Build, although the second change names only Build. The statement at `Design:349` that no actor reads `adapter` after the first write is consequently false.  
   **Smallest change:** preserve every currently inherited dependent actor before every compatibility-scalar change, including after a clear, or redefine clear so it cannot recreate that live dependency. Test set → clear Chat → change Build.

8. **Blocking — row c’s migration guarantee depends on readers scheduled for rows d and e.**  
   `Design:903` writes Build through to `adapter` and materializes Chat and Brainstorm, but Chat starts taking its actor in row d (`:904`) and Brainstorm in row e (`:905`). Until then, their actual readers still use `config.Adapter` (`ConversationRoutes.cs:48`; `DriverBrainstorms.cs:348`). Writing their future actor entries does not preserve their behavior in the independently landing row c. The existing setting-proposal adapter writer is also not migrated until row d.  
   **Smallest change:** move all shared-scalar compatibility readers and writers into row c’s boundary. Later rows can add model behavior and editors. Row c’s proof must exercise actual Chat and Brainstorm starts and the legacy proposal door after changing Build, rather than only `Effective`’s table.

9. **Blocking — restricted Ask Daoris is assigned a new unsupported-posture refusal.**  
   `Design:393` says restricted access needs the asking posture and attributes that to D183 point 3. `HelpAccess.For` instead permits restricted starts without inspecting `AskingPosture` (`Help/HelpMode.cs:83`); it requests `HelpRoom.Posture` where offered. Only full access requires a mapped `FullPosture` (`:98`). D183 point 3 records and discloses the mode; it does not establish the new condition. A restricted configuration that runs today can therefore stop running during the supposedly unchanged upgrade.  
   **Smallest change:** judge Ask Daoris’s pre-pass using `HelpAccess.For(...).Refusal`, and driven starts using `DrivenAccess.For(...).Refusal`. Add an as-before restricted start on a harness without an asking mapping.

10. **Blocking — the acceptance paragraph still describes Brainstorm’s additional voice as a fallback.**  
    Round 2 correctly says Brainstorm runs its choices together (`Design:452`, `:679`). Nevertheless, the actual acceptance paragraph at `Design:562` still says Daoris tries a later choice only when an earlier one cannot start. The section body at `:607` repeats that universal claim. Counting an added voice elsewhere on the card does not make this sentence true. This is the unresolved acceptance part of Opus B7.  
    **Smallest change:** make the acceptance text actor-specific: ordered actors exhaust earlier choices’ accounts; Brainstorm runs the listed voices together and identifies the added voices. Apply the distinction to both languages and the section body; test a card changing Build and Brainstorm together.

11. **Non-blocking — rollback loses more than the model.**  
    `Design:579` says an older build starts the same agent and rollback loses only the model. An explicit Chat agent can differ from Build, while the older Chat reader uses Build’s written-through `adapter` (`ConversationRoutes.cs:48`). Brainstorm also loses additional voices; ordered alternatives and effort are unavailable.  
    **Smallest change:** disclose the actual compatibility projection: older builds use the legacy scalar agents and original rules, losing actor-specific agent separation, alternatives, voices, model and effort. Make the older-build fixtures demonstrate that projection.

**11 findings; 10 blocking.**

**Verdict: another round.** The eight actors, named rule fences, per-account readings, session-level carriers and local tier provenance remain sound foundations. Round 2 closes many original defects, but reservation transfer, same-session defaults, conversation fallback, override restoration and the new confirmation store still need a consistent operational contract. Revise those mechanisms and land the compatibility readers with their writers; the next read should check those boundaries and their failure cases.

## Opus (foundation), round 2

The independent Opus reader's foundation read of round 2, read-only. Round 3 answers it.

### Opus's foundation read of MODELROLE1's design (D205, round 2)

*An independent Opus 5.5 reader, in Fable's place under D191's amendment of 2026-10-23. Read at `8b41f7b08`, the design's
branch at round 2. Round 2's own change against `3d3045848` is documents only (the design, D205's note and their records);
the code in that range is merged TREELOCK2 and MAKERTRAIL1 work, which this design does not touch. Read whole:
`docs/2026-10-23-actors-reads.md`, `docs/2026-10-23-actors-design.md`, `docs/decisions/D205.md` with its note, and
`git diff 3d3045848 HEAD` of the last two. Also loaded: `.claude/knowledge/session-record-fields.md`, D139 point 2, D176
point 2, D194 point 5, and the code at the lines below. Nothing was built, run or fetched, and Claude Code's documentation is
not checked here. Paths use the design's abbreviations (`Driver/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/`
= `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` = `src/Daoris.Web/src/`). Design lines are cited as `design:N`.*

Round 2 answers both reads honestly and mostly well. I would keep these parts as written: one effective order behind
`ActorChoices.Effective`, per-account readings, a per-start passed set, `no-posture`, carriers on every native prepare,
names kept before `TurnModels` touches them, and Brainstorm's frozen voices. The narrowing of B1 is right. It keeps B2's
order, and its rule, *the walk answers as if the refusal had been known before the spawn*, states the walk better than my
round-1 B1(a) did.

Six things remain open:

- The paths after the open are written for a driven first start only, so continuations and conversations have none.
- The confirmed copy misses four write and read paths.
- The update drain loses its way to stop intakes.
- The reservation names a mechanism the count does not have.
- A Build whose choices use both doors can fall onto the door that cannot take its quest.
- Brainstorm's card still uses the sentence B7 objected to.

#### 1. Round 1's blocking findings, checked

##### 1.1 The Opus read's B1-B8

| # | Round 2 | Against the code | Standing |
|---|---|---|---|
| B1 | Taken, narrowed (design:262-291) | It holds for a driven first start. The record opens at `Driver.cs:2104-2112`, before `Prepare` at `:2320`. The options are first seen in `AcpSession.OpenAsync` (`Acp.cs:911-945`). Before the first prompt no take is possible, so the quest is still `Open`, and the next look plans it as a first start (`Planner.cs:1311-1321`). | Closed for first starts. Open for continuations and conversations (R2-B1) and for the reservation (R2-B4) |
| B2 | Taken as named (design:269-274, :377-379, :762-767) | The set fits `Pass` beside `WalkMemory.Unknown` (`HarnessRoster.cs:1304-1309`, `:1360-1365`). Each account has its own home (`:1244-1245`). | Closed inside the walk. Open where an account is chosen outside it (the curve move, R2-B1) |
| B3 | Taken: `no-posture` (design:389-394) | `DrivenAccess.cs:104-111`. The hold after the walk is at `Driver.cs:1764-1775`, and Ask Daoris's refusal at `ChatRunner.cs:600-602`. | Closed. Its twin for the connector is R2-B5 |
| B4 | Taken (design:345-353, :581-590, :717-727) | (a) holds for the readers of `adapter` that start an actor (`Driver.cs:1672`, `ConversationRoutes.cs:48`, `DriverBrainstorms.cs:348`). (b) and (c) hold for the doors named. | Closed for (a)-(c). The hand-edit half is R2-B2, the drain R2-B3, and the doors' twins N10 |
| B5 | Taken (design:239-245) | `GoOnStart` prepares through `PrepareResume(target, …)` (`DriverContinuations.cs:899-907`). `AgainAsync` reuses its run's `info` (`:1031-1040`). `PrepareChat` takes the `ChatTarget` (`Adapters.cs:2172`). | Closed |
| B6 | Taken (design:335, :445-447) | `OpinionRules.cs:23-27`, `:140`, `:178-179` | Closed |
| B7 | Taken, voices (design:452-460, §7.1 rows) | The row and the editor say *all at once*. The card's quoted words do not. | Half closed: R2-B6 |
| B8 | Taken as named (design:404-409) | `HandOverRules.cs:57-58`, `handOverOffer.ts:24` | Closed |

##### 1.2 Codex's 4-15

| # | Round 2 | Standing |
|---|---|---|
| 4 | Taken: the empty-reviewers branch is withdrawn; `false` stays off; migration fixtures | Closed |
| 5 | Taken: `rule-default`, shown in the preview and the note | Closed |
| 6 | Taken: voices are planned and frozen; a voice that cannot run is dropped, never replaced; one entry per agent | Closed. Since nothing is ever substituted, no maker outside D176 point 2's per-maker showing receives the packet |
| 7 | Taken: the first write makes the other actors explicit; legacy writers named | Closed for Build, Chat and Brainstorm. The drain is R2-B3 and hand edits R2-B2 |
| 8 | Taken: per account, a day's validity, refreshed by the open | Closed inside the walk. The curve move and the resume are R2-B1 |
| 9 | Taken in aim | The mechanism does not match the count: R2-B4 |
| 10 | Answered: when no list is returned, the work goes on and is shown *not reported* | The reason holds. *ran* is the agent's word on every turn, *Details* shows both, and holding would strand an agent that never returns its list. Row a should measure what an accepted change returns (N5) |
| 11 | Taken: no file fallback for effort | Closed |
| 12 | Taken: `override` stored as category and value id | Closed (the protocol door only, which is where the composer works) |
| 13 | Taken: names kept before `TurnModels`, up to eight, with `more` | Closed for names. The `entries` list is unbounded (N6) |
| 14 | Taken: pass records carry the shapes, in row b | Closed |
| 15 | Taken as Codex's second option, an authority boundary | Open on four paths: R2-B2 |

##### 1.3 B1 and B2: the walk's contract

Round 2 settled B1 differently from the change I named. I judge the difference right, and I checked it against the code.
Take Build with choice 1 = Claude Code · opus and choice 2 = Claude Code · sonnet, and suppose account-1 refuses opus at
the open:

- If account-2 is ready, the re-asked walk answers choice 1 on account-2. The record ends, and the next look starts that,
  because account-1's kept reading passes it.
- Only where account-2 is cooling or passed does the answer come back to the same agent on the same account. Then sonnet is
  set on the open session.

My B1(a) would have taken sonnet on account-1 in the first case too, against B2. Each part of the contract:

- **A refused model once the record is open.** This holds for a driven first start, and only there. A resume, a go-on run, a
  carry-on, the curve move and a conversation are also opens, and none of them can take the stated path. A resume cannot
  change account; a conversation has no next look; a pipe chat's prompt is on stdin. This is R2-B1.
- **Per-account offers and refusals.** Keyed by agent, version and account, a day old at most, and refreshed by the open,
  they hold. §4.1's pseudo-code judges the readings *inside* the walk (design:366-367). Say so in words, so that every
  caller of `WalkAsync` gets them: `SelectHandedAsync`, `CarriedOnAsync` and `ResumeAsync`'s own-account pass
  (`HarnessRoster.cs:895-905`, `:968`, `:991-995`). The curve move chooses accounts without the walk (R2-B1).
- **Passing accounts within a choice for one start.** The per-start `passed` set is the right seam, and it is a start's
  alone. Once the record ends, only the kept reading carries the pass to the next look. So the reading must be written
  before the record ends (N4). The reservation around the set is R2-B4.
- **The pipe door's exception.** It holds where the prompt is in the arguments (`Adapters.cs:2092`, `:2127`). A model
  refusal on a silent turn did no work, and ending the part with no strike, keeping the refusal, is right. It has no
  counterpart for a pipe chat, whose words go on stdin (`Adapters.cs:2181`), or for a go-on run, where TURNFAIL2 hands the
  words back (`DriverContinuations.cs:934-940`). Both are in R2-B1.

So the walk's contract is right for first starts and **missing, not wrong**, for everything else.

#### 2. What round 2 introduced, read as a foundation

**The effective order (§4.0).** This is sound. There is one function and three stored states (*as before*, *off*, *set*).
Fences decide who runs; the actor decides the order only where no fence names agents, and always decides the model.
`rule-default` and `handed` give every run an index. Three things need saying:

- Helper's order is D155's maker passes, not the actor's order (N1).
- A pick or a handed agent whose account refuses the model has no stated path (N2).
- The order a start walks should be read once for that start (N3).

The upgrade path holds: absent keys read as today, and the first write makes Chat and Brainstorm explicit.

**The confirmed copy (§8.2).** Starts read the last confirmed actors, and a hand edit waits for a card. This holds against an
edit made with no intent to forge, and the design states its limit honestly. But it is narrower than it reads. Every other
key that a full-access shell reaches in the same file stays unconfirmed: an access rule's `unrestricted`, `cap`, the holds.
It also adds a second source of start-time truth that every reader must choose between. As written, it is open on four paths
(R2-B2).

**The carriers on every run (§3.2).** These hold, as B5 above.

**The tier that ran (§5, §9.3).** This holds:

- `ran` comes from the reported names as the door gave them (`Acp.cs:2217`, `:1384`).
- `mixed` stays a token fact (`TurnModels.cs:69-80`).
- Fields set after the open use the later door that `session-record-fields.md` describes.
- Brainstorm's pass records carry the same shapes.

Bound `entries` the way `models` is bounded (N6).

**Brainstorm's voices (§4.4).** These hold: one entry per agent, at most `MostAgents`, frozen, dropped and never replaced. The
request's and the policy's agents still come first, as at `DriverBrainstorms.cs:348`. The card's words are the gap (R2-B6).

**The migration of the old doors (§6.4).** This holds for the doors named:

- `driver adapter` writes with no route today (`DriverChoicesCommand.cs:528-536`).
- `setting_propose`'s three doors are at `HelpSettingProposals.cs:34-38`, `:195-207` and `:255-263`.

Three readers that predate actors are missed:

- The update drain turns intakes off by nulling a key the actor replaces (R2-B3).
- The planner decides whether an unadopted repository can be driven from `adapter`'s single door (R2-B5).
- Some twins of the retired doors are not named (N10).

**Concurrency with a running session.** Running work keeps its record's `asked` and `override`. That holds through
`ResumeAsync`, `GoOnStart`, `AgainAsync` and the composer's change. `agent-offers` has a stated multi-writer rule. Two gaps:
a Save made while a hand edit waits can confirm that edit (R2-B2(c)), and a start in flight should keep the order it began
with (N3).

#### 3. Blocking findings

**R2-B1. The paths after the open cover a driven first start only. Continuations and conversations have none.**

- *The design:* design:275-291 gives the after-the-open paths, ending "the next look starts what the walk now answers. A
  conversation keeps the person's words and says so; its press is not lost". design:412-413 says "A resume re-applies the
  record's asked choice". design:402-403 says a carry-on keeps "the same choice re-applied at the open". design:421-423 says
  "A continuation takes its record's asked choice".
- *The code:* five opens do not fit those paths.
  - **A resume** runs on its record's own account, never one the walk picks, because its conversation lives in that
    account's home (`HarnessRoster.cs:907-971`, `ChooseAsync(chosen: named)` at `:968`).
    - A refusal there cannot "end the record". That record already holds work and the person's words.
    - Nor can the start move to another account. If the reading is judged inside the walk, the resume holds every look
      until the reading expires, then is refused again.
  - **A pipe go-on run** fails as a turn (`DriverContinuations.cs:934-940`) and returns to that resume.
  - **The curve move** shifts a running chat to another account of its agent at the end of a turn, by the hand-over's box.
    No press is involved and the walk is not used (`CurveMove.cs:62-127`). Its candidates are judged ready without any
    model (`HarnessRoster.cs:1974-1994`). It can therefore take an account that refuses the chat's model, after ending the
    old conversation.
  - **A conversation** opens its record before it spawns (`ChatRunner.cs:424-499`; Ask Daoris `:606-620`), and no look
    follows. Nothing names who opens the walk's answer, or what the window shows meanwhile. A pipe chat's first message
    goes on stdin (`Adapters.cs:2181`), so the pipe exception's "next look" has no counterpart there.
  - **A carry-on's new record** that ends `choice-not-offered` leaves a `Taken` quest. `Plans` plans such a quest only by
    its last record's state (`Planner.cs:1377-1389`). An end the planner does not know leaves the quest unplanned and
    unsaid. A known end makes `CarriedOnFrom` carry on from the empty record (`:1341-1343`).
- *Smallest change:* one paragraph in §3.3, *After the open, for what is not a first start*, and its rows in §13.
  - (a) **A continuation keeps its asked choice.** This covers a resume, a go-on, a carry-on, a briefed move, a hand-over
    to another account of the same agent, and the curve move.
    - It walks only that choice's accounts, each judged by its reading. It never takes the actor's next entry; D163 says a
      running line never moves to another choice.
    - `SwitchCandidates` judges each candidate's reading for the record's `asked` (and `override`) model. A candidate that
      does not offer it is not ready, so the curve move never takes it.
    - A resume whose own account refuses goes the way a resume whose account cannot carry it goes today: through
      `ElsewhereAsync` (`HarnessRoster.cs:964`, `:973-980`), its words carried to a new record by D175 point 3. The old
      record is left as it was and never ended `choice-not-offered`.
    - With no account left: the wait's sentence, and D163's offer of the next choice, as for an agent whose accounts are
      all cooling (§4.2).
  - (b) **A conversation re-asks within the same press.**
    - `StartAsync` and `PlaceHelpAsync` end the refused record `choice-not-offered` and open the walk's answer in the same
      call. The window follows the new record, and its note says both.
    - With nothing left, the press is refused in the wait's sentence (§4.5).
    - A pipe chat's measured refusal on its first turn does the same, carrying the person's first message.
  - (c) **`choice-not-offered` is an end the planner reads.**
    - Name its state, or a code on `failed`, so that `Plans` plans it and strikes leave it out.
    - `LastRun` skips such a record, so a carry-on is planned from the record it continued.
    - Its fresh tree is let go as a session that never ran (the path at `Driver.cs:2113-2117`).
  - Tests:
    - A resume on an account that lost the model: carried to a new record, the old one untouched.
    - A curve move with one candidate not offering the model: that candidate not taken.
    - A Chat whose first entry is refused at the open: one press, one running conversation, two records.
    - A carry-on whose record ended `choice-not-offered`: planned again from the record before it.

**R2-B2. The confirmed copy misses four paths.**

- *The design:* design:722-727. "starts read the **last confirmed** actors". The copy is "written only where a `SET_ACTOR`
  or a Save is applied, holding the confirmed `actors` and the three keys". design:720-721 adds "No door writes them
  otherwise".
- *The code:*
  - (a) **The terminal acting alone.** When no instance runs, or a headless loop holds the home, a verb acts alone and
    writes `driver.json` itself (`DriverChoicesCommand.cs:292-321`; `InstanceDoor.cs:8-12`). With nothing running, that
    terminal is the person's door (D194 point 5; a session's terminal is refused at `:294-298`).
    - If it writes no copy, the person's own command is held as a hand edit.
    - Under a headless loop (the family rehearsal, a server machine) there is no window, so the card never shows and the
      edit is held for good.
  - (b) **A card's *Apply*.** This is the person's press too. It covers `actor_propose`, and `setting_propose`'s
    `intake`/`helper`/`adapter` doors until row h, which row d keeps writing. But it is neither `SET_ACTOR` nor the Save
    that §8.2 lists.
  - (c) **A Save can confirm another actor's pending hand edit.** `WithActor` changes the file as it stands under the keep
    lock (`DriverConfig.cs:914-923`). A copy "holding the confirmed `actors`", written from that file, confirms whatever
    hand edit was waiting there.
  - (d) **The readers.** "Starts read the copy" has to reach every reader of the keys that starts work. Those are:
    - `Driver.cs:903`, `:1672`, `:909`
    - `DriverIntakes.cs:58`, `:111`
    - `HelpRoutes.cs:52`, `:1265`
    - `ConversationRoutes.cs:48`, `:164`
    - `SessionsVerbs.cs:33`
    - `DriverBrainstorms.cs:348`
    - `BriefedMove.cs:111`, `ChatGoOn.cs:372`, `OpinionGateLook.cs:368`

    Today each reads `DriverConfig` directly. One that keeps doing so runs a held edit.
- *Smallest change:* either of two, both checkable without another read.
  - (i) **Keep the copy and name the four paths in §8.2:**
    - The alone path writes both files.
    - Under a headless loop, a held edit is said in the look's report, and `daoris driver actor --apply | --put-back` is
      the card's terminal twin.
    - Every card's *Apply* that writes these keys writes the copy.
    - A write confirms only the actors and keys it names, and the copy keeps its others.
    - One accessor serves the confirmed actors and keys to the readers listed. The editor shows both versions where they
      differ.
  - (ii) **Take Codex 15's first option.**
    - D205 point 9 holds for Daoris's own doors, every one through `SET_ACTOR` or the Save, as §6.4's table already makes
      them.
    - A raw edit of `driver.json` is D183's unenforced shell, as it is for every other key in that file.
    - Drop the copy, the card and the home entry.

  (ii) is smaller, and it does not make one family of keys in `driver.json` the only one guarded.

**R2-B3. The update drain stops intakes by nulling a key the actor replaces.**

- *The design:* design:577-579 writes `intakeAdapter` through, and Intake is read by `Effective` (§4.0); with the copy, from
  the confirmed keys.
- *The code:* a draining look plans with `InstallUpdate.Drained(config)` (`DriverWatch.cs:604`). That sets
  `IntakeAdapter = null` in memory (`InstallUpdate.cs:49-53`), which is how D139 point 2's "intakes too" holds: the gate is
  `DriverIntakes.cs:58`. An Intake actor that is *set*, read from `actors` or from the copy, escapes this, and intakes start
  while the app drains for an update.
- *Smallest change:* in §6.4, the drain sets Intake *off* in what `Effective` reads, not `intakeAdapter` alone. Test: a
  draining look with Intake *set* starts no intake.

**R2-B4. The reservation names a mechanism the count does not have.**

- *The design:* design:380-386: "The selection becomes a reservation counted as today, with `Release` (the same `Unchose`)
  … and `Commit` at the first prompt." Its tests promise "no distorted rotation".
- *The code:* a count is an entry in `_chosen` until the next look whose mark covers it. After that, the count is the record
  itself (`Look`, `HarnessRoster.cs:1651-1659`; `Chose`/`Unchose`, `:1662-1675`). A look "begins only once the last look's
  starts have opened their records", and the look waits only for the open (`Driver.cs:891-897`). So:
  - Nothing exists for *Commit* to do.
  - After the open, `Unchose` is usually a no-op. The release is the record's end.
  - The re-ask at the open is a pass under the gate, which counts a second time for the same start (`:1193`, `:1415`).
  - A record ended `choice-not-offered` still feeds `LastStarted` (`:1692`). The least-recent step then reads the refusing
    account as just used (`AccountRotation.cs:555-575`).
  - Today's holds after the walk do not `Unchose` (`Driver.cs:1775`, `:1796`).
- *Smallest change:* restate the second bullet of §4.1 in the count's own terms:
  - Before the record opens, a release is `Unchose`, including the holds after the walk at `Driver.cs:1775` and `:1796`.
  - After the open, the release is the record's end, and `Facts` (`:1688-1693`) leaves a `choice-not-offered` record out
    of `Running` and `LastStarted`.
  - The re-ask carries the open's own count in. A same-account answer keeps that count; any other answer releases it.
    The re-ask never takes a second count.
  - Drop *Commit*.

  §4.1's tests stand as written.

**R2-B5. A Build that mixes doors can fall onto the door that cannot take its quest.**

- *The design:* the per-entry pass codes (design:365, :389-394) and the write-through of `adapter` (design:345-353).
- *The code:*
  - The planner admits an unadopted repository only when the machine's single door is the protocol door
    (`Planner.cs:1729-1753`). Only that door hands the session the connector it takes its quest with: the pipe door gets
    the plugins' servers only (`SpawnServers.cs:55-67`, `Driver.cs:2202-2205`; the connector is added on the protocol door
    alone at `:3760-3771`).
  - That door is `config.Adapter`'s wire (`Driver.cs:618-628`), which becomes Build's first agent under write-through. So
    an entry on the pipe door can run in an unadopted repository: a whole session spent that cannot take or close its
    quest, then a strike.
  - The reverse case refuses a repository that a later entry could drive.
- *Smallest change:* a `no-connector` pass, judged per entry before the walk as `no-posture` is, from the repository's
  adoption and the entry's wire. The planner's gate asks whether *any* entry can drive the repository, not `Door()` alone,
  and the workspace set-up's use of `Door()` (`Driver.cs:1312`) does the same. Test: an unadopted repository, with Build's
  first entry cooling on the protocol door and its second on the pipe door, waits rather than spawning on the pipe.

**R2-B6. Brainstorm's card still says what B7 objected to.**

- *The design:* design:562-566 is the card's quoted acceptance sentence, the press that is D191 point 3's yes: "Daoris tries
  a later choice … only when an earlier one cannot start". §6.2's Brainstorm row adds a second voice (design:546).
  design:459-460 and design:964 promise that "the card counts the voices it adds", but neither §6.3 nor §7.1 has the words.
- *The code:* with nothing named, a brainstorm today runs one voice (`DriverBrainstorms.cs:348`).
- *Smallest change:*
  - In §6.3 and §7.1, give Brainstorm's before → after row its words, in both languages: *Brainstorm: two voices at once,
    where one ran before; this spends more* / *头脑风暴：同时运行两方，原来是一方；用量更多*.
  - Scope the general sentence to "every other actor".

#### 4. Not blocking, in order of weight

1. **Helper's order follows D155's maker passes, not the actor's order.** design:441 lists "Helper with no rule list" among
   the actors whose entries are the order. But `HelperChoice.ChooseAsync` walks the candidates through `ReviewerChoice`'s
   passes (`HelperChoice.cs:30-37`, `ReviewerChoice.cs:175-180`, `:215-226`): another maker than the asker's comes first.
   Say this in §4.0 and §4.4. The preview should say *another maker than the asker's first*, since the asker is known only
   at the ask.
2. **A pick or a handed agent whose account refuses the model.** A pick is "the one account, never moved" (design:342-343;
   `HarnessRoster.cs:1065`, `:1293`). Name what runs: that agent's next entry on that account, or a refused pick with the
   reason.
3. **The order a start walks is read once for that start.** A `SET_ACTOR` that lands between the walk and the re-ask at the
   open should not change the order the re-ask uses.
4. **The readings.**
   - Key `refused` by model.
   - A door that rejects `set_config_option` as a method is an agent-and-version reading, not one per account. Otherwise
     every account opens and ends a record once a day.
   - Write a reading before the refused record ends.
5. **Acknowledgement.** Row a should measure whether an accepted `set_config_option` returns the requested id as the current
   value. Until it does, a differing current value counts as a refusal only where it is another offered model, so that a
   normalised id does not pass an account.
6. **`ran.entries` is unbounded.** A conversation with many composer changes grows it on every change. Bound it as `models`
   is, with a count.
7. **If the copy stays (R2-B2(i)):**
   - A malformed hand edit is said in the card with `Judge`'s error and *Apply* refused. It does not hold starts as design:596
   says, since starts read the copy.
   - A copy that has gone missing after its first seeding is an unconfirmed change, not a new seed.
   - Its D195 class is `driver.json`'s.
8. **The hand-edit card's place.** It asks for a press, so name its screen and its classification under
   `.claude/knowledge/person-waits.md`. §4.5 adds no kind to *What needs you*.
9. **What a rollback and the old verb now do.**
   - An older build reads `adapter`, which is Build's first agent, for Chat and the brainstorm's fallback too
     (`ConversationRoutes.cs:48`, `DriverBrainstorms.cs:348`). A rollback can therefore change Chat's agent, not only its
     model, and the footnote should say so.
   - `driver adapter` used to move both Build and Chat. Its sentence should say that Chat stays on its agent.
10. **The retired doors' twins.**
    - The service's `HelpSettingProposalWriter.Doors` (`HelpSettingProposals.cs:27`).
    - `PipeWording`'s `SET_INTAKE` and `SET_HELPER` rows (`PipeWording.cs:188-189`).
    - The web's `useSetIntake` and `useSetHelper` (`Web/bridge/driver.ts:463`, `:486`).
    - The room's machine facts, which read the three keys (`HelpRoomMachineNow.cs:15-17`).
11. **Stale sentences carried over from round 1:**
    - design:209 says "reused as it is", while design:247-249 says "with one change".
    - design:98 says "the code already enforces it", which §8.2 now contradicts.
    - design:596 and §9.2 mention "the CLI writer's keep table". That writer left in PUSH1f2b (`jsonkeep.ts:8`), and my
      round-1 N7 was wrong to name it.
    - design:841's D24 note says "every actor works at *account default*".
12. **The opinion wire's same-agent display** judges against `config.Adapter` alone (`LinesRoutes.cs:155`,
    `DriverRoutes.cs:620-622`, `HelpSettingProposals.cs:730`). When Build runs on more than one agent, either say so or
    judge against Build's agents.
13. **A voice's bounded restart** on its agent's next account (`DriverBrainstorms.cs:603-609`) judges that account's reading,
    as the walk does.

The names were taken as round 1's §4 named them. I spot-checked §2.1 and §7.1 and did not run them against `names-check.mjs`.

#### 5. Verdict

**Accept after the changes named in R2-B1 to R2-B6.** Each is a change to the design's text at the lines named, with the
tests each names, and the steward can check each at those lines. The shape needs no other round, and neither does the walk's
contract for a first start.

If R2-B1 is settled differently from the change named, that is the walk's contract again and wants another foundation read.
An example would be a continuation taking the actor's next entry rather than holding its asked choice, or a resume ending its
record. Either option in R2-B2 is checkable without one.

**6 blocking (R2-B1 to R2-B6); 13 not blocking.**
