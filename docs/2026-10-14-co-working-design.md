# Co-working: one session per line of work, and an ask's channel

The design under D167, for SESSONE1. Written 2026-10-14 from the installed app's records (times UTC) and the code at
`9377ab7f`. The two repositories of the evidence are named here *repository A* (an app) and *repository B* (its database
repository).

## 0. The owner's direction, and what the install showed

> *"we created more sessions than we needed, this is kind of confusing too, for example I raised 2 tasks, with 1 task need
> 2 repo co-work so there should be just 3 sessions"*

Asked to choose between one session per repository per ask and a grouping on the page only:

> *"we can have multiple requests work together within the same repo, but one that continues should belong to the same
> session entry (not 100% the same session, but the entry should be the same), since we are moving towards a co-working
> session logic, so we might also need a centralized communication channel too"*

Then: *"please update the Daoris app and continue to finish my 2 tasks (quests) with the design we just mentioned"*. So the
first rows of §6 change what the owner sees, on the records the install already holds.

**Ask #c98a7e, one task over A and B**, made seven records and four repository sessions:

- an intake chat that failed, *exit 1 before publishing anything*; quests to A (`28b052ac`) and B (`3068f747`), which the
  person accepted;
- A's first session failed before taking its quest (*the ACP agent refused the call: Authentication required*); its retry
  is a second record, `62c005bc`, which reached done at 07:31;
- B's session `283ef343` asked a new quest of A (`711d2524`) at 06:58 and ended *completed*, its quest awaiting the answer;
- that quest opened another session in A, `8eba4af2`, in a new tree, while `62c005bc` was still working in A on the same ask;
- at 07:59 A's new session asked a new quest of B (`01ba2c71`), which opened another session in B, `e2ddbfc4`, in a new
  tree, while B's first tree holds the earlier work.

**Ask #841c38, one repository**, has its intake chat and one driven session the person's words resumed several times.

**What made the sprawl**, read from the code:

1. **Every request is a record, and every first start a tree.** The ledger keys a driven session on its quest, and a
   record is an attempt, never content-derived (`Sessions.cs:57`). Where a repository opens a tree per session (PAR1), a
   first start grows a tree of its own and only a resume or a carry-on goes back into one (`Planner.cs:1039-1043`,
   `1138-1157`). So a sibling's request ran beside the work it depended on, in a tree that did not hold that work.
2. **A retry is a new record**, as D80 and D131 §5 want: a failed start's work is a new attempt.
3. **The list shows records.** `SessionGroups.Read` places each record (D126 §2.4); nothing says two records continue one
   piece of work. The intake is a record among them.
4. **A sibling's request is on no ask.** Its sender is the repository that asked, and `AskWords.AskOf` reads only a sender
   of the form `ask #id` (`AskWords.cs:66`). So its sessions are not handed the ask's words (D133 §2), and nothing groups
   them with the ask. The publisher is recorded (`Quest.PublishedBy`, `Quests.cs:108`), and travels.
5. **Quests are the only talk between repositories**, and the person reads it quest by quest, on each quest's page.

## 1. The session entry

### 1.1 What it is

**A session, on the window, is one line of work in one repository**: the person's unit, what they count. Underneath, it is
an *entry* that holds one or more ledger records, its *parts*. A part is still one harness conversation and one record
(D131 §4, D137 §8): a first start, a retry, a carry-on, a hand-over, or a later request of the same work in the same
repository. Resuming a part with the person's words is the same part, as today. Nothing about a record's lifecycle
changes, and there is no new ledger state (D126's rejection stands).

### 1.2 Its key

Each record carries one field, `entry`: `<origin>/<repository>`, with `+<n>` where the person split a second session off
(§1.3). The origin names the line of work, and is read at the open, the first rule that applies:

1. **A conversation** the person starts: `session:<its id>`. A conversation handed over (D163's `from`) or started from
   another's words (MSG1f2) takes the entry of the one it continues.
2. **A quest a conversation took** (CHATTAKE1): that conversation's entry, so its later parts stay in its line.
3. **A quest an ask asked**, chain steps included (its sender is `ask #id`): `ask:<id>`.
4. **A quest a session published** (`PublishedBy`): the origin of that session's entry, read from its record, this
   machine's or a teammate's mirror. So B's request of A is on the ask, and A's request of B again.
5. **A chain step with none of these** (`Parent`): its parent's origin, by the same rule.
6. **Otherwise** `quest:<its id>`: a quest a person or an outside session published roots its own line.

An intake's entry is `ask:<id>/intake`. A helper and a reviewer keep their own entry (`session:<id>`) and are listed under
the session they serve by their links, as MIXAGENT3a does: neither is a part, and neither holds the entry's tree.

**Why a key and not a pointer.** The key is the same string on every machine that opens a part of the line, so a teammate's
mirror groups as this machine does, and two machines never need to agree on a record they each created. Quest ids are
content-derived for the same reason (driver design §3).

### 1.3 What joins an entry, and what starts a new one

| What arrives | Today | Under the entry |
|---|---|---|
| The person's words to a part that ended (D137) | the record reopens | the same part |
| An answer to a park (D131) | the record goes on | the same part |
| A retry of a failed start, a carry-on after a cut-off (D80), a fallback (D131 §3) | a new record | a new part: same quest |
| D79's resume once the awaited quest closes | a new record | a new part: same quest |
| A hand-over or a go-on to another agent or account (D163, MSG1g) | a new record with `from` | a new part: same quest, or rule 1 |
| *Start a conversation with these words* (MSG1f2) | a new conversation | a new part: rule 1 |
| A chain's next step to the same repository | a new quest and record | a new part: rule 3 or 5 |
| A sibling's request under the same ask, to this repository | a new quest, record and tree | a new part: rule 4 |
| A request of another ask, or of no ask, to this repository | a new record and tree | **a new entry**, side by side |
| The person's *Run as its own session* (below) | none | **a new entry** of the same ask and repository, `+2` |

**A second, concurrent request in the same repository** is what the owner allows: requests of different lines of work run
side by side, each its own session and tree (PAR1 stands). A second request of the *same* ask in the same repository joins
its session and waits for its tree (§1.5), because it continues that line and needs that line's work in progress. Where the
person wants it now, *Run as its own session* (另开会话) on its waiting row splits it off: a new entry, `+2`, with its own
tree, started at the driver's next look. Whether this should be the default is the owner's first open question (D167).

### 1.4 Where it lives

**One field on the session record**, written by the ledger at every open, which holds the quests and the sessions the rule
reads. It travels with the record on the session wire (`SessionWire.cs`, `SessionSync.cs:43-56`): it names a line of work
and a repository, no path and no account. A remote from before drops it, and the reader derives it as for an old record.

**Records from before are not rewritten.** The service answers `entry` on every record it reads: the stored key, or the
same rule over the facts it still holds, never written back. So the owner's existing records group the day the reader lands.
Where a fact is gone (a quest a clear forgot, HIST1b), the record is its own entry, `session:<id>/<repository>`. One rule, in
the service's Sessions module, decides both; the driver reads the answered key, so no twin of the rule exists.

**Not a record of its own.** An `entries` table would need its own store, wire, sync feed and a merge for two machines that
each open the line first. The key holds what the entry is; its name, state and parts are read from its records.

### 1.5 The tree and the branch

> **Amended 2026-10-14 (D167, the owner's choice):** a new request of an entry that finds the entry's tree held **runs
> alongside** in a tree of its own, as a further part of the same entry, rather than waiting; `EntryBusy`, *Run as its own
> session* and the one-branch rule below are superseded, and an entry may land more than one branch. Read this section, the
> replay in §3 and the rows in §6 with that change. **Amended again 2026-10-15 (D167, LANDSYNC0):** parts still run
> alongside, but one Accept lands an entry's parts on one branch; D167's amendment says when it is more than one.

**The entry is what a tree belongs to.** The tree stays the unit of exclusion (D51): one live part per tree. An entry's tree
is the tree its newest part on this machine held.

- **A new part goes into the entry's tree** when it stands and nothing live holds it: the planner's `into` (PAR1's rule for
  a resume or a carry-on), now also for a new quest of the entry.
- **While a part of the entry is live, a new quest of it waits**: a new verdict, `EntryBusy`, said on its row (*waits for
  part 2 of this session to end; it runs in the same tree after it*). A wait, never a strike. A wait cannot deadlock: a part
  that waits on another repository ends its turn and frees the tree (ask-and-wait design §2).
- **A tree the tidy removed** after a landing: the new part grows from the branch the entry last landed on (D82's rule, by
  entry rather than by chain), else from the base, and its first line says which.
- **A new part is told it continues the session**: the earlier parts' quests, how each ended, the previous part's plan and
  last words, and what it left uncommitted, as a carry-on is handed them (agent-neutral facts, the mixed-agents design §1).
  Never a transcript, and never the previous part's harness conversation (§5).
- **Different entries run side by side**, each in its own tree, under the cap. A repository whose sessions run in the
  checkout keeps one session at a time (D51); the entry changes only its grouping there.

**One branch per entry** (row 1f). D145 §3 lands a chain on one branch so that *a ticket has one pull request*. An ask is
the ticket, and its entry in a repository may hold several chains (A's `28b052ac` and B's request `711d2524`). So the
entry's first done names its branch, from its first quest, under the repository's landing rule, and each later part's done
advances it by D145 §3's fast-forward rule. Until 1f, each chain lands on its own branch, the later one holding the earlier
one's commits, which its review says.

### 1.6 On the page and in the terminal

- **The list shows entries.** One row per entry: its name, its repository, the leading part's word and second line, and
  *3 parts* where there is more than one. Its name is the ask's (short title, else first line; `askName`) for `ask:`, the
  root quest's for `quest:`, the conversation's for `session:`. Where a teammate's machine holds no ask, it is the first
  quest's.
- **Its group** is the first of D126's five any of its parts qualifies for, and the part that put it there leads (ties: the
  newest). *Waiting on you* stays one rule (`SessionGroups.WaitsOnYou`). A request waiting for the entry's tree is said on
  the leading part's second line (*and #711d2524 waits for its turn*).
- **Intakes.** By state, an intake is listed while it runs or waits on the person. An ended intake whose ask became quests is
  its ask's, and its ask's page already names it (D132's PAUSE1h). One that ended without publishing stays listed, since only
  the person moves its ask on.
- **Group by ask**, a third choice in the list's ⋯ beside *State* and *Repository* (D126 §4.1): each ask a heading, under it
  its intake, its sessions and its channel with its unread count; conversations and quest-rooted lines under *Not on an ask*.
- **The session's page** keeps its head (HEAD2c) for the entry: the name, the repository, the leading part's pill and acts.
  Under it, a *Parts* strip, oldest first: the number, the quest, how the part began (a start, a retry and why, a carry-on and
  why, *handed over from Codex*, *asked by B*, *your words*), its state and when. A press shows that part's conversation and
  *Details*; the newest is shown first. Every act stays a part's (D126 §3) and acts on the part shown; *Archive* marks every
  part, and a new part shows the entry again. A go that names a record (ENTRY1f2) opens its entry at that part.
- **The ask's page** lists its work by entry (D132's work list, `asks/workTree.ts`): each repository's session with its
  parts, each part's quest beneath.
- **The terminal**: `daoris-driver sessions` prints the same entries in the same groups, each entry's line then its parts
  indented (id, quest, state, how it began). A part's id still addresses every verb.

## 2. The ask's channel

### 2.1 What it carries

**One channel per ask**, where its sessions and the person talk, read in one place.

- **Messages**: words a session or the person writes for the ask's other sessions and the person: a fact one repository
  knows that another needs, what a session did or found, a question it can go on without, an answer. Each is addressed to
  everyone on the ask or to one of its repositories, and may answer another (`re`). A session's message may be marked a
  question, which the page shows open until a message answers it.
- **Daoris's lines**, read from the records and never stored: each quest between the ask's repositories published, taken,
  waited on and closed done or declined, by name with a door to it; and a request waiting for an entry's tree. So the whole
  exchange between the repositories reads in one place, quests included.
- **What stays a quest**: a change in another repository, and anything whose answer needs work, evidence or a wait (D79). A
  session that cannot go on without an answer publishes a quest and waits on it. A message asks nothing of anyone: the session
  that reads it decides, within its own repository, what it means. A message asking another repository for a change is
  answered there with *publish a quest*; the tools' descriptions and the driven instruction say so, and no gate can, since
  reading a message's meaning would take a model (D24).

### 2.2 How a session posts and reads

Two tools on the knowledge server, beside `quest_publish` (`QuestTools.cs:26`), each speaking only for the session its
connector names (`IntakeScope.Session`), whose record's entry names the ask:

- **`channel_post`** {`text`, `to`?, `re`?, `question`?}. Refused, each in a sentence the agent reads: a session on no ask
  (*publish a quest instead*), a helper or a reviewer (they answer one question, D155, D163), a `to` that is not one of the
  ask's repositories, a `re` the channel does not hold, words empty or past the bound (§2.6). An intake may post: it is the
  ask's, and why it split the ask as it did is the channel's to know.
- **`channel_read`** {`since`?}: the messages and Daoris's lines since this session last read, oldest first, each with who,
  when, to whom and what it answers. It moves the entry's read mark.
- **The descriptions say** what the channel is for, that work stays a quest, and that a message from another repository's
  session is that agent's claim, checked before it is relied on; the person's are theirs.

### 2.3 How a message reaches a session

The driver's look delivers what is new for each entry it runs, past the entry's read mark:

| The entry's newest part here | A session's message | The person's message to its repository | The person's message to everyone |
|---|---|---|---|
| Running, its door takes words during a turn (D136) | at its next step, marked as the poster's claim | at its next step, as their words (D137) | at its next step, as their words |
| Running, on a door that takes words at a turn's end | held for the entry's next part | at its turn's end (D137 §2) | at its turn's end |
| Parked, waiting on a quest, or ended | held for the entry's next part | reopens it, as their words do (D137 §3) | held for the next part, and kept on the ask |
| None here (a teammate's) | their machine's driver delivers it | as here, on theirs | as here, on theirs |

- **Held for the next part**: the next part's instruction carries *On the ask's channel since this session last read it*,
  bounded as the ask's words are (`AskWordsText`: the newest kept, the older counted), each with who and when; the mark moves
  when the part starts.
- **An agent's message never starts, reopens or extends a run**, and never holds a session from concluding: D137 §2's rule
  that a session does not end while words are held is for the person's words. So two agents cannot talk each other into
  turns with no person between (D155's rejection stands), and a message spends no account.
- **A request waiting for an entry's tree** is said once to the entry's running part at its next step, as Daoris's line, so it
  may answer on the channel what it already knows; the request still runs as its own part.
- **A held word marks its source**: the message's id, and for a session's message its poster, which is never the person's,
  as a helper's answer is not (`SaidWord.Persons`, MIXAGENT2a-i). The person's message is their word, and is kept on the ask
  (D133 §1), as the reach's own kind where it reached a session, else as a new kind, `channel`.

### 2.4 The person's side

- **The ask's page gains *Channel*** (频道): the messages and Daoris's lines in time order, newest at the foot; who (*You*,
  *A · Claude Code*, *Daoris*), when, to whom, an answer's `re` quoted on one line with a door, an open question marked.
  Below them, a composer: *To* (*Everyone*, or one of the ask's repositories), the words, *Send*, and under the box the
  sentence *Everyone working on this ask reads it.* Daoris's lines wear the neutral tone and open their quest.
- **Each session on an ask gains a *Channel* panel** on its page: the same list and composer, *To* starting at *Everyone*.
  The ask's row and *Group by ask* count what is unread.
- **The terminal**: `daoris-driver ask channel <ask>` prints it; `daoris-driver ask channel <ask> [--to <repository>] "…"`
  posts the person's words.
- **Ask Daoris**: the post is the person's words, so `CHANNEL_POST` is exempt in `HelpCoverageTests`, as *say* is (D137
  point 10); `CHANNEL_READ` is no press.

### 2.5 How it is stored, and where it travels

- **Service records** in the Asks module: a `channel` table, one row per message (id, ask, workspace, when, the poster's
  session or none for the person, the poster's repository and agent, to, re, question, the words, origin), and a read mark per
  entry, this machine's own. The HTTP host answers `GET /api/asks/{id}/channel` and takes `POST /api/asks/{id}/channel`,
  each DTO registered in `ApiJson`.
- **It travels as the session records do** (SYNC4), within the ask's workspace: this machine's messages up by a cursor, the
  team's down as `origin/id`, read-only here. The words are cleaned of machine paths on the way, as a note is
  (`SessionNote.ForAnotherMachine`), and a message carries no file. The shared host keeps and serves them, with no UI and no
  path (D47 §4). The ask itself stays this machine's (`Asks.cs:210`): a teammate reads the channel by the ask's id, which
  every quest of the ask carries.
- **Delivery is each machine's driver's**, for the parts it runs. On one machine with no remote the channel works whole:
  local operation needs no remote. A remote from before has no channel doors; the sync says so once, and the channel stays on
  the machine.

### 2.6 Bounds

A message is at most 4,000 characters of text; a link travels as text, a file not at all in this version. A part posts at
most ten messages, past which the tool refuses in a sentence. A next part is handed at most the twenty newest, within the
instruction's share (`AskWordsText`'s reason: the native door's one argument). These are first values, read again at the
canary (§6).

### 2.7 Both languages

The chrome translates in English and Chinese: the strip's *Part 2 of 3* (第 2 段，共 3 段), *Run as its own session*
(另开会话), the channel's headings, *To*, *Everyone*, the composer's sentence, the unread counts, and Daoris's lines, which are
coded and worded on the page in the reader's language (D142). The glossary gains *part* 段 and *channel* 频道, and *session*'s
meaning becomes the person's one line of work in a repository. Messages, quest titles and notes are shown as written. The
tools answer in English, as every tool does.

## 3. The evidence, replayed

| Record | Today | Under the design | Why there |
|---|---|---|---|
| The intake (failed) | a row in Sessions | ask #c98a7e's intake, on its page; out of the by-state list | its ask became quests |
| A's first session (failed before its take) | a row | A, part 1 | `28b052ac`'s sender is the ask (rule 3) |
| `62c005bc` (the retry) | a row | A, part 2, in part 1's tree | the same quest: a retry |
| `283ef343` (B) | a row | B, part 1 | `3068f747`'s sender is the ask |
| `8eba4af2` (A, for `711d2524`) | a row, a new tree beside `62c005bc` | A, part 3: waits for part 2 (07:31), then in A's tree | published by `283ef343`, whose origin is the ask (rule 4) |
| `e2ddbfc4` (B, for `01ba2c71`) | a row, a new tree | B, part 2, in B's tree, which `283ef343` left free | published by `8eba4af2`, on the ask |
| B's resume once `711d2524` closes (D79) | a further record | B, part 3, after part 2 if it still runs | the same quest, `3068f747` |
| #841c38's intake and session | two rows | its intake on its page; one session, its resumes the same part | D137 |

**What the person sees.** Sessions, by state: three rows, *A* and *B* under ask #c98a7e's name and the one session of
ask #841c38. By ask: each ask, its intake, its sessions and its channel. Ask #c98a7e's channel reads: *B asked A: … #711d2524*
(06:58), *B waits on #711d2524*, *A took #711d2524*, *A closed #28b052ac done* (07:31), *A asked B: … #01ba2c71* (07:59), each
with its door, and any message the sessions or the person wrote between.

**What it costs.** `8eba4af2`'s quest would have waited 33 minutes for part 2 instead of starting at once. In exchange it would
have run on the tree holding part 2's work, and landed with it on one branch. *Run as its own session* gives today's start back,
by the person's choice. Whether `711d2524` and `01ba2c71` asked for changes or for facts was not read; a fact would be a channel
question instead, answered without a part of its own.

## 4. What does not change

- **A session changes its own repository, and asks another for work through a quest** (AGENTS.md;
  `repository-owns-its-work`). The channel moves words and never a change.
- **One record per harness conversation** (D131 §4, D137 §8), the observed lifecycle (D46 §4), and no new ledger state.
- **The tree is the unit of exclusion** (D51); one live part per tree.
- **Records in the service, processes in the driver** (D46). The shared host serves no UI and no machine path.
- **No model** reads, sorts or summarises the channel; a message is a claim shown as written (`model-decoupling`).
- **Local operation needs no remote.**

## 5. Rejected

- **A session per repository per ask, forced into one process.** A session is one turn on either door, and a fresh session
  resumes (ask-and-wait design §2); a long-lived agent cannot be pushed to and burns context waiting (D46). A wait, a limit,
  a hand-over and a crash each end a process anyway. The entry gives the person one session without promising one process.
- **One harness conversation across an entry's parts.** A record is one conversation (D131 §4), a part may run on another
  agent or account, and a conversation that only grows is the one WAITSHOWN1 saw sit sixteen minutes before its first answer.
  A new part is handed the earlier parts' facts instead.
- **Grouping on the page only.** The page and the terminal would group differently (D126 §2.4's one reader), a teammate's
  mirror would not group at all, the planner's tree rule needs the entry, and a split or a hand-over cannot be read back from
  the records. D131 rejected a fold every reader must learn; the key is one field that travels and one reader that groups.
- **An entry record of its own**: §1.4.
- **The ask on the quest record.** A field an existing quest operation gains reaches ten places
  (`.claude/knowledge/quest-operations.md`), and the publisher is already recorded, so the ledger reads the line at the open.
- **A channel per repository pair.** An ask over three repositories would have three channels, and the person would read
  three places for one task; a pair hides from the third what it needs to know. The owner asked for one, centralised.
- **Replacing quests by channel messages.** A quest is the unit of ownership: its take is the lock, its done answers the
  person's requirements with evidence, it lands, its strikes bound it, and a wait on it resumes the asker. A message has none
  of these, so work asked by one would be done by whoever read it, unowned and unproven.
- **A message that starts or reopens a session.** Agents would talk each other into turns with no person between (D155), each
  turn spending an account. A question one cannot go on without is a quest with a wait.

## 6. Build rows

Each row is one worker's. The first three change what the owner sees, on the records the install already holds; the next
two change how the owner's continuing work is started; then the channel.

| Row | Lanes | What | Proof | After |
|---|---|---|---|---|
| **SESSONE1a** — the entry's key | service | The rule (§1.2) in the Sessions module; `entry` written at every open (`OpenAsync`, `OpenChatAsync`, `OpenIntakeAsync`, the open doors' optional split `+n`); the column added where the store opens; every read answers it, stored or derived, never written back; the session wire and the feed carry it; the DTO in `ApiJson` | service tests: the rule's table with the evidence's records as a fixture, a record from before derived, a forgotten quest, the wire's round trip, a feed without the field | none |
| **SESSONE1b** — the driver reads entries | driver | `SessionGroups.Read` groups by `entry`: each entry's group, its leading part, its parts and how each began, the intake rule (§1.6); `SESSION_GROUPS` answers `entries` beside `sessions` (added, nothing changed); `daoris-driver sessions` prints by entry | `SessionGroupsTests` (the table, the evidence), `SessionsCommandTests`; search the rehearsals for `SESSION_GROUPS` answers compared whole | 1a |
| **SESSONE1c** — Sessions shows entries | modules, web-shell | One row per entry; the *Parts* strip and acts on the part shown; archive per entry; *Group by ask*; the ask page's work by entry; both catalogues; the glossary's *part*, *session* | vitest (rail, frame, ask work list), stories in both languages and themes and at 420 px, `i18n:check`, `names:check --strict`; the installed window: the owner's two asks read as three sessions | 1b |
| **SESSONE1d** — an entry's parts share its tree | driver | The planner's `into` for a new quest of an entry; `EntryBusy`, no strike; a removed tree grows from the entry's landed branch, else the base, said; the new part handed the earlier parts' facts; different entries side by side | `PlannerTests` replaying the evidence (`8eba4af2` waits for `62c005bc`; `e2ddbfc4` goes into `283ef343`'s tree), the trees' growth tests, the instruction's golden | 1a |
| **SESSONE1e** — run as its own session | driver, modules, web-shell | The person's press on a waiting request: a box the look takes, the open's `+n`, its own tree; `daoris-driver sessions alongside <quest>`; both catalogues | driver tests (the box, the key, the start), web tests; `HelpCoverageTests` needs the press's row, door or exemption, which the parent adds at the merge | 1c, 1d |
| **SESSONE2a** — the channel's record and doors | service | The `channel` store and read marks; `channel_post` and `channel_read` with their descriptions; the HTTP routes and DTOs; Daoris's lines read from the ask's quests; the person's message on the ask's words (`channel` kind); the bounds | service tests (store, refusals, lines, bounds), the MCP tools' census, the HTTP tests | 1a |
| **SESSONE2b** — the channel reaches sessions | driver | §2.3: next step marked as the poster's claim, held for the next part, the person's by D137's reaches; the next part's section; the waiting request's line; the driven instruction's paragraph; no run started, reopened or held by an agent's message | driver tests over stub sessions on each door and state, the instruction's golden, a test that an agent's message holds no session from concluding | 2a, 1d |
| **SESSONE2c** — the channel on the page and in the terminal | modules, web-shell, driver | `CHANNEL_READ` and `CHANNEL_POST`; the ask page's *Channel* and the session page's panel; *To*; Daoris's coded lines; unread counts; `daoris-driver ask channel`; both catalogues, the glossary's *channel* | vitest, stories, `i18n:check`, `names:check --strict`; `HelpCoverageTests` (`CHANNEL_POST` exempt: the person's words), the parent's row at the merge; the installed window: the person's message reaches a running session | 2a, 2b |
| **SESSONE1f** — one branch per entry | driver | The entry's first done names its branch; each later part's done advances it (D145 §3's rule); the review and the plugin's frame say which parts it holds | landing tests with a two-part entry, an advance refused where the branch moved | 1d |
| **SESSONE2d** — the channel travels | service, tools | `ChannelSync` beside `SessionSync`; the remote host's two doors; `origin/id`; the words cleaned of paths | service sync tests; a family-rehearsal check that a message posted on one machine is read on the other (the parent runs it) | 2a |

**Order and what may run together.** 1a, 1b, 1c, then 1d and 1e, then 2a, 2b, 2c, then 1f and 2d. 2a is the service's and
may run beside 1b, 1c or 1d; 1d may run beside 1c. WAITSHOWN1 touches the same rows and heads as 1b and 1c, and its *waits
rather than completed* is the leading part's fact: dispatch it after 1c or fold it in. MIXAGENT3a-ii's nesting of helpers rides
1c's rows.

**The canaries.** After 1c, the parent's installed look at Sessions by state and by ask on the owner's two asks. After 1d,
the owner's next request between A and B: one tree per session, the waiting request said. After 2c, a message the person
posts reaching a running session, and a session's post on the ask's page.

## 7. What this document's gate does not cover

Documents only; nothing is built. The evidence is the brief's reading of the installed app's records on 2026-10-14; this
design did not read the install, its transcripts or its trees. The code facts were read at `9377ab7f`. Whether a
serialised entry costs the owner more waiting than the consistency saves is not measured, nor how many messages a real
co-working ask produces: §2.6's bounds are first values. Whether an agent obeys *work stays a quest* is a matter of its
instruction, which no gate reads. `verify` checks this document's place in the router and its links, and none of its words.
