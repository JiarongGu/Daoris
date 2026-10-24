# STEWARDROLE1: the reads of round 1 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-steward-role-design.md` and D209 at `48f9b3cff`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on the design's own tree. Codex asks another round; the Opus foundation reader would accept after named changes (20 findings, 7 blocking). Round 2 answers both.

## Codex

Codex's read of round 1.

### 1. Claims about the code

The central account is accurate: D208’s appointment door is unbuilt; D201’s standing confirmation is built but unoffered; ordinary confirmed words and hand-overs carry personal attribution; and the working steward runs branch code.

The incorrect or incomplete claims concern the hand-over approval payload, D201’s treatment of casing, tree-setting effects, the origin of index commands, a finish’s consequences, and the purported reversal of `done`. Findings 4 and 10–14 identify them.

References below use these source roots:

- `Driver/`: `src/Daoris.Desktop/Daoris.Desktop.Driver/`
- `Modules/`: `src/Daoris.Desktop/Daoris.Desktop.Modules/`
- `Core/`: `src/Daoris.Service/Daoris.Service.Core/`
- `Design`: `docs/2026-10-24-steward-role-design.md`

### 2. The mechanism’s holes

The appointment foundation can serve both roles: window-only appointment, independent generations and scopes, a shared dedicated holder, memory-only signing material, dormant records after restart, and separate revocation. Retiring D201’s unkeyed grant avoids a second authorization path with personal attribution.

The environment separation also addresses the named process threats within its stated limits. A Daoris agent, an escaped terminal process, or a hook run by the working steward receives no key. Reading another process’s memory, injection, and a compromised holder remain explicitly outside that protection.

The weaknesses arise **after admission**. Existing hand-over files can acquire the new role attribution without authenticated provenance. Route-specific overrules permit another management route to reverse the owner. Admission and execution are not ordered against the owner’s subsequent press. Queued words lack D208’s complete keep-to-hand discipline, and excluding them from the answers-alone count permits a mixed prompt to escape its hold.

Attribution also needs more than a new suffix or enum: current move notes explicitly name the person, and running-session words are copied onto an ask through a path that carries no author.

The single notification line is consistent with the requested quiet management role. Its overrule controls must, however, name real reversals and protect the owner’s resulting state.

### 3. The rows

| Row | Assessment |
|---|---|
| **1a — door and nine acts** | Can land dark, but needs authenticated deferred hand-overs, effective overrules and corrected attribution. Its proof currently tests the insufficient same-route overrule rule. |
| **1b — activation** | Correctly waits for containment’s installed proof. Its own installed proof omits important holder, revoke and restart cases already required by D208. |
| **1c — words** | Correctly separated from the initial door. Its proof needs mixed prompts, late-arriving owner waits, failed persistence, live queues and revocation races. “Neither counts nor resets” alone proves too little. |
| **1d — setup** | Correctly waits for ask attribution. The fixed-rule restriction must be enforced on the plan actually applied. |
| **1e — done** | Correctly waits for TERMTRUST1b’s transactional conditions. Its promised reversal does not exist. |
| **1f — desk** | Can land separately. Its open, claims-only request mechanism is appropriate; it must not become an alternative admission path. |

The first usable increment is **1a plus 1b**. The foundation is necessary, but the claim that completing all nine acts before words removes the most cards is unsupported by a card-frequency breakdown. Drive, trees and routine stops deserve an early usable slice; the deferred hand-over machinery should not silently become their critical path.

### 4. Findings

1. **Blocking — deferred hand-overs can impersonate the steward.**  
   `Design:236`, `:318` and `:458` add `HandOverBy.Steward` while retaining the existing hand-over mechanism. `Driver/Conversations/HandOver/HandOverFiles.cs:37` and `:80` reconstruct operations from writable files; `Driver/Loop/DriverHandOvers.cs:84` consumes them and `:133` preserves their claimed asker. Once `steward` becomes a known value, a planted or altered file can produce a steward-attributed act without its proof.  
   **Smallest change:** retain the admitted operation’s complete contents and receipt in memory; require an exact match before consuming a steward-labelled box or handed record. Test planted and altered files.

2. **Blocking — `owner-set` protects a route, not the owner’s decision.**  
   `Design:353` remembers route and target, yet promises the steward can never undo an overrule. An owner’s `STOP_SESSION` does not block `SESSION_HAND_OVER`. `Driver/Loop/DriverHandOvers.cs:105` explicitly releases an already-ended session’s stop through a hand-over. A new part also changes the session target.  
   **Smallest change:** protect the relevant setting or line of work across all acts that could reverse it, including successor parts. Test an owner stop followed by steward hand-over and words.

3. **Blocking — an admitted act can overwrite a later owner press.**  
   `Design:254`–`:256` admits under the judge’s lock, then runs the handler. The settings handlers write independently (`Modules/Bridge/Routes/DriverRoutes.cs:44`, `:54`, `:69`). A steward drive can be admitted, the owner can stop driving, and the delayed steward handler can write last. Remembering `owner-set` afterward does not restore the owner’s choice.  
   **Smallest change:** serialize conflicting effects per target, or compare an owner-change version immediately before mutation. Prove both execution orders without holding the judge’s lock across an await.

4. **Blocking — hand-over approval has the wrong form and insufficient binding.**  
   `Design:203` specifies `{ id }`; the handlers require `session` (`Modules/Bridge/Routes/SessionsRoutes.cs:400`, `:439`), and the terminal sends it that way (`Driver/Sessions/SessionsCommand.cs:432`). The row also lacks an explicit quest-session restriction and names no expected ask. The existing handler can act on a conversation and defaults to whichever ask is current (`SessionsRoutes.cs:405`–`:410`).  
   **Smallest change:** specify `{ session, ask }`, require a quest’s own session, bind the chosen agent and exact waiting ask, and refuse replacements. Test an owner conversation and an ask changed between judgment and application.

5. **Blocking — a neutral steward word bypasses the answers-alone hold.**  
   `Design:290`–`:292` excludes steward words from the count. But `Driver/Conversations/Domain/AnswersAlone.cs:35` holds only when **all** words are solicited answers; `Driver/Conversations/GoOn/AnswersAloneHold.cs:31` and `:57` repeat that condition. Adding one steward word makes a third helper/supervisor prompt fail the hold predicate. A new unmarked word also defeats `GoOnMarks.Judged` (`Driver/Conversations/GoOn/GoOnMarks.cs:130`).  
   **Smallest change:** make steward words neutral to eligibility as well as counting. They must not release answers held for the owner or an owner’s *Not now*. Test mixed prompts and an existing hold.

6. **Blocking — role words need a complete keep-to-hand protocol.**  
   `Design:263`–`:268`, `:286`–`:287` and `:347` specify an authored-id set, initial checks and a sweep. They do not define atomic attribution before dispatch, late question/go-ahead checks, or withdrawal from a running inbox. The existing path uses `KeepOrHoldAsync`, which deliberately falls back to process-only delivery after persistence refuses or fails (`Driver/Conversations/DrivenInbox.cs:488`). Removing a service record does not remove that inbox’s copy.  
   **Smallest change:** define a fail-closed role-word operation with an authenticated immutable payload and dispatch receipt; recheck applicable bounds before attempting delivery and withdraw through the owning dispatcher. Cover failed persistence, a wait appearing after enqueue, and revoke racing delivery.

7. **Blocking — appending `role.steward` leaves explicit personal attribution.**  
   `Design:316` appends the role part after the act’s own note. Those notes include “The person finished this at a checkpoint” (`Driver/Sessions/Requests/SessionMoves.cs:80`) and “the person stopped it” (`Driver/Sessions/Domain/Observation.cs:374`). Hand-over reason `press` also becomes “the person asked for it” (`Driver/Conversations/Domain/HandOverRecords.cs:46`, `:60`). A suffix does not make those statements true.  
   **Smallest change:** make the base move wording and hand-over reasons actor-aware, preserving their behavioral codes. Test stored English, translated presentation and resumed briefs.

8. **Blocking — session words can become owner requirements on the ask.**  
   `Design:278` promises correct readers, but reuses `SESSION_INPUT`. Its running-session path calls `KeepOnAsk(id, text)` (`Modules/Bridge/Routes/ConversationRoutes.cs:146`). `Driver/Conversations/GoOn/SessionSay.cs:112` forwards only text; `Core/Sessions/SessionLedger.cs:1472` constructs an authorless `AskWord`. `Driver/Quests/Domain/AskWords.cs:143` then introduces those words as the person’s verbatim words. The setup ask’s new `by` field does not repair this separate path.  
   **Smallest change:** exclude role words from personal ask-history writes, or carry their authenticated author through that history and every quotation.

9. **Blocking — the setup restriction must guard the plan actually applied.**  
   `Design:205` checks the fixed nine-rule form, then `:236` invokes the ordinary handler. That handler recomputes its plan (`Modules/Bridge/Routes/SetupRoutes.cs:46`); `Driver/Setup/SetupPress.cs:170`–`:171` adds whatever `RulesOf(plan)` returns. A setup-to-index change between judgment and application could introduce commands the appointment did not grant.  
   **Smallest change:** enforce the role’s non-index and exact-rule restrictions inside the application path, immediately before saving permissions, against the same plan being applied. Test a changed plan.

10. **Blocking — `Reopen` cannot reverse this `done`.**  
    `Design:350` offers *Reopen* after a steward done. `Core/Quests/Quests.cs:2057` requires the quest still be `Taken`, and `:2066` requires a current submission. `Driver/Quests/QuestReopenCommand.cs:59` likewise refuses without a submission. TERMTRUST1b’s legacy done closes a graphless quest; D201’s design explicitly says it has no reversal (`docs/2026-10-23-terminal-trust-design.md:136`).  
    **Smallest change:** remove the nonexistent reversal and explicitly disclose irreversibility in the appointment and rationale, or defer this act until a real reversal is designed.

11. **Non-blocking — the casing claim does not describe D201’s forms.**  
    `Design:190`–`:191` and `:462` require every other casing to fail while describing reuse of D201’s form rules. `Modules/Platform/StandingConfirmation.cs:465` and its form dictionaries compare field names case-insensitively.  
    **Smallest change:** explicitly decide whether steward forms preserve that behavior or deliberately narrow it; align the tests and comparison with D201.

12. **Non-blocking — tree settings govern starts, not relocation of existing sessions.**  
    `Design:35` and `:197` describe `off` putting sessions back in the checkout and `on` ensuring each session works in its own tree. `Driver/Platform/DriverConfig.cs:1417` changes the standing choice; `Driver/Loop/Driver.cs:1891` reads it for a start. Existing sessions are not relocated by the setter.  
    **Smallest change:** describe future starts and continuations precisely; retain the prohibition on steward `off`.

13. **Non-blocking — index commands are generated forms, not two declared commands.**  
    `Design:37` and `:205` describe the index commands as repository declarations or words. `Driver/Setup/SetupPress.cs:117` obtains them from `IndexForms.For`; `Driver/Setup/Domain/IndexForms.cs:54`–`:62` constructs fixed Node, .NET or Python commands.  
    **Smallest change:** correct their origin. The exclusion remains reasonable because they execute repository-local generator code.

14. **Non-blocking — a finish does not always leave a quest for owner acceptance.**  
    `Design:201` and `:351` overgeneralize the result. D201 explicitly includes helper delivery, workflow-node consequences and existing queued personal words (`docs/2026-10-23-terminal-trust-design.md:132`). An ordinary legacy quest remains open or taken and needs `quest done`, as `Driver/Sessions/SessionsCommand.cs:1061` says.  
    **Smallest change:** distinguish those cases and add their role-attribution proofs, or narrow finish to the intended session kinds.

15. **Non-blocking — adding a role to a live holder needs a permission transition.**  
    `Design:134` adds a role without restarting the holder; `:144` gives the session the union of its roles’ verbs. D208’s initial holder permits only its answer verb (`docs/2026-10-24-supervisor-design.md:310`). The transition between those permission sets is unspecified.  
    **Smallest change:** state how a live holder obtains the additional allowed verb and role instructions without exposing the key or loading repository configuration. Test both appointment orders with the holder session already running.

16. **Non-blocking — the row proofs weaken the inherited activation evidence.**  
    `Design:469`–`:474` does not retain D208’s installed checks of the holder’s exact command line, loaded configuration, absent project MCP process, revoke and restart behavior (`docs/2026-10-24-supervisor-design.md:1271`). Later rows also need explicit offered-state behavior so an existing appointment does not silently gain an unreviewed act.  
    **Smallest change:** carry those activation cases forward, specify each later act’s offering, and identify the first usable slice. Support “most cards” with evidence or remove that ranking.

**16 findings; 10 blocking.**

**Verdict: another round.** Keep the shared appointment and dedicated-holder direction. The next round needs authenticated deferred operations, overrules that survive alternate routes and races, a fail-closed word lifecycle, complete attribution through notes and ask history, and a truthful contract for `done`. These affect authority and execution, so they require another read of the revised mechanism.

## Opus (foundation)

The independent Opus 5.5 reader's foundation read of round 1.

## STEWARDROLE1 (D209), round 1: the independent foundation read (D191)

Read at `48f9b3cff`, the design's commit on `d580caeb4`; the two differ only in documents. Read whole: the design and D209. Read
against them: D208 and its notes, and the supervisor design §0, §2, §4.4-§4.6 and §5. Also D201 and its notes, the terminal-trust
design's §0 and §2.1, D156 points 1 to 7, D193 parts 1 and 18, D191 point 3, and the code cited below. With read-only git I also
looked at the SUPERVISOR1a-ii branch (`daoris/s-436db1cf`), because the design's first row depends on it.

### 1. Claims about the code that are wrong

1. **§5.1: the steward's mark "after the act's own note", drawn *By the steward, never You*.** The act's own note is the
   person's, so adding a part leaves the record saying the owner did it:
   - A live stop writes `Observation.Stopped`, *the person stopped it.* (`Observation.cs:374`, through `SessionMoves.cs:26`).
   - A parked stop writes `ByThePersonNoted("stopped")`, *The person stopped this at a checkpoint.* (`SessionMoves.cs:81`;
     `SessionsCommand.cs:946`).
   - The covered finish writes `ByThePersonNoted("completed")`, *The person finished this at a checkpoint.* (`SessionMoves.cs:62-63`,
     `:80`).
   - The page words these codes as the person's (`locales/en/note.json:39`, `:106`, `:107`; zh 手动).
   - The planner holds such a stop by its state (`Planner.cs:259`) and gives the reason *you stopped session `s1`; Try again carries
     it on* (`PlannerTests.cs:1451-1460`).
2. **§3.3: "the act runs through its own route's handler, the same code the owner's press runs".** For `SESSION_INPUT`, that code
   keeps the words a running session took on its ask, with no author:
   - The path is `ConversationRoutes.cs:146`, then `SessionWords.cs:654`, then `WordsOnAsk.Keep` (`SessionSay.cs:113-128`), then
     `POST /api/sessions/{id}/added` (`Program.cs:1462-1465`), as `AskWordKind.Added`.
   - The ask holds each such word as *One thing the person said on an ask, verbatim* (`Asks.cs:200`), and `AskWords.Beneath` quotes it
     to every later session as the person's own words (`AskWords.cs:143-144`).
   - The say and taken doors keep only `Persons` words (`Program.cs:1504`, `:1554`). The added door has no such filter.
3. **§4.3: "a word of another author does neither today (`AnswersAlone.cs:21-31`)".** That is true of `Count`, but the hold is
   `words.Count > 0 && words.All(Solicited) && Count(line) >= Bound` (`AnswersAlone.cs:34-35`). The look also takes a record as a
   candidate only where `Waiting.All(Solicited)` (`AnswersAloneHold.cs:31`, `:57`). A word that is not solicited, beside a held
   answer, therefore lifts the hold.
4. **§5.5: rows 1 to 9 and 12 each "only narrows ... or is undone by one press".** Row 12's done closes the quest "on the person's
   word, its requirements answered by none, a chain's next step published, a quest waiting on it resumed" (the terminal-trust design,
   line 63, citing `QuestExchange.cs:2233-2305`). A Reopen undoes neither the published step nor the resumed quest.
5. **§5.3: "none is a wait on the owner".** A steward's stop is held by its state alone (`Planner.cs:259`, `PersonStopped`) until
   *Try again*, which §3.2 gives to the owner (`RETRY_QUEST`). Every stop the steward makes leaves something waiting on the owner.
6. **§6.1: "the plans" are among the holder's reads "whose answer the instance gives".** The plan verbs (`InstanceRoutes.cs:94-99`) act
   alone where no instance answers or a headless loop holds the home (`InstanceDoor.cs:8-9`; `InstanceDoorTests.cs:90-146`). The alone
   path of `trees clean`, `trees sync` and `trees land --plan` runs git across the trees. The design's own rule, "never a verb that
   acts alone and reads the checkouts with its own git", excludes them.
7. **§0.1's forms and §0.5's citation.**
   - The terminal sends `STOP_SESSION { id, said: true }` and `RESOLVE_SESSION { id, state, said: true }` (D201 point 2), not `{ id }`
     and `{ id, state }`. The `said` flag picks the verb's path (`SessionsRoutes.cs:28`, `:96`).
   - "A hand-over from the terminal is recorded `HandOverBy.Person` (`ConversationRoutes.cs:247`)": line 247 is the window's path,
     which records `HandOverAsker.Window` (`:253`). The terminal's path is `SessionsCommand.cs:1663`, with `HandOverAsker.Terminal`
     (`:1669`). The substance, that it is recorded as the person's, holds.
8. **§0.2: "The door is not built ... `DAORIS_SUPERVISOR_KEY` ... in no source file"; §2.2: "No source names the old one yet".**
   These were true at `d580caeb4` but are now out of date. SUPERVISOR1a-ii is built dark on `daoris/s-436db1cf`
   (`a639d3475`..`967bdf748`) and not merged. That build has:
   - `DAORIS_SUPERVISOR_KEY` (`SupervisorProof.cs:20`, `GitDoor/Program.cs:15`, `WiringVariables.cs:103`) and one key per
     appointment;
   - the folder `work/rooms/roles/supervisor/`, plus `PipeClass.Role` and `PipeDoor.Role`;
   - by the owner's direction, the holder's terminal as a tab of Daoris's own terminal panel (`TerminalModule.OpenHolderAsync`), in
     place of D208 §2.2's console.

   `TASKS.md:376` already names that row's session.

Every other citation held as cited, or within a line or two of where it was cited.

### 2. The mechanism's holes

The foundation holds:
- The role is admitted by a key whose hash lives only in memory, and proved by an HMAC over the hello's nonce, never sent.
- D201's caller look is the first denial, and the image check refuses first.
- The holder is a dedicated session that runs no branch's code. The working session holds no key, and its presses ask as today.
- D201's four presses become the steward's acts rather than a second door.

The design settles the tension correctly. D208's round-4 reason, that every process a terminal starts inherits its key, rules out the
working session, and the desk/workshop split keeps that reason intact. Its cost is that the owner's cards fall only for acts the
holder makes. The relay from the working session to the holder is therefore on the owner's critical path (findings 15 and 7).

The holes, by the four questions:

- **Only the appointed steward acts as it.**
  - One holder per run hands the supervisor's adversarial inputs to a session that can now drive, stop, hand over and speak. These
    are the asking agent's question, its `does`, its commits and its last message. D208 §4.4's bound ("little gets through": an
    offered label only) no longer covers that session, so §11's "no bound the shared holder lacks" is wrong (finding 11).
  - A re-appointment after a revoke reuses the same live key and the same, possibly misled, session (finding 10).
  - Words are handed from the record's text rather than from memory, which D208 rejected (finding 8).
  - The plans' alone path runs git in the holder's tree (finding 12).
- **A covered act never carries the owner's decision, nor shows as the owner's.**
  - Stops and finishes show as the person's (finding 1).
  - Words to a running session land on the ask as the person's verbatim words (finding 4).
  - Words lift the answers-alone hold, which only the owner's `SESSION_ANSWER_ALONE` may lift (finding 5).
  - Row 12 is the owner's acceptance (finding 3).
- **One appointment model.**
  - The shape holds.
  - *Appoint again* revives every found role, and a set-up tick a file can carry, in one ask (finding 6).
  - The model has to be written against the door as built (finding 7).
- **The overrule and the revoke are real.**
  - `owner-set`, keyed by route and target, does not keep an overrule: a stop and its go-on are different routes, and a session's id
    changes across a retry or a hand-over (finding 2).
  - A revoke leaves a steward's hand-over box that has not been taken yet (finding 10).

### 3. The rows

- **STEWARDROLE1a** is not buildable alone as written.
  - It builds the door "where SUPERVISOR1a-ii has not landed first", but 1a-ii is built, and it differs from D209 §2.2 in the
    variable, the key per appointment, the folder and the terminal. The row has to amend the built door.
  - It joins the whole door, nine acts and the page in one dispatch, and should split into the door's amendments and the steward's
    acts with their records.
  - Its proof lacks finding 1's notes, finding 2's overrule across routes and quests, and the revoke's sweep.
- **STEWARDROLE1b** is the right second step. Its critical path is D201 part (i)'s case 4 on the doors not seen yet (plugin, piece,
  intake, helper, reviewer, brainstorm, workflow node). Its look should read a steward's stop and finish in en and zh, with no
  *person* or *you*.
- **STEWARDROLE1c** needs findings 4, 5 and 8. A fixed go-on would give the owner the outage go-on without free text (finding 14).
- **STEWARDROLE1d** needs finding 6's unticked set-up at *Appoint again*.
- **STEWARDROLE1e** should be dropped (finding 3).
- **STEWARDROLE1f** should come before 1b, or 1b should name a gitignored relay. Until the working session's needs reach the holder,
  1b removes only the cards the holder thinks of itself.
- **The first row and the owner.** 1a and 1b's acts (drive, trees, hold, stop, finish, hand-over) are the ones the direction names,
  less words and set-up, so the first row is the right choice. It gives the owner nothing, though, until the relay of finding 15
  exists.

### 4. Findings

1. **The steward's stop and finish read as the owner's** (§5.1; `Observation.cs:374`, `SessionMoves.cs:62-63`, `:80-81`,
   `note.json:39`, `:106-107`, `PlannerTests.cs:1451-1460`). **Blocking.** The move's own note says *the person stopped/finished*,
   and the planner's reason says *you stopped*.
   - Smallest change: in §5.1, the steward's acts write their own move notes, new codes such as `ended.stopped-steward` and
     `stopped.checkpoint-finished-steward`, in place of the person's.
   - Name the readers by code: TERMTRUST1b's done reads `stopped.checkpoint-finished`, and the planner's held reason.
   - Add a 1a proof that no record of a steward's act reads *person* or *you*.
2. **`owner-set` does not keep an overrule** (§5.4, lines 353-356). **Blocking.**
   - Keyed by route and target, the owner's go-on (`SESSION_INPUT`/`RETRY_QUEST`) after a steward's `STOP_SESSION` lets the steward
     stop again.
   - A retry or a hand-over gives the quest a new session id that no mark covers.
   - "The steward can never undo an overrule" is false as specified.
   - Smallest change: key the mark by the thing acted on, whatever the route. That is the repository for rows 1 to 5, and the
     session's quest for rows 6 to 10, so any owner press on that repository or quest refuses every steward act on it for the run.
   - Add a test for a stop, then the owner's go-on, then a stop refused, and the same across a retry.
3. **Row 12 (done) is the owner's acceptance** (§3.1 row 12, §3.2's own row "accepting what is delivered", row 7's "the quest then
   sits for the owner's acceptance"; D193 part 1; terminal-trust design line 63). **Blocking.** It closes the quest with its
   requirements answered by none, publishes the next step, and is not undone by Reopen.
   - Smallest change: drop row 12 and STEWARDROLE1e, and add `QUEST_DONE` to §3.2's acceptance row.
4. **Words to a running session are kept on the ask as the person's verbatim words** (§3.3, §4.1; `ConversationRoutes.cs:146`,
   `SessionSay.cs:113-128`, `Program.cs:1462-1465`, `Asks.cs:200`). **Blocking.**
   - Smallest change: in §4.1, a steward's word never takes `KeepOnAsk` or the added door. Either it goes only to records not
     running, as the supervisor's do, or the added door takes `from` and keeps only the person's.
   - Add a 1c proof: a steward's word to a working session is absent from `AskWords`.
5. **The steward's words lift the answers-alone hold** (§4.3; `AnswersAlone.cs:34-35`, `AnswersAloneHold.cs:31`, `:57`).
   **Blocking.** A steward's word beside a held third answer makes the prompt "not all solicited", so it goes on. That is
   `SESSION_ANSWER_ALONE`'s, which §3.2 gives the owner.
   - Smallest change: a condition `answers-held` refusing a steward's word to a record the hold marked or would mark.
   - The hold reads its words less the steward's.
   - Add a test to `AnswersAloneTests`.
6. ***Appoint again* revives what a file says** (§2.2's last two points, §2.3; supervisor design §2.3 lines 403-414, the round-4
   finding 7c). **Blocking.** One ask "re-appoints every role found", with the found record's `scope` and `setup: true`. Any process
   of the account can add a steward appointment, with its widening tick, to `standing-confirmation.json` while Daoris is down. A
   habituated press on *Appoint again* for the supervisor would then turn it on.
   - Smallest change: each found role is its own choice in the ask, unticked.
   - The `setup` tick is always shown off and is set only by the owner's tick in that ask.
   - Add a 1a proof with a planted steward record.
7. **STEWARDROLE1a is written against a door that is already built differently** (§10 row 1a; `daoris/s-436db1cf`; `TASKS.md:266`,
   `:376`). **Blocking** for the rows.
   - Smallest change: rewrite 1a as the amendment of the built 1a-ii. That covers `DAORIS_SUPERVISOR_KEY` to `DAORIS_ROLE_KEY`, one
     holder per run in place of a key per appointment, `work/rooms/roles/holder/` (the build's D172 room, not `<home>/roles/`), and
     the panel tab as the holder's terminal ("while that terminal's process lives" meaning that tab's shell).
   - Update §0.2 and §2.2's "no source names it".
   - Alternatively, hold 1a-ii's merge until D209 settles.
8. **The handed words come from the record, not memory** (§4.1-§4.2; D208's rejection of "the driver handing a word's stored text",
   `D208.md:132`). Not blocking.
   - Smallest change: the driver's memory keeps each steward word's text with its id, and the paragraph is composed from memory. A
     record word whose text differs is quarantined.
   - Daoris draws the act's id at admission, never the verb.
9. **A role added to a live holder meets a session started without its verbs** (§2.2's "adds the role to the same holder" against
   "its permissions the union of their verbs"). Not blocking.
   - Smallest change: the holder's printed start line always allows every role's verbs. The permission is not the authority; the
     judge's `not-appointed` is.
   - Or say that the harness session is restarted in the same tab.
10. **The revoke and the holder's end** (§2.2, §5.4). Not blocking.
    - Revoking the last role, or the holder's shell exiting, should drop the run's hash, so a later *Appoint* starts a fresh holder
      and session rather than reviving one the owner turned off.
    - The revoke's sweep should also withdraw a steward's hand-over box not yet taken (`HandOverQuest.cs:101-109`), as it takes back
      waiting words.
11. **One holder per run widens what the supervisor's inputs reach** (§2.2, §11's "no bound the shared holder lacks"; supervisor
    design §4.4). Not blocking.
    - Smallest change: correct §11's reason, and say in the ask that a holder with both roles reads asking agents' text with
      management acts in hand.
    - Offer *a holder of its own* at the second *Appoint*.
12. **The holder's plans verbs act alone with git** (§6.1; `InstanceRoutes.cs:94-99`, `InstanceDoor.cs:8-9`). Not blocking.
    - Smallest change: where `DAORIS_ROLE_KEY` is set, every verb refuses its alone path (`role-terminal`), as press verbs already
      do.
13. **The forms choose paths the design does not name** (§3.1 rows 1-3, 8 and 10 without `said`, rows 6-7 with it; §0.1; §0.5).
    Not blocking.
    - Without `said`, `SESSION_HAND_OVER` runs the window's path (`HandOverAsker.Window`, `ConversationRoutes.cs:253`) and
      `SESSION_INPUT` runs the screen's (`door: "screen"`, `KeepOnAsk`).
    - Row 10's "no `key` the verb did not draw" leaves the exact form open.
    - Smallest change: state each form whole, the path it runs and the record it writes. Correct §0.1's forms and §0.5's citation.
14. **Free words launder agents' text, and the paragraph overstates them** (§4.2, §4.4). Not blocking.
    - Text the holder read from a file, a desk request or a question can travel to another session as *From the steward ... manages
      this development for the owner*. §4.4 calls them claims, while §4.2's paragraph gives them management's standing.
    - Smallest change: 1c first offers a fixed `steward go-on <id>`, its text Daoris's own, which covers the outage go-on.
    - Free words wait for their own read, as D208 kept SUPERVISOR1f's.
15. **The relay from the working session to the holder** (§6.2, row 1f). Not blocking.
    - Before 1f, the holder learns what to do only from "a row or a note in this repository's records". Writing ephemeral requests
      such as "finish s-…" into versioned records is not those records' job.
    - Smallest change: before 1f, a gitignored request file the holder reads by path. Move 1f before 1b.
    - Write the working session's rule (never press what the holder makes) into the `dispatch-subagent` skill.
    - Say where the holder's own brief comes from, since it loads no instruction files.
    - Bound the desk's memory.
16. **A steward's stop waits on the owner** (§5.3; `Planner.cs:259`). Not blocking.
    - Smallest change: say so in §5.3, and *What needs you* names it as the steward's stop.
    - Or give the steward *Try again* on its own untouched stop, as `resume` is given on its own hold.
17. **The record marks are claims** (§5.1). `role.steward` on a record and `HandOverBy.Steward` in `<home>/handed/` are within an
    agent's ordinary reach. Not blocking.
    - Smallest change: draw *By the steward* live only where this run's memory recorded the act. Otherwise read it as history,
      which grants nothing.
18. **Drive's reach** (§3.1 row 1). Not blocking.
    - `scope: machine` lets the steward drive a repository the owner left undriven in an earlier run, since `owner-set` lasts the
      run only.
    - It can also drive a repository whose landing rule's plugin pushes and opens a pull request (`SetupPressRules.cs:64-67`'s
      words), and §3.2 makes a push the owner's.
    - Smallest change: say both in the ask, or add a condition that refuses `pushes` where the repository's landing pushes.
19. **The appointment's form and its flags** (§2.3, §10). Not blocking.
    - `SET_APPOINTMENT { role, scope, holder, on, setup? }` drops the supervisor's `repository`.
    - `StewardOffered` is not related to `AppointmentsOffered`.
    - Smallest change: give `scope` as `machine` or `repository:<name>`, and name which flag gates what.
20. **STEWARDROLE1a's size** (§10). Not blocking.
    - Smallest change: split it into the door's amendments (per-run holder, variable, scope, found roles, `ROLE_ACT`,
      `PipeDoor.Role`, `owner-set`) and the steward's acts with their records and page, each with its own proof.

**20 findings, 7 blocking.**

**Verdict: accept after named changes the steward can check without another read.** The foundation holds, and the design settles
the tension D208's round 4 left by splitting the desk that holds the key from the workshop that runs branch code:
- the role is admitted by a key only the holder's terminal carries and proved over the hello's nonce;
- the holder runs no branch's code;
- the working session holds nothing;
- D201's presses are folded into the role rather than kept as a second door that would record the steward as the owner;
- one holder serves every role of a run.

The seven blocking findings are each a bounded text change that can be checked at the lines cited:
1. Write the steward's stops and finishes with move notes of their own, and name the readers by code.
2. Key `owner-set` by repository or quest, whatever the route.
3. Drop row 12 and STEWARDROLE1e.
4. Keep the steward's words off the ask's added door.
5. Refuse the steward's words where the answers-alone hold is on, and keep them out of the hold's predicate.
6. Make *Appoint again* offer each found role, and the set-up tick, unticked.
7. Rewrite STEWARDROLE1a against the SUPERVISOR1a-ii door as built.

If the steward keeps free words in STEWARDROLE1c rather than starting from a fixed go-on, that row deserves its own read, as D208 gave
SUPERVISOR1f.
