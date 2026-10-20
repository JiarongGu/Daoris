---
name: person-waits
applies_when: adding a new thing a session waits on the person for (a go-ahead, a helper's yes, a held tool request, a rule proposal, an opinion), or changing where an existing wait is drawn, answered or counted
enforces: a wait the person answers is one kind in `work/waits/waits.ts`, drawn in the session's head and foot from one `WaitProps`, a row judged in .NET (`NeedsAPerson`) and worded with its acts in *What needs you*, which the badge counts, its hook named in Ask Daoris's coverage, and its words placed in the glossary's kinds, in the same change
---

# Waits the person answers — every place one goes

**A wait the person answers is drawn in four places and counted in one, and none of them finds a new kind by itself.**
AUTONOMY1b3c (D184) built the held tool request and rediscovered each place below; the helper's ask (MIXAGENT2c,
`useHelperWaits`) is the closest exemplar, since it too is a live wait per session with a hook, a band row with acts and a
badge count. A rule proposal's (D74) is the older one.

| Place | File | What goes there |
|---|---|---|
| The kind | `src/Daoris.Web/src/work/waits/waits.ts` | a member of the `Wait` union and its line in `waitsOf`, in the order the head draws them |
| The foot | `work/waits/WaitStrip.tsx` | its folded name (`waitName`) and its section in the strip |
| The head | `work/head/SessionHead.tsx` | its section, `data-wait-section` where it can be answered from there |
| The props | `work/WorkFrame.tsx` → `work/AttendedSession.tsx` → `SessionHead` | `WaitProps` built once in the frame, so the head and the foot press the same handlers |
| *What needs you*'s judge | `Daoris.Desktop.Modules/Loop/NeedsAPerson.cs` (the row: kind, id, since, the values it is worded from), `AttentionStateSlice.cs` (its input read from memory, `Fields` and `Holds`, its wake) | a row judged in .NET once for every window (PUSH1d4, D182 point 6); its cases in `AttentionStateSliceTests` |
| *What needs you*'s words | `work/attention/attention.ts` (`PushedAttention`, `KIND_ORDER`, `attentionWorded`, the groups, the offers), `work/attention/AttentionRow.tsx` (the kind, its act ids, handler and labels), `work/attention/AttentionBand.tsx` (the answer and its notice or refusal) | a row in the group its waiting belongs to (*Holding work* when a turn stands still on it), worded from what is pushed |
| The badge and the door | `app/App.tsx` | the pushed rows counted (`useAttention`), so the taskbar's count holds the wait once .NET judges it; a door that opens the session |
| The hook | the domain's bridge file (`bridge/sessions/sessions.ts` for a session's) | read and answer; a key under the scope that wakes it, or a poll while nothing publishes it |
| Ask Daoris | `Daoris.Desktop.Driver.Tests/Help/HelpCoverageTests.cs` | the answering hook in the `Starts` table (a hook pressed under `work/`), a door or an `Exempt` with its reason; `Controls` is for Settings' domains and places |
| Words | `locales/{en,zh}/work.wait.json`, `work.attention.json`, `note.json` for its coded notes, each code's values in `work/noteLines.ts`; `locales/glossary.json` | each new label under its kind; a label over its budget needs an accepted judgement, as `work.attention.rule` and `work.attention.helper` have; `note.test.ts` holds the notes' map to `NoteCodes` |
| Tests that pin the set | `AttentionStateSliceTests` (the row's membership and values, its fields classed against the page's `PushedAttention`), `work/attention/AttentionBand.test.tsx` (the band asks only the roster), `work/attention/attention.test.ts` (the group table, the order, the words), `app/sessionsBadge.test.tsx`, `work/attention/attention.stories.tsx` (`Required<AttentionActs>` fails the typecheck when an act is missing) | updated in the same change |

## The rules each place keeps

- **One set of handlers.** The frame builds the wait's handlers once; the head, the foot and the band press the same ones, so
  an answer from any of them is one answer.
- **Never an answer the asker did not offer.** A press per choice offered, by its own id; where none allows, the person can
  only refuse (D52, D184's AUTONOMY1b3a note).
- **Listed only with an act.** A row is in the band only where the person has something to do about it: a press on the row,
  or a door to the place of the press. What the cycle settles by itself, or a guard nothing of theirs lifts, is said on its own
  page instead; a unit of work is a row only where the clean-up's press settles it (OVERVIEWNOISE1b, D173's note).
- **Counted where it holds work.** A wait whose turn stands still until the person answers counts in the badge; a wait nobody
  sees becomes a refusal at its bound, or work that never moves.
- **"waiting on you" is taken.** `work.head.waiting` and `sessionState.awaiting-person` say it of a parked session, and the
  page's end-to-end spec reads the phrase in an intake's region (`e2e/platform.spec.ts`): word a new wait differently.
