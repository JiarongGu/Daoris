# The documents — what each one is now

Every document under `docs/`, by what kind of thing it is and where it stands. A document is written
once for a moment, and later decisions amend it. When a document and a later decision disagree, the
decision wins, and the amended documents say so where they were amended. The superseded ones are in
[`archive/`](archive/README.md), with what replaced each.

**Kinds.** A *contract* is what a part is and must keep being; it is built against. A *method* is
how something gets built. A *study* is input to a decision, read for its reasoning. *Evidence* is
what was measured, and it stays a record of that moment. A *record* is append-only history.

**Rows.** A row gives a document's kind, what it is for, and where it stands in one line naming the
decision that changed it. What was built under it is that decision's notes, never the row, so a build
changes no row (D127).

## Contracts and methods

| Document | Kind | For | Where it stands |
|---|---|---|---|
| `2026-08-04-daoris-design.md` | contract | the CLI | Current. Its notes carry D54, D59 and D105 where they changed it |
| `2026-08-05-knowledge-service-design.md` | contract | the service | Built. Its banner names what later decisions answered, D123's pieces of a long entry among them; §3–§4, the disclosure classes, still bind |
| `2026-09-19-driver-design.md` | contract | the driver (D45, D46) | Current, with D72's permission change, D104's interrupted stop, PAR1's sessions side by side in trees, and DEV3's sessions outliving their tick (§9) noted |
| `2026-09-19-platform-design.md` | contract | the platform (D38) | Current in shape; the views it names have moved since (D55, D66, D75) |
| `2026-09-19-platform-ux.md` | contract | the design language (D41, D56) | Current. §4 records what each looking pass settled; read it before changing anything a person looks at |
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
| `2026-09-23-plugin-design.md` | contract | plugins (D64), and making one with the kit (§9, D101) | Current, with D71, D77's `${data}`, D78's `${browser}`, D100's `work/land` (D102: a landed branch handed on after its landing) and PLUG9's making and installing from Ask Daoris noted |
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
| `2026-10-01-plugins-screen-design.md` | contract | Plugins get a view of their own: a list by what they need, a page per plugin with its health, activity, tests and data, what the host answers, and what leaves Settings (PLUGUI1, D119) | Designed (D119); building, and D119's notes say what each build settled |
| `2026-10-01-development-documents-design.md` | contract | The development documents every repository keeps for code generation, and fewer asks: the safe work a repository declares and how each agent is handed it (DOC1, UNBLOCK1) | Designed (D122); building, and D122's notes say what each build settled |
| `2026-10-01-tools-design.md` | contract | Tools: every program Daoris runs beside its agents (Git, Node.js, PowerShell, GitHub CLI, Azure CLI) is the system's, managed, or a file you name; a `resources.json` built in, and resource locations that extend it without a release (TOOLS1) | Designed (D121); building, and D121's notes say what each build settled |
| `2026-10-01-plugin-distribution-design.md` | contract | Plugins leave the repository: a workshop in the home where Daoris makes the plugins people ask for, Daoris.Plugins for Daoris's own, and NuGet as the package source *Find plugins* searches (PLUGREPO2, PLUGDIST1, D120) | Designed (D120); building, and D120's notes say what each build settled |
| `2026-10-01-workspace-setup-design.md` | contract | Setting a whole workspace up so sessions find answers instead of asking: the install's doctrine tool, a set-up quest, registration read from the line, a paced plan, a README baseline, and a session that looks before it asks (WSSETUP1, D124) | Designed (D124); building, and D124's notes say what each build settled |
| `2026-10-01-account-rotation-design.md` | contract | Account rotation: a limit read from the agent's own words on the door that refused, a cool-off to the reset it names, never a strike, and the next start on the next account of the person's order (TOOL4, D125) | Designed (D125); building, and D125's notes say what each build settled |
| `2026-10-02-session-management-design.md` | contract | Sessions that are easy to manage: listed by what they need, every act on its row and page header, a stop that holds its quest, what ended archived, a quest's short title, and each act's terminal and Ask Daoris doors (SESSUX1, D126) | Designed (D126); building, and D126's notes say what each build settled |
| `2026-10-02-session-economy-design.md` | contract | What a session reads and writes, measured, and the doctrine that keeps it small: rows and entries point to their detail, and a report holds their shape (SESSOPT1, DOC7) | Designed (D127). §1 measures, §2 drafts the canon text, §6 is the build |

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

## Records

| Document | What it holds |
|---|---|
| `DECISIONS.md` | Every decision, numbered, with its reasoning; from D51 on, with what it rejected |
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
