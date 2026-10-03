# The documents — what each one is now

Every document under `docs/`, by what kind of thing it is and where it stands. A document is written
once for a moment, and later decisions amend it. When a document and a later decision disagree, the
decision wins, and the amended documents say so where they were amended. The superseded ones are in
[`archive/`](archive/README.md), with what replaced each.

**Kinds.** A *contract* is what a part is and must keep being; it is built against. A *method* is
how something gets built. A *study* is input to a decision, read for its reasoning. *Evidence* is
what was measured, and it stays a record of that moment. A *record* is append-only history. A
*proposal* is a suggestion brought from outside the record, kept as written; a review weighs it before
anything is built.

**Rows.** A row gives a document's kind, what it is for, and where it stands in one line naming the
decision that changed it. What was built under it is that decision's notes, never the row, so a build
changes no row (D127).

## Contracts and methods

| Document | Kind | For | Where it stands |
|---|---|---|---|
| `2026-08-04-daoris-design.md` | contract | the CLI | Current. Its notes carry D54, D59 and D105 where they changed it |
| `2026-08-05-knowledge-service-design.md` | contract | the service | Built. Its banner names what later decisions answered, D123's pieces of a long entry among them; §3–§4, the disclosure classes, still bind |
| `2026-09-19-driver-design.md` | contract | the driver (D45, D46) | Current, with D72's permission change, D104's interrupted stop, PAR1's sessions side by side in trees, DEV3's sessions outliving their tick (§9), D131's resumed answer (§10) and D137's reopened record (§4) noted |
| `2026-09-19-platform-design.md` | contract | the platform (D38) | Current in shape; the views it names have moved since (D55, D66, D75) |
| `2026-09-19-platform-ux.md` | contract | the design language (D41, D56) | Current. §4 records what each looking pass settled; read it before changing anything a person looks at. D141 retired *prose at 65ch*: a page wraps at its pane's edge |
| `2026-09-19-frontend-architecture.md` | method | the platform's stack and tests (D42) | Current |
| `2026-09-20-workspace-design.md` | contract | workspaces (D48, WSP) | Current |
| `2026-09-20-interactive-design.md` | contract | chat, console, managed harnesses (D49, SES) | Current, with D67, CONSOLE2's streams and D98 (§7 reversed) noted |
| `2026-09-20-remote-design.md` | contract | the remote (D47) | Current; its superseded list names what D68 replaced |
| `2026-09-21-working-surface-design.md` | contract | the working surface (D51, D52) | Current, with D66 and D113 (a landed session's review reads its landed branch) noted |
| `2026-09-21-working-surface-components.md` | method | how a screen is built | Current: a story first, and a molecule imports no hook |
| `2026-09-22-instruction-file-design.md` | contract | the always-loaded tier in `AGENTS.md` (D59) | Current. D117 extends it to the on-demand tiers; the CLI's half is built (LAYOUT3) |
| `2026-09-21-desktop-frame-design.md` | contract | the desktop's frame (D56) | Current, with D66, D75, BRW7–BRW8 (the browser's door on the strip, and who is driving it) and D118 (every view's own list pane and main area, to be built) noted |
| `2026-09-22-toolchain-design.md` | contract | binaries, pins, accounts, usage (D57) | Current, with D63, D67 and D98 noted, and D125, whose design replaces its §2 part 4 and §6 (rotation). The home of the resolution rule |
| `2026-09-23-api-key-accounts.md` | contract | an account that is an API key (D67 §1) | Current |
| `2026-09-23-intake-design.md` | contract | an ask becomes quests (D65) | Current, with D70, D72 and D77 noted, and USE1c's done ask |
| `2026-09-23-map-design.md` | contract | the maps (D67 §3, MAP) | Current |
| `2026-09-23-plugin-design.md` | contract | plugins (D64), and making one with the kit (§9, D101) | Current, with D71, D77's `${data}`, D78's `${browser}`, D100's `work/land` (D102: a landed branch handed on after its landing) and PLUG9's making and installing from Ask Daoris noted. D145 adds two frame fields, D146 `${proof}` |
| `2026-09-23-sync-design.md` | contract | the remote as a git remote (D68) | Current, with D69 and D95 (a delete travels as `deleted`) noted |
| `2026-09-24-menus-design.md` | contract | the menus as setup domains (D75) | Current |
| `2026-09-24-permission-scopes-design.md` | contract | what an agent may do (D72, D74), and what it may reach across (D107, §4a) | Current |
| `2026-09-27-in-app-browser-design.md` | contract | Daoris's own browser, driven over MCP (D78) | Current in its seam (§3.4–§3.6); its banner says what D84, D85, D99 and each build since changed |
| `2026-09-27-ask-and-wait-design.md` | contract | A session asks another repository, waits, and is resumed with the answer (D79) | Current. ASK1–ASK3 built; §1 amended while building (the quest stays taken); §1.6 is D80, the carry-on after a cut-off |
| `2026-09-28-chromium-host-design.md` | contract | Daoris's page and its browser on an embedded Chromium it ships, under Shenora's frame (D85) | Current. CHR1–CHR4 landed (the page on Shenora's Chromium, D92; the install's shape, D93), and CHR8 puts the browser on the same engine, as the application started with `--daoris-browser` (**D99**): one Chromium |
| `2026-09-29-ask-daoris-design.md` | contract | Ask Daoris: a conversation about Daoris itself, which proposes and the person confirms (HELP1) | Built (D89, D110); §2 says where it lives, and §9's build order what each step since added |
| `2026-09-29-dock-design.md` | contract | Panels that dock as VS Code's do: views that move between regions, the region toggles, Quick Ask (DOCK1, SURF11) | Built (DOCK1a–e, §4); TABS1 and D118 are noted where they amend it |
| `2026-09-30-machine-log-design.md` | contract | The machine log: what happens on this machine, without anyone's words, to improve Daoris from (LOG1, D94) | Built (D94); §4 and §6 say, as built, what each line measures and each door reads |
| `2026-09-30-terminal-design.md` | contract | The terminal: a real shell of the person's own in the console panel (CONSOLE4, D96) | Built (CONSOLE4a–c, §4); the look on the window in both themes is still to take |
| `2026-09-30-setup-guide-design.md` | contract | The setup guide: what a first start walks through, on the facts Ask Daoris's starters read (SETUP1, D97) | Current; SETUP1a–b in its §3 order |
| `2026-09-30-parallel-development-design.md` | contract | Parallel development: what eighteen merges collided on, the rules, the splits and the lane map (MOD1–MOD9) | Built (MOD1–MOD9, D106); its status line says what D115 and DEV2 moved since |
| `2026-10-01-self-development-design.md` | contract | Daoris develops Daoris: lanes a repository declares, sessions side by side, a merge queue as a landing form, a steward that keeps the records (DEV1) | Designed (D115); building, and D115's notes say what each build settled |
| `2026-10-01-naming-design.md` | contract | Names: a name is a UI element, designed in each language; the kinds, their rules and budgets, the glossary and the check (NAME1, D116) | Current (D116); D116's notes say what each build settled |
| `2026-10-01-agent-layout-design.md` | contract | One repository, every agent: knowledge and skills under `.agents/`, a skills mirror and imports instead of links, rooms, and a repository set up by its own session (LAYOUT1) | Designed (D117); building, and D117's notes say what each build settled |
| `2026-10-01-frame-model-design.md` | contract | One frame for every view: the list pane and the main area each view owns, what each view puts in them, and the build (FRAME1, D118) | Designed (FRAME1a); built as FRAME1b–i, and PLUGUI1's screen on it |
| `2026-10-01-plugins-screen-design.md` | contract | Plugins get a view of their own: a list by what they need, a page per plugin with its health, activity, tests and data, what the host answers, and what leaves Settings (PLUGUI1, D119) | Designed (D119); building, and D119's notes say what each build settled. §3.1's groups and §3.2's header amended by D140 |
| `2026-10-01-development-documents-design.md` | contract | The development documents every repository keeps for code generation, and fewer asks: the safe work a repository declares and how each agent is handed it (DOC1, UNBLOCK1) | Designed (D122); building, and D122's notes say what each build settled |
| `2026-10-01-tools-design.md` | contract | Tools: every program Daoris runs beside its agents (Git, Node.js, PowerShell, GitHub CLI, Azure CLI) is the system's, managed, or a file you name; a `resources.json` built in, and resource locations that extend it without a release (TOOLS1) | Designed (D121); building, and D121's notes say what each build settled |
| `2026-10-01-plugin-distribution-design.md` | contract | Plugins leave the repository: a workshop in the home where Daoris makes the plugins people ask for, Daoris.Plugins for Daoris's own, and NuGet as the package source *Find plugins* searches (PLUGREPO2, PLUGDIST1, D120) | Designed (D120); building, and D120's notes say what each build settled |
| `2026-10-01-workspace-setup-design.md` | contract | Setting a whole workspace up so sessions find answers instead of asking: the install's doctrine tool, a set-up quest, registration read from the line, a paced plan, a README baseline, and a session that looks before it asks (WSSETUP1, D124) | Designed (D124); building, and D124's notes say what each build settled |
| `2026-10-01-account-rotation-design.md` | contract | Account rotation: a limit read from the agent's own words on the door that refused, a cool-off to the reset it names, never a strike, and the next start on the next account of the person's order (TOOL4, D125) | Designed (D125), amended by D130; building, and D125's notes say what each build settled |
| `2026-10-02-session-management-design.md` | contract | Sessions that are easy to manage: listed by what they need, every act on its row and page header, a stop that holds its quest, what ended archived, a quest's short title, and each act's terminal and Ask Daoris doors (SESSUX1, D126) | Designed (D126); building, and D126's notes say what each build settled |
| `2026-10-02-session-economy-design.md` | contract | What a session reads and writes, measured, and the doctrine that keeps it small: rows and entries point to their detail, and a report holds their shape (SESSOPT1, DOC7) | Designed (D127). §1 measures, §2 drafts the canon text, §6 is the build |
| `2026-10-02-setup-pilot-lessons-design.md` | contract | What the first real set-up taught: the repository's checks kept green (knowledge declared in place, a moved path the one change allowed in a CI file, red never `done`), knowledge and skills as an index read on demand, a document without frontmatter, the pilot's follow-up (WSSETUP14) | Designed (D128); nothing built. It amends D124 §2, D117 §5.4, D122's `knowledge` role and D59's region; D129 amends its §2.5 |
| `2026-10-02-knowledge-design-review.md` | contract | The knowledge design reviewed: what each agent loads, eight candidates, and the index as the floor with recall by meaning on top (KNOW2) | Reviewed (D129, pending KNOW3's measurement); nothing built |
| `2026-10-02-account-use-design.md` | contract | Account use, for any number of accounts: which may run each workspace, as the person states; by default the most work from all of them (§16), one by one as an override; what is left, by the agent's word (TOOL6) | Designed (D130); nothing built. Amends D125 §3, §6 and four TOOL4 rows |
| `2026-10-02-answer-continues-design.md` | contract | An answer continues the session: the parked record reopens and its harness conversation resumes on the same account, adapter and tree; otherwise today's carry-on, saying why (ANSWER1) | Designed (D131); building, and D131's notes say what each build settled. D137 amends §3: a finished record reopens on the person's words |
| `2026-10-02-pause-and-clean-up-design.md` | contract | Pausing and abandoning an ask or a quest: a pause that stops what runs and keeps its place, and an abandon listed first that declines with the person's reason and discards only what nothing else holds (PAUSE1) | Designed (D132); nothing built. Amends D126 §3.3, D88's removal and D68 rule 2 |
| `2026-10-03-decisions-record-design.md` | contract | The decisions record under parallel merges: how often union merges tore it and in which shapes, four options weighed, one file per decision with its notes, a check per file, and the migration (DOC8) | Designed (D134); nothing built. Amends D106's and D117 §2.4's rejection of a file per decision |
| `2026-10-03-session-messages-design.md` | contract | Session messages: a person's words reach a session at its next step, its turn's end, or by reopening its record and resuming its own conversation; Codex's and the native doors read (MSG1) | Current (D137): MSG1a–f built, with the shell's half (MSG1e2, MSG1c2); D137's notes say what each built. Amends D131's rejection of reopening a finished record, D90, D136 and CONV4a |
| `2026-10-03-update-when-idle-design.md` | contract | Update when idle: a build staged beside the install, a drain that starts nothing new, the launcher swapping `app/` while the application is closed, and its roll-back (UPDATE1) | Built (D139); the deployment rehearsal's phase 9 is its gate. Amends D60's and D93's publish |
| `2026-10-03-context-menu-design.md` | contract | The right-click menu: the page's own, each surface's acts from the owner it has, selected text, a link and a code span; the engine's menu kept for fields and suppressed elsewhere (CTX1) | Current (D138). Amends the platform language §4 and §6 |
| `2026-10-03-plugin-catalogue-design.md` | contract | The plugins page as a catalogue (installed, Daoris's own, available), a plugin's icon declared in its manifest and handed to the page as its bytes, and the monogram in its place (PLUGUI2) | Current (D140), built (PLUGUI2, PLUGUI2b). Amends the plugin design §3 and the plugins screen design §3.1–§3.2 |
| `2026-10-03-language-design.md` | contract | Languages: Daoris's lines in a session's note worded by code, someone's words shown as written, every line the driver writes coded, and a session language set for the work, apart from the window's (LANG1) | Designed (D142); LANG1a and LANG1c built (D142's notes). Amends the platform language §4 and the frontend architecture §3 |
| `2026-10-03-evidence-design.md` | contract | Evidence Daoris checks: a requirement names a path the done's commit must hold, or a declared gate read from the queue's verdict; a met answer without it holds the quest as a departure does (EVID1) | Designed (D144); nothing built. Amends D46, D133 §3–§4, D65 §4 and D115 §5. D146 amends its §7 |
| `2026-10-04-landing-and-proof-design.md` | contract | Accepting done work automatically, the pull request the last human step, a chain on one branch (LAND2); captured proof, screenshots and API answers kept on the machine, which a requirement may require (EVID2) | Designed (D145, D146); nothing built. Amends D82, D87, D100, D113 §3, D144 and D78 §3.5 |
| `2026-10-04-built-in-git-design.md` | contract | Git built in: a place holding each repository's branches by kind, history as a graph, a commit, a file's history and blame, a compare; acts on refs, a push only on the person's press, managed Git offered (GIT1) | Designed (D147); nothing built. Amends the push in D87, D100 and D109, D97 and D66 |
| `2026-10-04-plugin-hooks-design.md` | contract | Plugin hooks: the points plugins speak at today; a query asking a landed branch's plugin for its pull request's state, so a squash-merged pull request's branches can go; other processes weighed (PLUGHOOK1) | Designed (D148); nothing built. Amends D102's rejection of asking the platform, D88, D113, D147 §3.4 and D64 §4 |

## Studies and evidence

| Document | Kind | Carried by |
|---|---|---|
| `2026-09-20-working-surface-research.md` | study | SURF1, the working-surface design |
| `2026-09-21-dsh-evaluation.md` | evidence | D53 |
| `2026-09-21-ide-reference-study.md` | study | D55 |
| `2026-09-22-acp3-probe-evidence.md` | evidence | D53, ACP3 |
| `2026-09-22-first-deployment-case-study.md` | evidence | D60, D63. Read it before touching the desktop |
| `2026-09-22-plugin-design-study.md` | study | the plugin design, D64 |
| `2026-09-24-agt2b-channel-evidence.md` | evidence | D67, AGT2b |
| `2026-09-24-deploy1-acp-trust-evidence.md` | evidence | D73 |
| `2026-09-24-reference-gap-study.md` | study | D76, the live direction |
| `2026-09-25-stream-json-evidence.md` | evidence | D76, CONV3a, CONV5 |
| `2026-09-25-message-content-evidence.md` | evidence | D76, CONV4c, CONV4d |
| `2026-09-27-first-goal-study.md` | study | D77, the first goal: what INT6 would have met, the references, Lyntai's storage |
| `2026-09-28-console2-streams-evidence.md` | evidence | CONSOLE2: a subagent, a background task and a command's output on the protocol door, once declared. What it left open, whether a driven session waits for its background work, is D105's |
| `2026-09-30-console3-native-streams-evidence.md` | evidence | CONSOLE3c: the same on the native door's `stream-json`: a subagent's lines by `parent_tool_use_id`, tasks on `system` lines, and background work ended with the turn |
| `2026-09-28-after-the-first-workspace.md` | study | WSR1–WSR3, SESS1, MAP4, HELP1: what the first real workspace showed Daoris lacks. §3 is decided by D86 (WSR2 landed), §1 by D87 (WSR1 landed), D100 (WSR4 landed) and D102 (WSR5b landed), §2 by D88 (WSR3 landed) and D102 (WSR5a landed) |
| `2026-09-28-managed-edge-evidence.md` | evidence | D84: a real browser started by Daoris, its tabs, its sign-ins, and the account a fresh Edge profile brings |
| `2026-09-28-chromium-embedding-evidence.md` | evidence | D85, CHR1: an embedded Chromium's debug port, an agent's tab, a sign-in across a restart, the round trip, size, codecs and licences |
| `2026-10-01-entry-point-evidence.md` | evidence | D117, LAYOUT2: what Claude Code (both doors), codex, codex-acp and dsh read, from their shipped code, keylessly. The Claude Code ACP adapter loads project instructions; codex cuts its project docs at 32,768 bytes; dsh lists a mirrored skill once. §6 is the canary turn, the owner's to run |
| `2026-10-01-tools-resources-evidence.md` | evidence | D121, TOOLS3: where each line of the list built in was read from, each maker's published sum, each archive's layout, and the whole files streamed once and matched. MinGit carries an ssh of its own and no bash. A test holds the list to it |
| `2026-10-02-knowledge-bench-results.md` | evidence | KNOW3, for D128 and KNOW2 (D129): 72 real headless sessions over this repository's documents, behind six knowledge designs, by `tools/knowledge-bench.mjs`. Every design found every document. The region's table is fastest and costs 9,447 tokens a request at 73 documents; the on-demand index costs one more call and was read whole every time; a pushed BM25 top 5 was cheapest when right. §2 proves the isolation, §5 the threats |
| `2026-10-02-limit-signals-evidence.md` | evidence | TOOL4b, for D125 and D130: what each door says about an account's limits, read keylessly, one turn measured. Claude Code's frame gives each window's use on every turn; its protocol door forwards it in `_meta`; `codex-acp` forwards none, and its limit error's message is `Internal error` alone |
| `2026-10-02-ask-drift-evidence.md` | evidence | D133, DRIFT1: one ask's two requirements traced through its intake, two quests and ten sessions. The intake kept a word and lost its meaning; each correction reached one session, then a one-hop carry-on and an account's limit dropped it; a closing note became the requirement. §6 proposes the rows |
| `2026-10-03-steer-evidence.md` | evidence | D136, STEER1: what each door does with a word sent during a turn, read keylessly, one session measured. A second `session/prompt` reaches Claude Code at its next step; `_session/steering` interrupts the step in flight; a stop with words on their way loses their answer; `codex-acp` must not get a second prompt |
| `2026-10-03-knowledge-use-evidence.md` | evidence | D135, KNOWUSE1: the 46 questions 33 sessions of the owner's work repository put to the owner, classed by what could answer each. Sessions read their knowledge; 2 items were answered by a document they read, 25 needed the owner, 13 of them asks for three production acts. §6 proposes the rows |
| `DAORIS_FUTURE_DIRECTIONS.md` | proposal | An outside analysis the owner brought in (2026-10-03): 73 suggestions for proving, explaining, recovering and learning from driven work, kept as written. Reviewed in `2026-10-03-future-directions-review.md`; nothing is adopted from it without a decision |
| `2026-10-03-future-directions-review.md` | study | That proposal against the record: 18 sections exist, 38 partly, 15 are new and 2 conflict (D130; D46 with D69). It recommends TRACE1, EVID1, CONTEXT1 and OUTCOME1 first, and §66 as a decision. ROADMAP's horizon points to it |

## Records

| Document | What it holds |
|---|---|
| `decisions/` | Every decision, one file each (`D<n>.md`), with its reasoning and its notes; from D51 on, with what it rejected. `DECISIONS.md` is the page that says so (D134) |
| `task-archive.md` | Every finished task, with its date and outcome |
| `FIX-LOG.md` | Root cause, fix and verification for each non-trivial defect |
| `2026-09-25-rev3-review.md` | REV3's ledger, and CLEAN1's item by item |
| `2026-09-26-ux5-screen-audit.md` | UX5's ledger: every surface and state looked at, and what each finding became |
| `2026-09-28-sess1-session-view.md` | SESS1's ledger: the session view against the first real workspace's sessions, and what each finding became |
| `2026-09-29-sess2-session-head.md` | SESS2's ledger: the session's top section against the real heads, and what each finding became |
| `2026-10-01-naming-audit.md` | NAME1a's audit: every label key, its kind, the names in both languages before and after, with why; NAME1b applied every row (2026-10-01) |
| `2026-10-01-frame-audit.md` | FRAME1a's audit: every view's layout affordances read from the code, with the file and line that decides each, and what each finding became |
| `adoption/` | A local mechanics draft prepared for an adoption that has not run |
| `code-map.json` | Generated by the devkit's `map`, and gated by `map --check`; never edited by hand |
