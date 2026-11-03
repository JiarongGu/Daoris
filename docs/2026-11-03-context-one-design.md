# A session's standing context, one way: the repository owns it, Daoris manages its own sessions' (CONTEXTONE1, 2026-11-03)

**Standing: proposed by the steward 2026-11-03, under D191, on the owner's direction and three answers of the same day; D230.**
An Opus read is owed beside the first rows; Codex's when its spend cap allows. Written from main at `c255c544a`.

## 1. What is true today

A census of the owner's install read 882 session records, their events, the knowledge and memory stores, eight adopted
repositories' locks and instruction files, and the install's own `daoris status`. The evidence, with private names, is kept
untracked (`local/scratch/dogfood/2026-11-03-context-paths.md`). Three defects it confirmed are already fixed: the routing line
(ROUTEQUEST1), the brief's did line (BRIEFREADS1) and the tidy against a go-on (TREEGONE2).

What a session knows comes from many paths, and those paths drift apart:

- **Rules come from five places:**
  - the repository's synced region;
  - the knowledge DB's copy of that region;
  - the install's canon, which no session reads;
  - a machine's standing answer in `driver.json`;
  - Ask Daoris's room.

  On the install, every adopted repository is behind the canon. Seven lack `current-knowledge` and three lack `co-working`. Nothing
  reports it, because the canon's stamp has not moved since August and `daoris status` compares stamps only
  (`src/Daoris.Cli/src/doctrine/commands.ts:199-203`). The standing answer contradicts the repository's own rules, and it was handed
  to 448 first prompts.
- **Skills come from five places:**
  - `.claude/skills`;
  - `.agents/skills`;
  - the DB's skill rows;
  - each account's own synced skills (9 on two accounts, 13 on the other two, so a line that moves account changes its skills);
  - plugin skills.
- **Routing comes from three places:** the prompt's line, the co-working skill and `registry`.
- **Where things are, and what the line knows, come from five places each.** Each kind of session gets a different subset of them:
  - a driven start gets a 9 to 18 KB instruction;
  - a chat gets the person's words and a routing line;
  - Ask Daoris gets a room;
  - a move gets the brief.

  Memory holds no entries, and its tools were called once.
- **The harnesses differ:**
  - Claude Code sessions hold the knowledge, memory and quest tools as deferred names. 48 of 675 Claude builds searched the
    knowledge DB, against 7 of 10 Codex builds.
  - Codex reads at most 32 KiB of `AGENTS.md`. One work repository's rules begin past byte 44,000, so Codex never sees them.
- **Older records and keys are still read:**
  - 828 records carry no `from`, and their lines cannot be walked;
  - the old `adapter`, `intakeAdapter` and `helperAdapter` keys still choose the agent for every role but Build;
  - account homes are kept in two layouts.

## 2. The principle (the owner's answers)

1. **The repository owns its context.** Its rules, skills, knowledge and setup are files in the repository, versioned and reviewed.
   They work for any developer and any agent without Daoris. The brief's *doctrine works without a service* stands.
2. **The canon is something a repository reconciles with, not a copy imposed on it.** Not every repository develops as the canon
   says. So a **reconcile**, run by a model in the repository's own session, compares its doctrine with the canon. It finds:
   - missing rules and skills;
   - conflicts;
   - setup gaps.

   It then proposes changes, and the repository's session decides and commits. A difference the repository keeps is recorded as
   its own, with its reason, so it is not reported again. Daoris never writes into the repository from outside: the reconcile
   travels as a quest to that repository.
3. **Daoris manages the context of the sessions it runs.** A session Daoris starts gets:
   - one standing block, composed from the repository's records and Daoris's knowledge and memory, the same on every harness and
     every kind of session;
   - its knowledge, memory and quest tools loaded at the start;
   - the same skills on whichever account it runs, with the account's own memory off.

   A session Daoris does not run is left alone: nothing in a person's own tool home is changed or cleared.
4. **Machine configuration holds no repository rule.** What a machine key says about a repository belongs in that repository. It
   reaches the repository as part of the reconcile's proposal, and the key goes (D228).
5. **Older records and keys are read once and converted (D228).** Each conversion has the condition that ends it and the row that
   deletes the old reading.

## 3. The parts

**A. Drift that can be seen.**
- The lock judges each owned file by its content against the canon's own file, not by the stamp. `daoris status` says, per file:
  - behind;
  - missing;
  - edited here;
  - kept here.
- *Kept here* is a difference the repository recorded with a reason (part B).
- This stays offline and has no runtime dependency: the CLI carries the canon.
- The install's registry view shows the same per repository.

**B. The reconcile.**
- `daoris doctrine reconcile` composes the reconcile's brief for one repository:
  - the canon's rules and skills, beside the repository's;
  - the files part A finds behind or missing;
  - the machine keys that speak about the repository (point 4).
- A session in that repository works the brief. On the install that is a quest Daoris publishes to the repository when part A
  sees drift; elsewhere it is a person running the verb with their own agent.
- It decides each item:
  - **adopt**: sync the file;
  - **merge**: edit the local file toward the canon;
  - **keep**: record the difference and its reason in the lock's `kept` table;
  - **upstream**: propose the local wording to the canon, as a quest to this repository.
- It commits in the repository's own line, under its own workflow.
- No model runs in the CLI. The verb composes the brief, and the judging is the session's.

**C. The standing block.**
- The service composes one block per session at its start, at every go-on and at every move. It is bounded at 12 KiB, well
  under Codex's 32 KiB. It carries:
  - the repository's rule index, by name, with a get id per rule;
  - the routing;
  - where things are (the router's and the index's entries);
  - the repository's and workspace's facts;
  - the line's memory, and a pointer to its brief.
- It replaces the per-kind prefaces as one way (D228): the routing line, the process-only line, the standing answer and the parts
  of Ask Daoris's room that say the same.
- The repository's own instruction files remain what each harness reads natively. The block points to them and adds only what
  Daoris knows.
- Where no service runs, there is no block and the files serve alone.

**D. Tools at the start.**
- On every Claude door, the knowledge, memory, quest and line tools load at the start, not as deferred names. This extends
  HELP5's switch from Ask Daoris to every kind of session.
- Codex already holds them.

**E. The accounts Daoris runs.**
- Each account home a Daoris session runs in gets the same skill set: the repository's skills and Daoris's own, from one store.
- Account-level auto-memory is off for Daoris's sessions only. Memory lives in Daoris's memory store (D220).
- Daoris never touches the person's own tool home, and never touches an account's content outside its sessions.

**F. Codex's limit.**
- The reconcile reports an `AGENTS.md` over 32 KiB as a setup gap: rules past the limit are rules Codex never reads.
- The block carries the rule index either way.
- Handing the person's denies to Codex stays with its harness row (D117).

**G. The conversions, once each (D228):**
- the old adapter keys become actors;
- the standing answer becomes an item in the Daoris repository's reconcile brief, and its key is removed when that reconcile
  closes;
- the two account layouts are folded into one;
- a record before `from` is linked where its line is provable from its quest and tree, and is otherwise said as a line of its
  own.

## 4. Rows

Contract and proof for each row:

- **CONTEXTONE1a, drift by content** (cli; now). Proof: a fixture adopter with one file behind, one missing, one edited and one
  kept, each said by `daoris status`. A stamp that does not move no longer hides drift.
- **CONTEXTONE1b, the reconcile** (cli, driver; after a). Proof:
  - the verb's brief over a fixture adopter;
  - a `kept` entry no longer reported;
  - on the install, one adopter reconciled through its quest, on the owner's yes.
- **CONTEXTONE1c, the standing block** (service, driver; after the COMMS1 and workflow waves reach main, since it replaces lines
  they touch). Proof:
  - goldens of the block for each kind of session and each harness, every one at most 12 KiB;
  - the removed prefaces censused gone.
- **CONTEXTONE1d, tools at the start** (driver; now). Proof: a Claude driven start, chat and go-on, each listing the connector's
  tools, not deferred names. Measured: knowledge searches per session on the install, before and after.
- **CONTEXTONE1e, the accounts Daoris runs** (driver; after d). Proof:
  - two accounts handed the same skills;
  - auto-memory off in Daoris's sessions;
  - the person's own home untouched.
- **CONTEXTONE1f, the conversions** (driver; after c). Proof: a home with each older shape converted once, with the
  `Register.cs` entries ending them.
- **CONTEXTONE1g, Codex's limit** (cli; with b). Proof: an `AGENTS.md` over 32 KiB reported by the reconcile's brief.

## 5. What it rejects

- **Doctrine served only by Daoris.** A repository must work for a developer without Daoris.
- **A mechanical sync that overwrites a repository's differences.** Not every repository develops as the canon says, so a model
  judges each conflict in the repository's own session.
- **Clearing account folders or a person's tool home outside Daoris's sessions.**
- **A second preface beside the block.** Each line the block replaces is removed in the same chunk.
