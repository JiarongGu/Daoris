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
