# A move keeps the agent's own context, and a session goes on as one conversation whatever runs it (SWITCHCTX1, 2026-10-24)

**Standing: proposed, round 1, under D191.** Written by Claude Opus 5.5 from the code on main at `ddf0a24fd`; nothing here is
built. It changes a decided rule other designs rest on (D189 point 1's *never read, copied or written by Daoris*, and the
rejections of copying a transcript in D125, D189 and D206), so under D191's amendments of 2026-10-20 and 2026-10-23 it takes two
reads, Codex's and the independent Opus reader's, before anything is built. Its notes on D125, D131, D165, D167, D189, D197 and
D206 say *proposed* and point here.

The row SWITCHCTX1 in `TASKS.md` carries the case and the owner's direction of 2026-10-24: an account switch, and a switch to
another agent such as Codex, continue the session rather than start a new one. On the install a kit's chat moved at 06:52 UTC from
one account to another over the usage curve, and its agent started afresh: the person read it as a new session, though D167 keeps
every record on the line's one entry as its parts.

A path without a root is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; page paths are under `src/Daoris.Web/src/`. *An
account's home* is the configuration home Daoris makes per account (`<home>/harnesses/<agent>/<account>`, below).

## 0. The direction, and the two halves

A session is one line of work (D167 point 1). What runs it may change: the account, when the usage curve or a limit moves it
(D165, LIMITGOON1), and the agent, when it is handed over (D163) or its actor's next choice takes it (D205's (e), LIMITGOON2).
The direction is that none of these reads as, or works as, a fresh start. Two halves, because only one of them can keep the
agent's own memory:

1. **The same agent on another account** keeps its own conversation: the conversation's own files are carried to the next
   account's home, and the same conversation resumes there by the agent's own resume. Where that resume fails, the move goes on
   as it does today, from Daoris's brief, and says so.
2. **Another agent** cannot take the first agent's own conversation: one maker's store is not another's. The line goes on from
   Daoris's record of it, the brief, as LIMITGOON1's and LIMITGOON2's go-on does today.

**On the page, either half reads as one conversation**: the earlier parts' turns above, the move said between, never a fresh
session.

## 1. Today, read from the code

### 1.1 A move over the curve

- **The decision** is `SwitchDecision.Decide` (`Accounts/Domain/SwitchDecision.cs:83-85`), a pure function returning a
  `SwitchChoice` (`:10`): `Stay` with a reason (`:17`, reasons `:33-48`), `ReadOthers` (`:23`) or `MoveTo(Account, Window, Used,
  Curve)` (`:29`). A move is held for thirty minutes after the last (`Hold`, `:68`).
- **A conversation moves at its turn's end.** `ChatRunner` hands a turn's end to `CurveMove.Due`, or to the limit's move where
  the turn was limited (`Conversations/Chat/ChatRunner.cs:1497-1501`). `CurveMove.MoveAsync` (`Conversations/Chat/CurveMove.cs:62`)
  passes Ask Daoris, teammates' records and intakes by (`:73-74`), keeps the `MoveTo` (`:106`), and asks the hand-over with the
  agent unchanged, why `curve`, by `daoris` (`:107-108`), then takes it (`:118`).
- **The take opens a new conversation.** `ChatHandOver` holds the turns and starts the new part on the account the decision named
  (`Conversations/Chat/ChatHandOver.cs:142`, `:153`, `:156-157`). `HandOverTake.RunAsync` (`Conversations/HandOver/HandOver.cs:232`)
  composes the brief (`:250`), ends the old part (`:254`), starts a new record `from` it (`:256`), and hands the new agent a
  message holding the brief and nothing of the person's (`:271`). The new part's first line is `HandOverNotes.MovedFrom(left, move)`,
  `chat.moved-from`, and the old part's last line `HandOverNotes.Moved`, `chat.moved` (`:266-276`;
  `Conversations/Domain/HandOverRecords.cs:188`, `:198`).
- **A quest's session moves by its carry-on.** `Driver.ContinuesFrom` names the why: a hand-over, then `limit` on another account,
  then `curve` where the walk's step was over the curve or the most room (`Loop/Driver.cs:2548-2559`); the carry-on opens a new
  record `from` it (`:2435-2438`), handed the brief (`:2521-2525`), its lines `moved.on` and `moved.from` (`SayMoved`, `:2515-2528`;
  `HandOverNotes.MovedOn`/`MovedFrom`, `HandOverRecords.cs:159`, `:169`) and the machine log's `session.moved`
  (`Conversations/Domain/Continuation.cs:588-590`).

So every move over the curve is a new part on a new conversation, handed the brief. The account-switch design said what that
costs and left this row to measure it (`docs/2026-10-12-account-switch-design.md:132-135`).

### 1.2 A limit's go-on

- **LIMITGOON1**: a conversation whose turn its account's limit refuses moves at that turn's end to the next account of its
  agent the walk offers (`Conversations/Chat/LimitMove.cs:51`, the walk at `:85-86`). It ends the old part (`:133`), starts the new
  `from` it, why `limit`, by `daoris` (`:141-144`), and hands it the brief, then the words the refused turns held, then the words
  that waited (`:156-166`; `LimitHeld.Of`, `Conversations/Domain/LimitHeld.cs:11`). Its remarks say why the brief carries it:
  *the agent's own conversation is in the cooling account's home, which Daoris never reads, copies or writes (D189 point 5), so it
  cannot resume on the next account* (`LimitMove.cs:12-13`). D197's LIMITGOON1 note and D189's say the same
  (`docs/decisions/D197.md:371-379`; `docs/decisions/D189.md:246-250`).
- **LIMITGOON2**: where no account of its agent has room, `ElsewhereAsync` walks its actor's effective order without that agent and
  opens the new part on the first choice that can run (`LimitMove.cs:92`, `:426`), handed the same brief and words: D205's (e)
  (`docs/2026-10-23-actors-design.md:478-494`), *never the transcript*.
- **One conversation on the page, for Ask Daoris only.** Ask Daoris's history folds a part handed the brief into its line
  (`HelpConversation.Parts`, `Help/HelpConversations.cs:22`, the fold at `:233-249`, the link `Brief = "brief"` at `:124`), and
  the page follows a chosen conversation to its newest part (`help/AskConversation.tsx:202-207`) and draws the earlier parts above
  it, read-only (`:870-876`). LIMITGOON1's note says a repository's chat *was one session by its parts already* (D189.md:255-258).

### 1.3 A hand-over to another agent

D163 point 1: *a hand-over is a carry-on with its agent named*, in the same tree, handed Daoris's record of it
(`docs/decisions/D163.md:9-11`), since D189 the brief (D189.md:57). D163 rejects *carrying the transcript or a model-written
summary* (D163.md:21-22). It goes by `HandOverTake.RunAsync` as §1.1's move does, the opening line `chat.taken-over` and the
last `chat.handed-over` (`HandOver.cs:270`, `:274`; `HandOverRecords.cs:134`, `:148`).

### 1.4 A line's parts, and what the page shows of them

- **One entry, many records.** D167 point 1: a session is *an entry holding one or more ledger records, its parts*, each *one
  harness conversation and one record* (`docs/decisions/D167.md:14-16`). The entry's key is `<origin>/<repository>`, a
  conversation's origin its own `session:<id>`, and a record that continues another takes that one's origin through its `from`
  (`src/Daoris.Service/Daoris.Service.Core/Sessions/SessionEntries.cs:26-37`, `:74`, `:87-89`; written at open,
  `Sessions.cs:818-819`). So a line's key is `session:<first>/<repository>`, `<first>` its first record (the fallback,
  `Trees/SessionGroups.cs:129`).
- **The page shows one part at a time.** A repository's chat draws the *Parts* strip, one button per part, oldest first, how each
  began, *Part 2 of 3* (`work/head/PartsStrip.tsx:90-113`, hidden for one part, `:107`), and the conversation of the attended part
  alone (`work/WorkFrame.tsx:2456-2457`). Pressing an earlier part shows it in place of the newest. **This is what read as a fresh
  session on the install**: after the move the attended part is the new one, its conversation opens empty under the brief, and the
  turns before it are a press away.
- **The words call it a new session.** `note.moved.on` and `note.moved.from` say *went on in session {{session}}* and *the new
  session* (`locales/en/note.json:197-198`); `note.limit.resume-refused` says *it went on in a new session* (`:206`); zh the same
  (`locales/zh/note.json:197-198`, `:206`). The glossary's *session* is the whole line and a run is a *part*
  (`locales/glossary.json`, rows `session` and `part`).

### 1.5 Where each harness keeps a conversation, per account home

- **The homes.** `HarnessSettings.ProfileHome` puts each account's home at `DaorisHome.Entry(home, HomeEntry.AgentHomes, agent,
  profile)` (`Accounts/Harnesses/HarnessSettings.cs:881-882`; `harnesses/*/*`, `Platform/DaorisHome.cs:370`), and
  `HarnessSelection.Apply` creates it and names it to the agent by its variable (`Accounts/Harnesses/HarnessSelection.cs:579-580`):
  `CLAUDE_CONFIG_DIR` for Claude Code on both doors (`Agents/Adapters/Adapters.cs:1927`, `:2365`), `CODEX_HOME` for Codex
  (`:2110`; `Accounts/SignIns/CodexAccounts.cs:42`).
- **Claude Code** keeps a conversation as `projects/<folder>/<id>.jsonl` under its home, `<folder>` the working directory with
  every character but a letter or a digit turned into `-` (`Platform/ToolHomeLeftovers.cs:30-31`, `Flatten` `:55`); its resume
  reads it there and refuses a missing one with `resource_not_found` (`docs/2026-10-02-answer-continues-design.md:36-38`). Read on
  this install's own account homes, 2026-10-24, by listing names only: beside `<id>.jsonl` is a folder `<id>/` holding that
  conversation's `tool-results/` and `subagents/`; the home also holds `.credentials.json`, `.claude.json` (the account's own
  configuration, a tree's trust among it, `Accounts/SignIns/ClaudeTrust.cs:82`), `sessions/` (running processes' files),
  `session-env/`, `shell-snapshots/`, `plugins/`, `skills/`, `cache/`, `state/`, `telemetry/`, `backups/`, the policy and remote
  settings files, and `usage-read/`. A folder is the same on every account home for the same tree, since a move keeps its tree
  (D189 point 2).
- **Codex** files a session by the date it ran, the folder it ran in said only inside its file, which Daoris never reads
  (`ToolHomeLeftovers.cs:41-42`). No Codex account home on this install holds a session, so the file's name and whether it carries
  the conversation's id are not read here.
- **The ids.** The agent's conversation id is kept per record in `<home>/sessions/<record>.harness.json`, never read from the
  agent's home (`Conversations/GoOn/HarnessConversations.cs:8-10`, `Keep` `:31`, `Read` `:54`), as `HarnessConversation(Adapter,
  Conversation)` (`Conversations/Domain/GoOnRecords.cs:35`), kept for a chat at `ChatRunner.cs:2369`.
- **The resumes.** The protocol door resumes by `session/resume` where the agent advertises it, else `session/load`
  (`Agents/Acp/Acp.cs:3224`, `:3270-3288`), `resource_not_found` read as `gone` (`:3245`), for claude-code-acp and codex-acp alike
  (`Adapters.cs:1506`, `:1828`, `:2060`). The pipe door is Claude Code's alone, `--resume <id>` (`Adapters.cs:2212`,
  `:2219-2223`, `:2279`; `ChatRunner.cs:1311`); there is no native Codex adapter (`:1512`).
- **No resume follows a move.** The judge refuses a record whose account is not the one it ran on, *another account would not
  find it* (`Continuation.cs:177-181`, `:226-229`); every move opens a new record by `StartAsync(from: …)` and a fresh
  conversation (`HandOver.cs:256`, `BriefedMove.cs:132-134`, `LimitMove.cs:141-144`).
- **HOMELAYOUT5** (open, `TASKS.md:120`) asks the neighbouring question, whether a conversation resumes from a *moved folder*
  (`docs/2026-10-16-install-layout-design.md:166-167`, `:243-245`, `:272-273`): the tree moved and the home kept. Here the tree is
  kept and the home changes, so its answer is not this one's, and neither has been measured.

### 1.6 What the decisions say of copying

- The account-rotation design, D125's contract, §3.5 point 5: *What does not travel is the harness's own conversation … which
  Daoris never reads or copies (D49 §4, D66 §3) … Daoris's accounts are separate homes by design, and what crosses between them is
  Daoris's* (`docs/2026-10-01-account-rotation-design.md:336-339`). D125 point 4 and its rejection of *copying the harness's
  conversation between account homes (a read inside an account's directory, D66 §3)* (`docs/decisions/D125.md:25-30`, `:61-62`).
- D189 point 1: the agent's own conversation is *never read, copied or written by Daoris*; point 5: never handed *anything read or
  written in a harness's store*; rejected: *copying or writing a harness's store across accounts or makers* (D189.md:14-17,
  `:33-34`, `:51-54`). The session-context design §9 gives three reasons: it acts inside an account's home, it depends on a format
  the harness changes at will, and it can never cross makers (`docs/2026-10-20-session-context-design.md:368-371`).
- D206 (LIMITRESUME1) rejects *copying the transcript to another account's home: D189 point 5, D125's rejection*
  (`docs/2026-10-23-limit-resume-design.md:673`).
- D165 point 3 moves a conversation *by the hand-over with its agent unchanged* (`docs/decisions/D165.md:17-19`), and the
  account-switch design's row SWITCHCTX1 was an evidence row spending two short turns (`:198`).

## 2. The design

### 2.1 Which moves carry

A move **carries** where all of these hold, and only there:

- the agent is the same (its adapter unchanged) and the account changes;
- the tree is the same (every move keeps it, D189 point 2), so the conversation's folder is the same name in both homes;
- the old record kept a conversation id (`HarnessConversations.Read`);
- the door resumes: the protocol door, and the pipe door only once LIMITRESUME1f has recorded that its `--resume` of a missing id
  fails closed (§3.3);
- the agent's carry table (§2.2) names where its conversation lives.

The moves that meet it today: D165's curve move of a conversation (`CurveMove` → `HandOverTake`) and of a quest's session (the
carry-on, why `curve`); LIMITGOON1's move to another account of the agent (`LimitMove`, live or ended); D206's briefed move of a
limit-held conversation to another account that frees first; and the person's own hand-over to another account of the same agent.
Ask Daoris is carried as a repository's chat is: its room is its tree. Everything else (another agent, a teammate's record, an
intake, a tree gone, an id never kept) goes by the brief, as today.

### 2.2 What is copied, and what never is

The carry copies **the conversation's own files and nothing else**, named by its id, whole and byte for byte. Daoris never parses
them, never edits them and never writes into them; it moves bytes it does not read for meaning, as a file copy does. A versioned
table per agent names the files, written from what was read on the pinned version, beside the adapters:

| Agent | The conversation's own files, under the account's home | Read on |
|---|---|---|
| Claude Code (both doors) | `projects/<Flatten(tree)>/<id>.jsonl`, and the folder `projects/<Flatten(tree)>/<id>/` where it stands | this install's homes, 2026-10-24 (§1.5) |
| Codex (codex-acp) | the one file under `sessions/` whose **name** holds `<id>`, at the same relative path | not read: no Codex home here holds a session (§1.5); the row is written when one does, and until then Codex moves by the brief |

**Never copied**, whatever the table says: an account's credentials (Claude's `.credentials.json`, Codex's `auth.json`); its own
configuration (`.claude.json`, `config.toml`, `settings.json`), which holds its identity and its own trust of a tree; its history
of prompts across conversations; another conversation's file; running processes' files (`sessions/`, `session-env/`,
`shell-snapshots/`); its plugins, skills, caches, telemetry, policy and usage files. A name the table does not hold is not copied.
A name holding `..`, a link or a path leaving the home is refused, the carry falling back (§2.5).

**How it lands.** The old part has ended first and its process stopped (D51, `HandOver.cs:254`, `LimitMove.cs:133`), so the files
are no longer written. Each file is copied to a name of its own beside its destination and then renamed into place, so a reader
never sees half a file. A destination file of the same name is replaced only where Daoris's own record names that id as this
line's (a line moved back to an account it ran on before, whose copy there is older); a same-named file no record of this line
names is left as it is and the carry falls back. The source is **copied, never moved or removed**: the left conversation stays in
its account's home (D189 point 6), and the line's next part owns the copy. A copy left by a carry that fell back stays where it
landed, inside that account's home, and goes with the home when the account is removed (D66 §3); nothing else removes it.
`ToolHomeLeftovers` lists only the person's own tool home (`Platform/ToolHomeLeftovers.cs:25-27`), which a carry never touches.

**Reasons, against §1.6's.** It does act inside two homes, but only on the line's own conversation, never on an account's
credentials, identity or another conversation, and both homes are Daoris's own per-account homes on one machine (D66 §3 keeps the
person's own tool home out; this design never touches it). It does depend on where the harness files a conversation, which is why
the table is versioned and read per pin, and every failure of it falls back to the brief in the same start (§2.5). It does not
cross makers, which is why the other half is the brief.

### 2.3 The ids

- **The record.** A carried move is still a **new part**: a new record on the new account, `from` the old with the move's why
  (`curve`, `limit`, `press`), since a record names one account (D131 point 2) and one conversation (D167 point 1). The entry is
  unchanged, `session:<first>/<repository>`.
- **The conversation.** The new record's `harness.json` is written with the **old record's conversation id** before its start, as
  `HarnessConversation(adapter, id)`. The start then takes the existing resume, `Acp.ResumeAsync(cwd, id)` on the new account's
  home, judged by `Continuations.Judge` as any resume is: same adapter, the record's own account, the tree, the kept id, a door that
  resumes (`Continuation.cs:207-239`), all of which now hold. So two records name the same conversation id, each in its own home;
  the old one is left (D189 point 6) and only the newest part's is ever resumed.
- **Where the agent names another id** on the resume (the wire or the native `init` line names a different conversation),
  `HarnessConversations.Keep` replaces it as today, and the move's log line says so: the carry is then **not** counted resumed.
- **How it was handed** is a third value beside `transcript` and `brief` (`HelpConversations.cs:118`, `:124`): `own`, its own
  conversation carried. Ask Daoris's fold follows `own` as it follows `brief` (§2.6).
- **The machine log** writes `session.moved … carried=own` or `carried=brief why=<reason>` on the existing line
  (`Continuation.cs:588-590`), and one line of its own, `conversation.carried`, with the agent, its version, the two accounts,
  the files' count and bytes and the outcome: `resumed`, `refused <why>` or `not-found`.

### 2.4 The move, step by step

1. The turn ends; the move is decided as today (`SwitchDecision`, the limit's walk, the person's press).
2. The brief is composed as today (`HandOver.cs:250`, `LimitMove.cs:127-128`, `Driver.cs:2521-2525`), so it is ready if the
   resume fails. It is kept with the move, not handed yet.
3. The old part ends and its process stops, as today.
4. The carry: the table's files copied (§2.2), into the new account's home.
5. The new part opens on the new account, `from` the old, its `harness.json` holding the carried id, `handed: own`.
6. It resumes its own conversation, and is handed only what a resume is handed today: the words waiting, the refused turns'
   words where a limit moved it (`LimitHeld`, each once, as `LimitMove.cs:156-166` orders them), and one line of Daoris's saying the
   account changed. **Not the brief**: the agent has its own turns.
7. The lines are written: the old part's last line, the new part's first (§2.7).

The box D206 gives a move of an ended record (`docs/2026-10-23-limit-resume-design.md:395-406`) carries step 2's brief and step 4's
outcome, so a shell that ends between step 4 and step 6 comes up and goes on from the box: a resume where the carry landed, the
brief where it did not.

### 2.5 When the resume fails: the brief, in the same start, said

A carry or resume that does not hold falls back **in the same start** to today's move, never to a wait and never to the person:

- the table names no file for the agent, or the file is not there (`not-found`);
- a copy fails, or a name is refused (§2.2);
- the protocol door's resume is refused: `resource_not_found` → `gone` (`Acp.cs:3245`), or any other refusal on the wire;
- the pipe door's harness ends before it names its conversation, or names another one (§3.3).

The fallback hands the new part the brief kept at step 2, then the same words step 6 would have, as `LimitMove` and
`HandOverTake` do today, and marks it `handed: brief`. Where the protocol door refused the resume, the same record goes on with a
fresh conversation on the same process (`session/new`), so no second record is opened for one move. The new part's first line
says the carry failed and why (`moved.carry-failed`, §2.7); the log line says `refused <why>`. The fallback is never tried again
by spending a turn: the next move tries the carry afresh.

### 2.6 Another agent: the brief, as today

A hand-over to another agent (D163), LIMITGOON2's go-on with its actor's next choice (D205's (e)) and a pick of another agent
carry nothing of the agent's own: a Claude transcript is not a Codex thread, and writing one maker's store for another is what
D189 rejected and this design keeps rejected. The line goes on from the brief, as `HandOverTake` and `LimitMove.ElsewhereAsync`
already hand it. **What changes is only how it reads**: one conversation across its parts (§2.7).

### 2.7 On the page: one conversation, either half

- **A repository's chat draws its line, not its part.** The conversation pane draws every earlier part's conversation above the
  attended part's, oldest first, read-only, as Ask Daoris's already does (`help/AskConversation.tsx:870-876`): `WorkFrame` maps
  the entry's parts before the attended `SessionConversation` (`work/WorkFrame.tsx:2456-2457`), each earlier one `live={false}`.
  The *Parts* strip stays, and pressing a part scrolls to it rather than replacing the view. A driven session's line shows the
  same. A teammate's record shows only what it shows today (D47 §4).
- **Between two parts, the move is said once**, by the lines that are already there: the old part's last and the new part's first.
  No separator of the page's own, no *new session*, no greeting drawn as a start. A carried part draws no brief, since none was
  handed; a briefed part's brief stays folded as today.
- **Ask Daoris's history** folds a part `handed: own` into its line as it folds `brief` (`HelpConversations.cs:233-249`).
- **The words.** Moving and resuming are said in the session's words: a part, never a new session.

| Code | en | zh |
|---|---|---|
| `note.moved.carried` (old part, last line) | *Went on as part {{part}} with {{agent}} as `{{account}}`, because {{why}}: its own conversation went with it.* | *由 {{agent}}（账户 `{{account}}`）以第 {{part}} 段继续，因为{{why}}：它自己的对话随之继续。* |
| `note.moved.carried-from` (new part, first line) | *Continues part {{part}}, which ran as `{{account}}`, because {{why}}, in its own conversation.* | *接续第 {{part}} 段（原账户 `{{account}}`），因为{{why}}，在它自己的对话中继续。* |
| `note.moved.carry-failed` (new part, first line) | *Its own conversation could not go on as `{{account}}` ({{why}}), so it continues part {{part}} from Daoris's brief of it, {{count}} characters.* | *它自己的对话无法在账户 `{{account}}` 上继续（{{why}}），因此凭 Daoris 对它的简报（{{count}} 个字符）接续第 {{part}} 段。* |
| `note.moved.on`, `note.moved.from` (reworded) | *Went on as part {{part}} …* / *Continues part {{part}} …*, the rest as today | *以第 {{part}} 段继续……* / *接续第 {{part}} 段……* |
| `note.limit.resume-refused` (reworded) | *… so it went on as a new part with Daoris's brief.* | *……因此它带着 Daoris 的简报以新的一段继续。* |
| `work.parts.began.carried` | *went on from {{after}} in its own conversation* | *接{{after}}，在它自己的对话中继续* |

`chat.moved` and `chat.moved-from` keep their words and gain nothing: the curve's reason is theirs, the carry's outcome the
`moved.*` line beside it. Where a sentence names a quest or an ask, zh says 委托 and 需求; a *Try again* is 重试 (the glossary's
rows). The glossary's *part* (段) and *conversation* (对话) are the words; `i18n:check` and `names:check -- --strict` hold them.

## 3. What is unmeasured, and how it is measured

### 3.1 Unmeasured

- Whether Claude Code, on the pinned claude-code-acp, resumes a conversation whose two entries (`<id>.jsonl`, `<id>/`) were
  copied from another account's home: whether it reads anything else in the home for it, and whether a record inside the file
  names the first home (a persisted tool output's path does, as this session's own transcript shows) in a way that matters.
- Whether the resumed agent names the same id or forks another.
- Codex's file's name, and whether codex-acp's resume finds a file it did not write in that home.
- The pipe door's `--resume` of an id that is not there (LIMITRESUME1f's question, §3.3).

### 3.2 Measured on the install's own next real move, never by spending turns

No turn is spent to test it, and the account-switch design's two-turn evidence row is withdrawn. The build lands with the
fallback (§2.5), so the first real carried move on the install **is** the measurement: it either resumes or falls back, in the
same start, with nothing lost. The `conversation.carried` log line holds the outcome; the parent reads it from the machine log
after the install's next carried move and writes it as a dated note on D165, naming the agent's version, the outcome and the
part. Until that note, the carry's resumes are said as a resume, never claimed as measured, and the design's claim stays
*unmeasured*. A version of the agent that changes where it files a conversation shows as `not-found` and falls back; the table's
row is then read again on that version.

### 3.3 The pipe door waits for LIMITRESUME1f

The pipe door puts the person's words into `--resume`'s own arguments or stdin before the harness names its conversation
(`Adapters.cs:2219-2223`; `docs/2026-10-23-limit-resume-design.md:407-418`), so a resume that silently opened a fresh conversation would send them to an agent without
context. LIMITRESUME1f records on the real door what `--resume` of a missing id does. Where it fails closed, the pipe door carries,
an end before `init` read as the carry's failure and the brief taken; where it opens a fresh conversation, the pipe door never
carries and moves by the brief. Until 1f lands it moves by the brief.

## 4. What it amends, each by a dated note (proposed)

- **D125**, its contract's §3.5 point 5 and its point 4 and its rejection of copying between account homes: the line's own
  conversation, and only it, crosses to the next account's home of the same agent; the credentials and everything else of an
  account's own never do.
- **D131** point 2: *the same account* is the account whose home holds the conversation, which a carry makes the new one.
- **D165** point 3 and its design §4: a move over the curve with the agent unchanged carries the conversation; the evidence row is
  withdrawn, the first real move measures it.
- **D167**: the page draws a line's earlier parts above the attended one, and a part may begin `handed: own`.
- **D189** points 1, 5 and 6 and its rejection of copying a store across accounts: the same agent's own files may be copied whole
  between Daoris's own homes at a move, never parsed or written; across makers it stays rejected.
- **D197**'s LIMITGOON1 note: the carrier is the conversation's own, where it carries; the brief otherwise.
- **D206**'s rejection of copying the transcript, and its §3.5 briefed move to another account: carried where §2.1 holds.

**Kept**: D163 point 1 and its rejection of carrying the transcript, for another agent; D205's (e); D49 §4 and D66 §3 (no account's
credentials, and never the person's own tool home); D189 point 4's brief, now the fallback for the first half.

## 5. Rejected

- **Spending two short turns to measure first** (the account-switch design's row): the fallback makes the real move the measurement
  at no cost, and the owner's direction is not to spend turns on it.
- **Moving the files** rather than copying them: it takes the left conversation out of its account's home (D189 point 6) and loses
  it where the resume fails.
- **Copying the whole home, or the account's configuration**: it carries credentials and another account's own.
- **Sharing one home between two accounts**, or a link from one to the other: the accounts are separate homes by design (D66 §3,
  D125) and a link would carry everything.
- **Handing the brief as well as the carried conversation**: the agent has its own turns, and the brief repeats them at up to
  40,000 characters.
- **Writing a Claude transcript for Codex, or a Codex thread for Claude**: it parses and writes a store across makers (D189, kept).
- **Waiting for the person when the resume fails**: the brief is complete enough for a go-on (D189 point 3), so the line goes on.
- **A separator of the page's own between parts**: the move's two lines already say it, in the session's words.

## 6. Rows, in build order, with their proofs

Stand-ins are scripts the tests write under their own fixture home, as `ChatGoesOnProcessTests.Agent` does
(`src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Conversations/ChatGoesOnProcessTests.cs:748`): never a real agent, never a window,
never a real console.

| Row | What | After | Proof |
|---|---|---|---|
| SWITCHCTX1a | The carry (§2.2-§2.5) for a conversation on the protocol door: the agent's table, the allow-listed byte copy with its refusals, the new record's `harness.json` holding the carried id, `handed: own`, the resume through the judge, the fallback to the kept brief in the same start, `conversation.carried` and `session.moved carried=` | — | A stand-in ACP agent keeping each conversation as `projects/<Flatten(tree)>/<id>.jsonl` under its `CLAUDE_CONFIG_DIR`, answering `session/resume` from that file and `resource_not_found` where it is missing, and answering a prompt with the turns it holds: **a stand-in's conversation moved to another account's home resumes with its earlier turns** (the prompt's answer names them; the new part's `harness.json` holds the same id; no brief handed). **A resume that fails falls back to the brief and says so**: the stand-in refusing `resource_not_found` → the brief handed, `handed: brief`, `moved.carry-failed` on the new part, the waiting words delivered once. The fixture home's `.credentials.json`, `.claude.json`, `history.jsonl`, another conversation's file and `sessions/` are not in the destination; a name with `..` refused and fallen back |
| SWITCHCTX1b | Every move of §2.1 takes it: `CurveMove` through `HandOverTake` with the agent unchanged, `LimitMove` live and ended within the agent, D206's briefed move to an account that frees first, the person's hand-over to another account of the agent, and a quest's carry-on why `curve` or `limit` (`Driver.ContinuesFrom`) | 1a | The same stand-in per path: a curve move, a limit move with held words (each once, after the resume, no brief), an ended limit-held record, a person's press, and a driven carry-on resuming its own conversation; another agent on each path still briefed; a shell ended between the carry and the resume → the come-up resumes from the box |
| SWITCHCTX1c | The page (§2.7): a repository's chat and a driven line draw their earlier parts above the attended one; the Parts strip scrolls; Ask Daoris folds `own`; the coded lines and the rewording in en and zh | 1a | Page tests over fixture events: **a line handed to another agent shows one conversation across its parts** (its earlier part's turns above, `chat.handed-over` and `chat.taken-over` between, no *new session*), a carried line the same with `moved.carried`, a failed carry with `moved.carry-failed`; `HelpConversations` folding `own`; `i18n:check`, `names:check -- --strict`, `note.test.ts` |
| SWITCHCTX1d | Codex's row of the table, read from a Codex home that holds a session | a Codex account's session on the install | the row's names read on the pinned codex-acp; a stand-in filing by date resumed after a carry; until then Codex briefed (a test holds it) |
| SWITCHCTX1e | The pipe door carries (§3.3) | LIMITRESUME1f | the stand-in's reaction to 1f's recorded shape: an end before `init` → the brief, said; a different id named → the brief and the log's mismatch |
| SWITCHCTX1f | The measurement (§3.2): the install's first real carried move read from the machine log and written as D165's note | 1a-1c built and installed | the parent's install proof: the outcome, the agent's version, the part, and the window showing the line as one conversation |

## 7. What this design's gate does not cover

No real agent has resumed a carried conversation; §3.1 lists what is not known, and §3.2 says how it becomes known. The page's
change has not been seen in the window. Nothing here reads, parses or writes inside a conversation's file, and nothing here touches
the person's own tool home.
