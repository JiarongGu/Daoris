# Consolidation: the current truth, and one lean way to make a change (CONSOLIDATE1, 2026-11-04)

**Standing: the owner's direction and yes of 2026-11-04.** No feature work starts until this is done. The five builders running finish
their current row and stop.

## Why

A small change has been costing hours. The records it must read hold 230 decisions in 5.9 MB, with 1,315 dated amendments, plus 283
design documents, a 2.8 MB archive and a 1.3 MB fix log. A merge since 2026-11-03 touches 130 files on average: registers, census
fixtures, a note per row, catalogue rows and goldens. The cost is the reading and the bookkeeping, not the code, and amendments that
contradict each other steer builders by whichever one they read. Daoris hands the same machinery (the canon's rules and skills, the
standing block) to every session it runs, so the fix belongs in the canon as well as here.

## What it produces

1. **The current truth, per area.** One document per area under `docs/areas/`, written from the code as it stands: what the area does,
   its units and where they live, its live rules (each naming the decisions it replaces), and its open questions. A few pages each.
   The areas:
   - doctrine and the CLI;
   - knowledge, memory and records;
   - install, home and update;
   - driving (quests, asks, sessions and trees);
   - conversations and context;
   - agents and harnesses;
   - roles and supervision;
   - workflows, review and landing;
   - co-working and messages;
   - git and remotes;
   - the desktop app and its window;
   - plugins;
   - code structure and the development process.
2. **The contradictions,** listed in one place for the owner to settle in one pass.
3. **One lean way to make a change,** in the canon, so every repository and every session Daoris runs gets it:
   - read the area document;
   - change the code in a unit of its own (D231);
   - write and run its tests;
   - update the area document where behaviour changed;
   - nothing else unless a gate asks.

   The repository's gates carry what builders now carry by hand: census rows, registers and twins. The skills, the rules, the
   branch-worker definition and the dispatch skill are cut to it. The canon's version is what Daoris's sessions are handed.
4. **The record rules rewritten:**
   - `docs/decisions/` becomes history, kept in git and not read by default;
   - a status table maps each decision to its area rule, or marks it superseded;
   - `AGENTS.md`, the router and `daoris.json`'s records point at the areas;
   - the backlog's rows are a line or two.

## Order

1. The area readers run, five at a time, each writing its document and its contradictions. They read code and decisions, and change
   no code.
2. The process reader inventories every instruction a session gets here and in the canon, and drafts the lean way.
3. The steward merges the results: the contradictions to the owner, the canon edits synced, and the record rules rewritten.
4. Then CODESHAPE1 (D231), SCOPEVERIFY1, and features again, timing a few rows against today's hours.
