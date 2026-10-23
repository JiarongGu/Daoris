# SUPERVISOR1 and QUESTIONUI1: the reads of round 1 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-supervisor-design.md` and D208 at
`fa3124699`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on
the design's own tree. Both ask for another round: Codex with 18 findings, 14 blocking; Opus with 21, 11 blocking. Both find the
foundation's shape sound (a role appointed only in the window, recorded as itself, judged under D201's lock and caller look,
overrulable, off by default, on this machine alone), and both find QUESTIONUI1a buildable on its own once the card's details are
settled. Round 2 answers each finding in the design's reads table.

## Codex

Codex's read of round 1.

### 1. Claims about the code

The proposal’s basic account of `question_ask`, `QuestionFiles`, `SESSION_QUESTION`, choice presses, terminal Confirm, and D201’s unoffered grant matches the code. MODELROLE1b provides actor reporting; the proposed dependency on later choices rows is necessary.

Three corrections matter:

- `DriverContinuations.HandedToResume` selects words carried into a **new session**, not the prompt resuming the existing conversation. The proposed delivery change names the wrong path and conflicts with its own prohibition on carrying supervisor answers into new sessions. Finding 1.
- COWORK1e counts **helpers and second opinions**, not helpers alone. Finding 13.
- D201’s caller check establishes absence from the jobs it can inspect. It does not identify an appointed holder or exclude every process acting for a Daoris agent. Applying its safety argument to unrestricted answer text requires a new argument. Findings 2–3.

### 2. The mechanism’s holes

The appointment is appropriately explicit, local, off by default, and made through a window-only route. The refusal of uncovered role acts without producing Confirm cards follows the owner’s direction.

The principal gap is authority. A terminal appointment currently appoints an entire class of callers. D201 expressly leaves brokered starts, repository hooks, older headless agents, and account-level interference outside its containment. Its management presses were justified partly by their narrow effects. `SUPERVISOR_ANSWER` instead carries instructions whose consequences depend on their meaning.

The supervisor can also choose an option whose `does` authorizes an owner-only act, or put that authorization in its words. An open-go-ahead check and an optional agent mark do not prevent this. The holder must exercise judgment, but the design needs explicit instructions for both holder and receiving session that distinguish delegated decisions from owner authorization, plus adversarial measurement of that distinction.

`From: supervisor` correctly excludes the answer from `Persons` and `Session.Answer`. That alone does not preserve its identity, reasons, question, or hold across every delivery and recovery path. Nor does the planner-only hold establish the promised overrule boundary.

Out-of-bounds questions already have an owner-facing park. The missing cases are durable deferral, failure, timeout, uncertain completion, and an overruled question that the supervisor immediately encounters again.

### 3. The rows

| Row | Can it land alone, in this order? | Does its proof establish its claim? |
|---|---|---|
| **SUPERVISOR1a** | **No, as written.** It requires refusals and safeguards assigned to 1b, and its usable door waits for D201’s outstanding offering proof. It is also substantially larger than “small.” | Not yet: missing alternate-entry, recovery, mixed-author, rollback, and concurrent-admission cases. |
| **SUPERVISOR1b** | Its fields can be additive, but its essential bounds must precede offering 1a. | Shared-file and refusal tests are useful; mixed-answer counting and persistent deferral remain unspecified. |
| **QUESTIONUI1a** | Yes, independently, after settling draft ownership and question replacement. | Unit and axe checks do not prove layout inside WAITPANEL1. |
| **QUESTIONUI1b** | After a durable supervisor decision and question snapshot exist. | Molecule stories cannot prove that the integrated card remains reachable after answering or resuming. |
| **SUPERVISOR1d** | Yes as measurement tooling, once the actual input, policy, and result protocol are fixed. | Zero answered O cases is insufficient. Finding 12. |
| **SUPERVISOR1c** | After the named D205 choices rows and a holder-bound result protocol. | A successful stub does not establish exclusive authority, late-result rejection, deferral, or bounded retries. |
| **SUPERVISOR1e** | Repository scope can be extended later, but overlapping appointments need a resolution rule first. | Record round-trips alone do not prove effective holder selection or revocation. |

The first usable row should remain the steward’s own door, without per-answer Confirm. Make it a complete terminal-only slice with its essential bounds, durable decision, and overrule contract; defer the generalized extension points and actor execution.

### 4. Findings

Here, **S** means `docs/2026-10-24-supervisor-design.md`.

1. **Blocking — the delivery change targets the wrong kind of resume.**  
   **Location:** S:276, S:278; `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/DriverContinuations.cs:784`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Conversations/Domain/ResumeAsk.cs:59`.  
   `HandedToResume` feeds a new session. Adding supervisor words there would carry them across when an owner word accompanies them, despite the “own session” rule. Actual same-conversation delivery uses `ResumeAsk` and `SaidWordView.Handed`. **Smallest change:** name and extend that path; keep supervisor words excluded from new-session delivery even in mixed queues. Prove both doors and both mixed and supervisor-only fallbacks.

2. **Blocking — the terminal appointment does not appoint an exclusive holder.**  
   **Location:** S:135, S:140, S:255; `src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StandingCaller.cs:150`; `docs/2026-10-23-terminal-trust-design.md:238`.  
   Any readable caller outside the listed jobs passes. That includes unrelated external agents and processes started through the documented escape routes; Daoris’s own git hooks are another route. The appointment’s wording acknowledges external agents but omits these materially different callers. **Smallest change:** retain D201 as a denial check and add holder-bound admission issued through appointment. State the remaining account-level limits explicitly. Prove denial for a contained agent, plugin, partner, brokered caller, and repository hook.

3. **Blocking — ordinary say requests can manufacture supervisor authorship.**  
   **Location:** S:267; `src/Daoris.Service/Daoris.Service.Http/Program.cs:1487`; `src/Daoris.Service/Daoris.Service.Core/Sessions/SessionLedger.cs:826`.  
   The ordinary say endpoint accepts `From` from its body; `Known` is a vocabulary check. Simply adding `supervisor` makes that authorship available without the proposed role route’s appointment, question, bounds, or reasons. D156’s startup integration is also still incomplete. **Smallest change:** reject supervisor authorship on ordinary say requests and admit it only through a dedicated operation carrying trusted role admission. Test the bypass directly.

4. **Blocking — an answer can convey owner-only authorization without an open go-ahead.**  
   **Location:** S:170, S:203, S:215–227; `docs/decisions/D135.md:28`.  
   “Push now,” “use the owner’s account,” or “delete the old data” can arrive as an offered choice or free words without any `go_ahead_ask` record. A refused go-ahead is no longer open either. The bounds also omit D135’s explicit reservation where repository documents require the person’s agreement. **Smallest change:** add that reservation; hand the holder relevant go-ahead decisions; specify in the holder’s policy and receiving prompt that supervisor answers cannot grant or override owner authorization. Measure disguised authorization in labels, `does`, words, and reasons.

5. **Blocking — there is no prompt-injection contract.**  
   **Location:** S:180–195, S:199–206.  
   The question, recommendation, closing report, repository documents, and retrieved knowledge are inputs from potentially interested agents. The design specifies their content and order but no fixed supervisory policy or trust separation. A question can instruct the supervisor to treat its text as an appointment or an owner instruction. **Smallest change:** define the fixed role policy, clearly delimit attributed inputs as evidence, and prohibit those inputs from changing authority, tools, or scope. Add injection cases to the measurement gate.

6. **Blocking — essential decision data lives only in a separately written event.**  
   **Location:** S:267–277; `src/Daoris.Desktop/Daoris.Desktop.Driver/Sessions/Events/SessionEventLog.cs:399`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Conversations/Domain/ResumeAsk.cs:118`.  
   The queued word holds label and words; the event holds reasons, holder, and timing. There is no recovery contract for a crash between those writes or an unreadable event log. Today’s choice lookup returns an empty map on read failure, and resume events preserve only known author fields. **Smallest change:** persist one authoritative decision envelope containing question identity, choice, words, reasons, holder identity, appointment generation, and release time. Derive delivery and presentation from it; refuse delivery when required provenance cannot be read.

7. **Blocking — the five-minute hold is not enforced at admission to execution.**  
   **Location:** S:304–315; `src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/DriverContinuations.cs:258`, `:319`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Conversations/Domain/Continuation.cs:258`.  
   A plan can be made from an eligible queue, then a fresh supervisor answer can arrive before the move. The post-move read takes all current words. A failed read instead falls back to the earlier snapshot, potentially including an overruled word. Also, “only the supervisor’s” lets another agent’s or Daoris’s accompanying word defeat the hold, although only owner words are explicitly allowed to accelerate it. **Smallest change:** atomically claim the eligible decision identities when moving to working, and fail closed on an unreadable confirmation. Test these interleavings and mixed-author queues.

8. **Blocking — one question can receive competing answers, including after Overrule.**  
   **Location:** S:170–176, S:308–312, S:432–435.  
   The question file remains while answers are held. No refusal prevents another invocation from keeping another answer under a fresh press identity. After Overrule, the same question remains eligible for the supervisor, contradicting “stands for the owner.” **Smallest change:** allow one pending decision per question, make retries idempotent, and mark an overruled question owner-only until replaced. Prove duplicate calls, competing calls, and immediate re-answer after Overrule.

9. **Blocking — rollback can execute a pending answer without its hold or policy.**  
   **Location:** S:151–158, S:267, S:304; `src/Daoris.Desktop/Daoris.Desktop.Driver/Quests/Domain/Planner.cs:1461`.  
   The rollback discussion covers appointments only. An older loop already reads `said` and proceeds with waiting words; it knows neither supervisor release times nor the new attribution paragraph. Ignoring `appointments` does not protect a pending answer already in that queue. **Smallest change:** keep unreleased decisions in storage older builds cannot consume, and specify compatibility at release. Prove this with the older reader’s actual statements.

10. **Blocking — the actor’s exclusive result door and deferral protocol are undefined.**  
    **Location:** S:136, S:171, S:463–466.  
    “Only it is offered” is not caller authentication. The actor has only `supervisor_answer`, yet its proof requires deferral. Row 1a’s handler also explicitly accepts only a covered terminal. **Smallest change:** define an internal answer-or-defer result protocol bound by trusted code to one actor attempt, question, and appointment generation. Recheck appointment validity when accepting the result; reject another session’s result and late results after revoke, replacement, or question change.

11. **Blocking — supervisor attempts have no durable completion and retry rule.**  
    **Location:** S:234–237, S:339–344, S:463–466.  
    A recorded park can remain unchanged after deferral, timeout, failure, or an uncertain result. Nothing says what prevents the next look from starting the supervisor again. One turn and ten minutes bound one attempt, not repeated attempts or total token use. **Smallest change:** persist attempt outcomes per question; hand unresolved failures to the owner with a visible reason; permit retries only through a named rule or explicit act. Measure attempts and tokens per resolved question, including failures and repeated reads.

12. **Blocking — the measurement gate is weaker than D135’s existing gate.**  
    **Location:** S:458–461; `docs/decisions/D135.md:221`; `tools/question-bench.mjs:462`.  
    D135 treats answers to both **O and R** cases as wrong and checks claimed sources against the recorded source. The proposed acceptance condition names only O answers. A model could answer repository-required approvals, cite false authority, or defer everything and still meet that condition. **Smallest change:** retain O and R reservations and source checking; add the supervisor’s actual answer/defer oracle, injection and authorization cases, a nonempty successful-answer requirement, and explicit failure/no-verdict reporting.

13. **Blocking — the separate answers-alone counts do not compose.**  
    **Location:** S:241–244; `src/Daoris.Desktop/Daoris.Desktop.Driver/Conversations/Domain/AnswersAlone.cs:24`, `:31`, `:95`.  
    COWORK1e counts helpers **and opinions**. A prompt containing supervisor and helper answers is neither supervisor-only nor entirely solicited under today’s predicate, so it can evade both proposed checks. **Smallest change:** define one explicit mixed-author counting rule and its reset events. Test mixed prompts, alternating answer kinds, restart, and line continuation; do not count a role answer as owner participation.

14. **Blocking — the first row depends on safeguards scheduled later.**  
    **Location:** S:428–447; `TASKS.md:255`; `src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StandingConfirmation.cs:136`.  
    Row 1a promises all §3.1 refusals, but owner marking and the line bound arrive in 1b. It offers a door only when D201 is offered, which remains gated on outstanding installed proof. Its proof list also contains no installed supervisor-door proof. **Smallest change:** move essential bounds and deferral into the first usable terminal slice; name D201’s offering proof as a prerequisite or distinguish an inert landing from the usable row. Replace “small” with a realistic slice and prove the steward’s answer end to end without Confirm.

15. **Non-blocking — the new lifecycle is missing from attention and card integration.**  
    **Location:** S:291–296, S:367–370; `src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:280`; `src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/NeedsAPerson.cs:749`; `src/Daoris.Web/src/work/WorkFrame.tsx:514`.  
    Correctly excluding supervisor words from `Answer` leaves the park counted as needing the owner. After the run starts, the question file is cleared and the current card stops reading it, so a “gone on” molecule story does not establish the integrated state. **Smallest change:** expose explicit pending/deferred/overruled/consumed role-decision state to attention and the page, retaining the required question snapshot. Show an earliest release time separately from the observed execution time.

16. **Non-blocking — the card introduces another competing answer field inside the scroll region.**  
    **Location:** S:364–373; `src/Daoris.Web/src/work/conversation/SessionBox.tsx:143`; `docs/decisions/D185.md:355`.  
    The foot would contain the card’s words and Send plus the existing composer’s words and Send; go-aheads still use the latter. The new card field also sits inside the bounded wait content, undoing WAITPANEL1’s reason for keeping typing controls in sight. **Smallest change:** specify one foot draft and a visible answer footer, with explicit ownership of attachments and go-ahead words. Verify six long choices at the panel floor in a rendered browser.

17. **Non-blocking — changed-question and accessibility behavior are inconsistent.**  
    **Location:** S:361, S:380–383, S:451–453.  
    Recommendation text is required in the accessible name in one section and the description in another. Preserving selection after `question-changed` has no question-identity rule; a retained label can become a selection on a different question. **Smallest change:** choose one accessible-name contract; retain drafts against the old question identity, and require fresh selection for a replacement question. Test Ctrl+Enter under every disabled condition and verify focus, scrolling, and accessible output beyond axe.

18. **Non-blocking — overlapping appointments have no effective-holder rule.**  
    **Location:** S:248–250, S:468–469.  
    A workspace appointment and repository appointment may name different holders. “Either covers” allows both without defining who starts, whose result wins, or what revoking one means. **Smallest change:** define a single effective appointment and revision for each question—such as repository override—or prohibit conflicting overlaps. Test holder changes, both revocations, and late results.

**18 findings; 14 blocking.**

**Verdict: another round.** The owner-appointed supervisor and improved option selection are sound directions, and the steward’s own door should retain its freedom from per-answer Confirm. The next round needs a holder-bound authority contract, durable decision and execution boundary, composed bounds, and a self-contained first row. Those changes affect the foundation being reviewed and warrant another read.

## Opus (foundation)

The independent Opus 5.5 reader's foundation read of round 1. Its paths are relative to the design's tree.

## The foundation read of D208 (SUPERVISOR1, QUESTIONUI1), round 1

Paths are relative to the design's tree, read at `fa3124699`. I changed nothing and ran nothing.

### 1. Claims about the code

**These hold** (each read at its lines):
- `QuestionTools.cs:17-41`; `QuestionBox.cs:24-36`, `:50`, `:104-122`.
- `QuestionFiles.cs` (`Read`, `Settle`, `Clear`, and `QuestionWords` at `:179-198`).
- `SessionsRoutes.cs:365-387`; `PipeCensus.cs:192`, `:229`, `:267-269`.
- `ConversationRoutes.cs:116-139`; `SessionWords.cs:179`, `:373-453`.
- `Planner.cs:355`, `:650-666`, `:1461-1472`.
- `Sessions.cs:386-408`; `Driver.cs:2144-2152`, `:4143-4144`.
- `SessionLedger.cs:989-1030`, `:1016-1023`, `:1625-1638`; `DriverContinuations.cs:247-258`.
- `StandingConfirmation.cs:28`, `:136`, `:241-269`, `:370-380`, `:399-440`, `:462-499`, `:521-535`; `InstancePipe.cs:603-613`.
- `ParkAnswer.tsx:19-55`, `:107`; `AwaitingPerson.tsx:136-141`; `SessionBox.tsx:151-152`; `WorkFrame.tsx:1024-1051`.
- `attention.stories.tsx:823-841`; `Attention.cs:228-236`; `NeedsAPerson.cs:259-265`.
- `StandingAnswer.cs:25-48`; `SessionEventLog.cs:399-419`; `NoticesStateSlice.cs:189`.
- The queue really does leave out a word with a `from` (`SessionWords.cs:1068-1070`), and DRIFT keeps only `Persons` words (`Program.cs:1499`).

**These are wrong or incomplete:**
- **The handing seam (§0.2, §6.1).** `HandedToResume` (`DriverContinuations.cs:772-784`) and `WhileItWaited` (`Adapters.cs:724-735`) belong to the D79 resume, which opens a new session. A park answered in its own session is handed each word raw, through `SaidWordView.Handed` (`Planner.cs:660`) and `ResumeAsk.Blocks` (`ResumeAsk.cs:64-65`). F4.
- **"Judged as a fifth press" (§0.4, §11).** The pipe asks the judge only for `Person` routes. Any class other than null, `Window` or `Person` runs at once, as an open route does (`InstancePipe.cs:539-545`, `:557-562`, `:597-601`). The judge is off whenever no press is granted (`StandingConfirmation.cs:283`, `:406`). F5.
- **The service's say door (§0.2).** It takes `from` from any caller's body (`Program.cs:1481-1490`), and it is ungated until PERSONDOOR1g. §0.2 does not say so. F2.
- **The park's answered note (§0.2).** Not mentioned at all. `SayAsync` writes *Answered: …* onto a park's note (`SessionLedger.cs:878`, `:1153-1164`). That note travels (`:793-794`), and a take-back leaves it in place (`:984-986`). F3.
- **The terminal today (§0.3).** "It waits for the window's Confirm" holds only while Daoris runs. With none running, `sessions say` acts alone, with no card (`SessionsCommand.cs:495-499`, `:507`). The request folder also takes words as the person's (terminal-trust design §0.5). The route sits at `InstanceRoutes.cs:174-175`. F12.
- **The stories (§0.5).** `ParkAnswer` is also drawn with a question in `work/waits/WaitPanel.stories.tsx:28`, `:87`. F12.
- **The bench (§0.6).** It holds O **and** R as the person's own ("any answer to one is an answer in their place", `tools/question-bench.mjs:79`). Its fixtures are 6 cases, not the 46. F9, F18.
- **The countdown (§8.1).** `Elapsed` counts up. The countdown atom is `Remaining` (`design/phases.tsx:75-96`). F17.
- **The actor's tool (§2.2).** "A tool only it is offered" does not hold: a connector speaks for whatever session `DAORIS_SESSION_ID` names (`IntakeScope.cs:28-33`, `QuestionTools.cs:36-38`). F11.

### 2. The mechanism's holes

**Who else can answer as the supervisor.** The design carries over D201's caller look but not the premise that made it safe. D201 accepted these holes only because "every press only narrows":
- the one-command escapes (`explorer.exe <program>`, `wsl.exe`, a scheduled task, WMI, COM);
- git hooks run by Daoris's own git outside every job;
- an older headless loop's agents;
- the record written while Daoris is down.

The sources are `terminal-trust-design.md:241-262` and `D201.md:11-12`, `:60-63`. A supervisor's answer steers a session with the owner's delegation, so the premise no longer holds. A parked session's escaped watcher could answer its own question as "the supervisor, on your behalf". The appointment's ask (§5) also drops D201's sentence "unless one breaks out of Daoris's hold" (F1).

There are three further ways in:
- **The service door.** Once `SaidFrom.Known` takes `supervisor`, any process can post a supervisor's word straight to the service's say door, with no appointment and no judge. The driver would then wrap it in the delegation's framing (F2).
- **Alone paths.** Nothing says `sessions supervise` has no alone path, or that the request folder refuses a supervisor's answer (F2).
- **The actor.** The actor's tool is open to any connector that names the actor's session (F11).

Plugins and partners are contained, so the escape is their only way in.

**Owner-only decisions carried by words or a choice.**
- Row 1a holds structurally only one thing: an open go-ahead refuses the answer.
- A go-ahead put as a question is not held. The session is told the answer is "on their behalf", and nothing in the handed text says it grants no go-ahead, sign-in, access, spending, push, publication or deletion (F8).
- What actually holds those acts is the harness's deny rules (`Permissions.cs:162-169`) and `go_ahead_ask`. Under a full-access setup, the words are all that stands in their way.
- The R class is missing from the bounds: D135 point 7, a repository whose own documents say to ask the owner (F9).

**Recorded or shown as the owner's.**
- The note's *Answered:* line has no author, travels to teammates, and outlives an overrule (F3).
- The page draws every `from` other than `daoris` as *You* (`ConversationView.tsx:501`).
- `Show` writes waiting words with origin `person` (`SessionWords.cs:999-1011`).
- `ResumeAsk.Opening` and `HarnessCapture.Opening` mark words only by `Persons` and `Daoris` (`ResumeAsk.cs:118-133`, `HarnessCapture.cs:30-40`).
- `OpeningNoted` falls through to "your answer is" (`Continuation.cs:405-414`).
- The say door's own sentences call the words "your words" (`SessionLedger.cs:1124-1136`).
- The design names only `WentOnWith` (F4).

**The overrule's race.**
- The hold applies only where the supervisor's are the *only* waiting words. A helper's last words, which are kept on the parked asker (`DriverHelpers.cs:303-310`), or Daoris's own words, carry the record on at once.
- Only `WordsOn` is named. `SessionGroups.cs:1229-1236` and `AnswersAloneHold.cs:57` read the same go-on (F6).
- An overrule does not stick: nothing refuses a fresh answer to an overruled question whose file is still on disk, or a second answer while one is held (F7).
- If the owner answers the same question while the supervisor's is held, the session gets both (F16).
- The take-back's own race with the move to working is held by SAIDBACK1e, as cited.

**How an out-of-bounds question reaches the owner.** As today, which is safe. But during the hold the park stays a *What needs you* row, because `answer` reads only the person's words (`NeedsAPerson.cs:749-750`, `Sessions.cs:280`). Every park still raises its own notice. Where *left to you* is kept is not named (F15).

**Prompt injection.** The design does not address it.
- The question, its choices and their `does`, and the session's message, tree and commits are all written by the asking agent. They are handed to the steward, a high-privilege agent, as a standing workflow, and to the actor as its prompt.
- No rule says these are claims rather than instructions.
- The verb prints them unfenced.
- The bench has no adversarial cases (F10).
- With choice-only answers (F1's change), content can reach the session through the role only as one of its own offered labels.

**The appointment's grant, revoke and storage.** These are sound in shape: window-only, one lock, bound to this machine, off by default. But "a fifth press" contradicts "separate ticks", and the build must keep `appointments` through `Write` and the revoke (F5). The record can be forged while Daoris is down; that is the account's reach, unsaid for a widening act (F1).

**Tokens.**
- A terminal holder costs the steward's own runs, plus polling, since no notice reaches a terminal (F20).
- The actor costs one turn per question it looks at, answered or deferred.
- The bound on the ask's words in its prompt is unnamed.
- 1d's corpus does not persist, since question files are deleted as each run opens (F18).

**The card.** Its shape is right: native radios, nothing preselected, one *Send*, the pick marked apart from the recommendation. But:
- in the box it adds a second text field above the box's own (§8.1 part 4, lines 372-373);
- §8.1 and §8.2 disagree on whether *Recommended* is in the row's accessible name;
- the countdown should be `Remaining` (F17).

### 3. The rows

**SUPERVISOR1a** rests only on what is built: D201's judge and caller look, part (i)'s containment, SAIDBACK1a, PARKVIEW1, COWORK1b, `StandingLine`. It lands dark until D201 flips `OfferedInThisBuild`, which the design says.

It is not small. It spans four layers and about twenty parts. It is also offered whenever D201 is, before 1b's line count and the `owner` mark exist (F13, F14).

Its proof would not catch:
- a forged service word (F2);
- the note (F3);
- the same-session handed text or a new-session carry (F4);
- the `Role` class falling open in `Accept` (F5; `PipeStandingTests` would catch it only if written for it);
- other words cutting the hold (F6);
- a re-answer after an overrule (F7).

The best split is in two:
- **1a-i, the word as the supervisor's on the record:** every author reader, the note line, the handed text, the hold, the overrule. Provable with no pipe.
- **1a-ii, the door:** the appointment, `Role`, the judge, the verb, Setup's row, the notices.

**The other rows:**
- **SUPERVISOR1b** builds alone. Its count should name how it crosses a hand-over and that neither count resets the other (`AnswersAlone.cs:89-95`).
- **QUESTIONUI1a** depends on nothing in this design. It could land first and gives the owner the "better option selection" at once.
- **QUESTIONUI1b** is ordered right.
- **SUPERVISOR1d**'s proof rests on a corpus that is not kept, and scores O but not R (F9, F18).
- **SUPERVISOR1c** must answer F11 first.
- **SUPERVISOR1e**'s 0-minute window contradicts §12 (F19).

### 4. Findings

1. **Blocking. An agent Daoris runs can answer as the supervisor by leaving its job.** §2.2 (lines 133-140) and §5 (lines 252-257) import D201's caller look without D201's "only narrows" premise (`terminal-trust-design.md:241-262`; `D201.md:11-12`, `:60-63`). The same goes for git hooks run by Daoris's own git, an older headless loop's agents, and a record written while Daoris is down. **Change:**
   - add a "what it does not bound" section re-weighing each of these for a widening act;
   - put D201's "unless one breaks out" sentence in the appointment's ask;
   - in row 1a, a terminal holder answers one offered choice with reasons, the session is handed only the chosen label, the reasons are shown to the owner only, and words are deferred to a later row;
   - the answer's event and notice name the pipe client as the system names it, as the confirmation card does.
2. **Blocking. Anyone can post a supervisor's word at the service's door.** `Program.cs:1481-1490` takes `from` from the body; `SessionLedger.cs:826`; design §6.1 line 267. The verb's alone path (`SessionsCommand.cs:495-499`) and the request folder are further ways in. **Change:**
   - the driver treats a word as the supervisor's (its framing, hold, mark and notice) only where an event written by the judged `SUPERVISOR_ANSWER` handler names that word's id;
   - a bare `from: supervisor` word is handed and shown as unattributed;
   - `sessions supervise` has no alone path, and the request folder takes no supervisor answer;
   - §0.2 says the door stays forgeable until PERSONDOOR1g.
3. **Blocking. The park's note shows the answer with no author, and keeps it after an overrule.** `SessionLedger.cs:878`, `:1153-1164`, `:793-794`, `:984-986`; `Note.tsx:64-69`; `noteLines.ts:810`; `api.ts:523`. **Change:**
   - a coded `ledger.answered-supervisor` line, its English naming the supervisor, in place of `ledger.answered`;
   - an overrule appends a coded `ledger.overruled` line;
   - `NoteBy` gains `supervisor`;
   - tests in `SessionLedgerTests` and the `Note` test.
4. **Blocking. The handed paragraph is placed in the new-session resume, and other readers of a word's author are missed.** The seams are `DriverContinuations.cs:772-784` and `Adapters.cs:724-735`. The missed readers are `Planner.cs:660`, `ResumeAsk.cs:64-65` and `:118-133`, `HarnessCapture.cs:30-40`, `Continuation.cs:405-414`, `SessionWords.cs:999-1011`, `ConversationView.tsx:501`, `conversation.ts:446`, `:451` and `:503`, `SessionLedger.cs:1124-1136`, and `AnswersAlone.cs:89-95`. Placed there, the answer would reach a new session, against §6.1's own next bullet and §12. **Change:**
   - `HandedToResume` stays `Persons || Daoris`;
   - the framed paragraph is composed in the same-session go-on (`Handed`, `Blocks`) for a word F2's event names;
   - the waiting event is never origin `person`;
   - every listed reader gets the supervisor's case, held by one sweep test.
5. **Blocking. The judge and the pipe are under-specified, and `Role` would fall open.** §11 line 430 ("a fifth press") contradicts §2.3 lines 156-158 and §12 line 476. See `StandingConfirmation.cs:283`, `:406`, `:293-298`, `:352`, `:370-380`; `InstancePipe.cs:539-545`, `:597-601`; `RefusalLog.cs:95`. **Change:**
   - `Accept` gains a `Role` branch before the open path, refusing anything uncovered and never asking a card;
   - the role is judged by its own method under the same lock and caller look, independent of `_presses` and `IsOn`;
   - `supervise` is never in `Built` and never accepted by `Set`;
   - `Write` and the revoke keep `appointments`;
   - the handler reads a role-specific door flag, not `PipeDoor.Standing`.
6. **Blocking. Any other non-owner word cuts the overrule window.** §6.3 lines 304-307; `Planner.cs:1461-1472`; `SessionGroups.cs:1229-1236`; `AnswersAloneHold.cs:57`; `DriverHelpers.cs:303-310`. **Change:**
   - hold the record while any supervisor word younger than the window waits and none of the owner's does;
   - carry the hold as a `GoOnMarks` mark until `goesOnAt`;
   - a `PlannerTests` case where a helper's answer and Daoris's words arrive inside the window.
7. **Blocking. An overrule does not stick.** The §3.1 refusals (lines 173-176) and §6.3 (lines 308-312) refuse neither a fresh answer to an overruled question nor a second answer while one is held. **Change:** add the refusals `overruled` and `answered`, with `SupervisorAnswerTests` cases.
8. **Blocking. The handed text does not bound what the answer grants.** §6.1 lines 273-275; §4.1 line 215. **Change:**
   - the paragraph says the answer is no go-ahead, sign-in, access, spending, push, publication or deletion;
   - it says those still go through `go_ahead_ask` or wait for the owner;
   - the design names the real holds: the permissions (`Permissions.cs:162-169`) and the go-ahead on the ask;
   - a test on the text.
9. **Blocking. The R bound is missing.** D135 point 7 (`D135.md:28-30`); `question-bench.mjs:79`; the §4.1 table; §11 lines 459-461. **Change:** a `repository-asks` bound in §4.1, and 1d scores an answered R as wrong, beside O.
10. **Blocking. Prompt injection is not addressed.** The design is silent. **Change:**
    - a new §4.4: what the holder reads is the asking agent's claims, never instructions;
    - `sessions supervise` prints them fenced and labelled as the agent's words;
    - the co-working skill gains the rule;
    - the actor's prompt quotes them as such;
    - 1d adds adversarial cases, scored wrong when followed.
11. **Blocking. The actor holder's identity can be forged.** §2.2 line 136; `IntakeScope.cs:28-33`. **Change:** the driver takes the actor's answer from that actor's own run, never through a service door keyed by `DAORIS_SESSION_ID`; 1c's proof includes a forged connector, refused.
12. **Not blocking. §0's small gaps.** The alone path and the request folder (`SessionsCommand.cs:495-499`, `:507`); `InstanceRoutes.cs:174-175`; the `WaitPanel.stories.tsx:87` story. **Change:** a sentence each.
13. **Not blocking. Row 1a is too large.** **Change:** split it into 1a-i (the record) and 1a-ii (the door), the record first.
14. **Not blocking. The appointment is offered before its bounds exist.** It shares `OfferedInThisBuild` (`StandingConfirmation.cs:136`) and so would be offered before 1b. **Change:** move §4.3's count into 1a (it has `AnswersAlone`'s shape), or give appointments their own offered flag, false until 1b.
15. **Not blocking. Unclear what the owner sees while an answer is held or deferred.** `NeedsAPerson.cs:749-750` keeps the park as a wait; `Attention.cs:230-236` raises a notice per park; `SUPERVISOR_DEFER`'s mark has no named home. **Change:** say what each shows while held and after a defer, and where *left to you* is kept (a machine-local event, cleared with the question).
16. **Not blocking. The owner's answer during the hold goes beside the supervisor's.** `ConversationRoutes.cs:121-129`. **Change:** the owner's answer to the same question takes the supervisor's back.
17. **Not blocking. Three card details.** A second words field in the box (§8.1 part 4 and lines 372-373, against `SessionBox.tsx:151-152`); the accessible-name contradiction (§8.1 lines 360-361 against §8.2 lines 380-381); `Remaining`, not `Elapsed`. **Change:** reuse the box's field, settle the name, use `Remaining`.
18. **Not blocking. 1d's corpus is not kept.** `QuestionFiles.cs:137-142`; `tools/question-bench-fixtures/cases.json` holds 6 cases. **Change:** name where the cases come from.
19. **Not blocking. The window can be set to 0.** SUPERVISOR1e allows 0 minutes (§6.3 lines 304-305), which §12 line 478 rejects. **Change:** a floor above 0.
20. **Not blocking. The holder's discovery and the cost are unsaid.** No notice reaches a terminal, so the steward polls. The actor's cost is per question looked at, and the ask's words are unbounded in its prompt. **Change:** a listing of waiting questions in appointed repositories, and both costs and bounds said.
21. **Not blocking. The D135 amendment is worded around the fact.** `D208.md:63-64`; §10 lines 413-415. **Change:** say the steward is a model answering unmeasured by the owner's direction, rather than "not in the owner's place".

21 findings, 11 blocking.

**Verdict: another round.** The foundation's shape holds and should be kept: a role appointed only in the window, recorded as itself, judged under D201's lock and caller look, overrulable, off by default, on this machine alone. But three things at its core fail as written. First, who can answer as the supervisor: an escaped or older agent, any caller of the service's say door, or any connector naming the actor (F1, F2, F11). Second, how the answer is recorded and handed: an unattributed note that travels and outlives an overrule, and a handing seam that carries it into a new session (F3, F4). Third, whether the overrule is real: other words cut the window, and an overruled question can be answered again at once (F6, F7). These interact. F1's change to choice-only answers for a terminal holder reshapes §3.1, §3.3, §6.1, §8.1 part 6 and row 1a, and that is a design choice the author should make and justify, not an edit the steward can check alone. Round 2 can be narrow (§0.2-§0.3, §2.2-§2.3, §3.1, §4, §6.1, §6.3, §11 row 1a). With F1 to F11 taken as named, I expect it to be acceptable without a further foundation read.
