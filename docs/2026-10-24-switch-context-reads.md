# SWITCHCTX1: the reads of round 1 (D191)

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
