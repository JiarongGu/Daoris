# A session's standing context, one way: the repository owns it, Daoris manages its own sessions' (CONTEXTONE1, 2026-11-03)

**Standing: proposed, round 2, by the steward 2026-11-03, under D191, on the owner's direction and three answers of the same day;
D230.** Round 1 was read by the independent Opus reader against main at `2a1a85460`. That read found 13 blocking and 14 non-blocking
findings and asked for another round. Round 2 takes each one (§6). Codex's read is owed when its spend cap allows. Rows a and d
build now, as the read allows (§4).

## 1. What is true today

A census of the owner's install read 882 session records, their events, the knowledge and memory stores, eight adopted
repositories' locks and instruction files, and the install's own `daoris status`. The evidence, with private names, is kept
untracked (`local/scratch/dogfood/2026-11-03-context-paths.md`). Three defects it confirmed are already fixed: the routing line
(ROUTEQUEST1), the brief's did line (BRIEFREADS1) and the tidy against a go-on (TREEGONE2).

What a session knows comes from many paths, and those paths drift apart:

- **Rules come from five places:**
  - the repository's synced region;
  - the knowledge DB's copy of that region (the canonical rows hidden by default);
  - the install's canon, which no session reads;
  - a machine's standing answer in `driver.json`;
  - Ask Daoris's room.

  On the install, every adopted repository is behind the canon. Seven lack `current-knowledge` and three lack `co-working`. Nothing
  says so: the canon's stamp is `0.0.1` through every 0.0.x build, and `daoris status` looks for an update only when the versions
  differ (`src/Daoris.Cli/src/doctrine/commands.ts:199-203`). The standing answer contradicts the repository's own rules, and it was
  handed to 448 first prompts.
- **Skills come from five places:**
  - `.claude/skills`;
  - `.agents/skills`;
  - the DB's skill rows;
  - the vendor's account-synced skills (9 on two accounts, 13 on the other two);
  - Daoris's per-session plugin copy (`Agents/SpawnSkills.cs`).
- **Routing comes from three places:** the prompt's line, the co-working skill and `registry`.
- **Where things are, and what the line knows, come from five places each.** Each kind of session gets a different subset of them:
  - a driven start gets a 9 to 18 KB instruction;
  - a chat gets the person's words and a routing line;
  - Ask Daoris gets a room;
  - a move gets the brief.

  Memory holds no entries.
- **The harnesses differ:**
  - Claude Code sessions hold the knowledge, memory and quest tools as deferred names. 48 of 675 Claude builds searched the
    knowledge DB, against 7 of 10 Codex builds.
  - One work repository's `AGENTS.md` is 61 KB. Codex reads 32 KiB of it, so it never reaches the rules.

## 2. The principle (the owner's answers)

1. **The repository owns its context.** Its rules, skills, knowledge and setup are versioned files that work for any developer and
   any agent without Daoris. *Doctrine works without a service* stands, and so do D13 and D19: drift is the disk against the lock.
2. **The canon is something a repository reconciles with, not a copy imposed on it.** Not every repository develops as the canon
   says. Daoris shows a repository where the canon has moved. A model in the repository's own session then decides, item by item, to
   adopt, keep with a reason, or propose upstream, and commits in its own line. Nothing is written into a repository from outside it.
3. **Daoris manages the context of the sessions it runs, and only for those sessions.** Every change is scoped to the session:
   - the instruction it is handed;
   - the per-session plugin copy;
   - the tree;
   - the session's own settings and variables.

   An account's home, the vendor's synced set, and the person's own tool home are never written.
4. **A repository rule does not live in a machine's standing answer.** The standing answer stays the person's machine choices (D135
   §3: which environment to test in, which writes are allowed). A line in it that states a rule about the repository's own development
   (how to verify, how long a session is) is moved, through that repository's reconcile, into its records. Commit rules, landings and
   heavy gates stay machine and workspace settings, as D135 and the workflow decisions place them.
5. **Older shapes are ONEWAY1's.** The adapter keys, the account layouts and the older records are converted by ONEWAY1's rows
   (S1 to S3, w), and finished history is cleared by D228's note. This design adds no conversion of its own.

## 3. The parts

**A. An update that can be seen** (cli).
- `daoris status` runs the sync's own plan (`planChanges`, `materialize.ts:443-487`) whatever the stamps say, and lists per file what
  a sync would update or add. Drift stays D19's: the disk against the lock.
- The lock records a digest of the canon the repository was synced from. That gives `status` three answers:
  - **Behind:** the lock's version is older than this CLI's canon.
  - **Differs from this CLI's canon:** the versions are equal but the digests differ, so which side is newer cannot be told. `sync`
    keeps WSSETUP4's refusal (`refuseNewerLock`) and never rolls a newer lock back.
  - **Unchanged:** neither of the above.
- The install's registry reads `status --json` through the desktop program's hand-off to the CLI. There is no C# copy of D19's table
  (D117).

**B. The reconcile, a case of the set-up press** (cli, driver; D117 §6, D124 §2, D128 point 2).
- `daoris reconcile` sits at the root of the command tree (ONEWAY1 §11). Offline, it composes a brief in the canon's words, with no
  decision numbers, listing only the differences:
  - what A says a sync would update or add;
  - the repository's edited copies (D19's drift);
  - a set-up gap, such as `AGENTS.md` over Codex's 32 KiB, which `check` already reports (`drift.ts:365-374`).
- On the install, the reconcile is a new case of the set-up press (`Setup/Domain/SetupBrief.cs`). It reuses the press's verb
  allow-list (`SetupPressRules.Verbs`), its order and duplicate check (one open reconcile per repository), and D128 point 2's
  checks-green bound.
  - The driver adds the machine items to the quest it publishes, never to the verb: the standing answer's lines that state a
    repository rule (point 4).
  - The press is offered where A reports an update or a difference. Running it is the person's press, or a policy the person sets.
- The session decides each item:
  - **Adopt:** sync the file.
  - **Keep, with its reason:** recorded in `daoris.json`, as `switchedOff` is, together with the canon file's digest it was judged
    against. A later canon change to that file reopens it.

    A keep is a repository's own text of a rule. Dropping a core rule stays refused (D4, D71). Their amendment allows only a kept
    text with its reason.

    D19 gains the kept rows: kept and the canon unchanged; kept and the canon changed again (reported again); kept and the file absent
    (refused for a core rule); kept and the file retired (the keep is dropped). `sync` passes over a kept file, and `sync --force`
    passes over it too.
  - **Upstream:** outside the canon's own checkout, `daoris upstream` composes a quest's words, offline, to the canon's repository.
    It no longer writes across. Inside the canon's checkout it writes as today. The canon's text naming the route changes in the
    same chunk (`canon-authoring.md`, the doctrine region).

**C. The standing block** (driver; the service supplies knowledge rows).
- **Who composes it.** The driver composes it, because every input is the driver's: the routing standing, the process line, the
  brief, and the memory the line read.
- **When it is handed.** At a session's start, and at a move through the brief. A resume of the same conversation is not handed it
  again (the resume contract, `DriverContinuations.cs:425-426`). A go-on carries D229's counts and what changed, as today.
- **What it replaces.** Exactly these:
  - the routing line;
  - the standing answer's preface;
  - the driven instruction's "where things are" pieces: the map, the look and the indexes (`Adapters.cs:521`, `:688-693`).
- **What stays.** The driven instruction's quest, go-ahead, asking, closing, boundary, git, landing, trailer and language pieces are
  the instruction itself, and stay.
- **What it carries:**
  - where things are, as the one section that replaces those pieces;
  - the routing;
  - the person's machine choices (the standing answer, point 4);
  - the repository's and workspace's facts;
  - the memory the line read.

  It carries the rule index only where the native file is cut, as with Codex past 32 KiB. A get id reads the repository's own row in
  the DB.
- **Its bound.** The binding limit is the native door's single argument, 32,767 characters on Windows, which the instruction shares
  (`AskWords.cs:96-98`, `DAORIS_TARGET`). The proof is the whole first prompt at its bounds, per door and harness: the instruction,
  the person's words at their cap, the channel and the block, each under the limit with margin. Where they cannot all fit, the block
  goes through the door's file channel, not the command line.
- **Recording.** The block is its own section in CONTEXT1's composer account, recorded on the record as `chat.told-routing` is, with
  catalogue rows in both languages.
- **Every kind of session gets one,** including chats, Ask Daoris, helpers and intakes. A kind with no repository, such as a
  brainstorm, gets the workspace's part only.

**D. Tools at the start** (driver).
- Every Claude door loads the Daoris connector's tools at the start, not as deferred names. This reverses HELP5's choice for
  repository conversations, with a note under HELP5's record.
- The first request's size is measured with the tools loaded and without. Where the harness can scope the load to one server, only
  Daoris's connector loads; where it cannot, the cost is stated. Codex already holds the tools.

**E. Skills and memory in Daoris's sessions** (driver; not after d).
- A session's skills are the tree's own, plus Daoris's through the per-session plugin copy (`SpawnSkills`).
- The vendor's account-synced set is hidden for that session by its own settings or variables, where the pinned build allows it.
- Auto-memory is off for that session. Its memory is Daoris's memory store (D220).
- An account's home is never written: `ProfileHome`'s rule, "Daoris owns this location and nothing inside it", stands. Account-level
  connectors are scoped the same way as skills.
- Codex has no native skills. It reads the tree's skills by path, and the block says where they are.
- Proof: two accounts' first requests listing the same skills and connectors, and auto-memory off, with both homes byte-identical
  before and after.

## 4. Rows

Contract and proof for each row:

- **CONTEXTONE1a, an update that can be seen** (cli; now). Proof: a fixture adopter with
  - one file the newer canon changed;
  - one file it added;
  - one edited here;
  - an equal stamp with a different digest, said as *differs* and never rolled back.

  `status --json` carries each.
- **CONTEXTONE1d, tools at the start** (driver; now). Proof:
  - the Claude driven start, chat, go-on, hand-over, helper and intake each list the connector's tools;
  - the first request's size, measured and stated.
- **CONTEXTONE1b, the reconcile** (cli, driver; after a). Proof:
  - the verb's brief over a fixture adopter;
  - a kept file not reported again until its canon file moves;
  - `sync --force` passing a kept file;
  - `upstream` composing a quest outside the canon's checkout;
  - D4, D19, D71 and D135 amended;
  - on the install, one adopter reconciled through the set-up press, on the owner's yes, the Daoris repository's standing answer
    among its items.
- **CONTEXTONE1c, the standing block** (driver; after b, and after the COMMS1 wave reaches main). Proof:
  - the whole first prompt at its bounds, per door and harness, under the limit;
  - the replaced pieces censused gone;
  - the record note and catalogue rows in en and zh.
- **CONTEXTONE1e, skills and memory in Daoris's sessions** (driver; now, beside d). Proof: as §3 E.

## 5. What it rejects

- **Doctrine served only by Daoris.** A repository must work for a developer without Daoris.
- **A mechanical sync that overwrites a repository's differences.** Not every repository develops as the canon says, so a model
  judges each one in the repository's own session.
- **A reconcile beside the set-up press**, and an `upstream` beside the reconcile.
- **A C# copy of D19's table.**
- **Writing into account homes, the vendor's set, or a person's tool home.**
- **A block handed again on every resume.**
- **Conversions of older shapes outside ONEWAY1.**

## 6. Round 1's read, taken

- **1 and 14.** A is an update check, not drift. It reports `added`, not *missing*. Its order comes from the lock's canon digest and
  WSSETUP4's refusal.
- **2.** The keep is recorded in `daoris.json` with the canon digest it was judged against. D4, D19 and D71 are amended in row b.
- **3.** The registry reads `status --json`.
- **4.** The reconcile is a case of the set-up press. Its verb sits at the root.
- **5.** The machine items are the driver's, added to the quest. Point 4 is narrowed. D135 §3 is amended only for repository rules.
- **6.** `upstream` composes a quest outside the canon's checkout. The canon's text changes with it.
- **7.** The driver composes the block.
- **8.** The bound is the whole first prompt against the native argument, with a file channel past it.
- **9.** The replaced pieces are listed exactly. The standing answer has one fate: it is served as the person's machine choices, and
  its rule lines move through the reconcile.
- **10.** The block is handed at a start and a move, never on a resume.
- **11.** E is session-scoped, with no home written.
- **12 and 13.** G is removed: ONEWAY1 owns the conversions. The standing answer is never left unserved.
- **15 to 27.** Taken where named above. The row title is rewritten (25). Rows a and d build now on the read's word (26).
