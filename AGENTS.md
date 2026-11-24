# Daoris (道衍): the brief

Daoris is a desktop application that runs a software company of AI coding agents for one owner. This repository is the
product, developed by agents, the product's own sessions among them. Its parts: `src/Daoris.Desktop/` (host, driver,
launcher), `src/Daoris.Service/` (knowledge, quests, sessions), `src/Daoris.Web/` (the window's pages),
`src/Daoris.Cli/` (the CLI that distributes the doctrine in `canon/`) and `src/Daoris.Devkit/` (the gates).

Every change keeps these:

- Local operation needs no remote; the doctrine commands need no service and no runtime dependency.
- Machine-local state (streams, accounts, browser state, filesystem actions) stays on the desktop. A shared host
  exchanges permitted records, serves no UI and answers no machine path.
- Daoris supplies frameworks a workspace configures, never one way for every repository (D349).
- Replacing one logic changes its module and its wiring alone.
- Window chrome translates through the English and Chinese catalogues; authored content, configuration included, stays
  as written.
- A feature that uses a model says which tier actually ran.

The doctrine below is what this product gives every repository it serves, tried here first. Where the product makes
its own agents act against it, file that as a product defect.

<!-- daoris:rules — generated; edit the canon, not this -->
# Doctrine

Generated from the shared canon; edit the canon, not this. `daoris reconcile` says how to keep a local edit.

## Where things are

Declared in `daoris.json`; look here before searching.

- router `docs/README.md`
- index `docs/index/README.md`
- areas `docs/areas`
- backlog `TASKS.md`
- decisions `docs/decisions`, archive `docs/task-archive.md`, fixes `docs/FIX-LOG.md`: the library
- changelog `CHANGELOG.md`
- glossary `src/Daoris.Web/src/locales/glossary.json`
- gates `daoris.gates.json`: the checks, and what may run unasked
- knowledge and skills, each with when it applies: `.claude/INDEX.md`, searched for your kind of change

<!-- daoris: core/core/rules/every-session.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

## Rules for every session

1. **Ask the owner only what the risk policy puts to them.** Whether an act needs a yes, and whose, is one declared
   policy: where it declares none nothing waits, and only acts it classes theirs, and changes to the policy itself, go
   to the owner. With none: direction and targets, money and budgets, sign-ins and credentials, marked releases,
   history rewrites, deleting their data, publishing outside. The owner owns the outcome, not the steps: decide the rest, alone or with another agent; record
   why and go on. A question to the owner says what it is, what yes does, what no does
   and where to look. Work you cannot finish is reported as "gave up" with what you tried, never asked about.
2. **Ownership is local.** Change only this repository. What another needs is a request to whoever works there, with
   the evidence: a quest where a request system exists, a message to its owner where none does. Already touched one?
   Stop and say so (`reaching-in`).
3. **Delivery follows the risk policy:** what may run, land and publish unasked is declared (a policy,
   else a declaration beside the gates) and its brief names where; add no human step it does not name. Before landing come two agent reviews, wide (design, effects
   beyond the change) and close (details, edge cases, tests), from different agent families where two are ready.
4. **One way per job.** A better way deletes the old one in the same change: code, commands, options, words, defaults.
   An older reader kept for records already written names the condition that ends it.
5. **No private names** in tracked files or commit messages: machine paths, private repository, employer or customer
   names, credentials. Write a relative path or a neutral stand-in; keep private context untracked. The scan is a net,
   not a licence. A committed leak needs a history rewrite, the owner's call: report it at once (`leak-repair`).
6. **Act on what you read now.** A brief, note or memory is an earlier reading: read the tree, branch and check again
   before acting on them, and stamp a fact you pass on with its commit. Report each check as passed, failed or not run,
   with its command; a stand-in or a story proves only itself.
7. **Your done ends your turn, not the work:** a failed check or review comes back to you; carry it to landing.
8. **Stop what you start, and only that.** Every wait and background run has a deadline; never stop a process or a
   session you did not start.
9. **Use the file tools** to read, search and edit, never a script or a console redirection (`file-tools`). Scratch
   goes in the repository's ignored folder, never OS temp. Never route a command around an approval; pause before a
   destructive one.
10. **Project facts live in the repository,** never in an assistant's global memory.

<!-- daoris: core/core/rules/how-a-change-goes.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

## How a change goes

1. Start from your brief or row, then the area document of the part you change. Find names through the index where one
   is generated, and read the ranges it gives, not whole files. Look the library (decisions, archive, fixes) up for a
   reason, never to orient; before reversing a rule an area states, look up why it stands.
2. Mirror one exemplar; new behaviour goes in a unit of its own.
3. Write the test first and see it fail, then the code.
4. In the same commit, rewrite each area sentence the change made false, in the present tense. Write a decision only
   for a choice that rejected an alternative; a finished row leaves the backlog for one archive line.
5. Run the checks your diff can reach (the brief names them); commit when green as `type(scope): what changed`, with
   your maker's `Co-Authored-By` line. A regression fix names its root cause and the commit that introduced it.
6. Land only the way the brief names. A red main line comes first.
<!-- /daoris:rules -->

## This repository

- Node 24 and the .NET 10 SDK. Install with `npm ci` and
  `npm --prefix src/Daoris.Web ci`; a worktree starts with `node tools/worktree-setup.mjs`.
- While working: the filtered tests your area names, and `npm run quick`. Before done:
  `node tools/reached-checks.mjs --base main --tier fast` and `npm run verify:checks`.
- Land with `node tools/merge-branch.mjs <branch>`, never a plain `git merge`. What agents may do unasked is the
  risk policy: `daoris settings risk`.
- `npm run verify` is the baseline; the full set (`node tools/merge-branch.mjs --full`) belongs at an integration
  boundary. Real-process suites and rehearsals run one at a time and may open windows. `docs/development.md` says how.
- Areas: one document per part of the code, in `docs/areas/`.
- Written by tools, never by hand: the doctrine above and `.claude/` (edit `canon/`, then run
  `node src/Daoris.Cli/bin/daoris.mjs sync` here, in `examples/engine` and in `examples/game`); `docs/code-map.json`
 ; `docs/index/` (`node tools/orient-index.mjs`, never committed); versions and release headings
  (the release workflow).
- `local/` (ignored) holds private context and session scratch; `_fixtures/` is the tests' and tools' scratch.

## Read when

| When you | Read first |
|---|---|
| change `canon/` | `.claude/knowledge/canon-authoring.md` |
| change sync, the lock or drift | `docs/areas/doctrine-and-cli.md`, its sync rules |
| change a screen or text a person reads | `.claude/knowledge/translation-parity.md`, `src/Daoris.Web/README.md` |
| change what a store keeps, or are tempted to keep a reader of an older shape | `.claude/knowledge/compatible-builds.md` |
| build a feature that uses a model | `.claude/knowledge/model-decoupling.md` |
| add or change a check, or one you did not touch fails | `docs/development.md`, *Choose the checks* |
| brief or dispatch another agent | `.claude/skills/dispatch-subagent/SKILL.md` |
| were given a channel, a helper or a quest tool | `.claude/skills/co-working/SKILL.md` |
| configure Codex, or its review fails | `.codex/config.toml` |
| find a leaked name or credential | `.claude/knowledge/leak-repair.md` |
| plan a foundational redesign or a whole-product review | `.claude/knowledge/fresh-design-pass.md` |
