# COMPANYVIEW1: the company's structure configured and seen, as a list and as a graph — the brief

**Standing:** the brief of a D191 round, 2026-10-31. Two makers write sealed proposals from it alone; the steward synthesizes;
both makers read the synthesis.

## Why now

The owner's direction is a development company run by agents with minimal owner input (D193; `docs/2026-10-21-company-concept.md`).
Its structure is one of Daoris's main functions, yet today it is set in pieces: the roles and their staffing (D205's ordered
choices of agent, model and effort; `docs/model-roles.md`), the appointed roles (the supervisor D208, the steward D209), the work a
session does and its policy (FOUNDATION1's participants and `WorkPolicies`, the call table), the accounts each agent may run on, the
repositories of a workspace and their lines, rules and landings, the workflows (D190, D215's graph presets), the integration
declarations, the plugins. The owner asks for one place to configure and set this up, seen both as a **list** and as a **graph**,
with a UI and UX good enough for the function it is.

## What binds every proposal

- The UI and translation contracts the router names (`docs/README.md`); en and zh; the widths and themes the UI contract gives;
  D150's places; one press in one place; a presentational component imports nothing of the bridge.
- The glossary's words (`src/Daoris.Web/src/locales/glossary.json`): *role* (角色) is the configured position, *actor* is never a
  word on the window, *supervisor* and *steward* are appointed roles; read the glossary's rows before naming anything.
- What exists and its decisions: D205 (actors, MODELROLE1), D208 and D209 (appointments; `StewardOffered` dark until
  STEWARDROLE1d), FOUNDATION1 (D212: one participant model, works, the call table), D184 (permissions), D125/D206 (accounts and
  limits), D190/D215 (workflows and integration), the map design (`docs/2026-09-23-map-design.md`, the workspace's wiring graph),
  the Agents page, the Settings domains, the repository page's tabs (Workflow, Integration).
- Machine-local things stay on the desktop (accounts, sign-ins); a setting's change goes through its existing door and its
  confirmation; the steward may change only what D209 allows.
- D137: a view says nothing it cannot read; a graph never draws an edge it cannot prove.

## The questions

1. **The model.** What are the company's structural things and their relations (roles, their staffing, appointments, works and
   their policies, accounts, repositories and their lines, workflows, plugins), and which of them a person configures here versus
   reads? Name each one's existing home and door.
2. **The list.** How the structure reads as a list: its sections, its rows, what a row says, its edit in place or the door it
   opens, its filters, and what it says of a thing unset or invalid.
3. **The graph.** What the graph shows (nodes, edges, grouping), what it never draws, how it stays legible at a workspace of many
   repositories and roles, how a person edits from it (or does not), and how it relates to the existing map and workflow graphs.
4. **One model, two views.** How the list and the graph stay one truth: the same reads, the same selection, the same edits; where
   each view is better; how a person moves between them.
5. **Setup.** How a new workspace or repository is set up into the structure (the first roles, staffing, appointments, rules),
   with the fewest presses, and what the steward can set up for the person once appointed.
6. **The rows,** smallest first, each with its proof (stories at every width in en and zh and both themes, page tests, the
   installed window), and what waits on what (STEWARDROLE1d's activation, the routes each needs).

## The form

At most 1,800 words. Answer each question with a choice, its reason, and what it rejects. Name the code and records you read, with
paths. End with the rows in order. Write without reading the other maker's proposal.
