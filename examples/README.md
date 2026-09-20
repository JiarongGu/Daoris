# The example family — how projects run through Daoris

Two miniature projects — [`engine/`](engine/README.md) and [`game/`](game/README.md) — showing the
whole multi-project arrangement in the smallest form that is still real: each is a complete adopter,
and `npm run rehearse:family` drives everything below through the **real artefacts** (the packaged
CLI's own bin, the real HTTP host, the real `connect`). This is the setup story for the next real
family — a game and its subsystems with Daoris as their centralized router — written as working code
rather than as instructions.

## How a project joins

Five steps, executed by the project's own agent; the person says "join" at the start and reviews the
diff at the end (`autonomous-development`, D37):

1. **`daoris init`** — writes the manifest, names the available packs and everything the project
   already owns.
2. **Fill in `domain`** — one line of what the project *is*, the areas it **owns**, the kinds of quest
   it **accepts**. This is what makes it addressable: the registry answers "whose problem is this",
   which no search can (D34).
3. **`daoris sync`** — materializes the canon: always-loaded rules, on-demand knowledge, skills, the
   generated index, the lock. The project's own documents are untouched and listed `(local)`.
4. **`daoris check`** — the offline gate: drift, staleness, index freshness, the always-loaded budget.
   Wire it into the project's own verification.
5. **`daoris connect`** *(opt-in)* — pushes the declaration to a knowledge service, which is how a
   **remote** service learns the project exists at all. On one machine the service reads manifests off
   disk and this step only confirms the path works. The manifest's `remote` declaration travels with
   the registration (D47): `join` opts the project into a team deployment, `knowledge` — a second,
   separate declaration — feeds its indexed content too, and silence means local.

An agent session reaches the shared index and the quests by registering the MCP server in the
project's own `.mcp.json` — see `src/Daoris.Service/README.md` for the entry.

## How work routes

Projects here are never developed across (D32). When `game` needs something from `engine`, it
publishes a **quest** — what is needed and why, with the evidence, never the prescribed change — and
the engine's own agent pulls it, then answers: **take**, **done**, or **decline with a reason**. The
quest lives in the service's store; nothing is written into anyone's tree, and a repository that has
not adopted cannot be addressed, because nobody there could see the ask.

## How knowledge crosses

Each project's rules, knowledge, decisions and task outcomes are indexed per repository. What `game`
learned about world streaming is answerable from `engine` — or from the platform's Search view —
without opening `game` at all. Convergence detection then finds where two projects learned the same
lesson independently, which is how doctrine gets promoted rather than duplicated.

## How it is proven

`npm run rehearse:family` (`tools/family-rehearsal.mjs`) runs the whole story end to end and leaves a
transcript in `_fixtures/rehearsal-logs/`:

- both examples hold **current, clean doctrine** — a canon change that forgot to re-sync them fails
  here, which is the same discipline this repository applies to its own `.claude/`;
- the HTTP host comes up over a scratch store rooted at `examples/`;
- both projects register, through the real `daoris connect`;
- a quest goes `game → engine`; one to a stranger is **refused naming who is addressable**; declining
  without a reason is refused; the quest is taken and finished;
- `game`'s own knowledge answers a search made from outside it;
- a third project is **born mid-run** (D44) — init, declare, sync, check, connect — and answers its
  first quest on day one;
- the **driver drives** (D46): a quest becomes a stub session becomes a commit becomes done, a dirty
  tree holds the start, and the person's stop is honoured — no model in the gate;
- the **remote crosses** (D47): a shared host with minted keys, two simulated machines, a quest
  crossing them to done, a raced take standing down, and the remote's store scanned to hold nothing
  machine-local;
- the host is killed and restarted, and **nothing is lost**.

The examples are tracked in full — manifests, locks, synced doctrine — so they are readable as
examples, not only runnable as fixtures (D39). The cost is stated in the same decision: a canon change
syncs the examples in the same commit, and the rehearsal enforces it.
