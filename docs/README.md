# Documentation router

Find a document here, then read the relevant section. A **contract** states what a part must keep;
a **method** describes how to build it; **studies** inform decisions; **evidence** records a dated
observation; **records** hold history; a **proposal** needs a decision before adoption.

Later decisions override earlier contracts. A contract's baseline is historical; implementation
and amendments live in its numbered decision's dated notes. `TASKS.md` holds remaining work.
This router points to those homes rather than maintaining another build-status list (D127/D159).
Paths below are relative to this file. Historical documents have their own [archive router](archive/README.md).

## Guides

| Document | Purpose and standing |
|---|---|
| `../README.md` | Landing page on GitHub and npm (D211): pitch, quickstart, packages, status, agent rules; ships in the CLI package, so every link absolute |
| `guide/README.md` | The reference behind the README: commands, manifest, layers, seams; GitHub's About and social-preview text |
| `../llms.txt`, `../context7.json` | For coding agents (D211): adopter docs indexed; README's agent rules, held equal by `tools/doc-links.mjs` |
| `assets/` | Package icon (PNG from `tools/make-icon.py`), its SVG, and the README's 36 px logo; the seal's twins |
| `../AGENTS.md` | Shared brief and generated doctrine |
| `../CLAUDE.md` | Generated import of AGENTS; no separate guide |
| `development.md` | Contributor setup, checks, integration and documentation maintenance |
| `../ROADMAP.md` | Forward sequence; TASKS owns acceptance criteria |
| `../src/Daoris.Desktop/README.md` | Desktop installation, machine bridge and instruments |
| `../src/Daoris.Service/README.md` | Knowledge, quests, hosts, trust and configuration |
| `../src/Daoris.Web/README.md` | Platform views, frontend conventions and test loop |
| `../src/Daoris.Devkit/README.md` | Universal gates, code map and distribution |
| `../examples/README.md` | Adoption and routing; exercised by family rehearsal |
| `../examples/engine/README.md` | Example engine adopter |
| `../examples/game/README.md` | Example game adopter |
| `../examples/plugins/hold-by-title/README.md` | Family rehearsal plugin fixture |
| `../examples/plugins/browser/README.md` | Standalone browser plugin example |
| `../examples/plugins/in-app-browser/README.md` | Browser connection plugin example |
| `../examples/plugins/github-pull-request/README.md` | GitHub landing plugin example |
| `../examples/plugins/azure-devops-pull-request/README.md` | Azure DevOps landing plugin example |
| `../examples/plugins/local-preview/README.md` | Performed step plugin example, the kit's performer filled in |
| `../tools/dsh-probes/README.md` | Dated protocol probe instructions |

## Doctrine, knowledge and development contracts

| Document | Kind and purpose | Governing record |
|---|---|---|
| `2026-08-04-daoris-design.md` | Contract: CLI doctrine lifecycle | D54/D59/D105 amend original |
| `2026-08-05-knowledge-service-design.md` | Contract: indexing and disclosure | D21/D24/D123; banner lists amendments |
| `2026-09-22-instruction-file-design.md` | Contract: always-loaded instruction region | D59/D117/D128 |
| `2026-09-23-map-design.md` | Contract: workspace, workflow and code maps | D67; ORIENT1 note |
| `2026-09-30-parallel-development-design.md` | Contract: records, splits and lanes | D106/D115 |
| `2026-10-01-self-development-design.md` | Contract: concurrent work and landing queue | D115 |
| `2026-10-01-agent-layout-design.md` | Contract: agent layouts, mirrors and rooms | D117/D128 |
| `2026-10-01-development-documents-design.md` | Contract: roles and safe-work declarations | D122/D127/D151/D159 |
| `2026-10-01-workspace-setup-design.md` | Contract: setup quests and registration | D124/D128/D129 |
| `2026-10-02-session-economy-design.md` | Contract: reading cost and record shapes | D127 |
| `2026-10-02-setup-pilot-lessons-design.md` | Contract: preserve knowledge and green checks | D128/D129 |
| `2026-10-02-knowledge-design-review.md` | Contract: discovery floor and recall | D129; KNOW3 bench evidence |
| `2026-10-03-decisions-record-design.md` | Contract: per-decision files and checks | D134 |
| `2026-10-04-orientation-everywhere-design.md` | Contract: repository indexes and source ranges | D151 |
| `2026-10-24-ci-scope-design.md` | Accepted (D207, 2026-10-24, the steward after round 6), the final round, making the named changes of round 5's one read, by the Opus foundation reader (accept after named changes, 10 findings, 3 blocking; Codex out of credits, its read of the final text owed), for the steward to check: a modular CI. Every test unit's reach and subject by one census, its recall judged against each failure's introducing commit, a lower bound; merges queued with their message and records, joined into one candidate in a tree of their own, run on the union of their reach and bisected by class or file on red; a reached .NET fast half whole, the TypeScript suites by file, the Process halves by class with the sweep, the git half within a budget; the full set in the background, never blocking a merge, one admission record for both runners, the one run a republish of the install waits for, with no waiver; known red by test and fingerprint, waived only in the queue; the rehearsals side by side once proven; each test's value measured | D207; CIFAST1, CISCOPE1b-g, TESTVALUE1 |
| `2026-10-24-ci-scope-reads.md` | Reads (CISCOPE1, under D191): Codex's and the independent Opus reader's foundation reads of rounds 1 to 5, verbatim; Codex asks another round each time it read (round 4: 8, 3 blocking; none for round 5, out of credits), Opus accepts after named changes from round 2 (round 5: 10, 3 blocking) | D207 |

## Driver, sessions, sharing and landing contracts

| Document | Kind and purpose | Governing record |
|---|---|---|
| `2026-09-19-driver-design.md` | Contract: planning and session lifecycle | D45/D46; D72/D104/D131/D137 amend |
| `2026-09-20-workspace-design.md` | Contract: workspace membership and sharing | D48/D68 |
| `2026-09-20-interactive-design.md` | Contract: chat, console and managed agents | D49/D67/D98 |
| `2026-09-20-remote-design.md` | Contract: optional team deployment | D47/D68 |
| `2026-09-22-toolchain-design.md` | Contract: binaries, accounts and usage | D57/D63/D67/D98/D125/D130 |
| `2026-09-23-api-key-accounts.md` | Contract: API-key account declarations | D67 |
| `2026-09-23-intake-design.md` | Contract: asks become repository quests | D65/D70/D72/D77/D133/D154 |
| `2026-09-23-sync-design.md` | Contract: fetch, rebase and push | D68/D69/D95/D153 |
| `2026-09-24-permission-scopes-design.md` | Contract: permissions and reading across | D72/D74/D107/D156 |
| `2026-09-27-ask-and-wait-design.md` | Contract: cross-repository questions and carry-on | D79/D80/D131 |
| `2026-09-30-machine-log-design.md` | Contract: machine-local measurements | D94; §4/§6 define lines |
| `2026-09-30-terminal-design.md` | Contract: person's shell in the panel | D96 |
| `2026-10-01-account-rotation-design.md` | Contract: limits, cool-off and rotation | D125/D130 |
| `2026-10-02-account-use-design.md` | Contract: participation and capacity | D130 |
| `2026-10-02-session-management-design.md` | Contract: groups, actions, archive and deletion | D126/D152/D153 |
| `2026-10-02-answer-continues-design.md` | Contract: resume on the same conversation | D131/D137 |
| `2026-10-02-pause-and-clean-up-design.md` | Contract: pause, resume and abandon | D132 |
| `2026-10-03-session-messages-design.md` | Contract: queued words and reopened runs | D137/D136 |
| `2026-10-03-update-when-idle-design.md` | Contract: staged update, drain and rollback | D139 |
| `2026-10-03-evidence-design.md` | Contract: completion evidence checks | D144/D146 |
| `2026-10-04-landing-and-proof-design.md` | Contract: automatic landing and kept proof | D145/D146/D149/D154–D157; D87's LANDSYNC0 amendment and LANDREBASE1 note replace §5's moved-line row |
| `2026-10-04-built-in-git-design.md` | Contract: branches, history and ref actions | D147 |
| `2026-10-07-history-clearing-design.md` | Contract: clear local finished history | D153 |
| `2026-10-08-review-environment-design.md` | Contract: setup and person's review verdict | D154/D155–D157 |
| `2026-10-08-second-agent-design.md` | Contract: another maker's agent reviews | D155/D157 |
| `model-roles.md` | Reference: which model, version and effort does which role, its evidence, the trials, and when to ask another maker | D174/D160; living, dated |
| `2026-10-09-person-door-design.md` | Contract: person/driver authorization | D156; starter integration remains in TASKS |
| `2026-10-09-workflow-design.md` | Contract: choices, versions and gates | D157 |
| `2026-10-14-co-working-design.md` | Contract: one session per line of work, its records its parts; an ask's channel | D167; SESSONE rows in TASKS |
| `2026-10-16-install-layout-design.md` | Contract: the Daoris folder's top folders, every home path placed, and the move into them | D172; HOMELAYOUT rows in TASKS |
| `2026-10-16-terminal-rpc-design.md` | Contract: the running Daoris answers the terminal's verbs over a pipe into its own dispatcher; the census of what acts outside the loop; a home's identity and its one lease | D171 point 3; HOMEID1, HOMELEASE1 and RPC1 rows in TASKS |
| `2026-10-16-fabric-events-design.md` | Contract: events over polling (every wait inventoried, the source-to-publish shape, a watcher measured under antivirus) and the end of a piece of work observed in git (parts A and B, accepted); part C, proposed: a cycle each workspace and repository defines, plugin-contributed steps, and events with chains in place of clocks | D173's FABRIC-EVENT1, acceptance and FABRIC-CHAIN1 notes; FABRIC rows in TASKS |
| `2026-10-19-verification-lanes-design.md` | Proposed: a quest step's checks run as lanes beside its work and report back; accepting and verifying are two facts; what stays blocking; a lane's record; a failure's consequences across a chain and across repositories | D180 point 7, D173's FABRIC-VERIFY1 note; FABRIC-VERIFY1a in TASKS |
| `2026-10-19-ask-daoris-modes-design.md` | Proposed: Ask Daoris's selected mode, effective mode and recorded wiring kept apart; full access per harness; restricted mode's grants as typed capabilities, never a widened mode; the panel's picker, its confirmation and *What it has* | D183; HELPMODE1a-c and HELPWIRED1 in TASKS |
| `2026-10-19-cross-agent-brainstorming-design.md` | Contract, as D176 places it (filed by the owner from a sibling workshop repository): one scoped question fanned to several agents of different makers, structured proposals back, one synthesis that flags what is hard to reverse for an adversarial check; a ranked proposal document, not a change | D176; BRAINSTORM1a–f in TASKS |
| `2026-10-20-message-urgency-design.md` | Contract (accepted 2026-10-20): how a message reaches a working agent; three levels (ordinary, urgent, now) and no message stops a turn; who may send each; one delivery rule over every path; `said` as each session's durable queue; four receipts | D188; COWORK1a-d proposed |
| `2026-10-20-session-context-design.md` | Accepted: a session's context is Daoris's record; the agent's own conversation resumed wherever it holds; a move to another agent, account or past a lost conversation a new part handed a brief composed without a model; compaction, cost and measurements owed | D189; SESSCTX1 rows |
| `2026-10-21-company-concept.md` | Contract (D193): Daoris framed as a development company run by agents; each part of the company mapped to the mechanism that serves it, and the roadmap ordered by those parts |
| `2026-10-20-fabric-cutover-design.md` | Contract (accepted 2026-10-20 under D191): a run bound to a graph version governed by its graph; a steps run unchanged | D190; FABRIC-CUTOVER1 rows |
| `2026-10-20-pipeline-design.md` | Proposed: a start node routes work into pipelines; sessions run light checks; verification its own pipeline on landed work | D192; FABRIC-PIPELINE1a-j |
| `2026-10-21-alone-path-design.md` | Proposed: `daoris driver`, `agent`, `plugin` and `tool` run the driver's own program, the running Daoris's client or alone under the lease; each change one typed operation in the driver library; one text per verb; the families' TypeScript retired | D194; PUSH1f2a-f proposed |
| `2026-10-21-cleanup-design.md` | Accepted (2026-10-21, after three rounds): everything Daoris writes, inventoried with its rule; one rule per kind set in `driver.json`'s `keep` and shown on This Daoris; a set-aside tree's package folders dropped once it has settled; the look, a free-space floor and a press, one run at a time | D195; KEEP1a-g and KEEP1a2 |
| `2026-10-21-account-pool-design.md` | Proposed (round 3, after Codex's and Fable's reads): an agent's Daoris accounts are one set for every workspace, with one order and one entry of settings; the lists folded at the first write, each change said until dismissed; the add-account dialog asks only a name; the own sign-in never asked or written | D197; POOL1a-d proposed |
| `2026-10-22-coagent-design.md` | Accepted (D199, 2026-10-22), round 3, after three reads: agents of different makers in one session; a helper reads the work in progress; a measured partner of another maker writes a piece in a clone of its own that lands only through the lead; one capability sheet per provider | D199; COAGENT1a-g proposed |
| `2026-10-22-git-auth-design.md` | Accepted (D198, 2026-10-22; round 4, after two Codex reads and Fable's; the default's two doors and the build's order settled by the steward): Daoris's git acts as git identities of its own (SSH keys it holds, HTTPS sign-ins in the Windows store through Git Credential Manager's own namespace), one set each remote points at, applied at each act by two doors (an ssh launcher and a remote helper), handing none of the person's credentials unless chosen; a remote that cannot authenticate held and said once; git access (read, write, dangerous writes, each allow, ask or deny) in the access setup, today's behaviour by default, judged at those doors for the landing and an agent alike, an ask asked at the door while the act waits | D198; GITAUTH1a-h proposed |
| `2026-10-23-session-watch-design.md` | Accepted (D200, 2026-10-23), round 3 after Codex's and Fable's reads: no time limit on a working session; a watcher rides each look, reads only what Daoris holds and supports a quiet session by a word, then the person with an end one press away; an automatic end only where the install's record allows | D200; SESSWATCH1a-f |
| `2026-10-23-start-path-design.md` | Reading and proposal: the desktop's start timed stage by stage (`start.stages`); what runs before the first page has its data (Chromium's start before the host's spawn, the first look's follow and tidy, the help room's look), why the splash lifted by its bound, and the cut with expected savings | STARTFAST1; STARTFAST1b built |
| `2026-10-23-terminal-trust-design.md` | Accepted (D201, 2026-10-23), round 3 after Codex's and Fable's reads: the person's standing confirmation, granted once in the window, lets a terminal outside every agent Daoris runs stop or finish a session and hold or resume a repository without the card; agents contained through a launcher; each use said | D201; TERMTRUST1a-b |
| `2026-10-23-coworker-brief.md` | Brief (COWORKUX1's co-design under D191, phase 1): co-workers, one model for every agent working beside a session (helper, partner, reviewer), shown and driven like sessions; the constraints, today's records, page and words by `file:line` in both languages, a real helper on the install whose answer never reached its lead, how other tools present agents working together, and the questions each proposal answers | D155, D163, D167, D199; COWORKUX1 |
| `2026-10-23-coworker-proposals.md` | Proposals (COWORKUX1, under D191): each maker's independent proposal from the brief, sealed before it read another, added verbatim; the synthesis is written apart | COWORKUX1 |
| `2026-10-23-coworker-reads.md` | Reads (COWORKUX1, under D191): Codex's read and Fable's foundation read of the design's round 1, verbatim, and Codex's read of round 2; rounds 2 and 3 answer each in the design's §15 | D203; COWORKUX1 |
| `2026-10-23-coworker-design.md` | Proposed (D203, 2026-10-23), round 3, the synthesis of the three proposals with both reads and Codex's read of round 2 answered (§15), the steward's acceptance remaining: a session's co-workers are its team, each a record serving its line in a role (helper, partner, reviewer, the reviewer gaining `serves`), drawn as *Co-workers* in the head, a card per exchange and a thread per member; the structures co-working takes (team, alongside, relay, several reviewers, mixed makers) named with their rules and chosen by their doors; a lead waiting on its own helper waits on it, not the person (as HELPERWAIT1 built it, cited by line; the partner's wait deferred); the names in both languages (同事 over 协作者); the test plan and rows, CHANPART1 and several reviewers each deferred behind a design of their own | D203; COWORKUX1a-i, HELPERWAIT1, HELPERWAIT1b, CHANPART1 proposed |
| `2026-10-23-limit-goes-on-design.md` | Design note (LIMITGOON1, built 2026-10-23): a conversation whose account's limit refuses its turn goes on, in the same conversation, on the next account; why Daoris's brief carries its context and not the agent's own resume (the transcript is in the cooling account's home), which words are held, and Ask Daoris's parts kept as one conversation | D189, D197; LIMITGOON1 |
| `2026-10-23-git-app-brief.md` | Brief (GITAPP1's co-design under D191, phase 1): Daoris's git as comprehensive as a git management app and Review fast; the constraints, today's git code by `file:line`, Review's and the git screens' cost measured on this machine, what established apps offer and how they stay fast, and the questions each proposal answers | D147, D150, D198; GITAPP1 |
| `2026-10-23-git-app-proposals.md` | Proposals (GITAPP1, under D191): each maker's independent proposal from the brief, sealed before it read another, added verbatim; the synthesis is written apart | GITAPP1 |
| `2026-10-23-git-app-design.md` | Proposed (D202), round 5 of GITAPP1's design under D191, answering Codex's read of round 4 (§19) after round 3's (§18), in two parts. Part one, all D202 settles: Review as an uncached parallel dependency graph of today's own git reads, its answers exactly today's (REVIEWBASE3's merged tree and git's line guess kept), the reads that can run a configured program kept in today's order, every start counted through one seam under the existing gate, its depth and starts generated from the graph, leaving the page or closing its window stopping its git and the runners waiting for each exit, a test comparing the graph with today's chain branch by branch. Part two, proposed and read later: round 3's text, everything that keeps or reuses an answer across opens, its open questions listed | D202; GITAPP1a-i to 1a-iii, GITAPP1 part two |
| `2026-10-23-git-writes-design.md` | Proposed (D204), not yet read: the person's git acts and writes split from GITAPP1 in its round 3, round 2's text unchanged (the checkout lock, ref and remote acts, history edits, stash, staging, the switch, D198's prerequisites for a push), with Codex's round-2 findings 11-16 as its open questions | D204; GITAPP2 |
| `2026-10-23-git-app-reads.md` | Reads (GITAPP1, under D191): Codex's read and Fable's foundation read of the design's round 1, and Codex's reads of rounds 2, 3 and 4, verbatim; the design answers them in §16 to §19 | D202; GITAPP1 |
| `2026-10-23-actors-brief.md` | Brief (MODELROLE1's co-design under D191, phase 1): actors for every kind of agent Daoris starts, each an ordered range of agent, model and effort; today by `file:line`, every agent start, the constraints, how other tools pick models per task, and the questions each proposal answers | D24, D98, D155, D163, D174, D197, D199, D201; MODELROLE1 |
| `2026-10-23-actors-proposals.md` | Proposals (MODELROLE1, under D191): each maker's independent proposal from the brief, sealed before it read another, added verbatim; the synthesis is written apart | MODELROLE1 |
| `2026-10-23-actors-reads.md` | Reads (MODELROLE1, under D191): Codex's read and the independent Opus reader's foundation read of rounds 1 to 3, verbatim; the design's §16.1 to §16.3 answer each finding | D205; MODELROLE1 |
| `2026-10-23-actors-design.md` | Proposed (D205), round 4, the synthesis of the three proposals with the reads of rounds 1 to 3 answered (§16.1 to §16.3): one effective order per actor, offers and refusals per account, one count per start, the paths after the open for every kind of start (§3.5) with a conversation going on to its actor's next choice when its agent has no account left, every Daoris door the person's press; eight actors (Build, Intake, Second opinion, Helper, Partner, Brainstorm, Ask Daoris, Chat; *actor* / 角色), each one to four ordered choices of agent, model and effort in the tool's own words; a start walks each choice's accounts by D197's walk before the next, carries the model at the session level (`set_config_option`; `--model`, `--effort`) and says the actor, the choice and what the agent reported; the rules still say who may; nothing spends differently until the person presses a suggestion held to `model-roles.md`; Ask Daoris proposes, the person's press applies in both modes; the rows, the first measuring what each door accepts | D205; MODELROLE1a-h proposed |
| `2026-10-23-limit-resume-design.md` | Accepted (D206, 2026-10-24), round 3: work cut off by an account's limit resumes by itself, never a strike. A limited line resumes its own conversation at its own account's reset, else goes briefed to another account or choice, else waits showing the reset; conversations, other kinds of run and an update's close follow. §8 answers round 1's read, §9 round 2's two; rows a-j with proofs | D206; LIMITRESUME1c built 2026-10-24, the other rows open |
| `2026-10-23-limit-resume-reads.md` | Reads (LIMITRESUME1, under D191): the independent Opus reader's foundation reads of rounds 1 and 2 and Codex's read of round 2, verbatim; the design's §8 and §9 answer each finding | D206; LIMITRESUME1 |
| `2026-10-24-agent-errors-design.md` | Accepted (D210, 2026-10-24, the steward under D191; Codex's read owed), round 3, answering round 1's reads in its §14 and round 2's in its §15, by Codex (another round) and the Opus foundation reader (accept after named changes), with round 3's read's named changes placed in its §16: every agent's errors read at the agent interface into four kinds (limit, access, transient, fatal) by each adapter's own codes and recorded sentences, its own table and then its owner's; a transient error tried again in the same conversation, inside the protocol door's run, five tries after 1, 2, 4, 8 and 16 minutes of waits, a word once on the wire never sent again whatever the run's end, said once, never a strike, park, cool-off or walk; past the bound the planner holds the quest for a new word of the person's or Try again, released for that end alone; D206's limit unchanged, a limit's code with the unstated reset; access to the person; fatal fails with its reason; each kind of run within its minutes; rows a, a2, b-g | D210; AGENTERRORS1 |
| `2026-10-24-agent-errors-reads.md` | Reads (AGENTERRORS1, under D191): Codex's read and the independent Opus reader's foundation reads of rounds 1 and 2 and the Opus read of round 3, verbatim; Codex another round each time it read, Opus accept after named changes (round 3: 11, 1 blocking), Codex's read of round 3 owed | D210 |
| `2026-10-24-supervisor-design.md` | Accepted (D208, 2026-10-24, the steward after round 5), the final round: the Opus foundation reader's named changes to round 4 made for the steward to check (the window starts the holder's terminal with the key, the holder session outside every repository with no project configuration, an HMAC proof so the key never crosses the pipe), Codex's read of the final text owed when its credits return; round 4 answering both makers' reads of round 3 (Codex: another round; the Opus foundation reader: accept after named changes) as one lifecycle: the live decision whole in memory, a file that differs stale; the release owning its word by a receipt; admission by a single-use ticket, a failed hand restoring the park's queue; a revoke's mutation then its sweep; every owner door superseding; the sentences following who answered; the key's hash in memory only, the appointment lasting until Daoris restarts, the key stripped from every start, sent only to the install's pipe and held by a dedicated session that does only the role. Roles the owner appoints in the window, each act recorded as the role's and overruled by them; the supervisor (主管) as the first, per repository, answering a parked session's recorded question with one offered choice and sourced reasons, held five minutes for the owner's overrule and then released as the supervisor's word, never the owner's and never a card, its bounds what is the owner's, what it reads the asking agent's claims; and the question card (QUESTIONUI1), each choice a radio row read whole with the recommendation's why, sent with the box's own field | D208; SUPERVISOR1a-i, 1a-ii, 1a-iii, 1b-f, QUESTIONUI1a-b |
| `2026-10-24-steward-role-design.md` | Accepted (D209, 2026-10-24, the owner's decision; Codex's read owed), round 3, answering round 1's and round 2's reads in its §12.1-§12.2 and taking the named changes of round 3's Opus read (accept after named changes; Codex's owed) in §12.3: the steward as a role the owner appoints once in the window, on SUPERVISOR1a-ii's door as built, a later role sharing the live holder or its own; its holder makes development's management acts with no card (drive and undrive, trees on, hold and resume, stop, finish, *Try again* on its own stop, hand-over, one fixed go-on, a set-up the owner ticked), each in an exact form with a record of its own naming the steward, said in one line per role and overruled by the owner, whose press on a repository or quest makes it theirs for the run whatever the route; never what is the owner's, closing a quest as done included; D201's presses become its acts; free words left to their own row and read; the holder the steward's management desk, its working session holding no key and asking through the desk; Daoris starting the holder's pinned harness itself; one go-on per quest per run; no drive without session trees | D209; STEWARDROLE1a-i to 1g |
| `2026-10-24-steward-role-reads.md` | Reads (STEWARDROLE1, under D191): Codex's read and the independent Opus reader's foundation reads of rounds 1 and 2 and the Opus read of round 3, verbatim; Codex another round each time it read, Opus accept after named changes (round 3: 14, 4 blocking), Codex's read of round 3 owed | D209 |
| `2026-10-24-supervisor-reads.md` | Reads (SUPERVISOR1, QUESTIONUI1, under D191): Codex's and the independent Opus reader's foundation reads of rounds 1 to 4, verbatim; Codex asks another round each time it read (round 3: 12, 7 blocking; none for round 4, out of credits), Opus accepts after named changes from round 2 (round 4: 7, 3 blocking) | D208 |
| `2026-10-24-keep-size-design.md` | Accepted (2026-10-24, the steward under D191: round 3, answering every finding of round 2's reads in its §10, with the named changes of round 3's Opus read placed in its §11; Codex's read owed, before KEEPSIZE1b merges), a note amending D195: the install measured by kind (the set-aside folders grow about 27 GB a day); a finished tree kept as its text record in the knowledge system, searchable, and its tree let go an hour after its branch is deleted and its record kept, its tip on a ref, its own scratch with it (the owner's decision); one eligibility judgement and a recovery protocol for every removal; an allowance the person sets (48 GB by default), with parts and a stopping rule, taking only recorded folders; the press's bounds; the floor a warning only; rows KEEPSIZE1a-f, no backfill | D195's KEEPSIZE1 notes; KEEPSIZE1a built, the rest after D191's reads |
| `2026-10-24-keep-size-reads.md` | Reads (KEEPSIZE1, under D191): Codex's read and the independent Opus reader's foundation read of round 2 and the Opus read of round 3, verbatim; round 2: Codex another round (14, 11 blocking), Opus accept after named changes (22, 4 blocking); round 3: Opus accept after named changes (11, none blocking), Codex's read owed | D195 |
| `2026-10-24-confirm-wait-design.md` | Accepted (2026-10-24, the steward under D191, round 2 with its named changes, a note amending D156; Codex's read owed): a terminal's press for the person's Confirm waits until they answer, with no two minutes and no ten; a record named by the confirmation's own id in the home's `data/pipe-waiting`, never holding a key, re-judged at a restart on the home's lease, a card in Notifications; the id said at once, the verb waiting at most thirty minutes, its ending read by `result <id>` and withdrawn by `withdraw <id>`, which never touches a confirmed press; every person route's grounds and readers in one census table, each plan ground's fields named, a press whose grounds moved refused with its reason; each press its own card, ten at most; round 1's 25 findings answered and round 2's 20 placed in its §14 | D156; CONFIRMWAIT1a-i, 1a-ii, 1b |
| `2026-10-24-confirm-wait-reads.md` | Reads (CONFIRMWAIT1, under D191): the independent Opus reader's foundation reads of rounds 1 and 2, verbatim; round 1 another round (25, 7 blocking), answered in round 2's table; round 2 accept after named changes (20, 2 blocking), placed in the design's §14; Codex's read owed | D156 |
| `2026-10-24-cowork-view-design.md` | Proposed (COWORKVIEW1, 2026-10-24, round 2 under D191, notes amending D173, D203, D115 and D167; round 2 answers the Opus foundation read's 19 findings in §11, Codex's read owed): who is at work on a session or an ask as one read of the Fabric's records, never a store or the conversation's text (the entry's parts once each, a lane quest to its own repository being a further part; the lead's subagents by their lineage with a bounded label, parent, end word and moments; co-workers; quests from their units with their takers, waits and the driver's reason; the ask's channel); a side bar view *At work* (分工), `SESSION_AT_WORK` told through a `work` scope under NOBLOCK1, at 300, 430 and 680 px; *last heard* from the window's streams, giving way to *quiet*; the routing half: what each kind of session does after publishing, the person's drive, trees and cap as the rule, three hints where the way is not open; rows a-f, the first *a chat's subagents seen* | D173, D203, D115, D167; COWORKVIEW1 |
| `2026-10-24-chat-flow-design.md` | Accepted (2026-10-24, the steward under D191, round 2 with its named changes, notes amending D157 and D155; amended by Codex's owed read, its §17, D157's amendment note): a conversation's work in a tree of its own runs its repository's workflow; its run keyed `chat-<first>` by the line the record stores, bound when its tree opens to Current or a steps version only, and cleared by the session delete of the record its key names; a gate that refuses the person's Accept with its reason and asks the second opinion at that press, the end of a person's turn starting nothing; a look it cannot have set up read `cannot-start`, with the person's way past it; a send-back reaching the conversation only between turns as an ordinary word taken atomically, the person's words first, and D155's automatic recheck kept; round 2's read's named changes taken (its §16); Codex's read taken (its §17): the press reserving the turns and landing the tip it judged, the send-back's reservation in the dispatcher's keep with the person's turn count, the answering turn's frozen reading, an ended chat's go-on, the concrete work resolved before a line's origin, a candidate's readiness for the later steps, and a graph-bound chat run's candidate and lifecycle; a second opinion reading its candidate and opening, never its conversation; checks told only once enforced; the landing and production acts the person's; Ask Daoris, an intake and a checkout chat unchanged; the no-run answer carrying a code worded in en and zh and said in English on the terminal | D157, D155; CHATFLOW1a-i |
| `2026-10-24-cowork-view-reads.md` | Reads (COWORKVIEW1, under D191): the independent Opus reader's foundation reads of rounds 1 and 2, verbatim; round 1 another round (19, 10 blocking), round 2 accept after named changes (13, 3 blocking); Codex's read owed | D173 |
| `2026-10-24-chat-flow-reads.md` | Reads (CHATFLOW1, under D191): the independent Opus reader's foundation reads of rounds 1 and 2, verbatim; round 1 another round (24, 8 blocking), answered in round 2's table; round 2 accept after named changes (11, 2 blocking); Codex's read of the accepted text, reopen (12, 8 blocking), taken in the design's §17; an Opus check of that amendment (12, 1 blocking row c), taken in the design's §18 | D157 |
| `2026-10-24-event-hook-design.md` | Proposed (2026-10-24, round 2 under D191, answering the Opus foundation read's 16 findings in its §12 and taking round 2's read's named changes in its §13, Codex's read owed; notes amending D173, D119 and D120): the work cycle's events pushed to an outside agent's session by a plugin, never polled; one observation point `work/event`, told by the lease's loop, whose kinds are the attention watch's park, quest park and ending, `work.landed` from the install's landing, then the quest kinds and `person.answered`, carrying ids and codes only under one grammar, told in order per plugin with a run and a sequence and bounded; the plugin connecting to subscribers; Daoris's own `daoris-driver events relay`, run from a copy outside `app/`, holding no Daoris key and no lease, serving a user-owned pipe, refusing strangers, posting fixed words over minted ids and never a name, `--once` first and its Claude Code inbox the upgrade; the inbox's variables removed from every child; the knowledge server's channel second and opt-in; what Claude Code and the pipe do measured on this machine first | D173, D119, D120; EVENTHOOK1a-f |
| `2026-10-24-event-hook-reads.md` | Reads (EVENTHOOK1, under D191): the independent Opus reader's foundation reads of rounds 1 and 2, verbatim; round 1 another round (16, 7 blocking), round 2 accept after named changes (12, 2 blocking); Codex's read owed, the design not accepted on one maker's read | D173 |
| `2026-10-24-switch-context-design.md` | Proposed (round 3, under D191; the Opus reader's read of round 2 taken in its §9; Codex's read owed, not accepted on one maker's read; notes amending D125, D131, D137, D165, D167, D189, D197 and D206): a move keeps the agent's own context and a session reads as one conversation whatever runs it. The same agent between two of Daoris's accounts carries its conversation's own files, by a table per agent and pinned version and never credentials, another's or the person's own tool home, into the next account's home once the old process exited, never over turns another client ran, and loads it there through the start itself, counted only once the replay holds the agent's own messages by the `messageId` read from the pinned adapter's package, on the model and effort it last ran; nothing prompted at a curve move; the brief the fallback on the same process with today's choice, at the open or a first turn ending on the provider's refusal or a fatal failure, said; every decided rule it amends named, D131's answer given; measured on the install's next real carried move, no turn spent; another agent goes on from the brief, said at its boundary; the page draws a line's earlier parts above the attended one, split out as SWITCHCTX1c-i; rows with proofs; round 1's 12 findings answered in its §8, round 2's 10 in its §9 | D165; SWITCHCTX1 |
| `2026-10-24-switch-context-reads.md` | Reads (SWITCHCTX1, under D191): the independent Opus reader's foundation reads of rounds 1 and 2, verbatim; round 1 another round (12, 4 blocking), answered by round 2's §8; round 2 accept after named changes (10, 3 blocking); Codex's read owed, the design not accepted on one maker's read | D125 |

## UI, browser, tools and plugin contracts

| Document | Kind and purpose | Governing record |
|---|---|---|
| `2026-09-19-platform-design.md` | Contract: shared platform | D38/D55/D66/D150/D152 |
| `2026-09-19-platform-ux.md` | Contract: design language; read for UI changes | D41/D56/D141/D150/D152 |
| `2026-09-19-frontend-architecture.md` | Method: stack and test boundaries | D42/D142 |
| `2026-09-21-working-surface-design.md` | Contract: session-oriented working surface | D51/D52/D55/D66/D113 |
| `2026-09-21-working-surface-components.md` | Method: stories and hook-free molecules | D55 |
| `2026-09-21-desktop-frame-design.md` | Contract: window and frame | D56/D66/D75/D118/D152 |
| `2026-09-23-plugin-design.md` | Contract: declarations, wire and plugin kit | D64/D71/D101/D103/D140/D145/D146 |
| `2026-09-24-menus-design.md` | Contract: original domain menus and workspace naming | D75; D152 replaces menu layout |
| `2026-09-27-in-app-browser-design.md` | Contract: browser seam and agent tools | D78/D84/D85/D99 |
| `2026-09-28-chromium-host-design.md` | Contract: shipped Chromium | D85/D92/D93/D99 |
| `2026-09-29-ask-daoris-design.md` | Contract: help conversations and proposals | D89/D110/D158 |
| `2026-09-29-dock-design.md` | Contract: movable panels and Quick Ask | D118 amendments; §4 records build |
| `2026-09-30-setup-guide-design.md` | Contract: first-start guidance | D97/D150 |
| `2026-10-01-naming-design.md` | Contract: bilingual names and glossary | D116 |
| `2026-10-01-frame-model-design.md` | Contract: each view's list and main panes | D118/D150/D152 |
| `2026-10-01-plugins-screen-design.md` | Contract: plugin pages and actions | D119/D140 |
| `2026-10-01-tools-design.md` | Contract: system, named and managed tools | D121 |
| `2026-10-01-plugin-distribution-design.md` | Contract: workshop and packages | D120 |
| `2026-10-03-context-menu-design.md` | Contract: context actions and engine boundary | D138 |
| `2026-10-03-plugin-catalogue-design.md` | Contract: offers, icons and monograms | D140 |
| `2026-10-03-language-design.md` | Contract: coded notes and session language | D142 |
| `2026-10-04-plugin-hooks-design.md` | Contract: hook occasions and PR state | D148 |
| `2026-10-04-ux6-redesign.md` | Contract: settings on their things | D150/D152 |
| `2026-10-05-ux7-design.md` | Contract: menus, accounts and page heads | D152 |
| `2026-10-16-this-daoris-design.md` | Contract: Settings → This Daoris, every wire and its source, every setting found, the wiring set up | D171's WIRING1a note; WIRING rows in TASKS |
| `2026-10-20-flowchart-design.md` | Contract (accepted 2026-10-20): processes drawn as flowcharts: one auto-laid layered diagram, each flow from its record and where it is mounted, the rail as its narrow placement, the layout engine chosen on measured fixtures (hand-laid or dagre); the Map stays the map of requests | D186; FLOWCHART1 and MAPUX1 rows proposed |
| `2026-10-20-ui-ux-design.md` | Contract (accepted 2026-10-20): every screen since UX7 audited; the wide-pane, waiting-step and refusal rules; meetings, brainstorms, sign-in, access, heavy runs and work state designed; the shared parts; one meaning for a badge, what waits on you, running work a mark (§3.10, 2026-10-22) | D185; UIFIT1b, CLAUDESIGNIN1b, MEETUX1 and the feature rows in its §9; BADGEUX1 |
| `2026-10-21-notification-design.md` | Accepted (2026-10-21, after three rounds): everything that comes to the person in one view the title bar opens; a terminal's press there in plain words, never across the page, its signal kept while unshown; Overview a summary; presentation first, then a notice record, then channel summaries; arrivals said by identity | D196; NOTIFY2a-j |
| `2026-10-22-park-ask-design.md` | Settled and built (2026-10-22, after Codex's read): a parked session's ask at its conversation's end, its question recorded by its agent (`question_ask`) and bound to its park by the driver, drawn in its box with every answer a press through the box's existing doors | D185's, D135's and D168's notes; PARKVIEW1 |

## Studies and evidence

These retain their original dates, versions and measurement limits.

| Document | Kind and subject | Used by |
|---|---|---|
| `2026-09-20-working-surface-research.md` | Study: working-surface references | SURF1 |
| `2026-09-21-dsh-evaluation.md` | Evidence: protocol evaluation | D53 |
| `2026-09-21-ide-reference-study.md` | Study: IDE reference | D55 |
| `2026-09-22-acp3-probe-evidence.md` | Evidence: ACP postures | D53/ACP3 |
| `2026-09-22-first-deployment-case-study.md` | Evidence: deployment defects; read for desktop | D60/D63 |
| `2026-09-22-plugin-design-study.md` | Study: extension seams | D64 |
| `2026-09-24-agt2b-channel-evidence.md` | Evidence: vendor channels | D67 |
| `2026-09-24-deploy1-acp-trust-evidence.md` | Evidence: folder trust | D73 |
| `2026-09-24-reference-gap-study.md` | Study: conversation gaps | D76 |
| `2026-09-25-stream-json-evidence.md` | Evidence: native streams | D76 |
| `2026-09-25-message-content-evidence.md` | Evidence: message content | D76 |
| `2026-09-27-first-goal-study.md` | Study: first real workspace | D77 |
| `2026-09-28-console2-streams-evidence.md` | Evidence: ACP background streams | D83/D105 |
| `2026-09-30-console3-native-streams-evidence.md` | Evidence: native background streams | CONSOLE3c |
| `2026-09-28-after-the-first-workspace.md` | Study: workspace, sessions and help gaps | D86–D88/D100/D102 |
| `2026-09-28-managed-edge-evidence.md` | Evidence: managed Edge profiles | D84 |
| `2026-09-28-chromium-embedding-evidence.md` | Evidence: embedding, sign-in and debug port | D85 |
| `2026-10-01-entry-point-evidence.md` | Evidence: instructions read by harnesses | D117; canary remains owner-only |
| `2026-10-01-tools-resources-evidence.md` | Evidence: resource lists and digests | D121 |
| `2026-10-02-knowledge-bench-results.md` | Evidence: headless discovery bench | D128/D129 |
| `2026-10-02-limit-signals-evidence.md` | Evidence: account windows by door | D125/D130 |
| `2026-10-02-ask-drift-evidence.md` | Evidence: requirements lost across runs | D133 |
| `2026-10-03-steer-evidence.md` | Evidence: live steering boundaries | D136 |
| `2026-10-03-knowledge-use-evidence.md` | Evidence: questions and owner-only answers | D135 |
| `DAORIS_UNIFIED_COLLABORATION_ARCHITECTURE_AND_PLAN_2026-10-16.md` | Proposal: unified collaboration plan (ADR-A to F asked of the owner, §12; Appendix A holds the 2026-10-03 outside suggestions), preserved | Future-directions review; no implicit adoption |
| `2026-10-03-future-directions-review.md` | Study: proposal weighed against record | ROADMAP horizon |
| `2026-10-05-integration-review.md` | Evidence: earlier recovery and integration | Archive receipts; remaining proof in TASKS |
| `2026-10-07-second-opinion-review.md` | Evidence: code/UI findings | Archive outcomes; remaining work in TASKS |
| `2026-10-07-codex-usage-evidence.md` | Evidence: own-sign-in account windows | D125/D130 |
| `2026-10-09-review-serving-evidence.md` | Evidence: Chromium serving behavior | D154 |
| `2026-10-10-subagent-load-evidence.md` | Evidence: what dispatched subagents' context cost | D160; the trial in TASKS |
| `2026-10-11-entry1-audit.md` | Evidence: what Ask Daoris can start, against what a person starts | D161 §3; ENTRY1 rows in TASKS |
| `2026-10-12-entry-and-heads-design.md` | Contract: New conversation from a prompt; bounded heads | D162; NEW1 and HEAD2 rows in TASKS |
| `2026-10-12-mixed-agents-design.md` | Contract: a task handed to another agent; helpers on other agents; the view of who worked | D163; MIXAGENT rows in TASKS |
| `2026-10-12-account-switch-design.md` | Contract: each account's real use; the switching curve; a running session moved at its turn's end | D165; USAGEREAD and SWITCH rows in TASKS |
| `2026-10-12-usage-read-evidence.md` | Evidence: an account's plan usage read with no prompt | D165 point 1 |
| `2026-10-12-code-structure-design.md` | Contract: domain modules, a Domain for each one's model and rules (amended 2026-10-14), the structure gate and the moves | D166; STRUCT and FLAT rows in TASKS |
| `2026-10-14-duplication-survey.md` | Evidence: duplicated code across the .NET modules, by cluster, with its drift | D166's note of 2026-10-14; DEDUP rows in TASKS |
| `2026-10-14-partial-types-survey.md` | Evidence: the types split by hand across files, by shape, and the gate that holds them | D166's partial-type note; PART rows in TASKS |
| `2026-10-16-fabric-contract-audit.md` | Evidence: each collaboration concept's store, writer and fabric primitive; Claude Code's and Codex's native children measured against a server | D173's FABRIC-C0 note; FABRIC rows in TASKS |
| `2026-10-23-model-effort-doors-evidence.md` | Evidence: what each door accepts for model and effort, its refusals and what a turn reports | D205's MODELROLE1a note; MODELROLE1's later rows |
| `2026-10-24-modelrole1b-build.md` | MODELROLE1b's build checkpoints, readings, focused verification and installed-window expectations | D205 row b |
| `2026-10-24-modelrole1c-build.md` | MODELROLE1c's parts, which are built and which remain, the focused verification and the readings | D205 row c |
| `2026-10-24-modelrole1d-build.md` | MODELROLE1d's parts, which are built and which remain, the focused verification and the readings | D205 row d |

## Records and generated indexes

Read records by identifier; do not read a whole log to find one entry.

| Document | Job and standing |
|---|---|
| `../TASKS.md` | Open work, dependencies, contract and proof |
| `../CHANGELOG.md` | Release-facing changes; workflow stamps headings |
| `DECISIONS.md` | Pointer to decisions; append none here |
| `decisions/` | Numbered decisions and dated amendments, one file each |
| `task-archive.md` | Completed task wording, date and outcome |
| `FIX-LOG.md` | Defect cause, repair and verification |
| `2026-09-25-rev3-review.md` | REV3/CLEAN1 ledger |
| `2026-09-26-ux5-screen-audit.md` | UX5 surface and state ledger |
| `2026-09-28-sess1-session-view.md` | SESS1 session-view ledger |
| `2026-09-29-sess2-session-head.md` | SESS2 header ledger |
| `2026-10-01-naming-audit.md` | NAME1 bilingual naming ledger |
| `2026-10-01-frame-audit.md` | FRAME1 layout ledger |
| `2026-10-10-integration-review.md` | Recovery, review and gate receipts |
| `2026-10-10-documentation-review.md` | DOCSYS1/DOCSYS2 plans, drift findings and verification |
| `2026-10-10-code-and-docs-review.md` | MAINT1 review, refactoring findings and verification |
| `adoption/` | Local mechanics draft; adoption has not run |
| `code-map.json` | Generated by devkit `map`; checked by `map --check` |
| `index/` | Generated routes, verbs, catalogues, fixtures, outlines and decision digest |

`node tools/orient-index.mjs` regenerates `index/`; `--check` fails on stale output. The generated
index links hand-kept indexes and does not restate their rows. `.claude/INDEX.md` is generated by
doctrine sync and lists on-demand knowledge and skills. `node tools/doc-system.mjs` checks this
router's coverage; semantic claims still require a source review.
