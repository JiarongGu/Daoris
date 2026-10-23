# A conversation that meets an account's limit goes on, in the same conversation, on the next account (LIMITGOON1, 2026-10-23)

**Status: a design note for LIMITGOON1**, written by Claude Opus at `a92ea02a2` before its build, which follows it. It settles
one question the row leaves open, which carrier moves the context, and says what the build does. D197 and D189 carry notes
pointing here.

## 0. The case

The owner's report, 2026-10-23 07:28 UTC: an Ask Daoris conversation's account answered *You've hit your weekly limit* twice. Both
turns failed with the person's words in them; the record ended; a new Ask Daoris session opened on the next account with nothing
carried (`account.rotated … carries=null why=cooling`), so the person's lines stayed in the old chat, now under history.

Read at `a92ea02a2`:

- **A driven session already carries on.** A limit at a driven record's conclusion cools its account (`Driver.AccountLimited`), and
  the planner's next start carries the quest on in a new part on the next account of the walk, `from` why `limit` by `daoris`
  (`Loop/Driver.cs`, `ContinuedFrom(carried.Session, HandOverWhy.Limit, HandOverBy.Daoris)`), handed Daoris's brief since SESSCTX1b,
  and `account.rotated` names the part it carries (`carries=`).
- **A conversation meets a limit and stays.** Its refused turn is read for a limit (`ChatRunner.Limited`, D125's TOOL4d note): the
  account cools, the record says so (`account.cooling`), the turn ends `error`, and the conversation goes on, on the same cooling
  account. Every later word is refused the same way. Nothing moves it: the usage curve's move (SWITCH1c-ii, `CurveMove`) judges a
  turn's end by what the account said of its windows, never a refusal, and passes Ask Daoris by; the go-on with Daoris's brief
  (SESSCTX1c part B, `BriefedMove`) moves only a proven loss, on the same account.
- **The person's words are not lost but stranded**: each failed turn's words were taken (its `user` event) before the refusal, so
  nothing waits on the record's `said` to go on with, and a new conversation starts with none of them.

## 1. The carrier: Daoris's brief

The agent's own resume cannot move with the account. A conversation lives in the harness's store in the configuration home of the
account it ran on (D131 point 2; D189 point 1): Claude Code's transcript under that home's projects, Codex's thread under its
own. Resuming it on the next account means copying or writing that store from one account's home into another's, which D189
rejects ("copying or writing a harness's store across accounts or makers") and its point 5 forbids ("anything read or written in
a harness's store"), for D125 point 4's reason: an account's home is the account's, and Daoris never reads it. The design's own
table (session-context design §2) already says it: *Account — no: the conversation is in the other account's home — a new part*.

So the context moves as **Daoris's brief** (D189 point 4): a pure function of the line's records, composed without a model, the
person's words verbatim first. It is what a driven carry-on after a limit is handed already, and what a conversation's go-on after a
proven loss is handed (SESSCTX1c part B), so a limit adds no new carrier.

## 2. What is built

1. **A limit mid-conversation moves it.** Where a conversation's turn is refused for its account's limit (the account now cooling),
   the conversation moves at that turn's end, never mid-turn, to a new part of its line on the next account of the same agent the
   walk offers (the cooling one passed), handed the brief and then the words the refused turns held, as the person's, in one prompt:
   its record `from` the old part, why `limit`, by `daoris`. The old part ends first (D51), its process stopped. A repository's chat
   opens the new part in the same tree; Ask Daoris opens it in its workspace's room, the link kept in the room's history (its record
   takes no `from`).
2. **The held words** are the person's words of the turns refused by the limit since the last turn that ended otherwise, read from
   the record's events: what the refused turns took, never another agent's answer or Daoris's own words.
3. **Said once**, in the conversation, in English and Chinese: the old part's line names the move and why (`chat.limit-moved`), and
   the new part's first line where it came from (`moved.from`, as every move's).
4. **Where nothing has room** (every account cooling or signed out), it stays and its words stay as they are; the next refused turn
   tries again. A teammate's record never moves (D47 §6), and a conversation the person ended moves nothing, since no turn of it is
   refused.
5. **One conversation on the page.** Ask Daoris's history folds a conversation that continues another into the one it continues:
   the line is listed once, under its newest part, and opening it shows the parts in order. A repository's chat is already one
   session by its parts (D167, SESSCTX1d).

## 3. What the checks do not cover

The stand-in agents prove the move on the protocol door; no real account's limit, and no native-door conversation's, has met it.
The window has not been seen.
