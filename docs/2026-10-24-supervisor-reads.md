# SUPERVISOR1 and QUESTIONUI1: the reads of rounds 1 and 2 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-supervisor-design.md` and D208 at
`fa3124699`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on
the design's own tree. Both ask for another round: Codex with 18 findings, 14 blocking; Opus with 21, 11 blocking. Both find the
foundation's shape sound (a role appointed only in the window, recorded as itself, judged under D201's lock and caller look,
overrulable, off by default, on this machine alone), and both find QUESTIONUI1a buildable on its own once the card's details are
settled. Round 2 answers each finding in the design's reads table. Round 2 was read the same way at `607f12863`: the Opus
foundation reader finds the foundation holds and would accept after three named changes (12 findings, 3 blocking); Codex asks
another round (15 findings, 10 blocking) and finds QUESTIONUI1a buildable once its findings 14 and 15 are settled. Round 3
answers both.

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

## Codex, round 2

Codex's read of round 2, at `607f12863`: 15 findings, 10 blocking; another round.

### 0. Round-1 findings checked against round 2

**S** means `docs/2026-10-24-supervisor-design.md`. **Taken** means incorporated into the proposed contract, not built or proved. References to findings below use **F1–F15**.

Code-path abbreviations:

- **Driver**: `src/Daoris.Desktop/Daoris.Desktop.Driver`
- **Modules**: `src/Daoris.Desktop/Daoris.Desktop.Modules`
- **Core**: `src/Daoris.Service/Daoris.Service.Core`
- **Http**: `src/Daoris.Service/Daoris.Service.Http`
- **Web**: `src/Daoris.Web/src`

| Read | Round-1 finding | Disposition | Check against round 2 and code |
|---|---|---|---|
| Codex 1 | Delivery targeted the new-session resume | **Taken** | S:477–482 preserves `HandedToResume` and names same-session delivery. `Driver/Conversations/Domain/ResumeAsk.cs:59` is that delivery path. The remaining D79 terminology error is F13. |
| Codex 2 | No exclusive appointed terminal holder | **Open** | S:421–439 acknowledges the bypasses but does not close them. `StandingCaller.Place`, `Modules/Platform/StandingCaller.cs:150`, identifies absence from inspected jobs, not appointment. F1. |
| Codex 3 | Ordinary say can forge supervisor authorship | **Open** | S:469–479 adds public identifiers, not trusted admission or payload binding. `Core/Sessions/SessionLedger.cs:867` deduplicates by caller-supplied key. F2–F3. |
| Codex 4 | Answer can convey owner-only authorization | **Taken** | S:337–351, :403–414 adds the repository reservation, holder policy, receiving text and go-ahead context. These are semantic instructions, not a mechanical authorization boundary. |
| Codex 5 | No prompt-injection contract | **Taken** | S:383–399 labels evidence as claims, supplies a fixed rule and fences, and requires adversarial cases. |
| Codex 6 | Essential provenance only in an event | **Open** | S:294–318 supplies an envelope, but replaces it on the next question and recognizes authorship only while `released`. F7. |
| Codex 7 | Hold not enforced at execution admission | **Open** | S:516–541 changes planning and failed rereads, but does not condition the move at `Driver/Loop/DriverContinuations.cs:258` on the current decision. F5–F6. |
| Codex 8 | Duplicate answers and re-answer after Overrule | **Taken** | S:246–247 specifies one decision, idempotent retry and a sticky overrule. |
| Codex 9 | Older build can consume an unreleased answer | **Taken** | S:308–310 keeps held decisions outside `said`, which older `Planner.cs:355` cannot consume. This fixes the hold; it does not fix older presentation after release. F8. |
| Codex 10 | Actor result authority and deferral undefined | **Deferred with a reason that holds** | S:322–329 now fixes an internal, attempt-bound answer-or-defer protocol; its implementation properly waits for 1c. |
| Codex 11 | No durable attempt/retry rule | **Taken** | S:322–329 requires a persisted attempt before launch and one attempt per question, including failure and timeout. |
| Codex 12 | Measurement weaker than D135 | **Open** | S:730–734 restores O/R and source scoring, but its acceptance condition still permits other wrong answers and incomplete runs. F11. |
| Codex 13 | Separate answers-alone counts do not compose | **Taken** | S:373–379 adopts one count and unchanged resets. Implementation must extend the event counter as well as `Solicited`; F8. |
| Codex 14 | First row depends on later safeguards | **Open** | The separate flag fixes early offering, but S:705–710 creates an offering/proof/order cycle. F12. |
| Codex 15 | Lifecycle missing from attention/card integration | **Open** | S:359–368 adds states, but conflates release with execution and loses historical decision data. F7, F9. |
| Codex 16 | Competing field inside the foot’s scroll region | **Taken** | S:592–594 and :626–641 reuse `SessionBox.tsx:143`’s Composer outside the wait part. |
| Codex 17 | Question identity and accessible-name contradictions | **Taken** | S:607–614, :628–638 chooses one naming contract and binds selection to question identity. Remaining selection/send details are F15. |
| Codex 18 | Overlapping appointments lack precedence | **Taken** | S:448–450 chooses repository precedence. Requests and release still need the effective appointment’s generation; F4–F5. |
| Opus 1 | Escaped Daoris agent can answer as supervisor | **Open** | Choice-only answers and disclosure are taken, but S:424–427’s argument does not establish appointed-holder authority. F1. |
| Opus 2 | Service, alone and request-folder bypasses | **Open** | No-alone and no-request-folder paths are specified. The service bypass remains. F2–F3. |
| Opus 3 | Unattributed answered note survives overrule | **Taken** | S:491–493 specifies authored and overruled note codes. Forged submissions must not receive those codes; F3. |
| Opus 4 | Wrong delivery seam and omitted author readers | **Open** | The named resume paths are corrected, but the claimed exhaustive sweep still omits author carriers/readers. F8. |
| Opus 5 | Role falls open; judge and storage underspecified | **Taken** | S:693–704 specifies a separate Role branch, independent of `_presses`, a distinct door flag and preserving appointments. Existing writer locking needs explicit treatment; F10. |
| Opus 6 | Other words cut the window; proposed timed mark | **Answered with a reason that holds** | S:516–521 holds mixed queues and explains why the envelope, rather than untimed `GoOnMarks`, owns the clock. Admission remains F6. |
| Opus 7 | Overrule does not stick | **Taken** | S:247 and :529–530 makes the question owner-only until replaced. |
| Opus 8 | Handed text does not reserve owner acts | **Taken** | S:403–414 names the exclusions and the actual limits of their enforcement. |
| Opus 9 | Repository-required approval/R class missing | **Taken** | S:342, :348–350 and :730 reserves it and scores answering it as wrong. |
| Opus 10 | Prompt injection unaddressed | **Taken** | S:381–399 supplies the requested policy, labelled inputs and adversarial cases. |
| Opus 11 | Actor identity forgeable through connector | **Deferred with a reason that holds** | S:322–329 replaces the connector result door with the actor run’s own result, bound to an attempt; built in 1c. |
| Opus 12 | Missing facts about terminal and stories | **Taken** | S:89–95 and :118–121 adds alone/request-folder behavior and WaitPanel stories. |
| Opus 13 | First row too large | **Taken** | The requested record/door split is present. Neither the size nor the proof-order issue is resolved merely by splitting; F12. |
| Opus 14 | Appointment offered before bounds | **Taken** | S:215–217 expressly keeps `AppointmentsOffered` false until 1b. |
| Opus 15 | Held/deferred presentation and storage unclear | **Open** | Deferral has a home, but released-state presentation and historical retention remain wrong. F7, F9. |
| Opus 16 | Owner answer goes beside held supervisor answer | **Open** | S:522–524 specifies supersession, but the owner-word check and service keep are not one conditional operation. F5. |
| Opus 17 | Field duplication, accessible name, countdown | **Taken** | S:592–613 reuses the field, settles naming and uses `Remaining`. |
| Opus 18 | Corpus not retained | **Open** | S:727 names an origin, but the published evidence contains summaries, not the complete replay inputs, offered choices and source files required by the new scorer. F11. |
| Opus 19 | Zero-minute overrule window | **Taken** | S:515 and :741–742 establishes a one-minute floor. |
| Opus 20 | Discovery and costs unsaid | **Taken** | S:271–274 supplies discovery; :566–574 counts polling and every actor attempt, without inventing measured savings. |
| Opus 21 | D135 amendment evades unmeasured model answering | **Taken** | S:663–665 and D208:82–85 states the steward exception directly. |

### 1. Claims about the code

The central factual account holds: the question tool’s bounds, machine-local question file, question-identity check, choice composition, same-session resume, terminal Confirm, ungated service attribution, D201’s four presses and unoffered grant, and the existing question buttons.

MODELROLE1b is correctly treated as reporting infrastructure, not as the supervisor actor or its choice machinery. The latter remains dependent on D205’s later rows.

The remaining factual errors are:

- **The author-reader table is not exhaustive.** It misses the dispatcher’s author carrier, another event writer, `line_read`’s author projection and the event predicate that increments the answers-alone count. F8.
- **D79 is still described as a failed same-conversation resume.** Its new-session resume is a separate path. F13.
- **`SessionGroups.WordsWait` does not read the planner’s local `Waits` function.** It reaches another implementation of `WordsOn` and another local predicate. F13.
- **Waiting words make a park eligible; they do not guarantee it runs at the next look.** Existing marks and other holds can prevent it. F13.
- **Free words with go-aheads open do not send “as today.”** Today `WorkFrame.unaccepted` refuses them too. Round 2 would weaken that guard. F14.

The other findings concern guarantees proposed for the new mechanism, rather than descriptions of capabilities already built.

### 2. The mechanism’s holes

**Authority remains the principal hole.** The proposed Role branch is now properly separated from D201’s management presses. A contained agent, plugin or partner should be denied by the existing caller look; unknown containment should also refuse. But an escaped process, repository hook, older headless agent or unrelated external process can still answer. Naming its executable and PID records the pipe client, not the appointed decision-maker. Choosing among another agent’s offered labels does not establish that the holder decided.

The ordinary service door is a second route around appointment. Adding `decision` and `choice` does not authenticate a submission. The proposed predicate binds authorship to a public key and a mutable envelope state, without binding the word’s content to the decision. Moreover, the specified “unattributed” fallback still delivers the words and can start a turn. That is not failing closed.

**Owner-only acts are now reserved explicitly.** The holder must defer sign-ins, access, spending, destructive acts, pushes, publication, go-aheads and repository-required approvals. The receiving paragraph also denies authorization. Those are useful instructions, but option membership is not an effect boundary: an offered choice can itself describe an owner-only act. The permission rules cited at `Driver/Agents/Permissions/Permissions.cs:163` are not a universal guard for every listed act or harness, and a go-ahead record is not a general command interceptor. Under full access, the design correctly admits that the receiving agent’s compliance remains decisive. Consequently, that policy cannot justify allowing an unappointed caller to impersonate the supervisor.

**The appointment’s shape is mostly appropriate:** window-only, local, off by default, independently revocable and preserved alongside presses. Its missing request generation allows an old terminal answer to be accepted under a later appointment. The shared file also needs genuinely serialized mutation, rather than assuming D201 already serializes every write.

**The overrule boundary is incomplete.** Keeping held decisions outside `said` removes the original early-consumption path. It does not invalidate a plan already made, atomically exclude an owner answer arriving during release, or specify a complete recovery when the post-move read fails. Release, execution admission and observed delivery need distinct states and conditional transitions.

**Escalation is improved.** `left`, owner markings and sticky Overrule provide clear owner-facing outcomes. But unreadable provenance, uncertain execution and a released decision that has not actually run must remain answerable owner states; they cannot merely acquire a different label while their queued words continue through the ordinary planner.

**Prompt injection is addressed as policy, not containment.** The claims/instructions separation, nonce fences and refusal of an instructed question are worthwhile. They do not prevent the steward from obeying malicious evidence, nor make an attacker-controlled choice harmless. The actor’s own-run result protocol is a sound improvement; its measurement must test the fixed policy and actual protocol, including hostile evidence and disguised authorization.

**Costs are honestly unmeasured.** The terminal holder pays for polling and investigation in its own runs. The actor pays for every looked-at question, including deferrals and failures. One attempt and ten minutes limit repetition and duration, not a known token total. The bench must report that total and incomplete measurements without allowing an incomplete run to qualify the actor.

The card’s direction is sound: native radios, no preselection, recommendation distinct from selection, one foot composer outside the scroll region, question-bound selection and a non-ticking live announcement. Free-answer guards, clearing selection, head-card dispatch and released-state wording still need settlement.

### 3. The rows

| Row | Can it land alone, in this order? | Does its proof establish the claim? |
|---|---|---|
| **SUPERVISOR1a-i** | Not yet as specified. It promises release checks against an appointment while declaring “no appointment,” and exposes a new service authorship before trusted admission exists. | Missing valid-ID forgery, historical-envelope replacement, stale-plan admission and complete uncertain recovery. The author sweep is incomplete. |
| **SUPERVISOR1a-ii** | It can land dark after 1a-i. It cannot complete its specified installed proof in the stated order: appointment refuses while the flag is false, and that flag also waits for later 1b. | The ordinary contained-caller tests are appropriate. They prove neither appointed-holder exclusivity nor the accepted bypass argument. |
| **SUPERVISOR1b** | Additive after the record/door infrastructure, before any usable offering. | Needs both pending-word classification and event counting, plus release-time refusal tests. |
| **QUESTIONUI1a** | Yes, independently, after F14–F15 are settled. | The browser geometry proof is now appropriate. Actual head/foot sends belong in integration tests, not only the props-only molecule’s tests. |
| **QUESTIONUI1b** | After QUESTIONUI1a and durable historical decisions. | Must exercise a second question, release without execution, and navigation/reload—not only disappearance of the question file. |
| **SUPERVISOR1d** | Yes, once the replay protocol, fixtures and acceptance rule are complete. | Present scoring and acceptance still do not retain D135’s zero-wrong-answer condition. |
| **SUPERVISOR1c** | Correctly waits for measurement and D205’s choices rows, which are not all built. | The internal result contract is adequate in shape; add recovery of an interrupted persisted attempt to the named lifecycle cases. |
| **SUPERVISOR1e** | Yes after appointment identity and effective-appointment validation are fixed. | Precedence is now specified; test a new repository override appearing while a workspace decision is held. |
| **SUPERVISOR1f** | Properly deferred to its own design/read. | No present proof is claimed for words. |

The first record row is still a substantial four-layer change, not a small immediate build: it contains storage, release/recovery, authorship throughout delivery, overrule, attention, conversation UI and notifications. The underlying question flow, containment, judge, take-back and actor reporting are built. The appointment and offering proof are not. Preserve the steward’s eventual door without per-answer Confirm, but make the inert landing, prerequisites and activation proof explicit.

### 4. Findings

1. **Blocking — choice-only answers do not establish appointed-holder authority.**  
   **Location:** S:190, :421–439, :765–766; `Modules/Platform/StandingCaller.cs:150`.  
   The design expressly admits callers other than the steward, then argues that choosing an offered option adds no meaningful authority. It does: another process selects for another session and receives the appointed supervisor’s attribution. Existing ungated doors are not a justification for creating that trusted attribution. The claim that any holder-readable credential is equally readable by an escaped process also conflates an ordinary file read with the memory/injection attacks D156 deliberately distinguishes.  
   **Smallest change:** bind admission to a holder/channel selected during appointment, retaining D201 as an additional denial check. Keep account-level attacks as an explicit limit. Prove that an ordinary brokered caller and repository hook cannot obtain that admission.

2. **Blocking — a real decision identifier can authenticate different words.**  
   **Location:** S:314–316, :469–479; `Http/Program.cs:1487`; `Core/Sessions/SessionLedger.cs:867`.  
   A caller can supply a real decision key with different text. The proposed `Supervisor` predicate checks the key and `released` state, not equality with the canonical decision. The existing say door treats an already-waiting key as success without comparing payloads. Release recovery can therefore find the attacker’s word and complete the envelope transition around it.  
   **Smallest change:** reserve supervisor production for trusted admission; bind session, question, appointment, choice, word identity and canonical text. Derive handed text from the decision. Reject mismatches and prove prepopulation of a legitimate key.

3. **Blocking — “unattributed” is a delivery path, not a closed failure.**  
   **Location:** S:317–318, :477–479, :491–493; D208:43–45; `Driver/Quests/Domain/Planner.cs:355`; `Core/Sessions/SessionLedger.cs:878`.  
   A syntactically valid forged supervisor submission can still wake a park and deliver arbitrary words with a prefix. Meanwhile the proposed ledger code calls that submission “Answered by the supervisor” without access to trusted envelope admission. Thus delivery fails open and the travelling note can assert authority the driver disowns.  
   **Smallest change:** quarantine or refuse an unverified supervisor submission. It must neither create a turn nor receive a supervisor-authored note. Distinguish missing provenance from verified authorship in the service operation itself.

4. **Blocking — terminal requests carry no appointment generation.**  
   **Location:** S:204–205, :230–231, :448–450, :698.  
   The design promises `appointment-changed`, but neither answer nor defer carries the appointment the holder read. An old terminal request arriving after revoke/reappointment can be accepted under the new appointment of the same holder kind.  
   **Smallest change:** return an opaque appointment generation with the read and require it on answer/defer. Compare the effective appointment at admission and release. Test revoke/reappoint with the same holder, and a repository override replacing workspace coverage.

5. **Blocking — release checks are not one conditional admission.**  
   **Location:** S:311–316, :459–461, :522–524; `Core/Sessions/SessionLedger.cs:844`.  
   Checking the parked record, appointment and absence of owner words before a separate say operation leaves interleavings between them. An owner answer can land after the check; the release can still append its own word. The listed release checks also omit a renewed go-ahead refusal and the other structural bounds. Unread go-ahead context has no explicit refusal contract.  
   **Smallest change:** specify one conditional release operation, with an explicit admission point and lock/transaction ordering. Revalidate all structural eligibility there; distinguish no ask from an unread ask. Prove owner-answer and revoke interleavings and an unread or newly open go-ahead.

6. **Blocking — planning holds do not hold an already-planned execution.**  
   **Location:** S:516–541; `Driver/Loop/DriverContinuations.cs:258`, :319; `Driver/Conversations/Domain/Continuation.cs:258`.  
   A plan made from other waiting words can move the record after a held decision is kept; the move does not compare the decision state. The post-move read also admits words not present in the plan. On read failure, merely dropping a supervisor word from the snapshot can still run stale accompanying words while claiming the question stands for the owner. The queued role word’s subsequent disposition is unspecified.  
   **Smallest change:** condition execution admission on current decision/word identities. A failed supervisor-related confirmation must abort and put the record back, settling or quarantining the role word so it cannot later run as unattributed. Test a decision inserted after planning, Overrule before admission, and failed rereads with mixed queues.

7. **Blocking — provenance expires when the decision is consumed or replaced.**  
   **Location:** S:294–307, :473–474, :485, :598–599.  
   Authorship is valid only while the envelope says `released`, although its next legitimate state is `went-on`. The next question replaces the sole envelope. Historical decisions then lose their authoritative snapshot and reasons; a still-queued word can also cease qualifying as the supervisor’s during execution.  
   **Smallest change:** retain immutable decision records by decision ID for the session’s retained history, with a separate current-question pointer. Separate authenticated origin from permission to deliver. Prove attribution and reasons after consumption, a second park, reload and restart.

8. **Blocking — the author sweep omits necessary carriers and readers.**  
   **Location:** S:57–80, :475–490, :681–691. Relevant omissions include:
   - `Driver/Conversations/SaidDoor.cs:34`, which carries only Daoris versus absent author;
   - `Driver/Conversations/Domain/ChatMessage.cs:185`, another event-origin writer;
   - `Driver/Conversations/GoOn/SessionSay.cs:243`, which shows every non-Daoris word as the person’s;
   - `Core/Sessions/LineEvents.cs:458` and :347, which discard supervisor attribution and call the resulting target entry Daoris;
   - `Driver/Conversations/Domain/AnswersAlone.cs:94`, which increments the historical count independently of `Solicited`.

   Extending `Solicited` alone makes a queued supervisor word eligible for the guard without making its completed turn increase the count. The old renderer at `Web/work/conversation/ConversationView.tsx:501` also still calls an unknown author *You* after rollback.  
   **Smallest change:** extend the complete author carrier and both counting predicates; include these seams in the sweep. State and test the released-word rollback projection rather than implying that framed text repairs older presentation.

9. **Non-blocking — release is shown as execution, and its remaining overrule disappears.**  
   **Location:** S:363, :503, :506, :527–535, :547.  
   A released word can wait before a run starts. Showing *Went on* and retiring Overrule at release contradicts the later promise that a released, still-parked answer remains overrulable. Likewise, finding an appointment at startup does not prove it was appointed while Daoris was down; it may have been granted normally in an earlier run.  
   **Smallest change:** show released as waiting to go on, retain Overrule until execution admission, and record the observed execution time separately. Say an appointment was found at startup without inventing how it was written.

10. **Non-blocking — shared-record mutation requires changing existing writers, not just preserving a field.**  
    **Location:** S:204–213, :693–701; `Modules/Platform/StandingConfirmation.cs:305–330`, :339–352.  
    D201’s admission is locked, but its existing grant writes outside the lock and installs memory afterward; revoke also writes after releasing it. Carrying appointments through those writes without serializing the complete mutation can lose an appointment update or persist an obsolete grant after revoke.  
    **Smallest change:** explicitly put every press/appointment read-modify-write and corresponding memory update through one mutation protocol. Add concurrent press-grant, appointment-change and revoke cases.

11. **Blocking — the bench can accept incorrect or incomplete performance.**  
    **Location:** S:726–734; `tools/question-bench.mjs:455–471`; `docs/decisions/D135.md:218–223`.  
    Zero answered O/R/adversarial cases plus one right answer permits arbitrarily many wrong ordinary choices or false sources. Reporting failures does not stop an incomplete run from passing. Also, the published 46-item evidence supplies summaries and grouped entries, not complete choices, source files and exact replay answers.  
    **Smallest change:** require zero wrong answered cases of every class and completed verdict coverage of the required corpus; failures make the qualification incomplete. Version complete inputs, choices, source fixtures and the oracle. Label reconstructed evidence cases as constructed rather than historical replay.

12. **Blocking — the first-door installed proof cannot run in the specified order.**  
    **Location:** S:215–217, :678–680, :705–710; `Modules/Platform/StandingConfirmation.cs:136`.  
    `SET_APPOINTMENT` refuses while `AppointmentsOffered` is false. That flag waits for 1b and the installed proof, but the proof requires making an appointment before the flag turns on, and belongs to a row preceding 1b. Separately, 1a-i promises appointment-validating release while containing no appointment implementation.  
    **Smallest change:** name an admission interface for the inert record row, defaulting to refusal in production. Let the door land dark, then run activation proof after 1b in an explicitly identified candidate build. Keep D201’s outstanding offering prerequisite explicit. Do not call the dark row a usable steward door.

13. **Non-blocking — three descriptions still name the wrong existing mechanism.**  
    **Location:** S:65, :47, :517; D208:19.  
    D79’s new-session resume is not the fallback from a park that cannot resume its conversation (`Driver/Loop/DriverContinuations.cs:767–784`). `SessionGroups.WordsWait` uses its own `WordsOn`/`Waits` implementation (`Driver/Trees/SessionGroups.cs:1235–1251`). Waiting words remain subject to marks and other planning holds (`Driver/Quests/Domain/Planner.cs:1469–1471`).  
    **Smallest change:** distinguish D79 from fallback, name both duplicated predicates, and describe waiting words as eligibility subject to existing holds.

14. **Non-blocking — the card’s free-answer path weakens today’s go-ahead order.**  
    **Location:** S:631–636; `Web/work/WorkFrame.tsx:761–770`.  
    Today the guard rejects any answer while a recorded question has open go-aheads. Round 2 rejects only a selected choice and says free words send “as today.” That is both a factual error and an unintended bypass of the stated order.  
    **Smallest change:** retain the same go-ahead guard for selected and free answers, through both Enter and Send, in the head and foot. Test each.

15. **Non-blocking — selection clearing and the head’s send contract are unfinished.**  
    **Location:** S:592–594, :603–610, :628–639, :716–719; `Web/work/waits/AwaitingPerson.tsx:99`, :139, :166`.  
    Native radios cannot return to no selection after a choice is made. The design promises words alone but supplies no clear-selection action. It also specifies the box’s combined send without explicitly replacing the head’s separate choice press/free-answer send behavior. A props-only molecule cannot itself prove the `SESSION_INPUT` payload.  
    **Smallest change:** add an explicit clear-selection action; specify how the head’s Send combines its words with the selected question/label. Put actual payload, refusal retention and duplicate-send assertions in head/foot integration tests.

**15 findings; 10 blocking.**

**Verdict: another round.** Keep the owner-appointed role, choice-only first implementation, separate decision record, shared answers-alone count, internal actor result and improved question card. The remaining changes to holder admission, canonical decision binding, release/execution concurrency and retained provenance affect the foundation and need another read. QUESTIONUI1a can proceed independently after F14–F15 are settled; the steward’s usable door should still require no per-answer Confirm.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2, at `607f12863`: 12 findings, 3 blocking; accept after three named changes.

## The foundation read of D208 (SUPERVISOR1, QUESTIONUI1), round 2

Read at `607f12863` (round 2, documents only; the code is unchanged since `fb34cf7db`). Paths are relative to the design's tree. I
changed nothing and ran nothing. Here **S** is `docs/2026-10-24-supervisor-design.md`.

The foundation questions, in short:

- **Only the appointed holder answers as the supervisor?** The pipe holds: the `Role` class is judged by D201's caller look, and §4.6
  says plainly that the look admits any caller outside Daoris's jobs. The record does not hold. Two writes that S itself places in a
  contained agent's reach make a supervisor's word with any text:
  - the say door's producer key;
  - the decision envelope in the home, read at every look.

  The driver then hands that word as the supervisor's, and the owner overrules a choice the session never gets (F1).
- **Never what only the owner decides, never shown as the owner's?** Almost. The handed paragraph's first clause grants what the chosen
  `does` says (F3). Two readers S does not list still say *your answer* (F2).
- **Is the overrule real?** Yes while the decision is held and the decision is the handler's own: nothing is on the record, and every go-on
  skips the record. It is not real for a forged word or envelope (F1), and it leaks in two places (F5, F7).

### 0. Round 1's findings

**Codex, round 1**

| # | Verdict | Where (S) | Why |
|---|---|---|---|
| 1 | taken | §6.1 l.477-482 | `HandedToResume` stays `Persons \|\| Daoris` (`DriverContinuations.cs:784`). The same-session go-on hands `Handed` (`ResumeAsk.cs:57-66`). Checked. |
| 2 | answered; the reason holds only after F1 | §4.6, §12 | Rejecting the credential holds, since the same account reads it, as D201 rejects a caller's identity. The real bound is the act's shape (an offered label), and F1 defeats that shape. |
| 3 | taken in form, open in substance | §3.4, §6.1 l.469-474 | The envelope is called the authority, but it is a home file in an agent's reach. The release also adopts any word already kept under its key (F1). |
| 4 | taken | §3.2 part 3, §4.1, §4.5 | `repository-asks`, the go-aheads handed with their answers, the handed text, words deferred, and disguised authorization measured. The text's first clause needs F3. |
| 5 | taken | §4.4 | Claims, never instructions; a fence with a nonce; the skill's rule; `instructed`; adversarial cases. |
| 6 | taken | §3.4 | One envelope holding the question's snapshot, holder, `since` and `goesOnAt`, failing closed. Its crash recovery by key opens F1(a). |
| 7 | taken, one residue | §3.4, §6.3 | Nothing is on the record while held, and the re-read fails closed. The `uncertain` word stays on the record (F5). |
| 8 | taken | §3.1 | `answered`, an idempotent retry, `overruled` until a new question. `uncertain` is not covered (F5). |
| 9 | taken, one residue | §3.4 | A held decision is never in `said`, so an older reader cannot see it. An older page draws a released, untaken word as *You* (F11). |
| 10 | taken (contract) | §3.5 | The result comes from the actor's own run, bound to attempt, question and appointment. |
| 11 | taken (contract) | §3.5, §7 | One attempt per question, kept on the envelope. |
| 12 | taken | §11 1d | O and R answered count as wrong; sources checked; adversarial cases. The "nonempty" floor is one (F12). |
| 13 | taken in intent; the named change is wrong | §4.3 | `Solicited` (`AnswersAlone.cs:24`) is not what the count reads. It reads `Answers` (`:94-95`), so supervisor go-ons would never count (F4). |
| 14 | taken, one residue | §2.3, §11 | `AppointmentsOffered` waits for 1b. 1a-ii's installed proof cannot run while the flag is off, and D201's offering is not named as a prerequisite (F9). |
| 15 | taken | §4.2 | Each state and place is said. Cannot-go-on is missing (F6). |
| 16 | taken | §8.4 | One field and one *Send* in the box. The head's card is unspecified (F10). |
| 17 | taken | §8.2, §8.4 | One accessible-name contract, and the selection tied to the question's id. The markup contradicts the contract (F10). |
| 18 | taken (rule) | §5 | The repository's appointment wins, and the decision records which one it acted under. |

**Opus foundation read, round 1**

| # | Verdict | Where (S) | Why |
|---|---|---|---|
| 1 | taken in the steward's form | §4.6, §5, §3.1 | The holes are weighed again, with D201's sentence in the ask, one offered label and reasons for the owner. "Cannot hide" overstates the naming (F8), and F1 defeats the shape. |
| 2 | taken in form, open in substance | §3.4, §4.6 | There is no alone path, the request folder takes no supervisor answer, and the forgeable door is said. A forged word can still be named released (F1). |
| 3 | taken | §6.1 l.491-493 | `ledger.answered-supervisor`, `ledger.overruled`, `NoteBy` `supervisor`. |
| 4 | taken in part | §0.2, §6.1 | The seam is right and ten readers are listed. `WorkingNoted` and the planner's go-on sentence still say *your answer* (F2). |
| 5 | taken | §2.3, §11 1a-ii | A `Role` branch before the open path (`InstancePipe.cs:597`), its own method, never in `Built`, `Write` keeping `appointments`, a door flag of its own. |
| 6 | taken; the mark answered | §6.3 | Held in all three go-ons. Not a `GoOnMarks` mark, since those key on words and nothing is on the record while held. The answer holds. |
| 7 | taken | §3.1 | `overruled`, `answered`. |
| 8 | taken; wording wrong | §4.5 | The paragraph and the real holds are named, but its first clause grants the `does` (F3). |
| 9 | taken | §4.1, §11 1d | `repository-asks`; an answered R counts as wrong. |
| 10 | taken | §4.4 | |
| 11 | taken | §3.5 | No connector door; the run's own last message. |
| 12 | taken | §0.3, §0.5 | |
| 13 | taken | §11 | 1a-i, then 1a-ii. |
| 14 | taken, one residue | §2.3 | It has its own flag, which the installed proof cannot reach (F9). |
| 15 | taken | §4.2 | |
| 16 | taken while held | §6.3 l.522-524 | After release and before the move, the session gets both (F7). |
| 17 | taken | §8 | The box's field, one name contract, `Remaining`. |
| 18 | taken | §11 1d | The corpus is named. The 46 need choices written for them (F12). |
| 19 | taken | §6.3, §12 | Floor of one minute. |
| 20 | taken | §3.1-§3.2, §7 | `SUPERVISOR_WAITING`; the costs and bounds are said. |
| 21 | taken | §10, D208 | |

### 1. Claims about the code that are wrong

- **"The authority is the envelope, which only the judged handler writes"** (S l.433-435; also §3.4 l.304, §6.1 l.473-474). Both halves
  of that are false:
  - The envelope is `<home>/sessions/<session>.supervisor.json`, read at each look. D201's Why (`D201.md:62`) and S's own §4.6
    (l.425) count the home's files in an agent's ordinary reach.
  - The say door keeps one word per producer key, and answers a later say with that key as a retry, with the word already kept
    (`SessionLedger.cs:866-873`; `Sessions.cs:373-376`). The key comes from the caller's body (`Program.cs:1488`). S makes the decision's
    id that key (l.469-470) and adopts a word found under it (l.314-316).
- **Every reader of a word's author** (§0.2, l.56). Two readers that word a non-person go-on as the owner's are missing:
  - **The working note.** `DriverContinuations.cs:251` picks `Continuations.WorkingNoted` for every park, whoever answered. Its English
    is *resumes its own conversation with your answer* (`Continuation.cs:242-245`). It is written onto the record's note, which travels.
  - **The planner's go-on sentence.** `Planner.cs:1487-1488` reads `you answered session `s`: {written.Answer ?? said}`. `Answer` holds
    only the person's words (`Planner.cs:752`, from `Sessions.cs:280-281`), so a supervisor-only park prints the supervisor's text after
    *you answered*.
- **The answers-alone count** (§4.3 l.373-374). S says `AnswersAlone.Solicited` (`:24`) "gains the supervisor's released word, so a prompt
  … counts one". `Solicited` only decides `Holds` (`:30-31`). `Count` reads events through `Answers` (`:94-95`, helper or opinion) and
  `Resets` (`:89-91`, person or Daoris). A taken supervisor word (origin `target`, `From = supervisor`) is neither, so it never counts.
- **The holder's name** (§2.2 l.191-193, §6.2 l.502, §4.6 l.427). *held by a terminal: pwsh, 4120* cannot happen. `AskerOf` names
  the process that opened the pipe (`InstancePipe.cs:1076-1096`), and for `sessions supervise` that is `daoris-driver` (`AssemblyName`,
  `Daoris.Desktop.Driver.Host.csproj:9`). Every terminal decision reads *daoris-driver, <pid>*, whether the steward or an escaped
  agent made it.
- **"The planner's `Waits` …, which `WordsOn` and `SessionGroups.WordsWait` read"** (S l.516-517). `Waits` is local to the planner's
  `WordsOn` (`Planner.cs:1469`). `SessionGroups` keeps its own copy (`SessionGroups.cs:1233-1240`, FABRIC-CLOSE1c11), which needs the skip
  in its own right. S lists both as skipping, so only the sentence is wrong.

Everything else I checked held at its lines: §0.1, §0.3, §0.4, `StandingConfirmation.cs` (`:28`, `:136`, `:241-269`, `:283`,
`:291-299`, `:352`, `:370-380`, `:399-440`), `StandingCaller.cs:150-177`, `InstancePipe.cs:538-545`, `:597-613`, `Sessions.cs:386-408`,
`Program.cs:1481-1501`, `SessionLedger.cs:989-1030`, `Planner.cs:355`, `:650-666`, `:1461-1472`, `ResumeAsk.cs:57-66`, `:118-131`,
`Continuation.cs:254-269`, `:401-415`, `SessionEventLog.cs:399-419`, `QuestionFiles.cs`, `Driver.cs:2144-2152`, `:4141-4144`,
`AnswersAloneHold.cs:56-58`, `GoOnMarks.cs` (keyed on words, no until), `Permissions.cs:163-170`, `phases.tsx:75-96`, `ParkAnswer.tsx`,
`AwaitingPerson.tsx`, `WorkFrame.tsx:529-531`, `:994-1056`, `SessionBox.tsx:117-154`, `Composer.tsx:347`, and the box's queue leaving
out any word with a `from` (`SessionWords.cs:1068-1070`).

### 2. What round 2 introduced

**The decision envelope and its release** (§3.4) are the right shape: a decision held apart from the record. They have one hole (F1).
The release trusts two things:

- a word kept under the decision's key, which an agent can post first;
- the envelope file, which an agent can write.

The driver then hands the word's own text, not a paragraph composed from the decision. So what the owner reads and may overrule (the
handler's label and reasons, said in *Notifications*) and what the session gets can differ. A forged envelope is also released with no
notice ever raised.

**The handed text** (§4.5) is the right hold, but its first clause reads as a grant (F3).

**The fail-closed move** (§6.3 l.536-541) drops the supervisor's word from the snapshot but leaves it on the record. The next look hands
it as *words Daoris cannot attribute* (F5).

**`released` has two holes:**

- A supervisor-only park that cannot resume in its own conversation goes to `CannotGoOn` (`Driver.cs:2148-2151`), and its envelope stays
  `released` (F6).
- The owner's words after release, but before the move, go beside it (F7).

**The rows:**

- **1a-i** can be built as described, with no pipe, and its proof covers its claims once F1's cases and F2's two readers are in the sweep.
- **1a-ii's installed proof cannot run in its order** (F9). `SET_APPOINTMENT` refuses `not-offered` until `AppointmentsOffered` is true.
  That flag waits on the same installed proof (S l.216-217, l.705-707), and it is never true while D201's `OfferedInThisBuild` is false
  (`:136`). D201 is not offered yet, and S's row and TASKS do not name it as a prerequisite.
- **1b**, **1d**, **1c**, **1e** and **1f** can be built in their order. 1b's count needs F4's predicate.

**QUESTIONUI1a can be built at once in the box.** §8.4 settles:

- the box's draft and selection;
- the go-aheads' order;
- the question's identity;
- the WAITPANEL1 rule.

Three sentences are missing (F10):

- The head's card has no send rule for a selected row.
- The row's lanes omit the driver and the modules, which must carry `why`.
- §8.1's `<label>`-wrapped row would put `does` and *Why* into the radio's name, against §8.2.

None of these blocks starting. They are a builder's guesses until they are written.

### 3. Findings

1. **Blocking. Any agent in its job can make a word the driver hands as the supervisor's, with any text, and the owner overrules
   something else.** S l.311-318 (the release adopts a word under the decision's key), l.469-479 (the driver hands "a released word's
   text"), l.433-435.
   - **(a) The key.** An agent reads the held decision's id from `<home>/sessions/<session>.supervisor.json`. It posts
     `{ from: supervisor, decision: <id>, choice: x, text: anything }` to the ungated say door, which S admits on `decision` and `choice`
     alone. At `goesOnAt` the release's say gets that word back as a retry (`SessionLedger.cs:866-873`), and S's crash rule completes the
     move. The envelope then names the word `released`, `SaidWordView.Supervisor` is true, and the session gets the agent's text framed
     as *the supervisor's answer*. The owner was shown the handler's label and reasons, not that text.
   - **(b) The file.** An agent writes a `held` envelope with `goesOnAt` past, the appointment's `since` copied from
     `data/standing-confirmation.json`, and any `choice`. The release checks the question's id, the appointment and the owner's words,
     never the label (l.312-313), so the driver itself keeps a framed paragraph quoting arbitrary text as the label. No handler ran, so no
     notice was raised and there was no window to overrule in.

   **Change:**
   1. At hand time the driver composes the paragraph from the envelope's `choice` (§4.5's words). `SaidWordView.Supervisor` is true only
      where the word's text equals that paragraph.
   2. A release whose say comes back `Again` with a word it did not write takes that word back, logs `supervisor.unattributed`, and moves
      the decision to `stale`.
   3. The release checks again that `choice` is one of the question file's labels exactly.
   4. The instance honours only decisions its own handler kept in this run (ids in memory, under the judge's lock). At a start, every
      `held` or `released` envelope not yet `went-on` moves to `stale`, its word taken back if one is on the record, and the card and
      *Notifications* say so, as §4.6 says of an appointment written while Daoris was down.
   5. §4.6's say-door bullet states the envelope as the account's reach (PERSONDOOR2), with D201's conclusion: the role adds no act that
      reach lacks today.

   **Proof:** `SupervisorDecisionTests` cases for a word posted first under the decision's key, a forged `held` envelope, and a forged
   `released` envelope with a matching word, each handed as unattributed or not at all.
2. **Blocking. Two readers still word the supervisor's answer as the owner's.**
   - `DriverContinuations.cs:251` writes `WorkingNoted` for every park: *resumes its own conversation with your answer*
     (`Continuation.cs:242-245`). That note travels with the record.
   - `Planner.cs:1487-1488`'s go-on sentence prints *you answered session …:* and the supervisor's text, since `Answer` holds only the
     person's words.

   S's §0.2 claims to list every reader, and the sweep test is built from that list. **Change:**
   - a fifth working note, *resumes its own conversation with the supervisor's answer*, with its code, chosen where only a supervisor's
     word waits;
   - the go-on sentence naming the supervisor;
   - both rows in §0.2 and §6.1;
   - the sweep's set built by searching the readers of `Persons`, `.Daoris`, `From`, `Origin = "person"` and the English *your answer*
     and *you answered*, not kept as a hand list.
3. **Blocking. The handed paragraph grants what the chosen `does` says.** S l.406: *Choosing grants nothing beyond what the choice says
   you will do: it is no go-ahead, … push …*. For a `does` of *I push to origin now*, the first clause grants the push and the second
   denies it, which is exactly Codex's round-1 finding 4 case. **Change:** *Choosing tells you which of your offered answers to follow;
   it grants nothing: no go-ahead, sign-in, access, spending, push, publication, deletion or history rewrite, and it overrides no refused
   go-ahead and no repository document that asks the owner. If following it needs any of those, ask through go_ahead_ask or wait for the
   owner.* The text's test asserts that no clause grants the choice's `does`.
4. **Not blocking. §4.3 changes the wrong predicate.** `Count` reads `Answers` (`AnswersAlone.cs:94-95`) and `Resets` (`:89-91`), not
   `Solicited`. **Change:** name `Answers` gaining `e.From == supervisor`, and give `AnswersAloneTests` a case where three supervisor-only
   go-ons in a row are held.
5. **Not blocking. `uncertain` leaves the word on the record.** S l.536-541. A supervisor's word not handed from the snapshot stays in
   `said`, through `PutBackAsync` or beside the words that went (`DriverContinuations.cs:319-320`). The next look hands it as unattributed,
   since the envelope is no longer `released`. `answered` (l.246) does not refuse `uncertain` either. *Not sure it reached the session* is
   also wrong: Daoris knows it did not hand the word. **Change:** on `uncertain`, mark the word with a `GoOnMarks` code of its own so no
   look takes it on, take it back once the record reads, refuse `answered` on `uncertain`, and word the state *It was not handed*.
6. **Not blocking. A supervisor-only park that cannot resume in its own conversation stays `released`.** `Driver.cs:2148-2151` sends it
   to `CannotGoOn`, where it waits for the owner. §4.2 l.363 would draw *Went on* and leave it out of *What needs you*. **Change:**
   `CannotGoOn` moves the decision to `stale`, said, so the park is a wait again.
7. **Not blocking. The owner's words after release, but before the move, go beside the supervisor's.** S l.522-524 covers only `held`.
   **Change:** `SESSION_INPUT` naming the question takes back a released, untaken supervisor word in the same step (SAIDBACK1a's door),
   `superseded`.
8. **Not blocking. The holder's name is always `daoris-driver`.** See §1. **Change:** in §2.2, §4.6 and §6.2, say that the name is the
   verb's process and tells no holder from another. If a hint is wanted, keep the client's parent process as the system names it, said
   as a hint and never an authority.
9. **Not blocking. 1a-ii's installed proof cannot run in its order.** S l.216-217 and l.705-707; `StandingConfirmation.cs:136`.
   **Change:** name D201's offering as 1a-ii's prerequisite in §11 and TASKS. Say that the proof runs on the build that turns
   `AppointmentsOffered` on, installed locally and looked at before that change lands (as D201's own note on order offers its grant), not
   "before it is turned on".
10. **Not blocking. QUESTIONUI1a needs three sentences.**
    - **The head's card.** `AwaitingPerson` takes no go-aheads, and its presses are not held by them today (`AwaitingPerson.tsx:136-141`,
      `WorkFrame.tsx:994-995`). Say whose selection it is (the frame's, shared with the box, or its own), what *Send answer* sends with a
      row selected (`onChoice(label, words)`) or none, whether rows can be selected before *Answer and carry on…*, and the go-aheads' order
      there.
    - **The lanes.** `why` is read by `QuestionFiles.Parsed` (`QuestionFiles.cs:75-104`) and projected by `SESSION_QUESTION`
      (`SessionsRoutes.cs:376-384`). The row is service, driver, modules and web.
    - **The name.** With the whole row inside `<label>` (§8.1 l.587-590), the radio's name is the row's whole text. The input takes
      `aria-labelledby` (the label and its chip), with `does` and *Why* by `aria-describedby`.
11. **Not blocking. Rollback.** An older page draws a released, untaken `from: supervisor` word as *You* (`ConversationView.tsx:501`
    today). **Change:** say it beside §2.3's rollback note: a window of one look.
12. **Not blocking. 1d's gate.** *A nonempty count of right answers* is a floor of one. D135's 46 items predate `question_ask`
    (PARKVIEW1, 2026-10-22), so none carries offered choices. **Change:** say who writes each case's choices (the steward, the owner's
    settled answer among them) and a stated floor of right answers on the settled cases, as a first value.

**12 findings; 3 blocking.**

**Verdict: accept after named changes the steward can check without another read.** The foundation's shape holds, and round 2 took
both reads' blocking findings in substance. The role is appointed only in the window and recorded as itself. The pipe's `Role` class is
judged by D201's lock and caller look, with no card. The decision is held apart from the record, and every go-on skips the record while
it is held. The terminal holder answers with one offered label, and its reasons go to the owner alone. Three changes are needed first,
each a stated rule and a named test:

- **F1:** the driver composes the handed paragraph from the decision and attributes only that text; the release refuses a word it did not
  write and rechecks the label; only decisions this instance kept are honoured, and a start makes the rest `stale`.
- **F2:** the working note and the planner's go-on sentence get the supervisor's case, and the sweep's readers are found by search.
- **F3:** the handed paragraph grants nothing, the `does` included.

F4 to F12 should be taken before their rows start. None of them changes the foundation.
