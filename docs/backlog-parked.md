# Daoris (道衍) — parked work

Open rows set aside on 2026-10-30 and triaged on 2026-11-02 against D228 (`docs/2026-11-02-parked-triage.md`): the rows that build the structure or remove a second way went back into `TASKS.md` (inside the chunks that carry them, or as defects), the superseded ones to the archive, and the rest stay here, grouped by the condition that brings each back. To take a row up, move it to `TASKS.md`; a parked row finished meanwhile moves to the archive. Triaged again on 2026-11-11 against the code and the decisions as they stood: the closed rows are in the archive under *Parked rows closed in the triage of 2026-11-11*, eight went back to `TASKS.md`, and a condition that had gone stale is corrected in place.

## The next installed sitting, on the build staged from SCOPEVERIFY1's green run

Looks that no later design, and not the devtools' review of every screen (UIREVIEW1), has replaced.

- [ ] **UX6i2i — installed go cards** (parent; after republish). Ask Daoris answering "where do I turn a plugin on?" and "where is Convergence?": a go card reading *Open Plugins.* or *Open Knowledge → Convergence.* that opens the right place and mode.

- [ ] **OWNSPACE1e — on the install, no new session in the person's home** (parent; after the stage carrying OWNSPACE1a). A new conversation's and a driven session's transcript land under Daoris's profiles, and none new in the person's own tool home. Proof: the folders before and after, counted.

- [ ] **THEMESTART1w — the first frame on the install** (parent; after republish). With the OS light and Dark chosen, restart: the frame, border, caption and splash dark from the first frame, and Monitor's caption with it; the opposite way too; System follows the OS. Read `matchMedia('(prefers-color-scheme: dark)')` then for THEMESCHEME1b. The splash's lift moved with STARTPERF1 (What needs you loads after the first screen) and is seen in its after-measurement. D22's THEMESTART1 note, D56's SPLASHDATA1 note.

- [ ] **STEER2e — *Send now* on the installed window** (parent; after the stage carrying STEER2a/b). A conversation on Claude Code: words typed while it works, *Send now*, the step cut and the words taken at once; a queued word then *Send now* past it; a Codex conversation's *Send now* stopping and keeping; en and zh. Proof: the window, seen.

## A release of the LLM library, or the owner's key for a first provider

OPENROUTER1 is the first provider added to the contract, with the owner's key.

Owner direction, 2026-10-27: an agent-related feature built here (providers, adapters, agent sessions, tool calling, MCP) is weighed for Lyntai, the family's LLM library, too. What Lyntai should support is requested from it as a quest stating the general need and the evidence, never edited from here; AGENTINFRA1 is the first such feature. The parent drives Lyntai's quests through the owner's install, and Lyntai's release goes through GitHub in the owner's browser once Daoris's git identities (GITAUTH1) are ready and the owner has set up an SSH key. Lyntai's memory system is tested against this repository's tracked records, a corpus far larger than its own, by Lyntai's memory lane; no private store or transcript of this machine is handed to it. Daoris's agent-provider layer (one provider contract, a turn's event stream, account pools, one error policy, context that travels) is offered to Lyntai as a reusable package for other applications, orchestration staying here; Lyntai's steward designs it beside its vNext roadmap, and whether Daoris then consumes that package is a later decision of this repository's. Ahead of an official release, Daoris may consume a Lyntai package from Lyntai's local publish feed.

- [ ] **AGENTINFRA1 — providers across local and remote infrastructure** (owner, 2026-10-27; D213 amendment). Build the accepted `docs/archive/2026-10-27-provider-infrastructure-design.md` after mod-ix through AGENTPROVIDER1, on the layer AGENTLYNTAI1's accepted design places (D213's notes of 2026-10-29 and 2026-10-30), its stage B waiting on Lyntai's R-B release (LQ11, LQ12; that design's §5.2, §9.4); §11 records the scoped owner review exception. Proof: independent dialects, scoped limits, uncertain delivery, remote cancellation and older-driver admission; the three Codex defects remain regression inputs.

- [ ] **OPENROUTER1 — OpenRouter as a provider, its models by role, the free tier where a role's result holds** (design under D191, then rows; the owner's direction, 2026-10-24: a provider with free-tier models, and not only Claude or GPT models). OpenRouter is an aggregator of many model families (Claude, GPT, and open families such as Llama, Qwen, DeepSeek, Gemma and Mistral) behind one key. It appears in the records only as routing's prior art (`docs/archive/2026-10-02-account-use-design.md`, `docs/archive/2026-10-23-actors-brief.md`). Read 2026-10-24, to be confirmed against its own documentation before anything depends on it: Claude Code talks to it directly with `ANTHROPIC_BASE_URL` at `https://openrouter.ai/api`, the key in `ANTHROPIC_AUTH_TOKEN` and `ANTHROPIC_API_KEY` empty, its Opus, Sonnet and Haiku slots pointable at other models, though tool use is said to be reliable only on Anthropic's own; free (`:free`) models are capped at 20 requests a minute and 50 a day, 1,000 a day once about ten dollars of credit has ever been bought, are throttled at peak and rotate out without warning, and a free provider may log or train on what it is sent. The design: an OpenRouter account kind (a key the person enters in the window, kept in `profiles/` as D172 point 3 keeps keys, never in a tracked file or another session's environment), its models offered in D205's actors' choices as agent plus model, the ordered fallbacks and limits read into AGENTERRORS1's kinds and D125's cool-off (a daily cap a limit); and the co-working fit by the tier rule (`docs/model-roles.md`): a free or cheap model where a role's result is measured to hold, a brainstorm's extra voices (D176), a helper's read-only question, a first orientation pack (MODELROLE1j), an intake's first sort, a draft second opinion beside a real one (never the only one, D155), and never a build, the steward's or the supervisor's role, or any repository the person has not opened to it, a private one and the work workspace closed by default. Codex's own provider settings for OpenRouter are unread. Proof: the measurement row first, with the person's own key and free models only, an orientation pack and a brainstorm voice compared with Sonnet 5.5's on the same input; then a stand-in provider's account, its limit read as a limit, and a closed repository refused.

## The owner: a decision, a grant, a spend, a sign-in or a publication

The last four (KITREQ1, FRAME2b, THEMESCHEME1b, THEMEBROWSER1) wait on another owner than this repository's, the UI kit's. PLUGDIST1b and f are frozen with the plugin workshop and NuGet source (D233 point 3). A spend on a proof is granted once and small (D240 point 7), so a row here waits on the owner's act, not the spend.

- [ ] **PROOFSCRATCH1 — remove the generated startup-proof binaries** (cleanup held, 2026-10-26; owner-authorized retry rejected again 2026-10-27). The native cleanup command for two ignored fixture apps was rejected before execution as `blocked by policy`. Preserve reports and logs; remove only after the controlling permission changes and the guide's boundary, reparse and process checks pass. Exact targets remain in local recovery notes.

- [ ] **DOCSCRATCH1 — remove blocked verification scratch** (command review). Deletion of ignored `_fixtures/doc-system-preview-git/` metadata remains rejected after explicit owner authorization and repository-local defaults. Inspect the exact target, then native removal when permitted; proof: absence. Restriction and retry receipt: `docs/archive/2026-10-10-code-and-docs-review.md`.

- [ ] **WORKSPACEPILOT1 — the owner's own workspace of repositories set up through Daoris, a pilot first** (the parent with the owner; D124 §4, D77; the owner asked on 2026-10-19 that the family's workspace be run through the local Daoris). The install now holds a workspace of the owner's twenty repositories (Daoris and its plugins adopted; the rest imported only; the names are in the private notes). Adoption writes into each repository and spends the owner's accounts, so each is the owner's opt-in: driving on, trees of its own, and where its set-up lands (merged into its line, or a branch). After the next install, `setup <repository> --plan` for a pilot the owner names (a quiet repository: some have the owner's own sessions working in them), then the press, then the rest one at a time as WSSETUP6 paces it. Proof: the pilot's set-up landed and reviewed; the plan for the rest shown to the owner; anything the pilot finds wrong in the flow filed as rows.

- [ ] **SIGNIN1b — a Codex and a Claude sign-in proved on the install, with the owner signing in** (the parent with the owner; after the republish carrying SIGNIN1; D125's SIGNIN1 note). SIGNIN1 built the phases, the late panel and the log; whether Codex's device-code sign-in now completes on the owner's install, and how long Claude's takes, is shown only by a real sign-in, which is the owner's. Proof: each step shot and timed on the install, the machine log's `harness.*` lines for both, and the usual times written into the UI/UX design's `SIGNINLOG1` place.

- [ ] **ACCTPLAN1 — plan, spend and reset credits** (driver/modules/web; design first). Nothing reads them yet (D125:898-899); show them in a fold, and never redeem a credit without `InlineConfirm`. Second-opinion review lines 132-133.

- [ ] **USAGECAP1a1b — recorded frames for the readings the history parses from the evidence alone** (driver tests; D179 point 2, the usage-read evidence's field table; the spend is granted, D240 point 7, so it waits on an account that answers such a frame). A `get_usage` answer holding `limits[]` rows and a scoped week, and a Codex answer with a second limit (`rateLimitsByLimitId`), recorded from real answers on the owner's accounts so each parse grows only with a recorded frame, as the table's rule asks. Proof: `ClaudeUsage.Question.Recorded` and `CodexUsage.Question.Recorded` gain the answers, and the history's `Seen` tests run on them.

- [ ] **CODEXUSE2 — installed Codex reading** (parent/owner). Pins are verified; account sign-in remains unavailable. Use CODEXUSE3's own-sign-in reading meanwhile. D57/D63/D125.

- [ ] **CODEXKEY1 — Codex API-key account** (measure first; owner supplies throwaway key; APIKEYACCT1 built the key accounts for dsh). Determine spawn variable for both tools before declaring support. D67/D125; real variable-start proof.

- [ ] **QUESTBACK1 — hand back without a session** (owner's decision first; then service → driver + web). No hand back exists from anywhere: `QuestTransitions.Allows` lets nothing move back to open (D46 §3), and D126 ~:570-573 parks it. A new operation kind (quest-operations.md's eleven places), so a decision amending D46 §3 and D68 must say which taker facts survive (the claim, `Awaits`, set-ups, the ledger's carry-on, a remote's held claim). The label cannot be *Send back…* (DiffPane's); *Hand back…*/交还… to check against D116. QUESTCLOSE1b1, which shares its files, is built.

- [ ] **XAGENT1k — canon second reader** (canon/examples; parent's decision). §10 doctrine, resync and canon changelog. Verify/example rehearsal.

- [ ] **REVIEWENV1i — canon see-it-run** (canon/examples; parent's decision). §7 doctrine, resync and changelog. Verify/example rehearsal.

- [ ] **COST1 — measure long-turn cost** (owner's decision). Week of context/cache evidence before ceiling/window/compaction proposals. METER1/D127.

- [ ] **KNOW2a — recall probe** (meaning judged by owner). Paraphrases at recall 3/5. Review §2.G/§4.6; script/evidence.

- [ ] **KNOWUSE2b — all 46 questions** (owner chooses harness/account). Repository knowledge, no substituted owner answers. D135 §6; report before KNOWUSE3.

- [ ] **KNOWUSE3 — item-local review** (after KNOWUSE2b). Hint tier, never answer for owner. D135; stories/installed proof.

- [ ] **KNOWUSE4 — correction request** (owner may publish). Correct comparison and config-only contradiction; no cross-repository edit. Knowledge-use evidence §4.

- [ ] **WSSETUP13 — remaining repositories** (owner). Pilot-paced rollout, existing instructions and weekly parks. D124; original LAYOUT10 remainder.

- [ ] **WSSETUP14f — complete pilot** (owner; after republish). Two repositories, green checks, knowledge declaration, 32768-byte root and restored budget. Pilot §4.

- [ ] **LAYOUT2 — canary turns** (owner grant; frozen with the agents-layout migration, D233 point 3). Harness predictions/account flag from evidence §6; keyless half archived. D117.

- [ ] **BUDGET1 — budget scope** (owner decides). Align region-only behavior, README/design and analyze after decision. D59/REV3 F10.

- [ ] **TRUST2 — measured trust** (owner grant). Written-key honoring and parent/child trust. D73/evidence; exact-folder hold remains. Seen 2026-10-12 on the install: a driven session's transcript opened with *Ignoring 7 permissions.allow entries from .claude/settings.json: this workspace has not been trusted*.

- [ ] **TOOLS10 — managed-Git probe** (owner permits one start per agent; with the owner's answer to GITLIB1 §7, Daoris's own managed git as the default). D121 §7.

- [ ] **DEVDRIVE1 — the machine's cost of starting a program** (the owner's settings; never changed from here). Measured 2026-10-19 with no CI running: starting any program took 170-470 ms from PowerShell (`cmd /c exit`; `git --version` 550-720 ms) and 1.3-1.5 s through Git Bash, against about 10-30 ms normally; Windows Defender is not running, and two security products hook every program start (Tencent PC Manager's real-time protection `QQPCRTP`, and `AlibabaProtect`); a Microsoft Teams renderer used about three cores. Tests and builds start git and other programs thousands of times, so each start's cost multiplies. The owner's choices: an exclusion in Tencent PC Manager for this checkout and its tools (`git.exe`, `dotnet.exe`, `node.exe`), whether `AlibabaProtect` is needed, and Teams while working; the parent then measures a program's start and the git half before and after. On this repository's side: fewer program starts (PROCSPEED1b), and heavy loops started from PowerShell or Node rather than through Git Bash.

- [ ] **ONEINSTANCE2b — the installed proof of a second window** (the parent with the owner; after the next install; D171's ONEINSTANCE2 note). The parent's own repro launched the second copy from an agent's shell, whose token may not open the per-user mutex or pipe, which alone produces the symptom: the owner clicks `Daoris.exe` a second time, and the log is read (`window.launch` then `window.launched` `main:2`; or the guard's or the engine's line naming the path it took). Then MAINWINDOWS3's look: one window on the work workspace, `main:2` on `games`, each keeping its own; closing the first keeps the second.

- [ ] **PLUGDIST1b — pack/release workflow** (request to plugin owner). §7.

- [ ] **PLUGDIST1f — first publish** (owner). Account, trusted publishing and prefix. §7.

- [ ] **KITREQ1 — four requests to the UI kit's owner (Shenora, from v0.21.1; 0.22.0 and 0.24.0 answered none)** (the parent sends; the owner decides; never written into the kit from here). (1) `ChromiumEngine.Start` answers CEF's relaunch hand-over with a log line only: send it to the app's `OnActivated`, or expose a callback, so a launch the WinForms guard let through still reaches the app. (2) `ListenForLaterLaunches` ignores `ui.Post`'s result: a launch dropped while the form is not ready should be said. (3) `BringForward` and `OnActivated` share one posted body: run the app's callback on its own guard, so a failed bring-forward does not skip it. (4) `ChromiumView` raises no title event (`ChromiumChildBrowser.TitleChanged` is empty): expose the page's title so the frame can read it and Daoris's `SET_TITLE` route can go (MAINWINDOWS3b). Evidence: D171's ONEINSTANCE2 and MAINWINDOWS3 notes. Then ONEINSTANCE3: route the engine's relaunch to `LaterLaunch` once (1) ships.

- [ ] **FRAME2b — maximized restore under pointer** (owner sends request). Wait for runtime position support, then update handler. D56/D32; owner response and handler proof.

- [ ] **THEMESCHEME1b — the held scheme reaches the open page** (desktop; seen on the install 2026-10-15 at c65581d). With the viewer's choice dark and the OS light, the kit holds it (`SHENORA.COLOR_SCHEME` `GET` answers `dark`) but the running main window's `matchMedia('(prefers-color-scheme: dark)')` stays false: the engine's setting does not reach a renderer already open (THEMESCHEME1's one unproven step). Find whether a reload, a new window or the kit's own notification carries it; if the kit cannot, ask its owner (with THEMEBROWSER1). D22's THEMESCHEME1 note. Proof: the eval true after choosing dark, false after System, on the install.

- [ ] **THEMEBROWSER1 — Daoris's browser follows the chosen theme** (desktop; needs the kit). Its separate Chromium process has no colour-scheme option at Shenora 0.21.1, nor at 0.22.0 (`ChromiumBrowserProcessOptions`), so its own UI follows the OS whatever the viewer chose: ask the kit's owner for the option, then pass the choice at the browser's start. D22's THEMESCHEME1 note. Proof: a capture of the browser with the OS set opposite.

## Another row first

Several are re-cut onto the new structure when they come back. The review environment's rows (REVIEWENV1d2, e, g2) wait on REVIEWUX1's build: its settled design (D273, one review record per attempt) names no environment, so they are re-cut onto that record when they come back. The plugin distribution rows are frozen with the plugin workshop and NuGet source (D233 point 3), and the co-agent rows with partners' remaining rows.

- [ ] **FABRIC-COLLAB1 — the first cross-harness round trip** (service, driver; after FABRIC-C2b; the fabric events design's rows table (§E10), §D10). One message and one artifact by reference between a Claude Code and a Codex session.

- [ ] **FABRIC-DELEGATE1 — a delegated node** (service, driver; the fabric events design's rows table (§E10), §D10). A node delegated as a quest commitment another repository accepts and executes locally. Re-pointed (D215; integration design §18.3): the driver-originated request is built as INTEGRATE1p's `request` node (2026-10-31); a child's or another harness's delegation, and artifacts by reference, keep FABRIC-C2b (FABRIC-ACP1 and C2a are built), after COLLAB1.

- [ ] **FABRIC-WAITUNIT1 — waiting on other work as a node** (service, driver; the fabric events design's rows table (§E10), §E6). `wait.unit` through a commitment only, a distributed wait shown honestly. Re-pointed (D215): its adapter for driver-originated requests has INTEGRATE1n's waited start and INTEGRATE1p's `request` node to build on, both built; §E6's full commitments stay after COLLAB1.

- [ ] **WORKFLOW1i2 — the intake sets a kind that lowers nothing** (driver/service; design first). §4.3 lets an agent set a kind on quoted words where `Involvement.Lowers` is empty in every repository the work reaches; the service cannot judge that, so it needs an agent-facing driver door. D157's WORKFLOW1i-a note.

- [ ] **PERSONDOOR2 — beyond HTTP doors** (design; after PERSONDOOR1). Store/home files, connector token and remote key boundaries. D156 §7; decision before build.

- [ ] **XAGENT1i — second-opinion rehearsal** (tools/examples; after f/plugin maker). Findings reach work; disputes/missing reviewer hold. §6/§8.

- [ ] **REVIEWENV1d2 — live review_serve** (service/driver). Loopback serving wire reaches shell before session ends; tool waits for served answer. §2.3/§2.6.

- [ ] **REVIEWENV1e — separate review process** (driver/modules; after d). Procedure quote, Keep it, port/readiness/output and owned-tree stop. §2.3; stub/port/stop tests.

- [ ] **REVIEWENV1g2 — Stop showing** (modules/web; after d2). Route ends tab serving; press beside Show again. D154 point 8; route/gate tests.

- [ ] **GIT1j — offer managed Git** (web/driver; after GITAUTH1c, which took in TOOLS6, and TOOLS10; and the owner's answer to GITLIB1 §7, Daoris's own managed git as the default). Setup step, identity and explicit switch. §5; setup/coverage tests.

- [ ] **GITSTART6 — the built-in MinGit's `ucrt64` git past its launcher** (driver, service, devkit, cli; after TOOLS10; D121's GITSTART5 note). MinGit 2.56.0 holds its git under `ucrt64/`, which `GitForWindows.Prefixes` and its three twins do not name, so the managed git still starts through its launcher. Measure what that launcher hands its child first (GITSTART1's FIX-LOG method). Proof: a `ucrt64` row in `GitForWindowsTests` and each twin's table.

- [ ] **COAGENT1d — the lead asks for a piece** (service, driver, modules, web-settings, web-shell; 1b and 1g are built; frozen with partners' remaining rows, D233 point 3; design §9 row 4). `partner_ask` under the `partners` rule, its wait row, the Setup rows, `daoris driver partners`, Ask Daoris's doors, the canon skill's paragraph. Proof: as row 4 names it; then the owner's `on` for this repository and the dogfood.

- [ ] **COAGENT1f — measured both ways on the install** (the parent with the owner; after 1d; design §8.5, §9 row 6). Codex leading, Codex's resume and steering where ready, the first values read again in a D199 note.

- [ ] **USAGECAP1b — the capacity estimate, fitted and checking itself** (driver; after USAGECAP1a has three reset cycles of history; D179 points 1, 3, 4, 5 and 8). The versioned table of ratios (dated, sourced, never prices); intervals of five points or more inside one reset and one epoch; a 0.1-quantile fit through the origin, pooled per plan variant, with its sensitivity refit and its display gate; each new reading predicted first and its error kept; Codex's windows by duration and scope. Proof: tests on synthetic histories (a known capacity recovered within the range; a burst of unseen use; flat readings under rounding; a late reading; a reset inside an interval; a model switch; a cumulative report; a plan rebound starting an epoch; too little history saying *insufficient evidence*).

- [ ] **USAGECAP1c — the estimate on the account page and in the terminal** (web, cli, driver; after USAGECAP1b; D179 point 6). Per account and window: the capacity's range in reference-equivalent tokens, about how much is left at the last reading for this model mix, the reset, the reading's age, the error and the table's version, or *insufficient evidence*; en and 中文; never a price, an exact allowance or a promise that a session fits. Proof: web tests per state, `i18n:check`, `names:check -- --strict`, the CLI's twin on the shared fixture.

- [ ] **USAGECAP1d — the walk's last day compares validated absolute room** (driver; after USAGECAP1c and USAGERESET1; D179 point 7). Where every account compared has an estimate past D179's gate and check, USAGERESET1's last-day horizon orders by the conservative end of absolute room over the hours to its reset; anywhere else, percentages as before. The estimate never makes an account near or over. Proof: `AccountRotation` tests (two plans at equal percentages: the larger absolute room first; one weak estimate: percentages for all).

- [ ] **KNOW2b — prompt headlines** (after KNOW2a/DOC7). Five maximum, tier reservation, silent-service omission. Review §4.6.

- [ ] **KNOW2c — chat prompt hook** (after b/probe). Failing-open UserPromptSubmit headlines. Review §4.6; both-door probe/settings tests.

- [ ] **WSSETUP14b — knowledge stays put**. Declare documents.knowledge; index reads, sync never writes. Pilot §1.2–§1.3; twins.

- [ ] **WSSETUP14d — setup stays green** (after b; absorbs SETUP2). Before/after checks, preserved knowledge, moved readers and hand-index inventory. Pilot/D129; family proof. Seen 2026-10-24 on both kits' set-ups: each moved the records under `.agents/` and left the repository's own check scripts reading `.claude/`, so its verify fails until a request to that repository changes them; each session listed them, the scripts being outside what a set-up may change.

- [ ] **WSSETUP14e — finish setup branch** (after d). Exact follow-up merge rule. Pilot §4.2; composer/rehearsal tests.

- [ ] **ORIENT2f — standalone clone index** (examples/tools). Move/fail/regenerate/pass without Daoris. D151 §4; family proof.

- [ ] **ORIENT2g — post-adoption measurement** (owner install; read-only). ORIENT1e targets unchanged: halve calls/characters/searches, quarter whole reads, 80% uptake, no extra parks. D151 §5.3–§5.4.

- [ ] **PLUGREPO2e — retire migrated examples** (after PLUGDIST1g). Three examples/tests replaced by package offers. §7.

- [ ] **PLUGDIST1a leftovers — routing/package safety**. Move host installation to PluginsCommand; package kind through d, bounds through c. Original handback.

- [ ] **PLUGDIST1c — HTTP packages**. Bound extraction before acceptance. §5.3–§5.8/§7.

- [ ] **PLUGDIST1d — package host answers**. Records say package, not folder. §7.

- [ ] **PLUGDIST1e — Available catalogue** (after PLUGINEXT1's plugin UI design, `docs/2026-11-11-plugin-ui-design.md`, which replaced PLUGUI1f). §6/§7, amended by D140.

- [ ] **PLUGDIST1g — package offers** (after f). §7.

- [ ] **DOC6 — example document standard**. D122 §6; DOC7 is D127.

- [ ] **TOOLS9 — managed-tool rehearsals**. Loopback lists, stubs and tamper refusals. D121 §6/§7.

- [ ] **TOOL4h — account rehearsal/report** (tools; after TOOL4j). Stub window limits labelled Daoris-only; parallel account run. Rotation §8/D130 §5.4.

- [ ] **QUIETWINDOW1 — another project on this machine can ask Daoris's heavy runs to pause** (tools; with MERGETOOL1, whose runner and gates it holds; found 2026-10-23: three times in a day, another session publishing a different project asked the steward for a 15-40 minute quiet window because Daoris's test hosts made process starts on the machine take 190-850 ms and its spawn-heavy tests time out, and each time the steward stopped runs by hand). A machine-wide quiet request (a small file under the user's local application data, naming who asks, why and until when, bounded to an hour) that the CI watch, `--full`, `--rerun` and the merge tool's gates read before each heavy gate and wait on, saying so; a heavy gate already running finishes; the request ends by itself at its bound; a `node tools/merge-branch.mjs --quiet <minutes> --by <name>` and `--quiet-end` write and end it. Proof: a request holds the next heavy gate and lets a light one run; it lapses at its bound; the watch resumes by itself.

## A measurement or a sighting

- [ ] **GITOLD1 — absence on a git before 2.43** (driver; RECORDUNREAD1 is built; on a machine whose chosen git is older, the managed MinGit being 2.56; TOOLS5). Without `show-ref --exists` (exit 129) no gone branch's record is ever forgotten. Detect it and say the git is too old where git is chosen, or read the ref store. Proof: a fast test answering 129 says why.

- [ ] **WIRING-SIZES-LIVE1 — Settings fills in as a size walk counts** (modules, driver; EVENT5b is built). Keep a partial `SizeReading` while a walk runs and publish `wiring` at each entry; only if a walk is long enough to watch. Proof: a driver test reading mid-walk; a modules test publishing per entry.

- [ ] **OWNSPACE2e — moving Daoris's leftovers aside, bound to their identity** (driver, modules, web; D175's OWNSPACE2 note). The press back, safely: an identity-bound rename (`SetFileInformationByHandle`/`FileRenameInfoEx` on the opened handle), ancestors held without `FILE_SHARE_DELETE`, a second walk compared with the first, origins re-read after it, coordination with session starts. Proof: the review's three scenarios as tests.

- [ ] **CURVEROOM1 — "both are over their curves; it is the least over"** (driver/web; if the window shows it unclear). `WalkStep.Room` is said today by the other account's own reason; a coded reason of its own, en/zh. D165's SWITCH1b-ii note.

- [ ] **BRW14 — downloads as attachments** (design first). Define session download destination and quest transfer. D78/CHR3 §3.2.

- [ ] **SESSWATCH1f — the passes' own minutes, asked** (consider, after `TASKS.md`'s SESSWATCH1e; design §9 row 6).

- [ ] **DRIFT1e — follow-ups preserve requirements** (design first). Reopen parent corrections; closing notes are not requirements. D133 §5.

- [ ] **QUESTREBASE1 — pure rebase planner** (service; consider). Preserve operation identity, accepted-before-pending order and transaction. D69/D79; unchanged sync suite and loss tests.

- [ ] **TRACE1c — missing trace links** (driver/service/web). Late rules, local operations, merges, carry-ons, advances, detached section and commit kinds. D143 note.

- [ ] **KNOW3a — 169-document bench**. Opaque-name scale, whole index reads and skill truncation. Bench §5/§6.1; repeatable evidence.

- [ ] **STRAY1b — strays said on the Accounts screen and the terminal, and a repair door** (modules, web, cli; D125's HELPSTUCK1 note). A sign-in in a lock's folder is said only by Ask Daoris: the Accounts view and `daoris agent list` say it, and a door renames a stray to a fresh account id carrying its readings. Proof: a modules route test, the page's line in en and 中文, the CLI's list row.

- [ ] **QUESTCLOSE1b4 — a lost move's note keeps its parts** (service; consider). A quest's `conflicts` (`QuestConflict`) carry a lost move's note but not its parts, so a lost person's done reads in English. If the page should word it, `QuestConflict` and the answer gain parts. D142 point 2; a sync test.

- [ ] **FREEZE1b — capture next stalled bar** (modules/driver; trigger: ui.stalled). Move measured cause off UI thread; request runtime-owned changes. D56/log §4.

- [ ] **GOAHEAD2c — rehearsal go-aheads** (tools). Stub asks twice; first answer stays parked, second resumes once naming both. D135/phase 17a.

- [ ] **SQUASHTIDY1g — ancestry on another branch** (driver; decide, with PROCSTEPS1f's generic `tidy` and branch removals). Carried versus landed-elsewhere; offer/refuse consistently. D88/D102; content-offer/landing Process rows.

- [ ] **MCPSDK2 — protocol revision** (service; decide before SDK upgrade; the service is on `ModelContextProtocol` 1.4.1). Choose negotiation semantics before taking 2.x. MCPDISCOVER1; end-to-end probe.

- [ ] **SWAP2b — swap evidence** (tools). Marker distinguishes same-commit builds; retain longest holds from five loaded runs. D139/D60; failing marker proof.

- [ ] **SWAP2d — rollback by phase** (web/driver). Busy/move/start sentences and shared code table. D139/D41; bilingual twin cases.

- [ ] **FLAKE4b — read the stub's next refused fetch** (CLI tests; trigger: a stub turn failing on its quest door). FLAKE4 made the stub say what refused its fetch, and found no cause: keep-alive, a held loop and load did not reproduce it. Read the next sighting's cause and fix that. FIX-LOG's 2026-10-21 FLAKE4 note.

- [ ] **READSHARE1b — the shared read beyond the driver** (desktop modules, service; READSHARE1's remainder). READSHARE1 held the driver's readers of the files `AtomicFile` moves into place; still plain: the desktop modules' and app's readers of theirs (`KeptScheme`, `HomePlacing`, `BrowserSessionCookies`, `EngineBrowserHost`, `BrowserProcess`), the service's readers of its own `AtomicFile`'s files, and the driver's three reads of a repository's `daoris.json` and indexes the CLI writes by rename (`RepositoryIndexes`, `OpinionPackets`, `DriverBrainstorms`). FIX-LOG 2026-10-23. Proof: the census reaches those projects; each moved reader meets `MoveHold`.

- [ ] **WEBPORT1 — independent web hosts** (web). Allocate per-run port and pass base URL. REHEARSEPORT1; simultaneous-host test.

- [ ] **DEPLOYCOUNT1 — rehearsal-owned hosts** (tools). Count processes under this run's scratch only. Outside-process stand-in test.

- [ ] **TEST1 — capture Windows Node abort** (web/tools). Preserve reporter/exit at next 0xC0000409. FIX-LOG; no unsupported timeout tuning.

- [ ] **REH1 — captured canon-upgrade failure** (held). No tag until resolved or owner closes after clean runs. FIX-LOG/rehearsal transcripts.

- [ ] **TESTWAIT5b — `Held_words_name_what_holds_them`'s fallback cause, fixed and its retry dropped** (driver tests; after TESTWAIT5). TESTWAIT5 made the fallback's cause visible (`SessionsWorld.Fell`) and retries the test up to three times; once a loaded run names the cause, fix it (its `HeldClock`, FLAKE5, may be the shape) and drop the retry. Proof: the cause named in FIX-LOG, the class green three times beside a loaded machine with no retry.

- [ ] **TESTWAIT3b — `RunningService.Status`'s bound under load** (driver; HOMEID1). Its 2 s `Within` can be spent before a loaded local listener is asked, and the installed app calls it too: measure a loopback status answer's time on a busy machine, then lengthen `Within` or take the bound as a parameter. Proof: a test passing a longer bound to a slow listener, and the numbers.

- [ ] **TRYTOOLS1 — trial tools** (driver). Folder trial uses configured machine tools. D101/D121 §3; named-Node Process case.

## A real need shows

The workshop rows are frozen with the plugin workshop (D233 point 3).

- [ ] **CANON9 — WinForms pack** (held until second repository). Preserve two-repository bar. Original candidate.

- [ ] **TOOL5 — native adapters** (held until named tool). Measure each field. Toolchain §5/D23/D24/D57.

- [ ] **SEM2 — persistent vectors** (held until noticeable embedding latency). Existing SQLite/migrations; re-embed on model change. No lexical-only need.

- [ ] **PLUG7 — service plugin points** (held until requested). Reuse wire. D64.

- [ ] **A file tree in the dock** (held until browsing requested). D76; existing preview/terminal remain.

- [ ] **AFTER1 — multi-quest dependency** (held until real ask). Store dependencies and show unfinished predecessors. Future-directions §3/D65; planner/service tests.

- [ ] **MSG1k — fork terminal conversation** (held until real use). Messages §4.3; list/fork protocol stub.

- [ ] **PLUGHOOK2 — PR threads to work** (held until first manually carried request). Explicit review press carries person's words/new ask. Hooks §3.1.

- [ ] **PLUGDIST1h — repository signature** (held). Original §7 trigger retained.

- [ ] **WORKSHOP1a — workshop setting**. Home default and both doors. §2.1/§7.

- [ ] **WORKSHOP1b — workshop sessions**. §2.2/§7.

- [ ] **WORKSHOP1c — workshop view/proposal**. Creation uses workshop. §2.3/§2.5/§7.

- [ ] **WORKSHOP1d — handover/source**. §2.4/§7.

## OPTIMIZE1: after the foundation, the structure and the agents

What is left of GITAPP1 (1d-iii and 1h) comes back with OPTIMIZE1; its part two, the reader and the held object reader went with GITLIB1.

- [ ] **KEEP1a2 — `obj/` joins the slim, on evidence** (driver; after KEEP1a and a week of its measurement; D195, the cleanup design §9). `obj/` is measured apart from `bin/` over KEEP1a's first week; a second producer rule joins only where that bears it out, `bin/` never. Proof: as §9 names it, the measurement first.

- [ ] **FABRIC-END3b — the ref watcher's first day measured** (driver; after END3 is installed; §B4, canary 10). Read a day of the installed build's `work.held` and tidy lines: the delay from a merge to *held*, the keeper's git starts per judgement, any repository the watch missed. Proof: the numbers in D173's note; a row for any miss.

- [ ] **FABRIC-EVENT7 — the clocks as deadlines, raised on measurement** (driver, cli; after END3, EVENT3b, EVENT4 and END7, all built; §C5). `pollSeconds` and the tidy's pace become topic deadlines, raised only after canary 10 shows the wakes carry the work, with the CLI twin and the rehearsals that read it. Proof: canary 10 before and after; the twins' tests.

- [ ] **GITAPP1d-iii — the Branches tab's reads and the conflict preview** (1d-i is built; design §7.1). Stashes, tags, remotes, worktrees, reflog; the conflict preview under the session tree's own merge semantics, not previewed where a merge driver is defined. Proof: as 1d-i; the stash list moving on an older drop; the preview's merge-driver cases.

- [ ] **GITAPP1h — history at scale** (1d-ii is built; design §9). The file history's kept id list, the pickaxe as a long read, submodules, the commands to copy. Proof: a third page walks nothing; a cancelled pickaxe ends its git.

- [ ] **DOC7 — reading measurements**. session.read/session.skill by role and whole/partial. D127 §6.1.

- [ ] **STRUCT3OPLOOP — Review's opinion logic out of the loop** (driver; optional). `Loop/DriverOpinions.cs`, `OpinionDeliverer.cs` and `OpinionGateLook.cs` hold the pass, the delivery and the gate's look: what needs none of the loop's state moves into `Review/`, with no new ports. D166's STRUCT2g note. After PART10m these are classes in `Loop/` holding the front for its run machinery; the structure gate's `loop` rule lets nothing outside `Loop/` name them, so moving them needs that machinery out of the front first.

- [ ] **PROC1e — ten minutes, or a new bar** (decide; after PROC1b-d). PROC1a projects b and c to about 47 minutes under a full set's usual load and 24 on a quiet machine, the node-hook classes joining to about 20 quiet, and d to about 15; the 66 serial classes (670 s loaded, about 340 quiet) run alone in every plan. Ten minutes needs them shortened (`DrivenSessionInputTests` 109 s, `PluginKitTests` 102 s, `BranchTidyingProcessTests` 94 s) or run beside the pool with the merge tool's alone-rerun taking the load's failures; else the owner sets the bar the measures reach. Contract: FIX-LOG FLAKE1, PROC1a's paragraph; development.md:64-66. Proof: the chosen bar met three times, recorded there.

- [ ] **PROC1a-ii — the product's starts in the longest Git tests, counted again** (driver; measure only). The 517 and 402 starts PROC1a traced in `TreeSyncTests`' two longest were never counted after PROC1d-i to -iv. Recount them by command (`WorkingTree.Started`, single runs) so the next lever is chosen on data. Proof: a FIX-LOG paragraph, before (`cb15ad7`) and after.

- [ ] **PROC1-KNEE — the git half's parallel width measured whole** (driver tests; after GITSTART1). Re-measure `MaxParallelThreads` 4, 6 and 8 over the whole `driver-process-git` gate (37 classes) on a quiet machine; four classes cannot run more than four at once, so the knee is still PROC1a's. Proof: three walls with load and counts in FIX-LOG; change the setting only on a clear gain. PROCSPEED1, 2026-10-19: the whole git half once each, 4 threads 2033 s (CPU mean 50%, merges beside) and 6 threads 3496 s (CPU mean 64%, a family rehearsal, two CLI suites and another project's scans beside); 8 not run. A quiet machine is owed the answer.

- [ ] **TESTVALUE1 — every test earns its place** (driver, modules and web tests; the owner's question, 2026-10-22; folded into CISCOPE1's design). The driver's tests are 6,653 facts, 933 theories with 4,750 rows and about 2,000 generated cases, 13,458 in the fast half (about a minute) over 220,000 lines; the cost is upkeep, not time. Each test's coverage is recorded once (lines and branches it reaches), and the tests that add nothing another does not are listed, then folded into a table or removed with the reason; page and driver tests that pin English sentences where the words are not their subject assert the catalogue key or the code instead (the golden and catalogue tests keep the words). Proof: the list with its numbers, the suites' counts and coverage before and after, no line or branch losing its last test. Its measures, rule and target are D207's design §7 (proposed): CISCOPE1g measures first, and the git half goes first, to a target of class time set from CISCOPE1g's report (about 28 class-minutes would fit the scoped budget; 158 measured).

- [ ] **DRIVERTESTSPLIT1 — the driver's tests in projects a change can build alone** (driver tests, the gates; the owner, 2026-10-19, of a builder at 1 h 45 min that ran its verification gate several times). `Daoris.Desktop.Driver.Tests` is one assembly of about ten thousand tests, so any edit recompiles it whole and a run filtered to five tests took three to ten minutes on this machine (USAGERESET1: twelve such runs, 45 minutes of a 106-minute branch). Split it by area (accounts, quests and fabric, trees and landing, sessions and loop, help and plugins, platform) into projects that build alone over a shared fixtures library, keeping `StructureTests`' placement map, the `Process` category and the gates' commands (the fast half and the two `Process` halves run every project). Proof: a one-file test change rebuilds one project; the fast half's and the `Process` halves' counts unchanged; a red-green loop on one area timed before and after.

- [ ] **DRIVERTESTSPLIT1a — measure what a builder's filtered run spends, then move one area out** (driver tests; DRIVERTESTSPLIT1; a scout's plan, 2026-10-19). The test project is 637 files and about 177,500 lines over a driver of 157,000; a split saves compiling only on a test-only edit, since a changed driver signature rebuilds every test project, and builders usually change both. So first time, on a quiet machine, a filtered run after a test-only edit, after a driver body-only edit and after a driver signature change, each split into compile, discovery and run; record the numbers under the row. Only if the test compile dominates: a nested layout that keeps every path, namespace and gate command (the folder `Daoris.Desktop.Driver.Tests/` holds one `.slnx` listing a shared library and an area project per module folder, as the service's gate runs its folder), Platform and Setup out first with the meta tests and the Host and Launcher build references. Traps: `Alone` and `ProcessPath` are collections per assembly; `InternalsVisibleTo` per project; `SourceFiles.All` and `StructureCheck` skip `bin`/`obj` only at the first segment; `dogfood.test.ts` finds projects only three folders deep; one trx name per gate is overwritten per project (merge-branch.mjs `trxCommand`). Proof: the timings; the three gates' counts unchanged.

- [ ] **CIPRIO1b — a CI run's I/O at background priority, measured first** (tools; D180's CIPRIO1 line). Its CPU class is below normal; its disk and memory priority are not, since node has no API for background mode and whether children inherit it is unsettled. Measure a lowered run's children's I/O priority on this machine, then lower it the cheapest way that holds. Proof: a child's I/O priority read at the lowered level.

- [ ] **TESTGIT1b — shared GitFixture** (driver tests). Landing plugin/tidy/fixture/landing/auto classes; eliminate independent git runners. Serial Process proof.

## Notes the old sections carried

Contracts, orders and the owner's reports that the rows above were filed under, kept whole where a row still stands under them.

**This Daoris: its folders, its wiring and its settings**

The owner, 2026-10-16: *"for every setting and how the server and data are wired we need some place to show it, or to set it
up, with good UI/UX"* and *"we also need to shape the Daoris folder into a better separation since we will have more files
now (data, app, res, …)"*. An install today is `Daoris.exe`, `app/`, `data/` and `update/`, and `data/` mixes the person's
settings, records, sign-ins, downloaded runtimes (`toolchain` 1.4 GB, `chromium`, `webview2`, `bin`), plugins, session trees
(1.7 GB), locks and logs at one level. Order: HOMELAYOUT1 decides the folders, WIRING1a designs the page; then each builds.

**Accounts and browser**

Contracts: toolchain (D57 §3), account rotation (D125), account use (D130), browser (D78/D84).

**History and reliability visible to the person**

Contracts: `docs/archive/2026-10-07-history-clearing-design.md` (D153), D158,
D88/D102 cleanup notes, machine log §4. Installed clears require the owner's existing authorization.

**Consistent screens**

Contracts: plugin-screen design (D119), frame model (D118). PLUGUI1f and g went into PLUGINEXT1's plugin UI design
(`docs/2026-11-11-plugin-ui-design.md`). Installed looks cover both themes/languages.

**Workflows**

Contract: `docs/archive/2026-10-09-workflow-design.md` (D157), §14 order/proofs.
Foundation precedes editor/proposals/intake; waits/checks/stages follow the gate; installation last.

**Person-only doors**

Roles the owner appoints, and the question card. Contract: `docs/2026-10-24-supervisor-design.md` (D208, accepted 2026-10-24 by the
QUESTIONUI1a, the question card, is built (the archive).

**Second opinions**

Contract: `docs/archive/2026-10-08-second-agent-design.md` (D155), section proofs.

**Review environments**

Contract: `docs/archive/2026-10-08-review-environment-design.md` (D154), section proofs.

**Landing, trace and Git**

Landing in sync with the line (the owner, 2026-10-15: *"the merge fix needs a rebase (I think we kind of do need this logic when checking for branch)"*, *"we just have too many work branches … verify why we have this in-sync issue"*, *"I cannot accept for 2 sessions/quests at the same time"*). The install's case: a pull request squash-merged a session's first commit; the session went on in its tree on the old base; Accept landed all three commits, so the next pull request carried a duplicate of merged work and conflicted. Older parts of the same ask kept trees offering Accept, and each press made another branch. Designed in D87's and D167's amendments of 2026-10-15 (LANDSYNC0), with D109's note of the same day.
Contracts: D143/D145/D148/D149; plugin-hooks design;

**Sessions**

Contracts: session-management (D126 §9), session-messages (D137), answer-continues (D131),
Found driving the installed home clean on 2026-10-15 (the owner: *"please drive daoris and please also clean up all unfinished task/session in this local daoris so we can start the next round of tasks; currently we find a lot of issues because this design is for as automated as possible"*):

**Economy, setup and recall**

Contracts: session-economy (D127), workspace-setup (D124), setup-pilot lessons (D128),

**Plugin distribution**

Contract: plugin-distribution design (D120), §7 proofs. Plugin-repository work is a request to its owner.

**Declarations and managed tools**

Contracts: development-documents (D122 §6), tools (D121 §7). TOOLS6 is in GITAUTH1c; TOOLS10 precedes managed-Git sessions (GIT1j,
GITSTART6), and TOOLS9 rehearses what they build.

**Code structure**

**First, before new feature rows** (the owner, 2026-10-13: *"I think we do need to move the full refactor mentioned before
into priority since that will make the code better to develop"*; D166's note). Narrowed on 2026-10-14: a folder per module, a
`Domain/` for its model and rules, the rest flat, and duplication reduced (D166's note of 2026-10-14). Contract:
`docs/archive/2026-10-12-code-structure-design.md` as amended. Moves are behaviour-preserving; new driver files go into their module's
folder.
**Duplication** (the owner: *"we more focus on modularize the code to reduce duplication and easier to manage"*). Evidence: `docs/archive/2026-10-14-duplication-survey.md`, by section. After FLAT1; DEDUP1 to 3 touch the same files, so one at a time.

**Development and agent layout**

Contracts: self-development (D115), agent-layout (D117 §7), entry-point evidence.

**Remaining regressions, evidence and held work**

Contracts and complete reproductions: original backlog, FIX-LOG entries, named decisions.

## Frozen until the daily loop is solid (2026-11-04, D233 point 3)

- [ ] **DOCSHOME1 — managed documents and skills have two portable homes** (a new area of ONEWAY1's census): a document with more than one source is a second way. → `docs/archive/2026-11-02-oneway-census.md`
- [ ] **LAYOUT5 — doctrine moved to one agent layout** (cli; D117): two agent layouts are a second way. → `docs/archive/2026-10-01-agent-layout-design.md`
- [ ] **LAYOUT6 — the brief and the rooms on that layout** (cli; D117; after LAYOUT5): the brief and rooms follow the move. → `docs/archive/2026-10-01-agent-layout-design.md`
- [ ] **KNOWCENTRAL1 — the memory design a centralized, model-driven knowledge system rests on, made with Lyntai** (design across two repositories; D220): rounds 1 and 2 are built; the next round settles retirement through decay, which upkeep a model takes alone, and a memory shared across repositories. → `docs/2026-10-31-memory-coordinator-design.md`; `docs/areas/knowledge-memory-records.md`, Open
- [ ] **KNOWFRESH1 — a fresh fact replaces the one it updates, and a stale one is found and retired** (D220): a is built; **b** waits on a Lyntai release carrying MC7, MC11 and MC12 (a local build today, the release the owner's); **c**, the machine's memory server's half, is the owner's to route. → `docs/archive/2026-10-30-knowledge-freshness-design.md`; `docs/areas/knowledge-memory-records.md`, Open
- [ ] **STEWARDROLE1 — the steward a role the owner appoints, so development's management needs no click** (modules, driver, service, web; D209, D233 point 2): 1a to 1d are built (the seventeen acts, roles area rule 51), and 1d's installed look is `TASKS.md`'s STEWARDROLE1d, which waits on the owner appointing the steward's holder; after it, **STEWARDROLE1e** the steward's answer to an agent's hand-over ask, **STEWARDROLE1f** the go-on (after D210 answers the design's §4.3a), **STEWARDROLE1g** the set-up after f, and **1h** free words. → `docs/archive/2026-10-24-steward-role-design.md` §10
- [ ] **BRAINSTORM1 — the brainstorm's remaining rows** (service, driver, cli, web; D176): **BRAINSTORM1c** the measured run on real makers (the owner's yes; it spends accounts), then **BRAINSTORM1d** a caller from another repository (its page is MEETUX1's); **BRAINSTORM1b2** the stop and `--id` through the one verb (ONEWAY1t); with the meetings **BRAINSTORM1b3** a limit on a real door, **BRAINSTORM1a2b** a no-connector pass test, **BRAINSTORM1a4** a pass's traces, **BRAINSTORM1e** the skeptical pass; **BRAINSTORM1f** a workflow node. → `docs/archive/2026-10-19-cross-agent-brainstorming-design.md`
- [ ] **AGENTROOM1 — meetings of agents of different makers** (service, driver; D177): **AGENTROOM1b** `meeting_say`, so a real agent takes a meeting turn (COWORKVIEW1f waits on it); **AGENTROOM1c** re-cut as a kind of the one brainstorm verb, no `daoris meeting` (ONEWAY1t); **AGENTROOM1d** one account's turns one at a time across meetings. → `docs/areas/co-working-and-messages.md`, Open
- [ ] **MEETUX1 — meetings and brainstorms in Sessions** (web; D185): **MEETUX1a** started from the window, **MEETUX1b** rounds followed, **MEETUX1c** a brainstorm's document read, **MEETUX1d** a meeting waiting on the person in *What needs you* (with COMMS1j). → `docs/areas/desktop-app.md`, Open

## Waiting on STEWARDROLE1d's installed look, a sighting, a measurement or a release (2026-11-05, backlog plan §1; corrected 2026-11-11)

- [ ] **Rows that wait on STEWARDROLE1d's installed look** (the steward's and the supervisor's activations are built, roles area rule 51 and Open; each row extends an appointed role's acts, so it waits for the owner to appoint the steward's holder and see its acts): **XAGENT1f2** the person's opinion presses (→ `docs/2026-11-02-parked-triage.md`, STEWARDROLE1); **PERSONDOOR1c** publish attribution, **d** driver, **e** CLI and **g** shell authorization, **h** gates carry it, **i** every host enforces, each read against the code first, since the window's confirmations (`Service.Http/PersonConfirmations.cs`) and a person key per host start (`Modules/Loop/HostSupervisor.cs`) stand (PERSONDOOR1j goes to ONEWAY1n; → `docs/archive/2026-10-09-person-door-design.md` §10); **COMPANYVIEW1h** the steward's marks on the company view (→ `docs/archive/2026-10-31-company-view-design.md` §7); **LINESTOGETHER1e** the steward's fixed forms (→ `docs/2026-10-31-review-flow-design.md` §2–§5); **INTEGRATE1i** the steward's acts (→ `docs/2026-10-28-integration-design.md` §11).
- [ ] **COWORKVIEW1f — the co-working view's meeting rows** (driver, service, web; D173; after BRAINSTORM1d and AGENTROOM1b). → `docs/2026-10-24-cowork-view-design.md`
- [ ] **TREEOPENERR1 — the next fresh-tree opening that fails with no stderr, read** (driver): the open's git reads now say what git did (`TreeGit.GitSaid`), but no failing run was kept; on a sighting, fix what it names at the open/undo boundary, with no weaker locks, retries, sleeps or forced cleanup. → `docs/FIX-LOG.md`, 2026-10-30; `docs/areas/driving.md`, Open
- [ ] **AUTOTIDY1b — the strict cleanup guards measured** (driver; a measurement, with WORKFLOWCLEAN1's §11). → `docs/2026-11-02-parked-triage.md`
- [ ] **STAFFPOLICY1h — the staffing policy's measurements** (driver, modules; behind its measurements). → `docs/2026-11-02-staff-policy-design.md` §9, §11
- [ ] **DEVKITAUDIT1 — the devkit builds with NuGet's audit on** (devkit): the AOT compiler the SDK brings (`Microsoft.DotNet.ILCompiler` 10.0.13, advisory GHSA-p3jp-9v83-76w9) raises NU1903, which no package pin here moves; the devkit CLI keeps NU1903 a warning (`9759d6db8`) so the code map builds. Remove that exemption when the machine's SDK carries a fixed runtime (SDK 10.0.402 still carries the 10.0.13 runtime, read again 2026-11-11). → `docs/areas/doctrine-and-cli.md`
