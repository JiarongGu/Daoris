# A generic foundation for flow, decision and action (FOUNDATION1)

Status: proposed, 2026-11-14. Source: brainstorm `4019862c` on the installed Daoris, both makers answering (Anthropic `g1`,
OpenAI `g2`, synthesis `s1`), the first round having had one maker. Its limit: every source reached the passes cut at 12,000
characters and no code was in the packet (BRAINPACKET1), so what is declared against what is coded was read from documents only.
The step before building anything here is to check each claim marked *(to check)* against the code.

## The question

Daoris is becoming a platform where agents work together on long-running work, not only a development tool. Is its workflow,
actor and role system a generic foundation, such that long-running deliveries across repositories, recurring operations,
decisions by agents with the person's overrule, reactions to events, schedules and budgets are configuration rather than code?

## The answer: partly generic. Keep the core, open the closed lists

Both makers agree: a reusable foundation, not yet a proven generic one. **Keep**, and build no second engine for delivery or
operations:

- the one node runtime and whole, versioned graphs with runs pinned to a version;
- each kind in its own folder with its declaration, and plugin steps;
- `RoleDoor`'s judged writes compared by revision, scope resolution, the staffing walk and `StaffJudge`;
- the pure planner.

**What blocks flexibility:** the closed vocabularies and the single run subject.

- Decision kinds: six supervisor kinds, each a code row; seven fixed counts and actions; a `team` kind added for one feature.
- Works: twelve fixed works; `RoleCatalog.Fallback` coded; only two works may be off; two role shapes with `mixed-shape` refused.
- Graph rules: a two-value pipeline mark with nine barred kinds, named git kinds, one `land`, and `ProtectedOrder`, all in the
  save judge.
- The run subject: a run is a chain's work in one repository keyed by its first quest (the ask's delivery run the one exception);
  parallel parts cannot join; first-quest branch names once rewound a finished run.
- Time: everything waits on polling looks of 3 to 7 minutes; a budget acts only at a start.

## The foundation

1. **One Decision contract.** A decision kind is registered as data. A decision has a subject, the facts it read, options that
   are acts, holders in order (rules, then an agent, then the person), a grant and a window. The person's later word supersedes
   it on record, and voids acts not yet sent.
2. **One Act contract.** An act is registered with a handler. It is judged against the decision that authorises it, journaled
   under an idempotency key before it is sent, and closed by an observed receipt. Landings, releases, steward acts and presses
   are its instances. An act already sent is never unsent: an overrule compensates.
3. **A run's subject is a typed unit.** A unit is a chain, a part, an ask, a deployable or an occurrence of a schedule, with
   declared milestones and its state derived from its records. A run is (unit, graph version) with its own id and parent/child
   bindings. One evidenced Edge, waited on by `wait.unit`, is the only way one unit waits on another. Team and delivery plans
   compile onto units and edges; an ask is complete when its receipts satisfy it.
4. **One role shape.** Works and decision kinds are catalogue data (a selecting mark, a fallback, whether it may be off). A role
   is what it takes, its scope, staffing, policy, grants and holder; lead, scout, supervisor and steward are shipped assignments.
   Authority is always an explicit grant, never inferred.
5. **Triggers and meters.** A trigger is an event filter or a calendar, with a deduplication key, catch-up and a concurrency rule.
   A recurring trigger starts a new pinned run, never a cycle in a graph. Events wake the look, which stays the reconciling read.
   A meter is {measure, scope, window}; counts, caps, strikes and budgets are meters whose reach raises a decision.
6. **Graph rules as kind declarations.** Each kind declares what it writes, the place kinds it runs in and whether it is the
   person's part. A pipeline profile states invariants over those properties, so a new pipeline is a file. One rule stays in
   code: lowering the person's part needs the person's press.
7. **One owner per piece of state.** The service holds what resumes elsewhere: definitions, pinned bindings, node attempts,
   decisions and portable receipts. The desktop holds trees, accounts, processes, logs and act journals. One fenced lease owns a
   run's effects.

## The migration, one way per job

Each step removes one duplicate way, and a census fails while the old way is still read:

1. Unit and Edge (removes the first-quest run key and the ask's special delivery run).
2. The act journal (removes the separate landing, steward, press and deployment memories).
3. The decision registry (removes the coded supervisor kinds and count/action rows).
4. Kind-declared graph rules and profiles (removes the save judge's named lists and the two-value pipeline mark).
5. One role shape (removes the two shapes, `mixed-shape` and the coded fallback).
6. Triggers and meters (removes the fixed counts and the start-only budget).

TEAMWORK1 and DELIVERY1 are the contracts' first users. Proof: four scenarios (a delivery across repositories, a recurring
operation, an agent's decision the person overrules, a budget reached) run as configuration only, with no new code. A new
connector or effect still needs code, and that is the honest bound.

## Risks the makers flagged

- Rerouting the person's Accept, the supervisor's grants and every sent effect through new paths could lower the person's part
  or send an effect twice; the act journal and the census come before any cutover.
- Re-keying runs decides which branch a landing binds: a wrong key once rewound a finished run.
- A duplicate or missed event starts a run twice or not at all; budget admission must be atomic with starts and acts.
- After each step's conversion of stored records runs, a revert needs those records converted back.

## Decisions for the person

| Choice | Recommended |
|---|---|
| Build on the present core, or a new engine | **On the core**: both makers, with evidence |
| Order | **Unit and Edge first**, then the act journal; TEAMWORK1 and DELIVERY1 as first users |
| Before step 1 | **Check the claims against the code** (*to check*): which catalogues are already open, whether `wait.unit` is general, what of lead/scout and deployables is built |
| The four proof scenarios | **The person names** the recurring operation, schedule and budget meant beyond development |
