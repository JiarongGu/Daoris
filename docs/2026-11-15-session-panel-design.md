# A session's panel and its row (OUTPUTPANEL1, SESSIONROWS1)

Status: proposed, 2026-11-15. Source: brainstorm `uxpanel1`, both makers (Anthropic `g1`, OpenAI `g2`, synthesis `s1`), on the person's
two notes: a good desktop agent app keeps an output panel beside the conversation that gathers what the session made or has running,
and the session menu's rows merge the workflow's current step into each line, which reads messy now that a session holds many turns
and waits on steps (D293). Built after the foundation rows.

## The shape

1. **One session panel in the right dock, in a fixed order.** Scoped to the chosen session: where it stands and whom it waits on;
   *Passage* (steps passed, current, to come, failed; a check's attempts and its verdict); *Running* (background tasks, live
   previews, the step under way); *Made* (files and outputs, one row each, opening the read-only file tab). Headers carry counts;
   empty is told apart from loading. A view the frame already owns (the right side bar), no new place.
2. **Two-line session rows.** Line one: the state's glyph and word, the name, the pin. Line two: one standing phrase (whom it waits
   on, step *k* of *n*). No step marks or nested turns in the row or the session menu. The passage is drawn once, at the panel's
   top; each turn is a numbered divider in the conversation (cause, step and attempt, agent, account, spend), with a jump index.
3. **One layout read by the session's standing.** Working: Running open, Made growing. Waiting on a step: the agent paused at no
   cost, Passage open at the waiting step with its attempt and last verdict; a check in flight reads as running. Waiting on the
   person: only by the attention judge, with a door to the head's press. Ended: landed, declined or abandoned, kept as a receipt
   (passage, results, spend).
4. **Live from the existing state owners.** Standing, passage and turns read the one pushed session row the list reads, so list and
   panel never disagree. Running reads background tasks by session; heavy bodies (files, logs, diffs) are asked only for the chosen
   session's open section and asked again on its state change. No timer; counts tick, new rows are marked, focus and scroll hold.
5. **The risks met by rule.** Width: one collapsible dock view in one column, never a fourth column or a viewport breakpoint; closed,
   a head chip with the step and counts opens it. Many sessions: fixed-height rows, the groups .NET pushes, Ended folded, windowing
   if measured. No workflow: an explicit case (no steps, checks or landing claims; the row says the conversation's state), told
   apart from a passage not yet read or not readable.
6. **One way per job, after an inventory.** Passage owns a session's run and check detail, Running its background tasks, Made its
   outputs and files; existing links select the section instead of opening another summary. Repository Files, the place's Workflow
   tab, Review, Notifications, Ask Daoris, Console and Terminal stay. A per-session copy is removed only once the panel covers its
   jobs, after an inventory of today's surfaces.

## The first slice

Thin the row to its two lines; move the row's step and turn parts into the chosen session's dock view (data unchanged); add Running
from background tasks filtered by session. Files, outputs, previews, the ended receipt and the retirements wait for a second slice.
Proof is the installed window across one session's whole life.

## Open before the second slice

What At work, Timeline, Workflow and Review draw today for a session; whether background tasks are keyed by session and pushed; the
reads that list a session's files, runs, previews and outputs (only `SESSION_FILE` is named); attribution of outputs to a session that
survives tree clean-up; the end of a workflow-free session; the installed window's measured widths and a real list's size.
