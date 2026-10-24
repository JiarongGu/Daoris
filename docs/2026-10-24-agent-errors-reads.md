# AGENTERRORS1: the reads of rounds 1 and 2 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-agent-errors-design.md` and D210 at `6f089fe1f`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on the design's own tree. Codex asks another round; the Opus foundation reader would accept after named changes (16 findings, 3 blocking, and three more to take before acceptance). Round 2 answers both; its two reads follow theirs, Codex asking another round (11 findings, 8 blocking) and the Opus foundation reader accepting after named changes (11, 2 blocking). Round 3 answers those.

## Codex

Codex's read of round 1.

### 1. Claims about the code

The main diagnosis holds: ACP failures lose structured information at `Loop/Driver.cs:4325`; native results do not populate `turnFailed`; ordinary turn failures can count strikes and lead to briefed carry-ons.

These claims need correction:

| Design claim | What the code actually does |
|---|---|
| ACP `-32000` means access on every door (`docs/2026-10-24-agent-errors-design.md:182`) | The repository’s ACP stand-in uses it for arbitrary work failures (`tools/rehearsal-kit.mjs:889`). `Accounts/SignIns/AccountSignIns.cs:19` explicitly documents this counterexample. |
| Shape 2 is handled by the “steer” catch at `Acp.cs:878` (design:183) | That catch handles an additional `session/prompt`. Explicit `_session/steering` failures are swallowed separately by `SteeredAsync` (`Agents/Acp/Acp.cs:1396`). Both paths need coverage. |
| Existing `Unreached` can supply words to an in-process retry (design:233) | It is populated during final cleanup, which also clears the outstanding prompts (`Agents/Acp/Acp.cs:1094`). It is not an intermediate retry checkpoint. |
| A mark keeps a failed line waiting for the person, like `FailedTwice` (design:255) | Marks suppress `WordsOn`, but the carry-on guard recognizes only three specific reasons (`Quests/Domain/Planner.cs:1411`). `FailedTwice` is not one. |
| The native run ends with each result (design:230–232, applied to chats at :335) | Native chats accept repeated stdin messages on one process (`Agents/Adapters/Adapters.cs:2262`; `Conversations/Chat/ChatRunner.cs:3048`). |
| The watcher reads `background.waits` as an exempt wait (design:246–248) | It reads an `OwnWork` callback, grants longer quiet thresholds, and still permits `--quiet-end` (`Sessions/Watch/SessionWatch.cs:322`, :342). |

Driver paths above and below are relative to `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

The installed incidents and external adapter-source claims remain supplied evidence; I did not access private installation records or make network calls.

### 2. The mechanism’s holes

The four-kind interface is the right boundary, but classification and permission to retry need separate answers. A transient failure establishes why a request ended; it does not establish which words were accepted, which effects completed, or whether the work still needs another turn.

D206 composition needs explicit transition proofs. In particular:

- A transient failure during a limit resume must preserve the current conversation without replaying `limit.reset` or the person’s already accepted words.
- A retry that encounters a limit must leave the transient episode, cool once, and reach LIMITGOON1/LIMITGOON2 or the driven limit planner once.
- Exhaustion must suppress both word-based resumes and ordinary failed-quest carry-ons.
- A later person-authorized retry needs defined behavior when the account, conversation, or requested choice is unavailable. D205’s walk must not become an accidental escape from the transient bound.

Run coverage is also more than a shared capture function. Driven sessions have a planner; Ask Daoris and chats have a turn pump; helpers and opinions have pass deadlines and durable answers; intakes can already have published; partners retain work; brainstorm voices have a frozen membership and deadline. Each owner must decide whether another turn is still appropriate before invoking the common retry policy.

### 3. The rows

| Row | Can it land independently, and does its proof suffice? |
|---|---|
| **1a** | It targets the observed incident and is the fastest useful win. Its proof needs accepted-word/effect checks, concurrent-prompt cases, watcher coverage, and subsequent looks after exhaustion. The blanket `-32000` assertion is wrong. |
| **1b** | The dependency on 1a is reasonable. “One turn” needs a precise meaning: ACP currently records turn endings at the door. Prove that failed attempts neither release the queue nor trigger curve moves, notifications, or limit moves prematurely. |
| **1c** | Split native driven runs from live native conversations. Proving `--resume` on the former does not prove retry on the latter. Include missing-id and pre-`init` failures. |
| **1d** | Native retry support is a dependency whenever the row promises native coverage, regardless of this installation’s selected adapters. Pass deadlines, conversation-id retention and already-published answers need settling first. |
| **1e** | “Every door” requires 1c’s native failure capture; `After: 1a` alone is insufficient. Preserve the existing harness-line refusal path. |
| **1f** | Recording before mapping is correct. The two proposed terminal-error markings are not interchangeable; the retry-notice inference is unsafe. Prove both direct terminal failures and recovered retries. |
| **1g** | A real incident is useful observational evidence, but “the next provider error” is not a repeatable acceptance check. Add a controlled installed stand-in proof; keep the next real incident as subsequent evidence. |

The first row should remain the first delivery, narrowed to safe ACP driven-session recovery. Its classification, delivery, exhaustion and watcher contracts must land together.

### 4. Findings

1. **Blocking — blanket `-32000` classification disables healthy accounts.**  
   **Where:** design:117, :182, :409; `Accounts/SignIns/AccountSignIns.cs:19`; `tools/rehearsal-kit.mjs:889`.  
   An ordinary stand-in work failure becomes an access failure, loses its strike and marks the account unusable. Protocol vocabulary does not establish that every implementation uses this code faithfully.  
   **Smallest change:** make this mapping adapter-specific, with a documented protocol-conformance declaration where appropriate. Keep unknown implementations conservative. Prove an authentication refusal and an unrelated failure with the same code.

2. **Blocking — absence of a recognized limit sentence is treated as proof of transience.**  
   **Where:** design:118, :132, :296; `Accounts/Usage/AccountLimits.cs:111`, :299; `docs/2026-10-02-limit-signals-evidence.md:158`.  
   The existing table recognizes a limited grammar. The evidence already names usage-exhaustion prefixes outside it. Such a `rate_limit`/429 would receive five transient retries. Conversely, `usageLimitExceeded` is declared a limit without specifying how a code-only failure produces the `LimitSeen` and cool-off that the unchanged sentence reader requires.  
   **Smallest change:** require positive evidence for a transient 429; keep unread cases conservative. Define code-only limit handling, including an unstated reset and default cool-off. Test both missing-marker exhaustion and a genuine temporary rate limit.

3. **Blocking — shape 2 is neither captured at the correct two seams nor reliably attributed.**  
   **Where:** design:183; `Agents/Acp/Acp.cs:875`, :1396, :1618; `Conversations/Chat/ChatRunner.cs:2837`.  
   Additional prompts and explicit steering are different operations. A “last refusal” plus absence of later updates does not establish that the original turn failed: the original work can finish successfully while an added word fails. Unrelated usage or tool updates can also defeat the proposed predicate.  
   **Smallest change:** retain operation/request identity and word disposition for both paths. Specify when a failure belongs to the ending turn, rather than merely to an added word. Prove both response orderings, a successful original turn with a refused addition, and an unrelated update after refusal.

4. **Blocking — the retry lacks an acceptance and ownership contract for words.**  
   **Where:** design:233–240; `Agents/Acp/Acp.cs:1094`, :1564; `Conversations/SaidDoor.cs:72`; `Loop/DriverContinuations.cs:1273`.  
   A failed prompt may already have appended its user message and performed work. Native failure handling deliberately returns carried words, not only demonstrably undelivered words. Reusing that set can send the person’s instruction twice. Existing `Unreached` is available only during teardown, and a producer key deduplicates a waiting record entry, not a harness prompt.  
   **Smallest change:** define accepted, definitely undelivered and uncertain dispositions per word; transfer each through one dispatcher. Retry with a continuation word after accepted work, preserving files and ids. Prove a stand-in that records the person’s words, performs a visible effect, then fails: neither the original instruction nor the effect is replayed.

5. **Blocking — exhaustion does not actually hold the quest.**  
   **Where:** design:253–261; `Quests/Domain/Planner.cs:1345`, :1355, :1385, :1411, :2005`.  
   Adding `GoOnMarks.Transient` suppresses `WordsOn` but does not suppress failed-line carry-on or an open quest’s fresh start. Removing strikes makes this escape especially dangerous: successive fresh episodes can continue indefinitely. A failed mark write also currently permits another attempt.  
   **Smallest change:** make `ended.transient` an explicit planner hold for open and taken quests, independent of the word mark, released by the named person actions. Prove multiple subsequent looks, with and without commits, including a failed mark write.

6. **Blocking — 31 minutes is a delay sum, not an elapsed bound.**  
   **Where:** design:224–229, :249–254; `Loop/Driver.cs:3934`; `Sessions/Watch/SessionWatch.cs:342`.  
   Five harness attempts add their own retries and execution time; one may never answer. Person-triggered early attempts also make “five tries over 31 minutes” inaccurate. Suppressing quiet detection during the wait leaves no stated elapsed bound for the episode.  
   **Smallest change:** choose and name either an attempt bound or an elapsed deadline. Define deadline behavior during an active request, early person-triggered attempts, shutdown and restart. Give the watcher an explicit retry-wait signal. Prove slow and unanswered attempts, cancellation, and a restart using a held clock.

7. **Blocking — native retry conflates two process lifecycles and omits one-shot ids.**  
   **Where:** design:230–232, :335, :411; `Agents/Adapters/Adapters.cs:2262`; `Conversations/Chat/ChatRunner.cs:3048`; `Loop/Driver.cs:3603`, :4064`.  
   A live native chat should send its next message through its existing process. Spawning `--resume` against that still-live conversation risks competing writers. Native intakes, helpers and opinions currently retain no conversation id because `keepAs` is null for one-turn targets.  
   **Smallest change:** distinguish live-stream retry from exited-process resume. Capture an episode-local id for native passes, and specify missing-id behavior without replaying the original target. Prove all three cases separately.

8. **Blocking — retry happens before authoritative completion is checked.**  
   **Where:** design:221, :327–340; `Sessions/Domain/Observation.cs:104`, :116`; `Quests/Domain/Intake.cs:305`; `Loop/DriverHelpers.cs:652`.  
   Today, a submitted quest, a published intake and a helper’s stored answer can outrank a messy turn ending. An unconditional retry before conclusion can reopen completed work, repeat publication or continue a pass whose answer is already durable. The intake note’s “before it published anything” is not established by a transient error.  
   **Smallest change:** let each run owner check its authoritative outcome before approving another turn. Preserve completion, deletion and person-stop precedence. Prove completion or publication immediately before a provider failure.

9. **Blocking — helpers and opinions cannot reach the promised retry bound under their existing deadlines.**  
   **Where:** design:336–340, :412; `Conversations/Domain/HelperPass.cs:53`; `Loop/DriverHelpers.cs:978`; `Loop/DriverOpinions.cs:202`; `Loop/Driver.cs:3940`.  
   Helpers normally have fifteen minutes total, and opinions carry their rule’s bound. Their processes can be killed before the 31 minutes of delays finish, producing timeout/no-answer outcomes instead of the proposed transient reason. Only brainstorms receive an explicit deadline rule.  
   **Smallest change:** define how every pass’s deadline composes with retry time—whether waits consume it, and which outcome wins at expiry. Keep that decision explicit in the instructions and proof; do not silently extend pass budgets.

10. **Blocking — a Codex retry notification cannot establish terminal failure.**  
    **Where:** design:194–198, :414.  
    `willRetry` describes an intermediate failure. Recovery followed by an empty successful turn can satisfy the proposed inference, while a direct terminal failure may have no preceding retry notice. The default door’s error text chunk also makes “no model output after the notice” ambiguous.  
    **Smallest change:** require a trusted terminal failure marker, such as the measured typed extension, before classifying an `end_turn` as failed. Prove recovered retries, successful empty endings, cancellation, direct terminal errors and exhausted retries. Preserve fatal and access categories as well as transient ones.

11. **Blocking — routing access exclusively through the new failure shape drops an existing source.**  
    **Where:** design:159–176, :184, :377; `Sessions/Domain/Observation.cs:457`; `Loop/Driver.cs:3241`; `Sessions/ObservationTests.cs:146` under `Daoris.Desktop.Driver.Tests`.  
    Existing credential-refusal detection accepts harness-owned lines beside a failed result or nonzero exit. `DoorFailure` currently has only prompt, steer and result origins. A harness that fails on stderr without a result would lose that access reading, contrary to the claim that behavior is unchanged.  
    **Smallest change:** retain a trusted harness-failure origin and its existing failure gate. Test stderr-only credential rejection, ordinary stderr failure, and an agent message quoting the same credential sentence.

12. **Blocking — retry at the open has no phase-specific contract.**  
    **Where:** design:159–163, :316–317; `Agents/Acp/Acp.cs:793`; `Loop/Driver.cs:3595`.  
    The design promises the same retry policy before any prompt, but its failure origins and retry payload assume a conversation already holds the target. An open failure may have no session id and no delivered instruction. Sending only “go on where you stopped” loses the original task.  
    **Smallest change:** either defer open retries explicitly or add an open phase: repeat the failed opening operation, then deliver the original prompt once. Preserve D205’s model-refusal path. Prove failures before and after session creation.

13. **Not blocking — one immutable line cannot provide the proposed live retry display.**  
    **Where:** design:241–245, :335, :353–367; `src/Daoris.Web/src/work/waits/limitWait.ts:120`.  
    The first line contains only the first next-at time. Later attempts, early person-triggered attempts and request durations move the schedule. The proposed “no turn follows” reader also loses the indicator after a retried ACP turn ends. Section 7 names `chat.transient-retrying`, while the catalogue lists `agent.transient-retrying`.  
    **Smallest change:** choose one code and separate the once-only conversational announcement from current episode progress. Prove the head and Ask Daoris panel through multiple waits, success, stop and exhaustion in both languages.

14. **Not blocking — the dependency and evidence claims overstate what the rows prove.**  
    **Where:** design:211, :403–415, :417.  
    A scripted stand-in emitting an assumed frame does not record the real adapter’s wire. Row 1e’s every-door proof requires native capture from 1c; 1d’s native dependency cannot depend on installation settings. Existing proofs also omit the transition cases identified above.  
    **Smallest change:** correct those dependencies, distinguish source-derived fixtures from captured frames, and add each blocking finding’s counterexample to its owning row. Give 1g a repeatable installed stand-in check alongside later real-incident observation.

**Findings: 14. Blocking: 12.**

**Verdict: another round.** Keep the four-kind interface and the ACP driven-session first delivery. The next round needs settled contracts for classification, word acceptance, planner exhaustion holds, elapsed bounds, native process lifecycles and authoritative completion, with stand-ins proving their transitions through D206 and D205. Those decisions affect the mechanism itself and cannot be checked as editorial corrections alone.

## Opus (foundation)

The independent Opus 5.5 reader's foundation read of round 1.

## AGENTERRORS1 (D210), round 1: the independent foundation read (D191)

Read: `docs/2026-10-24-agent-errors-design.md` and `docs/decisions/D210.md`, whole, at the tree's `6f089fe1f`. Its code is
`64059110a`'s, since only those two documents changed. Against them I read the code they cite and the code around it. The
adapters were read in the install's toolchain cache: `claude-agent-acp` 0.84.0 (`dist/acp-agent.js`) and `codex-acp` 2.1.1
(`dist/index.js`). I also read D206 and its notes, D205 point 5 and its round notes, D137's TURNFAIL1-4 notes, and the archive's
LIMITGOON1 and LIMITGOON2. Nothing was run. Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they say otherwise.

**What holds.** The rest of this read stands on these, and none of them should move:
- four kinds, read at the interface from the door's own failure and never from the agent's text;
- the limit read first by the unchanged `AccountLimits.Read`;
- an unread error kept fatal;
- the retry inside the run, before any conclusion, so no strike, part or brief is reached while it runs;
- no new ledger state.

The citations I checked hold: §1.1's adapter ranges, `AcpRefusal` (`Acp.cs:192-240`), the capture
(`Driver.cs:4312-4326`), the conclusion's three readers (`:2775-2777`), `AccountLimited`'s guard (`:3161`), no pipe-door
`turnFailed` (`:3730`), the owner fallbacks (`HarnessRoster.cs:120-144`), `ReadStrikes` (`ServiceClient.cs:2099-2121`),
`FailedTurnWords` (`Observation.cs:494-528`), `AgainAsync` (`DriverContinuations.cs:1204-1237`), and the codex-acp error lanes
(`index.js:30792-30845`). What does not hold is below.

### 1. Claims about the code that are wrong

1. **§0 and §4.2, shape 2's adapter path.** The design says *"During a steer, an error-shaped result settles the turn `end_turn`
   instead (`acp-agent.js:4150-4153`), and the steered word's answer is the error."* That branch belongs to the `_session/steering`
   lane (STEER2a's *Send now*, `steeredEchoes`), and that lane's request answers `injected` at once (`acp-agent.js:2061-2132`), so
   it never carries the provider's error. Shape 2 is STEER1's mid-turn `session/prompt` instead. The adapter settles the running
   prompt `end_turn` only at the queued prompt's echo (`acp-agent.js:4455-4525`), which means after the SDK has taken the word.
   The 529 then answered the word's own prompt, which Daoris caught at `Acp.cs:879-883`.
2. **§4.2: *"The refused word goes back to the run as `Unreached`, as USERSHOWN1's words do (`Acp.cs:1087-1091`)."*** The word
   was taken:
   - When the prompt before it answered, `Answered` handed the turn to it (`head.Taken = true; Asked(next)`, `Acp.cs:1667-1671`).
   - Its own answer removed it from `_prompts` (`:1662`).
   - So `Untaken()` (`:1094-1102`) never lists it.

   It is in the agent's own conversation.
3. **§1.1 and §12: the pipe door's `api_error_status` and the SDK's `error` field are *"never seen on this machine"*.** Both were
   measured by MODELROLE1a:
   - `docs/2026-10-23-model-effort-doors-evidence.md:186-204` records the synthetic `assistant` frame with
     `error: "model_not_found"` and `is_api_error_message: true`, and the `result` with `api_error_status: 404`.
   - The same lines record a limit told apart the same way: `error: "rate_limit"`, `api_error_status: 429`, *You've hit your
     weekly limit · resets …*.
   - The frames are held in `Driver.Tests/Agents/ClaudeStreamJsonTests.cs:191-212`.

   Only a 5xx frame on that door is unmeasured.
4. **§1.3 and §10: *"a carry-on walks and is briefed (`Driver.cs:1731-1735`)"*.** At `64059110a` those lines are
   `RunOnceAsync`'s summary. The citation was carried over from D206, which was read at `9950b24cb`. The carry-on's walk is
   `Driver.cs:1990-2001` (`SelectHandedAsync`), and its brief is `:2502-2512` (`ContextBriefs.ComposeAsync`, `SayMoved`).
5. **§5.1, the words *"marked `GoOnMarks.Transient` so no look takes them (TURNFAIL1's `FailedTwice` does the same…)"*, read with
   *"It waits for you … The line is not parked"*.** A mark only stops the planner's `GoOn` (`Planner.cs:1494-1506`).
   - A taken quest's failed record is still carried on by `CarriedOnFrom` (`Planner.cs:1373-1387`; `CarriesOn` is true for every
     `failed` record, `:2005-2006`). `HeldForPerson` passes only `Elsewhere`, `AnswersAlone` and `NotNow` (`:1411-1413`).
   - An open quest's failed record is started afresh in a new tree (`:1355-1366`).

   TURNFAIL1's `FailedTwice` therefore does not hold a line either: its record is carried on, briefed. Also, with none of the
   person's words, `GoOnMarks.Judged` is false because it needs a word waiting (`GoOnMarks.cs:131-134`), so no mark can hold
   shape 1's record.
6. **§5.1 and §7: *Try again* *"(D137's resume of words waiting on a record, `DriverContinuations.ResumesOwn`)"*, offered on the
   header of a failed record.** No *Try again* exists for a failed record:
   - The page offers `retry` only for a parked record, or a stopped one that holds its quest
     (`src/Daoris.Web/src/work/acts.ts:157`).
   - `ResumesOwn` (`DriverContinuations.cs:40-45`) serves the planner's `GoOn` of words the person said, which marked words never
     reach.
   - TURNFAIL1's held words go again only *"with the next words you say"* (`Observation.cs:523-524`).
7. **§5.1: the watch *"reads it as waiting, as it reads `background.waits` (`Platform/NoteCodes.cs:912`)"*.** The watch reads no
   note. It reads the door's live flag `WaitsOnOwnWork` (`Sessions/Watch/SessionWatch.cs:13-16`, `:322-324`), wired at
   `Driver.cs:4223`.
8. **§7: *"Every kind of run takes §5.1's retry inside its run … What differs is the end past the bound."*** Helpers, opinions and
   partners run under their pass's own minutes:
   - helpers 15 (`Conversations/Domain/HelperInstruction.cs:34`);
   - opinions 20 (`Review/Domain/OpinionRules.cs:101`);
   - partners 45 (`Conversations/Domain/PartnerInstruction.cs:28`).

   At those minutes the run is killed (`Driver.cs:3919-3924`; `DriverHelpers.cs:978`, `DriverOpinions.cs:202`,
   `DriverPartners.cs:603`) and concluded `Observation.TimedOut(bound)` (`DriverHelpers.cs:993`, `DriverOpinions.cs:217`,
   `DriverPartners.cs:617`). So a helper's or an opinion's "past the bound" is never reached.
9. **§8's glossary entry: *"The glossary gains *try again* / 再试一次."*** The glossary already holds *try again* as 重试 and
   lists 再试 to avoid (`src/Daoris.Web/src/locales/glossary.json:652`). The design's zh lines use 再试, so `names:check --strict`
   fails them.

### 2. The mechanism's holes

- **Where the protocol door's retry runs** (finding 1). The design keeps the failure where the capture keeps `error.Message`
  today (`Driver.cs:4312-4326`). By then `RunAsync`'s `finally` has ended the reader (`Acp.cs:889-903`, `Release` `:1800-1804`),
  and the capture's own `finally` is about to close stdin (`Driver.cs:4335-4338`). No live session is left there to prompt.
  - *"A new `session/prompt` on the same live process"* exists only inside `AcpSession.RunAsync`, around its four prompt sites
    (`:823-825`, `:871`, `:877`, `:1048`).
  - `AcpSession` has no adapter, table or clock, so the reader must be handed to it.
  - During a wait the inbox's mid-turn route (`:809-812`) would send a word alone. The go-on sent after it, as a second prompt,
    would end the person's turn at its echo (the adapter's hand-off).
- **Past the bound the line is not held, and nothing bounds the cycle** (finding 2). The next look carries a taken quest on,
  walked and briefed, and starts an open one afresh. That is shape 1's loss and the walk §6.2 forbids. Because `ended.transient`
  counts no strike, an outage longer than the bound gives a new part every ~31+ minutes, each retried and carried on again, and no
  strike or park ever ends it. This is the loop the D206/D205 composition must exclude.
- **The person's words are sent twice** (finding 3). Shape 2's word was taken, and so are a resume's words once their prompt is on
  the wire (they are blocks, never `_prompts` words). Re-sending them on a try, or keeping them to go again past the bound,
  repeats them in the conversation.
- **A woken turn during the wait** (finding 4). A session whose turn failed while its background work runs is woken by its
  harness when the work ends (BGWAIT2/3, `Acp.cs:924-949`). A prompt sent into that turn is never answered: that is BGWAIT1's
  48-minute hang (`Acp.cs:936-939`), and a driven session has no time bound (`Driver.cs:3919-3923`).
- **A wrong kind can do worse than today** (findings 6 and 7).
  - A limit in a wording the table has not recorded becomes transient. The SDK declares twelve usage-limit prefixes
    (`docs/2026-10-02-limit-signals-evidence.md:157`) against `ClaudeLimits`' one marker (`Accounts/Usage/AccountLimits.cs:299`).
    Read transient, it holds a slot for 31+ minutes on every line started on that account, and the account is never cooled, so
    the walk keeps choosing it.
  - A generic `-32000` from a plugin harness marks an account signed out where no sign-in may exist to clear it.
  - The other direction is benign: a transient misread as fatal is today's behaviour.
- **The kinds of run with their own minutes** (finding 5): above, claim 8.
- **The open** (finding 8). A transient at `session/resume` or `session/load` is thrown as `AcpResumeRefused(Refused)`
  (`Acp.cs:2817-2824`). ANSWER1a and D206's step 1 take that as a refused resume and go briefed. §6.2's *"retried inside the start"*
  names no place for this.
- **The record and the page** (findings 9-12).
  - Each conclusion reader must learn the kind, not "the conclusion" alone.
  - The head cannot know a later try's time.
  - The streamed `API Error` message shows once per try.
  - Each send of the go-on is a `user` event.
  - *Try again* is unbuilt.
  - The zh words clash with the glossary.
- **Composition otherwise holds.** Three paths compose without a loop: a limit read first, a limit on a try leaving for D206, and
  `limit.not-lifted` reading limits only. D205 point 5 is kept inside the run. Conversations compose with LIMITGOON1: `LimitHeld`
  reads the person's words since the last turn event, and the tries emit none. The only loop is past the bound (finding 2).
- **Not counted, and unmeasured:** the 8- and 16-minute waits likely outlast the provider's prompt cache, so the try that succeeds
  re-reads the whole context (219,284 tokens in shape 1). That is the price of keeping the conversation, and §12 should say it.

### 3. The rows

- **1a** is not the fastest win as drawn: it carries both shapes, the page's head and three note codes. Its proof cannot show what
  it claims.
  - *"Past the bound … no park"* is asserted on the record, but the next look carries the line on (finding 2). The proof needs a
    tick: the look after `ended.transient` starts nothing, for an Open and a Taken quest, and the person's word resumes the same
    conversation id.
  - Shape 2's proof must assert that the stand-in receives the word once (finding 3).
  - Its `-32000` assertion overlaps 1e. In 1a, access and fatal should keep today's ends.

  The fastest win is 1a without shape 2 or the page: the reader, the protocol prompt failure, the retry inside `AcpSession` for a
  driven session, `ended.transient`, the strikes and the planner's hold. Shape 2, the head's line and *Try again* would follow as
  1a2.
- **1b** builds alone after 1a. Its proof should add that a word said during the wait goes in one prompt with the go-on, and is
  not cut by it (finding 1).
- **1c**: its *After 1b* is needed only for `NativeChat`, not for a driven native session.
  - Its first act ("records one, or replays the SDK's declared shape marked unmeasured") is moot for 404 and 429: both are
    measured (claim 3).
  - It must keep D205's pipe-door model refusal (finding 13).
  - Every `--resume` must carry `--model` and effort (`model-effort-doors-evidence.md:180-182`).
- **1d** cannot pass as written for helpers and opinions (claim 8, finding 5).
- **1e** and **1f** build alone after 1a.
- **1g** waits for a real outage on an unknown date. That is acceptable for an install proof, and the backlog should say so.
- **In no row:** the *Try again* press and its act, the watch's flag, and the page's drop rule for the tries (findings 1, 10, 11).

### 4. Findings

1. **The retry's seat on the protocol door is unnamed, and the place the design names cannot prompt.**
   - **What is wrong:** described in §2's first hole.
   - **Where:** `Driver.cs:4312-4338`; `Acp.cs:809-812`, `:823-825`, `:871`, `:877`, `:889-903`, `:1048`, `:1800-1804`;
     `acp-agent.js:4455-4525`; `SessionWatch.cs:13-16`. `AgentErrors.Read(ISessionAdapter, DoorFailure)` also cannot reach the
     owner's tables without the adapter set (`HarnessRoster.cs:120-144` resolve the owner through `adapters`).
   - **Blocking.**
   - **Smallest change:** the episode runs inside `AcpSession.RunAsync`, around its prompt sites. The capture hands it the reader
     as a delegate, the clock and `TransientRetry.Delays`. While it waits the inbox holds every word, and a try is one prompt of
     blocks: the person's words, then the go-on. The door exposes a `WaitsOnTransient` beside `WaitsOnOwnWork` for the watch. The
     reader takes the adapter set. The conversation's seat (`ChatRunner.cs:2889-2922`) is right as written, since its process
     lives across turns.
2. **Past the bound the line is carried on or started afresh, and no strike bounds the cycle.**
   - **What is wrong:** claim 5 and the second hole.
   - **Where:** `Planner.cs:1355-1366`, `:1373-1387`, `:1411-1413`, `:2005-2006`; `GoOnMarks.cs:131-134`. There is also a
     contradiction inside §5.1: *"Every end of the episode takes it off by id"*, against the go-on word that *"stay[s] on `said`,
     marked"*. And D206's rule is that Daoris's word comes off at every conclusion so it never reaches a new part
     (`DriverContinuations.cs:702-707`).
   - **Blocking.**
   - **Smallest change:**
     - The planner holds a quest, Open or Taken, whose last record here ended `ended.transient`. It reads the note's code, as
       `StoppedByReopen` reads its own (`Planner.cs:2020-2022`), says a sentence of its own and takes no slot.
     - The hold lasts until the person's word (the `GoOn`, which already comes first, `:1322-1329`) or *Try again*.
     - The release resumes the record's own conversation by D206's step-1 path (`LimitResume.Line`, `LimitResume.cs:26-32`), with
       Daoris's go-on kept just before `ContinueAsync`. A refused resume goes briefed, as D206's does.
     - Daoris's go-on comes off at every conclusion, as `limit.reset` does, and is kept again at the release.
     - Row 1a proves this at the next look.
3. **Shape 2's word and a resume's prompted words would be sent twice.**
   - **What is wrong:** claims 1 and 2, and the third hole.
   - **Where:** `Acp.cs:1094-1102`, `:1662`, `:1667-1671`, `:879-883`; `acp-agent.js:4455-4525`, `:2061-2132`.
   - **Blocking**, since the owner's direction says a retry never repeats the person's words.
   - **Smallest change:**
     - A mid-turn word's prompt refused with a kind is the turn's prompt failing (`Where: prompt`), and nothing of the person's is
       sent again. Only Daoris's go-on goes, plus words that were never on the wire.
     - Past the bound, words a failed prompt carried to the wire (a resume's blocks, `resume.Prompted`) come off rather than wait.
       TURNFAIL1's re-send stays for fatal ends, as the design keeps it.
     - The proof: one `user` event for the word, and one receipt of it by the stand-in.
4. **A woken turn during the wait is not handled.**
   - **What is wrong:** the fourth hole.
   - **Where:** `Acp.cs:924-949`, `:936-939`, `:1037-1055`.
   - **Not blocking.**
   - **Smallest change:** each try goes through `_wake` as `AfterEndedAsync` does. A woken turn that answers ends the episode as
     a try that answered, and Daoris prompts only where none woke within `AcpWake.Quiet`.
5. **Passes with their own minutes never reach the bound.**
   - **What is wrong:** claim 8.
   - **Where:** `HelperInstruction.cs:34`, `OpinionRules.cs:101`, `PartnerInstruction.cs:28`; `DriverHelpers.cs:993`,
     `DriverOpinions.cs:217`, `DriverPartners.cs:617`.
   - **Not blocking.**
   - **Smallest change:** apply the brainstorm's rule to every pass with minutes, so no delay is waited past the pass's deadline.
     A pass whose minutes end inside an episode ends with the transient reason (`HelperWhy.Transient`, the opinion's `transient`),
     not *timed out*.
6. **`rate_limit` or 429 with no sentence read is made transient.**
   - **What is wrong:** described in the fifth hole. In addition, Codex's `usageLimitExceeded`, decided by its code, has no reset
     path: `AccountLimited` cools only through the sentence (`Driver.cs:3172`).
   - **Where:** design §3.1; `AccountLimits.cs:79`, `:299`; `limit-signals-evidence.md:157`.
   - **Not blocking.**
   - **Smallest change:** a limit decided by a code or kind (`rate_limit`, 429, `usageLimitExceeded`) with no sentence the table
     reads is `limit`, cooled for D125's unstated default (`AccountLimits.DefaultCoolOff`, 60 minutes). It is never transient.
7. **`-32000` is read as `access` on every protocol door, a plugin's included.**
   - **What is wrong:** a non-conforming harness's generic `-32000` would mark its account signed out, and the hold lasts until a
     person looks again, though no sign-in may exist to clear it. Today the same error is one strike.
   - **Where:** design §3.1 and §4.2; `Driver.cs:3271-3275`.
   - **Not blocking.**
   - **Smallest change:** `-32000` is `access` where the adapter or its owner declares a sign-in (`SignIn` or `LoginCheck`).
     Elsewhere it is fatal, with its words.
8. **A transient error at the open has no seat.**
   - **What is wrong:** the open's hole.
   - **Where:** `Acp.cs:2817-2824`.
   - **Not blocking.**
   - **Smallest change:** read the open's `AcpRefusal` through the reader before `ContinueWhy.Refused`. A transient one waits and
     asks again within the start; anything else goes as today.
9. **The conclusion readers to change are named as one.**
   - **What is wrong:** each of these reads an end on its own, and `FailedTurnWords.Judge` needs a `WordsAfter` for the new kind
     (`Observation.cs:503-508`):
     - `Observation.Conclude`;
     - `Observation.Resumed` (`Observation.cs:303`, used at `DriverContinuations.cs:672-683`);
     - TURNFAIL1's `WentOn` (`Observation.cs:330`);
     - `IntakeObservation.Conclude` (`DriverIntakes.cs:395`);
     - `ReadHelper` (`DriverHelpers.cs:992`).
   - **Not blocking.**
   - **Smallest change:** list them in §5.1 and in each row.
10. **The page cannot say what §5.1 promises.**
    - **What is wrong:**
      - *Trying again at {{time}}* after the first try cannot be drawn from one line and the clock, since each try lasts as long as
        the harness's own retries (3.5 minutes in shape 1).
      - The head keeps saying it during a try that is working, because it waits for a turn event.
      - The streamed `API Error` agent message is dropped only beside `door.failed` (D137's TURNFAIL1 note, `toTurns`), so it shows
        once per try.
      - Each send of the go-on is a `user` event (`Asked`).
      - *"Over 31 minutes"* counts the waits alone.
    - **Not blocking.**
    - **Smallest change:**
      - A quiet per-try event (`agent.transient`, with `try` and `next`) that the head reads, never a line.
      - The drop rule keyed to the episode's events as well.
      - A re-sent go-on kept off the record.
      - The sentences say *31 minutes of waits between tries*.
11. ***Try again* for a failed record does not exist, and no row builds it.**
    - **What is wrong:** claim 6.
    - **Where:** `acts.ts:157`.
    - **Not blocking.**
    - **Smallest change:** name the press and its act (finding 2's release) in row 1a or 1a2, in en and zh.
12. **The zh words clash with the glossary.**
    - **What is wrong:** claim 9.
    - **Where:** `glossary.json:652`.
    - **Not blocking.**
    - **Smallest change:** use 重试, the existing term, and widen its `means` to a record past its retries.
13. **`model_not_found` is fatal on every door, against D205's pipe-door exception.**
    - **What is wrong:** D205's round-2 note makes the pipe door's measured model refusal on a silent turn a refused choice: no
      strike, the walk asked again, *"the one named exception to point 5's never a fallback after a prompt"*
      (`docs/decisions/D205.md:104-107`).
    - **Not blocking.**
    - **Smallest change:** on the pipe door, a `model_not_found` on a silent turn is D205's, read before this reader.
14. **The pipe door's frames are measured, and the field read is the harness's.**
    - **What is wrong:** claim 3. Also, on that door the `error` is a field of the harness's synthetic `assistant` frame, so §3.3's
      *never the agent's message* must allow a field the harness sets.
    - **Where:** `model-effort-doors-evidence.md:180-204`; `ClaudeStreamJsonTests.cs:191-212`.
    - **Not blocking.**
    - **Smallest change:** correct §1.1, §12, D210's *what the checks do not cover* and row 1c, and say the frame's field is read,
      never its text.
15. **Wrong citations.**
    - **What is wrong:** claims 4 and 7, and the adapter line in claim 1.
    - **Where:** `Driver.cs:1731-1735` should be `:1990-2001` and `:2502-2512`; `NoteCodes.cs:912` should be `SessionWatch.cs:13-16`
      and `:322-324`; `acp-agent.js:4150-4153` should be `:4455-4525`.
    - **Not blocking.**
    - **Smallest change:** cite the right lines.
16. **The rows' order and proofs.**
    - **What is wrong:** §3 above.
    - **Not blocking.**
    - **Smallest change:**
      - Split 1a2 (shape 2, the head, *Try again*) from 1a.
      - Add the next-look tick and the words-once assertions to 1a.
      - 1a keeps today's ends for access and fatal.
      - 1c follows 1b only for `NativeChat`.
      - 1g's open date is said in the backlog.

**16 findings, 3 blocking (1, 2, 3).**

**Verdict: accept after named changes.** The foundation holds: four kinds read at the interface from the door's own failure, the
limit first through the unchanged reader, an unread error kept fatal, the retry inside the run before any conclusion, and no new
state. Three things block, and each has a stated change that the steward can check against the cited lines without another read:
- **Finding 1:** the protocol door's episode lives inside `AcpSession.RunAsync`, with words held during the wait and each try one
  prompt.
- **Finding 2:** the planner holds an `ended.transient` line, Open or Taken, until the person's word or *Try again*, released
  through D206's step-1 resume. Daoris's go-on comes off at every conclusion. Without this, the line is carried on briefed and
  cycles with no strike.
- **Finding 3:** a refused mid-turn word, and a resume's prompted words, are taken, never sent again.

Findings 4 to 6 should also be written into the design before it is accepted, because they decide whether a wrong kind can do worse
than today and whether every kind of run is covered:
- 4: the try goes through the wake gate;
- 5: a pass with its own minutes ends with the transient reason;
- 6: `rate_limit` and 429 without a recorded sentence read as a limit with the unstated reset.

Findings 7 to 16 can be taken in their rows.

## Codex, round 2

Codex's read of round 2 at `f23020f80`: another round (11 findings, 8 blocking), keeping the four-kind interface, the conservative limit reading and the ACP driven-session first delivery.

## 0. Round-1 findings checked

`C` identifies Codex’s findings; `O` identifies the Opus foundation findings in `docs/2026-10-24-agent-errors-reads.md`. “Taken” means incorporated into the proposed contract, not implemented.

Below, **design** means `docs/2026-10-24-agent-errors-design.md`. Driver code paths are relative to `src/Daoris.Desktop/Daoris.Desktop.Driver/`; test paths name their project explicitly.

| Finding | Round-2 disposition, checked against code |
|---|---|
| **C1 — blanket `-32000` access classification** | **Taken.** Adapter-specific declarations replace the blanket rule. The counterexample holds: `Accounts/SignIns/AccountSignIns.cs:19` documents the stub’s generic use; `Adapters.cs:1731` shows why merely declaring sign-in would be insufficient. |
| **C2 — unrecognized limit wording becomes transient** | **Taken.** Code-only limits now receive an unstated reset. This closes the missing-marker case in `AccountLimits.Read` (`Accounts/Usage/AccountLimits.cs:111`). The temporary-rate-limit exception remains contradictory; finding 9 below. |
| **C3 — shape-2 capture and attribution** | **Open.** The measured Claude prompt handoff is addressed, and the steering seam is distinguished. The general claim that a false `Taken` mark proves nonacceptance does not follow from `Acp.cs:1618`; finding 1. |
| **C4 — word acceptance and ownership** | **Open.** Accepted start/resume blocks are excluded from transient retransmission, but uncertainty is declared away, and disposition does not survive a later limit/fatal outcome; findings 1–2. |
| **C5 — exhaustion does not hold the quest** | **Open.** The explicit planner hold addresses the original omission. Its release can remain valid indefinitely, machine-authored words bypass it, and a commitless Open quest lacks the promised carry-on route; findings 3–5. |
| **C6 — delay sum presented as elapsed bound** | **Answered with a reason that holds.** Round 2 chooses five attempts, explicitly excluding execution time. `Driver.cs:3934` supports the distinction between unbounded driven work and bounded passes. The watcher exemption is now explicit. |
| **C7 — native lifecycles and missing one-shot ids** | **Open.** Live-chat versus exited-process retry and episode-local ids are settled. Deferring implementation to 1c/1d is reasonable. However, the promised post-exhaustion resume still meets the ACP-only guard in `DriverContinuations.cs:58`; finding 7. |
| **C8 — retry precedes authoritative completion** | **Open.** An owner check is added, but the intake predicate omits existing authoritative endings in `Intake.cs:320` and `:326`; finding 8. |
| **C9 — pass deadlines preempt retries** | **Taken.** Waits consume existing pass minutes, and expiry during an episode receives the transient reason. This explicitly changes the timeout paths at `DriverHelpers.cs:993`, `DriverOpinions.cs:217` and `DriverPartners.cs:617`. |
| **C10 — Codex retry notices inferred as terminal failure** | **Taken.** That inference is withdrawn. Row 1f requires a measured terminal marker and includes recovered, empty-success, cancellation and direct-terminal cases. |
| **C11 — existing harness-line access source lost** | **Taken.** The failure-gated harness origin preserves `Observation.Refused` (`Sessions/Domain/Observation.cs:457`). Its source description needs correction; finding 11. |
| **C12 — open retries lack a phase contract** | **Taken.** Section 5.6 repeats the opening operation and sends the original prompt once. This correctly separates opening from continuing accepted work; `Acp.cs:1116`, `:2800`. |
| **C13 — immutable announcement cannot drive progress display** | **Taken.** Quiet progress events replace that inference; one announcement remains. Delivery order and actual-count wording still need correction; finding 10. |
| **C14 — dependencies and evidence overstated** | **Open.** Native dependencies and source-derived versus captured evidence are corrected. The first delivery’s display proof and native release proof remain incomplete; findings 7 and 10. |
| **O1 — retry seat, dependencies and watcher flag** | **Taken.** The episode moves inside `AcpSession.RunAsync`, before `finally` releases the reader (`Acp.cs:889`). The reader receives the adapter set, and waiting words are held. |
| **O2 — exhausted line automatically carried on** | **Open.** The hold is added, but its person-only, repeat-exhaustion and commitless-release guarantees do not yet hold; findings 3–5. |
| **O3 — taken shape-2/resume words repeated** | **Open.** The direct transient path is corrected. The acceptance inference and subsequent change of failure kind remain unsafe; findings 1–2. |
| **O4 — background wake during backoff** | **Taken.** Each retry goes through the wake gate, following `Acp.cs:1037`, rather than prompting into an autonomous turn. |
| **O5 — passes expire before five retries** | **Taken.** Section 7 expressly preserves their minutes and names the expiry outcome. |
| **O6 — code-only limits need conservative handling** | **Taken.** Unrecognized 429/`rate_limit` wording receives an unstated limit reset rather than transient retries. The temporary-limit exception needs ordering clarification; finding 9. |
| **O7 — generic `-32000` misread as access** | **Answered with a reason that holds.** Round 2 correctly rejects the proposed “declares sign-in” test: the stub declares sign-in yet uses that code generically. An explicit semantic declaration is narrower and appropriate. |
| **O8 — transient opening refusal goes briefed** | **Taken.** Section 5.6 introduces the missing phase before conversion to `ContinueWhy.Refused` (`Acp.cs:2817`). |
| **O9 — independent conclusion readers omitted** | **Taken.** Section 4.1 names them individually, including `Observation.Resumed`, `WentOn`, `FailedTurnWords.Judge`, intake and helper readers. |
| **O10 — page cannot represent retry progress** | **Taken.** Progress events, repeat-message suppression and once-only go-on display are specified. Some of that work is scheduled after the first row that needs it; finding 10. |
| **O11 — failed-record “Try again” does not exist** | **Taken.** The design now explicitly builds its act and offer. Today’s offer remains restricted at `src/Daoris.Web/src/work/acts.ts:157`. Its release mechanism needs findings 3 and 5. |
| **O12 — Chinese terminology conflicts with glossary** | **Taken.** The design adopts 重试 and schedules widening the existing glossary entry (`glossary.json:652`). |
| **O13 — native model refusal conflicts with D205** | **Taken.** The silent-turn exception is explicitly preserved before classification; `docs/decisions/D205.md:104`. |
| **O14 — native frames already measured** | **Open.** Measurement provenance is corrected, but the claimed 429 regression fixture is absent from the cited test; finding 11. |
| **O15 — wrong citations** | **Taken.** The original carry-on, watcher and Claude handoff citations are corrected. A separate wrong conversation citation remains; finding 11. |
| **O16 — row order and proofs** | **Open.** Splitting the first delivery and adding next-look/effect assertions improve it. Some essential once-only display work remains in 1a2, and release proofs stop before a second exhaustion; findings 3 and 10. |

## 1. Claims about the code

The principal diagnosis holds: structured ACP failure information is lost at `Loop/Driver.cs:4325`; native failures do not populate that tuple’s `turnFailed` (`:3730`); and failed quests can be carried on through the walk.

These claims are wrong or stronger than the code supports:

| Design claim | Code |
|---|---|
| `Taken == false` after an error means the agent never read the word; settling every response leaves no uncertainty (design:290–298). | `Taken` is local bookkeeping (`Agents/Acp/Acp.cs:429`). It is set by response order or selected subsequent updates (`:1667`, `:1697`, `:1944`), not by a negative acceptance acknowledgement. |
| The conversation’s refused-word catch is at `ChatRunner.cs:3048` (design:101). | That is native `TurnEnded`. The ACP refused-word catch is `Conversations/Chat/ChatRunner.cs:2837`. |
| Both measured 404 and 429 frames are held at `ClaudeStreamJsonTests.cs:191–212` (design:70, :593, :643). | Those lines hold the model-refusal/404 fixture only (`Daoris.Desktop.Driver.Tests/Agents/ClaudeStreamJsonTests.cs:201`). The file’s rate-limit-event tests concern account windows, not the claimed failed 429 result. |
| Existing trusted harness lines are stderr (design:183–185). | They also include stdout outside the structured protocol: `Agents/Streams/HarnessCapture.cs:159`; `Sessions/Domain/Observation.cs:618`. |
| An unanswered retry is watched “as any turn” across the mechanism (design:282, :328). | Driven sessions have `SessionWatch`; conversations await their process without that watcher (`Conversations/Chat/ChatRunner.cs:2405`). Bounded passes instead have their own deadline (`Loop/Driver.cs:3566`). |

The incident and adapter-package-source accounts remain supplied evidence here. Repository code confirms the capture seams; it does not independently establish every external adapter’s acceptance semantics.

## 2. The mechanism’s holes

The four-kind boundary remains sound. Conservative code-only limits, explicit opening phases, owner checks, pass deadlines and typed Codex terminal markers are substantial improvements.

The remaining foundation problem is that **failure classification, word disposition and release authorization still collapse together**. A transient episode can correctly avoid retransmission, then hand its original word set to an unchanged limit/fatal conclusion. An exhausted record can correctly be held once, then resume repeatedly because its session-keyed release never expires. Machine-authored words can also reach the pre-hold `WordsOn` branch.

D206 therefore needs more than “cooled once, then D206”: it needs the accepted-word boundary carried into its resume and briefed-move readers. D205’s walk must remain behind an authorization for the particular exhausted episode, including commitless Open quests and native records.

Coverage by run is improved but incomplete:

- **Driven sessions:** the hold’s release lifecycle and word ownership need settling.
- **Ask Daoris and chats:** `LimitHeld` must distinguish already accepted words from words still owed after transient recovery.
- **Helpers and opinions:** the stated deadline composition is buildable; stored answers/opinions must remain authoritative.
- **Intakes:** closing or answering the ask elsewhere must also stop retries.
- **Partners and brainstorm voices:** retaining their existing deadlines and ownership is appropriate.
- **Doors with insufficient evidence:** an unknown error may conservatively remain fatal. Likewise, an answered request without acceptance evidence must not become proof that its words are safe to resend.

Five attempts is now an honest attempt bound. It is not an elapsed completion guarantee, and conversations need that limitation stated accurately.

## 3. The rows

| Row | Independence and proof |
|---|---|
| **1a** | Still the fastest useful delivery: safe ACP driven-session recovery. Include the acceptance/disposition contract, complete exhaustion-release lifecycle and minimum repeat-message suppression. Its proof must include a second exhaustion after one release and a machine-authored word arriving while held. |
| **1a2** | Reasonable follow-up for shape 2, opening retries and richer presentation. Opening retries must repeat the raw operation, not call `OpenAsync`/`ResumeAsync` again through their one-shot handshake (`Acp.cs:1159`). |
| **1b** | Can follow the common mechanism, but “one turn” must coexist with durable accepted-word information for `LimitHeld`. Cooling once does not prove words are delivered once. |
| **1c** | Correctly separates driven native runs from live native chats. Add the missing-conversation measurement and post-exhaustion native release proof; an absent captured id is a different case. |
| **1d** | Native dependency is now correctly conditional on promised coverage. Deadline outcomes are explicit. Add intake closure/foreign publication and baseline-aware completion cases. |
| **1e** | Preserving the existing harness-line source is correct. Keep both stderr and outside-protocol stdout behind the failure gate. |
| **1f** | Measurement before mapping and typed-terminal counterexamples are appropriate. Apply the common disposition and release proofs to this door too. |
| **1g** | Controlled installed evidence is repeatable; later real-incident observation is properly separate. Specify the plugin stand-in’s declared owner/table: an undeclared plugin failure is fatal under this design. |

## 4. Findings

1. **Blocking — settling responses does not prove nonacceptance.**

   **Where:** design:290–298, :632; `Agents/Acp/Acp.cs:429`, :1662, :1697, :1944.

   `Taken` records Daoris’s inference about turn ownership. A JSON-RPC error does not say whether the server appended the word before failing. An empty `_prompts` collection establishes that calls answered, not that every false mark is a negative acknowledgement. Moreover, `Answered` removes the prompt before the awaiting catch receives its error.

   **Smallest change:** retain request identity and disposition before removal. Permit retransmission only for held/unwritten words or adapter-specific, proven nonacceptance; retain an uncertain disposition otherwise. Prove a stand-in that appends a queued word before returning an error without a handoff notification: the word must not be sent again.

2. **Blocking — a later limit or fatal failure can resurrect already accepted words.**

   **Where:** design:332–339, :433–440, :592; `Loop/DriverContinuations.cs:699`, :706; `Sessions/Domain/Observation.cs:503`; `Conversations/Domain/LimitHeld.cs:27`, :35.

   Round 2 changes the final transient judgment to `Taken`. If a later retry hits a limit, however, D206’s unchanged judgment returns `LimitWait` for the resume’s original ids. Conversations similarly collect all person words since the last turn event; row 1b deliberately emits no intervening turn ending. Accepted instructions can therefore be delivered again at the reset or move. A terminal fatal outcome can also reach TURNFAIL1’s retained original set.

   **Smallest change:** carry per-word disposition across every exit from the episode, independently of its final error kind. D206 and TURNFAIL1 receive only words still owed. Prove transient → limit → own-account reset, transient → limit → briefed move, and transient → fatal, asserting one receipt of each original instruction.

3. **Blocking — one “Try again” can release every later exhaustion of the same record.**

   **Where:** design:353–360, :505; `Platform/DriverConfig.cs:95`, :106, :121; `Loop/DriverContinuations.cs:640`.

   `Released` maps quest to session id. Its existing rationale explicitly depends on a later stop having a different session. This design resumes the same record. After another exhaustion, `config.Releases(quest, session)` remains true, contradicting “held again” and reopening an unbounded cycle.

   **Smallest change:** make authorization specific to the exhausted episode, or durably consume it before resuming. Define failure/restart handling so consumption cannot fail open. Prove exhaust → press once → exhaust again → several looks, including a restart: no further attempt.

4. **Blocking — `WordsOn` permits automatic release by another agent or Daoris.**

   **Where:** design:343–352; `Quests/Domain/Planner.cs:355`, :1325, :1503, :675; `Loop/DriverHelpers.cs:780`, :810.

   The pre-hold `WordsOn` branch tests waiting words, not person-authored words. A helper answer delivered after exhaustion becomes the next turn at the next look. Findings, Daoris words and other producers have the same general issue. Removing the retry go-on word does not close these paths.

   **Smallest change:** while transient-held, require a new person-authored release before `GoOn`; retain other waiting words for delivery alongside that authorized resume. Prove a helper answer and a Daoris word arriving after exhaustion start nothing, then travel once with the person’s release.

5. **Blocking — releasing a commitless Open quest does not select its old conversation.**

   **Where:** design:353–357; `Quests/Domain/Planner.cs:1345`, :1351, :1355, :1391; `Loop/LimitResume.cs:26`.

   Passing the new hold leaves the existing Open-quest branches. With no commits and no limit flag, neither `RetriedFrom` nor `LimitedFrom` selects the prior record. `Consider` plans a fresh start with no `Resumes`; widening `LimitResume.Line` cannot recover a record absent from the consideration.

   **Smallest change:** explicitly plan a released transient record as `CarryOn(quest, last)` for Open quests regardless of commits. Prove an exhausted, commitless Open quest resumes its original conversation after the press, with no fresh tree or original-target replay.

6. **Blocking — the first tables and the proposed inheritance rule do not compose.**

   **Where:** design:126–127, :209–217, :253–259; `Accounts/Harnesses/HarnessRoster.cs:120`, :135.

   The cited fallback pattern uses the owner’s table only when the adapter has none. But the proposed first entries give `claude-code-acp` its own `-32000` declaration and put transient codes/sentences on `claude-code`. Following that pattern literally hides the owner’s transient entries as soon as the adapter declares its own table.

   **Smallest change:** specify explicit composition: shared provider entries plus door-specific entries, with precedence and provenance, while keeping `-32000` semantics local to the declaring door. Alternatively, construct a complete table for each adapter. Prove an ACP adapter with its own authentication declaration still reads inherited overload and limit entries.

7. **Blocking — native recovery after exhaustion still falls through the ACP-only resume guard.**

   **Where:** design:355–357, :487–496, :593; `Loop/DriverContinuations.cs:54–62`; `Loop/Driver.cs:1970`.

   The design promises the same conversation after the person’s release through D206’s step 1. That path currently rejects every native adapter. Row 1c adds native retries inside an episode, but does not name changing this guard or satisfying its safety prerequisite. “No id captured” does not test `--resume` of an id that was captured but has disappeared.

   **Smallest change:** explicitly own the native step-1 change in 1c, preceded by a real-door missing-conversation measurement that establishes safe failure. Prove both post-exhaustion resume of the existing id and the named fallback when that id is gone, without replaying accepted instructions.

8. **Blocking — the intake owner check omits existing authoritative endings.**

   **Where:** design:409, :412, :483, :594; `Quests/Domain/Intake.cs:320`, :326.

   The proposed check stops for quests published by this intake or deletion. Existing conclusions also stand down when the person closes the ask, or somebody else answers it. Under the proposed predicate, retries continue against an ask already settled by someone else.

   **Smallest change:** derive permission to retry from the intake’s full authoritative outcome, using its start baseline, rather than the two listed cases. Prove closure without new quests and publication by somebody else immediately before the provider failure: no retry, original stand-down outcome.

9. **Not blocking — classification precedence contradicts the temporary-rate-limit exception.**

   **Where:** design:143–145, :159–170.

   A 429/`rate_limit` is classified as limit before transient sentences are consulted. Consequently, the promised recorded temporary-rate-limit sentence cannot select transient. Separately, “no code alone makes a transient” contradicts the listed overload/server-error code mappings.

   **Smallest change:** either remove the temporary-rate-limit exception for this delivery, or place its positively evidenced adapter-specific reading before the conservative code-only fallback, after recognized usage-limit sentences. Correct the code-alone sentence and test both sides.

10. **Not blocking — the first row’s proof does not establish the person-visible once-only behavior.**

    **Where:** design:308–322, :513–514, :590–592.

    Row 1a retries streamed errors, but leaves duplicate-error suppression and suppression of repeated go-on display to 1a2. Counting one `agent.transient-retrying` note does not prove the person sees the error once. Fixed “five tries/31 minutes” wording also becomes false after early person-triggered attempts or a pass deadline.

    **Smallest change:** move the minimum suppression behavior into 1a; leave the richer head display in 1a2. Assert the rendered conversation, not just note counts. Final wording must use actual attempts and waits, with deadline expiry worded separately.

11. **Not blocking — remaining citations and source descriptions overstate the evidence.**

    **Where:** design:70, :101, :183–185, :282, :593, :643.

    The 429 fixture is not in the cited test; the conversation catch citation points to native completion; trusted harness lines include outside-protocol stdout; and conversations lack the watcher invoked by the generic hung-retry statement.

    **Smallest change:** correct the catch to `ChatRunner.cs:2837`, add or accurately defer the failed-429 fixture, preserve the full trusted-line source, and scope watcher claims to driven runs while naming conversation and pass behavior separately.

**Findings: 11. Blocking: 8.**

**Verdict: another round.** Keep the four-kind interface, conservative limit reading and ACP driven-session first delivery. Before settlement, resolve word disposition across error-kind transitions and the full exhaustion-release lifecycle, including machine-authored words, commitless Open quests and native records. Specify table composition and the complete intake outcome check, then attach the counterexamples above to their owning rows. These changes determine whether the mechanism repeats instructions or escapes its bound; they require a checked contract rather than editorial acceptance.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `f23020f80`: 11 findings, 2 blocking; accept after named changes.

## AGENTERRORS1 (D210), round 2: the independent foundation read (D191)

**What was read.** `docs/2026-10-24-agent-errors-design.md` and `docs/decisions/D210.md` at `d713a2db4`, whole, against round 1's
reads (`docs/2026-10-24-agent-errors-reads.md`) and the round-2 diff. I checked them against the code at that tree:
- `Acp.cs`, the capture, the planner, `LimitResume`, `DriverContinuations`, `DriverConfig`, `StandingChoices`, `SessionWatch`,
  `Observation`, `ServiceClient.ReadStrikes`, `AccountLimits` and `HarnessRoster`;
- `DeclaredAcpAdapter`, `DriverHandOvers`, and the helper, opinion and intake starts;
- D206 and its notes, and D205 (point 5 and its round notes).

The adapters were read in the install's toolchain cache: `claude-agent-acp` 0.84.0 (`dist/acp-agent.js`) and `codex-acp` 2.1.1
(`dist/index.js`). `git diff 64059110a fb3eb89eb` touches no file the design cites (only the supervisor, platform wiring and
console files), so round 2's *same line numbers* claim holds. Nothing was run. Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they say otherwise.

**What holds, and should not move.**
- Four kinds, read at the interface from the door's own failure.
- The limit read first, now also by its code with the unstated reset.
- `-32000` read only where the adapter declares its meaning. Both adapters raise it only as `authRequired`:
  `acp-agent.js:2788`, `:4171`, `:4656`; `index.js:30261`, `:30805`, `:37594`.
- An unread error kept fatal.
- The retry inside `AcpSession.RunAsync`, before any conclusion.
- Words on the wire are taken and never sent again.
- The hold read by the note's code, never a mark.
- No new ledger state.

The direction of a misread is still benign at the reader: a transient read as fatal is today's strike, and no code alone makes a
transient. One cost to note, without a finding: a request-bound 5xx read as transient now loses today's self-healing briefed
carry-on, and needs the person (31 minutes, then the hold). Their way out is *Go on now with…*, which finding 4 keeps alive.

What does not hold is in the planner and the record past the bound. A release, or words left unsent, sends the line round again
with no strike to end it. That is the loop D210's *Why* says the hold exists to stop.

### 1. Round 1's findings

| Finding | Round 2's answer | Standing, checked against the code |
|---|---|---|
| **O1** (blocking): the retry's seat | Taken as named | **Holds.** The four prompt sites are where round 2 says. `:823-825` and `:871` let the `AcpRefusal` out, `:877` catches it, and `:1048` lets it out. `RunAsync`'s `finally` (`Acp.cs:889-903`) runs before the capture's catch (`Driver.cs:4312-4326`). The reader takes the adapter set. *New:* every kind of run shares this capture (`Driver.cs:3685-3693`), so row 1a must say which runs get the retry (F6) |
| **O2** (blocking): past the bound the line is carried on | Taken as named | **Does not hold yet.** The hold reads the note's code and precedes the carry-on and fresh-start paths. But *Try again*'s release is keyed by (quest, session), and the step-1 resume it starts continues that same record, so a second `ended.transient` on it is still released (F1, blocking). An Open quest's release has no planner path back to its record (F3). A hand-over and the awaiting-resume path are not passed or named (F4) |
| **O3** (blocking): taken words sent twice; shape 2 | Taken | **Holds.** `Answered` hands the turn to the next word (`Acp.cs:1667-1671`). The steering lane answers `injected` at once. A resume's blocks are taken once prompted, and `WordsAfter.Taken` takes `resume.Ids` off (`DriverContinuations.cs:717-725`). The settling step needs one more sentence (F8) |
| **O4**: woken turn during a wait | Taken | **Holds** (`AfterEndedAsync`, `Acp.cs:1037-1055`) |
| **O5**: passes with their own minutes | Taken | **Holds.** The passes conclude `TimedOut(bound)` on a null exit (`DriverHelpers.cs:993`, `DriverOpinions.cs:217`); row 1d owns the transient reason at expiry |
| **O6**: `rate_limit`/429 with no sentence | Taken | **Holds.** `AccountLimits.DefaultCoolOff` (`:79`) and `Read`'s unstated `LimitSeen` (`:111-113`) are the shapes cited |
| **O7**: `-32000` on every door | Taken otherwise (C1's narrower form) | **Holds.** O7's own test would mis-read the stub, as round 2 says (`Adapters.cs:1731`, `rehearsal-kit.mjs:889`) |
| **O8**: transient at the open | Taken otherwise (C12's form) | **Holds at the open itself.** Past its bound, a resume's words were never prompted, so they stay on the record, and `WordsOn` goes on with them ahead of the hold at every look, with no strike (F2, blocking) |
| **O9**: conclusion readers named | Taken | **Holds** |
| **O10**: the page cannot say §5.1 | Taken | **Holds** for the page. What the watch reads at the try after a long wait is new (F7) |
| **O11**: *Try again* unbuilt | Taken | **Holds** for the press. The act also needs `StandingChoices.Retry`'s case (F3) |
| **O12**: zh clashes with the glossary | Taken | **Holds** for 重试. The product's zh name in running text is a new clash (F10) |
| **O13**: D205's pipe-door exception | Taken | **Holds** (`D205.md:104-107`) |
| **O14**: pipe frames measured; the field is the harness's | Taken | **Holds** (`model-effort-doors-evidence.md:180-204`) |
| **O15**: citations | Taken | **Holds.** `Driver.cs:1990-2001` is the walk and `:2502-2512` the brief; `SessionWatch.cs:13-16` and `:322-324` are the watch's |
| **O16**: rows' order and proofs | Taken | **Holds**, except that 1g's plugin stand-in cannot read transient as written (F9). The open date is in `TASKS.md` |
| **C1** (blocking): blanket `-32000` | Taken (with O7) | **Holds.** The proof is in 1a |
| **C2** (blocking): limit read as transient | Taken (with O6) | **Holds** |
| **C3** (blocking): shape 2's seams | Taken (1a2) | **Holds.** The proofs name both orderings, the steer, and an unrelated update |
| **C4** (blocking): word acceptance | Taken otherwise: the wire settles, so *no third disposition* | **Holds on `claude-code-acp` in practice, for a different reason than stated.** Its `failActive` rejects only the active turn, and "later turns proceed" (`acp-agent.js:2750-2778`). So settling runs each queued word's own turn, and the error text it streams sets `Taken` through `TakeLate` (`Acp.cs:1697-1712`). Whether that settling turn is a try is unsaid (F8) |
| **C5** (blocking): exhaustion does not hold | Taken (with O2) | **Does not hold yet** (F1, F2, F3) |
| **C6** (blocking): 31 minutes is a delay sum | Taken: an attempt bound, `WaitsOnTransient` | **Holds.** What the quiet clock reads when a wait ends is unsaid (F7) |
| **C7** (blocking): native lifecycles | Deferred to 1c/1d, with the contract settled | **The answer holds.** The three cases are distinct, and the dependency is right |
| **C8** (blocking): completion before a try | Taken: the owner's check (§5.5) | **Holds in principle.** The driven check, read "as `QuestMovedAsync` reads it", answers *no* for an Open quest (`Observation.Parks`, `Observation.cs:216-219`). Row 1a must also keep the other kinds off the retry until 1d (F6) |
| **C9** (blocking): deadlines | Taken (with O5) | **Holds** |
| **C10** (blocking): `willRetry` inference | Taken: withdrawn; typed marker in 1f | **Holds.** The categories are in `codex-acp`'s source (`index.js:30852-30870`, `:39993-40004`) |
| **C11** (blocking): harness lines for access | Taken: `Where: harness` behind today's gate | **Holds** (`Observation.cs:457-462`) |
| **C12** (blocking): retry at the open | Taken (with O8) | **Holds at the open itself**; past the bound, F2 |
| Codex §2: composition with D206 | Taken | **Partly.** No replay of `limit.reset` or taken words holds, and a limit on a try leaves once. *Exhaustion suppresses word resumes and carry-ons* does not yet hold (F1, F2). A released line that meets its conversation gone takes D206's fallback, as said; but step 1 is protocol-door only (`DriverContinuations.cs:58`), so on the pipe door the release always goes briefed (F3's note) |

### 2. What round 2 introduced

**Wrong claims.**
- **§5.1 (`design:358-360`):** *"A release that fails again past its own bound ends `ended.transient` and is held again: an
  outage never cycles without the person."* It is not held again. The release is still written for that session (F1).
- **§5.1 (`design:350`):** *"Released by the person, and by nothing else."* `WordsOn` comes first, and its `Waits`
  (`Planner.cs:1503-1505`) passes any word on the record: Daoris's, a helper's answer, another session's message. So does a word
  the held run never sent (F2).
- **§4.1 (`design:212-213`):** the reader falls back to the owner "like `LimitsOf` and `SignInOf`". Those resolve *own or owner*
  (`HarnessRoster.cs:123`). `claude-code-acp`'s own `ErrorWords` (its `-32000` declaration, §4.4) would therefore shadow
  `claude-code`'s codes and sentences, and the install's 529 would read fatal (F5).
- **§5.5 (`design:405`):** the driven check is cited "as DONEEND1's `QuestMovedAsync` reads it". That predicate is
  `Observation.Parks`, which needs a `Taken` quest taken by this session, so it answers *no* for an Open quest (F6).
- **§5.1 (`design:296-297`):** *"`Taken` false: the agent never read it."* On `claude-code-acp` a queued word was already in
  the SDK's input queue (`promptQueueing`, `acp-agent.js:1257`), and the SDK runs it after a failed turn (F8).
- **§4.1 (`design:217`) and §12 (`design:611`):** *"a plugin's harness … reads nothing by code."* This holds only for a plugin
  with no `accountOf`. One with an owner reads the owner's tables through the same fallback (`DeclaredAcpAdapter.cs:52`), and
  row 1g relies on a plugin stand-in reading transient (F9).

**New holes.**
- The transient release loops (F1).
- An open's words left unsent loop through `WordsOn` (F2).
- An Open quest's release starts afresh (F3).
- The hand-over press and the awaiting-resume path (F4).
- The watch's quiet clock at a try after a wait (F7).
- The settling turn's place in the episode (F8).

**Rows whose proof cannot pass as designed.**
- 1a's *"the `retry` release resumes the same conversation id"* fails for an Open quest without commits (F3).
- 1a's retry of a stand-in that fails before taking its quest is refused by the owner's check as cited (F6).
- 1a's main stand-in, read through an own-or-owner `ErrorWords`, reads fatal (F5).
- 1g's plugin stand-in reads nothing (F9).
- 1a also changes the shared capture for every kind of run unless it is gated (F6).

The order of the rows is otherwise buildable.

**D210's body.** It matches the design, except:
- point 3's wording of the release (F11);
- point 3's *"Past the bound … until the person's word or Try again"*, which carries F1's and F2's wrong claims (fixed with them).

### 3. Findings

1. **The *Try again* release loops without the person.**
   - **What is wrong:** the release is `config.WithReleased(quest, session)`. It is one entry per quest, kept until the quest
     closes or a later release replaces it (`Platform/DriverConfig.cs:95-123`, `:129-139`). `TransientHolding` "passes a released
     record" (`design:353-357`). The released start takes D206's step 1, which continues the **same record**: `start with
     { Resumes = keptWord.Record, GoesOn = true }` (`Driver.cs:2267-2272`), then `ContinueAsync`, where `sessionId = park.Session`
     and that record moves back to `working` (`DriverContinuations.cs:226`, `:272-274`). If that run fails again past its bound,
     the same session ends `ended.transient` and `Releases(quest, session)` is still true. So the next look carries it on again,
     every 31 minutes or so, for as long as the outage lasts. No strike counts it (`ServiceClient.cs:2111` passes the code). The
     same happens after the person's word resumes a record they once released. A stop's release does not loop only because its
     carry-on opens a new session (D126's *"a later stop holds it again, since its session differs"*).
   - **Where:** `design:353-360`; `Planner.cs:1459-1467`; `DriverConfig.cs:106`; `Driver.cs:2267-2288`; `DriverContinuations.cs:226`.
   - **Blocking.**
   - **Smallest change:** key the transient release to the end it releases, not to the session alone. Either the release names
     the record's `ended.transient` end (its time), and the hold passes it only for that end, or the start that takes the release
     clears it before `ContinueAsync`. Key Daoris's go-on for the release by that same end, since the per-episode key
     `TransientRetry(session, episode)` names no episode across runs. Row 1a's proof: release, the resumed run fails past its
     bound again, and the next looks start nothing.
2. **Words an `ended.transient` run never sent go on at the next look, ahead of the hold, with no strike to end it.**
   - **What is wrong:** at the open (§5.6), past the bound, the resume's blocks were never prompted. The conclusion then judges
     `words = WordsAfter.Taken` without prompting (`DriverContinuations.cs:699-701`). It neither takes them off nor marks them
     (`:704-707`, `:717`), and it leaves Daoris's word too, since `limitWords` is empty when `words` is `Taken`. So the person's
     words, or Daoris's go-on kept just before a release's `ContinueAsync`, stay on `said`. `WordsWaiting` counts any word
     (`Planner.cs:355`), and `WordsOn` is judged before the hold (`:1325-1329`, `design:347`). The next look goes on with them,
     fails at the open again, and so on. Today the strikes end this through `GoOn`'s `Consider`; `ended.transient` counts none.
     In row 1a, before §5.6 lands, a failure `Where: open` must not read as transient either, for the same reason.
   - **Where:** `design:345-357`, `:415-423`; `DriverContinuations.cs:699-725`; `Planner.cs:1494-1506`.
   - **Blocking** (for 1a2's open, and for 1a if it reads the open).
   - **Smallest change:** at an `ended.transient` end with nothing prompted:
     - Daoris's go-on comes off by id;
     - the person's carried words are marked for the person, with a `GoOnMarks` reason that `Waits` honours and their next word
       releases (a mark is right here, since words exist);
     - until 1a2, a `Where: open` failure keeps today's end.

     Say also that the hold is released by any word waiting (`Waits`), not by the person alone, or judge non-person words after
     the hold. Row 1a2's proof: a resume whose open fails past the bound, then the next looks start nothing.
3. **An Open quest's release has no path back to its record, and the act has no case.**
   - **What is wrong:** after the release, an Open quest reaches `RetriedFrom` only with commits (`Planner.cs:1391-1397`),
     `LimitedFrom` only with the `limit` flag (`:1400-1405`), and otherwise `Consider`, a fresh start in a new tree (`:1355-1366`).
     So *Try again* briefs nothing and resumes nothing, against 1a's proof for an Open quest. `StandingChoices.Retry` refuses
     anything not `Exhausted`, `Stopped` or `Paused` (`StandingChoices.cs:108-131`), so the `retry` act needs the hold's verdict.
     *Note:* step 1 is protocol-door only (`DriverContinuations.cs:58`), so on the pipe door the release goes briefed until
     D206's row 1f. Rows 1c and §12 should say so.
   - **Where:** `design:353-357`, `:590`.
   - **Not blocking.**
   - **Smallest change:** `LimitedFrom` (or a sibling) also answers a released `ended.transient` record, ahead of
     `RetriedFrom`'s commit rule. `Retry` maps the hold's verdict to the release. 1c and §12 name the pipe door's briefed release.
4. ***Go on now with…* and the awaiting resume pass the hold unsaid.**
   - **What is wrong:** a hand-over of an ended record writes `HandedQuests` with `From` set to that record (`DriverHandOvers.cs:102-103`,
     `:133`). `StopHolding` passes it (`Planner.cs:1465`), but a hold read by the note's code alone would keep *Go on now with…*
     (§6.2, D210 point 7) dead. A Taken quest whose D79 resume ended `ended.transient` is planned `Resume` again
     (`Planner.cs:1367-1371`), and `WordsOn` skips it (`:1496`). §5.1's list of what the hold precedes (`design:346-349`) omits
     it.
   - **Not blocking.**
   - **Smallest change:** the hold passes a hand-over made from the held record, as `StopHolding` does. It sits directly after
     `WordsOn`, before the awaiting resume as well. For an awaiting quest, *Try again* is the release.
5. **`ErrorWords`' owner fallback, as specified, hides `claude-code`'s table from `claude-code-acp`.**
   - **What is wrong:** described in §2 above. `ClaudeAcpAdapter` has its own toolchain, owned by `claude-code`
     (`Adapters.cs:1912`, `AccountOf: "claude-code"`). It would carry the `-32000` declaration (§4.4), and an own-or-owner
     resolution then never reads `overloaded`.
   - **Where:** `design:209-217`, `:253-259`; `HarnessRoster.cs:120-129`.
   - **Not blocking**, since 1a's red-first proof would catch it.
   - **Smallest change:** the reader reads the adapter's own entries and then its owner's, both, own first. Or the `-32000`
     declaration is its own field beside the table.
6. **The driven owner's check, and which runs 1a retries.**
   - **What is wrong:** `QuestMovedAsync` reads the BGWAIT1 delegate (`Driver.cs:3445-3460`), which is `Observation.Parks`:
     `Taken`, and taken by this session. A first run that fails before its take, a likely moment for an overload, would get no
     try. A check that cannot be read is also unsaid. And the capture is shared by builds, intakes, helpers, opinions, partners
     and voices (`Driver.cs:3685-3693`).
   - **Where:** `design:405`, `:590`.
   - **Not blocking.**
   - **Smallest change:**
     - the driven check is its own predicate: no try when the quest is done, submitted, awaiting, stood down, closed or deleted;
       an Open quest or one this session holds goes on;
     - a check that cannot be read is a yes, as `QuestMovedAsync`'s unread look is;
     - a run handed no `TransientRetry` keeps today's end, and 1a hands it to driven quest sessions alone (`Driver.cs:2683`,
       `DriverContinuations.cs:431`).
7. **The quiet clock at a try after a long wait.**
   - **What is wrong:** `Heard` is stamped only by the capture's lines and output appends (`SessionWatch.cs:173-176`), and a
     later try's prompt is neither kept nor shown. So when `WaitsOnTransient` falls after the 16-minute wait, the session already
     reads at least 16 minutes unheard: past Q's mark at its 10-minute default (`:90`, `:323`). A headless `--quiet-end` shorter
     than the wait ends and carries on the run (shape 1's loss) the moment the try begins (`:342-348`).
   - **Not blocking.**
   - **Smallest change:** the wait's end stamps the session heard, so a try's quiet counts from its prompt. 1a's held-clock watch
     proof covers a silent try after the 16-minute wait.
8. **The settling step's turns are unplaced in the episode.**
   - **What is wrong:** described in §1's C4 row and §2. Settling the wire after a failed head runs each queued word's whole turn
     (`acp-agent.js:2750-2778`). That turn may answer, in which case the agent went on, or fail again.
   - **Where:** `design:290-301`.
   - **Not blocking.**
   - **Smallest change:**
     - an answered settling turn ends the episode with no try;
     - a settling turn that fails transient is that failure, and counts as a try;
     - the *never taken* reason reads *its prompt answered with an error before any update of its turn*, not *the agent never
       read it*.
9. **Row 1g's plugin stand-in cannot read transient.**
   - **What is wrong:** a declared plugin harness has no `ErrorWords` (`DeclaredAcpAdapter.cs:46-59`). It reads an owner's tables
     only through `accountOf`.
   - **Where:** `design:597`, `:217`, `:611`.
   - **Not blocking.**
   - **Smallest change:** 1g's stand-in declares `accountOf: claude-code`, so it reads that table by the owner's fallback, or 1g
     points a built-in adapter at a stand-in. §4.1 and §12 say *a plugin with no owner*.
10. **The zh product name in running text.**
    - **What is wrong:** the glossary's *Daoris* says *Daoris* in running text, keeping 道衍 for the wordmark and 问道衍
      (`glossary.json`, the `Daoris` entry). Locale strings follow it (`locales/zh/agents.json`: *Daoris 驱动…*).
      `agent.transient-retrying`'s zh reads 道衍会在本对话中重试.
    - **Where:** `design:513`.
    - **Not blocking.**
    - **Smallest change:** *Daoris 会在本对话中重试*.
11. **D210 point 3's wording of the release.**
    - **What is wrong:** *"until the person's word or Try again resumes the same conversation through D206's step-1 resume"*.
      Only *Try again* goes by step 1; the word goes by `WordsOn`'s `GoOn` (MSG1b, `ContinueAsync`), as the design's §5.1 says.
    - **Where:** `D210.md:40-42`.
    - **Not blocking.**
    - **Smallest change:** *"…until the person's word (MSG1b's go-on) or Try again (D206's step-1 resume)…"*.

**11 findings, 2 blocking (1, 2).**

**Verdict: accept after named changes.** The foundation now holds:
- four kinds read at the interface from the door's own failure;
- the limit first, by sentence and then by code with the unstated reset;
- `-32000` only where an adapter declares it;
- an unread error fatal;
- the episode seated inside `AcpSession.RunAsync`, with the wire settled, words held during a wait, one prompt a try, the wake
  gate and the owner's check;
- a word on the wire never sent again;
- a hold read by the note's code.

A misread kind does no harm worse than today's, and a retry repeats neither the person's words nor a turn's effects. What does not
yet hold is loop-freedom past the bound, in two places, each with a small change the steward can check against the cited lines
without another read:
- **Finding 1:** key *Try again*'s release to the `ended.transient` end it releases, or clear it as its start continues the
  record (`DriverConfig.cs:106`, `Driver.cs:2267-2272`, `DriverContinuations.cs:226`). Otherwise a released line cycles with no
  strike.
- **Finding 2:** at an `ended.transient` end with nothing prompted, take Daoris's go-on off and mark the person's carried words,
  so `WordsOn` does not take the record up ahead of the hold (`DriverContinuations.cs:699-725`, `Planner.cs:1325-1329`,
  `:1503-1505`).

Findings 3, 5 and 6 should be written in before row 1a is built, since 1a's own proof fails without them. Findings 4 and 7 to 11
can be taken in their rows.
