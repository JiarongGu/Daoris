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
