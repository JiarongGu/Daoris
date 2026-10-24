# SWITCHCTX1: the reads of rounds 1 and 2 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-switch-context-design.md` and its notes at `ff2461081`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out and is owed; under D191's note of 2026-10-24 this design, which reverses D189 point 1 on an agent's own transcript, is not accepted on one maker's read. The Opus foundation reader asks another round (12 findings, 4 blocking). Round 2 answers it.

## Opus (foundation), round 1

The read, verbatim.

### 1. Claims about the code

Paths without a root are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, as the design has it. I read main at `4d6a259d6`, whose
`src/` is unchanged since the design's `ddf0a24fd`. Most of §1 holds. These claims are wrong:

- **§1.1, the carry-on's lines.** `Driver.ContinuesFrom` is at `Loop/Driver.cs:4548-4559`, not `:2548-2559`; those lines are the
  closure binding and the workflow's checks. `SayMoved` is at `:4515-4528` and is called at `:2526`; it is not at `:2515-2528`.
- **§0 and §1.5, the account home's path.** The entry has two spellings: `profiles/agents/*/*` is its new place and
  `harnesses/*/*` its old one (`Platform/DaorisHome.cs:324`, `:370`). `<home>/harnesses/<agent>/<account>` names only one of them.
  `Adapters.cs:1512` is the interface's default `PrepareResume`. It does not show that there is no native Codex adapter (that is
  true: `CodexAcpAdapter`, `:2060`, is the only Codex adapter).
- **§2.2, "both homes are Daoris's own per-account homes".** The home's own contract says *Daoris owns this location and nothing
  inside it* (`Accounts/Harnesses/HarnessSettings.cs:878`).
- **§2.2 How it lands, "its process stopped … so the files are no longer written".** `EndForHandOverAsync` returns when the watch
  ends **or after 15 seconds** (`Conversations/Chat/ChatHandOver.cs:16`, `:229`). `LimitMove` ends the old part by the same call
  (`LimitMove.cs:133`).
- **§2.3, "the start then takes the existing resume … judged by `Continuations.Judge` … all of which now hold".** The judge is the
  go-on's judge for a record whose words are waiting. Where none wait it answers `ended` (`Conversations/Domain/Continuation.cs:217`).
  Its callers are the ended-record go-ons (`Conversations/Chat/ChatGoOn.cs:483`, `:525`, `:615`; `Loop/DriverContinuations.cs:61`,
  `:209`). At a curve move no words wait (`CurveMove.TurnJustEnded`, `CurveMove.cs:36-37`), and the take gives the new part none of
  the person's words (`HandOver.cs:271`). A new part's start (`ChatRunner.StartCoreAsync`, `Conversations/Chat/ChatRunner.cs:492-638`)
  passes no `GoingOn` to `RunAsync` (`:628-631`), so the protocol door opens with `session/new` (`:2618-2619`).
- **§2.3, "`harness.json` is written … before its start".** The new record's id is created inside the start, by the ledger's open
  (`ChatRunner.cs:582-587`; `Driver.cs:2431-2439`). `LimitMove` also picks the new account inside the start (`profile: null`,
  `LimitMove.cs:141-144`). So §2.4's step 4 (copy into the next account's home) cannot come before step 5 as written.
- **§2.3, "how it was handed is a third value beside `transcript` and `brief`".** `Handed` exists only in Ask Daoris's `.help.json`
  (`Help/HelpConversations.cs:11`, `:156`, `:188`). A repository's chat and a driven part have no such value, and the *Parts* strip
  reads how a part began from `fromWhy`.
- **§2.3, "`session.moved … carried=`".** That log line already has a `briefed` field (`Continuation.cs:585-593`).
- **§2.4, "the box D206 gives … carries step 2's brief".** The box holds the reason, the refused turns' bounds, the ids of the waiting
  words and the new part (`LimitMove.cs:250`, `:266`). The brief is composed again from those (`:293-310`). Only the ended-record move
  has such a box; the live limit move, the curve's take and a driven carry-on have none.
- **§2.4 step 6, "handed only what a resume is handed today".** On the protocol door a resume always sends a prompt as its next turn
  (`Agents/Acp/Acp.cs:774-776`, `:811-847`). An ended chat's go-on never resumes without waiting words (`ChatGoOn.cs:562`).
- **§2.5, "the same record goes on with a fresh conversation on the same process".** Today a refused resume kills the process
  (`ChatRunner.cs:2622-2640`), under the code's rule that a new session would be *a new conversation under the same record*
  (`:2616-2617`, D137 point 8). That is a new path, not today's move.
- **§2.1, "Ask Daoris is carried as a repository's chat is: its room is its tree".** Ask Daoris never moves by the curve or by a
  hand-over (`CurveMove.cs:70-77`, `HandOver.cs:190-193`). It moves only by `LimitMove.HelpStartAsync`, in the room that
  `HelpRoom.GoesOnIn` returns (`LimitMove.cs:402-416`). That room is not always the previous part's tree (ASKSCOPE1b,
  `ChatGoOn.cs:648-649`).

These I checked and found as written: the switch decision (`Accounts/Domain/SwitchDecision.cs:10-124`); `CurveMove.MoveAsync`
(`:62-119`); `HandOverTake.RunAsync` (`HandOver.cs:232-296`); the hand-over and move lines (`Conversations/Domain/HandOverRecords.cs:134-205`);
`LimitMove`'s live and ended bodies and LIMITGOON2's `ElsewhereAsync`; the help fold (`HelpConversations.cs:233-249`); the page
following a conversation and drawing earlier parts (`help/AskConversation.tsx:202-207`, `:870-876`); the Parts strip
(`work/head/PartsStrip.tsx:105-113`); the home variables (`Agents/Adapters/Adapters.cs:1927`, `:2110`, `:2365`;
`HarnessSelection.cs:579-580`); the kept ids (`Conversations/GoOn/HarnessConversations.cs`); the resume methods and `gone`
(`Acp.cs:3224-3288`); the pipe door's `--resume` (`Adapters.cs:2212-2223`); `Flatten` (`Platform/ToolHomeLeftovers.cs:55`); the
catalogue lines (`note.json:197-198`, `:206`, in en and zh); and the glossary's rows.

### 2. The mechanism's holes

**The reversal of D189 point 1 is wider than the design names.** Point 1 says three things: the agent's own conversation is *one per
record*, and it is *never read, copied or written* by Daoris. The design amends only the second half. The carry also changes these
decided rules, and the design names none of them:

- D131 point 4, *One Daoris record for one harness conversation* (`docs/decisions/D131.md:24`), and its rejection of *Resuming the
  conversation in a new record … That is the linked shape: one conversation over two records* (`:45-46`). §2.3's "two records name
  the same conversation id" is that shape.
- D137 point 8, *One record per harness conversation* (`docs/decisions/D137.md:31`).
- D167 point 1, *A part is still one harness conversation and one record* (`docs/decisions/D167.md:15`). The design's D167 note says
  this point stands.
- D189 point 1's *one per record* (`docs/decisions/D189.md:14`), and D189 point 6: *A left conversation is not resumed again … going
  back to its agent or account opens another new part* (`:35-36`). The design and its D189 note say point 6 stands, reading it as
  "the left copy stays". But every carry resumes the conversation the old part left, and §2.2's move back to an earlier account
  resumes it there too. That is also D189's rejected *going back to a left conversation with what came after* (`:53`).

D131's reason no longer binds as it was written. It rejected the linked shape because every reader of records would have to learn a
fold (`D131.md:37-38`, `:41-46`), and D167 has since made the entry that fold for every reader. So the design has an answer, but it
must give that answer and name each rule it changes.

The session-context design's three reasons against copying (§9, `docs/2026-10-20-session-context-design.md:368-371`):

1. **It acts inside an account's home.** The design answers this narrowly: only the table's names, and only after the old process
   stopped. But "both homes are Daoris's own" is not the answer, because the home's contract says Daoris owns nothing inside it.
   The honest statement is: Daoris now reads the previous home's files of a conversation the line owns, and writes them into the
   next home, both by name only. There is precedent worth citing: the trust grant writes `.claude.json`, on the person's own act
   (`Accounts/SignIns/ClaudeTrust.cs:69-73`). There is also a gap. §2.1's conditions do not exclude the person's own tool home.
   A record with no account can be moved by a limit (`LimitMove.cs:66` requires no profile) or handed over by the person (the judge
   at `HandOver.cs:135-174` requires none either). A legacy conversation still goes on in the person's own home
   (`ToolHomeLeftovers.cs:9`, `:122-123`). Copying out of that home is exactly what D49 §4, D66 §3 and D197 point 3 forbid, and what
   §2.2 and §7 say never happens.
2. **It depends on a format the harness changes at will.** This is answered only where a change shows up as a missing file. The
   table is called versioned but holds no version: its "Read on" column is a date, so the carry runs on any version. A change that
   keeps the file names but moves meaning elsewhere (an index of conversations, a sidecar file keyed by the id) would resume without
   the turns and still be counted `resumed`.
3. **It can never cross makers.** Answered: the other half goes by the brief.

**What crosses between homes.** The never-copied list is sound for Claude Code, with three gaps:

- It lists `sessions/` as running processes' files, but for Codex `sessions/` is where conversations are stored, and the Codex row
  copies from it. The list has to be per agent.
- The Claude row was written from the projects folder and a names-only listing. Claude Code versions have also kept files keyed by
  the conversation id elsewhere in the home (`todos/`, `file-history/<id>/`, `session-env/<id>/`, `debug/<id>`); §1.5's listing
  names only `session-env/`. Each such name on the pinned version should be classed "copy" or "never", so a missing one is a
  decision and not a silent half-resume.
- The id is used to build Windows paths. Hold it to a token grammar (letters, digits, `-`) instead of only refusing `..`: a `:`, for
  example, names an alternate data stream.

Separately, the carried transcript names the first home's path for persisted tool outputs (§3.1). Once that account is removed
(D66 §3 deletes its directory), those references point nowhere.

**A resume that half-works.**

- **An id that loads with turns missing.** `session/resume` replays nothing (`Acp.cs:3214-3216`), so `resumed` only means the wire
  accepted the resume. The door already receives a `session/load` replay and drops it (`:3236`, `:3290-3294`). For a carried move:
  resume by load, count the replayed user turns against the turns Daoris holds for the old part, and say `resumed` only where they
  agree; otherwise fall back in the same start. This reads the agent's wire, not its store. It is also what lets the first real move
  measure context rather than acceptance. D131 rejected load-first because the record already holds the history; a carry is the one
  place where the replay is worth it.
- **A model or effort that differs.** A resume carries the new record's chosen model and effort (`Acp.cs:3260-3261`). Where the next
  account cannot offer that model, the open ends `ChoiceNotOffered` after the resume itself succeeded (`Acp.cs:814-821`), and §2.5
  does not list that case.
- **Thinking bound to its model.** The earlier turns' thinking blocks are signed. Whether the API accepts another account's
  signatures, or another model's, is unmeasured, and §3.1 does not list it. Some evidence lowers the risk: the account-rotation
  design saw a conversation outlive a re-sign-in in the maker's CLI (`docs/2026-10-01-account-rotation-design.md:338`), but that was
  the same home. Until 1f measures it, carry only where the new part asks for the model and effort the old part ran
  (`PriorSession.Ran.Models`, as `LeftBy` reads it, `LimitMove.cs:454-455`).
- **The refused turns' words.** The limit refused the turn after the agent had taken the prompt, so the carried transcript may
  already hold those words beside the refusal. Handing them again "each once" may put them in the agent's context twice. This is
  unmeasured, and 1f should record it.

**The fallback's trigger, and whether a failed resume is always seen.** §2.5's triggers all happen at or before the resume. Suppose
the carried part's first turn fails before any answer: the API refuses the carried history, it does not fit the new model's window,
or the model is not offered. None of the triggers covers that. The words then meet an error in a conversation that was never given
the brief, and the brief "kept with the move" (§2.4 step 2) has no durable place on the curve, live-limit or driven paths. At a
curve move nothing is waiting, so that first turn is the person's next message, perhaps hours later. Smallest change:

- Treat the carried part's first turn failing before any answer as the carry's failure, and fall back on the same process as §2.5
  says.
- Keep the brief on the new part until its first turn answers.
- Because of the 15-second end, carry only once the old part's watch has ended (its process gone); otherwise `refused busy`, and the
  brief.

At a curve move, step 6's "one line of Daoris's saying the account changed" is itself a prompt. It spends a turn on the new account
that nobody asked for. The prompt cache is per account, so that first request writes the whole context H, where a briefed move
writes S and B (`docs/2026-10-20-session-context-design.md:275-279`). Where nothing waits, resume and send nothing: the account line
belongs to the page, and the agent gets it with the person's next words. §5 argues against handing the brief alongside the carry at
40,000 characters; it should also state that a carry's first request is H, and 1f should read that turn's cache write.

**Paths the design does not cover:**

- **A carried driven part.** A driven start composes what waited on the ask's channel for the entry's next part (D167 point 7,
  `Driver.cs:2540`, `:2620`) and marks those messages handed once the record works (`:2712`). A carried part given only a resume's
  words would drop them while marking them handed.
- **The person's own hand-over.** It asks the outgoing agent for a closing note (`ChatHandOver.cs:145-149`). That turn is wasted
  where the move carries.

**A line handed to another agent, read as one conversation.** Keeping the brief for another agent is right. But once the page reads
as one conversation, the boundary line, `chat.taken-over` (`note.json:195`), has to say what the new agent knows. Today it says
nothing about it, where `moved.from` says *handed Daoris's brief*. Two other gaps on the page:

- §2.7 says the move is said once, by lines that are already there. That fails twice. Parts that are not moves (a sibling's request
  of the same ask, D167 point 2, or a *Try again*) have no move lines, so their opening line has to be the boundary. And the curve
  path writes two lines on each side (`chat.moved` and `moved.carried`).
- Pressing a part must still attend it, since the strip promises that "every act on the page stays the part shown's"
  (`PartsStrip.tsx:91-95`).

The rewording also leaves other "new session" words about the same moves (`work.json:146`, `notifications.json:69`).

**What is measured before anything depends on it.** Nothing is, by the owner's direction. That means the fallback has to cover every
failure, and the outcome has to be honest. With the first-turn fallback and the replay count above, the first real carried move
measures context, not just acceptance. Without them, `note.moved.carried`'s *its own conversation went with it* claims more than the
wire showed.

### 3. The rows

- **1a cannot prove its claim alone.** "A stand-in's conversation moved to another account's home resumes" needs a move to make it,
  and 1b is where the moves are wired. 1a should name one path, the curve's take (the owner's case on the install), and the start
  shape it needs (finding 2). Its proof should add:
  - the person's own home refused;
  - a same-named file that no record names, left alone, with the brief taken;
  - the move back to an earlier account replacing that account's copy;
  - an old process that has not yet exited, with the brief taken;
  - a first turn that fails after an accepted resume, with the brief on the same process;
  - a short replay, with the brief taken.
- **1b** leaves out Ask Daoris's `HelpStartAsync` path, and needs the driven carried part's first prompt named. Its "the come-up
  resumes from the box" holds only for the ended-record move. On the other paths, it is the new part's kept id that lets a later
  word resume it through the go-on (`ChatGoOn.cs:480-528`). The row should say which and prove each.
- **1c** needs 1a only for `own` and the carried lines. The line view (earlier parts above, the strip scrolling to and attending a
  part, the other-agent boundary worded) depends on nothing else, and it is what made the move read as a new session (§1.4), for
  both halves. Split it: 1c-i, the line view and the words of today's moves, with no prerequisite; then 1c-ii, the carried lines,
  after 1a.
- **The first row is not the one that gives the owner the most, soonest.** 1c-i fixes what the person saw for both halves and
  reverses no decision. 1a gives the agent its memory, but only once a path is wired and installed.
- **`handed: own`** needs a home (finding 6) before 1a or 1c can use it.
- **1d and 1e** are gated correctly. 1d's reading should include the per-agent never-list.
- **1f's proof** is the right shape. With the replay count it proves context. It should add the first turn's cache write, and
  whether the refused turns' words were already in the transcript.

### 4. Findings

1. **Blocking.** The carry changes decided rules the design does not name, and it says two of them stand: D131 point 4 and its
   rejection of one conversation over two records (`D131.md:24`, `:45-46`); D137 point 8 (`D137.md:31`); D167 point 1
   (`D167.md:15`); D189 point 1's *one per record* (`D189.md:14`); and D189 point 6 with its rejection of going back to a left
   conversation (`:35-36`, `:53`). **Smallest change:**
   - Name each of these in §4 and in the notes as amended for a carry.
   - Give D131 its answer: D167's entry is the fold every reader now has, `session.moved` counts a move rather than a session, and a
     teammate's mirror carries no conversation id.
   - Restate what stays: each record still names one conversation id, and a conversation left behind by a carry is never resumed
     again in its old home.
2. **Blocking.** The new part's resume is described with the wrong machinery. The judge answers `ended` with no words waiting
   (`Continuation.cs:217`), and a fresh start takes no `GoingOn` (`ChatRunner.cs:628-631`, `:2618-2619`). The record id is created
   inside the start (`:582-587`), and `LimitMove`'s account is chosen there too (`LimitMove.cs:141-144`). **Smallest change:** in
   §2.3-§2.4, say:
   - `ChatRunner.StartAsync`, the driven open and `HelpStartAsync` take the carried conversation.
   - The copy runs after the selection settles the account and before the spawn.
   - The new record's `harness.json` is written after the open.
   - `RunAsync` resumes it as a `GoingOn` does.
   - The conditions are judged by a carry judge of its own, not by `Continuations.Judge`.
3. **Blocking.** A resume that is accepted and then fails is not seen, and `resumed` proves only the wire. The first-turn failures
   (`Acp.cs:814-821`, plus the unmeasured thinking and model cases) are not among §2.5's triggers, and the kept brief has no durable
   home. **Smallest change:**
   - The carried part's first turn failing before any answer counts as the carry's failure, falling back on the same process, with
     the brief kept on the new part until its first answer.
   - A carried move resumes by `session/load` and counts the replayed user turns against the old part's; `resumed` only where they
     agree.
   - Carry only where the new part asks for the old part's model and effort, until 1f has measured the cross-account case.
4. **Blocking.** The person's own tool home is not excluded. `LimitMove.cs:66` and `HandOver.cs:135-174` move records with no
   account, and legacy conversations still go on there (`ToolHomeLeftovers.cs:9`, `:122-123`), against D49 §4, D66 §3, D197 point 3
   and §7's own claim. **Smallest change:** §2.1 adds "both records name an account of Daoris's; a part on the person's own sign-in
   moves by the brief", and 1a's proof tests it.
5. **Not blocking.** The old process is not known to have stopped: the end returns after 15 seconds (`ChatHandOver.cs:16`, `:229`).
   **Smallest change:** carry only once the watch has ended; otherwise `refused busy`, and the brief. Add it to 1a's proof.
6. **Not blocking.** `handed: own` has nowhere to live outside Ask Daoris (`HelpConversations.cs:11`, `:156`, `:188`), and
   `carried=` duplicates `briefed` (`Continuation.cs:585-593`). **Smallest change:** name where a part's handing is kept for every
   kind of part (a machine-only record field through the field chain in `session-record-fields`, or a file beside `harness.json`)
   and every reader of it (the strip's "began" words, the fold, the log), and keep one log field.
7. **Not blocking.** The table and the never-copied list:
   - The list's `sessions/` contradicts the Codex row.
   - The table holds no version.
   - Files keyed by the id elsewhere in the home are not classed.
   - The id is not held to a grammar.

   **Smallest change:**
   - Make the never-copied list per agent.
   - Have each table row name the adapter version it was read on, carry only on that version (`refused version` otherwise), and
     re-read the row when the pin moves.
   - Class every home name holding the id as "copy" or "never".
   - Hold the id to a token grammar.
8. **Not blocking.** Three paths are uncovered: Ask Daoris's room (`LimitMove.cs:402-416`); a driven carried part's channel messages
   (`Driver.cs:2540`, `:2620`, `:2712`); the closing note on the person's hand-over (`ChatHandOver.cs:145-149`). **Smallest change:**
   - 1b lists `HelpStartAsync`, carrying only where the room is the previous part's tree.
   - Name the driven carried part's first prompt, including the channel's held messages.
   - Ask no closing note where the move carries.
9. **Not blocking.** At a curve move nothing waits, so Daoris's account line spends an unasked turn, and the first request on the new
   account writes the whole context uncached (session-context design §5, `:275-279`). **Smallest change:**
   - With no words waiting, resume and prompt nothing; the line is a note, handed with the next word.
   - §5 states the cost, and 1f reads the first turn's cache write.
   - §3.1 adds whether the refused turns' words are already in the carried transcript.
10. **Not blocking.** The page's words. The boundary for another agent does not say the new agent has only the brief
    (`note.json:195`). The curve path writes two lines on each side. Parts that are not moves have no move lines. The strip's press
    must still attend (`PartsStrip.tsx:91-95`). Other "new session" words remain (`work.json:146`, `notifications.json:69`).
    **Smallest change:**
    - `chat.taken-over` gains "handed Daoris's brief of it, not its turns", in en and zh.
    - One line per side wherever a move carries.
    - A part's opening line is its boundary where it was not a move.
    - Pressing a part attends it and scrolls to it.
    - Name the remaining strings, or say why they stay.
11. **Not blocking.** The rows: 1a cannot prove its claim without a path; 1b's box proof holds for one path only; §2.4's box sentence
    overstates what the box holds (`LimitMove.cs:250`, `:293-310`); 1c waits on 1a for what does not need it. **Smallest change:** as
    §3 says. 1a wires the curve's take and adds the listed proofs; 1b names the box for the ended move and the go-on for the others;
    §2.4 says the box carries the bounds the brief is recomposed from; 1c splits, with 1c-i first.
12. **Not blocking.** Citations and wording against the code: `Driver.cs:4548-4559` and `:4515-4528`; the home's two spellings
    (`DaorisHome.cs:324`, `:370`); §2.2's "Daoris's own homes" against `HarnessSettings.cs:878`; §2.5's same-process fallback, which is
    new (`ChatRunner.cs:2616-2640`). **Smallest change:** correct the lines, reach both homes only through `HarnessSettings.ProfileHome`,
    reword §2.2's reason as the narrow write it is, and say the same-process fallback is a new path.

12 findings, 4 blocking.

Verdict: another round. The direction is right and so is the split into two halves: the same agent keeps its own conversation,
another agent goes by the brief, and the page reads the line as one. The never-copied list is careful and the fallback is the right
safety net. But the design reverses five decided rules and names one of them; it builds the new part's resume on a judge and a start
path that cannot carry it; it leaves a resume that is accepted and then fails unseen; and its conditions let a move copy out of the
person's own tool home. Round 2 can be short: name the rules with D131's answer (finding 1), describe the start shape the carry
actually needs (finding 2), and close the first-turn, verification and own-home gaps (findings 3 and 4). Findings 5 to 12 are
changes the steward can take into that round. If something should move before round 2, the line view and the other-agent boundary
(1c-i) reverse no decision and could be split off as a row of their own.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `260881273`: 10 findings, 3 blocking; accept after named changes the steward can check without another read. Codex's read is owed, and by D191's note of 2026-10-24 this design is not accepted on one maker's read.

### 0. Round 1's findings, checked

Paths without a root are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, as the design has them; page paths are under
`src/Daoris.Web/src/`. I read main at `e89b2035a`. Its `src/` differs from the design's `426ed2b73` only in two workflow files.

| # | Round 1 (blocking in bold) | Round 2's answer | Checked against the code | Standing |
|---|---|---|---|---|
| **1** | **The carry changes rules the design does not name** | §4 and the notes name D131 points 2, 4 and 5 and its rejections, D137 point 8, D167 point 1, and D189 points 1, 5 and 6 with their rejections | The named rules read as cited (`D131.md:16-18`, `:24`, `:41-47`; `D137.md:31`; `D167.md:15`; `D189.md:14-17`, `:35-36`, `:51-54`). The carry also changes rules that are still unnamed: D131 point 2's *never refused* across a version and two of D131's rejections, two of D137's rejections, D189 point 7, and three of the code's own remarks (finding 5) | Taken, incomplete |
| **2** | **The new part's resume is described with the wrong machinery** | `CarryAsk` is taken by the three starts. Each runs the selection, then `Carries.Judge`, then the open, then the copy and `harness.json`, then the spawn | Holds. The selection comes before the open (`ChatRunner.cs:536-546`, `:582-587`; `Loop/Driver.cs:2417-2439`), and `LimitMove` passes `profile: null` (`LimitMove.cs:141-145`). One step is miscited: Ask Daoris's new part runs through `ChatRunner.RunAsync`, not the one-shot run (finding 4) | Taken |
| **3** | **A resume that is accepted and then fails is not seen; `resumed` proves only the wire** | Resume by load and count the replay. A first turn that fails falls back on the same process. Add a `model` condition | All three pieces are there, and none holds as worded. A provider's refusal arrives as the agent's message (`Agents/Acp/Acp.cs:144-151`), and a limit's refusal or the person's stop is not excluded. The replay has no stated counterpart in the record (`Conversations/Domain/ChatMessage.cs:174-175`, `:188-200`). A chat never ends `ChoiceNotOffered`: it runs a model that was not offered on the account's default, because only the driven start hands the re-ask (`Acp.cs:673`, `:713-733`; `Loop/Driver.cs:4259`) | **Open** (findings 1 to 3) |
| **4** | **The person's own tool home is not excluded** | The `own-home` condition; both homes reached only through `ProfileHome` with the settled accounts | Holds against `LimitMove.cs:66`, `Conversations/HandOver/HandOver.cs:152-173`, `Conversations/Chat/CurveMove.cs:73` and `Platform/ToolHomeLeftovers.cs:118-124` | Taken |
| 5 | The old process is not known to have stopped | `busy`: the watch must have ended; the fifteen seconds running out does not count | `EndForHandOverAsync` (`Conversations/Chat/ChatHandOver.cs:224-230`) returns without saying which ended; 1a has to make it say | Taken |
| 6 | `handed` has nowhere to live; `carried=` duplicates `briefed` | A machine-only field through the record's chain; `briefed` alone in the log | The chain's rows for the open are named. But `handed` is set after the open, and a field set later has its own update door (`.claude/knowledge/session-record-fields.md:40`). `.carry.json` has no row in the session's file inventory (finding 8) | Taken, with residue |
| 7 | The table and the never-copied list | A list per agent; a table per pinned version; every name holding the id classed; a grammar for the id | Holds; the Codex row is gated to 1d | Taken |
| 8 | Three paths are uncovered | `room`; the driven part's first prompt; no closing note where the move carries | Holds (`LimitMove.cs:402-416`, `Loop/Driver.cs:2537-2540`, `ChatHandOver.cs:145-149`). The driven run's same-process fallback has no named place (finding 10) | Taken |
| 9 | A curve move spends an unasked turn, uncached | Nothing is prompted; §3.1 and §5 state H; 1f reads the cache write | Holds (`CurveMove.cs:36-37`, `ChatRunner.cs:2618-2619`) | Taken |
| 10 | The page's words | `chat.taken-over` reworded; one line per side; the *began* words as a boundary; a press attends and scrolls; two more strings reworded | Taken in direction. The rewording breaks lines written before and drops H and B, and two cases still leave two lines on one side (finding 9) | Taken, with residue |
| 11 | The rows | 1a wires the curve; 1b separates the box from the go-on; 1c split in two | Holds | Taken |
| 12 | Citations and wording | Corrected | `Loop/Driver.cs:2422`, `:2526`, `:4515-4528`, `:4548-4559`; `Platform/DaorisHome.cs:324`, `:370`; `Accounts/Harnesses/HarnessSettings.cs:878`, `:881-882`; `ChatRunner.cs:2616-2640`; `Agents/Adapters/Adapters.cs:2060`: each stands as now cited | Taken |

### 1. Claims about the code

These claims are wrong:

- **§2.3 step 5, "the driven run and Ask Daoris's by the run's resume (`Acp.cs:774-776`)".** Only the driven run takes the
  one-shot `AcpSession.RunAsync` (`Acp.cs:798-812`). Ask Daoris's new part is run by `ChatRunner.RunAsync`
  (`LimitMove.cs:414-417`). That is the chat's protocol path, which resumes at `ChatRunner.cs:2618`, as Ask Daoris's own go-on
  does (`Conversations/Chat/ChatGoOn.cs:651-653`).
- **§1.5 and §2.5, "a choice the account does not offer ends the open `ChoiceNotOffered` after the wire accepted the resume
  (`:665-699`, `:814-821`)".** This holds only where the start hands a re-ask (`ModelRefused`), and only the driven start does
  (`Loop/Driver.cs:4259`). There the re-ask's later choice is set **on the same session** (`Acp.cs:676-706`), and the open ends
  only where no later choice comes (`:678`). A chat's session is built with no re-ask (`ChatRunner.cs:2591-2601`). So the set's
  refusal is not judged (`Acp.cs:673`), the model is set aside as not carried (`:729-733`, `:710`), and the session runs on the
  account's default. `:814-821` is the one-shot run's catch, which a chat never reaches.
- **§2.1's `model` row, "the model and effort the old part ran (`PriorSession.Ran.Models`, as `LeftBy` reads it)".**
  `Ran.Models` is not the model the part last ran. It is a bounded set of every model any turn reported, first seen first, up to
  eight (`Sessions/Domain/ActorRun.cs:120`, `:142-150`). It is provenance for a maker's trailer (`LimitMove.cs:453-455`), and it
  holds no effort. The part's last acknowledged model and effort are the last of `Ran.Entries` (`ActorRun.cs:119`,
  `:151-154`). Nor does a curve move's new part ask for any model. The take starts with no order
  (`ChatHandOver.cs:156-157`), so `chosen` is null and what it asks is `ActorAsked.Before`, the account's default
  (`ChatRunner.cs:499`, `:1268`; `ActorRun.cs:9`), which sets nothing (`Acp.cs:715`).
- **§2.5, "the turn fails before any update of the agent's answer (the API refuses the carried history or … signed
  thinking …)".** A provider's refusal first arrives as an update of the agent's answer. The adapter says the provider's
  `API Error` as the agent's message, then the turn ends `refusal` (`Acp.cs:144-151`, APIREFUSAL1, seen on the install). A
  limit's refusal ends `error` after the account's cooling line (`Conversations/Domain/LimitHeld.cs:7`, `:37`).
- **§2.3, "Read back": "the user turns it replays … against the turns Daoris's record shows the agent took".** The record
  keeps the person's words (`ChatMessage.Words`, `ChatMessage.cs:188-200`). What the agent was sent, and stored, is the composed
  prompt: the brief, the briefing, the told process and the preface, then the words (`ChatMessage.cs:174-175`). A go-on sends
  each word as a block of its own (`Conversations/Domain/ResumeAsk.cs:65-66`). The record also keeps the door's echo of a
  prompt as a person's `User` event (`Acp.cs:2956-2957`; `Daoris.Desktop.Driver.Tests/Sessions/QuestionFilesTests.cs:196-209`).
  "The turns" therefore has no stated counterpart in the record.
- **§2.3 `forked`, and §6's stand-in switch "fork an id".** On the protocol door, neither a load nor a resume names an id
  (`Acp.cs:3218-3221`). `ResumeAsync` keeps the id it asked for and tells no listener (`:3255`). `forked` can arise only on the
  pipe door's `init` line, which is 1e's.
- **§2.5, "the brief recomposed from it is the one the move would have handed".** The take composes the brief before the end,
  with the agent's own hand-over note in it (`HandOver.cs:247-251`), and the recipe (the old record, the why, the bound) does
  not hold that note. A brief composed again later also reads the tree's uncommitted state as it is then (D189 point 4).

These I checked and found as written:

- **The start's order:** the selection before the open (`ChatRunner.cs:536-587`; `Loop/Driver.cs:2431-2439`), and the opening
  line after it (`ChatRunner.cs:603-604`).
- **`LimitMove`:** its own walk (`:85-88`), its end (`:133`), `profile: null` (`:141-145`), the order of the words
  (`:156-166`), no account required (`:66`), the box (`:250`, `:266`, `:293-310`) and `HelpStartAsync` (`:393-418`).
- **The hand-over:** `HandOverTake.RunAsync` (`HandOver.cs:232-296`, the opening line riding on the first message at `:271`)
  and its judge (`:135-174`, `:190-193`).
- **`ChatHandOver`:** the fifteen seconds (`:16`, `:224-230`), and the closing note asked before the take (`:145-149`,
  `:154-158`).
- **`CurveMove`:** `:36-37`, `:70-77` and `:106-118`.
- **The judge and the log:** `Continuations.Judge` (`Conversations/Domain/Continuation.cs:207-239`) and `session.moved` with
  `briefed` (`:585-593`).
- **The resumes:** `ChatRunner.cs:2616-2640`, and the protocol door's resumes and dropped replay (`Acp.cs:3224-3294`).
- **The homes:** `DaorisHome.cs:324`, `:370` and `HarnessSettings.cs:878`, `:881-882`; the trust grant made only on the
  person's own act (`Accounts/SignIns/ClaudeTrust.cs:66-73`); the kept ids (`Conversations/GoOn/HarnessConversations.cs:8-10`,
  `:31`, `:54`).
- **Ask Daoris's handing:** the field and the fold (`Help/HelpConversations.cs:11`, `:118`, `:124`, `:188-189`, `:233-249`).
- **The page:** the strings (`locales/en/note.json:194-206`, `locales/en/work.json:146`, `locales/en/notifications.json:69`)
  and the note codes (`work/noteLines.ts:286-295`); `work/head/PartsStrip.tsx:90-113`; `work/WorkFrame.tsx:2452-2461`.
- **The rest:** `Accounts/Usage/UsageHistory.cs:659`, and the stand-in
  (`Daoris.Desktop.Driver.Tests/Conversations/ChatGoesOnProcessTests.cs:748`).

### 2. The mechanism's holes

#### The reversal of D189 point 1

The reasons round 1 named are now answered:

- **The three reasons of the session-context design's §9.** The act inside an account's home is narrowed to the table's names,
  after the writer has exited, through `ProfileHome` alone. The format is held to a pinned version, and to a read-back. Across
  makers, nothing is carried.
- **D131's reason, that every reader would learn a fold.** D167's entry is now that fold for every reader.

What still binds is a set of rules the carry changes without naming them (finding 5):

- **D131 point 2's last sentence and a rejection.** The sentence says a changed harness version is *said on the resumed run's
  first line, never refused* (`D131.md:18`), and D131 rejects *refusing a resume across a harness version* (`:50-51`). §2.1's
  `version` condition refuses exactly that, for a carry. The reason is sound, since the table's names are bound to a version,
  but this is a third rule of D131 that the design changes.
- **Reading the agent's home.** D131 rejects *checking for the conversation in the agent's home before resuming* (`:48-49`),
  and D137 rejects *reading the account's home* for one (`D137.md:55`). The carry checks and reads there, by name.
- **The linked shape in D137.** D137 rejects *a new record continuing the old conversation, linked to it* (`D137.md:53`). The
  D137 note names point 8 alone.
- **Compaction at a move, and its cost.** D189 point 7 says *Daoris's compaction is a new part handed the brief: at a move*
  (`D189.md:37-38`), and D189's reason calls the brief *cheaper on every later request* (`:46-48`). A carry keeps H on every
  later request on the new account. The session-context design's §5 already claims no way on is cheaper, so the answer exists;
  it has to be given.
- **The code's own remarks.** The rule is also stated at `LimitMove.cs:12-13`, at `HarnessConversations.cs:8-10` (*never read
  from the agent's own home … which D125 rejected*), and at `Continuation.cs:177-181`. §4 has 1a reword only
  `HarnessSettings.cs:878`.

None of these stops the carry from being made. Each is a rule it changes without naming, which is what round 1's finding 1
asked to close.

#### Anything of one account carried into another's home

The copy list, the per-agent never-list, the id's grammar and the `own-home` condition hold. No credentials, configuration,
history of prompts or other conversation crosses, and the person's own tool home is never a source or a destination.

The one write that can destroy content that is not the line's is the move back (finding 6). The session-messages design §4.3
lets the person open Daoris's conversation in their own terminal with the account's home: *turns run there are in the
harness's conversation and not in the record*. D137 rejects adopting such a conversation in place, because two clients would
write one conversation (`D137.md:56`). §2.2's move back *replaces* the left copy with the line's newest, which overwrites those
turns.

Replacing that copy is safe only where the left copy is still what the line left. That is exactly the case where the
destination file is a byte prefix of the source, since the source is the same file grown since.

#### A resume that half-works

- **An id that loads with turns missing** (finding 2). The check is the right check, but it has no unit and no shape.
  - What the pinned claude-agent-acp replays on `session/load` is not read: a tool result (a user-role entry in Claude Code's
    file), a compaction's summary, an interruption, a prompt of several blocks. It is readable from the package without a turn
    spent, as `Acp.cs:3218-3221` read this adapter's capabilities and D137 §1 read codex-acp's bundle.
  - Until that shape is read, the rule is either never met (every carry falls back `short-replay`, which is safe, and the owner
    still gets a brief), or met loosely enough to count a conversation with turns missing. The second is what the check exists
    to stop.
  - §6's stand-in is written to replay "that file's user turns", so it proves only itself.
  - The steadier anchor is the agent's own messages. The record keeps them as the wire gave them, each with its `messageId`
    (`Acp.cs:2950-2952`), where the package shows they replay unchanged. Daoris's prompt is composed of more than the record
    keeps of it.
- **A model or effort that differs** (finding 3). The condition as written reads the wrong field and judges what is asked.
  - For a curve move it judges the account's default, which is unknown before the spawn. Read literally, it refuses the
    owner's own case every time. Read as "both ask the default", it passes where the two accounts' defaults differ.
  - Where a chat's carried start asks a model the next account does not offer, nothing refuses it. The model is set aside as
    not carried, and the carried conversation runs on the new account's default, with the old model's signed thinking in its
    history (`Acp.cs:713-733`). This is the case the condition exists for.
  - On a driven run, the re-ask sets a later choice's model on the carried session (`:676-706`).
  - §2.5's fallback for `ChoiceNotOffered` opens `session/new` on the same process with the same choice asked, which is
    refused again. Today's briefed move asks no model at all, so the carry adds a way for a move that runs today to end without
    going on.
- **Thinking bound to its model.** With findings 1 and 3 taken, it is covered twice: the model is held equal, and a provider's
  refusal of the history is seen at the first turn.

#### The fallback's trigger, and whether a failed carry is always seen

- **The first turn** (finding 1). The trigger "fails before any update of the agent's answer" misses the case it names, since a
  provider refusal is said as the agent's message first (`Acp.cs:144-151`). That carry counts `held` and `handed: own`. The
  trigger also takes in ends that are not the carry's own:
  - **A limit's refusal** of the first turn. The fallback replaces the kept id and hands the brief on the limited account, and
    is refused there. LIMITGOON1's next move then carries the fallback's empty conversation, not the line's own.
  - **A transient failure.** The door already tries it again in the same conversation (`Agents/Errors/AgentErrors.cs:14`;
    `Acp.cs:841-847`).
  - **An access refusal.** No brief on that account mends it.
  - **The person's stop before any answer.** The fallback would hand them again the words they stopped.

  D210 already reads every failure into four kinds (`AgentErrors.cs:5-19`). The rule should be stated in those kinds and the
  turn's stop reason.
- **After the shell ends** (finding 7).
  - Once `harness.json` names the carried id, the next word goes through the go-on. The go-on resumes by `session/resume`, with
    no read-back (`Acp.cs:3227`), and its refusal ends in `CannotGoOn` or `LostAsync` (`ChatGoOn.cs:484-492`;
    `ChatRunner.cs:2622-2640`). The carry's file sits beside it unread. Where the shell ended before the load, no count was ever
    made.
  - The come-up of an ended move hands again the box's composed message, the brief in it (`LimitMove.cs:351-361`, `:300-309`).
  - `HarnessConversations.Keep` swallows a failed write (`HarnessConversations.cs:33-43`), so step 4 can leave a carried
    process under a record with no id.

#### A line handed to another agent

This holds. `chat.taken-over` gains *handed Daoris's brief of it, not its turns*, and `moved.from` already says *handed Daoris's
brief*. LIMITGOON2's part in Ask Daoris is written `brief` (`LimitMove.cs:406`) and folds as today. The line view draws turns
the new agent does not have, and the boundary says so once. Nothing promises context the agent does not hold.

#### What is measured before anything depends on it

By the owner's direction, no turn is spent. But two of §3.1's unknowns cost no turn at all: what the load replays, and whether
the load reads anything else in the home. Both are in the pinned package's source, and they should be read before 1a writes its
table row and its stand-in. With findings 1 to 3 taken, the first real carried move measures the agent's context, not only the
wire's acceptance. 1f should then also record the acknowledged model and effort, and the first turn's stop reason.

#### The page

Finding 9 covers these:

- **Lines written before.** The reworded `moved.on` and `moved.from` take `{{part}}`, but the code's values are `session`,
  `agent`, `account`, `why`, `tokens` and `count` (`work/noteLines.ts:290-291`). Every line written before has no `part`. The
  code's convention is a new code, with the old one's words kept for lines written before (`noteLines.ts:230-237`).
- **H and B.** The reworded `moved.on` drops `{{tokens}}` and `{{count}}`, which D189 point 8 keeps on every move's line.
- **The curve's figures.** §2.7 says the carried lines take `chat.moved`'s figures, but `moved.carried-from` has no
  `window`, `used` or `curve`.
- **A third line.** `RotatedOpening.Say` writes the new part's opening line at its open (`ChatRunner.cs:603-604`;
  `Accounts/Rotation/AccountRotation.cs:600-610`), a third line at the boundary.
- **A carry that fails later.** A carried part whose first turn fails holds both `moved.carried-from` (written at step 6) and
  `moved.carry-failed`.
- **The flowchart.** The strip is also the rail of `PartsFlow` (`PartsStrip.tsx:102-103`), whose press has to attend and
  scroll too.

### 3. The rows

- **SWITCHCTX1c-i is the right first row.** It fixes what the person saw for both halves and reverses no decision. But it sits
  inside a design that, by D191's note of 2026-10-24, is not accepted on one maker's read, so it waits for Codex's owed read
  like everything else. It gives the owner the most only if it can be built now. Split it out as a row of its own under D167
  and D189 point 8, accepted on its own read.
  - Its proof "a curve move's line the same with one line per side" has to say what becomes of the opening line, and its
    rewording has to keep the lines written before (finding 9).
- **SWITCHCTX1a** names the right path, the curve's take, and can be built alone. Its proofs are the right list. Add to them:
  - a provider refusal said as the agent's message and ending `refusal`, which falls back;
  - a limit's refusal of the first turn, which goes to the limit's move with the same id;
  - the person's stop and a transient retry, neither of which falls back;
  - the old part's model asked and not offered, which falls back with today's choice;
  - a stand-in that replays in the shape read from the package;
  - a left copy that grew elsewhere, which is left as it is (`exists`).

  As written, its proof of "resumes with its earlier turns" proves only the stand-in's own replay (finding 2).
- **SWITCHCTX1b** follows 1a. Two places for the driven path are missing:
  - **The run.** The same-process fallback goes inside `AcpSession.RunAsync`, where a refused `ResumeAsync` throws out of the
    run today (`Acp.cs:811-812`).
  - **The instruction.** The briefed instruction has to be composed before the spawn for that fallback to hand it.

  The come-up's re-delivery from the box has to drop the brief where the box says the carry landed.
- **SWITCHCTX1c-ii, d and e** are gated correctly, and their proofs fit their claims.
- **SWITCHCTX1f's proof** is the right shape. Add the acknowledged model and effort, and the first turn's stop reason.

### 4. Findings

1. **Blocking. The carried part's first-turn trigger misses the provider's refusal and takes in ends that are not the carry's**
   (§2.5). The provider's refusal is said as the agent's message and the turn then ends `refusal` (`Acp.cs:144-151`), so a
   history the API refuses counts `held`. A limit's refusal (`LimitHeld.cs:7`, `:37`) makes the fallback brief the limited
   account and replace the kept id, so the limit's next move carries the empty fallback conversation. A transient, an access
   refusal and the person's stop would each re-hand words, or brief where no brief helps. **Smallest change:**
   - Judge the first turn by how it ended, in D210's kinds (`AgentErrors.cs:5-19`) and its stop reason.
   - The carry's failure is an end on `refusal`, or a `Fatal` failure at the prompt.
   - A `Limit` goes to the limit's move, with the kept id and the carry's file left as they are.
   - A `Transient` that runs out, an `Access` refusal and a person's stop leave the carry pending, its file kept.
   - `held` only on any other end.
   - 1a proves each.
2. **Blocking. The read-back has no stated unit, and the replay it counts is not read** (§2.3, §3.1, §6). The record keeps the
   person's words (`ChatMessage.cs:188-200`), and the agent stores the composed prompt (`:174-175`; blocks per word,
   `ResumeAsk.cs:65-66`; the door's echo as a person's event, `Acp.cs:2956-2957`). What the pinned adapter's load replays is
   unread. The stand-in is written to the rule it tests. **Smallest change:**
   - Before 1a, read the pinned claude-agent-acp's `loadSession` replay from its package, as `Acp.cs:3218-3221` read its
     capabilities.
   - Write in §2.3 the unit and the anchor from that reading: which updates make one turn and how each is matched. Prefer the
     agent's own messages, kept with their `messageId` (`Acp.cs:2950-2952`), where they replay unchanged.
   - Build the stand-in to that shape.
   - Where that version's replay holds nothing that can be matched, refuse the carry on it (`door`), and say so.
3. **Blocking. The model and effort condition reads the wrong field, judges what is asked, and its fallback is refused
   again** (§2.1, §2.5). `Ran.Models` is a set of every model a turn reported (`ActorRun.cs:120`, `:142-150`). A curve move's
   new part asks the account's default (`ChatHandOver.cs:156-157`; `ChatRunner.cs:499`, `:1268`; `ActorRun.cs:9`). A chat has
   no re-ask, so an asked model that is not offered is set aside and the carried conversation runs on the account's default
   (`Acp.cs:673`, `:713-733`); a driven run's re-ask sets another model on the carried session (`:676-706`). The
   `ChoiceNotOffered` fallback asks the same choice again. **Smallest change:**
   - A carried start asks the old part's last acknowledged model and effort (the last of `Ran.Entries`, `ActorRun.cs:119`)
     explicitly, with no re-ask.
   - The condition is judged after the set, on the session's acknowledged options and the `Carried` callback (`Acp.cs:710`).
   - Where either differs or is refused, fall back by opening `session/new` with the choice today's move would have asked,
     and hand the brief.
4. **Not blocking. Five claims about the code are wrong** (§1): Ask Daoris's resume path (`LimitMove.cs:414-417`,
   `ChatRunner.cs:2618`); `ChoiceNotOffered` as the end for every door (`Loop/Driver.cs:4259`, `Acp.cs:676-706`); `Ran.Models`
   (`ActorRun.cs:119-150`); `forked` on the protocol door (`Acp.cs:3218-3221`, `:3255`); and the recomposed brief being the
   move's (`HandOver.cs:247-251`). **Smallest change:** correct each, and drop the stand-in's "fork an id" switch from 1a's
   protocol-door proof.
5. **Not blocking. Rules the carry changes that are still unnamed** (§2): D131 point 2's *never refused* across a version
   (`D131.md:18`) and its rejections of refusing across a version and of checking the agent's home (`:48-51`); D137's rejections
   of a linked new record and of reading the account's home (`D137.md:53`, `:55`); D189 point 7 and its reason's *cheaper on
   every later request* (`D189.md:37-38`, `:46-48`); and the code remarks at `LimitMove.cs:12-13`,
   `HarnessConversations.cs:8-10` and `Continuation.cs:177-181`. **Smallest change:** name each in §4 and in the notes, and give
   point 7's answer from the session-context design §5 (no way on is claimed cheaper). Have 1a reword those three remarks beside
   `HarnessSettings.cs:878`.
6. **Not blocking. A move back can overwrite another client's turns** (§2.2). The session-messages design §4.3 lets the person
   run Daoris's conversation in their own terminal with the account's home, and D137 rejects writing it in place
   (`D137.md:56`). **Smallest change:**
   - Replace the destination file only where it is a byte prefix of the source.
   - Under `<id>/`, add only the files that are missing.
   - Otherwise refuse with `exists` and hand the brief; a rename the disk refuses falls back the same way.
   - 1a proves that a left copy which grew elsewhere is left as it is.
7. **Not blocking. A carry the shell interrupts, or whose id write fails, is not watched** (§2.4, §2.5). The go-on resumes
   without reading the carry's file (`ChatGoOn.cs:480-586`, `Acp.cs:3227`); the come-up re-delivers the brief
   (`LimitMove.cs:351-361`); a failed `Keep` is silent (`HarnessConversations.cs:33-43`); the recipe lacks the agent's own note
   and is composed as of its own time. **Smallest change:**
   - The go-on, wherever `.carry.json` stands, takes the carried start's load, count and same-process fallback.
   - The come-up composes without the brief where the box says the carry landed.
   - Step 4 checks the write, and falls back `unkept` where it failed.
   - §2.5 says the brief is composed as of then, and the recipe keeps the agent's own note where one was written.
8. **Not blocking. Where `handed` and `.carry.json` are kept** (§2.3, §2.5). `own` is set after the first answer, and a field
   set later has its own update door (`session-record-fields.md:40`). `.carry.json` is a per-session file that needs a row in
   the inventory (`Sessions/History/SessionHomeFiles.cs:91-124`). A delete or a clear never removes the harness's conversation
   from an account's home (`SessionHomeFiles.cs:34-35`), and a carried line now leaves one copy in every home it ran on.
   **Smallest change:**
   - Name `handed`'s update: the store's method, the route, the client's call, and their tests.
   - Add `.carry.json` under the `hand-over` or `mark` name of the inventory.
   - Say that a carried line's copies stay, as the one copy does today, until each account is removed.
9. **Not blocking. The page's words** (§2.7). **Smallest change:**
   - Word the reworded lines as new codes, keeping the old codes' words for lines written before (`noteLines.ts:230-237`,
     `:290-291`), or derive `part` on the page and give a stated fallback.
   - Keep `tokens` and `count` on a briefed move's line (D189 point 8).
   - Put the curve's figures on the carried pair, or say which line holds them.
   - Say what the opening line (`ChatRunner.cs:603-604`) becomes at the boundary.
   - Write `moved.carried-from` at the first answer, or have `moved.carry-failed` replace it.
   - Make `PartsFlow`'s press attend and scroll as the strip's does.
10. **Not blocking. The rows** (§3). **Smallest change:**
    - Split 1c-i into a row of its own, accepted apart from the reversal, so it can be built before Codex's read.
    - Add to 1a's proofs the cases of findings 1, 3 and 6, and the stand-in shaped by finding 2.
    - Name 1b's same-process fallback inside `AcpSession.RunAsync` (`Acp.cs:811-812`) and the briefed instruction composed
      before the spawn.
    - Add to 1f the acknowledged model and effort, and the first turn's stop reason.

10 findings, 3 blocking.

**Verdict: accept after the named changes, which the steward can check without another read.** That is my read's verdict; by
D191's note of 2026-10-24 the design still waits for Codex's owed read. Round 2 took round 1's blocking findings on the rules,
the start's shape and the person's own home, and they now hold against the code. The direction, the two halves, the copy and
never-copy lists and the start order are sound. What remains blocking sits in one layer, the one round 1's finding 3 asked for:
whether a carry is seen when it fails and counted only when it holds.

- **The first-turn trigger** misses the provider's refusal, which arrives as the agent's message, and takes in a limit's
  refusal and the person's stop (finding 1).
- **The read-back** has no unit, and the adapter's replay it counts was never read, though it can be read at no cost
  (finding 2).
- **The model condition** reads a set and judges what is asked. In a chat, an unoffered model silently runs the carried
  conversation on the account's default (finding 3).

Each change is stated against the code lines that show it, and so are findings 4 to 10, which the steward can take in the same
pass. Finding 2's reading of the pinned package may show that the replay cannot be matched; the change already says what then
holds: no carry on that version, said. 1c-i should be split out now, since it reverses nothing and need not wait for the second
maker.
