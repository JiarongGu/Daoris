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
   it **accepts**. This is what an ask is matched against: the registry answers "whose problem is
   this", which no search can (D34).
3. **`daoris sync`** — materializes the canon: always-loaded rules, on-demand knowledge, skills, the
   generated index, the lock. The project's own documents are untouched, and its own knowledge and skills are listed `(local)`.
4. **`daoris check`** — the offline gate: drift, staleness, index freshness, the always-loaded budget.
   Wire it into the project's own verification.
5. **`daoris connect`** *(a management command, never run by a gate)* — registers the project with
   a knowledge service, carrying its declaration and, to a local one, its root. The registry is an
   explicit list (WSP2): a project nobody connected or imported is not a member, on one machine or
   many, and a registered one is addressable (D70). The manifest's `remote` declaration travels with
   the registration (D47): `join` opts the project into a team deployment, `knowledge` — a second,
   separate declaration — feeds its indexed content too, and silence means local.

An agent session reaches the shared index and the quests by registering the MCP server in the
project's own `.mcp.json` — see `src/Daoris.Service/README.md` for the entry.

## How work routes

Projects here are never developed across (D32). When `game` needs something from `engine`, it
publishes a **quest** — what is needed and why, with the evidence, never the prescribed change — and
the engine's own agent pulls it, then answers: **take**, **done**, or **decline with a reason**. The
quest lives in the service's store; nothing is written into anyone's tree. A repository nobody registered
cannot be addressed; one registered and not adopted can, and only a protocol-door session answers
it, because that door hands the session its connector (D70).

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
- a quest **addresses a lane**: `engine` declares two in its `daoris.lanes.json`, which `connect` sends
  as words; `engine:core` publishes, `engine:nope` is refused naming both, and `engine` may ask its own
  lane but never itself whole (D115);
- `game`'s own knowledge answers a search made from outside it;
- a third project is **born mid-run** (D44) — init, declare, sync, check, connect — and answers its
  first quest on day one;
- the **driver drives** (D46): a quest becomes a stub session becomes a commit becomes done, a dirty
  tree holds the start, and the person's stop is honoured — no model in the gate;
- the **remote crosses** (D47): a shared host with minted keys, two simulated machines, a quest
  crossing them to done, a raced take standing down, and the remote's store scanned to hold nothing
  machine-local;
- a **plugin speaks** (D64): [`plugins/hold-by-title`](plugins/hold-by-title/README.md) is installed
  with the real `daoris plugin add`, holds one quest with its own sentence, is told of an ending it
  keeps in its data folder, and is switched off from a terminal — while a second plugin declares the
  harness the sessions ran on;
- the host is killed and restarted, and **nothing is lost**.

## How it is extended

`plugins/` is not a project in the family — it holds the **example plugin**, a folder with a
`plugin.json` that the driver reads from the home's `plugins/` once it is added
(`docs/2026-09-23-plugin-design.md`). A plugin declares harnesses on the ACP door and may speak from
a process of its own; no code from one ever loads into Daoris. Two hand sessions a browser:
[`plugins/browser`](plugins/browser/README.md) launches one of its own, and
[`plugins/in-app-browser`](plugins/in-app-browser/README.md) attaches to Daoris's own window (D78),
where the person signs in once. Two land work (D100): once accepting a session has put its work on a
branch, [`plugins/github-pull-request`](plugins/github-pull-request/README.md) pushes it and opens a
pull request with `gh`, and [`plugins/azure-devops-pull-request`](plugins/azure-devops-pull-request/README.md)
with `az repos`. Neither runs until it is installed and a workspace's landing rule names it. The
published desktop carries those two and `in-app-browser` as **Daoris's own plugins**, offered in
Settings → Plugins and by `daoris plugin list` and never installed until a press (D103);
`hold-by-title` is the rehearsals' fixture, and `browser` is for a machine without the shell.

The examples are tracked in full — manifests, locks, synced doctrine — so they are readable as
examples, not only runnable as fixtures (D39). The cost is stated in the same decision: a canon change
syncs the examples in the same commit, and the rehearsal enforces it.
