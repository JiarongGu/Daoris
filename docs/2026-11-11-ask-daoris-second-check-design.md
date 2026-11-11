# Ask Daoris's second check: its acts read by a second model (ASKSUPERVISE1)

**Status:** proposed design, for an independent read before it is built; D272 is reserved for the decision written after the reads.
**Areas:** roles-and-supervision (rules 41 to 62), conversations-and-context (rule 42). Written from main at `f95f95e821`. Paths are
under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/` (`src/Daoris.Desktop/Daoris.Desktop.Modules/`) or
`Web/` (`src/Daoris.Web/src/`), as the roles area writes them, and line numbers are main's at that commit. Nothing was built or run.

## 0. In short

- **What is read** (§2): every turn of Ask Daoris's that did something no card or hold put to the person. In full access that is any
  turn with a proved press or a tool call of an acting kind (edit, delete, move, execute, or a call of a server other than Daoris's
  own); in restricted mode, a turn whose press ran with no card under a granted family. The turn is the unit, read whole: the person's
  words, the presses, the calls and what Ask Daoris told the person. Daoris classes calls by the harness's own tool kinds and never
  parses a command; the reader, a model, reads the commands.
- **When** (§3): after, at the turn's end. Nothing waits on a reading, in either mode. One reading runs per conversation at a time,
  and turns that end meanwhile are read together next.
- **What the reader may do** (§4): note, or flag. A note is kept and shown under the turn; a flag is also raised in *Notifications*
  and told to the conversation. It never holds and never decides: Ask Daoris's presses are the person's, and a go-ahead is the
  person's alone. Nothing it does is taken back, so *Overrule* has nothing to act on; a flag offers the act's reverse press where
  its route has one. Restricted mode's cards are unchanged and are not read.
- **Which model** (§5): another maker's agent by default. A seeded machine supervisor, *Second check*, deciding a new kind
  `ask-daoris`, is held by `agent`, with Codex first and Claude Code · opus · high second. Its walk puts every choice outside the family
  that ran the turn first (D155 point 4, `AgentFamily.Same`), and takes the same family only as *the same agent, fresh*. A reading is
  bounded to ten minutes and by the role's budgets. A turn no reader reads says *Not read* and why. Nothing is blocked.
- **Shown** (§6): a line under the turn it read; the reading's state beside each press in *Notifications*' pressed list; unseen flags
  in *Notifications*; the panel's access chip naming the reader; a flag told to Ask Daoris once, ahead of the person's next words, as
  the reader's claim; the machine log.
- **Reuse** (§7): the reader is an agent-held supervisor of a new kind, the one supervisor framework: its role door, resolution,
  grants, attempt walk, read-only turn, prompt, block, attempts and decision files, and the decision door for a terminal holder. What
  is new is the kind's row, the turn reader, the family order for this kind, the display and the told line. It does not use the
  window, *Overrule*, the allow machinery or the second opinion's gate.
- **Rows** (§8): a, the kind and the seeded reader; b, the turns read; c, the reading; d, what the window shows; e, what the conversation
  hears; f, the install's proof.

## 1. What is asked, and what is true today

**The rules this design serves.** A driven session's work passes its place's supervisor, which checks as well as decides. Ask
Daoris has no such reader, yet it is the assistant for the whole system, and in full access it presses as the person in every
workspace. A second model reading its acts is the check it lacks. Visibility is that check's purpose. A security improvement never
blocks full access or the development it serves. A person who wants more control chooses restricted mode.

**Today.**

| Part | Today | Where |
|---|---|---|
| A proved press, full access | Covered: run at once, no card, as the person's own; logged `help.pressed`, said once on the conversation's console, kept in this run's memory with its place and outcome | `Modules/Platform/PipeDelegate.cs:104-112`, `:116-138`; `Modules/Platform/PipeAdmission.cs:188-189`; `Help/HelpPressed.cs`; `Modules/Platform/HelpPressRecord.cs:14-27` |
| A proved press, restricted | Asked on the person's card, unless a granted family (`pause` alone today) covers it, which runs as full access's does | `PipeDelegate.cs:107-108`; `Knowledge/ActFamilies.cs:16-19` |
| A working session's asked press | Offered first to the allow supervisor at its repository (`SessionPressAllows`), falling to the card on its leave or after ten minutes. Ask Daoris's asked press goes straight to the card | `Modules/Platform/PipeAdmission.cs:222-224`; `Modules/Platform/SessionPressAllows.cs:22-43` |
| Tool calls, full access | The harness's unrestricted mode: no call reaches Daoris before it runs. Each is kept in the conversation's events log with its ACP kind (read, edit, delete, move, search, execute, think, fetch, other), status and bounded content. A shell command is never parsed (conversations area rule 42) | `Context/Events/SessionEventLog.cs`; `Events/Domain/SessionEvents.cs:335-336` |
| Tool calls, restricted | Inside its allow-list they run; outside it they are held for the person (agents area rule 23) | `Events/HeldPermissions.cs` |
| What the person sees | *Notifications*' pressed list, each press's place and outcome, and *Resume* for a pause (`helpReverse`); the turn's folded beyond receipt. Nothing says whether an act was what the person asked for | `Web/notifications/HelpPressLine.tsx`; `Web/person/standing.ts:55-57`; `Web/help/focus/BeyondReceipt.tsx` |
| The supervisor | Kinds `question`, `course`, `limit`, `allow`, `files`, `landing`, each a row of the one decision door. An agent holder's attempt walks the role's order, runs one read-only turn (ten minutes, no place under the cap, an empty folder for a subject with no tree), reads the one fenced block and keeps it through the kind's row | `Roles/SupervisorKinds.cs:22-33`; `Modules/Bridge/SupervisorKinds/ISupervisorKind.cs:10-27`; `Loop/Supervise/SupervisePass.cs:22`, `:27-64`; `Loop/Supervise/SuperviseTurn.cs`; roles area rule 42 |
| The second opinion | Another maker's reading of a session's candidate commits, its findings claims that go to the working session once and to the person; another maker first, the same agent only as *the same agent, fresh* | D155; `Participants/Domain/ReviewerChoice.cs:17-38` |
| Daoris's word to a conversation | `ChatWords.Tell` makes a turn of Daoris's own words. A focus change is told instead as one line ahead of the person's next words (`HelpFocusTurn.Ahead`) | `Loop/Chat/ChatWords.cs:60-66`; `Help/ChatHelp.cs:167-171` |

So every act Ask Daoris makes is recorded, but nothing reads it. The person sees what ran; nothing tells them whether it should have.

## 2. Which acts a second model reads

| | Option | Weighed against the code |
|---|---|---|
| A | Every press | One hook already exists (`PipeDelegate.Pressed`, the record's id). It misses what full access does through the shell, which is where outward and cross-repository acts happen: a `git push`, a publish, an edit in another repository's checkout. A press is already judged by its route and logged; a shell act is neither |
| B | A family: destructive, cross-workspace, outward | Needs a new mark on every person's route, beside the census class, the drain class and the grant families (`PipeCensus.cs:110-116`, `ActFamilies`), and it still misses the shell, where pushes and publishes live. *Beyond the focus* is no signal of a wrong act: the focus limits nothing (D253 point 3) |
| C | What the room marks consequential | The checked agent would choose what is checked; the census refuses a session reporting on itself for the same reason |
| D | Its tool calls too, read by turn | Covers presses and the shell alike. The turn holds the person's words that justify each act and the words Ask Daoris answered with, so the reader can say whether the account matches the acts. Daoris selects turns by the harness's own tool kinds and parses nothing |

**Recommended: D, by turn, limited to what ran with no yes.** A turn is read where it did something no card or hold put to the person:

- **Full access:** any proved press (`HelpPressRecord` names the conversation; a press is itself an `execute` call), or any tool call of
  kind `edit`, `delete`, `move` or `execute`, or `other` from a server other than Daoris's own connector. A call Daoris cannot place is
  counted as acting, so an unsure call is read. Turns of reads, searches, fetches and Daoris's connector alone are not read.
- **Restricted:** a turn with a press that ran with no card under a granted family. Its asked presses had the person's yes on the card,
  its held calls the person's answer, and its allow-listed calls run under the person's own rules, so none of them is read.

The terminal's tier `act` (D271) and a working session's own unrestricted press (D253 point 6) are not Ask Daoris and are not read
here (§9).

## 3. When it reads

| | Option | Weighed |
|---|---|---|
| A | Before each press: a hold until the reader answers | Blocks full access, against the owner's rule; a covered press would need the reservation path (`PipeReserved`, `SessionPressAllows`) as a second way to run it; and shell calls never reach Daoris before they run |
| B | After, at the turn's end | Blocks nothing; reads the turn whole, its last words included |
| C | Before in restricted mode, after in full access | Restricted mode already holds every asked press on the person's card, so a reading there would only annotate the card, not hold it. That is a second subject (one press, mid-turn) with a second display, for a yes the person gives anyway |
| D | After each press, mid-turn | Races the running turn and loses its last words; a flag would come at most one turn earlier |

**Recommended: B.** The reader starts once the turn has ended, as the console reads that end. One reading runs per conversation at a time.
Turns that end while it runs wait and are read together next, so a burst of turns costs one reading. Nothing waits on a reading, so
there is nothing to bound by a deadline except the reading itself (§5). C's reading of a card is left out of these rows (§9).

## 4. What the reader may do

| | Option | Weighed |
|---|---|---|
| A | Note | Kept and shown under the turn. Always useful, never in the way |
| B | Flag to the person | A note the person should see before going on: raised in *Notifications* and told to the conversation |
| C | Hold for the person | Nothing waits after the act (§3); a hold before it blocks |
| D | Decide as the allow supervisor does under its grants | Ask Daoris's presses are the person's (D253 point 6). A supervisor's yes to them in restricted mode would remove the control the person chose that mode for, and full access leaves nothing to allow. A go-ahead stays the person's alone and is no kind (D233's second note) |

**Recommended: A and B, as the kind's two acts.** `ask-daoris.note` lets a reader keep what it read. `ask-daoris.flag` lets it raise one
finding to the person and the conversation. The person grants each in the role editor, as every supervisor act is granted. A reader
granted `note` alone is a quiet one, its flags kept as notes. A leave (*I could not judge this*) needs no grant, as every leave does,
and is shown as such. The reader flags only where the person would want to know before going on. That means an act the person's words
did not ask for; an outward or destructive act they did not name (a push, a publish, a message sent, a remote changed, a deletion
not recreated); a write into a repository other than the one asked for; anything done around the floor or a deny rule (a force, a
skipped hook); or words to the person that do not match the acts. Anything else is a note.

**The person's controls.** *Overrule* takes back a held decision (rule 50); a reading holds nothing, so nothing is overruled. A flag
offers *Open the conversation*, the act's reverse press where its route has one (`helpReverse`: *Resume* for a pause), and *Seen*. The
reverse is the person's own press. Restricted mode's cards are unchanged: the person's yes is the check there, and a granted family's
card-less press is read after it runs, as full access's is.

## 5. Which model reads, its bound, and no reader

| | Option | Weighed |
|---|---|---|
| A | Another maker's agent first (D155 point 4) | An independent reader finds what the author's model shares; independence is by maker, never by model (`AgentFamily.Same`) |
| B | The place's role staffing | A turn may touch several workspaces; Ask Daoris's reach is the machine's, so a workspace's reader would answer a subject it holds only a part of |
| C | Ask Daoris's own staffing | The same family reading itself. Kept only as D155's last resort: *the same agent, fresh*, where the person listed it and no other can run |

**Recommended: A, staffed as a supervisor.** The reader is a supervisor deciding the new kind `ask-daoris`, placed at the machine alone.
The judge refuses `decides: ask-daoris` below the machine (`machine-only`), since every subject is the machine's. Resolution is
unchanged (rule 41, kind first): the machine's supervisor deciding the kind, else the machine's generic one, whose seed is held by
rules and so reads nothing. The seed gains *Second check* (`second-check`): machine scope, `decides: [ask-daoris]`, holder `agent`,
the order Codex at its account default, then Claude Code · `opus` · high, both acts granted, window 5 as the seed's supervisor has.
For this kind the walk takes the choices outside every family that ran the turns read first, in the order's sequence. It takes a choice
of the same family only where the person's order lists it and no other is ready, said *the same agent, fresh*. With Ask Daoris on
Codex, Claude reads first. The account is the one the walk chooses for any start (rules 16 to 20). The reading keeps its holder as an
agent decision does (`agent`, `model`, `effort`, `ran`, `posture`, `session`, tokens), so the tier that ran is said. Its facts reach the
reader's provider as any start's prompt reaches its own. A person who wants no second provider changes the order.

**Its bound.** Each reading is one read-only `supervise` turn of at most ten minutes (`SupervisePass.Minutes`), in an empty folder of its
own, shell denied and Daoris's connector alone (supervisor design §2.8). It takes one attempt per choice of the order, and only one
reading per conversation at a time. Its facts are capped: the acting calls' inputs, status and the first lines of their output; the
reads counted, never quoted; the oldest turns of a large batch summarized by count. The day's spend is bounded by the role's budgets,
if the person sets any (rule 28); none is set by default. Its turns take no place under the cap, and its tokens count in the role's
week (`RoleUse`).

**When no reader answers.** Nothing waits on a reading, so no answer blocks anything. A reading the walk cannot start (every choice
signed out, cooling, over budget or not installed) closes its subject unread, said in the walk's one sentence under the turn and on the
access chip. A reading that fails, times out or answers no readable block closes its subject too. A restart closes a running one
(the start's sweep, as for every attempt). Each closed subject is logged `ask-daoris.unread` with why. Its turns stay unread and are
never read again; the next acting turn starts a new subject. *Not read* is always said; a turn is never left looking read.

## 6. How it is shown

- **Under the turn.** One line per reading, below the turn it read, covering the turns it covered: *Reading…*; *Read by Codex
  (gpt-6.1-sol · high): nothing to flag*; *Flagged by Codex: …* with each reason and the call or press it names; *Left to you: …*; or
  *Not read: Claude Code's accounts cool until 14:20.* The words are the catalogue's, en and zh. Served by `HELP_READINGS { session }`:
  `[{ through, from, state, by, ran, reasons, seen }]`.
- **The press line.** `HelpPressNow` gains `reading`, its turn's state (`reading`, `noted`, `flagged`, `left`, `unread`), so
  *Notifications*' pressed list says beside each press whether its turn was read and how.
- **Notifications.** An unseen flag is listed under Ask Daoris's line, including a flag on a turn with no press (a shell push). Each
  row offers *Open the conversation* at the turn, the reverse press where one exists, and *Seen*. A note is not raised.
- **The access chip.** The panel's chip says the reader with the mode: *Full access · read by Codex*, *Full access · not read* with
  the walk's sentence, or *Full access · read by Claude Code, the same agent, fresh*.
- **The conversation.** A flag is told to Ask Daoris once, as one line ahead of the person's next words in that conversation. It is the
  reader's claim, never the person's, and is carried as a focus change is (`HelpFocusTurn.Ahead`, agent-facing English). It never makes
  a turn of its own (`ChatWords.Tell`). Ask Daoris therefore answers a flag in front of the person, never alone. Acting on it in full
  access would be acting on another model's claim as the person. The room says this: a finding is a claim to answer to the person, and
  an act follows only from the person's words. A note is not told.
- **The machine log.** The attempt's `supervisor.*` events as today; the reading kept as every supervisor decision is logged, its kind
  `ask-daoris`; `ask-daoris.unread` with why.
- **The roles.** *Who decides here* at the machine says *Ask Daoris's acts: read by Second check*, or *not read (your rules)* where
  the generic supervisor holds the kind; the role editor shows its kind, holder, order and the two acts.

## 7. Reuse, and what is new

| Reused, unchanged in kind | For |
|---|---|
| The role door, the catalogue's judge, `RoleCatalog.Supervising`, `SupervisorGrants.Covers` | Staffing, placing and granting the reader as any supervisor |
| `SupervisePass`, `SuperviseTurn`, `SupervisorInstruction`, `SupervisorFacts`, `SupervisorBlock` | The attempt walk, the read-only turn and its posture, the prompt with fenced facts, the answer's block |
| `SupervisorAttempts`, `SupervisorSweeps`, `SupervisorDecisionFiles`, `AgentSupervisorDesk` | Attempts, the restart's close, a reading kept as a decision file in the conversation's session folder, `went-on` at its keeping as an allow decision is |
| The decision door (`SUPERVISOR_WAITING`, `SUPERVISOR_DECIDE`, `SUPERVISOR_LEAVE`) | A terminal holder reads the same subjects with `sessions supervise --kind ask-daoris` |
| `AgentFamily`, the *same agent, fresh* label | Another maker first (D155) |
| `HelpPressRecord`, `SessionEventLog`, `HelpFocusTurn.Ahead`, `RoleUse` | The presses, the calls, the told line, the spend |

| New | Where |
|---|---|
| The kind `ask-daoris` and its acts `note`, `flag`; `machine-only` | `Roles/SupervisorKinds.cs`, the catalogue's judge |
| `AskDaorisKind : ISupervisorKind`: subject `{ session, through }` (the conversation and its newest read event's number, unchanged while it waits), its list, keeping, facts and place | `Modules/Bridge/SupervisorKinds/AskDaorisKind.cs` |
| `AskDaorisTurns`: a conversation's acting turns since its last subject, from the events log and the press record, with the access each ran under | a unit of its own in `Help/` |
| The family order for this kind's walk | beside `SupervisePass`'s order, a unit of its own |
| `HELP_READINGS`, `HelpPressNow.reading`, the flags in *Notifications*, *Seen* | Modules and Web |
| The seeded `second-check` role and the home migration step that adds it once | `Defaults/roles.json`, `Transitional/Migration/` |

**Not used:** the role's window and *Overrule* (nothing is held), `SESSION_SUPERVISOR`'s cards on the work head (Ask Daoris has no line
there), the allow machinery (`SessionPressAllows`, `StewardAllows`: nothing is allowed), and the second opinion's gate, rule, clone and
disputes (they read a candidate's commits before a landing).

**One way (D228).** This is the one way a model checks Ask Daoris. The second opinion still reads a candidate's commits; the two share
one family rule (`AgentFamily`) and no runner. No proposal kind, no new grant shape and no new route classification are added.

## 8. Rows, in order

| Row | What | Proof |
|---|---|---|
| **ASKSUPERVISE1a** | The kind and the seeded reader: `ask-daoris` with `note` and `flag`, refused below the machine (`machine-only`); the editor's and *Who decides here*'s words, en and zh, and the glossary's *Second check* (复核); the seed's `second-check`; a home migration step that adds it once, never where a supervisor deciding the kind exists | Catalogue judge and `SupervisorKinds` tests; the step's test on a home with and without one; `i18n:check`, `names:check -- --strict` |
| **ASKSUPERVISE1b** | The turns read: `AskDaorisTurns` and `AskDaorisKind`'s list, one subject per conversation at a time, coalescing turns, a closed subject moving the mark; place the machine | Driver tests over fixture events logs: a turn of reads alone is not listed; an edit, an execute, an unplaceable `other`, a full-access press are; restricted asked presses, held calls and allow-listed calls are not; a granted family's press is |
| **ASKSUPERVISE1c** | The reading: the family order; `AskDaorisKind.FactsAsync` (the person's words fenced as theirs, the presses, the acting calls bounded, the reads counted, the last words); the reader's charge (§4); a `note`, `flag` or leave kept `went-on`; unread closes logged | `SupervisePass` tests with a stub run (`context.SuperviseRuns`): another maker first, the same family only listed and labelled, unavailable closes unread and says so, a block for another subject dropped; a facts composer test; a terminal holder's print |
| **ASKSUPERVISE1d** | What the window shows: `HELP_READINGS`, `HelpPressNow.reading`, the line under the turn, the flags in *Notifications* with *Seen* and the reverse, the access chip | Modules route tests; vitest and stories for each state; `i18n:check` |
| **ASKSUPERVISE1e** | What the conversation hears: a flag told once ahead of the next words; the room's line on findings | Driver test of the lead lines (a focus change and a flag together, a flag told once); the room's text test |
| **ASKSUPERVISE1f** | The install's proof: a full-access conversation on a scratch workspace deletes a quest it was not asked to and runs a shell write in another repository's checkout; the reader flags both; *Notifications*, the line and the next words show it; tokens and minutes per reading measured; then with Codex signed out, *the same agent, fresh* | An evidence record of the measured run, and the area rewritten in place |

Each row rewrites its area's sentences in place: roles rules 41, 42 and 58, conversations rule 42.

## 9. Not in these rows

- **A reading of restricted mode's card** (§3 C): an annotation the card shows when the reader answers, never holding it. It is left
  out because it adds a second subject and display for a yes the person gives anyway. If the person wants it, it is a row of its own
  after f.
- **The terminal's tier `act`** (D271): its presses have no conversation, and their agent is the person's own, outside Daoris.
- **A working session's unrestricted press** (D253 point 6): its check is its place's supervisor and its own line's review.
- **A reader per workspace:** a subject is the machine's (§5 B).

## 10. For the owner

1. **On by default?** Recommended: yes. The seed and the migration add *Second check* to every home, so full access is read from the
   first turn. Reading blocks nothing, and the reader's spend is shown on its role. The alternative is off until the person staffs it,
   with the chip saying *not read*. That keeps spend opt-in, as the second opinion's occasions are, but leaves full access unchecked on
   every machine whose person never looks.
2. **Ask Daoris hears a flag with the next words, never as a turn of its own?** Recommended: yes (§6). A turn of its own would let it
   act on another model's claim while the person is away.
3. **Restricted mode's card read too?** Recommended: not now (§9).

## 11. Rejected, for D272

- A hold before a press in full access (§3 A): it blocks the work full access exists for.
- The reader deciding as the allow supervisor does (§4 D): Ask Daoris's presses are the person's, and a go-ahead is the person's alone.
- A family of routes, or what the room marks, as what is read (§2 B, C): a second classification that misses the shell, and the checked
  agent choosing its check.
- The second opinion as the reader's home: it reads a candidate's commits before a landing, with a gate and disputes that have no
  subject here; its rule is a repository's.
- A reading system of its own: a third way to run a reading model (D228).
- A flag told as a turn of its own (§6, §10 question 2).
