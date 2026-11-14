# A session as the work's passage through its workflow (SESSIONLIFE1)

Status: proposed, 2026-11-14, under decision D293. Sources: brainstorms `564feb57` and `20e6f579` (Anthropic: Fable), and
`bd364ac3` (DeepSeek: dsh, `deepseek-flash`), asked the same question; the OpenAI pass was unavailable on the Codex workspace's low
spend cap, and is asked when it has room. The two makers agree on points 1, 2, 4 and the migration below. They differ on point 3:
DeepSeek moves `quest.holds` after the landing with an explicit `quest.close` before it, Anthropic keeps the holds before the
landing (the graph rules require it) and writes the quest's Done at the graph's end; the second needs no change to graph rules 8
and 9 and is recommended. The packets cut several sources at 12,000 characters (BRAINPACKET1), so each claim marked *(to check)* is
read against the code first.

## The shape

1. **A waiting state, not completed-at-hand-back.** The agent's "done" ends its turn and parks the session on the workflow's next
   step: a new non-ended state `awaiting-step` beside `awaiting-person`, with no process (so no tokens and no polling), holding
   `waitsOn` {run, node, attempt, tip}. A step that needs the agent again puts a word on the session keyed by node and attempt;
   the next look resumes it by the answered-park path that exists today. Only the workflow's end ends a session: landed, declined
   or abandoned. Check_run's `ended.awaits-check` becomes this state. *(to check: SessionLedger's moves and check_run.)*
2. **Checks are the workflow's step.** `DriverHandbacks`' per-session asks (DriverHandbacks.cs, the loop over `seen.Missing`) are
   deleted; the hand-back's reading stays a note. Each development graph holds a `check` step after `content`: its red is said to
   the open session as a word, the agent's next turn moves the tip, and a new attempt checks the new commit. After a bound of
   rounds (3 proposed) one row reaches the person, never one row per red. (CHECKHAND1 starts this.)
3. **The quest's done moves to the graph's end.** `quest.holds` stays before the landing (the graph rules keep holds before
   landings) but reads the turn's submission instead of a Done; the quest's Done and the session's end are written together when
   the run reaches its end. This changes who writes quest state (the runtime, not the session's own connector) against driving rule
   2: the person's call, and the riskiest step, since other machines and dependent quests see the quest stay Taken longer.
4. **One run is one session, and the tree lives as long as it.** A step addresses `run:<run>`, its open session, then its tree,
   taken through the tree lock lent to one step at a time, so no agent turn shares the tree with a check. The tree is never tidied
   while the session is open; a missing tree is said once and grown again from the branch's tip. *(to check: whether a chain's
   later quests in one repository are rounds of one session.)*
5. **Each agent conversation is a numbered turn inside the session.** Today's `from` chain becomes a list of turns, each with its
   cause (a step, the person, a limit, the curve), the node and attempt that woke it, its agent, account, conversation and usage.
   A limit or a curve move ends a turn, never the session, and each wake chooses its account afresh; a conversation is resumed only
   on the same agent and account, otherwise the turn starts from the session's context (CONTEXT1).
6. **The window lists sessions as work.** One row per session: the bound workflow version's steps (passed, current, to come), who
   it waits on, one sentence of state, its turns beneath. The person's rows are sessions waiting on the person (a look, a go-ahead,
   a rounds bound reached, a missing tree), never a check. An ask counts its quests by where they stand: landed, waiting on you, in
   checks or opinion, being worked, not started.
7. **A workflow says its purpose.** The workflow file gains a one-sentence `purpose`, each version a `why`, and new records name the
   place (repository, or room later). Where a place's sessions keep skipping a step or its settings split by kind of work, the
   window offers a new room or repository. Whether per-kind choices stop being written is the person's call.

## The migration, one way per job

1. Delete the per-session asks, withdrawing those open (CHECKHAND1).
2. `awaiting-step` in the ledger; `ended.awaits-check` goes.
3. Step words, the rounds bound and a `check` step in the development graphs.
4. The quest's Done at the graph's end; done at hand-back goes.
5. Turns replace `from` chains.
6. The window's rows and the ask's counts.

A running session crosses at its next done; a run not ended whose session ended done reopens as waiting. Today (2026-11-14) is
replayed as a fixture: zero hand-back asks, zero check rows to the person.

## Decisions for the person

| Choice | Recommended |
|---|---|
| Who writes the quest's Done (step 4) | **The runtime at the graph's end**, reversing driving rule 2's "the driver never writes quest state" |
| A red check's rounds before the person hears | **3** |
| Are a chain's later quests in one repository rounds of one session | **Yes**: one piece of work |
| Per-kind workflow choices | **Kept for now**; the split offer suggests a room instead |
