# LIMITRESUME1: the reads of round 1 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-23-limit-resume-design.md` and D206 at `12a3997ef`, verbatim but
for its headings set one level down; Codex, the other maker, was out of credits that day. The steward ran it from outside the
install, read-only. Round 2 answers each finding in the design's reads table.

## Opus (foundation)

The independent Opus reader's foundation read, read-only.

### Opus's foundation read of LIMITRESUME1's design (D206, round 1)

Read on the design's branch (`12a3997ef`, the code as at `9950b24cb`): `docs/2026-10-23-limit-resume-design.md` and
`docs/decisions/D206.md` whole, against the code, D205's design §3.5 and its acceptance note, D189, D137 (with its TURNFAIL1
note), D125, D191, `.claude/knowledge/session-words.md` and `person-waits.md`. LIMITGOON1 was read as it is staged for landing
on main (`Conversations/Chat/LimitMove.cs`, `Conversations/Domain/LimitHeld.cs`, `docs/2026-10-23-limit-goes-on-design.md`).
Nothing was run. The install's machine log was not read (the brief's bounds), so §1's table is taken as written. Paths are
under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they say otherwise, as the design's are.

**Counts.** 41 stated facts checked: 34 hold, 7 need correcting (F1-F7). **7 blocking** findings (B1-B7, of which B6 and B7
are the steward's two install cases). **15 not blocking** (N1-N15). **Verdict: another round** (§5).

#### 1. Facts the design states, checked

| Claim (design §) | Code | Holds? |
|---|---|---|
| The limit is read from the door's failure only; `When` reads a time of day, weekday-and-time, or a date; zone read only where IANA; 2-minute margin; 60-minute default; nothing past 8 days (§2.1) | `Accounts/Usage/AccountLimits.cs:79-94`, `:108-143`, `:136`, `:187-191`, `:233-275` | Holds |
| The cool-off is per agent and account in `cooling.json`; it ends when `Until` passes, by *Try now*, by a sign-in (§2.1) | `Accounts/AccountCooling.cs:22-24`, `:55-58`, `:93-104`, `:131-135`; `Accounts/Harnesses/HarnessRoster.cs:257-285` | Holds |
| A stated weekly reset is kept by `AccountWindows.Told` (§2.1) | `HarnessRoster.cs:272-282` | Holds |
| A refused turn concludes `failed` (§2.1) | `Sessions/Domain/Observation.cs:121-126` (for `Taken` and `Open`) | Holds |
| `AccountLimited` replaces the agent's sentence with `account.cooling-*` and returns `Limit`; `AdvanceAsync(…, limit:)` stores it; the `limited` column (§2.1) | `Loop/Driver.cs:2815-2837`, `:2441`, `:2453-2455`; `AccountCooling.cs:280-284`; `src/Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs:545`, `:860`, `:875` | Holds |
| Only the protocol door's `turnFailed` is read; an unwritten cool-off is a plain failure (§2.1) | `Driver.cs:2819`, `:2826-2829` | Holds |
| `ReadStrikes` leaves a limited `failed` out (§1, §2.1) | `Service/ServiceClient.cs:2033-2035` | Holds |
| `CarriesOn` takes any `failed`; `Taken` carried on, `Open` started fresh or carried on with commits (§2.1) | `Quests/Domain/Planner.cs:1307-1322`, `:1329-1333`, `:1935-1944` | Holds |
| A carry-on walks; it is handed the brief; told its account changed only where the last account cools (§2.1) | `Driver.cs:1731-1735`, `:2192-2196`, `:2309`, `:2313` | Holds |
| The agent's own resume only with words waiting or an answered park; judged same agent, account, tree, kept id, door (§2.1) | `Loop/DriverContinuations.cs:40-45`; `Driver.cs:1987-1995`; `Conversations/Domain/Continuation.cs:207-239` | Holds |
| Words wait for the own account's reset, `ResumeChoice.Waits` (§2.1) | `Accounts/Rotation/ResumeAccount.cs:72`; `Driver.cs:1743-1751` | Holds |
| No account left: held before any record, `held.wait-*` or `held.cooling-account`; classed `Wait`, said once (§2.1) | `Driver.cs:1753-1758`, `:936`, `:1087-1116`; `Accounts/Rotation/AccountRotation.cs:717-752`; `AccountCooling.cs:232-245` | Holds |
| The wake is the look, every `PollSeconds` (§2.1, §3.4) | `Platform/DriverConfig.cs:73` (the empty config's 15), `Loop/DriverWatch.cs:789-796`, `:836-837` | Holds, but `pollSeconds` is the person's setting (N12) |
| A conversation's refused turn: `Limited`, `TurnNotTaken`, the cooling note, a turn `error`; the process stays (§2.2) | `Conversations/Chat/ChatRunner.cs:1257`, `:1879-1880`, `:2489-2521` | Holds |
| LIMITGOON1: the move to the walk's next account with the brief and `LimitHeld.Of`'s words; nothing written where no account has room (§2.2) | `LimitMove.cs:77-84`, `:96-124`; `LimitHeld.cs:17-56` | Holds |
| LIMITGOON2 open at `TASKS.md:344`; no `ActorChoices` in the code (§2.2, §2.3) | `TASKS.md:344`; no match in `src` | Holds |
| An ended chat's words wait for its own account's reset; four ways a held chat goes on (§2.2) | `Conversations/Chat/ChatGoOn.cs:119-150`, `:174`, `:235-275`, `:412-423` | Holds; F6 for one of the four |
| The held box is drawn only for a driven record (§2.2) | `src/Daoris.Web/src/work/conversation/ConversationView.tsx:190-191`; `src/Daoris.Web/src/work/WorkFrame.tsx:1441-1445` | Holds |
| A press with no account left is refused in the wait's sentence (§2.2) | `ChatRunner.cs:606-607` | Holds |
| The trace adds *by an account's limit*; the flag reaches the page (§1, §3.1) | `src/Daoris.Web/src/work/head/HowItCameToBe.tsx:651-662`; `src/Daoris.Web/src/work/head/trace.ts:40` | Holds |
| Each limited part is an *Ended* notice (§1, §3.1) | `Quests/Domain/Attention.cs:247-255` | Holds; `SessionEnded` carries no limit flag (N6) |
| The quest's page says *waits for an account* with no time (§1) | `src/Daoris.Web/src/quests/QuestPage.tsx:693-694` | Holds |
| D205's acceptance note records *a driven line's next start walks its choices* (§3.6) | `docs/decisions/D205.md:195-198` | Holds |
| The look's beside stages (§3.4) | `DriverWatch.cs:962-966`, `:986-993` | Holds |
| **F1.** Daoris's word `limit.reset` is kept on the ended record's `said` *through `DrivenInbox.KeepOrHoldAsync`* (§3.3 step 1) | `DrivenInbox` is a running session's dispatcher (`Conversations/DrivenInbox.cs:423-513`; `session-words.md` lines 15-21: *a record that is ending keeps nothing*). An ended record is told through the say door under a key: `Trees/Landing/RebaseTelling.cs:16-28`, `Loop/ReopenTelling.cs:246` | **Wrong seam** (B1) |
| **F2.** *A refused resume falls to step 2, said `account`, as today* (§3.3) | Today a fallback with only Daoris's words waiting starts nothing and marks them for the person (`Driver.cs:2036-2043`); with the person's words it hands Daoris's word on too (`:2005-2047`); the reason said is the refusal's own (`gone`, `unkept`, `elsewhere`, `exited`) | **Not today's behaviour** (B1) |
| **F3.** Step 2's held words go *in `BriefedMove`'s `SayHandedOver` order* (§3.3) | `BriefedMove` is the conversations' seam; a driven line's words go through the driver's fallback (`Driver.cs:2042-2047`, `CarriedOnAsync`) | Misnamed seam |
| **F4.** *The record keeps the reset the agent said in its note's `until`* (§3.2) | The note's `until` is the cool-off's `Until`, the reset plus the margin or the default (`AccountCooling.cs:280-284`, `AccountLimits.cs:119`, `:136`) | Imprecise (N11) |
| **F5.** LIMITGOON1's move carries an ended conversation at the wake (§3.5) | `LimitMove.MoveAsync` returns null with no live process (`LimitMove.cs:49`); `BriefedMove` opens on the record's own account (`Conversations/Chat/BriefedMove.cs:131`) | **Neither moves an ended conversation to another account** (B3) |
| **F6.** *the person's Go on* (`ChatGoOn.cs:282-290`) | That is `AnswerGoesOn`, the answers-alone bound's *Go on with this answer*, not a general press | Imprecise (N15) |
| **F7.** *15 s is inside the 2-minute margin* (§2.1, §3.4; D206 *Why*) | True for the default only; `pollSeconds` is set per machine | Imprecise (N12) |

Also read, and holding: the ledger's move out of an ended state clears `limit` (`Sessions.cs:836-860`, `forgive`), so a resumed
run concludes on its own; a resumed run's conclusion runs `AccountLimited` (`DriverContinuations.cs:552-554`) and a refused
resume puts the flag back (`:503-509`), so step 1 refused again by a limit stays no strike.

#### 2. Blocking findings

**B1. Step 1 has no decision point, and the one it implies repeats the install's bug.** Design §3.3 step 1 and D206 point 3.i:
the full resume is handed `limit.reset` *kept on its `said` through `DrivenInbox.KeepOrHoldAsync`*, runs through
`ContinueAsync`, and *a refused resume falls to step 2, said `account`, as today*.

- No inbox exists for an ended record (F1). The say door is the seam, as `RebaseTelling` uses it for an ended session's own
  conversation (`Trees/Landing/RebaseTelling.cs:16-28`, `DaorisKeys.Rebase`, a key that names no quest fact,
  `Conversations/Domain/DaorisWords.cs:128-150`).
- **Where the word is written decides whether the bug recurs.** A word said beside the look is planned only at the next look
  (the planner reads the snapshot first, `Planner.cs:1287-1291`). The same look plans the limited `Taken` quest as a `CarryOn`
  (`Planner.cs:1329-1333`). Its start walks (`Driver.cs:1731-1735`) and, with the own account just ready, can land on it briefed,
  which is exactly 9967271d.
- **The judge needs words.** `Continuations.Judge` returns `ended` with none waiting (`Continuation.cs:217`), and `ContinueAsync`
  puts the record back with none (`DriverContinuations.cs:254-255`). So the word must be kept before the judge, or the judge
  asked as if words waited.
- **"As today" is not today (F2).** With only Daoris's word waiting, the fallback returns `CannotGoOn` and marks the words for the
  person: *a message of Daoris's never starts a new session* (`Driver.cs:2036-2043`). The line would stop saying it cannot go on.
  With the person's words waiting, `limit.reset` (*Your account's limit has reset. Go on … where you stopped.*) would be handed to
  a new part on another account. The reason said is the refusal's own code, not `account`.

*Smallest change.* Decide step 1 in the carry-on's own start, before the walk: in `Driver.cs` at `:1716-1736`, a start whose
`Resumes` is `{ Limit: true }` asks `ResumeAsync` for its own account. Where that is ready and the judge passes (asked as if
words waited), it keeps `limit.reset` through the say door under its own key, as `RebaseTelling` does, re-reads the record
(`GoOns.HandedOnRecordAsync`, `Driver.cs:2007`) and goes through `ContinueAsync`. Otherwise it walks as today. On any refusal,
`limit.reset` is taken off the record by its id before the fallback, so step 2 is the plain carry-on with the person's words.
The reason said is the refusal's code. Name the key and its `DaorisKeys.Read` answer (no quest fact, as `Rebase`). 1b's proof
gains *the own account freeing in the same look as a carry-on is planned: a full resume, never a briefed part on it*.

**B2. The loop's bound is left implicit, and TURNFAIL1 contradicts it.** Design §3.3 says *none of the three counts one* and *a
limit with no stated reset tries again every 60 minutes … it never parks*. D125 point 3 says a quest is *never carried on into
the same refusal* (`docs/decisions/D125.md:23-24`), and D125 rejects *a separate count that parks* (`:62`). The design names
neither.

- **TURNFAIL1 parks the words on the second limited resume.** A resumed run's failed turn leaves its words `Wait` the first time
  and `Held` the second time running: *failed twice and wait for you*, marked `FailedTwice` (`Sessions/Domain/Observation.cs:450-476`,
  applied at `DriverContinuations.cs:556-566`). It reads any failed turn, a limit's included. So two limited full resumes in a row
  hold the person's words and `limit.reset` for the person, the opposite of *never parks*. `WordsOn` then skips the record
  (`Planner.cs:1450-1452`), and the next look carries the quest on briefed. The design does not mention it.
- **A reset that does not lift the refusal loops for as long as the refusal lasts.** The recorded spend-limit sentences name the
  *session* window's reset beside an admin's spend limit (`AccountLimits.cs:304`, `:316`, `:320`). If that limit outlasts the
  reset, each reset reopens the record, spawns, sends a prompt, and is refused at once. The page says *it resumes by itself at …*
  each time. With an unstated reset, that happens every 60 minutes for up to a week (`AccountLimits.cs:119`; Codex's *Try again
  later.*, `:359`).
- **The own account is not asked whether the reset lifted the limit.** The walk passes an account whose maker's reading still says
  it is out (`HarnessRoster.cs:1883`), and asks a just-reset account's maker (`ProbeWhy.Limit`, `:1895-1910`). A resume held to its
  own account skips both by design (USAGEONE1, `:1070-1077`), but step 1 now has a choice for a reading to change.

*Smallest change, inside D125.* State the bound as one attempt per cool-off, each refused attempt cooling again, never a count.
Make TURNFAIL1 agree: a conclusion `AccountLimited` marked limited is always `WordsAfter.Wait`, and `FailedTurnWords.Judge` takes
the flag. Name D137's TURNFAIL1 note among the amendments. Let step 1 ask the own account's reading after its cool-off, as the walk
asks (`ProbeWhy.Limit`), and treat a reading still out as cooling. Where a resume is refused at its first turn with the same `hit`
just after its account's reset, say on the line and in the band that *its last reset did not lift it*. That way the cost is seen,
and the person's *Go on now with…* is the way out, with no park.

**B3. The person's held words have two carriers and no single delivery.** Design §3.5: *the person's words from the limited turns
(`LimitHeld.Of`) and every word said while it waits are kept on the record's `said` through `ChatTurns` (`KeepAsync`)*, and at the
wake *each is handed Daoris's word, then the held words*.

- `LimitHeld.Of` reads the refused turns' words from the record's events, `user` events those turns already took
  (`LimitHeld.cs:27-29`, `:35-47`). LIMITGOON1 hands them in its move (`LimitMove.cs:121-124`). Re-keeping them on `said` makes a
  second copy. A move that also hands what waits (`BriefedMove` hands the record's waiting words) delivers them twice, and the page
  shows each twice.
- In the same conversation, the refused turns' prompts went to the agent (`ChatRunner.cs:2486-2487`). Re-sending them may repeat
  them in its own context. The design must say which, and test it.
- Driven step 1 says *Daoris's word, then the person's held words*, but `said` keeps keep-time order (`DriverContinuations.cs:254`),
  and the person's words were kept before the reset.
- For an ended conversation, the wake's move has no seam (F5): LIMITGOON1 needs a live process, and `BriefedMove` stays on the
  record's account. Moving it to another account is D205 §3.5(e)'s seam given the walk's account (MODELROLE1 row e, LIMITGOON2).
  That makes 1c depend on it, which §6 does not say.

*Smallest change.* One carrier per word. The refused turns' words stay in the events, and `LimitHeld.Of` stays their reader. Only
words said while waiting go to `said`. A same-conversation resume sends Daoris's word, then the words said while waiting. A move
sends the brief, the refused turns' words and the waiting words, each once, the waiting ones taken off by id. State the order
Daoris's word takes. Name the account-move seam for an ended conversation, and add it to 1c's *After*.

**B4. The person's presses: Stop has no target, Ask Daoris's press is refused, and nothing says the person outranks the resume.**
Design §3.7 says *Stop is the existing stop of the line*, and *Go on now with… opens D163's hand-over offer… the only way a pick
moves*.

- **Stop.** A waiting driven line's last record has ended (`failed`). *Nothing else leaves an ended state*
  (`src/Daoris.Service/Daoris.Service.Core/Sessions/SessionLedger.cs:1446-1451`), so there is no stop to make on it. A `limit.reset`
  word on the record would also be planned before the person's stop (`WordsOn` at `Planner.cs:1287` precedes `StopHolding` at
  `:1296`, and words release a stop, `:1284-1285`). `ReopenTelling` guards exactly this for its own words (`Loop/ReopenTelling.cs:238-244`).
- **Ask Daoris.** D163's offer refuses Ask Daoris's own conversation (`Conversations/HandOver/HandOver.cs:51-53`, `:188-190`). D205
  §3.5(e) as accepted keeps *the person's press … still never move it* (`docs/2026-10-23-actors-design.md:507-508`).
- **Precedence.** A hand-over the person made since the limited end (`snapshot.Handed`, `handedQuest`), *Go on in a new session*
  (`ResumeChoice.NewSession`), *Try again* or a pause can meet step 1 in the same look. The design says a handed agent does not
  walk, but not that step 1 yields to it.

*Smallest change.* For a waiting driven line, *Stop* is the pause (PAUSE1b, planned before words, `Planner.cs:1278-1282`), or a
named fact the planner reads before `WordsOn`. No `limit.reset` is said to a record whose line the person stopped or paused, as
`ReopenTelling` guards. For a conversation, *Stop* ends it as today, and the wake skips a person-stopped record. Ask Daoris's press
is its own door (`ResumeWords.HelpDoor`, *start a new conversation from this one*), not D163's offer. One sentence: *a press of the
person's since the limited end outranks the resume by itself*, checked in the start before step 1.

**B5. The scope stops at quests and conversations, but the direction and D206's title say work.** An intake that meets a limit ends
`failed` with the flag (`Loop/DriverIntakes.cs:272-276`). Its ask keeps its `Intake` binding, and the look picks only asks with
none (`:78`). So a limited intake consumes its ask and nothing resumes it, today or under this design. D205 §3.5(f) found the same
shape for a refused choice and named its seam (`EndRefusedIntakeAsync`, `docs/2026-10-23-actors-design.md:546-560`). Helpers,
second opinions, brainstorm voices and partners are not mentioned either.

*Smallest change.* A paragraph naming each kind. An intake at a limit is unbound by D205 (f)'s door, so its ask is picked again,
held at start by the cool-off as any start, with no strike. The one-turn owners take their own pass's next alternative, or wait
in the wait's sentence. Each gets a row, or the design says plainly which kinds it leaves out and why.

**B6. Install case 1, an update stops running conversations: the mechanism cannot carry it as written.**

- The update applies once no driven session runs and no turn is in flight (`src/Daoris.Desktop/Daoris.Desktop.Modules/Update/InstallUpdater.cs:225-240`).
  *A conversation between turns … is idle* (`src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/DriverLoop.cs:334-340`), and `Taking`
  is false on a text-only door (`ChatRunner.cs:1581-1582`). *Update now* applies whatever runs (`InstallUpdater.cs:226`).
- The close stops every conversation as the driver's act, `stopped` with `chat.closed`, and no `interrupted` flag
  (`ChatRunner.cs:358-362`, `:1367-1393`, `:2067-2073`).
- At the come-up, `TakeUpAsync` takes only ended chats with words waiting (`ChatGoOn.cs:204-218`). The sweep's path takes only
  `interrupted` ones (`:119-150`). Words a shell had already written to the agent are settled as uncertain and never sent again
  (`session-words.md` lines 33-37).
- So a conversation closed mid-turn loses that turn, and an idle one stays ended until the person writes. The design's wake is
  keyed to a cool-off's end and to conversations *held by a limit*, and neither describes these. Driven sessions under *Update now*
  are recorded `stopped` and interrupted (`InstallUpdater.cs:78`). That counts a strike (`ServiceClient.cs:2034-2035`, D104) and is
  carried on briefed, never in their own conversation (`Planner.cs:1938`, `Driver.cs:1731-1735`).
- What the design has that carries it is step 1's carrier: Daoris's word on `said`, then the existing go-on by the kept id.

*Smallest change.* At the close, each conversation with a turn in flight, a start pending or words held gets a Daoris word under
its own key, kept while it still takes words (`ChatTurns.KeepAsync`), before its process stops. Its text: *Daoris restarted for an
update while you were working; go on where you stopped*. `TakeUpAsync` then resumes it by its kept id: its own account, the limit
wait where that cools, the brief where no id was kept.

- Say what an idle conversation does: it stays ended, its page says Daoris closed it and that it goes on in its own conversation at
  the next word. Or, if the owner wants it back live, name what moves it, since no word-less move leaves an ended state.
- Count a conversation opening or going on (`ChatContext.GoingOn`, a pending start) as in flight for the drain.
- For driven work, either resume an interrupted take in its own conversation by step 1's path with no strike, the stop being
  Daoris's own, or say why D104's strike stands for an update.

**B7. Install case 2, no id kept: the full resume depends on it, and nothing makes its loss visible.**

- Both full resumes, step 1 and §3.5's same conversation, need a kept id (`Continuation.cs:233-236`). Without one the reason is
  `unkept`, which is `Lost` (`:46`). A conversation then goes to `BriefedMove` (`ChatGoOn.cs:392-396`), which is what `help resume`
  said, and a driven record goes to the fallback (B1). The design does not say where an unkept line goes, nor why ids were missing.
- The code keeps the id only when the door names it (`ChatRunner.cs:1975-1976`: ACP's `session/new`, the native `init`). A failed
  write is swallowed (`Conversations/GoOn/HarnessConversations.cs:35-42`).
- A conversation stopped for the driver's own reason never says its id was missing: `NotKept` is added only where no stop reason
  stands (`ChatRunner.cs:2071-2072`). That is never the case at an update's close, which is why the person met it first at
  `help resume`.

*Smallest change.* Name the unkept path: step 2's brief, never `limit.reset`. Diagnose the four records from the install before 1c
builds on the id: each `sessions/<id>.harness.json`, its adapter against the record's, and whether its door named a conversation
before the close. Write the cause into the design. Make a lost keep evidence: a machine-log line at the swallowed write, and at any
conversation's end with no id whatever stopped it.

#### 3. Not blocking, in order of weight

- **N1. A missing conversation on the native door.** Step 1 hands only *go on where you stopped* and no brief, so a resume that
  silently opened a new conversation would run blind. The protocol door refuses with `resource_not_found` → `gone`
  (`Agents/Acp/Acp.cs:2606-2612`). Say, per native door, what a resume of a missing id does, and compare the named id with the kept
  one before the prompt. §7's second unproven claim is this.
- **N2. The conversation wake's trigger is too narrow.** *A cool-off ended since the last look* misses three cases. The come-up
  has no previous read, so a reset between `TakeUpAsync` and the first look is missed. A signed-out account signed in, or one
  added, ends no cool-off. Under D205, another agent's account frees. Key the stage instead to *a held conversation whose computed
  wait (§3.2) has come*, the same computation the page shows. It is still one read per look.
- **N3. §3.6's cross-maker move of driven work** is supported by the owner's recorded direction (`D205.md:195-198`) and is sound as
  a foundation. Name its dependencies. MAKERTRAIL1: the merge tool refuses another maker's commits without the trailer
  (`TASKS.md`). D155's maker passes: an opinion's independence now meets a line with two makers. What `handOver: off` means for a
  driven line. D163's *Rejected: automatic hand-overs by default* needs a note for driven lines, as (e) gave it one for
  conversations.
- **N4. The amendments list is short of** D137's TURNFAIL1 note (B2); D163's rejection (N3); D205 §3.5(a)'s round-4 bullet, *(e)
  is reached only from a turn that met a limit, never from words said to a conversation already ended*
  (`docs/2026-10-23-actors-design.md:379-382`), which §3.5's last bullet changes; D125 point 6's *What needs you*; and D104 if B6
  takes an update's stop out of the strikes.
- **N5. Ended chats whose words wait for a cooling account that never met a limit.** These are D137 point 7's words the person said
  to an ended chat. §3.5 moves them to another account at a wake, replacing the exact conversation the person chose to go back to.
  Keep them on their own account, or say why moving them is right.
- **N6. The readers of *limited*.** Beyond the four §3.1 names: the terminal's `daoris-driver sessions` (D50's parity), Ask
  Daoris's session tools, Overview's counts and the list's filters. `SessionEnded` carries no limit flag
  (`Driver.cs:2494-2497`), so `AttentionWatch` needs one, as D205 N5 added `NotOffered`. Name the kind *the line's own news* is.
- **N7. 1b's proofs.** The planner cannot tell *own ready before another*, since readiness is read at the start. Steps 2 and 3 for
  a driven line are today's behaviour, apart from the held words and an `Open` quest. Put the proofs in the driver's stand-in test,
  B1's case first.
- **N8. The computed wait must use the walk's filters.** An account kept for conversations, or outside the scope
  (`ResumeAccount.cs:31-36`), is never a driven line's, so the page must not name it. The own sign-in has no `{{account}}`: give
  it its wording.
- **N9. `AccountCooling.Cool` replaces an entry whole** (`AccountCooling.cs:84-90`). A later, shorter reading on the same account,
  such as a session window or the 60-minute default from a conversation still running there, can shorten a stated weekly reset and
  waste a resume. Keep the later stated `Until`.
- **N10. A further part can take the waiting line's tree.** SESSONE1d's `Joining` goes into a tree nothing live holds
  (`Planner.cs:1588-1634`), which includes a limited line waiting days. The full resume then waits behind it or resumes into a
  changed tree. Today's carry-on shares this, so a line is enough.
- **N11. The page's words.** `limit.resumed`'s *reset at {{until}}* states the margin-added `Until`: say *ready again at*.
  `sessionState.limited` puts a non-state in the states' namespace. *现在换个继续…* is not grammatical: *改用其他选择继续…*. Keep 额度
  and 上限 apart as the glossary says. Say whether the line resumes in its own conversation or with Daoris's brief. Offer *Try now*
  beside the sentence.
- **N12. `pollSeconds` is set per machine.** The wake's latency is the setting, not 15 s. Correctness does not depend on it, so
  say that.
- **N13. Cite D189 point 6.** *A left conversation is not resumed again* is why *own* means the last part's account only; the
  design keeps the rule but does not cite it.
- **N14. §1's table** puts `starts.waiting account C` at 09:11:57, before C's `account.limited` at 09:11:59. Check the transcription.
- **N15. F6:** `ChatGoOn.cs:282-290` is the answers-alone bound's press, not a general *Go on*.

#### 4. What holds

The ledger's flag rather than a new state (`compatible-builds.md`). No *What needs you* row for a wait that settles by itself
(`person-waits.md`, *listed only with an act*). Own first, which is the owner's direction read exactly. An `Open` quest carried on
in its tree. The look as the driven wake, which the install showed working 6 s after the reset. No timer. D205's walk extended to a
driven line only at an end by a limit, never for a pick, a handed agent or an actor *as before*. The rows' order is right once 1c
also waits on the account-move seam (B3).

#### 5. Verdict

**Another round.** The shape is sound, and §3.6's extension of D205's walk is acceptable as a foundation with N3's dependencies
named. But B1 is the decision point of the design's one new driven behaviour, and B2-B4 are the safety and the person's control
around it. B6 and B7 are the steward's two install cases, which round 2 must cover anyway and which need a come-up rule this round
does not have. The read of round 2 can be narrowed to B1-B7's answers, N1 and N2.

## Codex, round 2

Codex's read of round 2 at `52f477767`, read-only. Round 3 answers it.

### 1. Claims about today’s code

Paths abbreviated below: `Driver/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `Web/` = `src/Daoris.Web/src/`.

**1. The quest page already has a reset-time presentation. Non-blocking.**  
The design’s `docs/2026-10-23-limit-resume-design.md:56-58` and D206’s `docs/decisions/D206.md:14` cite the quest page as showing no time. `Web/quests/QuestPage.tsx:696` also renders `sittingSentence`; `Web/data/signals.ts:236-243` supplies the account and reset time. Chinese explicitly uses `{{when}}` (`Web/locales/zh/work.json:173`); English passes through the driver’s reason, which already includes the first reset (`Driver/Accounts/Rotation/AccountRotation.cs:720-727`). The install observation may stand, but this source does not establish that absence.

*Smallest change:* distinguish the observed install from today’s rendering; describe the new benefit as a consistent, actionable sentence on the line.

**2. Three absolute statements need qualification. Non-blocking.**

- Design `:73` says any refused turn concludes failed. `Driver/Sessions/Domain/Observation.cs:100-115` gives an established wait or submission precedence over the refusal; the failed branches are specifically at `:121-126`.
- Design `:89-90` says a limit’s end never takes the own-resume path. `Driver/Loop/DriverContinuations.cs:40-44` does not exclude limited records: waiting words can already take that path.
- D206 `:47` still says the look wakes within the two-minute margin. `Driver/Loop/DriverWatch.cs:790-796` uses the configured `PollSeconds`, so that is no guarantee.

*Smallest change:* qualify these respectively as an unfinished `Open`/`Taken` run, a limit without waiting words, and the default polling interval.

**3. An opinion has no `Cooling` gate state. Non-blocking wording correction, necessary for row 1h.**  
Design `:375`, `:508` and D206 `:90-91` describe setting the gate `Cooling` so the existing re-ask runs. Today `Driver/Review/Domain/OpinionGate.cs:395` returns **`Unavailable` with a code and `Until`**. The re-ask requires exactly `State: Unavailable, Code: ReviewerUnavailable.Cooling, Until: reset` (`Driver/Loop/OpinionGateLook.cs:220-226`).

*Smallest change:* name that complete shape everywhere; do not introduce a new gate state.

### 2. Round 1’s blocking findings

| Finding | Verdict | Where | Why |
|---|---|---|---|
| B1 — decision point and refused full resume | Taken in part; open | Design `:182-220`; finding 4 | The start-time decision is concrete, but removing the sole automatic word still reaches a “nothing left” return before the briefed carry-on. |
| B2 — bound and TURNFAIL1 | Taken in part; open | Design `:230-246`, `:152-153`; finding 10 | One attempt per cool-off and limited words always waiting answer the original mechanism; a recognised limit still becomes a strike on a storage failure. |
| B3 — carriers, ordering and ended move | Taken in part; open | Design `:270-292`; finding 8 | The two classes of words and their order are clear; the reused move has no durable ownership transfer across a restart. |
| B4 — Stop, Ask Daoris and precedence | Taken | Design `:187-192`, `:349-361` | Pause supplies the driven control, Ask Daoris uses its own door, and the person’s press is explicitly first. These need competing-press proofs. |
| B5 — other kinds of work | Taken in part; open | Design `:374-378`; findings 3 and 9 | Intake is covered; the opinion shape needs correction. Brainstorm and partner deferrals have reasons that hold. The helper reason does not cover a lead waiting on that helper. |
| B6 — update close | Taken in part; open | Design `:397-418`; findings 4 and 6 | The restart intent and drain count are named, but keeping the word can dispatch it before shutdown, and a lost-context fallback can still stop. |
| B7 — missing kept id | Diagnosis answered; fallback open | Design `:422-443`; finding 4 | Never-spoken records need no restoration. A record that did work still needs an automatic briefed path when no personal word remains on `said`. |

### 3. Missing or unsafe behavior

**4. A wordless automatic fallback still stops before starting the briefed part. Blocking.**  
Design `:216-223` removes `limit.reset` before using the existing fallback; `:437-439` does likewise for the restart word. With no personal words waiting, `Driver/Loop/Driver.cs:2007-2012` returns `TakenBackFirst`. Leaving the automatic word instead reaches the prohibition on Daoris-only fallback at `:2039-2042`.

The conversation equivalent is equally explicit: `Driver/Conversations/Chat/ChatGoOn.cs:669` enters the lost-conversation move only with personal words waiting, and `BriefedMove.cs:97-100` refuses without them. A mid-turn update normally finds the original request in the events, already removed from `said`. Taking off the restart word leaves no trigger for the promised briefed restoration.

*Smallest change:* give limited and update-interrupted work an explicit automatic continuation intent, separate from personal queued words. Its fallback must compose the brief and start even with an empty personal queue, bypassing these guards only for that intent. Prove both driven and conversation cases with no new personal word.

**5. The cached conversation deadline can hide an account becoming ready early. Blocking.**  
Design `:257-262` answers N2 by computing a wait, but then avoids recomputation until the cached time arrives. If that time is tomorrow, a sign-in, *Try now*, a newly added account or an actor edit today leaves the cached comparison false. With no timed candidate at all, there may be no deadline to reach.

Those changes are real: `Driver/Accounts/AccountCooling.cs:93-104` and `:131-135` remove cool-offs early. The driver’s existing home topics do not include cooling or account changes (`Driver/Platform/HomeWatch.cs:196-208`).

*Smallest change:* specify which changes invalidate each cached wait and cause the next look to recompute it. Define the untimed-wait case and rescheduling after an unsuccessful wake. Prove an early sign-in, *Try now*, and an added eligible account while a future deadline is cached.

**6. Keeping the update word can send and consume it before the update. Blocking.**  
Design `:397-404` says to use `ChatTurns.KeepAsync` before stopping the process. That method does not merely persist: it calls `Take` (`Driver/Conversations/Chat/ChatTurns.cs:189-195`). `Take` starts the pump when idle (`:501-512`), and successful transmission removes the word (`:853-862`).

The interrupted turn can finish while the close prepares its words; the restart word then becomes a turn in the old process and disappears from `said`. `StopAll` subsequently stops that process (`ChatRunner.cs:1393-1405`). The next `TakeUpAsync` requires words waiting (`ChatGoOn.cs:209`), so the restoration can vanish.

*Smallest change:* define an update-close operation that prevents further dispatch, persists the restart intent without sending it, then stops the process. Pending starts and the new `limits` stage must obey that closing boundary. Prove a turn finishing during preparation and a door opening during preparation.

**7. Native id comparison happens after work has been handed over, and “no init” is not proven loss. Blocking.**  
Design `:293-298` promises no blind native resume by comparing `init` ids. A driven native resume already carries the prompt in its arguments (`Driver/Agents/Adapters/Adapters.cs:2123-2128`). A native chat writes the prompt to stdin (`ChatRunner.cs:2633-2658`); its capture later sees the named id (`:2049-2053`). Comparing that id cannot establish that no work ran without context.

The same paragraph also turns every end before `init` into `gone`. Today that event deliberately retains the distinction between a transient failed start and lost context: one retry, then `exited` (`ChatRunner.cs:2064-2077`; D189’s note at `docs/decisions/D189.md:166-170`).

*Smallest change:* require a measured native contract that fails closed on an unavailable resume before acting, or use a context-complete briefed path where that cannot be established. Apply it to driven resumes too. Preserve `exited` for unproven loss. An id-mismatch stand-in alone does not establish the real door’s behavior.

**8. The ended-conversation move can strand words between opening and delivery. Blocking.**  
Design `:282-292` reuses `LimitMove`’s body and says the waiting words leave the old record in that move. That body opens the linked replacement first (`Driver/Conversations/Chat/LimitMove.cs:106-112`), then hands an in-memory message to `SayHandedOver` (`:121-124`). `SayHandedOver` uses `Turns.Take`, not durable keeping (`ChatHandOver.cs:238-247`).

A restart after the replacement opens but before its prompt is recorded leaves the old record already continued. Recovery skips that old record (`ChatGoOn.cs:206-207`), while the replacement may have neither the refused words nor waiting words. Taking waiting words off earlier makes the loss larger; a successful queue admission is not delivery.

*Smallest change:* define one durable handoff owner for the brief, refused words, waiting word ids and files. Recovery must reconstruct an opened-but-undelivered continuation before suppressing its predecessor. Remove old queued words only at the specified delivery boundary. Prove interruption before delivery and around acknowledgement, not just successful prompt contents.

**9. The helper deferral assumes away a waiting lead. Blocking.**  
Design `:376` justifies no automatic recovery because the asker “is a live session that has moved on.” HELPERWAIT1 explicitly supports the opposite: a driven lead ends waiting on its own open helper (`Driver/Loop/DriverHelpers.cs:165-171`). Once the helper becomes refused with no answer, that lead is brought back to a person’s park (`:368-383`). Rewording the refusal as limited therefore still turns an account limit into manual intervention.

*Smallest change:* distinguish a lead still waiting on this helper from an asker that has moved on. Keep the former helper ask open, try an eligible alternative, otherwise wait for its reset and retry only while the lead still wants the answer. Defer automatic recovery for the latter case with that narrower reason.

**10. A recognised limit still counts a strike when cooling cannot be persisted. Blocking.**  
Design `:152-153`, `:234-235` and D206 `:21` expressly preserve this exception. The implementation confirms its consequence: `Driver/Loop/Driver.cs:2826-2829` returns `Limit = false`; `Driver/Service/ServiceClient.cs:2034` counts the failure. A healthy line can consequently exhaust its strikes solely because its account was limited and the cooling write failed.

This contradicts the requested “never a failure or a strike” and D125 point 3 (`docs/decisions/D125.md:23-24`). Preventing a spin is necessary, but quest strikes are the wrong bound.

*Smallest change:* separate recognising the limited end from successfully scheduling its retry. Preserve the limit classification and hold automatic retries on the persistence problem until a safe cooling record exists.

### 4. Amendments, build order and proof

**11. The actor amendment leaves contradictory controls and consent text. Blocking.**  
Design `:319` says `handOver: off` holds a driven line “as it holds a conversation,” while `:268-269` says D205’s conversation rule is unchanged. Accepted D205 expressly says `off` **does not** stop its automatic conversation move (`docs/2026-10-23-actors-design.md:539-540`).

The new driven behavior also invalidates D205’s acceptance words and Build editor: “a quest’s session never does” and “Running work never moves to another choice” (`:905-915`, `:1025-1029`). LIMITRESUME1’s amendment list changes the walk but does not replace those words.

*Smallest change:* state the driven and conversation meanings of `off` separately, amend D205 accordingly, and update the editor/card wording with the new limited-line behavior. Prove both `off` cases and that picks and handed agents retain their restrictions.

**12. Row dependencies and observational proofs need tightening. Non-blocking plan correction.**  
Design `:509` makes 1i depend only on 1b, although its cooling-account restoration relies on 1c’s conversation wake. The table calls itself build order but places 1g before its prerequisite 1i (`:507-509`). Section 3.6 requires the all-makers independence change before 1e (`:317-319`), but 1e’s dependencies do not identify its delivering row.

Finally, `:519-522` assigns real native-door behavior to a stand-in proof. A stand-in demonstrates the implementation’s reaction to an assumed shape. Likewise, absence of `session.moved` in 1g does not prove the original harness conversation resumed.

*Smallest change:* add 1c before 1i, order the rows topologically, assign the maker-independence prerequisite, and separate stub assertions from real-door evidence. The installed resume proof must observe the same harness conversation id on the same account; the native proof must establish refusal behavior before relying on it.

**12 findings: 8 blocking, 4 non-blocking.**

**Verdict: another round.** The ledger flag, own-account-first rule, brief across accounts, word ordering, and existing look remain sound foundations. Another round should check findings 4–11: automatic fallback without personal queued words, cache invalidation, an update-close operation that cannot consume its restart intent, native context safety, durable conversation handoff, the waiting-helper case, storage failure without strikes, and the actor control/consent amendment. Findings 1–3 and 12 are concrete corrections the steward can check directly.

## Opus (foundation), round 2

The independent Opus reader's foundation read of round 2, read-only. Round 3 answers it.

### Opus's foundation read of LIMITRESUME1's design (D206, round 2)

Read on the design's branch at `52f477767` (round 2). The code there is main's as at `2d2aedca0`, with LIMITGOON1 merged; no
file under `src/` has changed since. Read whole: `docs/2026-10-23-limit-resume-reads.md` (round 1), the design with its §8
table, `docs/decisions/D206.md` with its round-2 note, and `git diff d1e509c7f HEAD` of both. Also read: D125, the D205
sections the design amends (`docs/2026-10-23-actors-design.md` §3.5(a), (e) and (f)), and `.claude/knowledge/session-words.md`.
I checked the code at file:line. Nothing was run. The install's machine log and its `data/sessions/` were not read, so §1's
table and §3.10's diagnosis are taken as written. Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they say
otherwise, as the design's are.

**Counts.** 37 facts round 2 states or newly relies on were checked. 31 hold. 6 need correcting (F8-F13). Of round 1's
findings, F1, F3, F4, F6 and F7 are corrected. F2 and F5 are corrected in their words but each leaves a seam open, and those
two seams are B8 and B9. B2, B4, B5, B6 and B7 are taken. B1 is taken at its decision point but is open on its refusal path
(B8). B3 is taken for driven lines and for the move when another account frees first, and is open for a lost conversation (B9).
N1 is taken. N2 is taken in its key, but the in-memory wait misses its own cases (N16). This round has **2 blocking** findings
(B8, B9) and **14 not blocking** (N16-N29). **Verdict: accept after the named changes in §6.** The steward can check each one
against the lines cited here without another read.

#### 1. Facts round 2 states, checked

| Claim (design §) | Code | Holds? |
|---|---|---|
| A limited cut-off is planned as a `CarryOn` whose `Resumes` is the limited record, and `PriorSession` carries `Limit` (§3.3) | `Quests/Domain/Planner.cs:1329-1343`, `:1507-1540`, `:264` | Holds |
| Step 1's seam sits beside `ResumesOwn`, with `ResumeAsync` asked for the record's own account (§3.3) | `Loop/Driver.cs:1716-1736`; `Loop/DriverContinuations.cs:40-45` | Holds. `ResumesOwn` needs `WordsWaiting`, so step 1 is a branch of its own (N18) |
| The judge answers `ended` with no words waiting (§3.3) | `Conversations/Domain/Continuation.cs:217` | Holds |
| The say door takes Daoris's word under a key, as `RebaseTelling` does, and `DaorisKeys.Read` names no fact for it (§3.3) | `Trees/Landing/RebaseTelling.cs:16-28`; `Conversations/Domain/DaorisWords.cs:138-150`; `Service/ServiceClient.cs:464-499` | Holds. `limit-reset/{session}/{until}` has no numeric last segment, so it reads as no fact |
| The re-read is `HandedOnRecordAsync`, and `ContinueAsync` runs only for a start that goes on (§3.3) | `Driver.cs:2007`, `:1986-1995` | Holds |
| `CannotGoOn` for Daoris's words alone (§3.3) | `Driver.cs:2035-2043` | Holds |
| **F8.** *Where the refusal is `elsewhere` … the line holds as today (`:2020`)* (§3.3) | Today's hold is a mark on the waiting words: `CannotGoOn` marks them (`DriverContinuations.cs:767-782`) and `HeldForPerson` reads the mark (`Planner.cs:1359-1361`). `GoOnMarks.Mark` writes nothing for no ids (`Conversations/GoOn/GoOnMarks.cs:57-58`), and `Judged` needs words waiting (`:125-128`) | **It does not hold once the word is taken off** (B8) |
| **F9.** *Taken off … by the service's taken door, by Daoris* (§3.3) | The door's `by` names the session that took the words (`ServiceClient.cs:556-582`). The service keeps only the person's taken words on the ask (`src/Daoris.Service/Daoris.Service.Http/Program.cs:1511-1522`) | Imprecise: there is no *by Daoris* (N24) |
| `said` keeps keep-time order (§3.3) | `DriverContinuations.cs:254` | Holds |
| **F10.** *What follows is the plain carry-on* through the driver's fallback and `CarriedOnAsync` (§3.3 steps 1-2) | The fallback re-reads the record and returns `TakenBackFirst` when no word waits (`Driver.cs:2007-2012`), before `CarriedOnAsync` (`:2051-2077`) | **Not reached when no word is left** (B8) |
| TURNFAIL1 marks the words on the second failed turn running, and `AccountLimited`'s second value is at `:554` (§3.3 *bound*) | `Sessions/Domain/Observation.cs:455-476`; `DriverContinuations.cs:554-566` | Holds. The wait line's code is what the next judge reads (N20) |
| The maker's reading is asked after a cool-off (`ProbeWhy.Limit`), and USAGEONE1's skip applies to a resume (§3.3 step 1) | `Accounts/Harnesses/HarnessRoster.cs:1895-1910`, `:1070-1079` | Holds only where the agent's door carries none of its windows (`:1070`, the freshen's own condition) (N21) |
| `LimitHeld.Of` is the refused turns' only reader, from the events (§3.5) | `Conversations/Domain/LimitHeld.cs:17-56` | Holds. It answers only while the last turn is limited (`:35-55`) (N26) |
| **F11.** The seam that moves an ended conversation is `LimitMove`'s body with the hold skipped (§3.5) | `Conversations/Chat/LimitMove.cs:49`, `:92-104`, `:109-111`, `:124` hold. The body also refuses the record's own account: the same account, or none, *stays* (`:81-84`) | Holds for another account. It is no seam when the own account frees first and the conversation is lost (B9) |
| **F12.** A conversation that took turns and kept no id goes by the briefed move, `LostAsync` (§3.10 point 2) | `Conversations/Chat/ChatGoOn.cs:392-396`, `:663-688`. `BriefedMove` hands only the person's words on `said` and refuses `NoWords` otherwise (`Conversations/Chat/BriefedMove.cs:96-100`). `LostAsync` then reaches `CannotGoOn` (`ChatGoOn.cs:669-687`) | **Not for a conversation held by a limit**, whose refused turns' words are in the events only (B9) |
| The native door resumes by `--resume <id>` (§3.5) | `Agents/Adapters/Adapters.cs:2115-2127` | Holds |
| D163's offer refuses Ask Daoris's own conversation, and `HelpDoor` is its door (§3.7) | `Conversations/HandOver/HandOver.cs:51-53`, `:188-190`; `Accounts/Rotation/ResumeAccount.cs:233-237`; `docs/2026-10-23-actors-design.md:507-508` | Holds |
| **F13.** Step 0 is read *in this order*: a pause, a stop, a hand-over, a new session, the person's words (§3.3) | The planner plans the pause, then the words, then the stop. Words release a stop (`Planner.cs:1278-1300`) | Imprecise in its order and in where each is read (N17) |
| `ReopenTelling` guards a person-stopped record (§3.7) | `Loop/ReopenTelling.cs:238-244` | Holds |
| Every other kind passes a cooling account at its start (§3.8) | `Loop/DriverIntakes.cs:124`, `:134`; `Review/Domain/ReviewerChoice.cs:326`; `Loop/DriverBrainstorms.cs:660-661` | Holds |
| None counts a strike, since `ReadStrikes` counts only records with a quest (§3.8) | `ServiceClient.cs:2037` | Holds |
| A limited intake ends `failed` with the flag, and its ask is used up (§3.8) | `DriverIntakes.cs:79`, `:273-276` | Holds. An intake that filed quests ends `completed` and is never limited (`Quests/Domain/Intake.cs:305-316`), so picking the ask again cannot file them twice |
| A limited opinion is `Unavailable` with `Why ?? "ended"` and no `Until`, and the re-ask needs `Cooling` with an `Until` passed (§3.8) | `Review/Domain/OpinionGate.cs:395`, `:400`; `Loop/OpinionGateLook.cs:220-221` | Holds (N25) |
| A helper is refused `NoAnswer` (§3.8) | `Loop/DriverHelpers.cs:631-637`, `:539-548` | Holds |
| A brainstorm's one repair and a partner's `Ending` (§3.8) | `DriverBrainstorms.cs:598-609`, `:723-727`; `Loop/DriverPartners.cs:620-623`, `:328-331` | Holds |
| The drain is idle when no driven session runs and no turn runs, `Taking` counts only a running turn, and *Update now* applies at once (§3.9) | `src/Daoris.Desktop/Daoris.Desktop.Modules/Update/InstallUpdater.cs:225-240`; `Setup/InstallUpdate.cs:74`; `Conversations/Chat/ChatRunner.cs:1604`; `src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/DriverLoop.cs:334-340`; `Conversations/Chat/ChatContext.cs:63` | Holds |
| The close is `StopAll` with `chat.closed`, concluded `stopped` with no `interrupted` (§3.9) | `ChatRunner.cs:362-366`, `:1389-1415`, `:2097-2103`, `:2707-2713` | Holds. The driver's stop leaves the record's kept words on it (`Conversations/Chat/ChatTurns.cs:746-761`) |
| A driven session closed by the driver is `stopped` and interrupted, a strike, and is carried on (§3.9) | `Driver.cs:2500-2514`; `ServiceClient.cs:2034-2035`; `Planner.cs:1935-1938` | Holds |
| `TakeUpAsync` takes ended chats with words waiting, and the sweep takes only interrupted ones (§3.9) | `ChatGoOn.cs:174-233`, `:119-150` | Holds. Both settle attempted words first (`:210-216`, `:135-139`) |
| A note part's code passes `ReadStrikes`, as `AccountRefused` does (§3.9 point 4) | `ServiceClient.cs:2412-2414` | Holds |
| The update's close (§3.9) | The updater's `close` is `MainForm.CloseForGood()` (`src/Daoris.Desktop/Daoris.Desktop.App/Program.cs:180-182`; `InstallUpdater.cs:78-79`), the person's close | Holds as stated. Nothing tells the close it is an update, or which build (N22) |
| The native door names its conversation at its `init` line (§3.10) | `ChatRunner.cs:2049-2054` | Holds |
| The write is swallowed, and `NotKept` is added only where no stop reason stands (§3.10) | `Conversations/GoOn/HarnessConversations.cs:35-42`; `ChatRunner.cs:2101-2102` | Holds |
| `Cool` replaces an entry whole (§3.2) | `Accounts/AccountCooling.cs:84-90` | Holds |
| The walk's filters (§3.2) | `ResumeAccount.cs:28-36` | Holds |
| `SessionEnded` carries no limit flag (§3.1) | `Driver.cs:2494-2497` | Holds |
| D125 point 3 and its rejection of a separate count (§3.3 *bound*) | `docs/decisions/D125.md:23-24`, `:62` | Holds |

#### 2. Round 1's findings, as round 2 answers them

| Finding | Round 2's answer, checked | Standing |
|---|---|---|
| F1 | The word goes through the say door under its own key, as `RebaseTelling` does | Corrected |
| F2 | The word is taken off before the fallback, and the refusal's own code is said | Corrected in words. The fallback that follows carries nothing on, or holds nothing (B8) |
| F3 | The driver's fallback and `CarriedOnAsync` | Corrected |
| F4 | The margin-added `Until`, said as *ready again at* | Corrected |
| F5 | `LimitMove`'s body for an ended record, within its agent | Corrected for another account. Open where the own account frees first and the conversation is lost (B9) |
| F6, F7 | `AnswerGoesOn`, and `pollSeconds` | Corrected |
| **B1** | Decided inside the carry-on's own start, before the walk, and judged as if words waited. The word goes through the say door and comes off before any fallback | **The decision point is taken**: the own account is asked before the walk, so the install's 9967271d cannot recur. **The refusal path is open**: after the take-off the fallback returns with nothing carried on, and the next look spawns again; `elsewhere` holds nothing; and the word outlives a limited conclusion, which brings back `CannotGoOn` (B8) |
| B2 | One attempt per cool-off, inside D125 point 3. Limited words always `Wait`. `notLifted` is said with no park. The maker's reading is asked | Taken. N20 (the wait line's code) and N21 (a reading exists only for some agents) |
| B3 | One carrier per word, and Daoris's word last | Taken for a driven line, and for the move when another account frees first. Open for a lost conversation (B9). The order sentence cannot be reached in step 1 (N19) |
| B4 | *Pause* for a driven line, `HelpDoor` for Ask Daoris, and a press since the end outranks the resume | Taken. N17 (step 0's order) and N27 (a conversation's *Stop* on an ended record) |
| B5 | Intakes and opinions are covered; helper, brainstorm and partner are deferred with their reasons | Taken. The reasons hold. N25 |
| **B6** | The restart word is keyed by build, and the come-up resumes by the kept id. An idle conversation gets no word and is said `chat.restarted`. Opening counts in the drain. *Update now*'s driven stop counts no strike | **Taken.** The mechanism carries case 1: the driver's stop keeps the record's words (`ChatTurns.cs:746-761`), and `TakeUpAsync` resumes an ended chat that has words waiting by its kept id. Two seams remain to name: the close does not know it is an update (N22), and the word can be dispatched before the stop (N23). A pending start that kept no id falls under B9's take-off |
| **B7** | The four records never opened, the brief is the named fallback, `conversation.unkept` is logged, and `chat.not-kept` is said beside a stop | **Taken.** The fallback named for a conversation held by a limit is the wrong seam (B9). N28 (`help resume` on a never-spoken record) |
| N1 | The `init` id is compared with the kept one, and a mismatch is `gone` | Taken |
| N2 | Keyed to the computed wait | Taken in its key. The in-memory earliest wait misses *Try now*, a sign-in and an added account (N16) |

#### 3. What round 2 introduced in the foundation

- **The loop's bound inside D125.** The bound is one attempt per cool-off, each refused attempt writes a new cool-off, and there
  is no count. This keeps D125 point 3 and its rejection of a separate count. The TURNFAIL1 reconciliation is right in kind:
  `FailedTurnWords.Judge` takes `AccountLimited`'s flag. Its line code still counts as a first failure (N20). The bound does not
  cover a resume refused on the wire with no limit met (`gone`, `exited`, `refused`, `elsewhere`). Those write no cool-off, so
  under §3.3's text they repeat at every look (B8).
- **The single carrier.** For a driven line it holds: the person's words live on `said`, and Daoris's word is kept and taken by
  its id. For a conversation it holds where the move is `LimitMove`'s (another account frees first), because that body reads
  `LimitHeld.Of`. It fails where the own conversation is lost, since every seam named for that case reads `said` only (B9). The
  order paragraph in step 1 cannot be reached (N19).
- **The seam that moves an ended conversation.** `LimitMove`'s body with the hold and `EndForHandOverAsync` skipped is sound for
  another account of the same agent. It needs no row e and no LIMITGOON2, as the design says. It stays on the own account by
  construction (`LimitMove.cs:81-84`), which is exactly the case B9 needs it for.
- **The person's presses.** *Pause* is the right press for an ended driven line, and the planner reads it first. `HelpDoor` is
  Ask Daoris's door. *A press since the limited end outranks the resume* is right, but in the code it is mostly the planner's
  verdicts, which a start never sees (N17). The conversation half has the same *Stop on an ended record* problem that B4 found
  for driven lines (N27).
- **Scope.** Intakes and opinions are covered, and their seams exist or are named (D205 (f)'s door; the gate's `Cooling`).
  Helpers, brainstorm voices and partners are deferred with reasons that hold. I checked one risk and found none: an intake that
  filed quests is never limited, so picking its ask again cannot file them twice.
- **The conversation wake beyond a cool-off's end.** It is keyed to the computed wait, which is right. But keeping it in memory
  undoes N2's sign-in and added-account cases, and also *Try now*, the press §3.7 puts beside the sentence (N16).
- **Strikes.** `update.closed` read as a note code is the right shape, with no new column. It needs the seam that tells the
  close it is an update (N22).
- **Rows and proofs.** 1b's first proof is the install's case, which is right. 1i after 1b is right, since it reuses step 1.
  The proofs lack B8's and B9's cases, named below.

#### 4. Blocking

**B8. Step 1's refusal path: the word comes off, and then nothing carries the line on or holds it.**

*Design.* §3.3 step 1: *On any refusal, `limit.reset` is taken off the record by its id … before the fallback, so what follows
is the plain carry-on with the person's words alone … Where the refusal is `elsewhere` … the line holds as today (`:2020`).*
§3.3 *The bound*: *a limited conclusion is always `WordsAfter.Wait` … The words stay waiting, unmarked, and go with the next
attempt.*

*Code.*

1. **Step 1 runs only where no unmarked word waits.** Otherwise the planner plans `GoOn` (`Planner.cs:1287-1291`, step 0). So
   once Daoris's word is taken off, the record holds no word. A resume refused on the wire (`gone`, `exited`, `refused`) is put
   back as it ended (`DriverContinuations.cs:494-511`). The fallback then re-reads it (`Driver.cs:2007`) and, with nothing
   waiting, returns `TakenBackFirst` (`:2008-2012`). Nothing is carried on. The next look plans the same `CarryOn`
   (`Planner.cs:1341-1343`). The judge passes again, because nothing forgets a kept id on `gone`
   (`Conversations/GoOn/HarnessConversations.cs` has no removal). The word is kept again under the same key, a process spawns,
   and the resume is refused again. This repeats at every look. The bound does not stop it, because no limit is met. If the
   take-off comes after `:2008` instead, the local record still names Daoris's word, and `:2039-2042` returns `CannotGoOn`. That
   is F2's case.
2. **`elsewhere` holds nothing.** Today's hold is a mark on the waiting words (F8). With the word taken off,
   `GoOnMarks.Mark(..., [], ...)` writes nothing, `HeldForPerson` reads no mark, and the line spins as in item 1, writing a
   *cannot go on* line each look.
3. **The word outlives a limited conclusion.** The always-`Wait` rule applies to `resume.Ids` (`DriverContinuations.cs:559-566`),
   and those include Daoris's word. So the word stays waiting on a `failed` record that has the flag. `WordsWaiting` counts any
   word (`Planner.cs:350`), so the next look plans `GoOn` (step 0), not step 1. Suppose the own account is cooling again and
   another account frees first (step 2's change to `Waits`). That `GoOn` then falls back with Daoris's word alone, reaches
   `CannotGoOn` (`Driver.cs:2039-2042`), marks the word, and says the line cannot go on. The line goes on briefed one look later,
   with the marked word left on the record. A later step 1 keys a second word by the new `until`, and `ContinueAsync` clears the
   marks and hands both (`DriverContinuations.cs:126-130`, `:254`).
4. **Where the word is kept.** §3.3 keeps it at `:1716-1736`. That comes before the holds at `:1764-1978` (access, tree,
   replay, trust). A hold after the keep leaves the word waiting, and item 3's path follows.

*Smallest change*, written into §3.3 step 1 and D206's note:

1. Ask the own account and the judge at `:1716-1736`. Keep the word only immediately before `ContinueAsync` (`:1987`), after
   every hold.
2. When a step-1 start's resume is refused, before or on the wire, with any code but `elsewhere`, take the word off by id and
   go on **in the same start** as the `CarryOn` that was planned. That means the walk through `CarriedOnAsync` (`:2051-2077`)
   and the brief, never the `TakenBackFirst` return at `:2008` or the Daoris-only `CannotGoOn` at `:2039`. Say the refusal's
   code.
3. On `elsewhere`, keep the word and mark it `elsewhere`, as today's words are marked. `HeldForPerson` then holds the line for
   the person, and it is said once.
4. A resumed run's conclusion takes Daoris's limit word (and §3.9's restart word) off by id along with the run's words,
   whatever the conclusion. The always-`Wait` rule applies to the person's words alone. A step-1 word then never reaches `GoOn`,
   a fallback, or a second key.
5. Row 1b's proofs gain three cases, each in the driver's stand-in test:
   - a resume refused `gone`: a briefed part on the walk's account in the same start, no word left, and the next look spawns
     nothing for that record;
   - a resume refused `elsewhere`: the line is held for the person, and no process spawns at the next look;
   - two limited full resumes running, then another account free: no *cannot go on* line, at most one Daoris word on the
     record, and none handed to the briefed part.

**B9. A conversation held by a limit whose own conversation is lost has no seam that carries the person's request.**

*Design.* §3.5: *the refused turns' words stay in the record's events … `LimitHeld.Of` stays their only reader.* §3.10
point 2: *a conversation that took turns and kept no id goes by the brief … the briefed move for a conversation
(`ChatGoOn.cs:392-396`, `LostAsync` `:663-688`) … `limit.reset` … taken off by id before the fallback.*

*Code.* At the wake the own account is ready, and the judge or the wire says the conversation is lost (`gone`, `unkept`,
`unable`, `offered`: `Continuation.cs:46`). `GoOnAsync` hands the case to `LostAsync` (`ChatGoOn.cs:392-396`). `BriefedMove`
hands only the person's words on `said` and refuses `NoWords` where there are none (`BriefedMove.cs:96-100`). After Daoris's
word comes off, there are none, because the refused turns' words are in the events alone. `LostAsync` then reaches
`CannotGoOn` (`ChatGoOn.cs:669-687`), which marks whatever waits and says the conversation cannot go on. The one body that reads
`LimitHeld.Of`, `LimitMove`'s, refuses the record's own account (`LimitMove.cs:81-84`). So the person's request, held since the
limit, goes nowhere. This is the shape of the owner's LIMITGOON1 report. The same holds for every ChatGoOn refusal path
(`LostAsync`, `CannotGoOn`, `WentBackAsync`) when Daoris's word alone waits (a limit word, or §3.9's restart word on a pending
start that kept no id): `CannotGoOn` marks it and says *cannot go on*. §3.10's take-off rule is named only for §3.3's driver
path.

*Smallest change.* Name the fallback for a conversation held by a limit whose conversation is proven lost (`reason.Lost`). It
is `LimitMove`'s ended-record body, with the record's own account allowed for that reason alone (relaxing `:81` for it). It
hands the brief composed `before: held.Before`, then the refused turns' words, then the words said while waiting, each once,
with the waiting words taken off by their ids. Every ChatGoOn refusal path takes Daoris's word off by id before it falls back,
and a non-lost refusal (`exited`, `refused`) waits for the person as today. Row 1c's proof gains this case: own account ready, a
stand-in that answers `resource_not_found`, and the result is a new part with the brief and the refused turns' words, never
*cannot go on*.

#### 5. Not blocking, in order of weight

- **N16. The wake's in-memory wait (§3.4) misses N2's own cases.** It is computed when the hold is written. *Try now*
  (`AccountCooling.End`, `AccountCooling.cs:93-104`), a sign-in (`:131-135`) and an added account all make an account ready
  without changing that number. *Try now* is the press §3.7 puts beside the sentence, so pressing it would not wake a held
  conversation until the stored moment. Drop the cached wait when the cooling store or the account list changes (one
  modified-time read per look does it).
- **N17. Step 0's order and place.** The planner plans the pause, then the words, then the stop, and the words release a stop
  (`Planner.cs:1278-1300`). The pause, the stop and the words are the planner's verdicts, so a start never sees them. A
  `failed` record is never `PersonStopped` (`Planner.cs:254`), so no stop can hold a limited line. In the start, step 0 is the
  hand-over made from the limited record (`HandedFrom`, `Planner.cs:1403-1404`). An older hand-over is the line's agent, not a
  press since the end. Say so, in that order.
- **N18. Step 1's guard.** A `GoOn`'s `Resumes` is the same limited record (`Planner.cs:1459-1473`), so the branch reads
  `!start.GoesOn && start.Resumes is { Limit: true }`.
- **N19. The order paragraph cannot be reached in step 1.** When the person's words wait, the planner plans `GoOn` and step 1
  is not tried. A driven full resume therefore carries either the person's words without Daoris's word (`GoOn`) or Daoris's word
  alone (step 1). D206 point 3.i and its note promise both. Choose one. Either a `GoOn` on a limited record whose own account
  resumes it also keeps the limit word, after the person's words (the shape's *the person's held words go with it*), or the
  paragraph says step 1 carries Daoris's word alone.
- **N20. TURNFAIL1's line code.** The limited wait's line is `ended.words-wait` (`Observation.cs:470-472`), and both
  `FailedTurnWords.Judge` (`:457`) and `Continuations.Unstarted` (`Continuation.cs:336-338`) read it as the first failure. After
  one limited end, then, a single ordinary failure, or a resumed run that cannot start, marks the words *failed twice*. Give the
  limited wait a code of its own that neither reads.
- **N21. The maker's reading exists for some agents only.** The freshen asks only where the agent's door carries none of its
  windows (`HarnessRoster.cs:1070-1079`). For an agent whose door carries its windows (Claude Code), there is no reading to ask,
  and the attempt itself is the probe. Say so. §7's third claim then covers only the agents that do have a reading.
- **N22. The update's close is the person's close.** The updater calls `MainForm.CloseForGood()` (`Program.cs:180-182`).
  Nothing tells `StopAll` (`ChatRunner.cs:1389-1415`) or the driver's close (`Driver.cs:2500-2514`) that this is an update, or
  which build is staged. Both `DaorisKeys.Restart(session, build)` and `update.closed` need that seam: name it. Also say what a
  person's close or restart does to a turn in flight (the install's 05:28): unchanged, the turn lost, no word.
- **N23. The restart word can be dispatched before the stop.** `ChatTurns.KeepAsync` takes the word as the next turn
  (`ChatTurns.cs:189-196`). If a turn ends between the keep and the stop, the word is sent to the agent and counts as attempted.
  At the come-up an attempted word is settled as uncertain and never sent (`ChatGoOn.cs:210-216`, `session-words.md`), and the
  conversation does not come back. Hold the turns first, as `LimitMove.cs:92` does, or keep the word through the say door once
  the watch has concluded the record `stopped`. The driver's stop leaves record words on it (`ChatTurns.cs:746-761`).
- **N24. The take-off door (F9).** Name the call: `TakenAsync(session, [id])`, whose `by` names a session, not Daoris. Also
  name what the conversation shows for a Daoris word that was kept and then taken off unsent.
- **N25. A limited opinion pass has no `Until` on its gate.** The pass opened its opinion, so the gate reads `first.Why` with no
  `Until` (`OpinionGate.cs:400`). It does not reach the ask's `Until` (`:395`). Name where row 1h reads the `Until` from: the
  opinion's record, or the cool-off entry by its session.
- **N26. The refused turns' words have one carrier, which lasts only while the last turn is limited** (`LimitHeld.cs:35-55`).
  A wake turn that ends with an error that is not a limit drops them, after one send. Say this is a failed turn's loss, as in
  any conversation, or keep them for one more turn.
- **N27. A conversation's *Stop* (§3.7)** has no target once the held conversation has ended (after a close or the sweep):
  B4's finding, on the conversation half (`src/Daoris.Service/Daoris.Service.Core/Sessions/SessionLedger.cs:1446-1451`). Name
  its press there (the words marked and the wait dismissed), or say that an ended held conversation offers *Go on now with…*
  and *Try now* only.
- **N28. `help resume` on a never-spoken conversation still answers `unkept`.** §3.10 point 1 keeps that case, and the install's
  four records were it. Say *it never took a turn* there, so the person is not told an id was lost when none existed.
- **N29. Records and words.** D206 point 4 still reads round 1's trigger (*only where a cool-off ended since the last look*), and
  the round-2 note re-reads points 3, 5 and 7 but not 4: add a bullet. `work.limit.waitsChoice`'s zh text, 已达额度上限, joins
  the two words the glossary keeps apart. The UI's own phrase is 已达上限 (`src/Daoris.Web/src/locales/zh/harness.json:116`).

#### 6. What holds, and the verdict

**What holds.**

- Round 1's *what holds* stands.
- Step 1 is decided inside the carry-on's own start, before the walk, so own-first is real and the install's 9967271d cannot
  recur.
- The bound is one attempt per cool-off, with no count, inside D125 point 3.
- Limited ends always wait.
- `notLifted` is said with no park.
- One carrier per word for driven lines.
- `LimitMove`'s body is the seam to another account of the same agent, needing neither row e nor LIMITGOON2.
- *Pause* is a driven line's press, and `HelpDoor` is Ask Daoris's.
- The scope's covered and deferred kinds stand, each with a reason that holds.
- The update's restart word is carried by the kept id, an idle conversation gets no word, and opening counts in the drain.
- `update.closed` is read as a note code, with no column.
- The diagnosis is that the four records were never spoken.
- §3.6's reach is unchanged and foundational, with N3's dependencies named.

**Verdict: accept after named changes.** The steward checks each against the lines above, with no further read:

1. **B8**, items 1-5. The word is kept just before `ContinueAsync`. A refused step 1 carries on in the same start as its planned
   `CarryOn`. `elsewhere` keeps the word, marked. A conclusion takes the word off. Row 1b gains three proofs.
2. **B9.** For a limit-held conversation proven lost, the fallback is `LimitMove`'s ended body, own account allowed. Every
   ChatGoOn refusal path takes Daoris's word off. Row 1c gains its proof.
3. Recommended in the same pass, since each is one sentence: **N16** (drop the in-memory wait on cooling and account changes),
   **N19** (choose where Daoris's word goes in a `GoOn`), **N20** (a code of its own for the limited wait), **N22** (the
   update close's seam), and **N29**'s D206 bullet. The rest can wait for their rows.
