# STEWARDROLE1: the reads of rounds 1 and 2 (D191)

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

## Codex, round 2

Codex's read of round 2: another round, keeping the shared appointment, the separate working session, the steward's own records, the fixed go-on and done left to the owner.

### 0. Round-1 findings checked again

Read at `c5cb45753`, including the complete round-1 reads and the requested diff. **Taken** below means corrected in the proposed contract, not already implemented.

References use:

- **Design**: `docs/2026-10-24-steward-role-design.md`
- **Supervisor**: `docs/2026-10-24-supervisor-design.md`
- **Driver/**: `src/Daoris.Desktop/Daoris.Desktop.Driver/`
- **Modules/**: `src/Daoris.Desktop/Daoris.Desktop.Modules/`
- **Core/**: `src/Daoris.Service/Daoris.Service.Core/`

| Finding | Disposition | Check against code and round 2 |
|---|---|---|
| C1 — deferred hand-overs can impersonate the steward | **Open** | Design:411 adds memory matching at the look, but `Driver/Loop/Driver.cs:1896` independently rereads the handed file at the start. Finding 3 below. |
| C2 — `owner-set` protects a route rather than the decision | **Open** | Design:450 correctly changes the key to repository or quest. Deferred effects and the omitted take-back route still escape that protection. Findings 2 and 5. |
| C3 — an admitted act can overwrite a later owner press | **Open** | Design:458 fixes synchronous settings handlers such as `Modules/Bridge/Routes/DriverRoutes.cs:44`. Its handler-only gate does not cover later hand-over execution. Finding 2. |
| C4 — hand-over approval’s form and binding | **Open** | `{ session, ask }` fixes `SessionsRoutes.cs:400`, but carries neither the listed agent nor an immutable listing reference. Finding 4. |
| C5 — steward words bypass the answers-alone hold | **Taken** | Design:340–367 refuses mixed queues and makes steward words neutral to both predicates and `GoOnMarks.Judged`. This addresses `AnswersAlone.cs:35` and `AnswersAloneHold.cs:31`, `:57`. |
| C6 — role words need a complete keep-to-hand protocol | **Open** | Removing running-session delivery and the process-only fallback is correct. The explicit `not-handed` recovery still contradicts the inherited lifecycle. Finding 7. |
| C7 — a suffix leaves personal attribution | **Open** | Design:395–400 supplies appropriate sibling codes. The terminal stop’s request-folder carrier still needs authenticated actor information at consumption. Finding 2; `SessionsCommand.cs:949`, `SessionRequestWatch.cs:349`. |
| C8 — session words become personal ask requirements | **Taken** | Design:340, :363 excludes the running-session and added-door paths. `Core/Sessions/SessionLedger.cs:1448` remains a separate personal-history operation. |
| C9 — setup must guard the applied plan | **Taken** | Design:260, :630 puts the check immediately before the permission save, addressing `SetupPress.ApplyAsync` rather than trusting `SetupRoutes.cs:46`’s earlier plan. |
| C10 — Reopen cannot reverse this done | **Taken** | Design:216–219 removes steward done and retains owner acceptance. This agrees with `Core/Quests/Quests.cs:2057`, `:2066`. |
| C11 — casing claim differs from D201 | **Answered with a reason that holds** | Design:240 preserves case-insensitive field names, duplicate rejection and exact values, matching `StandingConfirmation.cs:763`. |
| C12 — trees settings do not relocate sessions | **Open** | The immediate-relocation claim is removed, but Design:251 now overstates continuations. They can retain the former checkout. Finding 10. |
| C13 — index commands are generated | **Taken** | Design:40 correctly names `IndexForms.For`; `SetupPressRules.cs:36`, :48 keeps the fixed/generated distinction. |
| C14 — finish has consequences beyond its session | **Taken** | Design:255 excludes helpers, workflow-node sessions and waiting words. That is a valid narrowing of D201’s broader finish. |
| C15 — adding a role needs a permission transition | **Taken** | Design:174–180 permits the role verbs from the first session and refreshes the brief; the judge retains authority. This addresses `HolderTerminal.cs:27`. |
| C16 — activation proofs weakened inherited evidence | **Open** | The holder checks return in Design:602, but the shared flag can activate the supervisor before its remaining bounds and activation proof. Finding 6. |
| O1 — stop and finish read as the owner’s | **Open** | The sibling notes address `SessionMoves.cs:80` and `Observation.cs:374`; the deferred stop carrier remains unspecified. Finding 2. |
| O2 — overrule does not stick | **Open** | Quest identity now survives successor sessions, but deferred execution and `SESSION_QUEUE_EDIT` remain outside the stated protection. Findings 2 and 5. |
| O3 — done is owner acceptance | **Taken** | Steward done is removed, including its nonexistent reversal. Design:275 expressly reserves `QUEST_DONE`. |
| O4 — running-session words become personal ask history | **Taken** | The fixed go-on never uses `KeepOnAsk` or `/added`; Design:363 expressly requires the absence proof. |
| O5 — words lift the answers-alone hold | **Taken** | Both eligibility and counting are addressed, including mixed prompts and existing marks. Design:365; `AnswersAloneHold.cs:57`. |
| O6 — Appoint again revives file contents | **Taken** | Design:201–205 reopens one role’s ordinary ask, ignoring stored `acts` and `setup`, with setup unticked. This extends the built per-row ask appropriately. |
| O7 — the appointment door is already built differently | **Taken** | Design:53–76 now describes the merged door. `StandingConfirmation.cs:534`, `HolderTerminal.cs:47` and `TerminalModule.cs:238` confirm the principal facts. |
| O8 — handed words come from the record | **Taken** | Design:356–358 requires composition from memory and rejects differing stored text, following D208’s memory-authoritative model. |
| O9 — a live holder lacks the added role’s verbs | **Taken** | Design:174 allows both role families from the beginning; appointment membership still controls admission. |
| O10 — revoke and holder termination | **Open** | Last-role termination and fresh reappointment are specified. Deferred approvals and the explicit tab-close path are not fully covered. Findings 2 and 11. |
| O11 — sharing widens the supervisor’s input reach | **Taken** | Design:161–162 discloses the widening and offers a separate terminal; :514–517 preserves the limitation. This is a reasoned owner choice. |
| O12 — plan verbs can act alone with git | **Taken** | Design:298–302 refuses every alone path in a role terminal. The implementation must use the entry’s captured marker, as finding 12 explains. |
| O13 — forms select unnamed paths | **Open** | Proposed steward forms now name their paths. The current-code table still omits mandatory `said: true` on say and hand-over and misidentifies the say path. Finding 9. |
| O14 — free words launder agents’ text | **Taken** | The fixed go-on removes arbitrary text; free words are deferred to STEWARDROLE1h with a separate design and read. That reason holds. |
| O15 — working-session relay | **Taken** | The bounded desk precedes activation; the brief is supplied by Daoris and the working-session rule joins activation. Design:495–502. No interim tracked request file is needed. |
| O16 — a steward stop waits on the owner | **Taken** | Design:436 says so and gives the steward retry of its own untouched stop. This addresses the planner’s stop hold. |
| O17 — record marks are claims | **Open** | The live/history distinction is sound, but the direct start’s disk read remains outside the described authentication point. Finding 3. |
| O18 — driving can reach pushing landings | **Taken** | Design:249 refuses driving where the landing pushes and :428 discloses the earlier-run choice. `SetupPressRules.cs:64` supports the identified plugin case. |
| O19 — appointment form and flags | **Taken** | Scope compatibility and offered-act membership are explicit at Design:190–199 and :562–565. The separate activation dependency defect is finding 6. |
| O20 — first row is too large | **Taken** | Door amendments, immediate acts, desk and activation are separate rows. Their remaining proof gaps are identified below. |

### 1. Claims about the code

The revised description correctly recognizes SUPERVISOR1a-ii on main: appointments share D201’s record; hashes remain in memory; the holder opens in the terminal panel; the proof uses the connection nonce; and both offerings remain dark.

The remaining incorrect or unsupported code descriptions concern:

- The assertion that the printed holder flags exclude hooks, plugins and instruction files: finding 8.
- The terminal’s actual payloads and say path, and the holder-key constant’s owning type: finding 9.
- The effect of enabling trees on existing conversations’ continuations: finding 10.
- The assertion that shell termination always produces `TERMINAL_EXITED`: finding 11.

Two implementation facts also undermine the proposed mechanism: a hand-over remains active configuration when the start rereads its file, and the driver host removes the key variable before command dispatch. Findings 3 and 12 address those.

### 2. The mechanism’s holes

The appointment foundation can serve the supervisor and steward, with D201’s unkeyed grant retired. Window-only appointment, dormant records after restart, independent generations, a dedicated holder and separate revocation remain coherent. Sharing a holder is now an explicit choice with an accurately stated consequence.

Within the stated D156 boundary, an ordinary Daoris agent, an escaped terminal process or a hook run by the working steward receives no key. That separation is useful. It still depends on the holder actually excluding executable configuration; the current start line does not establish that exclusion.

The largest remaining holes are between admission and effect. Revocation is not rechecked when an admitted act finally acquires its gate. Hand-over approvals and boxes take effect after their handlers return, outside the proposed gate and sometimes through an agent-attributed path. Authentication at the look also misses the start’s later disk read. These allow authority, attribution or the owner’s intervening decision to change before the actual effect.

The owner-only exclusions are substantially improved: done and acceptance stay personal, free words leave this design, setup receives an explicit tick and application-time restriction, and the go-on cannot enter a running inbox. However, the owner’s own take-back is omitted from takeover marking, and the go-on’s failure recovery is incomplete.

The new notes and quiet notification line are suitable. Their proofs must follow the actual deferred consumers, rather than stopping at the route handler or a vocabulary sweep.

### 3. The rows

| Row | Buildability and proof |
|---|---|
| **1a — door amendments** | Can land dark. Add revocation while awaiting a target gate, every terminal-close path, and an entry-level `role-terminal` case. |
| **1b — immediate acts** | Can follow 1a, but the live stop needs its request-folder authority and attribution protocol. Existing form and note tests alone do not prove that path. |
| **1c — desk** | Can land independently after its declared predecessor. Its admission-denial and bounded-queue proofs are appropriate. The notification presentation also reaches web code. |
| **1d — activation** | Not safely ordered as written: its shared flag also enables supervisor appointments without an explicit dependency on the supervisor’s remaining bounds and activation proof. |
| **1e — hand-overs** | Needs authenticated pending approvals, execution-time gating, immutable agent binding and authentication at the actual start. The proposed tests cover admission ordering but miss an already-kept operation overtaken by the owner. |
| **1f — go-on** | Properly separated from free words and allowed after 1d without depending on 1e. Add recovery from every failure after the move to working, preserving subsequently arrived owner words. |
| **1g — setup** | The application-time rule check and author field form a buildable slice. Its dependency on 1f is sequencing rather than an evident mechanism dependency. |
| **1h — free words** | Correctly outside this design’s acceptance. |

The first usable increment is **1a through 1d**, not 1a alone. Isolating routine settings, stops and finishes from hand-over machinery is reasonable. Nothing read establishes that this ordering removes the most cards; round 2 appropriately withdraws that claim. The activation count can inform whether the fixed go-on or setup should precede hand-overs.

### 4. Findings

1. **Blocking — revocation does not invalidate an act waiting for its target gate.**

   **Where:** Design:317–319, :442–444, :458–463.

   Admission checks the generation, releases the judge’s lock and waits for the target gate. Under that gate it rechecks only `owner-set`. Turn off can therefore finish before an admitted, still-waiting drive or stop runs. This contradicts the stated cutoff: only an act already past its gate is exempt from recall.

   The built supervisor deliberately rechecks admission later; see `Driver/Sessions/Questions/QuestionSupervisorDecisions.cs:357` and `Modules/Platform/StandingConfirmation.cs:629`.

   **Smallest change:** under the target gate, revalidate the holder and appointment generation immediately before committing execution. Specify its ordering with revoke without holding the judge’s lock across an await. Test revoke while an admitted act waits for the gate.

2. **Blocking — the gate and revoke sweep do not cover all deferred effects or their actor.**

   **Where:** Design:254, :257–258, :411–415, :442–463; rows 1b and 1e.

   A hand-over handler keeps a box; the later look stops the session and releases its stop (`Driver/Conversations/HandOver/HandOverQuest.cs:101`, `Driver/Loop/DriverHandOvers.cs:102`, :133). An approved agent ask may instead wait until the turn ends, then write a hand-over as `HandOverBy.Agent` (`DriverHandOvers.cs:279–294`). It need not have an untaken box for revoke to clear.

   Thus: steward approval or box first, owner stop or revoke second, deferred consumption third is not protected by a gate held only across the original handler. The resulting act can also lose steward attribution.

   Live stop has another deferred carrier: `SessionsCommand.cs:949` writes a `SessionRequest`; `SessionRequestWatch.cs:349–370` later constructs the move note without the handler’s `PipeDoor.Role`.

   **Smallest change:** give every pending role operation an authenticated in-memory lifecycle, including stop requests and approved hand-over asks. Preserve its actor, generation and complete operation; cancel pending operations on takeover or revoke; acquire the quest gate and recheck authority at the final consumer. Test both deferred hand-over paths and the request-folder stop, including their final notes.

3. **Blocking — authenticating hand-over files at the look misses the start’s independent read.**

   **Where:** Design:411–415, :614–618; `Driver/Loop/Driver.cs:1896–1897`.

   The start reads `new HandedQuests(home).Read(quest.Id)` directly and selects its agent from that answer. `HandOverFiles.cs:97–99` also reads its account and note. A file replaced after the look’s validation can therefore affect the start without the stated memory comparison.

   A consumed handed file is not merely history in today’s implementation: it remains the quest’s agent/account override.

   **Smallest change:** make the start consume the authenticated operation from memory, including the required null account, rather than rereading authoritative fields from disk. Treat later disk differences as tampering. Add a replacement between look and start and prove no changed agent, account or note is used.

4. **Blocking — hand-over approval does not bind the agent the holder saw.**

   **Where:** Design:258, :616–617.

   The exact form contains only `{ session, ask }`, while the condition promises comparison with “the agent the holder’s listing showed.” Neither that agent nor an immutable listing reference is carried in the authenticated form.

   The existing approval obtains the agent from writable state (`Driver/Conversations/HandOver/HandOverAskWord.cs:44–50`); `Modules/Bridge/Routes/SessionsRoutes.cs:405–410` supplies no listing snapshot.

   **Smallest change:** include the expected agent or an instance-issued immutable snapshot reference in the signed form. Compare it at application and deferred consumption. Test a same-id ask whose selected agent changes after listing but before admission, as well as after admission.

5. **Blocking — Take back does not make the quest the owner’s.**

   **Where:** Design:447, :450–454.

   The design offers Take back as the owner’s reversal of a waiting go-on, but omits `SESSION_QUEUE_EDIT` from takeover marking. That is the actual take-back/reword route (`Modules/Bridge/Routes/ConversationRoutes.cs:355–380`).

   After the owner withdraws the go-on, the queue can again be empty and satisfy the steward’s conditions. The steward can issue it again during the same run.

   **Smallest change:** include `SESSION_QUEUE_EDIT` in the quest’s owner gate and mark. Derive the takeover census from all relevant personal mutation doors rather than the present examples alone. Test steward go-on, owner take-back, steward go-on refused `owner-set`.

6. **Blocking — steward activation can prematurely activate the supervisor.**

   **Where:** Design:223–225, :562–564, :602–612.

   `AppointmentsOffered` controls every role, and the built judge admits supervisor appointments through it (`Modules/Platform/StandingConfirmation.cs:537`, :633, :657). Row 1d turns it on after steward 1c and containment proof.

   D208 additionally requires completed SUPERVISOR1a-i, SUPERVISOR1b’s bounds and SUPERVISOR1a-iii’s supervisor activation proof. Those remain open or incomplete in D208’s notes and `TASKS.md:266`, :269. Testing the holder’s command line and steward acts does not prove the supervisor’s bounds, held answer and overrule.

   **Smallest change:** either make 1d depend explicitly on those completed supervisor prerequisites and proof, or introduce an independent supervisor offering gate. Prove that activating the steward alone cannot appoint or admit an unfinished supervisor.

7. **Blocking — the go-on’s `not-handed` recovery leaves the session lifecycle unresolved.**

   **Where:** Design:359–362, :620–627.

   “`not-handed` restoring nothing” is not a safe specialization of SUPERVISOR1a-i. Admission first moves the record to working (`Driver/Loop/DriverContinuations.cs:268–274`). A failure before spawn must restore the park, even though the steward word itself must be withdrawn.

   The existing supervisor path explicitly restores the park while preserving other words (`DriverContinuations.cs:798–819`). Its service operation currently removes only supervisor words (`Core/Sessions/SessionLedger.cs:1091–1097`).

   **Smallest change:** distinguish restoring the **park** from restoring the **word**. Restore the park, remove the admitted steward word, retain other and newly arrived owner words, and finalize its ticket. Extend the restore operation accordingly. Test reread, preparation and spawn failures, including an owner word arriving after admission.

8. **Blocking — the holder’s printed start line does not establish the claimed configuration exclusion.**

   **Where:** Design:479–480; `Modules/Platform/HolderTerminal.cs:27–29`.

   The line selects **user** settings. It supplies an empty MCP configuration, but contains no mechanism establishing the absence of user hooks, plugins or instruction files. D208’s own contract makes the additional condition explicit: `--setting-sources user` **with no user hooks or plugins** (`Supervisor:306–308`). Round 2 presents that condition as something the flags already guarantee.

   Automatically loaded code receives the holder’s environment before the role verb strips anything. This is different from a holder deliberately choosing to run forbidden code. The current holder test checks the printed string (`Modules.Tests/Platform/HolderTerminalTests.cs:90`), not configuration exclusion.

   **Smallest change:** specify a reproducible holder configuration that excludes executable user configuration and unintended instructions, rather than assuming the ordinary user configuration is clean. Make activation prove that configuration, including a harmless configured-hook/plugin canary that does not execute.

9. **Non-blocking — the current-code inventory still gives wrong payloads and a wrong symbol.**

   **Where:** Design:41–42, :62, :103–104, :170.

   `sessions say` and `sessions hand-over` both send `said: true`: `Driver/Sessions/SessionsCommand.cs:440–441`, :536–538, with `Body` adding it at :501. Say consequently takes `ConversationRoutes.cs:104–116`, not the screen branch at :141–146. The personal ask-history concern remains valid through the terminal’s own delivery path.

   Also, the variable is `HolderKey.Variable` (`Driver/Sessions/Questions/SupervisorProof.cs:20`), not `SupervisorProof.Variable`.

   **Smallest change:** correct those payloads, trace the terminal say path separately, and correct the constant’s owning type.

10. **Non-blocking — trees on does not move every later continuation into an isolated tree.**

    **Where:** Design:38, :251, :691.

    The same-conversation continuation takes `park.Tree` directly (`Driver/Loop/DriverContinuations.cs:227`). Its tree predicate accepts an existing directory (`Driver/Conversations/ConversationTrees.cs:11`); it does not require the current isolation setting. `Driver.cs:2285–2287` can choose that continuation despite having prepared another tree.

    A session originally run in the checkout can therefore continue there after trees are enabled.

    **Smallest change:** distinguish fresh sessions/new parts from resuming an existing conversation in its recorded directory. Remove the universal continuation guarantee, or explicitly design and prove that relocation.

11. **Non-blocking — closing a holder tab does not emit the exit event named by the lifecycle.**

    **Where:** Design:69, :165–168, :572.

    `TerminalModule` removes a tab before disposing it on CLOSE (`Modules/Windows/TerminalModule.cs:147–153`). `Ended` then returns without emitting `TERMINAL_EXITED` because the tab is already absent (:270–274). Listening only for that event misses the ordinary close button.

    **Smallest change:** notify the judge through one internal holder-ended callback on explicit close, natural shell exit and disposal. Handle an exit during appointment startup too. Prove each path in 1a, before the installed activation look.

12. **Non-blocking — `role-terminal` must use the captured entry state, not the environment after startup.**

    **Where:** Design:298–301, :582–583.

    The driver host’s first action is `HolderKey.Take()` (`Daoris.Desktop.Driver.Host/Program.cs:337`). It removes the variable and retains it privately (`Driver/Sessions/Questions/SupervisorProof.cs:50–57`). A later verb guard checking whether the environment variable “is set” will always miss it.

    **Smallest change:** specify a captured role-terminal marker from the entry operation, independent of whether the key parses successfully. Exercise the real entry-to-dispatch path for both a personal press and an alone-path refusal.

**12 findings; 8 blocking.**

**Verdict: another round.** Keep the shared appointment foundation, separate working session, steward-specific records, fixed go-on and removal of steward done. The next round needs execution-time revocation, authenticated deferred operations through their final consumers, immutable hand-over binding, complete owner takeover coverage, independently safe role activation, queue-preserving go-on recovery and a reproducibly clean holder configuration. These determine who may act and whose later decision wins, so they require another read of the revised mechanism.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2: 14 findings, 5 blocking; accept after named changes.

## STEWARDROLE1 (D209), round 2: the independent foundation read (D191)

Read at `c5cb45753` (round 2 of the design and D209; the code is main's at `f5c515f0b`, with SUPERVISOR1a-ii's door merged). Read
whole: the design, D209, round 1's reads and round 2's diff. Read against them: D208 and its notes (points 2, 3 and 7, the
SUPERVISOR1a-ii note), D201 and its notes, D156 points 4, 5 and 7, D193 parts 1 and 18, the terminal-trust design's §2.1, the
supervisor design §2.2, and the code cited below. Paths are under `src/Daoris.Desktop/` unless said; *design* is
`docs/2026-10-24-steward-role-design.md`.

### 1. Round 1's findings

| Finding | Round 2 | Checked against | Outcome |
|---|---|---|---|
| **O1** stop and finish read as the owner's | Own codes `ended.stopped-steward`, `stopped.checkpoint-stopped-steward`, `stopped.checkpoint-finished-steward`; every reader by code; 1b's sweep proof (design §5.1, :390-406; §10 1b) | `SessionMoves.cs:78-83`, `Observation.cs:374` are the sites to fork; `Planner.cs:259` holds by state, so the sibling keeps the hold | **Taken as named; holds** |
| **O2** `owner-set` does not keep an overrule | Keyed by repository or quest, successor sessions covered (§5.4, :450-457) | The key is right, but the mark is still a list of routes. Ask Daoris's applied proposals (`HELP_APPLY`, `HelpRoutes.cs:473`) drive, undrive, hold, resume, give or take trees, set the landing and retry (`HelpSettingProposals.cs:61`, `:80-96`, `:105`) with no mark and no gate; D201's `Forget` runs only in `SET_HOLD` (`DriverRoutes.cs:62`); several quest-scoped `Person` routes are not listed | **Taken otherwise; does not hold yet** (finding 1) |
| **O3** row 12 is the owner's acceptance | Row 12 and the old 1e dropped; `QUEST_DONE` in §3.2's acceptance row (§2.4, :216-219; :275) | The terminal-trust design's `finish` row (*not by a press*) | **Taken as named; holds** |
| **O4** words kept on the ask as the person's | No free words; the go-on never reaches a running record, so it never takes `KeepOnAsk` or the added door (§4.3, :352-364) | `Program.cs:1507` (say) and `:1556` (taken) keep only `Persons` words; the added door (`:1464-1467`) is reached only from a running session | **Taken as named; holds** |
| **O5** words lift the answers-alone hold | `answers-held`, *no word waiting of any author*, the hold's predicates read words less the steward's (§4.2, :345; §4.3, :365-368) | `AnswersAlone.cs:34-35`, `AnswersAloneHold.cs:31`, `:57`: with no other word allowed at the keep, a mixed prompt cannot form, and a later word withdraws it | **Taken as named; holds** |
| **O6** *Appoint again* revives the file | One role at a time from the window's ask, the tick always off, the file's `acts` and `setup` never read in (§2.3, :201-205) | The built reader takes a record's own fields (`StandingConfirmation.cs:367-375`); 1a changes it to `scope` and is named so | **Taken as named; holds** |
| **O7** 1a written against a door built differently | §0.2 and §2.2 rewritten against the built door; 1a its amendment | `_hashes` by generation (`StandingConfirmation.cs:236`, `:586-587`); `:257`; `HolderTerminal.cs:27-58`; `TerminalModule.cs:238-274` (`TERMINAL_EXITED`, no judge told); `RefusalLog.cs:104`; `PipeCensus.cs:205`, `:284`, `:287`; the strip sites | **Taken as named; holds**, but §6.1 restates the start line's flags wrongly (finding 5) and 1d's flag breaks D208 point 3's order (finding 2) |
| **C1** deferred hand-overs impersonate the steward | The admitted operation kept in memory; a steward box or handed file consumed only on an exact match (§5.1, :411-415) | `DriverHandOvers.cs:84`, `:91`, `:133` carry `askedBy` into the handed file | **Taken; holds** for the box and the handed file. Row 10's yes goes through a third file (finding 11) |
| **C2** `owner-set` protects a route | As O2 | As O2 | **Does not hold yet** (finding 1) |
| **C3** an admitted act writes after an owner press | A gate per target, `owner-set` checked again under it, the owner's mark set inside it (§3.4 step 7, :317-319; §5.4, :458-463) | `DriverRoutes.cs:44-75` write synchronously | **Taken; holds** for the routes that take the gate. `HELP_APPLY` takes none (finding 1), and the gate does not recheck the appointment (finding 6) |
| **C4** the hand-over answer's form and binding | `{ session, ask }`, no `said`, a quest's own session, the ask and its agent judged at application (row 10, :258) | `SessionsRoutes.cs:400`, `:405` read `session` and default `ask`; a quest session never reaches the chat branch (`:409-426`) | **Taken; holds** |
| **C5** a steward word bypasses the hold | As O5 | As O5 | **Taken; holds** |
| **C6** a complete keep-to-hand protocol for role words | Free words leave D209; the fixed go-on is kept only by the service's conditional step, with nothing held in the process, then checked again and handed by ticket, or withdrawn (§4.1, §4.3) | `DrivenInbox.cs:488` is never on its path | **Taken otherwise; the lifecycle holds**. What it admits does not: the go-on does not require an outage (finding 4) |
| **C7** a suffix leaves the person's attribution | As O1, with the hand-over's reasons in 1e | `HandOverRecords.cs:46`, `:60` | **Taken; holds** |
| **C8** session words become the owner's requirements | As O4 | As O4 | **Taken; holds** |
| **C9** the set-up guard on the applied plan | The check is held inside `SetupPress.ApplyAsync`, immediately before the save (row 12, 1g) | `SetupRoutes.cs:46` recomputes; `SetupPress.cs:170-171` saves `RulesOf(plan)`, and `SetupPressRules.cs:36` is the fork | **Taken; holds** |
| **C10** *Reopen* does not reverse `done` | `done` dropped, the owner's acceptance | As O3 | **Taken; holds** |

### 2. What round 2 introduced

- **Wrong claims.** §3.1 row 3, *Only narrows* (:251), is false where the landing auto-accepts and a plugin pushes (finding 3). §6.1,
  *no hooks, plugins or instruction files (`HolderTerminal.SessionLine`'s flags)* (:479-480), and §6.3, *An agent cannot make itself
  the steward* (:511), are both false: the line loads the user's settings (finding 5). §5.4 and D209 point 6 say *whatever the route*,
  but the design gives a list of routes (finding 1). §2.2 says the tab's printed line tells the holder session that a role was added
  (:177-180), but nothing the panel prints reaches the model (finding 7).
- **New holes.** The target's gate puts a wait between admission and the handler, and a revoke can fall in that wait (finding 6).
  The go-on's conditions admit any quiet park, whatever ended its turn (finding 4). A hand-over chooses *on what* runs (finding 9).
  `RETRY_QUEST`'s one form also forgives strikes (finding 10). Row 10's yes goes through the ask's state file (finding 11).
  `scope: machine` takes in any process's registration (finding 12). Any caller can fill the desk (finding 13).
- **Rows and proofs.** 1d turns on `AppointmentsOffered`, which activates the supervisor ahead of SUPERVISOR1b and 1a-iii. It
  also leaves SUPERVISOR1a-iii with a prerequisite, D201's offering, that D209 makes permanently false (finding 2). 1f is ordered
  before AGENTERRORS1, which it needs (finding 4). 1a's `OwnerSetTests` proves *each route §5.4 names*, which is the list and not the
  claim (finding 1). `HolderTerminalTests` would hold a start line that does not allow the reads §6.1 relies on (finding 8).
- **D209's body.** Point 3 says the steward stops driving only a repository whose landing does not push; row 2 does not say that.
  The Amends list omits D208 point 3, which is where the offering prerequisite and *Appoint again*'s ask live. Point 2's *every role's
  activation* is finding 2 (finding 14).

Every other citation of round 2 held as cited, or within a line or two.

### 3. Findings

1. **`owner-set` is still a list of routes, and the owner's other doors act on the same things unmarked and ungated** (design
   §5.4, :450-463; D209 point 6, :58-59; 1a's `OwnerSetTests`, :583-585). **Blocking.**
   - `HELP_APPLY` (`Person`, `PipeCensus.cs:238`) carries out Ask Daoris's proposals as the owner's own press. It drives, undrives,
     holds, resumes, sets trees on or off, sets the landing (`HelpSettingProposals.cs:80-96`, `:105`), retries a quest (`:61`) and sets
     a repository up (`HelpSetupProposals`), all through the config editor and not through `SET_*`.
   - So, after the owner applies *Stop driving X*, *Trees off* or *Hold X* from Ask Daoris, the steward may drive X again, give it
     trees again, or resume it.
   - D201's own *resume only its own hold* breaks the same way. `Forget` is called only by `SET_HOLD` (`DriverRoutes.cs:62`). If the
     steward holds X and the owner then resumes and re-holds it through Ask Daoris, `_held[X]` and `HeldNow` are both true
     (`StandingConfirmation.cs:721-727`), so the steward's resume releases the owner's hold.
   - Quest-scoped `Person` routes outside the list leave the quest unmarked: `SESSION_GO_AHEAD`, `SUPERVISOR_OVERRULE`,
     `SESSION_QUEUE_EDIT`, `CANCEL_TURN`, `SESSION_GO_ON_NEW`, `SESSION_GO_ON_BRIEF`, `SESSION_PERMISSION_ANSWER` and
     `DISCARD_SESSION_TREE` (`PipeCensus.cs:216-243`). After the owner's go-on in a new session, or a discard of the tree, the steward
     may stop the quest again or try it again.
   - Smallest change: define the mark by the thing acted on, not by a route. Every `Person` route, and every `HELP_APPLY` plan, whose
     payload or plan names that repository, that quest or one of its sessions sets the mark and takes the gate; `HELP_APPLY`'s hold
     door calls `Forget`. Replace "from each route §5.4 names" in `OwnerSetTests` with a census sweep that fails on any `Person` route
     or proposal door that names a repository, a quest or a session and does not mark it, plus a case of an applied Ask Daoris
     *Stop driving* followed by the steward's drive, refused.

2. **1d turns on every role, the supervisor included, before its bounds and activation** (design §10, :562-566 and :602-612; D209
   point 2, :35-36; `StandingConfirmation.cs:205-209`, `:534-541`, `:653-657`; D208 point 3, `D208.md:64`; `TASKS.md:266`, `:269`).
   **Blocking.**
   - `AppointmentsOffered` alone gates the supervisor: `AppointAsync` checks only it and `Roles.Known`, and `JudgeRole` and `Admit`
     check only it.
   - 1d's candidate turns it on with `StewardOffered`. That opens the supervisor's Setup → Driving ask and `SUPERVISOR_ANSWER`
     without SUPERVISOR1b's bounds (still open) and without SUPERVISOR1a-iii's look. D208 point 3 and the flag's own comment require
     both first.
   - D209 also keeps `OfferedInThisBuild` false, so SUPERVISOR1a-iii's stated prerequisite ("D201's offering") can never be met.
   - Smallest change: add a role flag `SupervisorOffered`, alongside `StewardOffered` and false until SUPERVISOR1a-iii. It gates the
     supervisor's ask, its `SET_APPOINTMENT` and its admission. In D209's Amends and §10, give SUPERVISOR1a-iii the containment's look
     in place of D201's offering, after SUPERVISOR1b as now.

3. **Trees on, and a stop or a finish, can publish in a repository whose landing auto-accepts and pushes** (design §3.1 rows 3, 6
   and 7, :251, :254-255; D209 point 3 and *Why*, :37-41, :66-68; `AutoLanding.cs:203-209`; `Landing.cs:1082-1091`;
   `SetupPressRules.cs:64-67`; `AutoLander.cs:103-104`; the terminal-trust design's `stop` row, line 134). **Blocking.**
   - A done reaches the landing only from a tree of its own. In the checkout it is `NoTree` and lands nothing. Under a branch rule
     that auto-accepts (set by the owner, or turned on by the access setup) with a plugin, the landing hands the branch to the plugin,
     which pushes it and opens the pull request.
   - So `steward trees <repo> on` in a repository the owner drives in the checkout turns every later done into an automatic push.
     Row 3 has only row 2's conditions (*As 2*), which leave out `landing-pushes`. Row 1's own reason, *its every landing publishes*,
     applies here word for word.
   - Likewise, a steward's finish of a park whose quest is already `Done` moves it out of `awaiting-person`. `IsParked` is the only
     thing that held its due landing back (`AutoLanding.cs:206`, `Attention.cs:365`), so the finish releases the push. A steward's
     stop of such a session fixes the moment the push is made, with committed but unfinished work.
   - Smallest change: give row 3 row 1's `landing-pushes`, judged on `LandingRules.Effective`. Refuse rows 6 and 7 with
     `landing-pushes` where `AutoLandingRules.Concluded` would answer `due` under a pushing plugin. Drop *Only narrows* from row 3 and
     correct D209's *Why*. Add both cases to 1b's `RoleActTests`.

4. **The go-on is admitted to any quiet park, not one an outage ended** (design §4.2, :336-346; §0.1, :49-51; D209 point 5, :49-51;
   §10 1f, :620). **Blocking.**
   - The conditions name only formal waits: a question, a go-ahead, a word, a hold, a mark. A session that ends its turn by asking
     the owner in prose ("which approach do you want?") meets every one of them.
   - The steward's *go on with your quest where you left off* then lets the agent decide something it asked the owner. Only the owner
     or the supervisor, inside D208's bounds, may answer that.
   - Today the driver reads no outage at all (AGENTERRORS1's own row: no `529`, no *Overloaded*), so Daoris cannot hold "after an
     outage".
   - Smallest change: add a condition, `not-outage`. The record's last turn ended on an error kind that AGENTERRORS1 reads and its
     policy leaves waiting; anything else is refused. Put 1f after AGENTERRORS1's kinds, and add to `RoleGoOnTests` a park ended on a
     prose question, refused.

5. **The holder's start line loads the user's own configuration, so an agent can make itself the steward** (design §6.1, :479-480;
   §6.3, :509-511; `HolderTerminal.cs:27-29`; the supervisor design §2.2, line 307; D156 point 7, `D156.md:42-47`). **Blocking.**
   - `--setting-sources user` loads `~/.claude/settings.json`: its hooks, its enabled plugins and their hooks, its status line and
     helper commands. The user's own instruction file is loaded too.
   - Each hook, plugin, status line or helper command runs as a child of the holder's harness, outside every agent job and with
     `DAORIS_ROLE_KEY` in its environment.
   - Any process of the account can write that file, so a hook planted once makes every steward act with no key leaving the holder.
     D208 wrote this as a precondition, *with no user hooks or plugins*, which 1d's look checks at one moment only.
   - Under `scope: machine`, the stakes are every repository, not one repository's answers.
   - Smallest change, either of two:
     - make the line switch these off itself (an inline `--settings` that turns every hook off and names no plugin, status line or
       helper command), and have `HolderTerminalTests` hold it; or
     - correct §6.1, name the user-level configuration in §6.3's *Not closed*, in the ask's plain sentences and in D209 point 7, and
       have 1d's look and the brief read the user's hooks and plugins.

6. **A revoke falls between an act's admission and its gate** (design §3.4 step 7, :317-319; §5.4, :442-444). Not blocking.
   - Step 7 releases the judge's lock and then waits for the target's gate. Under it, it checks `owner-set` and nothing else.
   - A steward act admitted while another runs on the same target (the holder can run two verbs at once) therefore runs after *Turn
     off*. A hand-over admitted that way can write its box after the revoke's sweep has cleared the untaken ones.
   - The design's *An act already past its gate is not recalled* implies one not yet past it is recalled; step 7 does not do it.
   - Smallest change: under the gate, check again that the generation is live and the holder's hash present (`appointment-changed`
     otherwise). Add the revoke between admission and gate to `OwnerSetTests`.

7. **The role-added line never reaches the holder session** (design §2.2, :177-180; §12 C15 and O9; `TerminalModule.cs:248`).
   Not blocking.
   - What the panel prints goes to the tab's screen, over the harness's interface. It is not the model's input, so the session never
     learns that it holds a new role.
   - Smallest change: every `steward` and `sessions supervise` answer names the holder's current roles and the brief's revision. The
     session reads `roles brief` again when either changes. The printed line is said to be for the owner.

8. **The start line and §6.1 name different permissions** (design §2.2, :174-176; §6.1, :481-483; D209 point 7). Not blocking.
   - §2.2 allows `sessions supervise:*` and `steward:*` beside the read tools.
   - §6.1 and D209 add `roles brief` and the open reads `sessions`, `notices` and `history`. Each of those would prompt in the tab,
     or, if `sessions:*` is allowed, it would cover the press verbs as well (refused `role-terminal`, but no longer narrow).
   - Smallest change: state the line whole, each verb by its exact prefix, and have `HolderTerminalTests` hold that text.

9. **A hand-over chooses *on what* runs, which §3.2 gives the owner** (design row 9, :257; §3.2's spending row, :276;
   `ActorChoices.cs:94-114`; `HandOverQuest.cs:73-87`). Not blocking.
   - The roster is every built or plugin adapter with a profile, and a handed agent overrides the owner's `build` actor order
     (`SET_ACTOR`) for that quest. Leaving the account to Daoris does not leave the agent to the owner.
   - Smallest change: `agent` must be in the repository's effective `build` order (`ActorChoices.Effective`), `not-actor` otherwise.
     Otherwise, say in §3.2 why choosing among the owner's adapters is not *on what*.

10. **`RETRY_QUEST`'s one form also forgives strikes** (design row 8, :256; `StandingChoices.cs:108-131`). Not blocking.
    - The same `{ quest }` releases a stop where the verdict is `Stopped`, and marks the quest forgiven where it is `Exhausted`. The
      second is the bound before the owner (D58).
    - The steward's condition reads the last session and memory, not the verdict. A strike limit lowered after the steward's stop,
      for example, makes its retry a forgiveness.
    - Smallest change: admit row 8 only where `StandingChoices.Retry` would answer `Released` for the steward's own stop.

11. **Row 10's yes goes through the ask's state file, which §5.1 does not authenticate** (design row 10, :258; §5.1, :411-415;
    `SessionsRoutes.cs:390-430`). Not blocking.
    - The approve writes `approved` into the home's ask-state file. The look later turns that into the box, as `HandOverBy.Agent`.
    - The steward's attribution has to travel through a file that §5.1's exact-match rule does not name, and any process can write
      that file.
    - Smallest change: the steward's yes writes the box itself, matched against memory, or §5.1 names the state file among the
      authenticated artifacts. Add a planted `approved` claiming the steward to 1e's `DriverHandOversTests`.

12. **`scope: machine` takes in any process's registration** (design §2.3, :190-192; `Daoris.Service.Http/PersonDoors.cs:215`).
    Not blocking.
    - `POST /api/registry` is a `Driver` door, so it is ungated until PERSONDOOR1g, as §0.6 says of every door. The `unregistered`
      condition proves registration, not that the owner registered it.
    - Smallest change: say it in §6.3 and the ask, or limit the steward to repositories registered before the appointment or by the
      owner's own press this run.

13. **Any caller can fill the desk** (design §6.2, :495-500). Not blocking.
    - `STEWARD_REQUEST` is `Open` with one bound of 100 for everyone, so an agent can make the working session's requests answer
      `desk-full`.
    - Smallest change: a bound per caller process, or say the denial in §6.3.

14. **D209's body against the design** (D209 points 2, 3 and 6, and the Amends list, :30-36, :37-38, :58-59, :82-86). Not
    blocking.
    - Point 3's "drive and stop driving a registered repository whose landing does not push" disagrees with row 2.
    - The Amends list names D208 points 2 and 7 but not point 3. Point 3 is where *Appoint again* "opens the same ask naming the scope
      found" and the offering's prerequisites ("only after D201's offering, the bounds (1b) and the activation proof") are decided,
      and D209 changes both.
    - Point 6's *whatever the route* stands only after finding 1.
    - Smallest change: make the three edits.

**14 findings, 5 blocking.**

**Verdict: accept after named changes the steward can check without another read.** The foundation holds in its shape. A role is
admitted only by a key whose hash lives in memory and is proved over the hello's nonce. The holder runs no branch's code and the
working session holds nothing. D201's four presses become the steward's acts, not a second door. `done` stays the owner's. The
steward's acts write records of their own, and the go-on is fail-closed and kept off the ask. Round 1's seven Opus and ten Codex
blocking points are taken, all but `owner-set`'s, which needs finding 1. Five bounded changes remain, each checkable at the lines
cited:
1. Mark `owner-set` by what was acted on, through every owner door, `HELP_APPLY` and its `Forget` included, with a census sweep.
2. Give the supervisor a flag of its own, so 1d cannot activate it, and give SUPERVISOR1a-iii a prerequisite that can be met.
3. Add `landing-pushes` to trees on, and to a stop or finish that would make a pushed landing due.
4. Admit the go-on only after an error AGENTERRORS1 reads, and order 1f after it.
5. Either neutralise the holder line's user-level hooks and plugins, or state that path honestly as an open limit and check it at
   activation.

Findings 6 to 14 are text or condition changes the build rows can take as they land.
