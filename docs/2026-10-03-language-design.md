# Languages — design

> LANG1, from the owner, 2026-10-03, seeing a page-built preface in English: *"we should be building a multi language
> support"*; then, on where a session's language is set: *"so the UI language and session language should be able to
> set differently this is more like system level or usage level"* and *"because session itself is bind to the work and
> the ui is bind to the user I think you got this in reverse?"*. The decision is **D142**. Status: **designed; nothing
> built.** Read with `.claude/knowledge/translation-parity.md`, `.claude/knowledge/twins.md`, D116 (a name is designed in
> each language), the platform language §4 (*Language*), the frontend architecture §3 and §4a, D126's verdicts by code and
> D137's reasons by code (MSG1f).

## 1. What was there

Read from the code at `122db847` (the integration branch carrying MSG1b–MSG1f) and from the owner's install:

- **Every session record's note opens with a line the driver wrote in English.** All 70 on the install do: *the quest
  reached done.*, *exited without touching its quest.*, *It stopped with its quest still taken, to ask you:* and the
  agent's own question. The 中文 window shows them as written: `SessionHead.tsx` (`Said`, the parked card, an intake's),
  `QuestPage.tsx`'s session rows, `AskPage.tsx`'s intake, the attention band (`work/attention.ts`) and the timeline.
- **A few of the driver's sentences are already worded by code**: D126's verdicts (`signals.ts`, `work.sitting.*`), MSG1f's
  reasons (`work/say.ts`, `work.say.why.*`), the modules' refusals (`Refusals`, `errors.*`) and an account's cool-off
  (`harness.cooling.why.*`). Everywhere else *the driver's words stand* (platform language §4).
- **A note is a stack, written by two artefacts.** The driver writes a conclusion and, going on, prefixes the record's
  earlier note (`Continuations.CannotNote`, `WentNote`, `EndedNote`) or appends an account's line (`AccountLimited`,
  `AccountRefused`). The service appends *Answered:* and the person's words (`SessionLedger.AnsweredNote`) and *Went on
  with your words at … UTC.* (`GoOnAsync`).
- **The note travels and is read as English**: to a teammate through the remote, cleaned by
  `SessionNote.ForAnotherMachine`; into the next session's prompt (*Its record reads: …*, `TargetPrompt.Before`); into Ask
  Daoris's where-preface (`help/where.ts`); onto the terminal (`daoris-driver sessions`); and, byte for byte, into the
  family rehearsal (section 4's *Answered:* notes) and the deployment rehearsal (`CLOSED_NOTE`, `SWEPT_NOTE`).
- **Everything Daoris writes to an agent is English**: the claiming, resuming and carrying-on instructions
  (`TargetPrompt`), the intake's (`IntakePrompt`), a resumed conversation's opening (`Continuations.Opening`), Ask Daoris's
  room and where-preface, and MSG1f's preface for *Start a conversation with these words* (`bridge/conversation.ts`), which
  the chat keeps as a note event (*told where the person is: …*). **Nothing tells a session which language to write in.**
- **The window's language is its viewer's** (`daoris.language` in the page's store; D66; D110 §4), chosen in Settings →
  Appearance.

## 2. Whose words a text is

| Text | Whose | Shown |
|---|---|---|
| A line Daoris writes about a session: how it ended, how it started, where words went | **Daoris's: chrome** | Worded by the page from a **code and its values**, in the reader's language, from both catalogues |
| An agent's question, last words, closing note or a turn's failure in its own words | **The agent's: content** | As written, never in a catalogue |
| The person's answer, a stop's or a decline's reason, an ask's closing note | **The person's: content** | As written |
| A quest's or an ask's title and body | **Its author's: content** | As written (D42) |
| An exception's message, git's words, a plugin's | **A program's, passed through** | As written, as `DRIVER_REFUSED` passes the driver's sentence (frontend architecture §4a): re-authoring each in two languages is two copies to drift |

**A note that is both is split.** *It stopped with its quest still taken, to ask you:* is Daoris's lead-in, a code; the
question beneath it is the agent's words, a part of its own. The page words the first and shows the second.

## 3. The record's shape

**`noteParts` beside `note`.** An ordered list, one entry per line:

```json
[
  { "code": "ended.parked-asked", "values": {}, "text": "It stopped with its quest still taken, to ask you:" },
  { "words": "Which branch should the release land on?", "by": "agent" },
  { "code": "ledger.answered", "values": {}, "text": "Answered:" },
  { "words": "main", "by": "person" }
]
```

- **A coded part** has its `code`, its `values` and `text`, that part's English as its writer wrote it, so a page that does
  not know the code shows the part rather than nothing. **A words part** has the words and `by`: `agent`, `person`,
  `program`, or `before` (an English note from before parts, carried whole).
- **Values are facts, never sentences**: quest, ask and session ids, a list of quest ids, an exit code, a count of minutes,
  an adapter's name, a branch, an ISO 8601 UTC moment the page formats in the reader's language and zone, a reason's code
  (MSG1f's `ContinueWhy`) with that reason's own values. **Never a path and never an account's name**: the note travels
  (D125 §3.6), so a line says *the account it ran on*, as the cool-off's line already does.
- **`note` stays, byte for byte.** Each writer keeps composing its English exactly as today, from the same values at the
  same site, and every coded part's `text` is a substring of the note it built (a test per site). The English is the
  terminal's, the driver loop's lines', the next prompt's, an older page's and an older host's, and the rehearsals'.
  **LANG1a changes no English.**
- **Adding to a note** reads the record's parts and adds its own after them. A record with a note and no parts is carried as
  one `before` part. **A move that writes a note without parts clears the parts**, so an older driver's or a rehearsal's
  note never sits beside stale parts; a move that writes neither keeps both, as the store keeps a note today.
- **The store**: `sessions.note_parts` (JSON text), null for a record from before.
- **The routes**: `POST /api/sessions/{id}/state` and the record's create door take `noteParts` beside `note`;
  `GET /api/sessions` and a record's route answer them. An older host ignores the field and keeps the English; an older
  driver sends none. **The remote feed** carries them, every string through `SessionNote.ForAnotherMachine`. **The
  modules** carry them wherever they carry `note`: `ServiceClient`'s session view, `SESSION_GROUPS`, the work routes, and a
  stop request in `<home>/sessions/requests/` (a pause's or an abandon's line, SESSUX1g).
- **What stays English**: the terminal (§8); the driver loop's lines and every notice (an OS notification, the headless
  host's attention line), as D126 SESSUX1i keeps them; the machine log, which holds no note (D94); and everything handed to
  an agent (§7).

## 4. Every line

Read from every note site in the driver, the modules and the service: **72 English lines** (templates and their variants)
at about 60 sites in 15 files, **66 codes**, and two kinds of words with no code. The page's key is `note.<code>` in a
new area, `note.json`, unless the row names another. The English column is the page's, a sentence designed in its own
right (D116); the record's English stays as written. Ids render as the page draws them; 中文 sets them, and numbers, apart.

**A session's end** (`Observation.cs`; `Driver.cs`, `Driver.Continue.cs` and `Driver.Intake.cs` for 18–21):

| # | Code | Values | English | 中文 |
|---|---|---|---|---|
| 1 | `ended.awaits` | awaits | Asked #{{awaits}} of another repository and waits for its answer; the quest goes on in the same tree once it is answered. | 已向另一个仓库提出委托 #{{awaits}}，等待答复；答复后委托在同一工作树中继续。 |
| 2 | `ended.exit` | exit | It exited with code {{exit}}. *(after 1, 10, 11, 42 and 43, where the exit was not 0)* | 进程退出码为 {{exit}}。 |
| 3 | `ended.turn-failed-taken` | + agent's words | The agent's turn failed with the quest still taken: | 智能体这一轮失败，委托仍已接下： |
| 4 | `ended.turn-failed-open` | + agent's words | The agent's turn failed before it took its quest: | 智能体这一轮在接下委托前失败： |
| 5 | `ended.parked-asked` | + agent's words | It stopped with its quest still taken, to ask you: | 它停了下来，委托仍已接下，想问你： |
| 6 | `ended.parked-transcript` | — | It stopped with its quest still taken, to ask you; what it needs ends its transcript. | 它停了下来，委托仍已接下，想问你；它需要什么写在记录末尾。 |
| 7 | `ended.parked-short` | — | It stopped to ask you. | 它停下来问你。 |
| 8 | `ended.answered-unfinished` | awaits, exit | It went on with #{{awaits}} answered, and ended with the quest still taken (exit {{exit}}). | #{{awaits}} 答复后它继续工作，结束时委托仍已接下（退出码 {{exit}}）。 |
| 9 | `ended.carried-unfinished` | exit | It carried the quest on, and ended with it still taken (exit {{exit}}). | 它接续了委托，结束时委托仍已接下（退出码 {{exit}}）。 |
| 10 | `ended.done` | — | The quest reached done. | 委托已完成。 |
| 11 | `ended.declined` | — | The session declined; its reason is on the quest. | 会话已谢绝，理由写在委托上。 |
| 12 | `ended.stood-down` | — | It exited cleanly with the quest taken: someone else has it. | 它正常退出，委托已被别人接下。 |
| 13 | `ended.taken-exit` | exit | Exit {{exit}} with the quest still taken. | 退出码 {{exit}}，委托仍已接下。 |
| 14 | `ended.untouched` | — | It exited without touching its quest. | 它退出了，没有动它的委托。 |
| 15 | `ended.untouched-exit` | exit | Exit {{exit}} before taking its quest. | 退出码 {{exit}}，未接下委托。 |
| 16 | `ended.went-on` | — | It went on with your words and ended; its quest stays as it closed. | 它带着你的话继续并已结束；委托保持关闭时的状态。 |
| 17 | `ended.went-on-exit` | exit | It went on with your words and exited {{exit}}; its quest stays as it closed. | 它带着你的话继续，退出码 {{exit}}；委托保持关闭时的状态。 |
| 18 | `ended.timeout` | minutes | Timed out after {{minutes}} minutes and was killed. | 运行超过 {{minutes}} 分钟，已被终止。 |
| 19 | `ended.stopped` | — | The person stopped it. | 已被手动停止。 |
| 20 | `ended.lost-claim` | — | Stopped by this machine's driver: another machine's take on the quest reached the remote first, so this session's work would double someone else's. | 被本机驱动停止：另一台机器先在远端接下了这个委托，这个会话的工作会与之重复。 |
| 21 | `ended.driver-closed` | — | The driver was stopped while this ran; the session's process was ended with it. | 运行期间驱动被停止，会话进程随之结束。 |
| — | *no code* | a program's words | An exception's message, as today's note is (five sites, `ChatRunner.cs` among them) | 按原文 |

**An account's line**, appended to a failure (`Driver.cs`, `AccountCooling.cs`):

| # | Code | Values | English | 中文 |
|---|---|---|---|---|
| 22 | `account.cooling` | until, why | The account it ran on is cooling until {{until}} ({{why}}); nothing starts on it until then. *`why` is `harness.cooling.why.*`'s four* | 它运行时用的账户冷却至 {{until}}（{{why}}），在此之前不会在它上面启动任何会话。 |
| 23 | `account.refused` | owner | Its provider refused the {{owner}} account it ran on (401). Replace the key or sign in again, in Settings or with `daoris agent`, and Daoris starts sessions on it again. | 服务方拒绝了它运行时用的 {{owner}} 账户（401）。请在设置中或用 `daoris agent` 更换密钥或重新登录，Daoris 随后会再在它上面启动会话。 |
| 24 | `account.refused-own` | owner | The same, for {{owner}}'s own sign-in. *(Today's English names the account, and the cleaner cuts it when it travels; the values never carry it)* | 同上，针对 {{owner}} 自身的登录。 |

**While it starts and works**, replaced by its end (D51 rule 4; `SessionTrees.cs`, `Driver.cs`, `Continuation.cs`):

| # | Code | Values | English | 中文 |
|---|---|---|---|---|
| 25 | `started.tree` | branch, basedOn | Opened a session tree on {{branch}}, from {{basedOn}}. A fresh tree holds nothing git does not track: no installed dependencies, no build outputs. | 已从 {{basedOn}} 在 {{branch}} 上打开会话工作树。新工作树只含 git 跟踪的内容：没有安装的依赖，也没有构建产物。 |
| 26 | `started.tree-unrecorded` | + a program's words | Daoris could not record where its branch started, so bringing it up to date will cut where its work first differs from the line: | Daoris 无法记录它的分支从哪里开始，更新到最新时会从它与主线首次不同处切开： |
| 27 | `started.resumes-answered` | quest, answered | Resumes #{{quest}} now that #{{answered}} is answered. | #{{answered}} 已答复，继续 #{{quest}}。 |
| 28 | `started.in-asking-tree` | session | In the tree session {{session}} asked from. | 在会话 {{session}} 提问时所在的工作树中。 |
| 29 | `started.words-open` | quest, session | Starts #{{quest}} with your words to session {{session}}. | 带着你对会话 {{session}} 说的话开始 #{{quest}}。 |
| 30 | `started.words-taken` | quest, session | Carries #{{quest}} on with your words to session {{session}}. | 带着你对会话 {{session}} 说的话接续 #{{quest}}。 |
| 31 | `started.answer` | quest, session | Carries #{{quest}} on with your answer to session {{session}}. | 带着你给会话 {{session}} 的答复接续 #{{quest}}。 |
| 32 | `started.released` | quest, session | Carries #{{quest}} on: you stopped session {{session}}, and released it. | 接续 #{{quest}}：你停止了会话 {{session}}，之后放开了它。 |
| 33 | `started.cut-off` | quest, session | Carries #{{quest}} on after session {{session}} was cut off. | 会话 {{session}} 中断后，接续 #{{quest}}。 |
| 34 | `started.other-account` | — | It runs on another account. | 它改用另一个账户运行。 |
| 35 | `started.same-tree` | — | In the tree it worked in. | 在它原来的工作树中。 |
| 36 | `started.fell-back` | why (+ its values) | A new session, because {{why}}. | 改为新会话，因为{{why}}。 |
| 37 | `working.resumes-answer` | — | Resumes its own conversation with your answer, in the tree it worked in. | 带着你的答复接续它自己的对话，在它原来的工作树中。 |
| 38 | `working.goes-on` | — | It goes on with your words in its own conversation, in the tree it worked in. | 它带着你的话在自己的对话中继续，在它原来的工作树中。 |

**Where the person's words went** (`Continuation.cs`), after the record's earlier parts; `{{why}}`, here and in 36, is
`work.say.why.<why>` worded with the reason's own values, MSG1f's one wording per reason:

| # | Code | Values | English | 中文 |
|---|---|---|---|---|
| 39 | `went.cannot` | why (+) | *key `work.say.cannot`, reused:* It cannot go on in this session, because {{why}}. | 它无法在这个会话里继续，因为{{why}}。 |
| 40 | `went.new-session` | why (+) | Your words went to a new session, because {{why}}. | 你的话转到了新会话，因为{{why}}。 |
| 41 | `went.carried-on` | why (+) | Carried on in a new session, because {{why}}. | 已在新会话中接续，因为{{why}}。 |

**An intake** (`Intake.cs`; the person's ending, 48–50, `Driver.Intake.cs`):

| # | Code | Values | English | 中文 |
|---|---|---|---|---|
| 42 | `intake.ask-deleted` | — | The ask it answered was deleted while it ran. | 它答复的需求在运行期间被删除。 |
| 43 | `intake.published` | quests, ask | Published {{quests}} onto ask #{{ask}}. | 已在需求 #{{ask}} 下发布 {{quests}}。 |
| 44 | `intake.ask-closed` | ask, + the person's words | Ask #{{ask}} was closed while it ran: | 需求 #{{ask}} 在运行期间被关闭： |
| 45 | `intake.ask-answered` | ask, quests | Ask #{{ask}} was answered while it ran: it became {{quests}}. | 需求 #{{ask}} 在运行期间已被答复，成为 {{quests}}。 |
| 46 | `intake.turn-failed` | ask, + agent's words | The agent's turn failed before publishing anything onto ask #{{ask}}: | 智能体这一轮在需求 #{{ask}} 下发布任何内容前失败： |
| 47 | `intake.asks` | ask | It published nothing: the declarations did not settle ask #{{ask}}, so it asks you rather than guess; its question ends its transcript. `daoris-driver ask --publish {{ask}} --to <repository>` answers it; `daoris-driver ask --close {{ask}} --reason "…"` ends it. | 它什么也没发布：声明无法确定需求 #{{ask}}，所以它不去猜，而是问你；它的问题在记录末尾。`daoris-driver ask --publish {{ask}} --to <repository>` 答复它；`daoris-driver ask --close {{ask}} --reason "…"` 结束它。 |
| 48 | `intake.exit` | ask, exit | Exit {{exit}} before publishing anything onto ask #{{ask}}. | 退出码 {{exit}}，未在需求 #{{ask}} 下发布任何内容。 |
| 49 | `intake.person-answered` | ask, quests | The person answered ask #{{ask}}: it became {{quests}}. | 需求 #{{ask}} 已由人答复，成为 {{quests}}。 |
| 50 | `intake.person-closed` | ask, + the person's words | The person closed ask #{{ask}}: | 需求 #{{ask}} 已由人关闭： |
| 51 | `intake.person-deleted` | ask | The person deleted ask #{{ask}}, so there is nothing left for it to wait on. | 需求 #{{ask}} 已被删除，它没有可等的了。 |

**A stop** (`PausedWork.cs`, `WorkAbandoning.cs`, `SessionMoves.cs`, `Orphans.cs`, `ChatRunner.cs`):

| # | Code | Values | English | 中文 |
|---|---|---|---|---|
| 52 | `stopped.paused-ask` | ask | Paused with ask #{{ask}}. | 随需求 #{{ask}} 暂缓。 |
| 53 | `stopped.paused-quest` | quest | Paused with quest #{{quest}}. | 随委托 #{{quest}} 暂缓。 |
| 54 | `stopped.abandoned-ask` | ask | Ask #{{ask}} abandoned. | 需求 #{{ask}} 已放弃。 |
| 55 | `stopped.abandoned-quest` | quest | Quest #{{quest}} abandoned. | 委托 #{{quest}} 已放弃。 |
| 56 | `stopped.checkpoint-finished` | — | The person finished this at a checkpoint. | 已在检查点手动完成。 |
| 57 | `stopped.checkpoint-stopped` | — | The person stopped this at a checkpoint. | 已在检查点手动停止。 |
| 58 | `stopped.checkpoint-moved` | — | The person moved this at a checkpoint. | 已在检查点手动移动。 |
| 59 | `stopped.orphan` | — | Nothing on this machine was running it any more: its process ended with the application that started it, and the record had not been told. | 本机已没有任何东西在运行它：进程随启动它的应用一起结束，而记录没有被告知。 |
| 60 | `chat.closed` | — | The application closed while this conversation ran; its process was ended with it. | 对话进行中应用被关闭，进程随之结束。 |
| 61 | `chat.ended-by-person` | — | The person ended the conversation. | 对话已被手动结束。 |
| 62 | `chat.ended` | — | The conversation ended; its commits are its record. | 对话已结束；它的提交就是它的记录。 |
| 63 | `chat.cancelled` | — | The request that started it was cancelled before its process started. | 启动它的请求在进程启动前被取消。 |
| — | *no code* | the person's words | A finish, decline or stop with the person's words (`RESOLVE_SESSION`; `sessions finish --note`, `sessions decline --reason`), as today | 按原文 |

**The service's own lines** (`SessionLedger.cs`):

| # | Code | Values | English | 中文 |
|---|---|---|---|---|
| 64 | `ledger.answered` | + the person's words | Answered: | 已答复： |
| 65 | `ledger.parked` | — | It stopped to ask the person; its question is in its transcript. | 它停下来问人；问题在它的记录中。 |
| 66 | `ledger.went-on` | at | Went on with your words at {{at}}. | 于 {{at}} 带着你的话继续。 |

A list of quests is joined in the reader's language (`Intl.ListFormat`), a moment by `format.ts`, a count in its number
with i18next's `count`. The 中文 column is a proposal: LANG1a holds it to `names:check --all`, which holds a sentence to
the words a term must not be called.

## 5. One list, held both ways

The codes are a twin (`.claude/knowledge/twins.md`): two writers and one reader agree on them with no shared code.

- **Each writer declares its codes once**, each with its value names: the driver's `NoteCodes` (`Daoris.Driver`), the
  service's `LedgerNoteCodes` (`Daoris.Knowledge`). A site writes a part only through its code.
- **Each writer's test reads both catalogues**, as `RefusalCatalogueTests` does: every code has its key in `en` and `zh`;
  every placeholder an entry has is one of its code's values; and every value a code declares appears in its entry, a
  reason's values in that reason's entry (a dropped value renders the fixed part and silently loses the fact,
  translation-parity). **So a new driver line fails the driver's test until both languages word it.**
- **The page's test parses both writers' declarations** (as the CLI's twin tests parse the driver's theories) and holds
  them to `note.ts`'s map, code for code with the same values, and every `note.*` key to a declared code, so a retired
  code leaves nothing behind. The web's parity gate already holds `en` and `zh` to one key set.
- **What a page shows for a part it cannot word** (a code it does not know, a newer driver than the page, or a value its
  sentence needs that is absent): the part's `text`, **shown as recorded**, marked. A words part is never marked: it is
  always shown as written.

## 6. Records from before

A record with no `noteParts` shows its `note` **as kept, marked *shown as recorded* / 按原文显示**: all 70 on the install,
and a teammate's record from an older driver. **No pattern table re-reads the English**: it would be a third copy of every
sentence, drifting with the driver's wording and silently misreading a variant, which the platform language already
forbids (*never by matching the English*). **No migration rewrites the store**: that is the same re-reading, made
permanent.

## 7. What Daoris writes to an agent, and the session language

**The instructions stay one copy, in English**, plus **one line naming the session language** where one is set.

- **Why one copy.** The instructions are a contract (translation-parity's third row): their wording was tuned against
  failures (STANDDOWN2's park, D79's wait, the go-aheads) and a translated copy is a second instruction kept by whoever
  translated it, drifting where its maintainer does not read. **The driver's checks never read the agent's prose**: a
  record moves on the exit code and the quest (D46 §4); `Observation.Refused` and the account limits read the harness's own
  output; `ParkedWords` quotes the last words and parses nothing. So a line asking for another language changes no check.
- **The line**, handed only where one is set: *Write what you say to a person in Simplified Chinese (简体中文): a question
  to them, a quest's closing note or a decline's reason, and your last words when you stop. Keep code, commands,
  identifiers, file names and anything you quote exactly as written.* It goes beside the close instruction, which it
  governs, in the claiming, resuming and carrying-on instructions and the intake's, and again in a resumed conversation's
  opening, since the setting may have changed since that conversation was handed it.
- **Not handed** to a chat or Ask Daoris, where the person writes to the agent directly in the language they choose; nor
  does it govern the quests a session publishes for another repository, which are written for that repository.
- **The language is the work's** (the owner: the session *"is bind to the work and the ui is bind to the user"*). It is set
  for **a repository** or for **a workspace**, and the repository's wins; neither set is none. An intake, which answers a
  workspace's ask in no repository, takes the workspace's. **There is no machine-wide
  language**, and **the window's language never sets it nor is set by it**: two settings, in two places, Settings →
  Appearance for the viewer and the work's own for the session.
- **Where it is kept**: `driver.json`, as `languages` and `workspaceLanguages`, beside `lines`/`workspaceLines` and
  `landings`/`workspaceLandings`, which resolve the same way, and `standing`, *kept here, never in the repo*. A twin of
  `driverconfig.ts` and `DriverConfig.cs`, each writer keeping the other's sections. The values are a closed table on each
  side, `en` and `zh`, naming each language for the agent; adding a language is a row in both tables, and needs no window
  catalogue, since a session may write in a language the window does not speak.
- **The doors** (D50): `daoris driver language <repo> en|zh|--clear`, with `--workspace <name>` for each repository there
  with none, and `daoris driver list` showing what each repository resolves to and from where; on the screen, a
  repository's page in Repositories beside its standing answer, and Settings → Workspace beside its lines; Ask Daoris's
  `setting` kind takes it (D110), judged by the table and the registry.
- **Today's behaviour holds until it is set.** With nothing set no line is handed and every instruction is byte for byte
  today's, held by the prompt tests; the window's language default is unchanged.

**Where else it could be kept, weighed.** *The repository's manifest* would travel with every clone, and registrations
carry the manifest to teammates (SYNC5), but the manifest has no door but hand-editing (the workspace design §2b refuses
that), a screen's press would write a tracked file in a checkout Daoris drives, and the workspace design rejected tracked
wiring for the fork and mirror case. *The registry* holds membership, has no workspace record, and a workspace record for
one field would be a table, routes, a feed and a rule for two teammates disagreeing. **What does not travel is said**: a
teammate's driver hands its own sessions the line set on that machine. A repository that wants every agent, every clone
and every teammate to write in one language says so in its own instruction file, which needs no Daoris (D48 §2a).

## 8. The terminal

**English, unchanged.** `daoris` has no catalogue by construction (zero runtime dependencies), and the headless host's
console, `daoris-driver sessions` and `daoris driver list` print the record's English. A terminal in 中文 needs a catalogue
in two artefacts and would read these same codes; it is a later question, with no row yet.

## 9. The build

- **LANG1a — the record's half.** `NoteCodes` and `LedgerNoteCodes`; `noteParts` at every site in §4, each site's `note`
  unchanged; the store's column, the routes, the remote feed through the cleaner, the modules' pass-through and the request
  file; every code's entry in `note.json` in both languages (the twins change together); the writers' catalogue tests and
  the page's declaration test. Lanes: the driver, the service, the modules, the web's catalogues.
- **LANG1b — the page's half.** A `Note` molecule, story first, drawing parts (a lead-in and the words beneath it as a
  quote); every place that shows a session's note (§1); the fallbacks and the mark (§5, §6); and Settings → Appearance's
  hint (*what the service says is shown as it said it*), re-read as the platform language asks of a claim. Lane: the web.
- **LANG1c — the session language.** The `driver.json` twin and its language table; the terminal verb, the two screens and
  Ask Daoris's door with `HelpCoverageTests`' row; the line in the instructions and a resumed opening; the glossary term
  *session language* / 会话语言. Lanes: the CLI, the driver, the modules, the web.
- **Later, no row yet**: the conversation's note events (about 22 sites; *went* and *cannot* already carry `why`), the
  notices, the terminal in 中文, and whether Ask Daoris, which is the user's and not a repository's work, answers in the
  window's language and names its places by the window's names (its room would need both names).

## 10. Rejected

- **Translated instruction templates**: §7.
- **A pattern table over the English**, and **migrating old notes**: §6.
- **One structured note in place of the English**: the terminal, the next prompt, older hosts and the rehearsals read it.
- **One part holding the lead-in and the agent's words as a value**: the words would ride through a catalogue entry, and a
  reader could not tell whose they are.
- **A machine-wide session language**, and **the window's language as the session's**: the owner's correction; the reader
  of a window and the work a session does are two things.
- **An ask's or a quest's own language**: a field on the ask's and the quest's wire, store, feed and both composers, for a
  case not yet seen; the repository's override covers a repository that differs. Reopened when one is wanted.

## 11. What the checks do not cover

Documents only; nothing is built. The sites were read at `122db847`, which main does not yet carry. The 中文 wording is a
proposal, not yet held by `names:check`. Whether agents write in the language the line names was not measured: no turn
was run. Nothing looked at the window.
