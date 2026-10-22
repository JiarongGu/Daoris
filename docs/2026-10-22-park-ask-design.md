# A parked session's ask at its conversation's end, with its answers as presses (PARKVIEW1, 2026-10-22)

**Standing:** settled under D191 on 2026-10-22 after Codex's read (§8), each finding checked against the code before it was taken;
built by PARKVIEW1 on the same branch, and recorded in D185's dated note. Codex holds that the question's file (§2) is a protocol
between two processes, which D191's amendment counts as a foundation for Fable's read; the builder had no Fable reader, so that read
is named for the steward to run or waive (§8). Nothing here publishes, spends an account, signs in or removes the person's data.

Read for it: the row PARKVIEW1; D131 (an answer continues the session), D135 (go-aheads: KNOWUSE1a, KNOWUSE1c, KNOWUSE1a2, GOAHEAD2,
GOAHEAD2b), D137 (words to a session, MSG1f), D126 §9, D168's waits at the foot (WAITFOOT1-2), D196 and the UI/UX design's R1-R7;
`.claude/knowledge/person-waits.md`, `session-words.md`, `bridge-presses.md`, `translation-parity.md`; the glossary. Every
statement about today was read from the code at `662151ce1`.

## 0. The case, and what the person sees today

The owner reported (2026-10-22) that when a session waits on them (等你处理), its ask is hard to find and hard to read at the
conversation's end, and the chat box offers no fitting answer, so they usually cannot answer it correctly. Drawn today in a real
browser at the main area's three widths in both languages (a scratch harness over the frame's own molecules: the conversation, the
strip and the box, as `WorkFrame.tsx:2288-2391` lays them out), a session parked on a production go-ahead and one question shows:

| What the person sees | Read at |
|---|---|
| The agent's whole closing report as the conversation's last message: *Needs you*, *Readings*, *Tests*. The question and its two readings, with the agent's recommendation, are a bullet of prose inside it, and nothing marks which bullet is the question. | `Observation.cs:150-158` (the park quotes the last words); `Adapters.cs:855-865` (the closing note's two lists, words and not a format) |
| *2 waiting on you* above the box, then only the go-ahead: the park's question is counted and not drawn, since the box below "is its door". The person reads a count of two and finds one. | `WaitStrip.tsx:136-139`, `:156` |
| Two text fields: the go-ahead's *your words, if any* with *Approve* and *Refuse*, and the box's *your answer — the same session goes on with these words*. Nothing says which field answers what. | `GoAheadList.tsx:175-194`; `SessionBox.tsx:135-146` |
| The box takes a free answer (`SESSION_INPUT`, `WorkFrame.tsx:757-784`), and nothing else: the readings the agent offered are not presses. | `Composer.tsx:637-640` |
| The note's lead says the quest stays taken; nothing at the foot says that, that its branch is kept, that nothing runs, or that no deadline runs. | `Observation.cs:154` |
| Answering the last open go-ahead sends the park on with the blank *carry on.*, so a question asked beside it goes unanswered unless the person answered it first; answering the question first sends it on with the go-ahead still open. Nothing says so. | `ConversationRoutes.cs:208-234` (`goesOn: true`); D135's GOAHEAD2; D137 MSG1b |
| At the top, the head's card quotes the same whole report verbatim, with the go-ahead's own field and presses beneath it. | `AwaitingPerson.tsx:90-92` |

At the 400 px floor the report runs to several screens, so the question is a scroll away from the box; in 中文 the chrome is
translated and the report, the agent's own words, stays as written.

## 1. What a park asks: its structured facts

A park's ask is three kinds of thing, and only one was recorded as a fact:

- **Go-aheads** (KNOWUSE1a): recorded on the ask, by number, with the act and the agent's why (`go_ahead_ask`). Answered yes or no
  with the person's words.
- **A question**, with or without answers the agent offers: today only prose in the last message. **This design records it** (§2).
- **Anything else** the agent wrote: prose, read in the conversation. Nothing here parses it (D135's KNOWUSE1c: the headings are
  words, not a format).

A question is recorded by the agent, never read out of its words: the agent is the one that knows which readings it offers and what
it will do on each, and a parser would answer the person in the agent's place where it misread.

## 2. The question, recorded

**The tool.** The connector gains `question_ask` (`QuestionTools`, beside `go_ahead_ask`), offered by the connector's default rule
and denied to a brainstorm's pass and to a second opinion's reviewer and a helper, as every acting tool is. Its input:

| Field | Bound | What it is |
|---|---|---|
| `question` | 1-1,000 characters | what only the person can decide, in a sentence or two: the decision, not the report |
| `choices` | 0-6, each `{ label, does }` | the answers the agent offers: `label` (1-80) as the person would press it, `does` (1-300) what the agent will do on it; no two labels alike, case aside |
| `recommend` | a choice's number, optional | the one the agent recommends (`autonomous-development` asks a park for a recommendation) |
| `by` | 1-200, optional | by when the work needs the answer, as the agent knows it (a release window): its claim, since Daoris keeps no deadline |

A go-ahead is not asked here: the tool says so, and an act's yes stays `go_ahead_ask`'s, held on the ask for every session there.

**Who may ask.** A driven record of this machine on a quest, working or starting. Refused, each with the sentence the agent reads
and nothing kept: no session in the connector's scope, one nobody holds, a helper, a reviewer, an intake, Ask Daoris's own
conversation, any other conversation (its person reads its words as they come), and a record on no quest. (`HandOverAskBox.Unfit`
alone judges only the first four of these, so the question's box judges every one itself.)

**Kept** as `<home>/sessions/<session>.question.json` `{ id, session, question, choices: [{ label, does }], recommend, by, at }`,
written whole and atomically by the connector (`QuestionBox`, the service), one per session: a later ask replaces it under a new
id. The driver's `QuestionFiles` reads it; the two share no code and each side's tests hold the shape, as the hand-over ask's two
files do (`HandOverAskBox.cs`, `HandOverAskFiles.cs`). It is machine-local: no wire carries it, and a teammate's page has none. It
goes with the session's history under `mark` (`SessionHomeFiles`), so a clear or a delete takes it.

**Bound to its park by the driver.** At each conclusion of a run of the record, the first run's (`Driver.cs:2273-2337`), a resumed
run's (`DriverContinuations.cs:461-565`) and a refused resume's (`:485-499`), with its final state (a stop's, a hand-over's and an
account's ending included) and before the record moves, the driver keeps the file only where that state parks (`awaiting-person`)
and the question was asked at or after the run's last handing: the later of the run's opening (`openedAt`) and the newest moment
the record shows words handed to the agent (a prompt or a word recorded as said to it, or a `sent` receipt). Otherwise it deletes
it. So a question on file while the record is parked is the one its last turn asked: one asked in an earlier park, or in a turn
that held words then went on in the same record on either door (MSG1b), is gone. A word steered in after the question, at the
turn's next step, deletes it too: the safe side, since that word may have answered it. A restart between the settle and the record's
move leaves a record the orphan sweep ends, so nothing parked shows a question it did not ask.

**What the agent is told.** The tool answers: the person sees the question at the end of the session's conversation with each answer
a press; say in your last message that the work waits on it, commit what you have and end your turn with the quest still taken;
the answer comes back as your next prompt, in the person's words, a choice as its label, then any words of theirs. Every driven
instruction's closing paragraph (`TargetPrompt.Closing`) gains one sentence: where the connector offers `question_ask`, ask there
what only the person can decide before ending the turn on it, with the answers offered and the one recommended.

## 3. The ask at the conversation's end: in the box

The box of a parked session (`SessionBox`, answer mode) carries the whole ask above its one text field (`work/waits/ParkAnswer.tsx`),
so it is always at the foot, where the person reads the last message, at every width, whatever is on screen above. It is bounded
at 40% of the main area and scrolls within, as the strip does. In order:

1. **Its question**, the agent's words as written; under it each answer it offered as a press (its label, the recommended one
   primary and marked *recommended*), and beside each what the agent will do on it, its words; then, where the agent named one,
   *It says it needs your answer by …*, in its words. With no question recorded, one line: it asked in its last message, above.
2. **Each go-ahead still open**, as the ask lists it (the act, where, the agent's why), with *Approve* and *Refuse*.
3. **By when**, Daoris's: *No deadline here: it has waited 12m, and waits until you answer*, from the record.
4. **Meanwhile**, Daoris's facts from the record: its quest stays taken, its branch is kept (named where it has a tree of its own),
   and it runs nothing until it is answered.
5. **What an answer does**: it goes to the same session as the person's words, which goes on with it at the driver's next look,
   unless a pause, a hold, the cap or a cooling account holds it, as D137 says; where it cannot resume, D131's carry-on takes it.
   Where a question and go-aheads both wait, that the go-aheads come first.

Then the text field, whose placeholder says its words go with the answer pressed, or alone as the answer, and *Carry on with this
answer*, the free answer, as today.

**One place to answer.** Where the box below is a driven park's answer box, the strip draws no park wait (today its question; now its
go-aheads too), and the head lists them without presses (a parked conversation's composer draws none of the ask, so there both keep
theirs): its card shows the recorded question and its answers in place of the
whole last message (the note as today where none was recorded), and its go-ahead list says each is answered in the box below.
The head keeps every press where no box below takes the answer (a teammate's park draws none, as today). One set of handlers
still backs every press (D168 point 3). The question and each go-ahead carry *In the conversation* (WAITFOOT1c), and a raising
block's *To the wait* for either leads to its line in the box.

## 4. The order of answers, and how each reaches the session

Every press is the person's, through a door the box already uses; none skips the person's door: the window's press is theirs, and
a terminal's press of either route still waits for the window's Confirm (`PipeCensus.cs`: both `Person`).

**The order, on this page.** Where a question is recorded and go-aheads are open, the question's presses and the free answer wait
until every go-ahead is answered, and say so; each go-ahead press meanwhile keeps the park (`goesOn: false`). Then the question's
answer sends it on, and the resumed run is handed the person's answer and every go-ahead's (`GoAheadsText.Resumed`). `goesOn:
false` is GOAHEAD2's own provision, kept "for a caller that answers the park itself", which this page now is; the service's rule
is unchanged. Where no question is recorded, a go-ahead answers as today: the last one open sends the park on.

| Answer | Door | What is kept, and what goes on |
|---|---|---|
| A choice | `SESSION_INPUT` `{ id, text, question }`, `text` the label, then the words after a blank line | refused `{ sent: false, why: 'question-changed' }`, keeping nothing, where the session's question on file is not `question` (answered and replaced in another window); else as any word: kept on the record's `said` as the person's (D137), the park's answer |
| A free answer | `SESSION_INPUT` `{ id, text }` | as today |
| A go-ahead | `SESSION_GO_AHEAD` `{ id, ask, number, approved, words, goesOn }` | the answer kept on the ask with the words (GOAHEAD2b); `goesOn: false` where a question is recorded, else absent, read as `true` |

`question` and `goesOn` are optional fields of the requests, read as absent by an older host's handler; neither answer's shape
changes. **The box's words** are taken at the press: they leave the box only once the door kept them, and only where the box still
begins with them, so words typed while the press was on its way stay; a refusal is said in the box, the words kept.

**Not covered: the ask's own page and the terminal** keep D135's rule. Answering a session's last go-ahead there sends its park on
with the blank answer though its question waits, and the resumed run is handed the go-aheads' answers and asks again. Making every
door wait for the question would change D135's rule and D137's words-send-a-park-on for every door, a change of decided rules this
design does not take (§8, Codex 3.1).

## 5. Words

The chrome is worded in both catalogues (`work.park.*`), with the glossary's names: park 挂起, go-ahead 放行, quest 委托, take 接下,
branch 分支, waiting on you 等你处理 (which a new wait must not reuse, `person-waits.md`). The agent's question, labels, what each does
and its *by* are content, shown as written in every language.

## 6. What it does not do, and what was rejected

- **Parsing the closing note for its *Needs you***: a format agents never agreed to, misread at the person's cost (§1).
- **A question as a field of the session's record**: it would travel to a teammate's mirror and need a store column on every
  build that opens the store (`compatible-builds.md`); a file beside the record, as the hand-over ask is, needs neither.
- **The ask in the strip above the box**: the strip stands aside while the head's waits are on screen (WAITFOOT2a), so the presses
  in the box would stand alone; the box is always drawn (Codex 5.3).
- **A new route answering the go-aheads and the question in one press**: ordered by the host, it would be correct on every
  timing, but adds a `Person` route for what two existing doors and an order on the page already do.
- **A deadline Daoris keeps**: none exists, and an invented one is a promise nobody keeps (D185's rejection of a countdown).

## 7. Proof

Each answer kind reaching the session is a test at the frame (`WorkFrame.test.tsx`: a choice sends its label, the words and the
question's id through `SESSION_INPUT`; a go-ahead sends `goesOn: false` while the question waits, with the box's words; the free
answer as before; the question's presses waiting while a go-ahead is open; a refusal keeping the words) and at the doors
(`DriverModuleGoAheadTests` for `goesOn: false`; `DriverModuleSessionQuestionTests` for the read and the `question-changed`
refusal; `McpQuestionToolTests` for the tool; `QuestionFilesTests` for the file and its settle). The stories draw the box at the
frame's three widths in English, 中文 and dark, the owner's case among them in neutral words: a session parked on a production
go-ahead with the person's words in the box.

## 8. The reads

| Reader | Finding (Codex's numbering) | Verdict, checked against the code |
|---|---|---|
| Codex, `codex exec -s read-only`, 2026-10-22, on the draft at `8b8a1ff84` | 1.1 free answers already work; the note says the quest stays taken (non-blocking) | taken: §0's rows say so |
| | 1.2 a resume is conditional (non-blocking) | taken: §3 item 5 names the holds and the carry-on |
| | 2.1 `openedAt` does not bind the last turn's question: a native run goes on with held words in the same record (blocking) | taken: the binding is the run's last handing (§2), held by `QuestionFilesTests` |
| | 2.2 the refused resume, a stop, a restart; the read to reject a record not parked (non-blocking) | taken for the refused resume and the stop (§2); the page reads a question only for a parked, unanswered record of this machine, which it already holds pushed, so the route answers the file and the page judges the record |
| | 3.1 `goesOn: false` is not authoritative: the ask's page answers with `true` (blocking) | taken in part: the order is promised on this page only, and §4 says the ask's page and the terminal keep D135's rule; making every door wait changes decided rules (dropped, with why) |
| | 3.2 a choice is not bound to the question shown; identical labels (blocking) | taken: the press names the question's id and the door refuses `question-changed`; labels must differ |
| | 3.3 the shared draft needs a contract (blocking) | taken: §4's words paragraph |
| | 4.1 the head's editable go-aheads beside the box's are two places (blocking) | taken: the head lists them without presses where the box answers (§3) |
| | 4.2 the D191 exemption does not hold: a protocol between processes and D135's rule (blocking) | taken in part: the standing says so and names Fable's read for the steward; `goesOn: false` is GOAHEAD2's own provision, so D135's rule is unchanged |
| | 5.1 `HandOverAskBox.Unfit` admits conversations and records on no quest (blocking) | taken: §2 lists every refusal, the box judges each |
| | 5.2 the build's checklist (non-blocking) | taken: the permission lists and their tests, the route's places, the catalogues (built) |
| | 5.3 a smaller shape: the ask and its presses in the box, the head read-only (non-blocking) | taken: §3 |
