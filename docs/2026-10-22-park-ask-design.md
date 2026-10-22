# A parked session's ask at its conversation's end, with its answers as presses (PARKVIEW1, 2026-10-22)

**Standing:** settled under D191 on 2026-10-22 by the steward, after Codex's read (§8); built by PARKVIEW1 on the same branch. A
section of the UI/UX design's family (D185, whose dated note records the read). Fable did not read: the design adds one file beside
a session's record in the hand-over ask's shape (§2), read by one route, and one optional field on an existing press's request
(§5); it changes no record, no graph, no store, no answer's shape and no decided rule, which are D191's foundations.

Read for it: the row PARKVIEW1; D131 (an answer continues the session), D135 (go-aheads: KNOWUSE1a, KNOWUSE1c, GOAHEAD2,
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
| The box offers no answer: *Carry on with this answer* is disabled until words are typed, and the readings the agent offered are not presses. | `Composer.tsx:637-640` |
| Nothing says what the session holds while it waits, nor that no deadline runs. | (absent) |
| Answering the last open go-ahead sends the park on with the blank *carry on.*, so a question asked beside it goes unanswered unless the person answered it first. Nothing says so. | `ConversationRoutes.cs:208-234` (`goesOn: true`); D135's GOAHEAD2 |
| At the top, the head's card quotes the same whole report verbatim. | `AwaitingPerson.tsx:90-92` |

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
and denied to a brainstorm's pass and a second opinion's reviewer as every acting tool is. Its input:

| Field | Bound | What it is |
|---|---|---|
| `question` | 1-1,000 characters | what only the person can decide, in a sentence or two: the decision, not the report |
| `choices` | 0-6, each `{ label, does }` | the answers the agent offers: `label` (1-80) as the person would press it, `does` (1-300) what the agent will do on it |
| `recommend` | a choice's number, optional | the one the agent recommends (`autonomous-development` asks a park for a recommendation) |
| `by` | 1-200, optional | by when the work needs the answer, as the agent knows it (a release window): its claim, since Daoris keeps no deadline |

A go-ahead is not asked here: the tool says so, and an act's yes stays `go_ahead_ask`'s, held on the ask for every session there.

**Who may ask.** A session the driver started on a quest, this machine's (as `HandOverAskBox.Unfit` judges: not a helper, a
reviewer, an intake or Ask Daoris's conversation, and not a conversation of the person's, whose person reads it as it comes). Any
other is refused with the sentence the agent reads, and nothing is kept.

**Kept** as `<home>/sessions/<session>.question.json` `{ id, session, question, choices: [{ label, does }], recommend, by, at }`,
written whole and atomically by the connector (`QuestionBox`, the service), one per session: a later ask replaces it, since the
latest is what it asks now. The driver's `QuestionFiles` reads it; the two share no code and each side's tests hold the shape, as
the hand-over ask's two files do (`HandOverAskBox.cs`, `HandOverAskFiles.cs`). It is machine-local: no wire carries it, and a
teammate's page has none. It joins the session's own files under `sessions/` (`SessionHomeFiles`), so clearing its history takes it.

**Bound to its park by the driver.** At each conclusion of a run of the record, the first run's (`Driver.cs:2273-2337`) and a
resumed run's (`DriverContinuations.cs:461-565`), before the record moves, the driver keeps the file only where the conclusion
parks (`awaiting-person`) and the question was asked since that run opened (`openedAt`); otherwise it deletes it. So a question on
file while the record is parked is that park's, and a resumed run that parks again without asking shows none. One write by the
connector during the run, one settle by the driver after it: never two writers at once.

**What the agent is told.** The tool answers: the person sees the question at the end of the session's conversation with each answer
a press; say in your last message that the work waits on it, commit what you have and end your turn with the quest still taken;
the answer comes back as your next prompt, in the person's words, a choice as its label, then any words of theirs. Every driven
instruction's closing paragraph (`TargetPrompt.Closing`) gains one sentence: where the connector offers `question_ask`, ask what only
the person can decide there before ending the turn on it, with the answers offered and the one recommended.

## 3. The ask at the conversation's end

**Where.** At the foot, above the box and outside the conversation's scroll, in the strip's place for the park's question
(WAITFOOT1b), where the person reads the last message: the strip now draws the question rather than counting it. It keeps the strip's
rules: bounded at 40% of the main area and scrolled within; standing aside while the head's waits are wholly on screen
(WAITFOOT2a), where the head's card draws the same question. Nothing waits, nothing is drawn.

**What it says**, in this order (`work/waits/ParkAsk.tsx`):

1. **Its question**, the agent's words as written, then each answer it offered: the label, *recommended* beside the one it
   recommends, and what it will do on it, the agent's words. With no question recorded, one line: it asked in its last message
   above, *In the conversation* beside it.
2. **The go-aheads it asked and still waits on**, each as the ask lists it (act, where, the agent's why, when), read-only here.
3. **By when.** *No deadline here: it has waited 12 min, and waits until you answer*, from the record; then, where the agent named
   one, *It says it needs your answer by: …* in its words.
4. **Meanwhile**, Daoris's facts from the record: its quest stays taken, its branch is kept (named where it has a tree of its own),
   and it runs nothing until it is answered.
5. **What an answer does**: the same session goes on with it at the driver's next look; where a question and a go-ahead both wait,
   that a go-ahead answered keeps it waiting for the question, whose answer sends it on with every go-ahead's answer handed beside.

The head's waiting card (`AwaitingPerson`) shows the question and its answers (1 and 3's agent line) in place of the whole last
message where a question was recorded, and the note as today where none was.

## 4. The box's presses

The box of a parked session (`SessionBox`, answer mode) carries every answer as a press, above its one text field, so there is one
place to write and one to press:

- **A choice**: a press per answer offered, its label, the recommended one primary. It sends the label, then the box's words
  beneath it after a blank line where there are some.
- **A go-ahead**: per go-ahead still open, *Approve #n* and *Refuse #n*, the box's words going as that go-ahead's words.
- **A free answer**: the box's words alone, with *Carry on with this answer*, as today.

The field's placeholder says its words go with the answer pressed, or alone. The go-ahead's own field and presses leave the strip at
the foot, where the box carries them; the head keeps its `GoAheadList`, the same handler (one set of handlers, D168 point 3). A
teammate's park and a browser draw no press, as today.

## 5. How each answer reaches the session

Every press is the person's, through the doors the box already uses; none is new, and none skips the person's door: the window's
press is theirs, and a terminal's press of either route still waits for the window's Confirm (`PipeCensus.cs`: both `Person`).

| Answer | Door | What is kept, and what goes on |
|---|---|---|
| A choice | `SESSION_INPUT` `{ id, text }`, `text` = the label, then the words | kept on the record's `said` as the person's words (D137); the park takes them as its answer, and the same conversation resumes with them at the driver's next look (D131, MSG1b) |
| A free answer | `SESSION_INPUT` `{ id, text }` | as today |
| A go-ahead | `SESSION_GO_AHEAD` `{ id, ask, number, approved, words, goesOn }` | the answer kept on the ask with the words (GOAHEAD2b). New: `goesOn: false` while the park's question is recorded and unanswered, so the park waits for that answer and the resumed run is handed the go-aheads' answers beneath it (`GoAheadsText.Resumed`); absent or `true` as today |

`goesOn` is an optional field of the request, read as `true` when absent, so an older page sends what it did; the answer's shape
does not change. Its toast already says a park still waiting (`goAheadToast`'s *It still waits on you*).

## 6. What it does not do, and what was rejected

- **Parsing the closing note for its *Needs you***: a format agents never agreed to, misread at the person's cost (§1).
- **A question as a field of the session's record**: it would travel to a teammate's mirror and need a store column on every
  build that opens the store (`compatible-builds.md`); a file beside the record, as the hand-over ask is, needs neither.
- **A choice as a toggle and one composite *Send***: two doors in one press, ordered by the page, where one press per door says
  what it does.
- **A deadline Daoris keeps**: none exists, and an invented one is a promise nobody keeps (D185's rejection of a countdown).
- **Go-ahead presses beside the strip's own field**: the two fields the person could not tell apart are the report's defect.

## 7. Proof

Each answer kind reaching the session is a test at the frame (`WorkFrame.test.tsx`: a choice sends its label and the words through
`SESSION_INPUT`; a go-ahead sends `goesOn: false` while the question waits and the box's words; the free answer as before) and at
the doors (`DriverModuleGoAheadTests` for `goesOn: false`; the question's route in `DriverModuleSessionQuestionTests`; the tool in
`McpQuestionToolTests`; the file and its settle in `QuestionFilesTests`). The stories draw the foot at the frame's three widths in
English, 中文 and dark, the owner's case among them in neutral words: a session parked on a production go-ahead with the person's
words in the box.

## 8. The reads

| Reader | Findings | Taken | Dropped, and why |
|---|---|---|---|
| Codex (`codex exec -s read-only`, 2026-10-22) | (filled in below) | | |
