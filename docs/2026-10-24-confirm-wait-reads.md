# CONFIRMWAIT1: the reads of rounds 1 and 2 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-confirm-wait-design.md` and its D156 note at `c44121e2a`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out, so this round had one reader; Codex's read is owed on the final text. The Opus foundation reader asks another round (25 findings, 7 blocking), finding the direction sound and the reversal of CONFIRMSEEN1 surviving. Round 2 answers it.

## Opus (foundation), round 1

The read, verbatim.

### 1. Claims about the code

Read at `6accf4a01`; nothing the design cites has changed since `aa16658e7`. `Modules/`, `Driver/` and `Host/` are
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, `…Driver/` and `…Driver.Host/`; `Web/` is `src/Daoris.Web/src/`.

Most of §1 holds line by line. These all check out: the two minutes and the ten (`PipeConfirmations.cs:116-133`, `:165-174`, `:191-228`,
`:320-339`), the limits (`InstancePipe.cs:42-46`, `:199`), the journal and `Accept` (`:590-611`, `PipeJournal.cs:22-36`), a waiting press's
absence from the journal (`:604-609`, `:652-655`, `:686-726`), `CancelAsync` and `Result` (`:809-872`), the sixth refused (`:637-651`), the
census rows (`PipeCensus.cs:212-266`, `:273`, `:278-279`), the row, balloon, notice and words (`NeedsAPerson.cs:464-481`,
`AttentionStateSlice.cs:116`, `SessionBalloon.cs:132-134`, `NoticesStateSlice.cs:198-203`, `person.json:28/33/36/38`,
`work.attention.json:112-113`, `notifications.json:39`), the verb (`InstanceDoor.cs:62`, `:234-288`, `:413-472`, `SentPresses.cs:21`, `:52`)
and the close (`InstancePipe.cs:275-279`, `:695-719`, `DriverLoop.cs:1353`). These are wrong:

- **a. "What this is not" (design :17-20).** The steward's appointment is not admitted at `InstancePipe.cs:621-631`. That path is the
  standing confirmation's alone. D209 point 4 gives the steward its own route, `ROLE_ACT`, of class `Role`. It is judged, and refused
  where uncovered, as `SUPERVISOR_ANSWER` is (`InstancePipe.cs:559-571`), and it is never asked. D209 point 2 makes D201's four presses the
  steward's, and the standing confirmation is "not offered as a grant of its own". In this build the standing confirmation admits nothing:
  `OfferedInThisBuild = false` (`StandingConfirmation.cs:193`), and its caller look answers `untold` until D201 part (i) lands (`:160-162`).
  So until STEWARDROLE1d, every development press from a terminal is a card, and under this design none of them expires.
- **b. §4.1, "Ctrl+C still withdraws a waiting press it sent (CANCEL)".** No client sends CANCEL. When the token is cancelled,
  `InstanceClient.RequestAsync` returns null and sends nothing (`Driver/Platform/InstanceClient.cs:134-176`). No `SHENORA.REQUESTS` CANCEL
  is written anywhere in the driver or the CLI. The families' own Ctrl+C sentence says the opposite: *A change already sent to Daoris's
  window may still be confirmed there* (`Host/AgentsConsole.cs:22-27`; also `DriverChoicesConsole.cs:33`, `PluginConsole.cs:25`,
  `ToolsConsole.cs:29`). RPC1h built only the instance's half.
- **c. §4.3 and §10's row 1b name the wrong census.** They say the new verb's census is `cli-usage.test.ts`'s refusals fixture. That test
  belongs to the TypeScript `daoris` dispatcher (`src/Daoris.Cli/test/cli/cli-usage.test.ts:9-16`). `daoris-driver result <id>` is the
  driver host's verb (`Host/Program.cs:704-708`). Its usage line is in `DriverCommand.Usage` (`Driver/Loop/DriverCommand.cs:78-81`), held by
  `DriverCommandTests.The_usage_names_every_verb_the_host_answers` (`DriverCommandTests.cs:101`). `withdraw <id>` belongs there.
- **d. §8.1, "`person.left` and `person.until` go; `person.gone` loses *its two minutes passed*".** The same card also draws the service
  door's confirmations, which keep their two minutes from the ask (`Web/person/Confirmations.tsx:24-50`, `:240-241`, `:331-333`;
  `PersonConfirmations.cs:127`). Both lines, and the two minutes in `gone`, stay for `source: 'service'`.
- **e. §8.2, "which the slice's test classes against each other".** The twin test compares only a row's own fields and an account's
  (`AttentionStateSliceTests.cs:1648-1656`), not `confirmation`'s. Removing `expires` from one side fails nothing.
- **f. §12, "D156 point 4: *within two minutes* reads …; *Five may wait* reads *ten*".** Point 4 is the service door's text
  (`D156.md:28-33`: `POST /api/confirmations` … *within two minutes. Five may wait.*). The design means to leave that door unchanged
  (§11's last row, and the last sentence of the D156 note). Its code is `PersonConfirmations.Most = 5` and `Lasts` = two minutes
  (`PersonConfirmations.cs:124`, `:127`). The pipe's rule lives in other places: the RPC1c and CONFIRMSEEN1 notes, and D196 point 4
  (*its two minutes start then and stay two; its ten minutes to be found stay*, `D196.md:36`). §12 names D196 only as "NOTIFY2a's amendment".
- **g. §5.1's `session` ground reads "its latest run's id".** No session record has one. The service's `Session`
  (`src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:101-123`) and the driver's `SessionRecord`
  (`Driver/Trees/SessionGroups.cs:14-123`) carry a state and `Updated`, and no run.
- **h. §5.1, "every route `PipeCensus` makes `Person` (`PipeCensus.cs:212-266`)".** That range misses four groups of `Person` routes:
  `DAORIS.REGISTRY`'s `WRITE_DECLARATION` and `DAORIS.REMOTES`' `SET` and `REMOVE` (`:122-124`); the three forms classed by payload
  (`:290-294`); and every unnamed route of a module classed route by route, which is `Person` by default (`:137`).
- **i. §9.1, "The journal binds each asker's id to its own key, so `RESULT` answers each".** A waiting press is not in the journal, as §1.3
  itself says. While it waits, `RESULT` reads `_running` (`InstancePipe.cs:859-862`).
- **j. §1.6's crash.** A verb still on its line hears the connection drop, finds no pipe for `RESULT`, and says *Daoris was still working on
  it* (`InstanceDoor.cs:274`), not the line at `:273`. The *stopped while this was under way* sentence comes from a later `result <id>`
  (`:461-462`).

### 2. The mechanism's holes

**Does CONFIRMSEEN1's reason still bind?** Its stated reason, that the verb would hold its line indefinitely, is answered: the request
outlives the line in a record, and the verb returns. But the answer holds only under two conditions the design does not meet.

- The asker must learn the request's id. The id is said only when the verb's own wait ends (thirty minutes by default), and an agent
  harness kills a tool call long before then. That asker is the owner's actual case (a steward's terminal), so "one that left finds it in
  the request's record" fails for exactly the asker that needs it.
- Connections must not be held for long. There are only eight (`InstancePipe.cs:19`, `:262`), and a connection with a press under way never
  goes idle (`:1029-1033`). The design raises the bound to ten, lets `--wait` run to 1440 minutes, and counts joined askers as nothing.

What the clock also used to do, and nothing now does, is drop a request whose asker no longer wants it. Grounds judge the world, not the
asker. A request from an agent whose session ended waits until the person confirms it, and the card does not say its terminal has gone.

**A press made long after it was asked.** §5.1 gives six grounds and no table for the hundred or so `Person` routes. The census test it
proposes would pass any choice. Here is each kind of press:

- **Set-up** (`SETUP_PRESS`). It reads its plan again at the press (`SetupRoutes.cs:31-47`). Nothing compares that plan with the one read
  when it was asked, and no ground can.
- **Drive** (`SET_DRIVABLE`) and **trees** (`SET_TREES`). The `setting` and `repository` grounds fit. But a value already set when the
  press is asked makes a card that can only be refused.
- **A session's words** (`SESSION_INPUT`). The `session` ground compares a field that does not exist (§1 g). Where only a turn passed, its
  sentence reads *it was parked, it is parked*.
- **Hand-over** (`SESSION_HAND_OVER`, `…_APPROVE`/`…_DECLINE`, `HANDOFF`). The hand-off holds its push to a result read when it runs
  (`DriverCommandTests.cs:297-320`). Nothing compares the hand-over ask or the branch with what they were when the press was asked.
- **Finish** (`RESOLVE_SESSION`). The `session` ground fits once it is defined.
- **Land** (`LAND_SESSION_TREE`, `LAND_ENTRY`, `LAND_ASK`). The landing holds to "the result the press was made on"
  (`DriverCommandTests.cs:270-295`), which is read when the press runs. A branch that gained commits during the wait lands them. The `ask`
  ground checks only that the ask is still open.

The presses with the most at stake are classed `timeless` (§5.1, design :195): `SWEEP`, and `TREES_SYNC`, which "fast-forwards the lines
and replays and deletes branches, every repository's at once" (`PipeCensus.cs:263-266`). With them go `HISTORY_CLEAR`, `SYSTEM_SPACE_FREE`
and `WORK_ABANDON`, each of which acts on a set that a plan route of the census reads (`SWEEP_PLAN`, `TREES_SYNC_PLAN`, `HISTORY_PLAN`,
`WORK_PLAN`, `SETUP_PLAN`, `HANDOFF_PLAN`; `SYSTEM_SPACE_FREE`'s `plan: true`; `PipeCensus.cs:193-200`, `:294`). Hours later, that set is
not the one anyone saw.

Answers to a held wait need that wait's own id as their ground, and none of the six grounds says it: a go-ahead, a permission, a helper, a
hand-over ask, a held attempt, a workflow node, a supervisor overrule. The `session` ground stands in for them only by proxy.

Where the grounds are judged is also a problem. §5.2 judges them "under [`PipeConfirmations`'] lock", reading the session, the quest, the
ask and the setting there. That lock is the one `Accept` → `Ask` and `Show` take, and the standing confirmation's own rule is that a lock is
never held across a read like that (`StandingConfirmation.cs:171-175`). The read at the ask has no named place in `Accept` at all.

**A press made twice.**

- **Two windows.** These are safe: the first answer wins under the lock, and the second gets `gone`.
- **A confirm racing a withdrawal.** This is not safe. `withdraw <id>` sends the kit's CANCEL, and for a press already confirmed and running,
  `CancelAsync` hands that CANCEL to the dispatcher, which aborts it mid-run (`InstancePipe.cs:840-846`). An asker's later word undoes part
  of the person's Confirm.
- **A resend.** Joining (§9.1) breaks four ways:
  - Each joined asker's `_waitingFor` names the same confirmation, so one asker's CANCEL withdraws the card for all of them (`:825`,
    `:840`).
  - A `CONFIRMING` that names the first asker's id is dropped by the verb's own filter (`InstanceDoor.cs:495`), so the second verb never
    says it waits, and cannot tell waiting from running (§4.1).
  - A press asked at 10:00 joins grounds read at 09:00, and is refused `changed` for a change that came before its own ask.
  - Joined askers count nothing and are unbounded, while the record holds one binding (§7.1).
- **A restart.** The order "begin, remove, run" relies on the journal still holding the begin line at the next start. It does so for
  24 hours only (`PipeJournal.cs:74`, `:189`). And a refused or withdrawn ending whose journal line fails to write is swallowed
  (`InstancePipe.cs:712-719`), while §7.1 removes the record only after that line. The record is then kept, so the refused request comes
  back after the restart.

**Who withdraws, and the asker across a restart.** The asker is whoever holds the key: the record keeps the key's fingerprint, and
`SentPresses` keeps the key itself under the home. Any process that reads `pipe/sent/` can withdraw, which D156 point 7 already allows.
That much is sound. With joining, though, "the key's holder" is several holders of one card.

**What a restart keeps, and whether a later process may confirm it.** It may, and that is the design's point. It is safe only if the
restore re-applies every check `Accept` makes. As written it does not:

- **The file name.** The record is `<home>/pipe/waiting/<id>.json`, named by the terminal's request id. The instance takes any id of 1 to
  128 characters (`InstancePipe.cs:551`); only the verb's own copy is held to letters, digits and `-` (`SentPresses.cs:63`). An id such as
  `..\..\data\standing-confirmation` makes the instance write the record outside its folder. When the request ends, the instance deletes
  that file.
- **The record's contents.** It holds "its card's fields and named values" beside the payload. The restore names no step that re-judges the
  census class, the shape, the bound, or the payload's fingerprint against the binding. So a cut record, one from an older build, or one
  any process of the account wrote can do any of these:
  - show one thing and run another (PERSONDOOR1b's "the card shows only what binds");
  - put a `Window` or `Role` route on a card, which the census keeps off cards (`PipeCensus.cs:280-284`);
  - pass the bound.

  §7.1's "grants nothing it could not ask over the pipe" holds only once those checks run again.
- **Newer and cut records.** A newer build's record is moved aside and journaled `lost`, so a later build that could read it never does.
  The standing confirmation instead leaves a newer record in place (`StandingConfirmation.cs:146-150`). `unread/` is never pruned, and it
  holds payloads, among them a remote's machine key (`RemotesModule.cs:51`).
- **A pipe that is not served.** The restore runs inside `Serve`, so where `Serve` does not serve (`InstancePipe.cs:237-255`), no request
  comes back and nothing is said.

**The bound, once nothing expires, and what floods.**

- **Connections, not cards, are the real bound.** Ten waiting verbs cannot all be on their lines with eight connections: the ninth meets
  *did not answer* (exit 2), not `confirmations-full`. Seven verbs at `--wait 1440` hold the pipe for a day, and during it no `result` or
  `withdraw` can connect.
- **Withdrawals and asks flood.** Every withdrawal becomes a notice in *Done and failed* (§6), and every ask raises a balloon
  (`PipeConfirmations.cs:181-182`). An asker that loops ask-then-withdraw floods both, and the bound counts neither.
- **The ten are shared.** Agents' jobs and the person's own terminal draw on the same ten. D201's caller look (`in-agent-job`/`outside`) is
  the identity that a per-class bound could use. §11 rejects a per-terminal bound without considering it.

**What the person sees on a card hours old.** In en and zh they see:

- what the press does, by its act and every value;
- its age (*asked {{ago}}*, weighted after an hour);
- Daoris cannot tell who ran it;
- the asker, unless the card was restored.

They do not see:

- whether a terminal still waits on it, or the asking process still lives. §8.2 groups the row by "a terminal still connected", but no
  field carries that and no event re-tells it when a line ends;
- what the press rested on when asked;
- after a restart, who sent it at all. The asker's name is kept on disk and withheld, though D156 point 7 already concedes the home's files
  and the card already says Daoris cannot tell.

That is enough to refuse, and thin for confirming a set-up or a land asked six hours ago.

The words have three problems:

- The new English chrome names the thing *request* four times, but the glossary's `request` row says it is "not a name on the window"
  (`glossary.json:488`). Today's English calls it *a terminal's press* (`notifications.json:39`).
- `restored.title` and `restored.body` take a count without an `_other` form (`agents.json:8-9` has the pattern).
- `{{setting}}`, `{{then}}` and `{{now}}` need catalogue words, not codes.

**How it composes with D201, D208 and D209.** A standing-confirmation admission never reaches the card. Neither does a role's act,
whether the supervisor's or the steward's, since a `Role` route is never asked. D209's `owner-set` is marked when a confirmed card is
dispatched (steward design :593-598), so a request that is only waiting marks nothing, and a steward's act during the wait shows up as
`changed` at the Confirm. That composition is right, but it is unstated.

`PipeGrounds` would declare, per route, which payload field names a session, repository, quest or ask. D209's `PipeCensus.Targets`
(steward design :596-598) declares the same thing over the same routes. Two tables would drift; one should serve both.

**The census and the person's doors.** `PERSON_CONFIRM_SHOWN` stays `Window`. Its reason in the census comment (`PipeCensus.cs:278`,
"would start its two minutes") becomes stale. `PERSON_CONFIRM` gains `changed` with a code and values, and the card words that reason.
The grounds census must run over `PipeCensus.Routes`, payload forms included, plus the `Person` default (§1 h).

### 3. The rows

**Row 1a cannot be built alone, and main does not keep working.** 1a drops `expires` and changes the row's fields. The window still
requires `expires: string` and draws it as *It waits until …, then is refused* and *is refused at …* (`Confirmations.tsx:44`, `:272`,
`:332`; `attention.ts:520-522`, `:906`). Between 1a and 1c, the card shows an invalid time or a false refusal, and the twin test that would
catch it does not cover `confirmation` (§1 e). Either the card's line and the row's words land with 1a, or 1a keeps `expires` and sends
`null` until 1c.

**1b is in order, but its proof names the wrong census** (§1 c). `--wait` "on every verb that presses" touches each family's usage
(`DriverCommand`, `AgentsCommand`, `ToolsCommand`, `DriverChoicesCommand`, the plugin commands) and the `daoris` forwards. It should be
parsed once, where the world is made (`InstanceHost.World`).

**1c is in order.** Its balloon and its notices' words are partly the modules'.

**The proofs prove less than the rows claim:**

- The hour's wait and the refusal are proved.
- The stale case covers four of the six ground outcomes, with no `ask`, no `setting` *changed*, and no case of a change made before the ask
  that must hold.
- Nothing proves joining (the 2026-10-24 drive sent twice), `withdraw <id>`, its race with a Confirm, the restore's validation, the bound at
  the restore, or the record's file name.
- "A crash is the same test with no `BeginStopping`" is not a crash. Disposing the pipe calls `BeginStopping` (`InstancePipe.cs:339`). The
  crash case is the record read back from a home whose first pipe was never disposed.

**The first row is not the one that gives the owner the most, soonest.** 1a carries the clock's end, the records and the restore, a
grounds table for about a hundred routes, joining, the bound, notices and the row. The owner's loss was two minutes running out. A smaller
first row would give most of that:

- **1a-i**: no time end; a request still ends at a close, with a notice; the grounds table, with every route not yet classified keeping
  today's clock; `waiting` in `RESULT`; the card's line; and the id said at `CONFIRMING`.
- **1a-ii**: the records and the restart.

### 4. Findings

1. **The record is named by the asker's own id**, which the instance takes unvetted (`InstancePipe.cs:551`; the verb vets only its own,
   `SentPresses.cs:63`). The instance writes, and later deletes, a file at a path an agent chooses. **Blocking.** Smallest change: name the
   file by the confirmation's own id (`PipeConfirmations.cs:168`) or by the SHA-256 of the request id, and keep the request id inside it.
2. **The restore trusts the record** (§7.1-§7.2). It names no re-judgement of the census class, the shape, the bound, or the payload's
   fingerprint against the binding, and it holds the card's fields beside the payload. **Blocking.** Smallest change, at the restore:
   - rebuild the request from its route and payload alone;
   - class it by the census, `Person` only;
   - recompute its fields with `FieldsOf` and its named values with `PipeWording.Named`;
   - compare the payload's fingerprint with the binding;
   - apply the bound;
   - end anything that fails as `lost`, with one notice;
   - never read the card's fields from disk.
3. **The grounds have no per-route table, and they cannot judge set-ups, lands, sweeps and answers** (§5.1, design :195). There is no table
   for the `Person` routes. Set-ups, lands, sweeps, `TREES_SYNC`, `HISTORY_CLEAR`, `SYSTEM_SPACE_FREE` and `WORK_ABANDON` are timeless or
   unassigned, and answers to a held wait have no ground. The census test would pass any choice and misses `REGISTRY`, `REMOTES`, the payload
   forms and the `Person` default (`PipeCensus.cs:122-124`, `:137`, `:290-294`). **Blocking.** Smallest change:
   - put the route-to-grounds table in the design;
   - add a `plan` ground, the fingerprint of the paired plan route's answer, for every press that has one;
   - add a `branch` ground, the tip read at the ask, for lands and hand-offs;
   - add a `wait` ground, the held wait's id still open, for every answer;
   - keep `timeless` for presses that delete and move nothing;
   - refuse a `Person` route with no row at the ask, as `ungrounded`, never treating it as timeless.
4. **The id is said only when the verb's wait ends** (§4.1). The default is thirty minutes, and an agent harness's tool timeout is far
   shorter, so the owner's own asker never learns the id. **Blocking.** Smallest change: the `CONFIRMING` line names the request id and the
   `result` and `withdraw` commands at once (`InstanceDoor.cs:499-501`).
5. **Joining (§9.1) breaks four ways:**
   - one asker's withdrawal ends the shared card for every asker (`InstancePipe.cs:825`, `:840`);
   - a `CONFIRMING` naming the first asker's id is dropped by the second verb's filter (`InstanceDoor.cs:495`);
   - a later ask joins grounds read before it;
   - joined askers are unbounded, and the record holds one binding.

   **Blocking.** Smallest change: drop joining from this design, since without a clock and with the id said (4) nobody needs to resend.
   Otherwise, give a request a bounded set of askers, each with its own binding, all written to the record. `CONFIRMING` names each
   asker's own id. A withdrawal removes only its own asker, and the card ends when none remain. A press joins only where the grounds read
   at its ask equal the waiting request's.
6. **`withdraw <id>` cancels a press the person already confirmed** (§4.3): for a running press, `CancelAsync` dispatches the kit's
   CANCEL (`InstancePipe.cs:844-846`). **Blocking.** Smallest change: `withdraw` ends only a waiting request. One under way answers
   `running` and nothing is cancelled. Use a pipe-own `WITHDRAW { id, key }`, or a CANCEL flag that touches only what waits.
7. **§12 and the D156 note amend the wrong text** (§1 f). They amend D156 point 4, which is the service door's (`D156.md:28-33`,
   `:277-279`), contradicting §11 and `PersonConfirmations.cs:124`/`:127`. They do not name D196 point 4's own two and ten minutes
   (`D196.md:36`). **Blocking.** Smallest change: leave point 4's service text and add one sentence for the pipe. Name D196 point 4's
   sentence and give D196 a dated note at acceptance. Correct the D156 note to match.
8. **Ctrl+C does not withdraw today** (§1 b), and four consoles tell the person it does not. Not blocking. Smallest change: keep Ctrl+C
   as it is, and add `withdraw <id>` to those four sentences. Or, if it is to withdraw, say that it is new, send CANCEL on a fresh token,
   and change the four sentences.
9. **The grounds are read under `PipeConfirmations`' lock, and the read at the ask has no place** (§5.2). Not blocking. Smallest change:
   read at the ask in `Accept`, before `Ask`, outside every lock. At the Confirm, take the request from what waits under the lock, read
   outside it, then end it `Changed` or run it. Readers come from the loop's memory, or are bounded.
10. **The `session` ground compares a field no session record has** (§1 g), and its sentence misleads when only a turn passed. Not
    blocking. Smallest change: name the field compared (the state and the record's `updated`, or the last word or move it holds). Give
    *ran another turn while this waited* its own reason. Word `{{then}}` and `{{now}}` by `sessionState.*`.
11. **A press asked for a value already set makes a card that can only be refused** (§5.1's *already*). Not blocking. Smallest change:
    judge the `setting` ground at the ask too, and answer an already-set press at once, with its reason and no card.
12. **The card's time lines and `gone` also serve the service door** (§1 d). Not blocking. Smallest change: draw `person.waits` for
    `source: 'pipe'` only. Keep `person.left` and `person.until`, and add a `gone` that names the two minutes, for `source: 'service'`.
13. **Row 1a breaks the window until 1c** (§3), and no test sees it (§1 e). Not blocking. Smallest change: 1a sends `expires: null`, and
    the card and row handle a request with no time in 1a's web lane. Extend the twin test to `confirmation`'s fields.
14. **The verb's proof and `--wait` are placed wrongly** (§1 c, §3). Not blocking. Smallest change: hold `withdraw <id>` in
    `DriverCommand.Usage` and `DriverCommandTests`. Parse `--wait` once, in `InstanceHost.World`, and name each usage text and golden it
    changes.
15. **The bound is ten, but only eight connections exist, and `--wait` runs to a day** (§9, §4.1). A ninth verb on its line meets *did not
    answer*, and seven long waits hold the pipe for a day, `result` and `withdraw` included. Not blocking. Smallest change: cap `--wait` at
    thirty minutes, the default today (longer waits read by `result`), and state the bound beside `PipeLimits.Connections`.
16. **The restart's ordering leaves holes** (§7.1-§7.2):
    - a begin line older than 24 hours is gone at the next start (`PipeJournal.cs:189`);
    - a failed journal write keeps the record, and a refused request returns (`InstancePipe.cs:712-719`);
    - an unserved pipe restores nothing.

    Not blocking. Smallest change: write `begun` or the ending into the record itself before acting. Remove the record even where the
    journal line failed. Restore in the loop before `Serve`, so cards come back whether or not the pipe is served.
17. **Unread records are handled badly** (§7.2). A newer build's record is journaled `lost` and moved aside for good. `unread/` is never
    pruned, and it keeps payloads such as a remote's key (`RemotesModule.cs:51`). Not blocking. Smallest change: leave a newer record in
    place, not asked, as `StandingConfirmation.cs:146-150` does. Remove a cut record after its notice. Say in §7.1 that a refused request's
    payload now reaches the disk until it ends.
18. **Withdrawals and asks flood the person** (§6, `PipeConfirmations.cs:181-182`). Every withdrawal is a notice, and every ask a balloon.
    Not blocking. Smallest change: give a notice only for a request some window showed (`shownAt`), and coalesce balloons by route within a
    minute.
19. **An old card does not say whether anyone still waits on it, or who sent it after a restart** (§8.1-§8.2). Not blocking. Smallest
    change: push a `waitedOn` fact, re-told when a line ends, and use it for the card's line and the row's group. Keep the system-named
    asker in the record and show it as *sent by {process} ({pid}) before Daoris restarted*.
20. **The words conflict with the glossary and miss forms** (§8.3). English *request* breaks the glossary's `request` row
    (`glossary.json:488`); the count strings lack `_other` forms; and `{{setting}}` is a code. Not blocking. Smallest change: add a
    glossary row for a terminal's request, en and zh, or keep *press* as the English does today. Add `_other` forms, and word each setting's
    name.
21. **`SentPresses`' longer keep is incomplete, and a late `result` says something false** (§4.2). Keeping past 24 hours needs both
    `Prune`'s mtime (`SentPresses.cs:74`) and `Find`'s age (`:52`) changed. A press last read as `waiting` that then ended unread is kept
    forever. A `result` more than a day after the end says *it stopped while this was under way* (`InstanceDoor.cs:461-462`), which is
    false. Not blocking. Smallest change: cap the keep (thirty days, say), and make `unknown` say that Daoris keeps an ending for a day.
22. **The design's account of D201, D208 and D209 is off** (§1 a, §2). Not blocking. Smallest change:
    - correct "What this is not";
    - state that until STEWARDROLE1d every press is a card;
    - build `PipeGrounds` on D209's `PipeCensus.Targets`, so there is one table;
    - say that a waiting request marks no `owner-set`;
    - list a bound per D201 caller class (`in-agent-job`/`outside`) as the later answer to agents filling the ten.
23. **The proofs leave claims unproved** (§3): joining, `withdraw` and its race, the `ask` and `setting`-changed grounds, a change made
    before the ask that must hold, the restore's validation and bound, and the file name. The crash case goes through `Dispose`
    (`InstancePipe.cs:339`), so it is not a crash. Not blocking. Smallest change: add one case for each, and model the crash as a second
    pipe over a home whose first pipe was never disposed.
24. **The first row is too large to give the owner the gain soon** (§3). Not blocking. Smallest change: split 1a into 1a-i (no time end,
    the grounds table with today's clock kept for any route not yet classified, `waiting`, the card's line, the id at `CONFIRMING`) and
    1a-ii (the records and the restart).
25. **Smaller misstatements** (§1 i, §1 j): the journal is said to bind waiting askers, and §1.6 quotes the wrong crash sentence. The line
    answer at a close (§7.2) also names no code, so the new verb cannot exit 1 `waiting` on it. Not blocking. Smallest change: correct
    both sentences, and answer the close as `waiting` with `why: close`. An old verb then reads it as a refusal, exit 2.

Findings: 25, of which 7 are blocking.

**Verdict: another round.** The direction is right, and §3's reversal of CONFIRMSEEN1 survives the read: a record that outlives the line,
a verb that returns, and an asker who reads how it ended. But four of the blocking findings are design, not wording: what each `Person`
route rests on and how *changed* is judged (3), what a restore must re-check (1, 2), and what joining and withdrawal mean for a press
already shared or already confirmed (5, 6). The steward cannot check a hundred-route grounds table, or the restore's validation, without
reading them. Round 2 should settle those four, say the id at `CONFIRMING` (4), correct the D156 and D196 amendment text (7), and take
the non-blocking changes above. Most of them are sentences the steward can check against the lines named.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `48c614823`, the only read of the round: 20 findings, 2 blocking; accept after named changes the steward can check without another read. Codex's read is owed on the final text.

### 0. Round 1's findings, checked against the code

Read at `ca046e7fe`. Nothing the design cites has changed since `ffcf42926`, apart from tests. The checkout holds the steward's
staged, uncommitted work, which shifts `SessionsRoutes.cs`' lines and adds a second `Role` route (`SUPERVISOR_DEFER`,
`PipeCensus.cs:287`). I read the committed tree, not that work. Paths are as the design's head gives them (design :30-31).

| # | Round 1's finding | Status | Checked |
|---|---|---|---|
| 1 | The record is named by the asker's id | Taken | §7.1 names it by the GUID `PipeConfirmations.cs:168` mints; restore step 1 removes any other name; §10's file-name row proves it. |
| 2 | The restore trusts the record | Taken | §7.2 step 5: `PipeCensus.Of` (`PipeCensus.cs:132-138`, `Person` only), route and fingerprint against the binding (`InstancePipe.cs:582-585`), `FieldsOf` (`PipeConfirmations.cs:254-260`) and `PipeWording.Named`, a grounds row, the bound; no card field read from disk. |
| 3 | No per-route grounds table | Taken in part | The table names every `Person` route of `PipeCensus.cs:212-266`, the three forms (`:290-294`), REGISTRY and REMOTES (`:122-124`) and the default (`:137`); every handler line it cites holds at HEAD. Open: the `plan` fingerprint (4.1), the readers (4.9), the plan read's bound (4.8), four rows' fields (4.10). |
| 4 | The id is said only at the wait's end | Taken, in 1b | The rows let 1a-i land without it (4.4). |
| 5 | Joining breaks four ways | Taken (dropped) | Holds: a resend is its own card, and the grounds refuse most duplicates once the first runs (a setting set, a session moved, a plan changed). |
| 6 | `withdraw` cancels a confirmed press | Taken | `WITHDRAW` ends only what waits; its lookup is `CancelAsync`'s (`InstancePipe.cs:819-843`) without the dispatch (`:845-846`). |
| 7 | §12 amends the wrong text | Taken | D156 point 4 kept (`D156.md:28-33`); D196 point 4's clock (`D196.md:36-38`) and service sentence (`:41-42`) named. D171's notes are missed (4.16). |
| 8 | Ctrl+C withdraws nothing | Taken | §1.2 says what is so (`InstanceClient.cs:134-176`; the four consoles' sentence). |
| 9 | Grounds read under the lock | Taken at the ask | At the Confirm the press leaves what every window is told before it is judged (4.6); the ask's read is misplaced (4.11). |
| 10 | `session` compares no field | Taken | `State` and `Updated` (`SessionGroups.cs:14`, `:35-36`), moved by `SetStateAsync` and a noted word (`Sessions.cs:890`, `:1562`). Holds. |
| 11 | An already-set value makes a refusable card | Taken | Its reader and the payload's value per row are not named (4.9). |
| 12 | The card's time lines serve the service door | Taken | §8.1 keys on `source` (`Confirmations.tsx:241`). Holds. |
| 13 | 1a breaks the window until 1c | Taken | `expires: null` and the card's line land together. The row half fixes a row no screen draws (4.12). |
| 14 | The verb's proof and `--wait` misplaced | Taken | `DriverCommand.cs:78` and `DriverCommandTests.cs:101` hold; `InstanceHost.World` takes no arguments (4.5). |
| 15 | Ten waiting over eight connections | Open in part | `--wait` is capped at thirty, but the default thirty holds a line per waiting verb for thirty minutes (4.14). |
| 16 | The restart's holes | Taken for two; wrong for the third | The record carries `begun` and `ended` and is removed past a failed journal line. The held-pipe exception contradicts the restore's own order and the lease (4.3). |
| 17 | Unread records | Taken | A newer record is left in place as `StandingConfirmation.cs:146-150` does; there is no `unread/`. The folder's category is a new problem (4.2). |
| 18 | Withdrawals and asks flood | Taken in part | A notice only where shown. The coalescing is placed on the event the page's toasts key on (4.13). |
| 19 | No sign of who waits, or who sent it | Taken on the card | The row's group is moot (4.12). |
| 20 | The words | Taken in part | *Press* is kept and the counts have `_one`/`_other`. Gaps remain (4.19). |
| 21 | `SentPresses`' keep; the late `result` | Taken | `Find`'s age and `Prune`'s time read one rule (`SentPresses.cs:52`, `:74`); `unknown` is reworded. Holds. |
| 22 | D201, D208 and D209 | Taken; the per-class bound deferred with a reason that holds | The look answers `untold` (`StandingConfirmation.cs:160-162`). What stays a card after STEWARDROLE1d is unstated (4.15). |
| 23 | Claims left unproved; the crash | Taken in part | The crash case is now a second pipe over a first that was never disposed. It contradicts the held exception (4.3). Gaps remain (4.20). |
| 24 | The first row is too large | Answered; the reason holds | The answer is one rule, not a clock kept for some routes. The row stays large, and its order with 1b is wrong (§3, 4.4). |
| 25 | Smaller misstatements | Taken | §1.3 (`InstancePipe.cs:859-862`) and §1.6 (`InstanceDoor.cs:274`, `:461-462`) now hold; the close answers `waiting`. |

§13 folds round 1's §1 corrections a to j into findings 3, 7, 8, 10, 12-14, 22 and 25. Each checks out at its line.

### 1. Claims about the code

§1's citations hold line by line at HEAD. So does every handler line in §5.1's table, each checked against its `[DriverRoute]`.
These claims are wrong:

- **a. *What needs you*'s row is drawn nowhere** (§1.5, design :107-111; §8.2, :466-470). `needsListed` drops every
  `confirmation` row (`Web/work/attention/attention.ts:110-117`). It does so for the band in view (`AttentionBand.tsx:152-153`),
  Overview's summary (`overview/NeedsSummary.tsx:63`), the place counts (`attention.ts:185`) and the app's count
  (`app/App.tsx:513`). The app mounts only the band in view (`App.tsx:1355`). So the row's why (`attention.ts:906`) and §8.2's
  group rule for `waitedOn` change nothing anyone sees.
- **b. No *checking that nothing changed…* line can be drawn** (§5.2, :361), and `changed` cannot be said where the card was
  (:366-367, §8.1 :462). `Answer` → `End` → `Take` removes the press from what waits and tells every window
  (`PipeConfirmations.cs:320-347`, `Settle` `:350-357`). The card is drawn from that telling and is gone when the answer returns
  (`person/Confirmations.tsx:230-237`, NOTIFY2a's comment). `PERSON_CONFIRM` is sent with the bridge's default 30 seconds
  (`bridge/person.ts:164-165`, `bridge/call.ts:8`), and §5.2 lets a plan read take 60.
- **c. The restore runs before `Serve` and is skipped where `Serve` says held** (§7.2, :411-412 against :432-433). It cannot
  know `Serve`'s answer before calling it. The loop already holds the home's lease before either step (`Loop/DriverLoop.cs:834`,
  then `:852`; D104), so no second instance on this home has records of its own. §10's crash case (:568) keeps the first pipe
  serving the name, which is exactly the case this exception would skip.
- **d. *That adds no reach*** (§7.1, :401-404). The `pipe` entry is in `run/` (`Driver/Platform/DaorisHome.cs:406`). D172 lets
  anyone lose `run/` while Daoris is closed (`D172.md:19`), never backs it up (`:45-47`), and keeps every file that holds a key in
  `profiles/` (`:27-28`). A waiting `DAORIS.REMOTES` `SET` carries a deployment key (`RemotesModule.cs:51`).
- **e. `LAND_SESSION_TREE`'s field `branch`** (:280) is the name the person entered for the landing (LANDNAME1a,
  `TreesRoutes.cs:470-477`), absent by default. It is not the branch landed, which is the tree read by `id` (`:498`).
- **f. `PLUGIN_READ` as the plan of a press that installs or runs code** (:329) answers the manifest alone: id, name, version,
  hook command, agents, servers (`PluginsRoutes.cs:158-185`), so it sees no change to the code.
- **g. `TOOLS_GIT, way: "file"`** (:334). The `Person` form is every `way` but `system` and `managed`, an absent one included
  (`PipeCensus.cs:290`).
- **h. *Every press that deletes … names `plan`, `branch` or both*** (:254-256). `SESSION_DELETE` names `session` alone (:300).
  `DISCARD_SESSION_TREE`'s *re-judges its unforced proof* (:287) is false for a press with `force`, which skips that proof
  (`TreesRoutes.cs:429-431`).
- **i. *The config from `DriverConfig.Load`*** as the `setting` reader (§5.2, :357-360). Several `setting` rows keep their value
  elsewhere:
  - `RULE_ACTION`: the permission rules file (`RulesRoutes.cs:27-55`);
  - `SET_SESSION_OPTION`: the agent's own options (`ConversationRoutes.cs:423-432`);
  - `DAORIS.REMOTES`: `remotes.json` (`RemotesModule.cs:46-80`);
  - `ACCOUNT_USE`, `TOOLS_USE` and `TOOLS_LOCATION`: their own files.
- **j. *A plan by sixty*** (:360). The page waits two minutes for `SETUP_PLAN` (`bridge/repositories/setup.ts:18`).
  `TREES_SYNC_PLAN` gets a bound that grows with the repositories, since it fetches (`bridge/call.ts:36-37`,
  `bridge/repositories/lines.ts:404-406`), and `HISTORY_PLAN` gets `lookBound(1)` (`bridge/sessions/history.ts:35`).
- **k. *The verb tells the two apart by the `CONFIRMING` it heard*** (:194). A press confirmed during the verb's wait and now
  running was also preceded by `CONFIRMING`. `PressAsync` already asks `RESULT` at every lost answer (`InstanceDoor.cs:262-264`),
  and that answer, `waiting` or `running`, is the one that tells.
- **l. *Parsed once where the world is made (`InstanceHost.World`)*** (:187). `World(home, verb)` takes no arguments
  (`Host/InstanceHost.cs:10-13`), and it has 23 callers.
- **m. 1b *can land before or after 1a-i*** (:547-548). Before 1b, today's `result <id>` reads `waiting` in its default branch
  and says *it stopped while this was under way, and nothing was done again* (`InstanceDoor.cs:460-464`). Today's press, at its
  wait's end, says *Daoris was still working on it* (`:272-274`).
- **n. *The balloon on ask (`Attend`) is raised at most once a minute*** (:471-473). `Attend`'s event is also the page's toast's
  arrival and the window's announcement, keyed by its `Id` (`notifications/announce.ts:15-21`, `PipeConfirmations.cs:363-371`;
  D196 point 4: *said on the press's own event by its id*). Folding the event folds those as well.
- **o. *A waiting card holds none once its verb has returned, which `--wait`'s thirty minutes ensure*** (:518-519). For its first
  thirty minutes each waiting verb holds one of eight connections (`InstancePipe.cs:19`, `:262`), which never goes idle while a
  press is under way (`:1029-1033`). Today a press ends within twelve minutes.
- **p. The close *ends each connection's wait with `waiting`*** (:408-410). `Drain` waits on every `_running` answer
  (`InstancePipe.cs:304-316`), and a waiting press is in `_running` (`:609`). Unless the close completes those answers, every
  close with a press waiting spends its 15 seconds (`DriverLoop.cs:652`) and logs `pipe.drained`.
- **q. *`questState.*`*** (:269) names no catalogue. A quest's state words are `status.*` (`locales/en/status.json:2-3`).

### 2. The mechanism's holes

**Does CONFIRMSEEN1's reason still bind?** No. Its reason was that the verb would hold its line indefinitely. §3's three
changes answer it:

- the press is a record;
- the verb says the id at once and returns within thirty minutes;
- `RESULT` reads how it ended.

Two conditions remain. First, the id said at once is 1b's, so the rows must not let 1a-i land without it (4.4); §3 itself says
each change holds only with the others. Second, the bound that now binds is the pipe's eight connections for each verb's first
thirty minutes, not the ten cards (4.14). *Its asker told then* holds for a terminal still on its line. An asker that returned
learns the end only by asking `result` again. That is the row's own wording, and it is enough.

**A press made long after it was asked**, by kind:

- **Set-up.** It rests on `repository` and `plan` (`SETUP_PLAN`). The plan's answer changes with no change to what the press does:
  - its `Title` and `Body` carry the day (`SetupQuests.cs:53-56`);
  - the body is also composed from the line's facts, the Daoris version and the workspace's other repositories
    (`SetupPress.cs:145-148`);
  - the answer carries the line's `Commit`, a tip (`SetupRoutes.cs:101`).

  As the rule reads (*ids, tips and states, never a time*), a set-up confirmed the next morning, or after any commit on its line,
  is refused `changed`. That is the owner's first case (4.1).
- **Drive and trees.** `repository` and `setting` over `DriverConfig`'s `Drivable` and trees fit, and so does `already`, once
  the payload's `drivable` and `ownTree` (`DriverRoutes.cs:49`, `:73`) are named as the value set.
- **A session's words.** `State` and `Updated` see a session that moved. A named question's `wait`, with its handler's
  `question-changed` (`ConversationRoutes.cs:124-129`), sees a stale answer. A turn that moved no state is unseen, and §5.1 says so.
- **Hand-over.** `SESSION_HAND_OVER` rests on `session`. The approve and decline routes take the absent `ask` as the current
  one, recorded at the ask. That is right: the handler picks the current ask at its run
  (`Optional(request, "ask") ?? files.Ask(session)`). Their reader is unnamed (4.9).
- **Finish.** `RESOLVE_SESSION` rests on `session`. Holds.
- **Land.** The `branch` field names the wrong thing (§1 e), but `LANDING`'s plan answers `Lands` with each commit
  (`TreesRoutes.cs:667-679`). So a fingerprint over those shas, without the gate's moments, catches commits added during the wait.

`timeless` is held to the four that delete and move nothing (:337-340). The readers are named for four of the nine grounds, and
the `wait` rows (fourteen kinds) have none (4.9).

**A press made twice.**

- **A Confirm racing a withdrawal.** Safe: both take the press under the confirmations' lock, and `WITHDRAW` answers `running`
  to one taken.
- **A resend.** Its own card. The grounds refuse the second once the first has run, for a setting, a session or a plan.
  `CHANNEL_POST` rests on `ask` alone, so two confirmed cards post the same words twice. That is the person's double Confirm,
  and acceptable.
- **A restart.** The record's states order it. A crash during the Confirm's read leaves the record `waiting`, so it is asked
  again and nothing ran.
- **Two windows.** The first answer wins.

**Who withdraws, and the asker across a restart.** The key's holder, through the binding the record keeps. The key itself is in
`pipe/sent/` (`SentPresses.cs:32`), so any process of the account can withdraw, as D156 point 7 already says. Sound.

**What a restart keeps, and where.** A later process may confirm a kept press. That is safe once step 5's re-judgement runs, and
the design now makes it run. Two things are wrong about where the record lives:

- **The folder.** It is in `run/`, which D172 says anyone may lose while Daoris is closed, and which is never backed up.
- **What it may hold.** It may hold a deployment key, which D172 keeps in `profiles/` (§1 d, 4.2).

The held exception would hide every waiting press for a whole run, with no notice (§1 c, 4.3).

**The bound, and what floods.**

- **Connections, not the ten.** For thirty minutes the eight connections are the real bound (4.14).
- **The ten are shared.** Agents can fill the person's ten, and the per-class answer stays deferred, for a reason that holds.
- **Floods.** An ask-then-withdraw loop gets no notice and one balloon a minute. Each ask still toasts in the focused main
  window, by D196 point 4, unless the coalescing moves to the event (4.13).

**What the person sees on a card hours old**, in en and zh:

- the act and every value;
- *asked {{ago}}*, weighted after an hour;
- whether a terminal waits on it;
- the record's asker after a restart, said as the record's;
- *Daoris cannot tell*.

That is enough to refuse. For a press that rests on a plan (a land, a set-up, a sweep, a sync) it is thin to confirm. The
grounds hold the press to the world at the ask, but the person sees only the payload. A door from the card to the plan's own page
would show them that world, which the grounds require to be unchanged (4.18).

**Composition.**

- **D201.** Not offered, so it admits nothing.
- **D208.** The supervisor's acts are `Role`, so never a card.
- **D209.** A waiting press marks no `owner-set`; it marks at its Confirm. The steward design agrees at :855-857.

What the design leaves unstated is the state after STEWARDROLE1d:

- **A role's terminal makes no card.** Every press verb there refuses `role-terminal` (`D209.md:73`).
- **The owner's three cases become steward acts only later.** The set-up, the hand-over and the go-on wait for 1g, 1e and 1f
  (steward design :956-1012).
- **D209's `Targets` names no `ask`.** It names a repository, a quest or a session (steward design :656-657), and the grounds
  need `ask` (4.15).

**The census and the person's doors.**

- **`PERSON_CONFIRM_SHOWN`.** Stays `Window`, with its comment reworded.
- **`PERSON_CONFIRM`.** Gains `changed`.
- **`WITHDRAW`.** The pipe's own route, outside the census, as `RESULT` is.
- **The service door.** Untouched (`PersonConfirmations.cs:124-130`).

### 3. The rows

**1a-i** can be built alone, after 1b. It is still large: the whole table, about twenty-five plan projections and fourteen
`wait` readers. The design's reason for not keeping the clock on routes not yet grounded holds. But 1a-i ends every waiting press
at each close, with a notice. On the owner's install, which the steward republishes while the owner is away, its gain is partial
until 1a-ii. So 1a-ii should follow it directly, and the row should say this.

**1b** can be built alone, and should land first. Today's verbs misread `waiting` (§1 m), and without 1b an asker the harness
killed never learns the id.

**1a-ii** is in order. Its crash case contradicts the held exception (§1 c).

The proofs prove the stale cases per ground, the race, the file name and the restore's judgement. They do not prove:

- `waitedOn` re-told;
- a read that fails or times out, at the ask and at the Confirm;
- a plan read twice with nothing moved across a day, `SETUP_PRESS` above all;
- `WITHDRAW` during the Confirm's read;
- the drain's refusal leaving the press waiting;
- 1a-i's close notice;
- a close with a press waiting draining at once;
- two asks in a minute and the page's toasts (4.20).

### 4. Findings

1. **The `plan` ground's fingerprint is a rule, not a projection per route, and the owner's own set-up fails it**
   (§5.1, :249).
   - **The set-up.** `SETUP_PLAN` answers the day (`SetupQuests.cs:53-56`), a body composed from the day, the line's facts, the
     Daoris version and the workspace's other repositories (`SetupPress.cs:145-148`), and the line's commit
     (`SetupRoutes.cs:101`). So a set-up confirmed the next morning, after an update, or after any commit on its line, is refused
     `changed`.
   - **Code.** `PLUGIN_READ` answers no code (`PluginsRoutes.cs:158-185`), so a press that runs code sees no change in it.
   - **The census test** checks no projection.

   **Blocking.** Smallest change: each `plan` row names the fields its fingerprint takes and those it leaves out. `SETUP_PLAN`
   takes its case, rules, landing form and plugin, agent, refusal codes and `pressable`, and leaves out its title, body, commit
   and tool versions. `LANDING` takes `Lands`' shas, `LeftOut`, `Conflict` and the gate's states without their moments. A plugin
   folder's form takes every file under the folder. Then one census case per plan route: two reads of an unchanged world across a
   moved clock and a new day give one fingerprint, and one moved item gives another.
2. **The record lives in `run/` and may hold a deployment key** (§7.1, :385-389, :401-404). `HomeEntry.Pipe` is `run/pipe`
   (`DaorisHome.cs:406`). D172 lets anyone lose `run/` while Daoris is closed and never backs it up (`D172.md:19`, `:45-47`).
   Its point 3 keeps a key in `profiles/` alone (`:27-28`), and `DAORIS.REMOTES` `SET` carries one (`RemotesModule.cs:51`). So
   *kept across a restart* rests on a folder D172 says may be cleared, and *adds no reach* is false. **Blocking.** Smallest
   change: give the records an entry of their own in `data/`, in all three home twins and `home-census.test.ts`. Keep no record
   for a press whose payload carries a key (`DAORIS.REMOTES` `SET`); it ends at a close as today, with the lost notice. Say both
   in §7.1 and the D156 note.
3. **The held-pipe exception contradicts the restore's order and the lease** (§7.2, :411-412, :432-433; §10, :568). The restore
   runs before `Serve`, after the loop holds the home (`DriverLoop.cs:834`, `:852`). A held name, such as a closing instance's,
   would hide every waiting press for the run, with no notice. Not blocking. Smallest change: drop the exception and say the
   lease (D104) makes the restoring loop the home's one writer.
4. **1a-i can land before 1b, which breaks §3's second condition and today's `result`** (§10, :547-548). Today's verb reads
   `waiting` as *it stopped while this was under way* (`InstanceDoor.cs:460-464`), and its asker never learns the id. Not
   blocking. Smallest change: 1b lands first. It is safe against today's instance, as the design says. Also say that 1a-i's close
   still ends what waits, and put 1a-ii right after it.
5. **The verb's end is chosen by the wrong signal, and three details are unnamed** (§4.1, :187, :194).
   - The end should be chosen by `RESULT`'s state, which `PressAsync` already asks (`InstanceDoor.cs:262-264`), not by the
     `CONFIRMING` heard. A press confirmed and running is not *still waiting for your confirmation*.
   - `InstanceHost.World` takes no arguments (`InstanceHost.cs:10-13`).
   - `already`, exit 0, needs a branch beside `:288`.

   Not blocking. Smallest change: choose the sentence by `RESULT`'s state. Take `--wait` from the arguments in the host's entry
   before the verb is dispatched. Map `already` to 0.
6. **At the Confirm, the press leaves the card before it is judged** (§5.2, :361, :364-367; §8.1, :462).
   - `Take` tells every window (`PipeConfirmations.cs:342-347`), so the card is gone (`Confirmations.tsx:230-237`).
   - `PERSON_CONFIRM` is waited on for 30 seconds (`bridge/person.ts:164-165`, `bridge/call.ts:8`).

   Not blocking. Smallest change: keep the press told, with `checking: true`, until it is judged (`WITHDRAW` answers `running`
   then). Otherwise, say the reason is toasted and drop the checking line. Give `PERSON_CONFIRM` the plan's bound as its timeout.
7. **A read that fails at the Confirm ends a press that waited hours** (§5.2, :362-363). A service reconnecting or a git lock
   costs the person the press, the loss the row was written for. Not blocking. Smallest change: leave it waiting and say so on
   the card, as the drain's refusal does (§7.2, :434-435).
8. **The plan read's 60 seconds is shorter than the page's bounds for the same reads** (§5.2, :360; `setup.ts:18`,
   `call.ts:36-37`, `lines.ts:404-406`, `history.ts:35`). On a slow machine a set-up is refused `unread-grounds` at its ask. Not
   blocking. Smallest change: each plan ground reads within its route's own bound (`SETUP_BOUND`, `lookBound`), named in its row.
9. **Readers are named for four grounds of nine** (§5.2, :357-360). `wait`, `ask` and `repository` have none, and the `setting`
   rows whose value is not in `DriverConfig` have none either (§1 i). `already` needs each row's value from the payload
   (`drivable`, `ownTree`, `held`: `DriverRoutes.cs:49`, `:59`, `:73`). Not blocking. Smallest change:
   - each `wait` row names the open read that lists the wait (`PERMISSION_WAITS`, `HELPER_WAITS`, `ANSWER_WAITS`,
     `SESSION_HAND_OVER_ASK`, `OPINION_GATE`, `OPINION_WAITS`, `WORKFLOW_RUN`, `HELP_PROPOSALS`, `RULES`, `SESSION_QUEUE`,
     `SESSION_SUPERVISOR`, `TOOLS_LIST`);
   - each `setting` row names the read that holds its value (`STATE`, `RULES`, `SESSION_OPTIONS`, `ACCOUNTS`, `TOOLS_LIST`,
     `DAORIS.REMOTES` `STATE`) and the payload field that holds the value it sets.
10. **Four rows name the wrong thing** (§1 e, g, h). Not blocking. Smallest change:
    - `LAND_SESSION_TREE`'s `branch` ground reads the session's tree head by `id`;
    - a `DISCARD_SESSION_TREE` with `force` also fingerprints the tree's uncommitted paths;
    - `SESSION_DELETE` comes under §5.1's rule, or the rule excepts it with its reason;
    - `TOOLS_GIT`'s row is *every `way` but `system` and `managed`*.
11. **The ask's read is placed before the standing judgement and the bound, inline in a synchronous `Accept`** (§5.2, :356;
    `InstancePipe.cs:521`, `:624-631`, `:637`, `:980`). A press the standing confirmation would admit could be refused
    `ungrounded`, and an eleventh press reads a plan before `confirmations-full`. Not blocking. Smallest change: read after
    `:624-631` and after the bound check, inside the press's later task.
12. ***What needs you*'s row is drawn nowhere** (§1 a). Its group rule and the `work/attention` proofs move a path no screen draws.
    Not blocking. Smallest change: say the row is .NET's and undrawn, drop §8.2's group rule, and keep `waitedOn` on the card.
13. **Coalescing `Attend` folds the page's toasts and the window's announcement** (§1 n; §9.4, :522-523). This contradicts D196
    point 4 unamended. Not blocking. Smallest change: coalesce in `SessionBalloon` and keep one `Confirming` event per press.
    Otherwise, amend D196 point 4 in §12 and correct §9.4.
14. **For thirty minutes the connections, not the ten, are the bound** (§1 o; §9.2). Eight waiting verbs fill the pipe. The ninth
    asker, and every `result` and `withdraw`, meet *did not answer*, exit 2. Not blocking. Smallest change: raise
    `PipeLimits.Connections` above `Waiting`, with room for the pipe's own requests (sixteen, say), and state the two beside each
    other.
15. **What stays a card after STEWARDROLE1d is unstated** (*What this is not*, :25). A role's terminal refuses every press verb
    `role-terminal` (`D209.md:73`). The set-up, the hand-over and the go-on become acts only at 1g, 1e and 1f (steward design
    :956-1012). D209's `Targets` has no `ask` (steward design :656-657). Not blocking. Smallest change: say which presses remain
    cards after 1d and from where, and that the shared table adds `ask`.
16. **§12 misses D171.** Its RPC1d note says *the window's refusal or the two minutes' end … exit 1* (`D171.md:403-405`). Its
    RPC1j and RPC1h notes give `result`'s states and *the verb's Ctrl+C … is not built* (`:365-371`). §12 names *RPC1j* without
    its decision. Not blocking. Smallest change: name D171's three notes in §12 and the D156 note.
17. **The close must complete the waiting presses' answers, or every close waits out its drain** (§1 p). Not blocking. Smallest
    change: the close's `waiting` answer completes each waiting press's line answer and removes it from `_running` and `_owners`,
    keeping its record. Add a test that a close with a press waiting drains at once.
18. **An hours-old card that rests on a plan shows the payload alone** (§8.1). Not blocking. Smallest change: a door from the card
    to the plan's own page (the session's review, the repository's *Set up*, the branches' sweep or sync), where the current plan
    is shown.
19. **The words** (§8.3).
    - `notifications.done.lost_other` says *{{count}} terminal's presses could not be taken back* (:502). That is ungrammatical,
      and *taken back* is the design's word for a withdrawal (`person.gone`, `done.withdrawn`).
    - There are no keys for *checking that nothing changed…*, for the Confirm's *Daoris could not read what this rests on*, or
      for a restored asker with a pid alone or none (today's `person.asker.pid` and `.unknown` exist).
    - `questState.*` is not a catalogue (§1 q).
    - `{{setting}}` *by `settings.*` names* names no key per route, while `person.route.<ROUTE>` has every `Person` route's act.

    Not blocking. Smallest change: *could not be restored*; add the four keys in en and zh; `status.*`; word the setting by its
    route's act.
20. **Proofs missing** (§10). These cases are missing:
    - `waitedOn` re-told when a line ends;
    - a read that times out, at the ask (`unread-grounds`) and at the Confirm (the press stays waiting, per 7);
    - `SETUP_PRESS` asked, the clock moved past midnight with nothing else moved, then confirmed: it runs;
    - `WITHDRAW` during the Confirm's read: `running`;
    - the drain's refusal: the press still waits;
    - 1a-i's close notice;
    - a close with a press waiting: the drain is not spent;
    - two asks within a minute: one balloon, and the page's toasts as 13 settles them.

    Not blocking. Smallest change: one case each.

Findings: 20, of which 2 are blocking.

**Verdict: accept after named changes the steward can check without another read.** Round 2 answers round 1 where it was
deepest. The record is named by the confirmation's own id and re-judged from its route and payload. Joining is gone.
`WITHDRAW` never touches a confirmed press. Every `Person` route and form has a row, with citations that hold. CONFIRMSEEN1's
reason no longer binds, provided 1b lands first (4). What remains is narrower:

- the `plan` ground needs its projection per route (1), or the owner's own set-up is refused the next morning;
- the record needs a home that D172 does not let anyone clear, and must never hold a key (2).

Both can be checked against named lines. So can the non-blocking changes, above all: the held exception (3), the row order (4),
the Confirm's card and failed read (6, 7), the plan bound and the readers (8, 9), the close's drain (17) and the coalescing (13).
Codex's read stays owed under D191.

## Codex, the accepted text (owed)

Codex's read of the accepted design and D156's note at `16e0950e2`, owed since round 1 when its credits ran out, read after rows 1b and 1a-i were built: 16 findings, 9 blocking; reopen, keeping the unbounded wait, the separate cards and the withdrawal rule.

The design's §15 and D156's amendment note of 2026-10-24 say where each finding went.

### 1. Claims about the code

Read against `main` at `16e0950e2`, including both earlier reads verbatim and D156’s proposal, acceptance and built notes. The design’s §1 describes its earlier baseline; CONFIRMWAIT1b and 1a-i have since landed. Persistence remains unbuilt.

Paths below use `M` for `src/Daoris.Desktop/Daoris.Desktop.Modules`, `D` for `src/Daoris.Desktop/Daoris.Desktop.Driver`, and `W` for `src/Daoris.Web/src`.

These claims do not hold against current code:

| Text | What the code actually does |
|---|---|
| Design lines 292–296: landing plans carry full shas; hand-off and sweep plans carry tips. | `M/Bridge/Routes/TreesRoutes.cs:675` shortens landing commits; `:1022` returns hand-off identity and count, without a tip. `LinesRoutes.cs:726` and `:780` return sweep rows without tips; `:459` shortens sync’s `From` and `To`. D156’s built note acknowledges these departures, but they weaken the accepted grounds. Finding 1. |
| Lines 354 and 858: a discard’s branch ground reads the tree’s head, including the path form. | `PipeTargetRows.cs:104` uses `HeadOf("id")`; `PipeGrounds.cs:251` returns no reading without `id`. The handler supports `path` alone (`TreesRoutes.cs:440`). Finding 2. |
| Lines 277–278: `SESSION_DELETE` deletes a record, “never work.” | It also deletes local words, transcript, attachments and other session files (`D/Sessions/History/SessionDeletion.cs:74`; `SessionHomeFiles.cs:99`). It preserves Git work through separate refusals. Finding 16. |
| Lines 519–520: an interrupted journal entry makes `RESULT` say it stopped under way. | `InstancePipe.cs:1050` handles answered entries and otherwise returns `unknown`. `WITHDRAW` already handles interrupted entries separately at `:1094`. Finding 10. |
| Lines 441 and 568–571: a changed reason appears on the retained card before it goes. | `PipeConfirmations.cs:400` settles it; `:518` removes it and `:527` publishes that removal before `PERSON_CONFIRM` returns. The page consequently may toast the reason during an ordinary answer in the same open view. Finding 11. |
| Lines 324–326: an already-set setting is answered without a card. | This works only where the row supplies `Sets`. Several setting rows do not, including `SET_HEAVY_GATES`, `WORKFLOW_LANDED` and `TOOLS_USE` (`PipeTargetRows.cs:163`, `:179`, `:203`). Finding 12. |
| Lines 572–574: plan cards have a door to their plan. | `W/person/Confirmations.tsx:121` accepts no plan navigation callback, and its card’s actions at `:359` contain no such door. Finding 13. |
| Lines 416–419: the census proves each named field belongs to its handler. | The test searches the entire source file containing the route marker, not that handler (`M.Tests/Platform/PipeGroundsCensusTests.cs:55`). Finding 14. |
| Lines 442–444: the slowest ground or plan bound covers the Confirm. | Grounds run sequentially, and the implementation correctly uses their **sum** (`PipeGrounds.cs:373`, `:385`). A setup alone takes up to five seconds plus two minutes. Finding 16. |
| Lines 628–629 name the folded balloon’s keys; line 721 specifies one balloon for two asks. | `SessionBalloon.cs:142` uses `confirmingMany`; `waiting` already means an account wait at `:130`. `BalloonFold.cs:46` shows the first immediately and `:68` shows the later aggregate. Finding 15. |

The design also still says “Nothing here is built” at line 11 and “stays proposed” at line 870, contrary to D156’s acceptance at line 304 and built notes at lines 316 and 339.

### 2. The mechanism’s holes

The reversal of CONFIRMSEEN1 remains sound. A terminal need not hold its connection indefinitely, and a card can survive without that connection. The remaining problem is what the eventual Confirm authorizes.

**A late press.** The earlier reads checked whether each route had a ground and whether changing one example changed its fingerprint. They did not establish that the ground identifies the work. A branch can acquire different commits without changing its count. A pause can change without changing an ask’s state. A repository name can resolve to a different checkout while remaining registered. An attachment can change while its path and session record remain identical. These are ordinary changes during a long wait, not merely the acknowledged race between the final read and dispatch.

The path-only forced discard is the clearest destructive case: its head is not read at all, and a clean tree’s empty uncommitted fingerprint remains empty after new commits arrive.

**A press made twice.** Two windows answering the same in-memory card are guarded correctly by `PipeConfirmations.AnswerAsync`’s lock (`PipeConfirmations.cs:342`). `WITHDRAW` also leaves a checking or running press alone. Distinct requests deliberately remain distinct cards; two confirmations of those are two authorizations.

The restart specification does not state the equivalent uniqueness rule across restored files. It checks the journal, but does not explicitly reserve each restored terminal id against the other restored records. Two valid filenames can therefore describe one request id, or duplicate a confirmation id inside their records. The code’s ordinary acceptance path has an explicit `_running` binding check (`InstancePipe.cs:629`); restore needs that rule too.

**A kept record that runs different work.** Rebuilding card fields from the payload fixes the earlier direct card/payload mismatch. It does not bind the objects reached through names and paths. The payload can remain byte-for-byte identical while its checkout, file contents or landed branch changes.

**Failures and the phase before the card.** Persistence specifies successful writes but leaves failed terminal-state rewrites unsettled. Separately, grounding can take eighteen minutes before `CONFIRMING` says the id. During that interval the request has a reserved place but no card. Withdrawal, disconnect and close do not have a complete specified transition for that phase.

The asker’s recovery also remains weaker than the card’s lifetime: a killed terminal may never mark its sent record waiting, and its key remains under disposable `run/pipe/sent`.

### 3. The rows

| Row | Can it land alone, in order? | Does its proof establish its claim? |
|---|---|---|
| **1b** | Yes, first. Its compatibility with the previous instance is sound. Its recovery behavior needs findings 6 and 9 amended. | The id-at-`CONFIRMING` proof misses grounding before that event and a harness killed before its first `RESULT`. |
| **1a-i** | Its ordering and simultaneous web changes are sound. Its stale-work guarantee needs findings 1–5 amended, including repairs to the row already on main. | One changed example per ground proves the comparison machinery, not target identity. It misses equal-count replacement commits, path-only discard, replacement pauses, registry rebinding and changed attachment bytes. |
| **1a-ii** | It can follow 1a-i, but its restore and write-failure protocol needs findings 6–9 settled first. Its declared scope must include the driver, service and CLI home twins, beyond “modules, web.” | The listed cases miss duplicate restored ids, failed `begun`/`ended` rewrites, publication before persistence, and close while grounding. Its interrupted-result proof also requires finding 10. |

The projection proofs use hand-written JSON (`PipeGroundsCensusTests.cs:87`). Replacing words and dates in that JSON proves those substitutions are ignored; it does not prove the real reader’s shape or every field promised by the projection. Each destructive route needs a case changing the actual target while preserving the currently compared count, state or displayed abbreviation.

The crash fixture can prove restoration from a surviving waiting record. Keeping the first pipe alive does not itself prove the production lease handover or exclude two live executors.

### 4. Findings

1. **Counts and display abbreviations replace the required commit identities — blocking, 1a-i.**  
   Design lines 292–296 and 350–358 require tips and full commits. `PipeTargetRows.cs:96` gives an id-only `HANDOFF` no branch reading and fingerprints its count at `:99`; sweep and sync similarly take counts at `:111` and `:115`. A landed branch rewritten to different commits of the same count can pass and be handed to the plugin.  
   **Smallest change:** read and fingerprint full source, destination and relevant branch tips from the underlying plans, including an id-only hand-off’s resolved branch. Add equal-count replacement cases; changing a count is insufficient proof.

2. **A forced path-only discard does not bind committed work — blocking, 1a-i.**  
   `PipeTargetRows.cs:104` reads the head by `id` only, while `TreesRoutes.cs:446` removes by `path`. With `force`, `LoopGrounds.cs:145` compares only uncommitted paths and working-file bytes. New clean commits leave that fingerprint unchanged. Staged content can also change while the working-file bytes stay identical.  
   **Smallest change:** resolve both forms to the actual tree and fingerprint its head, index and uncommitted contents. Prove a clean new commit and changed staged bytes each produce `changed`.

3. **An old Resume can release a new pause — blocking, 1a-i.**  
   Design line 363 and `PipeTargetRows.cs:75` ground pause/resume only in ask or quest state. A pause is explicitly a local file, “never a state” (`D/Sessions/Work/WorkPausing.cs:60`); Resume removes whichever pause exists when it runs (`:225–236`). An ask can remain open throughout.  
   **Smallest change:** ground Resume in the particular pause, including its generation or `At` and stopped-session bindings. Ground Pause in the work set it will stop. Prove a replacement pause is preserved.

4. **Registration is presence, not target identity — blocking, 1a-i.**  
   Design line 267 and `LoopGrounds.cs:25` compare only whether the name is registered. Hand-off resolves that name to its current checkout at `TreesRoutes.cs:1050`. Unregistering and re-registering the name elsewhere can leave the ground unchanged while redirecting execution.  
   **Smallest change:** retain the resolved registry identity, workspace and checkout identity in the repository ground. Test rebinding the same name to another checkout.

5. **Terminal attachments remain mutable references — blocking, 1a-i.**  
   `SESSION_INPUT` receives only session/question grounds (`PipeTargetRows.cs:39`). Its terminal form reads `paths` after Confirm (`ConversationRoutes.cs:104`); `D/Sessions/SessionsCommand.cs:1169` then reads their current bytes. Keeping the payload does not keep what the asker attached.  
   **Smallest change:** fingerprint the resolved attachment paths and bytes at the ask and compare them at Confirm. Persist those readings. Test replacement of a file while the session and path remain unchanged.

6. **The card can outlive its only recovery key — blocking, 1b and 1a-ii.**  
   `InstanceDoor.cs:702` prints `CONFIRMING` without marking the sent record waiting; that happens only after a later `RESULT` at `:286`. A killed harness can therefore leave the one-day retention rule (`SentPresses.cs:18`) in force. The key also lives in `run/pipe/sent` (`SentPresses.cs:42`; `DaorisHome.cs:406`), while the card will live in backed-up `data/`.  
   **Smallest change:** durably mark waiting when `CONFIRMING` arrives; retain unresolved bindings in an appropriate kept, key-bearing home entry. Do not prune a still-live press’s only key on an unrelated next command. Prove killed-harness recovery after a day and recovery after restoring the supported backup.

7. **Restore lacks explicit uniqueness and baseline-shape checks — blocking, 1a-ii.**  
   Design lines 516–530 validate individual records but do not require filename/id agreement, unique confirmation ids, or unique terminal ids across the restored set. They also do not require saved grounds to match the current row’s required subjects and kinds. `PipeGroundRow.Judge` iterates only the saved readings (`PipeGrounds.cs:406`); an empty or incomplete baseline cannot protect omitted grounds.  
   **Smallest change:** validate complete record and baseline shape, require filename/id agreement, and reserve terminal ids and bindings before publishing cards. Collapse identical duplicates; reject conflicts without overwriting an existing ending. Add duplicate-id and missing-ground restore cases.

8. **Failed state rewrites have no defined safe outcome — blocking, 1a-ii.**  
   Design lines 491–497 cover an initial write failure and a journal failure, but not failure to rewrite `waiting` as `begun` or `ended`. The existing answer path removes and publishes the card before its asynchronous execution continuation (`PipeConfirmations.cs:527`, `:534`). A failed rewrite can otherwise leave a disk record saying waiting after the person answered.  
   **Smallest change:** make successful persistence a prerequisite for publishing the terminal transition or dispatching. On failure, run nothing and retain an explicitly unresolved card. Define recovery when deletion fails too. Inject failure at each write boundary, then reopen the home and prove no answered press returns as waiting.

9. **The reserved grounding phase has incomplete lifecycle rules — blocking, 1b, 1a-i and 1a-ii.**  
   Grounds precede the card and id notification (`InstancePipe.cs:776`, `:798`, `:806`), potentially by eighteen minutes (`PipeTargetRows.cs:122`). `WITHDRAW` has no waiting-card mapping then and returns running (`InstancePipe.cs:1086`, `:1118`). Close only closes existing cards (`:308`); the grounding continuation can subsequently publish one. Disconnect also misses its eventual `waitedOn` update (`:494`).  
   **Smallest change:** print the recovery id before grounding, and track the reserved place through withdrawal, disconnect and shutdown. Promotion to a card must recheck those states. Prove withdrawal and close during a held initial reader, with no later card and no spent drain.

10. **Interrupted `RESULT` is missing — not blocking.**  
    Design lines 519–520 promise an outcome the present `Result` switch does not return (`InstancePipe.cs:1050`).  
    **Smallest change:** return the interrupted answer for `PipeJournal.Seen.Interrupted`, as `Withdraw` already does at `:1094`; include that branch in 1a-ii’s proof.

11. **A changed reason is not reliably retained on its card — not blocking.**  
    Design lines 568–571 conflict with removal before the answer returns (`PipeConfirmations.cs:400`, `:527`). The page test keeps the card mounted artificially (`W/person/Confirmations.test.tsx:248`).  
    **Smallest change:** retain a local settled card until its reason has been presented, or amend the contract to use a toast. Test the real removal event arriving before the mutation answer.

12. **`already` is only partially implemented — not blocking.**  
    Design line 324 promises it for setting presses generally. `ReadGround.Already` requires `Sets`, which several direct-setting rows omit (`PipeTargetRows.cs:163`, `:179`, `:203`).  
    **Smallest change:** supply canonical requested-value projections for direct assignments; explicitly name the exceptions for merging operations.

13. **The plan door has no implementation or assigned proof — not blocking.**  
    Design lines 572–574 promise it, but `W/person/Confirmations.tsx:121` and `:359` provide no navigation mechanism.  
    **Smallest change:** assign the door to a row, pass a route-specific navigation action, and prove that it opens the named press’s plan and scope.

14. **The census and projection proofs overstate their reach — not blocking.**  
    `PipeGroundsCensusTests.cs:55` accepts a field mentioned anywhere in the handler’s file; `:87` supplies synthetic plan answers. Neither establishes the claimed handler correspondence and complete real projection.  
    **Smallest change:** restrict field checking to the actual handler and pair projections with real reader output. Mutate each safety-relevant field independently, including the equal-count cases above.

15. **The flood and balloon claims are inconsistent — not blocking.**  
    Design line 655 says ten simultaneous cards keep per-press toasts few; an ask/withdraw loop reuses those slots without bounding arrivals. Line 721 also contradicts the immediate-first balloon behavior (`BalloonFold.cs:46`, `:68`), and lines 628–629 reuse account-wait keys.  
    **Smallest change:** state the actual arrival behavior, correct the balloon proof and use `confirmingMany`. If bounded toast volume is required, add an arrival-rate rule; the card bound does not provide one.

16. **The settled text still contains incompatible instructions and descriptions — not blocking.**  
    Besides its status statements, §13 retains superseded directions: parsing in `InstanceHost.World` (line 829), skipping restore for a held pipe (831), grouping the undrawn row (834), and placing 1b between or beside the other rows (839). The timeout wording at 442 contradicts the implemented sum; the record-only deletion rationale at 277 omits local history deletion.  
    **Smallest change:** mark §13’s answers historical and superseded by §14, update build standing, specify the summed bound, correct deletion’s scope, and include all home-twin components in 1a-ii.

Findings: **16**, of which **9 are blocking**.

**Verdict: reopen.** The unbounded wait, separate cards and withdrawal rule stand. The accepted grounds still permit materially different work to pass, and the restart protocol leaves duplicate records, failed rewrites and the reserved grounding phase insufficiently defined. Findings 1–9 require amendments before the rows they touch proceed; those touching 1b or 1a-i also require repairs to the implementation already on main.
