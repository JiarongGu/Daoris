# SWITCHCTX1: the reads of rounds 1 to 4 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-switch-context-design.md` and its notes at `ff2461081`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out and is owed; under D191's note of 2026-10-24 this design, which reverses D189 point 1 on an agent's own transcript, is not accepted on one maker's read. The Opus foundation reader asks another round (12 findings, 4 blocking). Round 2 answers it. The Opus read of round 2 follows, which round 3 takes in the design's §9, and Codex's read of round 3, which round 4 answers in the design's §10. Both makers then read round 4, neither shown the other's read, and SWITCHCTX1c-i, the line view, was accepted alone on those two reads (the design's Standing).

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

## Codex, round 3

Codex's read of round 3 at `972b17577`: 26 findings, 17 blocking; another round. It ran read-only on main from outside the install, its links made repository-relative.

Read-only review completed; nothing changed. I formed my findings before reading the earlier reviews. The adapter bundles cited in §1.7 are absent from the permitted checkout, so their internals remain independently unverified.

#### 1. Claims about existing code and packages

1. **Blocking — child replay is not already filtered.** `docs/2026-10-24-switch-context-design.md:250` says the existing filter passes over subagent replay, but `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Acp/Acp.cs:3432` matches only the parent session ID. Parent announcements can be suppressed while child messages reach `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Acp/AcpStreams.cs:98`, find no registered child, and fall through into the parent record.

2. **Blocking — `LimitHeld` cannot recognize the replay exception specified.** `docs/2026-10-24-switch-context-design.md:396` permits an extra assistant message containing “the limit’s own sentence, which `LimitHeld` already reads.” `src/Daoris.Desktop/Daoris.Desktop.Driver/Conversations/Domain/LimitHeld.cs:37` instead recognizes coded cooling notes followed by an `error` turn; it contains no sentence matcher.

3. **Blocking — the adapter version does not identify the transcript writer.** The table keys Claude’s file layout to claude-agent-acp 0.84.0, including “both doors” (`docs/2026-10-24-switch-context-design.md:317`), but `src/Daoris.Desktop/Daoris.Desktop.Driver/Accounts/Harnesses/HarnessSelection.cs:604` explicitly selects the underlying Claude executable independently. Bind file-layout compatibility to the actual CLI version as well; the pipe door needs its own versioned row.

4. **Non-blocking — package evidence remains unavailable here.** §1.7’s `getSessionMessages`, model restoration and message-ID claims cite an installed bundle, not checkout artifacts (`docs/2026-10-24-switch-context-design.md:233`); repository comments also reference another bundle/SDK pair (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Adapters/Adapters.cs:1895`). Preserve exact package identities and source-derived replay fixtures in the checkout before treating the proposed stand-in as independent evidence.

#### 2. The carry

1. **Blocking — duplicate prompts remain an admitted outcome.** `docs/2026-10-24-switch-context-design.md:399` counts user messages after the anchor but explicitly leaves delivery unchanged, then `docs/2026-10-24-switch-context-design.md:452` sends the refused words again. Counting and logging duplication does not prevent it; unmatched suffixes need a defined safe disposition before prompting.

2. **Blocking — first-turn fallback can repeat completed tool work.** `docs/2026-10-24-switch-context-design.md:502` treats every `refusal` or prompt-level `Fatal` as carry failure and resends the original words, although `Fatal` includes an unknown failure (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Errors/AgentErrors.cs:17`). A turn can execute tools before a later model request fails; require positive evidence of rejection before execution, and retain ordinary uncertainty handling otherwise.

3. **Blocking — assistant IDs do not establish complete context.** The acceptance rule ignores user messages, thoughts and tool updates (`docs/2026-10-24-switch-context-design.md:390`), while existing files beneath `<id>/` are retained without comparing their contents (`docs/2026-10-24-switch-context-design.md:334`). Matching assistant IDs can therefore coexist with missing or conflicting tool state; specify the stronger invariant or narrow “read back” to what was actually verified.

4. **Blocking — the copy is not protected against another writer.** A byte-prefix observation followed by rename does not reserve the destination; another terminal may append between them, and existing sidecars can independently differ (`docs/2026-10-24-switch-context-design.md:328`). D137 rejects two clients writing one conversation (`docs/decisions/D137.md:56`); require enforceable exclusion or refuse replacement, rather than promising other-client turns are never overwritten.

5. **Blocking — recovery lacks a committed delivery state.** The carried ID and carry file are separate writes (`docs/2026-10-24-switch-context-design.md:376`), and non-box paths recover only when another word arrives (`docs/2026-10-24-switch-context-design.md:464`). Specify recovery for every write boundary and preserve the pending first instruction/refused words with their delivery disposition; otherwise a crash can bypass verification or strand an automatically requested turn.

6. **Non-blocking — the intended account boundary is sound.** Both-account eligibility and normal start selection exclude the person’s own sign-in (`docs/2026-10-24-switch-context-design.md:267`); D197 intentionally permits every Daoris account in every workspace (`docs/decisions/D197.md:21`). Clarify that home resolution uses `Toolchain.Owner(adapter)`, since ACP account ownership differs from its adapter name (`src/Daoris.Desktop/Daoris.Desktop.Driver/Accounts/Harnesses/Harnesses.cs:281`).

#### 3. The reversals

1. **Blocking — D125 and D189’s copying prohibition is not yet sufficiently answered.** The narrow allow-list and account exclusions are reasonable, but the replacement safety argument depends on the incomplete replay and concurrency proofs above (`docs/decisions/D125.md:1424`, `docs/decisions/D189.md:271`). I disagree with the earlier reader’s claim that byte-prefix equality establishes safe replacement (`docs/2026-10-24-switch-context-reads.md:417`): it establishes an observed relationship, not exclusive ownership.

2. **Blocking — D131, D137 and D167’s record reversal is only partly justified.** D167’s entry supports grouping records, so the linked-record shape has a sufficient reason; however, replacing the conversation after a turn that already emitted messages or tools contradicts the claimed “only one conversation answered” justification (`docs/2026-10-24-switch-context-design.md:517`, `docs/decisions/D137.md:762`). State how those events and usage remain attributable, or restrict same-record replacement to a verified pre-execution failure.

3. **Non-blocking — D165 and D189’s compaction reversal has a sufficient product reason.** Preserving exact context serves continuity, and the design acknowledges retaining H rather than claiming savings (`docs/2026-10-24-switch-context-design.md:652`). Keep the cost claim unmeasured until the real move’s evidence lands; successful replay alone does not establish retained working memory.

4. **Blocking — D197 and D206’s limit amendments inherit unresolved delivery semantics.** Their notes promise refused and waiting words “each once” and recovery without the brief (`docs/decisions/D197.md:382`, `docs/decisions/D206.md:323`). Those promises need the suffix and crash rules above, including consecutive limited carries before any successful answer.

#### 4. Other decided rules

1. **Blocking — earlier does not mean ended.** The proposed `live={false}` for every earlier part (`docs/2026-10-24-switch-context-design.md:533`) conflicts with D167’s explicit allowance for multiple live parts (`docs/decisions/D167.md:76`); `src/Daoris.Web/src/work/conversation/conversation.ts:894` then marks unfinished turns cut and tools stopped. Separate read-only controls from actual lifecycle, and distinguish continuation ancestry from alongside sibling work.

2. **Blocking — `handed` misses D182’s push chain.** The field chain and update endpoint omit `src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/SessionsStateSlice.cs:39`, whose allow-list controls what windows receive. Add the field, its update publication/wake and a test through the pushed projection; service read-back tests alone cannot prove the page changes.

3. **Non-blocking — the new endpoint needs explicit driver authority.** `/handed` is specified as loopback-only (`docs/2026-10-24-switch-context-design.md:422`), but `/actor` also has a `DoorClass.Driver` entry (`src/Daoris.Service/Daoris.Service.Http/PersonDoors.cs:256`). Name that census entry and its keyless-refusal test; an unclassified write defaults to the person’s door, not the driver’s.

4. **Non-blocking — translation requirements are incomplete.** `moved.carry-failed` interpolates raw `{{code}}` (`docs/2026-10-24-switch-context-design.md:569`), whereas `src/Daoris.Web/src/work/noteLines.ts:795` translates known reason families explicitly. Define that family in both languages and retain `{{why}}` in the complete reworded `limit.resume-refused` entry (`src/Daoris.Web/src/work/noteLines.ts:316`); key parity alone is insufficient.

#### 5. Rows and order

1. **Blocking — SWITCHCTX1c-i.** Keep it independent, but fix lifecycle and sibling handling before accepting it (`docs/2026-10-24-switch-context-design.md:736`). Also give the combined view one scroll owner: each existing `SessionConversation` invokes its own shared-region scrolling (`src/Daoris.Web/src/work/conversation/SessionConversation.tsx:191`); I disagree with the earlier review’s unconditional endorsement of this row.

2. **Blocking — SWITCHCTX1a.** Its stand-in must cover child replay, unmatched user/tool suffixes, post-tool failure, concurrent destination mutation and crashes between persistence steps (`docs/2026-10-24-switch-context-design.md:737`). These require settled contracts before a builder can choose correct assertions.

3. **Blocking — SWITCHCTX1b.** Require consecutive limited carries and restart recovery of an unsent live/driven first instruction, not merely recovery upon a later word (`docs/2026-10-24-switch-context-design.md:738`). It correctly follows 1a once 1a supplies one delivery/recovery protocol.

4. **Non-blocking — SWITCHCTX1c-ii.** Declare 1c-i as a prerequisite alongside 1a: this row modifies boundaries and folds belonging to that combined renderer (`docs/2026-10-24-switch-context-design.md:739`). Test pending, successful and failed carry states through real pushed `handed` values.

5. **Non-blocking — SWITCHCTX1d.** The Codex evidence prerequisite is appropriate, but also declare dependence on the corrected common carry machinery (`docs/2026-10-24-switch-context-design.md:740`). No readable anchor must continue to mean brief fallback without attempting a carried prompt.

6. **Blocking — SWITCHCTX1e.** Missing-ID failure is necessary but insufficient: the pipe has no replay verification, and its CLI version cannot use the ACP-version row (`docs/2026-10-24-switch-context-design.md:620`). Specify its equivalent context proof, actual version row and dependence on 1a before enabling it.

7. **Non-blocking — SWITCHCTX1f.** Measuring the next genuine move respects the stated constraint, but one successful move cannot settle return-copy, compaction, refusal and restart cases (`docs/2026-10-24-switch-context-design.md:742`). Record exactly which case ran and keep every other case explicitly unmeasured.

#### 6. A smaller shape

1. **Non-blocking — retain the two halves, reduce the carry protocol.** First ship the corrected line view; then support Claude ACP on the curve path using one durable carry record containing phase, source/destination, compatibility identity, anchor and pending delivery disposition (`docs/2026-10-24-switch-context-design.md:368`). Fall back before prompting whenever proof fails, preserve ordinary uncertainty handling after execution starts, and extend that same protocol to limits and other doors; another agent continues through today’s brief.

**Verdict: another round.**

## Opus (foundation), round 4

The independent Opus 5.5 reader's foundation read of round 4 at `4698728a5`: 11 findings, 4 blocking; accept after named changes.

### 0. Codex's round-3 findings

Paths without a root are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and page paths are under `src/Daoris.Web/src/`, as in the design. I read `4698728a5`. Its `src/` is the same as `972b17577`'s.

| # | Codex (blocking in bold) | Round 4's answer | Checked against the code | Standing |
|---|---|---|---|---|
| **1.1** | **Child replay is not filtered** | During the load, drop updates on the conversation's own id and on `<id>:replay-subagent:*`; count only the own id | `Replayed` matches the own id only (`Acp.cs:3431-3435`, applied at `:2489`), and a child falls into the parent's record (`Acp.cs:2519-2533`, `AcpStreams.cs:95-99`). The answer holds, but it depends on the package's shape for a child's id. Dropping every update while the load is answered is safer (finding 5) | Taken |
| **1.2** | **`LimitHeld` cannot recognise the exception** | Exception withdrawn; the lists must be equal | `LimitHeld.cs:17-56` reads coded cooling lines; `AccountLimits.cs:60-67` reads the door's failure alone | Taken |
| **1.3** | **The adapter's version does not identify the transcript's writer** | The identity is the adapter's version plus the CLI's | `HarnessSelection.cs:603-608` holds. Nothing says how a start reads the CLI's version (finding 7) | Taken, residue |
| 1.4 | Package evidence is not in the checkout | 1a checks in a fixture | It is a plan, not evidence. Errors in the reading fail safe (§2) | Taken |
| **2.1** | **Duplicate prompts** | U/R decided before any prompt | Holds for one carry. Two limited carries in a row can hand the same words twice (finding 3) | Open in part |
| **2.2** | **The fallback can repeat tool work** | Only `refusal` or a prompt `Fatal` with no `Tool` event falls back | Holds for tool calls (`Acp.cs:3100-3112`; a subagent's card, `AcpStreams.cs:169-181`). "Nothing ran" claims more than that (finding 6) | Taken |
| **2.3** | **Assistant ids do not establish context** | Messages, thoughts and tool calls by id; equal lists; claim narrowed | `Acp.cs:3091-3112` keeps each id | Taken |
| **2.4** | **The copy is not protected against another writer** | Create-only, never replace | A move refuses a name that already exists. The cost is real (finding 8) | Taken |
| **2.5** | **Recovery has no committed delivery state** | A carry record, its phases, a come-up table | The success path holds. The chat's open runs after the take has already spoken (finding 2), and the fallback's own boundaries are missing (finding 4) | Open in part |
| 2.6 | Name `Toolchain.Owner` | Named | `Harnesses.cs:281`; `CurveMove.cs:86` | Taken |
| **3.1** | **D125/D189's ban on copying** | Answered through 1.1, 2.3 and 2.4 | Holds | Taken |
| **3.2** | **The record reversal** | A further part; no record names a second conversation | Holds, except that a shell ended inside a fallback can rewrite `harness.json` (finding 4) | Taken, residue |
| 3.3 | Compaction's reason; keep the cost unmeasured | Kept unmeasured | — | Taken |
| **3.4** | **D197/D206 inherit the delivery rules** | U/R, and new notes | Finding 3 | Open |
| **4.1** | **Earlier does not mean ended** | Ancestry by `from`, each part's own liveness, a read-only mode | `conversation.ts:881-901`; `D167.md:73-82` | Taken |
| **4.2** | **`handed` misses the push chain** | Added to `Fields`, a declared change, slice tests | `SessionsStateSlice.cs:39-47`. Precedent at `StatePublisher.cs:258-264`. The seam between driver and modules is not named (finding 10) | Taken |
| 4.3 | Driver authority for the endpoint | Its door row | `PersonDoors.cs:256` | Taken |
| 4.4 | Translation | Family `carry`, `{{why}}` kept | `noteLines.ts:316`, `:795-806` | Taken |
| **5.1** | **SWITCHCTX1c-i** | Lifecycle, siblings, one scroll owner | `SessionConversation.tsx:161-200`; `followTail.ts:49-61` | Taken |
| **5.2** | **1a's stand-in** | Each case named | The main proof passes only on a fixture that holds `Ran.Entries` (finding 1). Cases for findings 2 and 4 are missing | Taken, residue |
| **5.3** | **SWITCHCTX1b** | Consecutive carries; delivery at come-up | The consecutive proof claims more than the rule gives (finding 3) | Open |
| 5.4 | 1c-ii's prerequisites | After 1c-i and 1a | Its codes belong to the driver (finding 10) | Taken |
| 5.5 | 1d's dependence | After 1a and 1b | — | Taken |
| **5.6** | **SWITCHCTX1e** | A read-back of its own and a row keyed by the CLI | — | Taken |
| 5.7 | 1f names which case ran | Named | — | Taken |
| 6.1 | A smaller shape | Adopted. The first-turn fallback and D206's box are kept apart, each with a reason | Both reasons hold | Taken |

### 1. Claims about the code

These claims are wrong:

- **§1.7, and both `model` rows of §2.1.** The design says "the part's last acknowledged model and effort are the last of `Ran.Entries` (`ActorRun.cs:119`, `:151-154`)".
  - Only a composer change writes an `ActorEntry` (`ChatRunner.cs:2106`).
  - A new part's watch starts with no prior: `ChatRunner.cs:1468` passes `goOn?.Record.Ran`.
  - A driven run is handed only its own record (`Loop/Driver.cs:3652-3665`), and no driven path writes an entry.
  - So a part whose model nobody changed holds no entry.
- **§2.1, "the session's acknowledged options (`LastAcknowledgedOptions`) name both".**
  - That property holds only the list a set returned, and is null where nothing was set (`Acp.cs:597`, `:604-605`).
  - `CarryAsync` sets nothing where the current value already equals what was asked (`Acp.cs:723`). That is exactly the case §1.7 expects after a load lands on the transcript's model.
- **§2.3 *Where it runs* ("in each, in this order … 6. the spawn; the agent's `session/load` … 7. … `verified`"), and §2.4 step 6 writing the lines after it.**
  - `ChatRunner.StartAsync` returns as soon as the process is spawned and its watch started (`ChatRunner.cs:1609-1630`).
  - The protocol open belongs to the watch (`CaptureProtocolAsync`, `:2645-2647`), and `chat.Opened` (`:2647`) releases the queued words.
  - By then `HandOverTake.RunAsync` has queued its message and written the old part's last line (`HandOver.cs:256-276`).
  - The live `LimitMove` has queued the refused words in the same way (`LimitMove.cs:141-172`).
- **§2.5, "on the same process … opens `session/new`".**
  - An `AcpSession` opens once: its handshake refuses a second open (`Acp.cs:1614`, `:1658`).
  - After a load answers, updates on any non-child id reach the session (`Acp.cs:2496-2533`): a `config_option_update` replaces the options and a usage frame is measured, whatever the id.
- **§2.5, the further part "by today's move".**
  - The hand-over's judge refuses the same agent on the same account (`HandOver.cs:162-166`).
  - `LimitHeld` reads only turns refused while the account cooled (`LimitHeld.cs:31-37`).
  - So nothing today opens a further part, or reads the words, of a first turn that ended `refusal`.
- **§2.4, "Consecutive limited carries … its own prompt is in the transcript, so nothing it was handed goes twice".** The rule does not give this (finding 3; `LimitHeld.cs:17-56`; `ChatRunner.cs:2149-2165`, `:2198-2204`).
- **§2.2, `projects/<Flatten(tree)>/`.** The code itself says Claude Code names a folder longer than 200 characters with a hash of its own, which `Flatten` does not reproduce (`Platform/ToolHomeLeftovers.cs:36`, `:49-50`).
- **§2.3, `KeepHandedAsync`.** The name is already used for KEEP1b's downloads (`Loop/Driver.cs:3015`, `Platform/DownloadKeeping.cs:33`, and the service's `OpinionStore.cs:130`).

These I checked and found as written:

- The curve and the take: `CurveMove.cs:36-37`, `:62-118`; `ChatHandOver.cs:16`, `:145-157`, `:224-230`; `HandOver.cs:135-174`, `:232-296`.
- The driven carry-on: `Loop/Driver.cs:2445-2462`, `:2544-2549`, `:2563`, `:2643`, `:2735`, `:4582-4626`.
- `LimitMove`: `:12-13`, `:51-189`, `:205-313`, `:352-379`, `:411-436`.
- The resume machinery: `Acp.cs:671-756`, `:804-853`, `:3355-3435`; `ChatRunner.cs:496-648`, `:2618-2667`.
- The record of a turn: `Acp.cs:3084-3129`.
- `Continuation.cs:170-239`, `:585-593`; `HarnessConversations.cs:8-56`; `SessionHomeFiles.cs:34-35`; `AgentErrors.cs:5-19`.
- The actor door: `Sessions.cs:1406-1423`, `Program.cs:1398-1409`, `PersonDoors.cs:256`, `ServiceClient.cs:2266`.
- The slice: `SessionsStateSlice.cs:39-47`, `:62-67`.
- The homes: `HarnessSettings.cs:878-882`, `DaorisHome.cs:373`, `HarnessSelection.cs:579-580`.
- The page: `conversation.ts:881-901`, `SessionConversation.tsx:161-200`, `WorkFrame.tsx:2471-2472`, `PartsStrip.tsx:90-113`, `AskConversation.tsx:870-876`, `locales/en/note.json:202-214`, `noteLines.ts:296-318`.
- No reader of a kept id looks a record up by its conversation (`ChatGoOn.cs:480`, `LimitWaits.cs:224`, `DriverContinuations.cs:43`, `:62`, `:208`, `:1220`, `HelpConversations.cs:296`, `ToolHomeLeftovers.cs:149`, `SessionGroups.cs:958`). Two records naming one id therefore confuse none of them.

### 2. The carry

**A carried context reaching an account or agent it must not.** None found.

- `own-home` checks both records. The homes are reached only through `ProfileHome(…, Toolchain.Owner(adapter), …)`.
- `agent` holds the adapter unchanged.
- A teammate's record never moves (`CurveMove.cs:73`, `LimitMove.cs:66`), and `handed` stays off the sync wire.
- The destination is always an account the start was already allowed to run (D197 point 1).
- The never-copied list is per agent, and a path through a link is refused.

**A turn lost, doubled or replayed.** Three holes, all in delivery rather than the copy:

1. **The brief goes into the carried conversation.** The take queues its brief before the carried open even runs (finding 2). As the code stands, the brief would be the carried conversation's first prompt. That gives the agent both the brief and its own transcript, and spends a turn on the new account that nobody asked for — exactly what §5 rejects.
2. **Words handed twice.** Two limited carries in a row, each finding none of the refused prompts in the transcript, hand the earlier refused words twice (finding 3).
3. **A delivery lost.** A shell that ends inside a fallback, after the carry record is removed but before the brief and words are kept, loses that delivery (finding 4).

The replay itself is never kept, which holds (`Acp.cs:2489`).

**A tool call run twice.**

- At the open, nothing is prompted, once finding 2's order is fixed.
- At the first turn, the test of whether the turn's record holds a `Tool` event is sound for tool calls: background work starts from a recorded call, and a subagent becomes a card.
- It does not show that nothing ran at all: the prompt's hooks run before the model does (finding 6).
- On the driven path, a refused first turn is also a failed run that the look will carry on. Nothing stops the design's further part and the look's own carry-on from both opening (finding 10).

**A file replaced.**

- Create-only moves hold: neither `File.Move` without overwrite nor `Directory.Move` writes over a name that exists.
- The names this carry wrote are recorded only at `copied`. A shell that ends mid-copy leaves this carry's own names, which the come-up then reads as `exists` (finding 7).

**A crash between the carry record's phases.**

- The success path's rows hold.
- The fallback has no row. It names its `session/new` in `harness.json` through `onConversation` (`ChatRunner.cs:2623`, `Acp.cs:1640`) while the record still says `copied`. A come-up at `copied` then reruns the carry and writes the carried id over the fallback's.
- The record is removed before the fallback's brief and words are handed by today's `say`, which is not durable (`HandOver.cs:271`, `LimitMove.cs:167`).
- At `opened` or `copied` on a curve move, the come-up spawns a record that has nothing to say. It could instead leave the record for its next word's carried way.

**A session read as finished when it is not.** None found.

- The line view takes each part's own liveness, which holds.
- A carry record whose new part went on elsewhere before its first answer stays pending forever and is never ended. In 1a this happens on today's limit move of a carried part (finding 10).

**The fallback's proof that nothing ran.**

- At the open, the proof holds by construction, provided `chat.Opened` comes only after `verified` or the fallback (finding 2).
- At the first turn, it proves that no tool call started, not that nothing ran (finding 6).

**The package's claims and the fixture plan.** Enough for what they are used for.

- Every misreading of the package fails safe: an id stamped differently gives `short-replay`, a tool result replayed as a user message gives `suffix`, and an unread CLI gives `version`.
- The one unsafe gap is between the adapter's replay (read by the SDK's `getSessionMessages`) and what the CLI actually loads into context. The design names it as unmeasured (§2.3, §3.1), and SWITCHCTX1f is where it is measured.
- The fixture is a transcription. It is enough to shape the stand-in, provided it also states how a start reads the CLI's version (finding 7) and the rule for naming the project folder, including its 200-character bound.

### 3. The reversals and the line view

- **D125, and D189 points 1 and 5 (copying between account homes).**
  - The reason is sufficient: copy by name only, after the writer has exited, never over a name that stands, never into or out of the person's own home, held to the pinned identity, and read back before any prompt.
  - What rested on the old rule is named: `HarnessSettings.cs:878`, `HarnessConversations.cs:8-10`, `LimitMove.cs:12-13`.
  - The deletion inventory's "never the harness's conversation" still holds, with one copy per home the line ran on.
- **D131 points 2, 4 and 5 and their rejections; D137 point 8 and its two rejections; D167 point 1.**
  - The reason is sufficient: D167's entry is the fold, and no reader keys a record by its conversation (§1).
  - D131's "one conversation would have two homes" is the copy itself, answered by point 6 standing.
- **D189 point 7 (compaction at a move).** Sufficient: no way on is claimed cheaper (session-context design `:288-289`).
- **Unnamed reversal.** D189 point 3's last sentence also changes: *a carry-on after a limit and a hand-over stay new parts, handed the brief*. So does its *Why*: *without reaching into an account's home* (finding 9).
- **SWITCHCTX1c-i** draws only the attended part's ancestry by `from`, each part with its own record's liveness, read-only by a mode of its own, and leaves alongside parts to the strip. One scroll owner holds as specified. It reverses nothing (D167 point 5 keeps *every act on the part shown*). Three things are left unsaid (finding 11):
  - An ended line opens at the region's top (`followTail.ts:56`), which now means the attended record's head followed by the oldest part.
  - Pressing an earlier part hides the parts after it.
  - Nothing bounds how many parts a long line loads.

### 4. The rows

- **SWITCHCTX1c-i** can be built alone, in the web lane only. Its rewordings keep every placeholder, so `NoteCodesTests` (`NoteCodesTests.cs:52-60`) still passes. Its proofs prove its claims; it should add the ended line's opening position.
- **SWITCHCTX1a** cannot prove its main claim as specified.
  - Its stand-in passes only where the old part holds `Ran.Entries`, which no real chat that kept its default model has (finding 1).
  - It has no proof that the take waits for the open's outcome (finding 2), and none for a shell ending inside a fallback (finding 4).
  - It cannot write the carried lines' codes without their en and zh entries, which belong to 1c-ii (`NoteCodesTests.cs:8-12`). It should say that it writes today's curve lines until then (finding 10).
- **SWITCHCTX1b** comes after 1a.
  - Its proof "two limited carries in a row: the second hands nothing the transcript holds" fails when neither carry found the refused prompts in the transcript (finding 3).
  - These are unnamed: the driven come-up, the driven go-on's carried way (`DriverContinuations.cs:61`, `:209`, `:1220`), the driven `SayMoved` (`Driver.cs:2549`) moving after the open, and when `.help.json`'s `Handed` is written (today `brief`, at `LimitMove.cs:424`, before the outcome is known).
- **SWITCHCTX1c-ii** spans two lanes: its codes are the driver's (`NoteCodes`, `HandOverNotes`), and its words are the web's.
- **SWITCHCTX1d, 1e and 1f** are gated correctly, and their proofs fit their claims.

### 5. Findings

1. **Blocking. The model condition cannot be met in the owner's case.** §1.7, §2.1, §2.5.
   - **What is wrong.** `Ran.Entries` is written only by a composer change (`ChatRunner.cs:2106`) and is never carried into a new part (`ChatRunner.cs:1468`; `Loop/Driver.cs:3652-3665`). So the first judgement refuses `model` on every curve move of a chat whose model nobody changed, on every driven carry, and on every second carry. Each is said as `moved.carry-failed`.
   - **Also wrong.** `LastAcknowledgedOptions` is null where `CarryAsync` set nothing (`Acp.cs:597`, `:604-605`, `:723`), which is the case expected after a load.
   - **Smallest change:**
     - Keep what ran as an `ActorEntry` from the session's current options after every protocol open's set (the carried part's included) as well as at each composer change.
     - Judge "what ran" on the session's current options after `CarryAsync`, and on `LastAcknowledgedOptions` only where a set returned one.
     - 1a proves a line whose old part never changed its model, and two carries in a row.
2. **Blocking. The carry's steps do not run inside the start.** §2.3, §2.4 steps 4-6.
   - **What is wrong.** The chat start returns before the protocol open (`ChatRunner.cs:1609-1630`, `:2645-2647`). The take and the limit move write their lines and queue the brief or the words before the outcome is known (`HandOver.cs:256-276`; `LimitMove.cs:141-172`).
   - **Smallest change:**
     - `ChatStart` carries the open's outcome: a task the watch completes at `verified` or at the fallback's `session/new`.
     - `HandOverTake`, `LimitMove` and `HelpStartAsync` await it before writing their lines and `session.moved`, and before saying anything.
     - `chat.Opened` comes only after it. The driven `SayMoved` moves after the run's open.
     - 1a proves that no brief is ever queued to a carried part.
3. **Blocking. Two limited carries in a row can hand the earlier refused words twice.** §2.4 step 5; D197's and D206's round-4 notes; 1b.
   - **What is wrong.** R and the words to hand are read across the parts by `LimitHeld.Of`. The carried part's own refused prompt is recorded as the person's words (`ChatRunner.cs:2149-2165`, `:2198-2204`), and it already held the earlier words wherever U was 0.
     - With U = 0 twice, those earlier words go once as their own turn's and again inside the carried part's.
     - With the mixed orders, the count fits neither case and falls back.
     - D206's box come-up composes the refused words whatever the disposition (`LimitMove.cs:296-313`, `:376-379`).
   - **Smallest change:**
     - Count R and U from the still-pending earlier carry record's disposition: only refused turns after that read-back, and only user messages after its last matched item.
     - Hand only the newest part's refused words, since an earlier carried prompt settled the ones before it.
     - The box's come-up applies the carry record's disposition, not only dropping the brief.
     - 1b proves the orders (0,0), (0,1), (R,0) and (R,1).
4. **Blocking. A fallback's own boundaries are not recoverable.** §2.4 *Recovery*, §2.5.
   - **What is wrong.**
     - The fallback's `session/new` is named in `harness.json` while the record still says `copied` (`ChatRunner.cs:2623`, `Acp.cs:1640`). The come-up then reruns the carry over it.
     - The carry record is removed before the brief and words are kept. Today's `say` is not durable (`HandOver.cs:271`, `LimitMove.cs:167`), so a crash in between loses the brief, and on a limit move the refused words. D197's note claims the opposite.
   - **Smallest change:**
     - Write a phase `falling-back`, with its code, before the fallback opens anything.
     - At the come-up, a record at `falling-back` finishes the fallback (the brief from the recipe and the words by their disposition, kept through the dispatcher) and never reruns the carry.
     - Remove the carry record only once that delivery is kept on the new record.
     - Leave a curve move found at `opened` or `copied` to its next word.
     - 1a proves a shell ended inside the fallback.
5. **Not blocking. The same-process fallback is a new door path, and the child filter rests on the package.** §2.3, §2.5.
   - **What is wrong.** Covered in §1 (`Acp.cs:1614`, `:1658`, `:2496-2533`, `:3431-3435`).
   - **Smallest change:**
     - Name the second `session/new` on one handshake, or respawn for the fallback.
     - After the fallback, drop every update not on its own id.
     - While the load is answered, drop every update whatever its id, and count only the own id's.
6. **Not blocking. The first-turn rule claims more than the record shows, and its further part has no opener.** §2.5.
   - **What is wrong.**
     - The absence of a `Tool` event shows that no tool call started, not that nothing ran: a prompt's hooks run before the model does.
     - `refusal` does not distinguish a provider refusing the carried history from one refusing the prompt.
     - The hand-over's judge refuses the same account (`HandOver.cs:162-166`), and `LimitHeld` does not read a turn ended `refusal` (`LimitHeld.cs:31-37`).
   - **Smallest change:**
     - Word the rule as "no tool call started".
     - Name the opener: `LostAsync`'s shape, why `retry`.
     - Name a reader for the words of a turn that ended `refusal`.
     - `conversation.carried` keeps the class of the provider's words for 1f.
7. **Not blocking. The table's identity and names are underspecified.** §2.1, §2.2.
   - **What is wrong.**
     - Nothing says how a start reads the CLI's version: the SDK's `package.json` under the pinned adapter, or the pinned `claude`.
     - The folder rule ignores the code's own 200-character and unproven-name bounds (`ToolHomeLeftovers.cs:36`, `:49-50`).
     - The names this carry writes are recorded only at `copied`.
   - **Smallest change:**
     - Name the CLI version's read.
     - Refuse a tree whose flattened name is unproven (`tree`), rather than meeting it as `not-found`.
     - Write each name to the carry record before its move.
8. **Not blocking. Create-only costs most where the owner works.** §2.2, §3.2.
   - **What is wrong.** With the curve's 30-minute hold and a few accounts, every return to an account goes by the brief.
   - **Smallest change:**
     - Say so in the owner's terms in §2.2 and §3.2, and have 1f count `exists`.
     - Leave a set-aside of the left copy (renamed, never deleted or written) to a later row, if the count shows it is worth it.
9. **Not blocking. A reversal is unnamed.**
   - **What is wrong.** The carry also changes D189 point 3's last sentence and D189's *Why* (*without reaching into an account's home*) (`D189.md:22-24`, `:45-46`).
   - **Smallest change.** Name both in §4 and in D189's note.
10. **Not blocking. The rows, the lanes and the names are incomplete.** §6, §2.3.
    - **What is wrong.**
      - The carried lines' codes need their catalogue entries before the driver can write them (`NoteCodesTests.cs:8-12`).
      - The seam from the driver's `handed` write to `StatePublisher.Publish` (`StatePublisher.cs:258-264`) is not named.
      - `KeepHandedAsync` is a name already in use.
      - A carry record whose new part went on elsewhere before its first answer has no end.
      - On the driven path, the further part and the look's own carry-on can both open.
    - **Smallest change:**
      - 1a writes today's curve lines until 1c-ii lands, and 1c-ii is a driver-and-web row.
      - Name the publish seam, and rename the update, for example to `KeepHowHandedAsync`.
      - Add a come-up row that ends a carry record whose part went on elsewhere.
      - 1b names the driven come-up and the driven go-on's carried way, opens one further part only, and writes `Handed` after the outcome.
11. **Not blocking. Three things in 1c-i are left unsaid.** §2.7.
    - **What is wrong.** Where an ended line opens (`followTail.ts:56`), that pressing an earlier part hides the later ones, and that a long line's parts are unbounded.
    - **Smallest change.** State the first two in the row's proofs, and bound the parts drawn, with a press for more.

11 findings, 4 blocking.

**Verdict: accept after named changes the steward can check without another read.** This is my read's verdict; by D191's note of 2026-10-24 the design still waits for Codex's read of round 4. Round 4's answers to Codex hold against the code where they reach it: the child filter, equal lists by id, create-only landing, the further part keeping each record to one conversation, and the line view's lifecycle and scroll. The reversals have sufficient reasons; the one unnamed amendment (finding 9) is a wording change. What blocks is in the plumbing. The model condition reads a field only the composer writes, so the carry would never run in the owner's own case (finding 1). The carry's steps are placed inside a start that returns before the protocol open, so as the code stands the brief would be queued into the carried conversation (finding 2). Two limited carries in a row can hand words twice (finding 3). The fallback, which will be the commonest outcome at first, is outside the recovery table (finding 4). Each change is local and stated against the lines that show it, and so are findings 5 to 11. Finding 3 is 1b's alone. SWITCHCTX1c-i is sound as round 4 writes it; finding 11 can be taken in the same pass.

## Codex, round 4

Codex's read of round 4 at `4698728a5`: 22 findings, 9 blocking; another round. It ran read-only on main from outside the install, its links made repository-relative.

Reviewed HEAD `4698728a5`. Nothing changed; I read neither `local/` nor files outside this checkout. The installed adapter bundle is unavailable within that boundary.

Below, **design** means `docs/2026-10-24-switch-context-design.md`. Driver source paths are relative to `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

**0. Round-3 findings**

“Taken” assesses the design contract, not an implementation that already exists. Severity below describes what remains blocking now.

| Round-3 finding | Disposition | Basis |
|---|---|---|
| 1.1 Child replay | Taken — **non-blocking** | `design:457` filters parent and replay-child updates. |
| 1.2 Limit sentence exception | Taken — **non-blocking** | `design:466` removes the unsupported exception. |
| 1.3 Transcript writer’s version | Taken — **non-blocking** | `design:312` requires adapter and CLI identities. |
| 1.4 Package evidence unavailable | Deferred — **non-blocking** | `design:361` requires checked-in evidence before building the carry. |
| 2.1 Duplicate prompts | Open — **blocking** | `design:544` substitutes counts for prompt identity; see 2.1 below. |
| 2.2 Post-execution fallback | Open — **blocking** | `design:608` trusts an event log that can lose tool events. |
| 2.3 Incomplete context proof | Taken — **non-blocking** | `design:476` expressly limits what verification proves. |
| 2.4 Concurrent replacement | Taken — **non-blocking** | `design:374` requires create-only moves. |
| 2.5 Recovery and delivery commitment | Open — **blocking** | `design:438`, `:567`, `:601` leave intermediate writes unresolved. |
| 2.6 Account-home ownership | Taken — **non-blocking** | `design:347` names `Toolchain.Owner`. |
| 3.1 Copying prohibition | Taken — **non-blocking** | `design:394` answers the reasons with a bounded exception. |
| 3.2 Record/conversation attribution | Taken — **non-blocking** | `design:622` retains refused events and usage on the carried record. |
| 3.3 Compaction and cost | Taken — **non-blocking** | `design:739` keeps retention and cost unmeasured. |
| 3.4 Limit amendments | Open — **blocking** | `D197.md:392`, `D206.md:331` still promise unsupported “each once.” |
| 4.1 Earlier versus ended | Taken — **non-blocking** | `design:640` separates ancestry, liveness and read-only controls. |
| 4.2 Pushed `handed` | Taken — **non-blocking** | `design:507` includes projection, wake and projection tests. |
| 4.3 Endpoint authority | Taken — **non-blocking** | `design:503` requires `DoorClass.Driver` and keyless refusal. |
| 4.4 Translation contract | Taken — **non-blocking** | `design:694`, `:718` specify the family and placeholder checks. |
| 5.1 Line-view row | Taken — **non-blocking** | `design:921` includes lifecycle, ancestry and scroll proofs. |
| 5.2 Carry stand-in | Open — **blocking** | `design:916` tests saved phases, not every intervening effect. |
| 5.3 Other moves and restart | Open — **blocking** | `design:923` names the cases, but their delivery assertions remain unsettled. |
| 5.4 Carried presentation prerequisites | Taken — **non-blocking** | `design:924` requires both preceding rows and pushed-state tests. |
| 5.5 Codex prerequisites | Taken — **non-blocking** | `design:925` requires the common protocol and readable evidence. |
| 5.6 Pipe verification | Taken — **non-blocking** | `design:769` keeps it disabled until all three prerequisites hold. |
| 5.7 Case-specific measurement | Taken — **non-blocking** | `design:754` names measured and unmeasured cases separately. |
| 6.1 Smaller shape | Taken — **non-blocking** | `design:1043` adopts the scope; the protocol defects below remain. |

None of the round-3 findings is shown wrong. The outstanding package evidence has a reasonable implementation-stage home, but remains independently unverified here.

**1. Claims about existing code and the pinned package**

1. **Blocking — the dispatcher does not guarantee the delivery durability claimed.** `design:535` and `:569` rely on keeping and attempting words before sending them. However, `Conversations/Chat/ChatTurns.cs:177` permits process-only delivery after a keep failure, and `Conversations/SaidDoor.cs:86` explicitly permits sending after an attempt-mark failure. The carry needs a checked persistence path, or must acknowledge the resulting loss/replay possibility.

2. **Non-blocking — the detailed package claims remain unverified.** `design:264` through `:291` cite installed package internals absent from this checkout; `Agents/Adapters/Adapters.cs:1895` documents a different package/SDK pair. Preserve the exact identity and auditable source-derived evidence in SWITCHCTX1a before enabling its row; the current repository alone cannot establish those claims.

**2. The carry**

1. **Blocking — `U = R` does not establish which prompts survived.** `design:544` treats equal counts as proof that every refused prompt is present, while `design:478` explicitly declines to verify prompt identity. One missing refused prompt plus one unrelated user message produces the same count, with unchanged agent/tool anchors; terminal turns outside the record are expressly possible (`docs/2026-10-03-session-messages-design.md:291`). Persist a correspondence to the actual composed prompts and their constituent words, and refuse ambiguous suffixes; also define that correspondence when several refused words become one subsequent prompt.

2. **Blocking — “no recorded tool” is not positive evidence that nothing ran.** The fallback at `design:614` can repeat tool work because `Sessions/Events/SessionEventLog.cs:84` swallows event-write failures and `Agents/Acp/Acp.cs:2554` likewise tolerates failed recording. Furthermore, `Agents/Errors/AgentErrors.cs:17` includes unread failures in `Fatal`, which proves no pre-execution rejection. Require execution evidence independent of successful event logging and an explicit pre-execution rejection; incomplete evidence must retain ordinary uncertainty handling.

3. **Blocking — opening the new record is not recoverably idempotent.** `design:438` opens the ledger record and only afterward saves its ID in the carry record, but `design:565` assumes `judged` means no new record exists. The store generates a fresh ID on every create (`src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:799`), and the open has no operation key (`SessionLedger.cs:652`). Define recovery of a committed open whose answer or subsequent carry write was lost, rather than repeating an unidentified open.

4. **Blocking — delivery needs a durable terminal disposition.** `design:567` distinguishes delivery not kept, kept and attempted, but does not define the committed handoff after a word leaves `said`. The existing door removes it once written (`Agents/Acp/Acp.cs:2076`; `Conversations/Chat/ChatTurns.cs:1121`), before an answer, so queue absence cannot distinguish “never kept” from “already sent.” Specify durable delivery identity and attempted/sent-or-uncertain state, including the D206 box’s ownership, before recovery may enqueue anything.

5. **Blocking — fallback discards its recovery recipe too early.** `design:601` removes the carry record before handing the brief and words, leaving a crash window with an empty fallback conversation and no pending recipe. “As today” does not close it: `Conversations/Chat/ChatHandOver.cs:248` uses process-only `Take`, and `Conversations/SaidDoor.cs:33` persists `Text`, not the message’s `Brief` metadata. Keep a journaled fallback target and recipe until its delivery reaches a durable disposition, including the further-part fallback at `design:622`.

6. **Non-blocking — the replacement problem is answered.** `design:374` requires absence checks followed by moves that refuse existing destinations, including a destination created between the check and move. Partial copies may remain after failure, as explicitly stated at `design:380`; no described copy path replaces an existing file.

**3. Reversals and their dependents**

1. **Non-blocking — D125 and D189 have a sufficient reason for the bounded copying exception.** `design:394` addresses account boundaries, changing formats and maker boundaries separately, while restoring left-copy fallback (`D189.md:291`). Existing home-ownership comments must change as listed at `design:825`; the delivery defects above still prevent accepting the complete mechanism.

2. **Non-blocking — D131, D137 and D167 have a sufficient record-shape reason.** The entry supplies the grouping, and the further-part fallback preserves conversation attribution (`design:782`, `:805`; `D137.md:772`). The newest-part restriction remains essential to resume selection and to aggregating anchors; it must not become a lookup by conversation ID alone.

3. **Non-blocking — D165 and D189’s compaction reversal is adequately bounded.** Continuity explains retaining the larger conversation, and `design:799` withdraws any cheaper-way claim. `D165.md:377` correctly limits installed evidence to the case that actually ran.

4. **Blocking — D197 and D206’s promises exceed the delivery proof.** `D197.md:392` and `D206.md:331` assert that replay counts prevent duplicate delivery, but findings 2.1 and 2.4 show why they do not. Amend those notes together with the corrected prompt correspondence and recovery protocol; retaining the built box is reasonable only with one explicit delivery owner.

**4. Other decided rules and line view**

1. **Non-blocking — the earlier D182, person-door and translation omissions are addressed.** `design:503` names driver authority, `:507` includes the pushed projection and wake, and `:718` requires bilingual reason and placeholder tests. These match `PersonDoors.cs:256`, `SessionsStateSlice.cs:39` and the existing reason-family reader in `src/Daoris.Web/src/work/noteLines.ts:795`.

2. **Non-blocking — SWITCHCTX1c-i now has a buildable contract.** `design:640` excludes alongside siblings, preserves each ancestor’s liveness and gives only the attended part automatic scroll ownership. Those changes answer the actual behavior at `src/Daoris.Web/src/work/conversation/conversation.ts:881` and `SessionConversation.tsx:161`; the row’s proofs at `design:921` cover the relevant regressions.

3. **Non-blocking — D191’s standing is now honest.** `design:13` and `:1054` require both round-4 reads and leave the carry proposed. The independent line-view row is clearly separated from accepting the reversals.

**5. Rows and order**

1. **Non-blocking — SWITCHCTX1c-i can remain first.** Its ancestry, lifecycle, scrolling and historical-wording assertions are concrete (`design:921`). It need not wait for the carry protocol.

2. **Blocking — SWITCHCTX1a still leaves recovery assertions to the builder.** `design:922` needs crash points between ledger creation and journal update, between fallback creation and delivery, and after delivery leaves `said`. Add failed event-write, failed keep and failed attempt-mark cases; saved-phase tests alone cannot prove the stated guarantees.

3. **Blocking — SWITCHCTX1b names scenarios without settling their word mapping.** `design:923` needs assertions for a missing prompt replaced by an unrelated one, batched refused words across consecutive limits, and restart ownership across the carry record and D206 box. It correctly follows 1a once those contracts are settled.

4. **Non-blocking — SWITCHCTX1c-ii has the right dependencies.** `design:924` tests absent, `own` and `brief` through pushed state. Its presentation work can follow the corrected 1a without waiting for every later door.

5. **Non-blocking — SWITCHCTX1d has an explicit evidence dependency.** `design:925` leaves Codex disabled until its file layout and replay anchor are read. A builder is waiting for evidence, not being asked to invent compatibility.

6. **Non-blocking — SWITCHCTX1e is correctly held.** `design:926` requires missing-ID evidence, its own versioned row and a read-back without prompting. If that read-back does not exist, the row remains unavailable and the brief remains the path.

7. **Non-blocking — SWITCHCTX1f measures an appropriately narrow claim.** `design:927` records the actual case and preserves every other case as unmeasured. Its evidence must follow the corrected delivery guarantees before the next real move can reasonably be described as risking “nothing lost” (`design:752`).

**Verdict: another round.**
