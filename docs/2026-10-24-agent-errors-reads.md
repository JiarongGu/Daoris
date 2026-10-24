# AGENTERRORS1: the reads of round 1 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-24-agent-errors-design.md` and D210 at `6f089fe1f`, each verbatim but for its headings set one level down. The steward ran both from outside the install, read-only, on the design's own tree. Codex asks another round; the Opus foundation reader would accept after named changes (16 findings, 3 blocking, and three more to take before acceptance). Round 2 answers both.

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
