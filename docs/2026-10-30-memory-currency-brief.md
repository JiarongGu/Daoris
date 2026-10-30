# KNOWCENTRAL1: memory a centralized, model-driven knowledge system can keep current — the brief

One brief for two sealed proposals under D191, one from Codex and one from Lyntai's own session, each written from this brief alone
and before its author reads the other. Lyntai's session then writes the synthesis and decides in its own repository; Codex and
Daoris's steward read it. The library is Lyntai's, so the decision is Lyntai's; Daoris states what it needs and why.

## Why now

Daoris is heading for a centralized knowledge system across the repositories it drives, whose upkeep models carry: an agent proposes
what a new fact replaces, and the store keeps what it holds current (D220, `docs/decisions/D220.md`). Lyntai's memory is the engine
that system would rest on. Lyntai answered Daoris's LQ16 to LQ20 on 2026-10-30 with a study (`docs/memory-currency-study.md`, commit
`fa30715f` in Lyntai, not yet landed), two decisions and rows MC1 to MC5: an entry ended by reference (MC1), write tools for agents
(MC2), a candidate lookup at write time (MC3), writer-made `Disputes` and `Amends` marks (MC4), and an opaque `Recheck` whose failure
ends the entry (MC5). This round asks what those rows leave open for a system models keep current at scale.

## What binds every proposal

- Lyntai's generic-library test: no host's names or policies in the library.
- Lyntai's evidence on a model's judgement: D90 (no "newest wins"), D109 (a relevance judge endorsed the superseded fact 3.4 times as
  often as the current one), D215 (similarity never establishes contradiction), D217 (a model-chosen key matched its predecessor a
  quarter of the time), and the study's measurement that ending whole entries cut an amendment's own recall from 66% to 14%. A
  proposal that has a model decide alone must say why this evidence does not apply to that step.
- Lyntai's decay: D40 (an entry fades by how far the engine has moved since it was last used, not by time) and D41 (a faded entry is
  buried by a relative floor, never deleted; authoritative material exempt).

## The questions

1. **The retirement channel.** Daoris's owner proposes that an entry a writer supersedes, or whose re-check fails, is retired through
   the decay seam (buried, recoverable, outranked) rather than ended, a passing re-check counting as a use, and decay alone forgetting
   what nobody uses. Weigh it against MC1's end and MC5's failed check, given that use refreshes a stale fact that is still asked about
   and that a superseded and a current fact rank alike for one question. Say what recall returns, and what history keeps, under each.
2. **What a model does alone.** For each step of upkeep (finding candidates, proposing `Supersedes`, `Amends` or `Disputes`, running a
   `Recheck`, ending, burying), say whether a model may take it alone, needs a second model's or a check's confirmation, or needs a
   person, and what would be measured before a step moves from one class to another.
3. **Memory shared across repositories.** A fact one repository relies on and another owns (Lyntai's roadmap Wave 4): scopes, who may
   write and end, how an owner's change reaches the readers, and which half is the library's and which the coordinator's.
4. **Records as memory.** Daoris indexes each repository's tracked records (decisions, knowledge documents, the backlog), and will mark
   which record supersedes which (KNOWFRESH1a, `docs/2026-10-30-knowledge-freshness-design.md`). Should those enter the memory as
   authoritative entries, exempt from burial, with supersession derived from the records' marks, or stay a separate index the memory
   only cites?
5. **The order.** What builds first among MC1 to MC5 and anything this round adds, and what each step measures.

## The form

At most 1,500 words. Each answer states a choice, its reason and what it rejects; evidence by file and line; numbers only where a
source gives them. Write nothing in another repository: Codex reads read-only; Lyntai's session writes its proposal in its own
repository.
