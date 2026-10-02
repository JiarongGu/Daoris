# Pausing and abandoning an ask (PAUSE1)

**Carried by:** PAUSE1. **Status:** design, written before the build; **D132** records what it settles. Nothing is
built. §0 is what is true today, read from the code at `32cbf03`; §1 onward is the design. Read with **D126**
(`docs/2026-10-02-session-management-design.md`) and its notes, **D65** with USE1c, **D79**, **D80**, **D88**, **D95**,
**D102**, **D104**, **D51**, **D46 §3–§4**, **D47 §4**, **D68**, **D69**, **D50**, **D110** and **D116**.

> *"so there is a pause and cleanup feature needed"* — the owner, 2026-10-02, about a request whose work had gone the
> wrong way.

The owner wanted to stop an ask's work and found no act for it. The parent did it by hand, in two steps that each
reached the wrong scope: `daoris driver hold <repository>`, which stops every new start in the whole repository,
other asks' work included; then *Stop…* on the running session, which holds that one quest (SESSUX1b). Nothing could
pause the ask itself, all its quests and sessions at once, and nothing removes what the work left behind: quests
open or taken, trees under the home, local `daoris/*` branches with commits nobody pushed, and sessions in the list.

This design gives an ask, and one quest, two acts. **Pause** stops the work on this machine and keeps everything, so
*Resume* carries it on where it stood. **Abandon** gives the work up for good. It lists every piece first, and the
second press takes only what nothing else holds. The dispatch and the owner called the second act *clean up*. §8.2
says why the window calls it *Abandon*: *clean up* already names the opposite removal.

## 0. What is true today

### 0.1 What a person can do to an ask's work

| Act | What it reaches | What it leaves |
|---|---|---|
| `daoris driver hold <repository>` (`DriverConfig.Holds`, `StartVerdict.Held`) | every new start in that repository, whatever asked for it | what runs keeps running; other repositories the ask reached keep starting |
| *Stop…* on a session (D126 §3.3, SESSUX1b; `StartVerdict.Stopped`, `released`) | that session, and its quest held on this machine until *Try again* | the ask's other quests and sessions; a quest its stop did not reach |
| *Close ask* (`AskDesk.CloseAsync`) | the ask's record: `Closed`, with the reason; a later publish onto it is refused (`PublishAsync`) | **every quest it became, open or taken, still planned and still worked**: the close writes only the ask's row |
| *Decline…* on a quest's page (`QuestExchange.RespondAsync`) | one quest, open or taken (`QuestTransitions.Allows`), committed here and carried by the next sync pass | its sessions, which nothing stops (the driver stops only a session whose take lost, D68 rule 2); its trees and branches |
| *Delete…* (D95) | a quest nobody started on, or an ask with every quest it became | anything a session record names: refused |
| *Discard tree* (`SessionTrees.RemoveAsync(force: true)`) | one session's tree and its branch, work included | everything else |
| The clean-up (D88, D102; `daoris-driver trees clean`) | every session branch whose work a branch of the person's holds, with its tree | **exactly the work an abandoned ask leaves**: unlanded commits, uncommitted changes |
| *Archive*, *Archive what ended…* (D126 §5) | sessions in *Ended*, one machine's list | a session in *Waiting on you* or *To review* |

### 0.2 What the code already knows about an ask's work

- **A quest asked by an ask** has `From` = `ask #<id>` (`AskDesk.SenderOf`), chain steps included, since every step is
  asked on the chain's asker's behalf (D65 §4 as built).
- **A question a session asked another repository** (D79) has `PublishedBy` = that session (SESS1), and the asking
  quest names it in `Awaits`.
- **A session** names its quest, its `Tree`, its `BaseCommit`, whether it `Took` its quest (STANDDOWN2), its `Origin`
  when it is a teammate's (SYNC4), and, for an intake, its `Ask`.
- **A session's branch** is `daoris/<the tree's folder name>` (`SessionTrees`, `s-` and eight hex characters).
- **A landing** names the session whose work it took, in `<home>/landings.json`, standing or as a trace (D102, D113).

So an ask's whole work can be read from records that exist. Nothing reads it as one thing yet.

## 1. The work of an ask, and of a quest

**The work of an ask** is:
1. every quest asked by it (`ask #<id>`), chain steps included;
2. every quest published by a session that worked on a quest of the work, which is how a question asked of another
   repository (D79) joins it, applied again to what that adds;
3. every session whose quest is in the work, and the ask's intake;
4. each such session's tree and branch on this machine, and any landing that names one.

**The work of a quest** is the same with that one quest in place of step 1, and no intake. A chain's next step is not
in one quest's work: it is a quest of its own, asked by the ask.

**One reader decides it**: `AskWork.Read`, in the driver library, over the service's quests and records, the trees
and `landings.json`. For each piece it answers what a pause would do to it and what an abandon would do to it, with
the reason for whatever it would keep (§2.1, §3.2). `WORK_PLAN` hands it to the page and `daoris-driver` prints it, so
the screen and the terminal cannot disagree, as `SessionGroups.Read` is one reader for both (D126 §2.4).

**Why questions asked of other repositories belong to the work.** A question exists because a session of this work
asked it. If the work stops, the question's answer is wanted by nobody. If the work is abandoned, leaving the question
open means another repository starts a session to answer it, and that is the wasted work the owner wanted to stop.
**Why the work is read again at every look, never kept as a list**: a chain step published by a close, or a question
asked a second before the pause, joins the work as it appears, so nothing slips past a pause made a moment earlier.

## 2. Pause and resume

### 2.1 What a pause does, piece by piece

| Piece | Pause | Why |
|---|---|---|
| A live session of the work, this machine's (`queued`, `starting`, `working`) | **stopped**, as the person's stop (not interrupted, D104), through today's routes; the pause records it as its own | it is the work in flight |
| A live session another Daoris process on this home runs (`STOP_SESSION`'s *elsewhere*) | stopped through SESSUX1g's request folder, as `daoris-driver sessions stop` stops it | one home, one answer |
| A session waiting on you (`awaiting-person`) | **left parked**; an answer to it is kept, and whatever the answer starts (a carry-on, or the conversation going on once ANSWER1 lands, D131) waits for *Resume* | nothing runs, and stopping it would end the question it asked |
| A running intake | **left running**; every quest it publishes joins the work and is paused with it | an ask has one intake (D65, INT4b), so a stopped intake could not come back on *Resume* |
| An ask whose intake has not started | its intake does not start | nothing of a paused ask starts |
| An open quest | **paused**: the driver here starts nothing for it | a pause holds this machine only (§5.1) |
| A taken quest | **paused**: no carry-on, resume, or start an answer would make; it stays taken here | the take is the lock (D68), and it stays this machine's |
| A teammate's session (`Origin` set) | untouched, named | its process is on their machine (D47 §4) |
| A closed quest, a tree, a branch, a record, an archive mark | untouched | a pause changes no work |

**A pause takes nothing that *Resume* cannot give back.** That one rule decides the two exceptions: a parked
session stays parked, and a running intake goes on.

### 2.2 What it keeps: the plan's place

- **An open quest keeps its place.** The planner starts the oldest open quest first (D46 §3), ordered by when it was
  published, and a pause moves nothing in that order. On *Resume* it starts when it would have.
- **A taken quest keeps its take, its tree and branch, its `awaits`, and its strikes and mark.** The pause's stop is
  no strike (D58). On *Resume* it is carried on in its tree as a released stop: the planner's carry-on (D80) and the
  ledger's (SESSUX1b2), whose rule that the take is this machine's holds, since the pause stopped this machine's
  session.
- **A chain keeps the steps it has not published yet.** A step published while paused is asked by the ask, so it is
  in the work and paused too.
- **A waiting quest (D79) keeps waiting.** Its question is in the work and paused with it here. Once the question is
  answered, the resume waits for *Resume*.
- **The ask keeps its state**, and its page says *paused* beside it.

### 2.3 The planner's verdict: `Paused`

The planner gains `StartVerdict.Paused` for every quest the look finds in a paused ask's or a paused quest's work. It is
**the reason the quest sits, before every other**, a person's stop included. While it holds, a release of a stop would
not start anything, and *Resume* is the one press that moves the quest. Its sentence names the pause and its door:
- *paused with ask `#a`; Resume carries it on — `daoris-driver ask --resume a`* (a taken quest),
  or *… Resume starts it* (an open one);
- *you paused `#q`; Resume carries it on — `daoris-driver quest resume q`*.

It sits as any held quest sits: under *Sitting* on its page, on the tick's line, in the room. `Consideration.PausedBy`
carries the scope and the id, as `HeldBy` carries a stop, so 中文 says it from facts and never translates the
driver's English (`work.sitting.Paused`, as SESSUX1d built `work.sitting.Stopped`).

**The look computes the paused set; the planner reads it.** At each look the loop reads `driver.json`'s pauses,
asks `AskWork` for each one's quests, and hands the planner the set, as it hands `LastRun`. The planner never holds a
second copy of §1's rule.

**This amends D126 §3.3 and SESSUX1b's note** (*a stop is the reason its quest sits, before every other*): a pause now
comes first, and the stop second.

### 2.4 Resume

*Resume* removes the pause and **releases every stop the pause made**: for each quest it stopped, `released` names the
session it stopped (SESSUX1b's field), so the `Stopped` verdict does not hold it once `Paused` is gone. Then the next
look plans the work as it would have: a taken quest carried on in its tree, an open one in its place.

What still holds after *Resume* is said, once, in the answer, never released by it:
- **a stop the person made before the pause**: *`#q` stays held by your stop of session `s`; Try again carries it on*;
- **the strikes' park** (`Exhausted`): *Try again* is still its press;
- **a repository's hold**, an account's cool-off (D125), a quest's own pause inside a resumed ask's work, or the ask's
  pause around a resumed quest. Each is named by what holds it.

### 2.5 Where a pause is kept

In `driver.json`, beside `holds` and `released`, and machine-local like everything in that file:

```json
"pausedAsks":   { "a1b2c3": { "at": "2026-10-02T14:02:11Z", "stopped": { "q7f3e1": "s-1a2b3c4d" } } },
"pausedQuests": { "q9d0aa": { "at": "2026-10-02T14:05:40Z", "stopped": {} } }
```

`stopped` maps each quest the pause stopped to the session it stopped. *Resume* writes those into `released` and
removes the entry. Absent is none, and the fields are written only when set.

**A twin file.** `DriverConfig.cs` writes both fields. `driverconfig.ts` reads them, since `daoris driver list` shows
each pause, and leaves them as they are when it writes the file, as it leaves every field it does not edit. Each side
reads them with its own code and one table (`.claude/knowledge/twins.md` gains the row). The pause and resume
themselves are `daoris-driver`'s, which reads the work and reaches the running loop, as §7.2 says; `daoris driver`
talks to nothing (D50).

### 2.6 It asks once when it ends work in flight

A pause that stops a session asks once, under the page header, as *Stop…* does (D126 §3.3). The ask says what follows:
*Stops 2 running sessions now and starts nothing of ask `#a` on this machine until you resume it. Their trees keep
what they wrote, and the quests they took stay taken here.* Where they apply, it adds two sentences:
- *Another machine may still take `#q2`: a pause holds this machine only.*
- *Its intake keeps reading; what it publishes waits too.*

The moves are *Pause ask* (danger) and *Never mind*. **A pause that stops nothing applies at once**, with a toast,
since nothing is lost. *Resume* never asks: it starts nothing itself, and the next look plans what it released.

## 3. Abandon

### 3.1 Listed first, then pressed, with the person's reason

As *Archive what ended…* (D126 §5.3) and the clean-up (D88) work:
1. **The first press lists** under the page header, from `WORK_PLAN`, everything the abandon would take and everything
   it would keep, each kept piece with its reason. It asks for the reason, which every declined quest keeps (§4.1).
   The moves are *Abandon ask* (danger), offered once a reason is typed, and *Never mind*.
2. **The second press sends exactly the pieces the first listed**, held from when the list opened, since the reader
   answers again on every look. Each piece is judged again as it goes. One that changed since the list is kept and
   counted: *Abandoned 9 of 10 pieces; 1 changed since the list and was kept.*

It is offered while the reader finds anything it would take: a quest it would decline, a tree or branch it would
discard, or a session it would archive. A first press that finds nothing left says so and offers only *Close*, never a
press that abandons nothing.

### 3.2 Piece by piece

| Piece | Abandon takes it | Abandon keeps it, and says |
|---|---|---|
| A live session, this machine's | stopped, as the person's stop | — |
| A session waiting on you | ended `stopped` through `RESOLVE_SESSION`, unanswered | — |
| A live intake | stopped; the ask then closes, so nothing it would publish lands (`PublishAsync` refuses a closed ask) | — |
| An open quest | **declined with the reason**; on a wired workspace the decline applies only while it is open (§5.2) | — |
| A quest a session of this machine's took | its sessions stopped, then **declined with the reason** | — |
| A quest taken on another machine | — | *taken on `<machine>`: its work is theirs; decline it on its page if you mean to stop it there* |
| A quest taken here outside Daoris (this machine's take, and no session record that took it) | — | *taken here outside Daoris: its work is wherever its taker works, not in a tree Daoris made* |
| A quest done or declined | — | *done: finished work keeps its record*, or nothing, since it is closed |
| A session's tree, and its branch | **discarded with its branch** when nothing it holds is anywhere else (§3.3): its uncommitted changes and its commits go | *its commits are on `<branch>`*: landed, pushed, or on a branch of yours, named; or *a landing took its work*; or *git could not say*. The tree stays for its review, the clean-up and *Discard tree* |
| A `daoris/*` branch whose tree is gone | deleted when nothing it holds is anywhere else | as for a tree |
| A branch a landing made (D102) | — | never abandon's: landed work is the clean-up's and the hand-off's |
| A session record | **archived** here, last, once nothing above keeps it needing the person | *still to review*: a tree kept with work keeps its session in *To review* (D126 §5.2) |
| The ask (ask scope) | **closed with the reason** | — |

**What the abandon takes is only what Daoris made for this work on this machine.** A quest whose work is somewhere
Daoris did not put it, on another machine, in the person's own checkout, or on a branch of theirs, is named and left.
The person can still decline it on its page: that is an answer about the quest, given on purpose, one at a time.

The first press names, for each tree it would discard, its repository, its branch, how many commits it holds, and how
many files carry uncommitted changes, naming up to five of them relative to the repository. **It never prints a path
on this machine** (the platform language's §4).

### 3.3 The proof for a tree and a branch: nothing it holds is anywhere else

D88's proof clears a branch whose every commit a branch of the person's holds, so that removing it loses nothing.
Abandon's proof is the inverse: **it discards a tree and branch only when none of their work has left Daoris's own
branches on this machine**, so what it throws away is exactly the abandoned work, and nothing anyone else may have.

- **The commits judged** are those after the session's `BaseCommit`, up to the branch's tip. Where a record has no base,
  the merge-base with the repository's line (D86) stands in.
- **Anywhere else** is any ref but this machine's local `refs/heads/daoris/*`:
  - the line, local and remote;
  - any other local branch, a branch a landing made among them;
  - **any remote-tracking branch, a pushed `daoris/*` included**;
  - a tag.

  D88's proof excludes `*/daoris/*` remote-tracking branches (`UnlandedLogAsync`), rightly, since a pushed session
  branch has not landed. For a discard the question is different: pushed means somebody may have it, so it is kept.
- **A session a landing names**, standing or a trace (D102, D113), keeps its tree and branch whatever git says. A
  squash leaves the commits on no other ref while their content is on the line, and landed work is the clean-up's to
  judge, never abandon's.
- **Uncommitted changes are discarded**, since they live only in Daoris's tree. The first press names how many, and
  which.
- **A count git cannot give keeps the tree**, as the clean-up keeps what it cannot judge (D126, SESSUX1a's note).
- **Partly elsewhere is kept whole.** A branch with two commits on `main` and three only here is kept by abandon,
  since some of its work left, and by the clean-up, since some did not. Both name it, and its review's *Discard tree*
  is the person's door for it.

The removal itself is `SessionTrees.RemoveAsync(force: true)`, the person *saying it again, meaning it*, reached only
behind this proof. **This amends D51 rule 7 as D88 amended it**: a removal happens by a person's press behind a proof,
and abandon's second press with its stated reason is that press. The new proof is `SessionTrees.OnlyHereAsync`.

**What git keeps afterwards is said.** A deleted branch's commits stay in the repository until git collects them, so
the answer, and the record (§4.2), give each branch's tip. The answer says how to bring a branch back meanwhile:
*`git branch daoris/s-1a2b3c4d 9f3e2a1`, in `<repository>`.*

### 3.4 The order of the second press

1. **Pause the scope first**, writing its entry, so nothing of the work starts between the steps that follow.
2. **Stop** every live session of the work here; **end** every parked one `stopped`.
3. **Decline** each listed quest with the reason, the open ones with `whileOpen` (§5.2). **Close the ask** with the
   reason (ask scope).
4. **Sync**: one pass per wired workspace, before the answer, as D95's delete runs one. Each shared decline is then
   *confirmed*, *lost* (§5.2) or *unconfirmed*, and the answer says which.
5. **Discard** each listed tree and branch behind §3.3's proof, judged again now.
6. **Archive** each listed session the reader now places in *Ended*.
7. **Tidy `driver.json`**: drop `released` and `forgiven` for the quests now closed, since a closed quest is never
   planned (D46 §3), and then the pause entry. Every quest the abandon kept open or taken is taken elsewhere or
   outside Daoris, which this machine's driver starts nothing for.
8. **Write the record** (§4.2) and the log line (§4.3).

A step that fails keeps what is left and says so, as the clean-up keeps what it cannot clear. **A failed step leaves
the scope paused**, so a half-finished abandon never leaves work starting again, and the answer names *Abandon…* again
or *Resume* as the two ways on.

### 3.5 What abandon never touches

A branch of the person's, the line, a remote branch, a tag, a branch a landing made, or any checkout. The intake's
room (`<home>/intake/<workspace>/`), which every ask in the workspace shares. A quest's or an ask's kept files, which
stay with their records. A session record, which is archived and never deleted (D126 §5.4). The usage counts and the
machine log. A teammate's record, beyond this machine's archive mark.

## 4. What the record keeps

### 4.1 On the quests and the ask

- **Each declined quest's note is the person's reason, verbatim** (a quest operation's `Note`). It travels with the
  decline on a wired workspace (D68), with the machine that made it, so whoever reads the quest reads why. Daoris adds no
  words to it: a sentence prefixed by the tool would be English in a record that is content, and nothing is decided
  from a note (D48 §6).
- **The ask's close note is the same reason** (`Ask.Note`), shown under *Closed because* as today. Asks never leave
  the machine (D65, INT4a).
- **The sessions' records are unchanged** but for their ends: `stopped`, the person's, for those the pause or the
  abandon stopped, with a note a person reads (*paused with ask `#a`*, *ask `#a` abandoned*). They travel as records
  do (SYNC4). Nothing reads the note to decide anything (D104).

### 4.2 This machine's record of what went

`<home>/abandoned.json` holds one entry per abandon, written atomically by the driver library (`AbandonRecord`) at both
doors, as `landings.json` is kept (D102):
- **the scope and its id**, when, by which door, and the reason;
- **what went**: each quest declined; each tree discarded, as its session, repository, branch, tip, commit count and
  uncommitted count; each branch deleted alone; each session archived;
- **what stayed**: each kept piece and its reason, in the words of §3.2;
- **each shared decline's answer**: confirmed, lost or unconfirmed.

It holds no path on this machine and no conversation's words. **A missing or unreadable file is no record, and the
abandon still abandons.** An entry stays for as long as its ask or quest has a record here: it is the only trace of a
discarded tree, and it is small. The ask's page and the quest's page show it as *What went* and *What stayed*.

**Why under the home and not on the record.** The trees, the branches and the tips are this machine's facts, and a
quest's record travels (D47 §4). An ask's record does not travel, but one home for both scopes keeps one reader.

### 4.3 The machine log (D94)

Three events, names and counts only (D94 §5):
- `work.paused`: `scope`, `stopped`, `door`;
- `work.resumed`: `scope`, `released`, `door`;
- `work.abandoned`: `scope`, `declined`, `discarded`, `branches`, `archived`, `kept`, `lost`, `door`.

A pause leaves nothing in `driver.json` once resumed, and a pause changes no work, so the log and the stopped records
are its whole trace.

## 5. Another machine, and the remote

### 5.1 A pause is this machine's

A pause is the driver's, in `driver.json`, and **nothing of it travels**, as a repository's hold and a stop's hold do not
(D126 §3.3: *another machine may still take an open one*).
- **A quest taken here** stays taken by this machine on the remote, so the take keeps every other machine off it: a
  pause of a taken quest holds everywhere, because the take does.
- **An open shared quest** can still be taken by a teammate's driver while it is paused here. The pause's ask names
  each such quest (§2.6), and *Abandon* is the act that reaches every machine, since a decline travels.
- **A teammate's running session** of the work is not reached. Processes never leave their machine, and a
  cross-machine stop request is held open (D47 §4). The pause names it.

**Why not a pause that travels.** It would be a new operation every machine's planner obeys: a second lock beside the
take, which D68 keeps as the one lock. And an open quest belongs to whoever takes it. A decline is the travelling
answer, and abandon gives it.

### 5.2 A decline made of an open quest applies only while it is open

A decline is allowed from open or from taken (`QuestTransitions.Allows`). So an abandon's decline of an open shared
quest could reach the remote after a teammate's take did, and then decline their started work under them. That
breaks D68's rule 2, *first push wins*, in spirit: the abandon was judged on an open quest. D95 met the same race for
a delete, whose condition is *nobody has taken it*, and let the rebase drop it.

So **a decline the abandon makes of an open quest carries `whileOpen`**. `QuestLog.Applies` applies such a decline only
to an open quest, as it applies a delete. Pushed after another machine's take, it does not apply: the rebase turns it
into a **conflict on the quest** (D68 rule 2), shown for a person with what it attempted and its reason, and the
quest stays taken by the machine that took it first. The abandon's pass (§3.4 step 4) finds this before it answers:
*`#q2` was taken on `<machine>` before your decline reached the remote; it stays theirs, and the conflict is on the
quest.* Offline, the decline is unconfirmed and travels on the next pass, where the same rule applies.

- **A decline of a quest this machine took** carries no flag: it is this machine's work to decline.
- **The flag is part of the operation and the wire** (`QuestWire`, the store's column). It is written only when set.
- **An older reader ignores it**, and replays the decline as a plain one: a remote built before this applies it over
  a take. Named in §13.
- **Only the abandon sets it.** The quest page's *Decline…* stays the plain answer, since a person declining a quest
  they see taken means to.

This amends the sync design's §5: beside a losing take, the moves made on a claim that lost (D69) and D95's delete,
which the rebase drops, a `whileOpen` decline after another machine's take is a conflict.

### 5.3 What a teammate sees

| Of a pause | Of an abandon |
|---|---|
| Nothing of the pause. The session records it stopped arrive `stopped`, as any record does | Each declined quest, with the person's reason and this machine's name; a lost `whileOpen` decline as a conflict on the quest; the stopped records |
| An open quest of the work stays open to their driver | Never the trees, the branches, the archive marks, the abandon record or the ask, all this machine's |

### 5.4 The shared deployment

A remote has no pause or abandon route, as it has no delete route (D95): trees, processes and `driver.json` are a
machine's, and a person abandons on their own machine. The declines arrive at the remote by the sync, as operations.
The remote judges `whileOpen` with the one judgement class (D47 §5, `QuestLog`), so a remote's build decides whether
the flag is honoured there.

## 6. How it meets what is there

### 6.1 D126's session acts

- **Stop.** The pause and the abandon stop sessions through the same routes *Stop…* calls: `STOP_SESSION`, with its
  three answers, and for a parked session `RESOLVE_SESSION`'s `stopped`. Each is recorded as the person's stop. A
  stop the pause made is the pause's to release (§2.4). A stop the person made is theirs, before or during a pause.
- **Try again.** It is not offered on a paused quest. *Resume* is, as the primary act on its session's header and on
  its page (§7.1). After *Resume*, *Try again* is offered again wherever a stop or the strikes still hold.
  `RETRY_QUEST` on a paused quest refuses with `QUEST_PAUSED`, naming the pause and its *Resume*.
- **Archive.** The abandon archives through `SessionArchive` with its refusals, so it never hides what needs the
  person. A session whose tree it kept with work stays in *To review*.
- **The list.** A session the pause stopped is grouped as a stop's is: *To review* where its tree holds work, which
  the pause's whole point is to let the person look at, else *Ended*. Its line says *paused with ask `#a`; Resume
  carries it on*. `SessionGroups` gains `PausedBy` beside `HoldsQuest`. **Why To review**: nothing will write into
  that tree until *Resume*, as for a stop's hold (SESSUX1b), and the person paused it to look.

### 6.2 SESSUX1b's held stop

A pause is the same kind of hold, wider. It uses the person's stop to end what runs, `released` to give it back, and
the ledger's carry-on of a released stop (SESSUX1b2) to carry a taken quest on. Its own verdict comes first (§2.3).
CARRY1, open, is the ledger's check of whose take a cut-off carries on. It does not touch a pause: the pause's stops
are of sessions that took here, or that had not taken at all.

### 6.3 D88's proof and the clean-up

Two removals with inverse proofs, by two presses:
- **The clean-up** removes what is proven elsewhere and loses nothing.
- **Abandon** removes what is proven nowhere else and loses exactly the abandoned work.

What one keeps the other may take, and a branch partly elsewhere is kept by both (§3.3). Neither ever removes a
branch of the person's.

### 6.4 Landing (D51, D87, D102)

- **A landing is a press on the review**, not a session, so a pause does not stop one.
- **A paused session's tree is in *To review*** with its review, land and discard as today.
- **Abandon never judges landed work** (§3.3). Once it discards a tree, the session has no review, since nothing is
  left to read, and D126's rule offers *Review* only where there is.
- **A chain's next step that grew from a step's branch** (D82) still holds those commits on its own branch, so
  discarding the earlier branch loses nothing it builds on.

### 6.5 Close, Delete and Decline

- **Close** stays the person's word that an ask is answered, and it still leaves its quests as they are. Its ask now
  says so: *Closing leaves its quests as they are; Abandon declines them.*
- **Delete** (D95) stays the act for a mistake: an ask or quest nobody started on. Abandon declines rather than deletes
  even a quest nobody started, since the reason is what the record should keep (§11).
- **Decline…** on a quest's page stays the plain answer about one quest.

## 7. The doors

### 7.1 The screen

Each act is offered where it applies and absent where it does not, never disabled (D119 §3.2).

| Where | Pause | Resume | Abandon |
|---|---|---|---|
| **The ask's page** (Quests), its header | *Pause…*, while any quest of its work is open or taken, or its intake has not run, and it is not paused | *Resume*, primary, while paused | *Abandon…*, quiet, while §3.1 offers it, its second press the danger one; beside *Close ask* and *Delete…* |
| **A quest's page**, its header | *Pause…*, while open or taken and not paused on its own | — | *Abandon…*, beside *Decline…* |
| **A quest's page**, under *Sitting* | — | *Resume*, for its own pause, where *Try again* stands for `Exhausted` and `Stopped`; for its ask's pause, the sentence and a door to the ask, where that pause is resumed | — |
| **A session's header and row ⋯** (D126 §3.1, `work/acts.ts`, `work/sessionActs.ts`) | *Pause quest…* for a live driven session; *Pause ask…* where its quest is an ask's | *Resume ask* or *Resume quest*, primary, where `PausedBy` holds its quest, in *Try again*'s place | — |

- **The ask's page also shows its work**: its quests section lists each quest of the work with its state and sitting
  reason, then under it the questions its sessions asked, and the sessions of each, as doors into Sessions.
  `WORK_PLAN` answers it.
- **After an abandon**, the page shows *Abandoned*, with when, and *What went* and *What stayed* from §4.2.
- **Abandon is on the ask's and the quest's pages only.** It is decided where the ask or the quest is decided
  (D126 §3.6), after looking. Pause is also on the session, because that is where a person meets work going wrong,
  as *Try again* is on the session that holds its quest (D126 §3.4).
- **A browser** has no driver, so the pages offer none of the three. A sentence there names the terminal's commands.
- **Below 560 px** the header's acts take their own line, as D126 §3.2 says.

### 7.2 The terminal (D50)

`daoris-driver`, beside `ask --publish`, `ask --close`, `ask --delete` and `quest delete`:

```
ask --pause <id>  ·  ask --resume <id>
    stop what of an ask's work runs on this machine and start nothing of it, or carry it on.
ask --abandon <id> [--reason "…" --yes]
    list what abandoning the ask would decline, discard and archive, and what it keeps and why;
    with --reason and --yes, abandon what the list holds.
quest pause <id>  ·  quest resume <id>  ·  quest abandon <id> [--reason "…" --yes]
    the same for one quest and the questions its sessions asked.
```

- **Without `--yes`, `--abandon` is the first press**: it prints the list and changes nothing. With `--yes`, it judges
  every piece again and abandons what may go, as `trees clean --yes` does. `--yes` without `--reason` is refused.
- **A session another process runs** is stopped through SESSUX1g's request folder, which every loop on the home
  watches. Where nothing on this machine runs it, it is ended as the screen ends an orphan (`Orphans.EndAsync`).
- **Exit codes** are the family's: 0 done or listed, 1 refused, 2 could not.
- **Why `daoris-driver`'s and not `daoris driver`'s**: the verbs read the service's quests and records and reach the
  running loop. `daoris driver` edits `driver.json` and talks to nothing (D50). It lists the pauses (§2.5).

### 7.3 The routes

Every route is `DAORIS.DRIVER`'s, in a new `DriverModule.Work.cs`, each with MOD5's three things: a `[DriverRoute]`
handler, its row in the Desktop README, and a call from a new `bridge/work.ts`.

| Route | Payload | Answers |
|---|---|---|
| `WORK_PLAN` | `{ ask }` or `{ quest }` | every piece of the work, what a pause and an abandon would do with each and why, and whether it is paused or abandoned (§1, §4.2) |
| `WORK_PAUSE` | `{ ask }` or `{ quest }` | what it stopped and what it could not reach |
| `WORK_RESUME` | `{ ask }` or `{ quest }` | what it released, and what still holds each quest (§2.4) |
| `WORK_ABANDON` | `{ ask }` or `{ quest }`, `reason`, `pieces` | what went, what stayed, how many changed since the list, and each shared decline's answer |

The refusals, each a code in `Refusals`, an entry in both catalogues and a throw site:

| Code | Says |
|---|---|
| `WORK_UNKNOWN` | *no ask `#a` on this machine* · *no quest `#q` here* |
| `WORK_REASON` | *abandoning needs your reason: each declined quest keeps it* |
| `QUEST_PAUSED` | *`#q` is paused with ask `#a`; Resume carries it on* (from `RETRY_QUEST`) |

A resume of what is not paused, and an abandon with nothing left to take, are information-class answers, never
refusals (D48 §6).

### 7.4 Ask Daoris (D110)

- **A `pause` kind**: `pause_propose`, its doors `pause` and `resume`, naming an ask or a quest, exactly one. It is judged
  against the facts, which gain this machine's paused asks and quests and, for each ask, its work's live sessions. A
  card says §2.6's sentence and its terminal line, and nothing applies until Apply (D89). *Stop everything for that
  ticket* is a thing a person asks, and Apply is still their press.
- **Abandon is exempt**: it declines quests with the person's reason, which is their answer (D37), as D126 §7.3
  exempted *Decline…*. Ask Daoris names the ask's page and the terminal line, and never writes the reason.
- **The room**: its doors table gains *pause or resume an ask or a quest* and *abandon an ask or a quest*, the second
  marked exempt with its reason. *The machine now* lists what is paused.
- **`HelpCoverageTests`** reads Sessions' acts (D126 §7.3), so *Pause ask…*, *Pause quest…* and *Resume* are each
  answered for by the `pause` kind's doors.

## 8. Both languages

### 8.1 The names

Every name follows D116: its kind decides its form and budget, and a concept takes the glossary's term. A count is
English characters and Chinese units (D116 §4). A button that names its object says it in the glossary's word, as
SESSUX1d settled for 会话.

| Key | Kind | English | 中文 | Measured / budget |
|---|---|---|---|---|
| `asks.record.pause` | button | Pause… | 暂缓… | 6, 3 / 20, 8 |
| `asks.record.pauseMeanIt` | button | Pause ask | 确认暂缓需求 | 9, 6 / 20, 8 |
| `asks.record.resume` | button | Resume | 恢复 | 6, 2 / 20, 8 |
| `asks.record.abandon` | button | Abandon… | 放弃… | 8, 3 / 20, 8 |
| `asks.record.abandonMeanIt` | button | Abandon ask | 确认放弃需求 | 11, 6 / 20, 8 |
| `asks.record.abandonWhy` | placeholder | why — kept with each decline | 原因——随每条谢绝留存 | 28, 11 / 40, 16 |
| `asks.record.paused` | status | paused | 已暂缓 | 6, 3 / 16, 5 |
| `asks.record.abandonedAt` | field | Abandoned | 放弃时间 | 9, 4 / 36, 14 |
| `asks.record.went` | section | What went | 放弃的部分 | 9, 5 / 32, 12 |
| `asks.record.stayed` | section | What stayed | 保留的部分 | 11, 5 / 32, 12 |
| `quests.detail.pause` | button | Pause… | 暂缓… | 6, 3 / 20, 8 |
| `quests.detail.pauseMeanIt` | button | Pause quest | 确认暂缓委托 | 11, 6 / 20, 8 |
| `quests.detail.resume` | button | Resume | 恢复 | 6, 2 / 20, 8 |
| `quests.detail.abandon` | button | Abandon… | 放弃… | 8, 3 / 20, 8 |
| `quests.detail.abandonMeanIt` | button | Abandon quest | 确认放弃委托 | 13, 6 / 20, 8 |
| `quests.detail.abandonWhy` | placeholder | why — kept with each decline | 原因——随每条谢绝留存 | 28, 11 / 40, 16 |
| `work.act.pauseAsk` | menu | Pause ask… | 暂缓需求… | 10, 5 / 24, 10 |
| `work.act.pauseQuest` | menu | Pause quest… | 暂缓委托… | 12, 5 / 24, 10 |
| `work.act.resumeAsk` | button | Resume ask | 恢复需求 | 10, 4 / 20, 8 |
| `work.act.resumeQuest` | button | Resume quest | 恢复委托 | 12, 4 / 20, 8 |
| `common.cancel` *(exists)* | button | Never mind | 取消 | — |

**The sentences** are the build's to write in both languages:
- the pause's ask (§2.6), the resume's answer (§2.4), the abandon's list and its kept reasons (§3.2);
- the toasts, the refusals (§7.3), the line *paused with ask `#a`*, and `work.sitting.Paused` from its facts;
- the close's new sentence (§6.5), and the terminal's lines.

A toast keeps to two lines (110 and 55). The driver's sentences and the reason are content, never translated
(`translation-parity`). **A pause's sentences say *paused*, never *held***: the `hold` term's `match` reads *is held*,
and would ask for 暂停.

### 8.2 Two new terms, and why each is not an existing one

- **`pause`**: en *pause*, zh **暂缓**. Its inverse is *resume*, 恢复, as for a hold. *Stop an ask's or a quest's
  work on this machine for now: what of it runs is stopped, nothing of it starts, and everything stays as it was
  until you resume it.*
  - **Why not *hold*.** A repository's hold stops nothing that runs (D46 §3), and a pause stops what runs. Under one
    name, nobody could tell from the button whether it ends work in flight.
  - **Why not 暂停.** It is the glossary's word for *hold*, and *park*'s entry already avoids it for that reason.
    暂缓 is what a team says of an ask set aside for now (需求暂缓).
  - Avoid: en *suspend*; zh 暂停 (hold's), 挂起 (park's), 搁置 (sitting's) and 中止, which is an end, not a pause.
- **`abandon`**: en *abandon*, zh **放弃**. *Give up an ask's or a quest's work on this machine for good: its quests
  declined with your reason, what only Daoris holds of it discarded, its sessions archived; what reached any other
  branch is kept.*
  - **Why not *clean up*, the owner's and the dispatch's word.** The glossary's *clean up* (清理) is D88's removal of
    what landed. It keeps exactly the work this throws away, and it is the safe press in Settings. One name for both
    would make a destructive press read as a safe one.
  - **Why not *discard*.** *Discard* (丢弃) throws away one session's tree; abandon also declines, closes and archives,
    and keeps what discard would not ask about.
  - Avoid: en *clean up*, *cancel*; zh 清理 (clean up's), 删除 (a delete removes a record, and this keeps every one)
    and 取消, the back-out's word.

**The glossary's doors**: none new. *Resume* under *Sitting* is the act itself, not a door.

## 9. The build

Rows ready for `TASKS.md`. D132 decides all of it, so no row takes a decision number unless building it finds
something D132 did not decide. **Every row's proof also has**:
- a failing test first;
- the fast half of each .NET suite it touches;
- `npm run verify`, with parity and `names:check` clean for each new key;
- the old words searched in what a worktree cannot run, for a row that changes words a test reads;
- the parent's look after the merge, for a row that changes the window: both themes, both languages, at 1280, 888
  and 680 px.

| Row | What and why | Contract | Proof |
|---|---|---|---|
| **PAUSE1a** | **The work, and the pause's file** (driver; cli). `AskWork.Read`, and `pausedAsks` and `pausedQuests` in `driver.json` with both twins, shown by `daoris driver list`. So one reader answers what an ask's work is before anything acts on it | §1, §2.5 | `AskWorkTests` (chain steps, questions, the intake, a teammate's record); the twin tables, both sides |
| **PAUSE1b** | **Pause and resume** (driver; modules; after a and SESSUX1g). The `Paused` verdict first, the look's paused set, `WORK_PLAN`, `WORK_PAUSE` and `WORK_RESUME`, the terminal and the log. It ends what the parent did by hand | §2, §4.3, §6.1, §7.2, §7.3 | `PlannerTests`; `DriverModuleWorkTests`; `SessionGroupsTests`; the golden. By the parent, a `Process`-half tick: paused, nothing spawned, resumed in its tree |
| **PAUSE1c** | **A decline that applies only while open** (service; any time). `whileOpen` on the operation, its store, wire and replay. So an abandon's decline never lands on a take that reached the remote first | §5.2 | `QuestLogTests`; the sync suite over the real wire: take first, decline first, an older record |
| **PAUSE1d** | **Abandon** (driver; modules; after b and c). The list, `WORK_ABANDON` in §3.4's order behind `OnlyHereAsync`, `abandoned.json`, the terminal's `--yes` and the log. So giving work up is one listed press that throws away only what nobody else holds | §3, §4, §7.2, §7.3 | `AbandonTests` (each piece and keep, a change between presses); `SessionTreeOnlyHereTests` (`Process` half, real git); a family rehearsal phase, by the parent |
| **PAUSE1e** | **On the screen** (web-shell; after b and d). The ask's and the quest's pages and a session's acts: *Pause…*, *Resume*, *Abandon…* with its list and reason, *What went*, the line and the names. So each act is where its ask, quest or session is | §6, §7.1, §8 | Stories; vitest over a mocked bridge; `names:check`; the look |
| **PAUSE1f** | **Ask Daoris reaches pause** (driver; service; web-shell; after b and SESSUX1h). The `pause` kind, its facts and the room, abandon exempt. So a person can ask for a pause, and Apply stays theirs | §7.4 | `HelpPauseProposalsTests`; `HelpCoverageTests`; the room's golden files; `ProposalCard.test.tsx` |
| **PAUSE1g** | **Looked at on the install** (the parent's, after a republish carrying a–f). An ask paused mid-session and resumed in its tree; one abandoned with a landed session kept | §12 | A ledger, as SESS1's, at every width in both themes and languages |

**Order.** PAUSE1a, then PAUSE1b and PAUSE1d in the driver lane. PAUSE1c in the service lane any time before PAUSE1d.
PAUSE1e after PAUSE1b and PAUSE1d. PAUSE1f after PAUSE1b and SESSUX1h. PAUSE1g last. **PAUSE1b alone gives the owner
the act they lacked**: one press that stops an ask's work and keeps its place.

A row that adds a refusal also adds its entry to both catalogues, in the web-shell lane, as SESSUX1a did. PAUSE1b and
PAUSE1d add their log events to the machine log design's §4.

## 10. What does not change

- **D46 §3–§4**: the quest has four states and a session's record is concluded from its exit and its quest. A pause
  is a verdict and a file, never a state; abandon declines, and adds none.
- **D58**: a stop is no strike, the pause's included.
- **D68's one lock**: the take. A pause adds no lock, and the abandon's decline loses to a take that came first.
- **D95**: a delete stays the act for a mistake, and a record work stands on is never deleted.
- **D126**: Stop, Try again and Archive keep their meaning. Archive never hides what needs the person.
- **D47 §4**: processes never leave their machine; the pages and the sentences never print a path on it.
- **D32**: nothing here writes into a repository's files beyond Daoris's own trees and `daoris/*` branches.
- **D41's palette** and **D116's names**.

## 11. Not chosen

- **Naming the second act *Clean up*.** §8.2: the glossary's *clean up* keeps exactly what this throws away.
- **A pause that travels.** §5.1: a second lock beside the take.
- **Stopping a running intake on pause.** It could not come back on *Resume* (one intake per ask).
- **Stopping a parked session on pause.** Nothing runs, and its question would be lost.
- **A pause as a repository hold narrowed by a filter.** A hold leaves running sessions running. A filter on `holds`
  would be a second meaning for a field two twins read.
- **Abandon deleting the quests nobody started**, as D95 allows. A delete says the ask was a mistake. Abandon says the
  work was given up, and the reason is what the record should keep.
- **Abandon discarding a tree whose work is partly elsewhere**, keeping only the landed commits. It would judge which
  half of a branch the person meant to keep. Both removals keep it, and its review decides.
- **Abandon reaching a branch a landing made, or a landed session's tree.** Landed work is the clean-up's and the
  hand-off's (D102).
- **Abandon declining a quest taken on another machine or outside Daoris.** Its work is not Daoris's here.
  *Decline…* on its page is one deliberate answer.
- **Prefixing each decline's note with the abandon's words.** It would put English into content, and the record of
  what went is the abandon record.
- **Ask Daoris abandoning.** The reason is the person's answer (D37).
- **Abandoning several asks at once.** Each reason is an answer about one ask; *Archive what ended* covers the bulk of
  what is left in the list.
- **An undo for an abandon.** A decline does not reopen (D46 §3), and the branch tips in the record are how a discarded
  branch comes back, by hand, while git keeps its commits.
- **A new ask state, *paused* or *abandoned*.** A pause is this machine's, and an abandoned ask is closed with its
  reason.

## 12. What only the window and a real run can prove

- **The widths**: the ask's and the quest's headers with *Pause…*, *Abandon…*, *Close ask* and *Delete…* at 680 and
  560 px, and the list's kept reasons at 888 px, in both languages. The budgets are estimates until the window
  measures them (D116 §4).
- **A pause of a real driven session**, nothing spawned while paused, and *Resume* carrying it on in its tree.
- **An abandon on the owner's install**: a real tree with uncommitted work discarded, a landed session kept, and the
  answer's branch tips good for `git branch` afterwards.
- **A `whileOpen` decline losing to a teammate's take** over a real remote, and the conflict on the quest.
- **How long `WORK_PLAN` takes** for an ask whose work reaches several repositories with their trees.
- **Whether 暂缓 and 放弃 read as the owner means them.**

## 13. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the code at `32cbf03`**: the service's `Asks.cs` (`CloseAsync`, `PublishAsync`, `Standing`),
  `Quests.cs` (`Parent`, `Awaits`, `PublishedBy`), `QuestLog.cs` (`QuestTransitions`, `Applies`),
  `QuestExchange.RespondAsync`, and `Sessions.cs` (`Tree`, `BaseCommit`, `Took`, `Origin`, `Ask`, `Interrupted`); the
  driver's `Planner.cs` (`StartVerdict`, the stop's place in the loop, `Consideration.HeldBy`), `DriverConfig.cs`
  (`Holds`, `Forgiven`, `Released`), `SessionTrees.cs` (`RemoveAsync`, `UnlandedLogAsync`, the branch's name),
  `SessionGroups.cs` (`HoldsQuest`), `Driver.cs` (`StopLostClaimsAsync`), `Driver.Intake.cs`, the module's session
  and driver routes, and the Ask Daoris kinds; the CLI's `driverconfig.ts`, which keeps what it does not edit; the
  web's `AskPage.tsx`, `QuestPage.tsx` and `ViewMain.tsx`; both catalogues and the glossary.
- **The owner's case** is as the dispatch records it; no record or transcript was read here.
- **Assumed, not read**: that SESSUX1g's request folder will be built as D126 §7.1 says, and PAUSE1b waits on it.
  D131 (ANSWER1) and D133 (DRIFT1) were in flight and not read; §2.1 holds whatever an answer starts, in either
  shape.
- **An older remote applies a `whileOpen` decline as a plain one** (§5.2). The two versions of one workspace's
  machines and remote are not held to agree by anything here.
- **Found while reading, and filed rather than decided here**:
  - **D124's set-up plan *Pause*** stops nothing that runs, only what it would publish next. By this glossary that
    is a hold, so WSSETUP7 names it *Hold* (暂停), or makes it stop the set-ups that run and keeps *Pause* (暂缓).
  - **The code calls a repository's hold *paused*** (`DriverConfig.Holds`, `StartVerdict.Held`). Once *pause* names
    this act, those comments mislead, so PAUSE1b rewords them.
  - **Closing an ask leaves its quests running**, which nothing on its page says. PAUSE1e adds the sentence (§6.5).
- **Not measured**: every item of §12.
- **`verify` checks** this document's links, the decision log's shape, the router's row, the budgets and the
  duplicates, and none of these words.
