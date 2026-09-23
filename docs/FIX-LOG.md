# Fix log

Root cause, fix, and verification for non-trivial defects — the record version control cannot carry:
a diff shows what changed and never why the old behaviour was wrong. Newest first. The knowledge
service indexes this file per entry, so a sibling can ask "has anyone hit this" without opening the
repository.

## A login from the Machine view could never finish (2026-09-23)

**Symptom.** Pressing *Log in* on an account opened a browser and then nothing: the row kept
reading *not logged in*, the output under the door (a card below the button) showed the sign-in
link twice around a scatter of `]8;;`, and every control on the page stayed disabled until the
window was closed (owner: *"the ui for login claude, during and after include the entire login
workflow itself need better ui/ux"*).

**Root cause.** Four, found by running the harness's login with no console attached and then on
the installed shell. The flow Claude Code takes when its stdin is not a terminal is the *paste the
code* flow: it prints the link and `Paste code here if prompted >` and waits on stdin — which the
action never redirected, so nothing could answer it and the process waited for ever. The prompt has
no newline, and the pump delivered whole lines only, so the one line the person had to answer never
reached the page. The link is written as a terminal hyperlink escape (OSC 8), which a console well
renders literally. And the request that started the login **waited for it**: the bridge times a
request out at thirty seconds, a login waits on a person for minutes, so the page's call failed
while the process ran on — the panel closed, the row said nothing had changed, and a `claude auth
login` was left waiting for a browser nobody was told about (measured: the process was still alive
after the page had given up).

**Fix.** `HarnessActions.RunAsync` redirects stdin and hands a `HarnessRun` (send a line, cancel)
to whoever asked; `PumpAsync` delivers a partial line once the stream has been quiet for 250ms;
`Clean` strips OSC, CSI and lone escapes. `DriverModule` answers a process action once it has
**started** and announces its end as `HARNESS_ENDED` (the shape a conversation's ending already
took, D49 §3), keeps the running action by `harness:action`, and answers `HARNESS_INPUT` and
`HARNESS_CANCEL`, refusing both naming the action when nothing runs. The page's `SignIn` sits on
the account row with the three steps — the link with a copy button, the code box once asked, the
row's own pill as the result — hears the end as news, and keeps the tool's output one disclosure
away.

**Verify.** `HarnessRunTests` drives a node stand-in through the prompt-without-newline, the answer
and the cancel; `SignIn.test.tsx` the steps over a mocked bridge. On the installed shell, a
throwaway profile's sign-in streamed onto its row and **completed in the browser with no code
asked** — with a stdin present the harness takes its callback flow and prints only *Opening
browser…* — so the first step now says the browser was opened rather than waiting for a link that
never comes; the profile was forgotten afterwards. The paste-the-code path is held by the tests.

**Commit.** _pending_

## The deployed shell flashed a console window on every tick (2026-09-23)

**Symptom.** With the installed application running, a terminal window kept appearing and vanishing
on the desktop (owner: *"a console window keep popup up"*). Nothing in the workspace dev loop showed
it.

**Root cause.** The shell is a windowed process with no console, and a console child of such a
process is given a console of its own unless the spawn sets `CreateNoWindow`. Four spawn sites did
not: the session shell in `Adapters.cs`, the harness probe and the harness action in `Harnesses.cs`,
and git in `WorkingTree.cs`. The host supervisor and the hook processes did, which is why the
loop's own children never showed — but git runs every tick and the probes on every roster refresh,
each one flashing a window. Invisible from the workspace because `desktop.mjs run` starts the shell
from a terminal, whose console every child inherits.

**Fix.** `CreateNoWindow = true` at all four sites, and `NoConsoleWindowTests` in the driver's tests
scans every `new ProcessStartInfo` under the desktop's projects for it — the property belongs to every
spawn and the next site written will not know to.

**Verify.** The scan failed on the four sites before the fix and passes after; the installed shell
republished and run with the loop ticking, no window.

**Commit.** `f11a3d4`

## The rehearsal's ACP stub hung the driver on its second quest (2026-09-23)

**Symptom.** The family rehearsal's new plugin phase (D64) drove a second quest over the ACP stub
agent, and the driver sat until the gate's 90-second kill with the session record left `working`.
The transcript ended at *"handler failed: Command failed: git … commit"* and *"the ACP agent's
stream ended before it answered"*.

**Root cause.** Two, in the fixture. The stub wrote one fixed file with fixed content, so the
second session's tree had nothing to commit and `git commit` exited non-zero; and its
`session/prompt` handler let that exception escape, so the prompt was never answered — the driver
did what it should with an unanswered turn and waited on its own timeout. A turn that fails is
still a turn, and a harness that crashes mid-turn exits; a stub that neither answered nor exited was
a hang dressed as a session. It never showed while one quest a run rode the door.

**Fix.** `tools/family-rehearsal.mjs`: the stub writes `acp-answer-<quest>.md` and answers a failed
prompt with a JSON-RPC error, which the driver concludes as a failed session in seconds.

**Verify.** `npm run rehearse:family` 192/192, with phase 18's two quests both driven over the stub.

**Commit.** `77e1458`

## The testbed's own connector held every testbed quest (2026-09-23)

**Symptom.** After D63 moved the installed hosts under the Daoris home, `tools/testbed.mjs` was
re-run to re-point each testbed's `.mcp.json` — and the deployed shell then toasted *"held #… →
testbed-core: the working tree has uncommitted changes (1 paths) — somebody's work in progress"*
for every testbed. The driver was right: each tree was dirty. The one path was the connector.

**Root cause.** The script commits once, at birth (`README.md`, *"the starting point"*), and never
again: `init`, `sync`, the declaration and the connector are written afterwards and left for
somebody to commit. On the first build a driven session happened to land them with its own work;
on a re-run nothing did, so the file the script rewrote wholesale sat modified, and the driver's
clean-tree rule (D46) held the quest on the fixture's own wiring. A fixture that leaves the driver's
precondition unmet on every re-run is a fixture that tests nothing after the first day.

**Fix.** `tools/testbed.mjs` commits what the run wrote, per repository: the set of paths dirty
after the run and not before, plus the files it writes wholesale every time (`.mcp.json`,
`.claude/settings.json`, `daoris.json`) whether or not an earlier run left them dirty — because
"dirty before" there is the previous run's, and the previous run was this script too. Anything
else dirty is somebody's and stays. The row says `(committed)` when it did.

**Verify.** Re-run against the deployed install: three `(committed)`, `git status --porcelain`
empty in all three testbeds, the last commit *"adopted, synced and wired by tools/testbed.mjs"*,
and no held toast on the next tick.

**Commit.** `fe41055`

## The account a person has was invisible, and Forget forgot nothing (2026-09-23)

**Symptom.** Owner: *"a lot ui/ux issue which does not allow to manage account easily"*. Exercised
on the deployed application: `claude-code` read **"No accounts — sessions run in this tool's own
configuration home"** to an owner who was logged in there; *Add an account* made a row with a name,
a *not logged in* pill and a *Log in* button, and nothing about the browser window about to open or
where its output would go; a work account for the work circle could be set from a terminal
(`daoris harness profile default … --workspace`) and not from the screen; the toast read
*"claude-code: harness.profile-add finished."*; and **Forget on the new account did nothing anyone
could see** — the row stayed, the directory stayed.

**Root cause.** Four. The roster reported named profiles only, never the tool's own home — the
account that exists before any is named. The page carried no workspace on `profile-default` and
the payload carried no per-circle defaults, so the file's own shape (D49 §4) was invisible from
the screen. Three action names had no catalogue key. And "deletes nothing" was literal: a profile
is a directory, the roster lists directories, and Claude Code scaffolds a fresh home the first
time it is asked `auth status` — so a fresh account was never empty and never went away.

**Fix.** The probe asks the harness about its own home exactly as it asks each profile
(`HarnessReport.OwnLogin`); the roster leads with *this machine's own · logged in · sessions use
this*, and takes no login from here — that is the tool's own business and the row says so. A
logged-out row says what *Log in* will do. Each row offers *use for a workspace…* over the circles
this machine has, the payload carries `workspaceDefaults`, and the row says *sessions in X use
this*; `profile-default` with no profile **clears** rather than refuses. Forget takes the
directory when there is nothing signed-in to destroy by the harness's own word — empty, or
reported signed out — and keeps it otherwise, saying which and where; the CLI's `profile remove`
draws the same line, and the family rehearsal holds both halves with the stub harness and `dsh`.

**Verify.** Driver, modules, CLI and roster tests, each seen red first. On the install: the own
row logged in; *work* added, its hint under it, Forget → row and directory gone.

**Commit.** `adeb520`

## The roster listed a fixture as a tool, and spoke the runtime's words (2026-09-23)

**Symptom.** The Machine view on the deployed application ended with an agent tool called `stub` —
*not installed*, an **install** button, *no command to run* — and `dsh`'s absence read *"`dsh` is
not on this machine's PATH — An error occurred trying to start process 'dsh' with working directory
'<the checkout>'. The system cannot find the file specified."*

**Root cause.** Two. The stub adapter declares a toolchain with **no binary of its own** — the
"binary" is whatever `driver.json` names, which is what lets the family rehearsal gate the roster
with no model — and the roster enumerated every adapter with a toolchain; a machine that names no
command got a fixture presented as a tool. And the probe appended the runtime's exception message to
its own sentence for every failure alike, including the one where the sentence already says
everything: file not found, plus a machine path the probe's working directory happened to be.

**Fix.** A door whose toolchain has no binary and for which no command is configured is not on the
roster (`HarnessRoster.RosterAsync`, structural, and the spawn path is untouched); name a command and
it is a door again. A `Win32Exception` with `ERROR_FILE_NOT_FOUND`/`ENOENT` keeps the sentence and
drops the message; any other start failure keeps the runtime's words, which are then news.

**Verify.** `HarnessTests` — the fixture door off the roster until a command names it, and the
absent-harness sentence exact — both seen red. On the install: no `stub` card, and `dsh`'s row
reading the one sentence.

**Commit.** `06e560b`

## A blue ✕ in a palette with no blue — the OS accent through a native control (2026-09-23)

**Symptom.** The Search field on the deployed application showed a blue ✕ at its right once a
query was typed, in both themes. Nothing in the platform is blue: D41's palette is ink, accent and
the validated status hues, and `tokens.test.ts` holds every size to a named step — colour was not
being watched the same way.

**Root cause.** `<input type="search">` brings Chromium's native clear button, and WebView2 paints
it with the **Windows accent colour** — the person's system setting, not a token. A browser test
cannot see it (jsdom draws nothing), and Playwright's Chromium paints it grey, so it was invisible
until a screenshot of the real window.

**Fix.** The native button and decoration are hidden (`tokens.css`, `-webkit-appearance: none`),
the field keeps its type, and `SearchView` draws its own clearing control in the platform's
language — a ghost button that exists only while there is something to clear.

**Verify.** `SearchView.test.tsx` (seen red without the control). On the install, in both themes:
the field with a query shows the platform's ✕ and nothing of the accent.

**The trap to inherit.** 🔴 **A native form control carries the OS accent into the page.** Any
`type="search"`, `<select>`, checkbox or range left native is painted by the system, in the system's
colour; the platform draws its own (D41 §4), and this one had been missed.

**Commit.** `92afb48`

## One result, zero marks — the marker cut 中文 differently from the index (2026-09-23)

**Symptom.** Searching `会话记录` on the deployed application: one result, correctly — a body that
says `会话的记录` — and nothing marked in its excerpt, though marking the matched term is the whole
reason the excerpt is shown.

**Root cause.** The index matches a run of ideographs by its overlapping bigrams (`会话 话记 记录`,
the 中文 search fix of the same day); the marker looked for the query as it was typed, one
four-character term, which the body does not contain. Two halves of one rule cut two ways — the
same shape as the tokeniser's floor, found the same day, in the other direction.

**Fix.** `termsOf` cuts a run of ideographs into its bigrams before marking (`highlight.ts`,
`cut`), a lone ideograph staying one unit and everything else whole — `Text.Segment`'s rule, in the
marker's language.

**Verify.** `highlight.test.ts`: `会话记录` → `会话 话记 记录`, and the body `会话的记录在此` marked as
`会话` · 的 · `记录` · 在此 — both seen red. On the install, the same search: one result, the two
words marked in its excerpt.

**Commit.** `fbfa609`

## The drawer's close button was under the window's own (2026-09-23)

**Symptom.** In a quest drawer on the deployed application, the header's × was not there: a sliver
of a button showed beneath the window's caption buttons, and the rest was hidden. ESC and the
scrim still closed the drawer; the control the header carries for it did not exist to a person.

**Root cause.** The app strip is the window's title bar (D56), and the three caption slots in it
are painted by the **window**, over the page. The scrim learned this on 2026-09-22 (`top-9`, held
by `tokens.test.ts`); the panel beside it did not, and stayed `fixed inset-y-0` — so its header sat
in the strip, under the buttons the page does not draw. Invisible in every browser test, because
a browser has no caption to hide behind; visible in one screenshot.

**Fix.** The drawer starts at `top-9`, like the scrim (`ui.tsx`). The rule is now held for every
panel, not only every scrim: `panelsOverTheStrip` in `tokens.test.ts`, seen red on the drawer's
own class list.

**Verify.** The tokens test, red then green. On the install: the quest drawer's header sits below
the strip with its × visible and the caption buttons beside it, not over it.

**Commit.** `57281d2`

## The Overview asked whether anything was sitting, and never said why (2026-09-23)

**Symptom.** On the deployed application with one open quest held on the trust flag: the Overview's
outstanding row read *pill · title · route · filed 8h ago* and nothing else, and the only place the
reason appeared was a toast — three of them, as the hold's reason flapped — gone in seconds. A person
looking at the row had no way to learn that this machine's driver had looked at the quest every ten
seconds and declined to start it, or why.

**Root cause.** Three halves. The driver has said why every open quest is sitting since D46 §3
(*"sitting must always say why"* — `Consideration.Reason`), and the shell forwarded it in every tick
as `considered`; the page read the tick's `events` and dropped the rest. The loop forwarded a tick
only when it had events or a planned start — so a quest sitting *silently* (not drivable here, no
root, at capacity) never reached the page at all, on any tick. And the holds a real machine actually
hits — a dirty tree, a logged-out harness, a tree that would not grow, a trust flag never given —
are decided at **spawn**, after the planner said `Start`; the report kept the plan, so the quest
this machine held every fifteen seconds read `Start` in every consideration. The first build of
this fix shipped the page's half, republished, and showed nothing under the row: that is how the
third half was found.

**Fix.** A spawn-time hold becomes the quest's verdict in the report — `StartVerdict.Blocked`, the
hold's own sentence as the reason (`Considerations.Blocked`; the plan itself is untouched). The tick
writes `considered` to its own query key (`keys.considered`, never invalidated, never fetched);
`useConsidered` reads it; the Overview row carries *sitting — <reason>* truncated with the whole
sentence in its tip, and the quest drawer carries it whole. The loop forwards a tick when the
considerations' **signature** changes (`Considerations.Signature`, order-free), so a stable sitting
set costs nothing and a change always arrives. A `Start` verdict is not sitting.

**Verify.** `sittingBecause`, the signature helper and the blocked fold-in, each seen red first;
the shell test seen red against the previous Overview (the sentence never rendered). On the
deployed application: the row under `#7786da` reads the driver's trust-flag sentence, and stays.

**Commit.** `9a6390c`

## A page seen once from a stale host was the page on every start after (2026-09-23)

**Symptom.** With the host precedence fixed and the install demonstrably running its own host, the
window still loaded `index-CeYefnb-.js` — a bundle that host does not have (its `/assets/` answers
404 for it). `performance.getEntriesByType('navigation')` in the live page: `deliveryType: cache`,
`transferSize: 0` — for the page, its script and its stylesheet.

**Root cause.** The HTTP host serves `wwwroot` with `ETag` and `Last-Modified` and **no
`Cache-Control`**, so the browser applies heuristic freshness to `index.html` and reuses it without
asking. The one time the window loaded a page from the machine's older host (adopting it, the
minute before), the WebView2 profile under `data/` kept that `index.html` **and the bundle it
named**, and every later start was answered from there — a stale page with no request ever reaching
the correct host. The hashed assets are the right thing to cache forever; the unhashed page that
names them is the one thing that must never be reused without revalidation, and it was the one
thing with no instruction at all.

**Fix.** `Program.cs` (local mode): `/assets/*` is `public, max-age=31536000, immutable` — a
hashed name is a promise — and everything else, the page above all, is `no-cache`, which with the
ETag the host already sends is one conditional request and a 304 per start.

**Verify.** The deployment rehearsal's phase 3 asserts both headers on the published host — seen
red against the previous host (`cache-control: (none)`, 32/34), green after (34/34). On the deployed
application, republished and started twice: the first start fetched the page from the network
(775 B, 200); the second made a conditional request (300 B — the ETag exchange) before reusing the
body, while the hashed bundle and stylesheet were reused at 0 B. Blind reuse is the `0 B` the
symptom showed on the page itself, and it no longer happens to the page.

**The trap to inherit.** 🔴 **A page that names hashed assets is itself unhashed, and it is the
page that decides what the window runs.** Every other fix in this area — the precedence, the
adoption notice — reaches the browser through this file, and none of them can be seen while it is
served from a cache.

**Commit.** `94845c9`

## The deployed shell ran the machine's older host, not the one published with it (2026-09-23)

**Symptom.** `npm run publish:desktop -- --to <install> --service`, then start the install: the
window showed the **previous** page. `performance.getEntries()` in the live page named a bundle
(`index-CeYefnb-.js`) that existed nowhere on disk except under `~/.daoris/bin`; the install's own
`wwwroot` held the new one, and the host answering on 5177 served a 404 for it. The host process
was the shell's child and its path was `~/.daoris/bin/daoris-knowledge-http/` — not
`<install>/app/daoris-knowledge-http/`.

**Root cause.** `ServiceHostLocator` ranked the machine's installed home **above** the host beside
the shell, deliberately, with the reason written down: *one service, upgraded once for every shell
on it — a deployed copy outranking it would make a service upgrade invisible*. The second
deployment produced the inverse: the **desktop** upgrade was invisible, because the install's own
upgrade path (`publish:desktop --service`, one command, both halves) lands beside the shell and the
shell was not looking there first. The first deployment's 4d was the same class through a different
door (adopting a host already *running*); this one spawned the wrong host with nothing running.

🔴 **The deployment gate said so and passed.** Its header listed *"`~/.daoris/bin` outranks the
install"* under *what this gate cannot control*, and phase 4 asserted only that the host was *not
this workspace's build* — which the machine's host satisfied — while printing
`located: ~/.daoris/bin/…` in every transcript. 32/32, with the wrong host named on line 33.

**Fix.** The order is now *what the person said → what the install carries → the machine's
installed home → the workspace build* (`ServiceHostLocator.cs`); a shell published without
`--service` still falls through to the machine's home. Phase 4 of the rehearsal asserts the started
host is the one **under the install** — on a machine that ran `publish:service --install` the
machine's copy is the decoy, and on a clean machine the check is the same as before.

**Verify.** `ServiceHostLocatorTests` — two tests seen red on the old order. `npm run
rehearse:deploy` seen red at phase 4 on this machine with the strengthened check and the old order,
then green with the new. On the deployed application: `document.scripts` names the bundle the
install's `index.html` names, and the Projects page renders this commit's sentences.

**The trap to inherit.** 🔴 **"What this gate cannot control" is a list of things the gate is
wrong about until proven otherwise.** A stated limitation that names the exact defect is a check
waiting to be written, and the line that names the machine's host in every transcript was the
evidence nobody read.

**Commit.** `48b0deb`

## The tokeniser's separators were a list, and 中文 punctuation was not on it (2026-09-23)

**Symptom.** `D51：会话` tokenised to `d51：` and `会话` — a term with a fullwidth colon glued to it,
which no index row has ever held. The service's own excerpt could not find it, and convergence
counted `d51：` and `d51` as two words. Small, and the same shape as the 中文 search defect two
entries below: a Latin assumption in a place that had just been taught the other language.

**Root cause.** `Text.Separators` was a hand-listed ASCII string. Any list of characters is wrong
the first time text arrives with a character not on it, and the file's own header already said so
about the *three* lists it replaced — one list is the same failure with a smaller surface. FTS5's
`unicode61` never had the problem: its rule is *a letter or a digit is a word character; everything
else separates*, in any script.

**Fix.** `Text.Tokenize` splits by that rule (`char.IsLetterOrDigit`) instead of a list — hyphen and
underscore still separate, as the header's own example needs. The platform's `termsOf` already
splits on `[^\p{L}\p{N}_-]`, so the two agree on punctuation; its deliberate difference (a
hyphenated term marks as one) is held by its own test.

**Verify.** `TextTests.Tokenize_separates_on_fullwidth_punctuation_as_it_does_on_ascii`, seen red on
the list (`["d51：", "会话"]`) and green on the rule. 275 service tests.

**Commit.** `99eda22`

## The Projects page said "nothing indexed" three ways (2026-09-23)

**Symptom.** On the deployed family of twenty registrations, a repository present in the index with
a count of zero read *"0 entries · 0 local · 0 canonical"*; one the index had never seen read
*"nothing indexed yet"*; one outside the family read *"—"*. Three renderings of one fact, on one
page, and the first of them looked like a measurement.

**Root cause.** Two branches decided by two different tests — `counts` present, then `total > 0` —
written at different times against a fixture in which every repository had entries. The fixture
cannot show a vocabulary drift, because it has one repository.

**Fix.** `ProjectsView.tsx`: both cards render `projects.nothingIndexed` for *absent or zero*, and
the count sentence only for a count above zero.

**Verify.** A shell test with the three cases side by side, seen red (one sentence of three) before
the change. 483 web unit tests.

**Commit.** `99eda22`

## The status bar counted a rebuild half-fed, and kept the number (2026-09-23)

**Symptom.** After the index's schema bump, the deployed application's status bar read
*"555 entries · 7 repositories"* — and stayed there — on an index that held 1,050 across 17. Neither
the old count nor the new one: a number nothing else on the machine agreed with.

**Root cause.** A schema mismatch drops the index and the next refresh re-feeds it, repository by
repository, over several seconds — so the rebuild is **observable**, and the page happened to ask for
the summary seven repositories in. That is fine; what is not is that the answer was then cached
with no path to a second look. `useRepositories` sets no `staleTime` and no interval, and the
driver tick — the page's one heartbeat for *"the world may have changed"* — invalidated sessions,
quests and the driver, and not the index. Pressing *refresh index* refetched it (555 · 7 →
1,051 · 17 in one second), which is how the theory was confirmed and also why nobody would have
found it: the fix for the symptom is the button beside it.

**Fix.** `DRIVER_TICK` invalidates `keys.allRepositories` too (`ShellSignals.tsx`). One line, and
the existing tick test lists the key with the others.

**Verify.** The tick test extended. On the deployed application, the honest way: the page had cached
*1,051 · 17*; a re-index was triggered over the API, not the button (a new fix-log entry made it
1,052); **ten seconds later the bar read 1,052 · 17**, on the driver's tick, with nothing pressed.

**The trap to inherit.** 🔴 **A count taken during a rebuild is a snapshot of the rebuild.** Any
cache of a summary needs a refetch path that fires without a person, or the first partial answer is
the answer for as long as the window stays open.

**Commit.** `e3df788`

## Every Chinese query returned the whole corpus — 中文 search had never worked (2026-09-23)

**Symptom.** On the deployed application's real index, `记录` and `会话` each returned **41 hits in
the same order, the rules TEMPLATE first, the term in no excerpt** — indistinguishable from each
other and from any other Chinese query. `encoding` returned 26 with the term in 17 excerpts. The
platform ships 简体中文.

**Root cause, in two layers.** `Text.Tokenize` drops any token of two characters or fewer — right for
*of* and *a*, wrong for a script in which two characters is a whole word. A Chinese query therefore
produced **no terms**, and `SqliteKnowledgeSearch` treats no terms as the browse an *empty* query
gets: `SELECT … ORDER BY repository, title`. Every Chinese query was that browse. Clearing the floor
exposed the layer beneath: FTS5's `unicode61` tokenizer **keeps a run of ideographs as one token** —
there are no spaces between words in 中文 — so `每个会话都留下一份记录` was indexed whole and a
phrase query for `记录` matched nothing inside it. With the floor fixed alone, every Chinese query
would have returned *nothing*, which is the same defect in a quieter coat.

🔴 **Neither layer can show itself on a fixture.** The fixture has no Chinese prose, and the platform's
own Playwright suite proves the *interface* speaks 中文, which is a different claim from the *index*
finding it. The test that caught it was written for the excerpt marker in the view, in the other
language — it failed on the two-character floor, the same floor was then found in the service, and
the service's test cleared it and returned empty instead of wrong.

**Fix.** `Text.Segment`: every run of ideographs is cut into its overlapping two-character bigrams,
space-separated — the standard answer to CJK search without a dictionary; index and query are cut
the same way so they meet. Applied to the FTS row at write time (`SqliteKnowledgeStore`, schema **3**,
so every existing index rebuilds on open) and inside `Tokenize` (so lexical scoring, the MATCH
builder and excerpt terms all use the same units); never to the stored body, which an excerpt and
the Reader read as written. `Tokenize`'s floor keeps a token in a short-word script whatever its
length. Bigrams of one query are joined with OR like every other term, so a three-character word
ranks documents holding both bigrams above those holding one.

**Verify.** Watched failing twice — browse (*"contained 2 items"*), then empty (*"collection was
empty"*) — then service 274, with nine tests on the segmenter and the tokeniser. On the deployed
application after the rebuild: `中文` → **23 hits, the term in all 23 excerpts, and exactly 23 of
974 indexed entries contain it**; `委托` → 1, centred; `简体中文` → 24, with the full term in 5 and a
bigram in the rest. `记录` → 0, and **0 of 974 entries contain it** — the 114 family files that do
are not indexed kinds. Before: 41, 41, and 41.

**The trap to inherit.** 🔴 **"No usable terms" is not the same as "no query".** A fallthrough to
browse is right for an empty box and catastrophic for a query the tokeniser silently emptied —
because the result looks like success. And a text index is only as multilingual as its tokenizer:
`unicode61` is not a CJK segmenter, and nothing about a green English suite says otherwise.

**Commit.** `7faa2de`

## The sessions frame said "nothing" over four real session records (2026-09-23)

**Symptom.** The Work frame — the frame whose subject is sessions — opened on the deployed
application with four sentences saying there was nothing: *Nothing is running*, *Nothing attended*,
*Choose a session in the rail…*, *Attend a session and its console streams here.* The machine held
four session records from the first deployment's driven runs, each with a transcript on disk.

**Root cause.** The rail fetched closed records (`includeClosed: true`) and then filtered to
`SESSION_ACTIVE` states, keeping a closed one only if it was **already selected** — so a session
that ended while you watched stayed listed, and one that ended before the window opened was
reachable exactly never. The test that held this in place asserted a finished record was absent and
*"left to the views that review them"*; no such view existed. The rail was the only door to a
session.

🔴 **It contradicted the design it was built from.** Working-surface design §7: *"after a restart the
records are the service's and the transcripts are on disk … read when a surface next opens."* The
fixture never showed it because the fixture has one parked conversation and nothing that has ever
ended.

**Fix.** `partition` (`rail.ts`): live sessions grouped by repository as before; beneath them an
**ended** section, newest first, capped at twelve with the remainder counted. No repository headers
there — those carry live facts (drivable, held, which tree is busy) an ended session has none of.
The full empty state appears only when both lists are empty; a machine with no live session but a
record to read gets a one-line *Nothing is running* above the records rather than a panel saying
the records are nothing. The attended-stays-listed rule is untouched.

**Verify.** Six tests on the partition and three on the rail, including the rewritten one. On the
deployed application: the four runs listed as `failed 1m · failed 9m · stopped 2m · failed 7m` —
the case study's own table — and choosing one opens its record. Web 472 + 14 Playwright.

**The trap to inherit.** 🔴 **An empty state rendered over existing records is a misreading, not an
absence.** Any list that filters to "live" needs to answer what happens to the rest, and "some
other view" is not an answer until that view exists.

**Commit.** `39503dd`

## A 990-character template was "substantially the same words" as a 326 KB document (2026-09-23)

**Symptom.** The Convergence view — the knowledge half's lead view — opened on the first real index
with a group at similarity **1.000** holding a rules `TEMPLATE`, a `dev-conventions` rule and a
`pitfalls` knowledge document from three repositories, and the sentence *"A copy that has drifted."*
Thirteen of the forty-eight groups were that shape.

**Root cause.** Restatement scores **containment over token sets, normalised by the smaller set**:
`|A ∩ B| / min(|A|, |B|)`. That was a deliberate choice, so a copy that grew a paragraph still
scores — and it has a failure mode nothing in a fixture can show. A short document made of common
words (*rule*, *why*, *reason*, *apply*, *sessions*) has a vocabulary any long document covers
entirely, so it is "fully contained" in every tome on the index. The fixture's longest entry is a
paragraph; the real index has 100 KB and 326 KB documents, and against those a template scores 1.0
with anything.

🔴 **Measured before it was fixed**, across every group on the real index: the 13 false groups all
had a vocabulary ratio (smaller ÷ larger) at or below **0.09**; the 35 genuine ones — every one the
same file in two repositories — were all at or above **0.46**. Nothing sat between. That gap is what
made the fix a number rather than a heuristic.

**Fix.** `Comparable(a, b)`: the smaller vocabulary must be at least **0.25** of the larger before
containment is consulted at all (`RestatementSizeFloor`, `ConvergenceDetector.cs`). A copy may
grow fourfold and still be found; a template against a tome is not compared. Two tests: the
pathology, reproduced as a template inside a document of ten times its vocabulary, and the case the
guard was written beside — a copy that grew by a paragraph is still a restatement.

**Verify.** Watched failing first (`Assert.Empty() Failure: Collection was not empty`), then service
264. On the deployed application: **36 groups, none below 0.25, minimum 0.463**, and the lead group
is two identical templates in two repositories — which *is* a copy. Query time unchanged (4.1 s).

**The trap to inherit.** 🔴 **A similarity normalised by the smaller side needs a size floor, or every
small generic thing matches every large thing.** The same shape as Jaccard-versus-containment
arguments everywhere; what makes it a trap is that it is invisible until the corpus has both a very
short and a very long document, which a fixture never does.

**Commit.** `9fd67b1`

## Adding a tooltip broke stories that had nothing to do with it (2026-09-22)

**Symptom.** Giving a component a `Tip` turned unrelated stories red — *"`Tooltip` must be used
within `TooltipProvider`"* — from `stories.test.tsx`, which renders every story as a smoke test.
Twice in one session: the status bar's items, then the toast's clamped text.

**Root cause.** `composeStories` applies each story's **own** decorators and the project
annotations it has been given — and it had been given none. So `.storybook/preview`'s globals never
reached the test, and the provider the application mounts once in `main.tsx` was absent for every
story. Both times the apparent fix was "wrap this story", which is the wrong fix twice: the suite
exists to render *what a reviewer sees*, and it was rendering something slightly different.

🔴 **The suite was right and its harness was wrong**, which is the awkward case — a green story in
Storybook and a red one in the test look like a flaky test rather than a missing global.

**Fix.** The provider becomes a project decorator in `.storybook/preview.tsx`, and
`src/test/setup.tsx` calls `setProjectAnnotations(preview)` so `composeStories` renders through it.
Both files gained a `.tsx` extension to carry the JSX.

**Verify.** `npx vitest run src/stories.test.tsx` — 96 stories, including the two that had been
wrapped by hand, with the hand-wrapping no longer load-bearing. The next component to gain a `Tip`
needs nothing.

**Commit.** `c58591a`

## A re-published install kept every bundle it had ever served (2026-09-22)

**Symptom.** Listing a deployed install's `wwwroot/assets` showed **seven** hashed JS bundles. It
produced a wrong answer about which build was live — twice: once in the first-deployment case study,
and once while re-publishing to that same machine afterwards, by the session that had read the case
study warning about it an hour earlier.

**Root cause.** `dotnet publish` does not clear its output directory, so `desktop-publish.mjs`
published the HTTP host *over* the previous one and every earlier hashed bundle stayed. Nothing is
actually broken by this — `index.html` names the current bundle and the host serves it — which is
why it survived: the folder is **correct and unreadable**, and the only thing it costs is the
judgement of whoever reads it.

🔴 **The sibling had already fixed it.** `service-publish.mjs` does `rmSync` then copy, with a
comment naming this exact failure: *"actively misleading to anybody trying to tell which build is
live by listing the folder. That is exactly how a stale deployment was diagnosed the slow way once."*
Two scripts doing one job, one of which learned. That is the counterpart-set shape this repository
keeps meeting — the install layout vs the locator's candidates (2a), the declared gates vs the
workflow, the toolchain twins — and it is the reason the fix is a gate rather than a second comment.

**Fix.** `rmSync(host, { recursive: true, force: true })` before the host publish in
`desktop-publish.mjs`, mirroring its sibling.

**Verify.** `npm run rehearse:deploy` seeds a decoy bundle into a published install, re-publishes
into it, and asserts the decoy is gone and that `index.html` names a bundle the install actually has
— watched failing first (`index-STALEBUNDLE.js, index-yAgtBJUV.js`), then 32/32. The machine's own
install went from seven bundles to one on the next publish.

**Commit.** `f873d16`

## Three real driven runs failed on a flag the repository could not set (2026-09-22)

**Symptom.** A driven session did the work — source, tests, decision records — and then recorded
`failed — exited without touching its quest`, three times, nine minutes and a real login each. The
repository's `.claude/settings.json` allowed exactly the tools it needed.

**Root cause.** The harness said it on the transcript's first line: *"Ignoring 9 permissions.allow
entries from .claude/settings.json: this workspace has not been trusted."* Claude Code ignores a
repository's allow-list until a **person** has accepted that path, recorded per-path in the harness's
own config under the profile — not in the repository, and not anywhere Daoris may write. So the chain
was: driver instructs the session to claim its quest → ACP4 makes the tools reachable → the
repository allows them → the harness drops the allow-list unread.

🔴 **Why it had never been seen.** DRV4's scratch repository had been opened by hand during that
session, and its archive entry names the dependency in a parenthetical — *"its own
`.claude/settings.local.json` trusting it"*. The claim it confirmed is true **given a trusted
workspace**, and that qualifier lived in one clause nobody was reading as a precondition.

**Fix.** `ClaudeTrust` reads the record and the driver **holds** before spawning, naming the path, the
one command, and what it costs. Not written — that flag is the person's grant, and a tool that set it
on their behalf would remove the only step in the chain that was theirs. Declared per harness
(`HarnessToolchain.TrustFile`), so a harness with no notion of trust is unaffected.

**Verify.** Eleven tests, including the case that actually bit: a path the harness has never recorded
is **untrusted, not unknown**, because it prompts on first visit. Then against the real tree — the
driver held with the sentence instead of spawning.

**The trap to inherit.** 🔴 **Only a definite NO refuses**, and absent is not always unknown: for a
login the harness cannot answer, absent means unknown and is permissive; for a trust prompt that
appears on first visit, absent means never-accepted and is a definite no. Two absences, opposite
readings, and the difference is whether the thing records its own negatives.

**Commit.** `7ff0e04`.

## A session transcript was decoded as the machine's ANSI codepage (2026-09-22)

**Symptom.** The first real driven run's transcript held `鈥?` where every em-dash belonged. Raw
bytes: `e9 88 a5 3f`, where the session had written `e2 80 94`.

**Root cause.** `e2 80 94` read as **CP936** is `鈥` + an unmappable byte, and that is what got
re-encoded to UTF-8 on the way to the file. .NET defaults a redirected stream to the **console's**
codepage, and nothing in `Spawning` set `StandardOutputEncoding`. On an English machine the default
is close enough to ASCII that a transcript looks right; on this one it is not.

🔴 **The failure is silent and permanent.** The file afterwards is valid UTF-8, so nothing downstream
can tell it was ever wrong — no parse fails, no gate trips, and the original bytes are gone. And it is
worse than a mangled dash: this platform ships **简体中文**, so a transcript that cannot carry a dash
carries no Chinese at all.

**Fix.** `StandardOutputEncoding` and `StandardErrorEncoding` set to UTF-8 in `Spawning.Shell` — the
one place every adapter goes through, which matters because the **protocol door parses JSON-RPC off
the same stream**, so this was never only a cosmetic problem.

**Verify.** A theory over all six adapters in `Acp3AdapterTests`, watched failing on every one first.

**The trap to inherit.** 🔴 **Redirecting a stream chooses an encoding whether you say so or not.**
Anything that reads another program's output states UTF-8 explicitly — and the check belongs on the
`ProcessStartInfo`, not on the text, because by the time the text is wrong it is indistinguishable
from text that was always that way.

**Commit.** `bd19905`.

## The shell looked for the installed HTTP host where the installer never puts it (2026-09-22)

**Symptom.** None, on any developer machine — which is the whole entry. A machine with the service
correctly installed (`npm run publish:service -- --install`) still had `ServiceHostLocator` fail to
find its HTTP host, and the shell silently fell through to the **workspace build** instead. Found by
deploying the desktop to a real folder and asking what it would locate from there.

**Root cause.** The installer gives the HTTP host a **directory of its own** —
`~/.daoris/bin/daoris-knowledge-http/daoris-knowledge-http.exe` — because its `wwwroot` bundle has to
travel beside the executable, while the MCP host installs flat as `~/.daoris/bin/daoris-knowledge.exe`.
The locator only ever built the **flat** candidate, for both. The publish script even prints the nested
path on success, so the two halves disagreed in writing and nothing compared them.

🔴 **The fallback is what hid it.** The locator's third candidate is the workspace build, which exists
on every machine this was ever run on, so it always found *a* host and nobody asked which. On a
deployed machine there is no workspace to fall through to and the shell reports no host at all — the
one place the bug is fatal is the one place it had never been run.

**Fix.** `ServiceHostLocator.Candidates` gains the packaged path after the flat one (a hand-placed
binary stays the more deliberate of the two), and a third for the copy a deployed shell carries beside
itself — otherwise `desktop-publish --service` would publish a host nothing looks for, which is a flag
that is a claim nothing reads.

**One test had to change, and its reason is the lesson.** `No_workspace_means_no_dev_candidates_and_no_crash`
asserted `Assert.Single(candidates)`. It said *one* while meaning *nothing from a workspace*, so every
correct new candidate read as a regression. It now asserts the property it was always about.

**Verify.** `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests` — 276, with the new case
watched failing (`Assert.NotNull() Failure: Value is null`) against the real installed layout first.

**Commit.** `5e73ce9`.

## A single-line fixture passed the CRLF test the multi-line case fails (2026-09-22)

**Symptom.** `region.ts` had fourteen green tests, including one for CRLF files. Run against a **real**
adopter's `AGENTS.md` — 133 CRLF lines — the body written into the region did not read back. Caught
before it landed, and only because the module was driven over real input rather than over a fifteenth
fixture.

**Root cause.** `findRegion` normalised the body with `.join('\n').replace(/\r$/, '')` — one strip, at
the very end. That is correct for a **one-line** body and wrong for every longer one: each interior
line came back still carrying its `\r`. The CRLF test used the body `'the tier'`, so it proved the
file's line endings survived and never proved the body round-tripped.

**Fix.** Strip per line — `.map((line) => line.replace(/\r$/, '')).join('\n')`. The line endings
belong to the **file**, which `writeRegion` preserves by detecting and re-using them; the body is
**content**, which is LF here like everything else. Two more tests: a multi-line body through a CRLF
file, asserting the round trip *and* that re-writing what was read changes nothing; and the refusals
on a CRLF file, because a marker comparison that forgot the `\r` would read every marker in a Windows
checkout as prose and silently append to a damaged region instead of refusing.

**Verify.** 17 tests, and the real file: body round-trips, idempotent, the adopter's bytes are a
byte-for-byte prefix, and the file is still CRLF throughout.

**The trap to inherit.** 🔴 **A fixture with one line proves nothing about line endings.** The bug
lives *between* lines, so any test whose input has no interior line boundary cannot see it — and it
will be green, which is worse than absent. This is the third line-ending assumption in this
repository (D25 was the first; the fixture-vs-real gap is the same shape as `examples/` existing at
all). Anything that reads or writes multi-line text gets a multi-line fixture with the other
platform's endings, and gets run once over a real file before it is called done.

## The presence probe asked about a different binary than the spawn would run (2026-09-22)

**Symptom.** ACP2's driven run refused: *"`claude-code-acp` is not installed on this machine, so there
is nothing to spawn"* — about a harness whose pin `daoris harness list` printed on the very next line,
and which answered `--version` when run by hand. The same roster reported `codex` absent on a machine
with `@openai/codex` installed globally.

**Root cause.** Twin rule 4 — *the binary is the explicit command, then the managed pin, then `PATH`*
(TOOL2/D57) — was implemented where a session **spawns** and not where presence is **decided**. Both
twins probed `toolchain.binary` against `PATH` while resolving the pin separately, so the driver
resolved a binary correctly and then vetoed it on an answer about a different program. The CLI twin
failed the other way round and worse: with a pin set it reported the machine's own `claude` **as the
pinned one** — the silent substitution the pin exists to prevent, arriving through the presence
question instead of through the spawn.

Underneath it, on the Node side only, the second half of the Windows trap from the entry below: `ask()`
still called `spawnSync` directly, so a managed pin's `.cmd` shim could not be probed at all. That is
what hid `codex`. .NET's `Process.Start` runs a `.cmd` with `UseShellExecute = false`, measured — so
the driver never had this half.

**Fix.** Both probes resolve the pin before asking, and both refuse a pin with nothing installed at it
by name rather than falling back to `PATH`. The CLI's `ask()` goes through `spawnable()`, and the login
question is asked of **the same binary the version came from** — asking the pin whether it runs and
then asking `PATH` whether it is logged in answers about two different installs.

**Verify.** Four tests, watched failing first: a pinned harness probes as present *on the pin* and a pin
with nothing behind it probes as absent, in `toolchain.test.ts` and `HarnessTests.cs`. By hand,
`daoris harness list` now reports `claude-code-acp 0.79.0` and `codex codex-cli 0.155.1` where both
said "absent".

**The trap to inherit.** 🔴 **A rule implemented at one of its two call sites is not implemented.**
Resolution and presence are the same question asked twice, and the driver's own code computed both
and compared them without noticing they disagreed. Where a rule decides *which* thing, every predicate
about *that* thing has to be asked of the resolved one — and the fixture has to be honest about it:
this fix broke two selection tests whose managed shim was an empty file, which was true enough while
nothing executed it.

## A scratch host with no root of its own adopts the machine's whole family (2026-09-22)

**Symptom.** ACP2's proof run hung with nothing on screen. Asked directly, the scratch host at
`localhost:5201` answered a registry containing the developer's **neighbouring repositories** — real
sibling projects the run had never heard of, beside a fixture family of two.

**Root cause.** Two defaults, both correct on their own. The host's knowledge root defaults to *the
parent of its workspace* and its index to `~/.daoris/knowledge.db` — which is exactly right for the
machine's own service, whose job is to index the family. The proof script set neither (it passed
`DAORIS_STORE`, which is not a variable anything reads), so a **scratch** host came up pointed at the
**real** family root and the **real** index, read every sibling's `.claude/` documents and decision
logs, and refreshed the machine's shared database with them. Nothing was written into any sibling
repository and nothing left the machine — but a fixture run had reached well outside its fixture.

The hang was separate and made it invisible: every read in the script was a bare `fetch`, which has no
timeout, so a host that accepts a connection and answers slowly stalls the run with no output at all.

**Fix.** `tools/acp2-proof.mjs` now passes `DAORIS_KNOWLEDGE_ROOT`, `DAORIS_KNOWLEDGE_DB` and
`DAORIS_REMOTE_CONFIG` into its scratch directory — the same confinement `tools/family-rehearsal.mjs`
has always applied. Every read goes through a bounded `ask()`. And a **guard** reads the registry back
before a quest is published or a model is spent: a scratch host that can see a repository this run did
not create is pointed at the wrong world, so the run refuses and says which names it found.

**Verify.** `node tools/acp2-proof.mjs --drive` reports *"the host sees this scratch family and
nothing else"*, and its registry holds only `proof-asker` and `proof-repo`.

**The trap to inherit.** **A default that is right for the product is wrong for a fixture**, and it
fails silently in the one direction nobody checks — outwards. Anything that spawns the service for a
test names its root and its store, and then *asserts what it can see*: confinement you set and never
read back is confinement you are hoping for. The guard is the half that matters, because the env
variable was misspelled and a misspelled variable looks exactly like a correct one.

## `daoris harness install` has never worked on Windows (2026-09-22)

**Symptom.** `daoris harness pin claude-code-acp 0.79.0` — TOOL2's new verb, on its first real use —
answered *"`npm` could not be run — spawnSync npm ENOENT"*, on a machine with npm plainly installed.

**Root cause.** Two Windows facts in a row, and `install` and `update` have carried both since they
were written. **`npm` is `npm.cmd`**, and `spawnSync` with `shell: false` does not resolve `PATHEXT`
— so it answers `ENOENT`, which reads as "npm is not installed". Resolve the extension and the second
lands: **Node refuses to spawn a `.cmd` or `.bat` without a shell at all** (the 2024
argument-injection fix, CVE-2024-27980), answering `EINVAL`. Between them, every npm-shaped mechanism
this module declares was broken on Windows — silently, because nobody had run one: the gates never
install a harness, deliberately (D49 §4 keeps install, update and login out of every gate as person
actions).

**Why `shell: true` is the wrong fix, twice.** Node does not quote arguments for it on Windows, so a
configuration home under `C:\Users\<a name with a space>\` breaks; and it would put a version string a person typed
onto a command line `cmd.exe` parses, where the directory-name check that guards a pin does not refuse
`&`.

**Fix.** `spawnable()` in `toolchain.ts`: resolve the shim through `PATHEXT`, and for a `.cmd`/`.bat`
build the `cmd.exe /d /s /c` invocation explicitly with `windowsVerbatimArguments`, quoting every
token itself. A token containing `"`, `%` or a newline is **refused** rather than escaped — every
argument here is a path or a `package@version`, none of which legitimately contains one, and
"escaped correctly for cmd" is a claim nobody should have to verify.

**Verify.** `daoris harness pin claude-code-acp 0.79.0` installs 105 packages into a directory Daoris
owns and records the pin. Run against a scratch `DAORIS_HARNESS_CONFIG`, so the developer's real
`~/.daoris` was untouched.

**The trap to inherit.** **A mechanism no gate runs is a mechanism nobody has run.** These three were
kept out of the gates for a good reason — they install software and spend logins — and the cost of
that decision is that their first real execution is by a person, in anger. Where a gate cannot run
something, the first manual run is the test, and it should happen before the feature is called done
rather than months later.

## `desktop kill` orphaned the host it was supposed to stop (2026-09-22)

**Symptom.** A `npm run desktop -- build` failed with MSB3027 — seven `daoris-knowledge-http`
processes from this checkout's Debug build were holding `Daoris.Service.Core.dll`. They had
accumulated silently over a session of `run`/`kill` cycles.

**Root cause.** `stopAll` closes the shell rather than killing it precisely so the app's own shutdown
path runs — that path is what stops the HTTP host the shell spawned and owns. It used
`Process.CloseMainWindow()`, which closes whichever window **Windows** calls main. **SURF8 made that
not necessarily the application's**: with the monitor open, `CloseMainWindow` closed the *monitor*,
the app kept running, the 8-second wait expired, and `Stop-Process -Force` landed — killing the shell
before it could stop its host. A regression from the commit that introduced secondary windows, found
two commits later by its second-order symptom rather than by the kill itself, which reported success
every time.

**Fix.** Close repeatedly, re-reading `MainWindowHandle` each time: the secondary windows go first,
the main window last, and the app exits on its own terms. Six attempts at 2.5s, then the force kill
as a genuine last resort. `tools/desktop.mjs`.

**Verify.** `run`, open the monitor, `kill`, then `Get-CimInstance Win32_Process` for
`daoris-knowledge-http` — none left from this checkout, and the next `build` succeeds.

**The trap to inherit.** This is the **third** disguise of one fact: `MainWindowHandle` answers for
one window and Windows chooses which. The other two are in the SURF8 entries below. When a change
makes a process multi-window, every caller of that property is a caller that has silently changed
meaning — and the ones that *report success anyway* are the expensive ones.

## A second window opened, and then failed its bring-up on a thread-affine environment (2026-09-22)

**Symptom.** The monitor window (SURF8) opened with its native frame, and its content was one
sentence: *"CoreWebView2Environment members can only be accessed from the UI thread."* The main
window was unaffected, nothing was logged, and the build was green.

**Root cause.** A `CoreWebView2Environment` is **affine to the thread that created it**, and
`SecondaryWindows` runs every window on its **own STA thread with its own message pump** — which is
the whole reason that API exists. The new form was handed the DI-registered
`WebViewEnvironmentOptions` singleton, which is correct, but `WebViewHost` defaults to
`UseSharedEnvironment = true` — the process-wide environment, created by and for the **main UI
thread**. So the first call into it from the window's own thread threw.

**Why nothing caught it.** Nothing could. There is no test in this repository that starts a second
STA pump with a real WebView2 in it, and there will not be: the failure is a property of two real
threads and a real browser process. It was found by opening the window and looking at it, which is
what the standing polish direction is for.

**Fix.** `UseSharedEnvironment = false` on the secondary host's options — the framework's documented
answer, which its own thread-affinity note names for exactly this case. Same options and same
user-data folder, so the two environments still share one browser process; it costs a handle, not a
browser. `src/Daoris.Desktop/Daoris.Desktop.App/SecondaryForm.cs`.

**Verify.** `npm run desktop -- run`, open the monitor from the palette, and the window renders the
platform. Captured in both themes.

**The trap to inherit.** **A "process-wide" singleton is a claim about a thread**, and a framework
that hands out per-thread constructors is telling you so. Before sharing anything into a window with
its own pump, look for the affinity note — this one was written down in the API documentation and was
simply not read before it was needed.

## A secondary window wore the old theme, because `SET_THEME` belongs to exactly one window (2026-09-22)

**Symptom.** With the OS in dark mode the monitor window's page was dark and its **title bar was
light**. The main window was correct in both.

**Root cause.** The main window follows the OS theme by being **told**: the page pushes `SET_THEME`
over `WindowCommandModule`, and the form re-applies the DWM border, the fill and the dark-mode flag.
`WindowCommandModule` targets one form and its module name (`SHENORA.WINDOW`) is **reserved and
singular** — recorded in D55 §b as the reason secondary windows keep their native frame — so a
secondary window's page has no channel to its own frame. The form read the OS theme once, at
construction, and never again.

**Fix.** The secondary window follows the OS **directly**:
`SystemEvents.UserPreferenceChanged` → `ApplyChromeTheme`. Two details that are the whole fix:
marshalled with `BeginInvoke` and never `Invoke`, because the notification arrives on the
system-events thread and a blocking marshal into a window with its own pump is the deadlock the
framework warns about; and unhooked on `FormClosed`, because `SystemEvents` holds a **static**
handler list, so every monitor the person ever opened would otherwise stay alive handling theme
changes.

**Verify.** `npm run desktop -- shot --window monitor --theme dark|light`, both read correctly.

**The trap to inherit.** **When a mechanism is documented as singular, everything downstream of it is
singular too** — and the second instance does not fail, it just quietly stops being updated. Ask what
*tells* a component its state, not only what renders it.

## The dev instruments addressed whichever window Windows called main (2026-09-22)

**Symptom.** With the monitor open, `shot` photographed the **main** window while reporting success,
and `eval` attached to the monitor's page and then refused with *"the page on 9333 is not this
shell"*. Two consecutive `shot` runs with the same windows open returned different windows.

**Root cause.** Both instruments were written when the shell had exactly one window and one page, and
both said so — `shot-window.ps1` used `Process.MainWindowHandle`, which answers for one window chosen
by **Windows**; `pickPageTarget` took the first non-devtools page and carried the comment *"the shell
has exactly one page"*. SURF8 made both false. The `eval` refusal was the identity check working
correctly on the wrong page: the monitor's URL carries `?window=monitor`, which is not the origin the
run recorded.

**Fix.** `--window <monitor|session:ID>` on `shot`, `eval` and `click`. One flag for both layers
though they address different things — `pickPageTarget(targets, window)` matches the page whose URL
carries that `window` parameter (and null means the page with none), while the capture enumerates the
process's visible top-level windows with `EnumWindows` and matches the caption. A name that matches
nothing is **refused**, never a fall back to the main window: the point of asking is that the main
window is not the one wanted. `tools/desktop.mjs`, `tools/cdp.mjs`, `tools/shot-window.ps1`.

**Verify.** `node tools/desktop.mjs eval --window monitor "document.querySelector('h1').textContent"`
answers `"Monitor"` and the same call without the flag answers `"Overview"`.

**The trap to inherit.** **An instrument encodes the shape of the thing it measures**, and when that
shape changes the instrument does not fail — it answers about something else. Both of these carried
the old assumption in a comment, which is the cheapest possible place to have found it and the last
place anybody looks.

## The design language claimed `aria-modal` and no dialog had ever had it (2026-09-22)

**Symptom.** A test written for the new command palette asserted what D41 §6 says every drawer is —
`role="dialog"` **with `aria-modal`** — and got `null`. Not a palette bug: the existing drawer, which
that sentence was written about, had never had the attribute either.

**Root cause.** Radix's `Dialog.Content` writes the role, portals the content, traps focus and handles
ESC — but it does **not** write `aria-modal`. `grep -c aria-modal` in the installed package returns
**0**. The platform relied on the library for something the library never claimed to do, and the
design document asserted it as fact.

**Why nothing caught it.** Nothing reads prose. D41 §6 is a design document, the drawer's own tests
covered its behaviour (opens, closes on ESC, traps focus) and not its ARIA surface, and a missing
`aria-modal` breaks nothing visible — a sighted person notices no difference at all. It is the exact
shape `claims-need-checks` describes: the claim and the enforcement are written at different moments
and only the claim is easy.

**Fix.** Both modal surfaces set `aria-modal="true"` explicitly, and `ui.test.tsx` now asserts it for
each. The test is written as "every modal surface says it is modal" so the next one is caught by the
act of not being listed. Removed the attribute from the drawer and watched the assertion go red before
believing it.

**The trap to inherit.** **A behaviour you get from a library is a claim about the library**, and the
one place it will not be checked is the document that states it most confidently. When prose in a
design record says a component *is* something, the cheap move is to grep the dependency for the thing
before believing it — and the durable one is a test that reads the attribute, because the next
library upgrade is another chance for this to become false silently.

## git walks UP, so a diff of a session tree nearly showed the parent project's work (2026-09-22)

**Symptom.** Caught before it shipped, by a test written in the same hour: `WorkingTree.DiffAsync`
pointed at a directory that is not a git repository returned a **clean exit code and a diff**, where
it was supposed to return null. The test asserted null and went red.

**Root cause.** `git` searches upward for a repository. Run it in any directory that sits inside one
and it answers for the ENCLOSING repository — exit 0, real output, nothing to suggest the answer is
about somewhere else. So a session whose tree had been discarded, or a checkout that was never a
repository, would have produced a diff of whatever project contained it.

**Why that is worse than it sounds here.** The desktop's own example family are plain directories
under this repository's gitignored `_fixtures/`. Opening *Review* on a session in one of them, before
the guard, showed **Daoris's own last commit** — four files from the previous landing — rendered as
what the `engine` session did. Confirmed by hand:
`git rev-parse --show-toplevel` in that session tree answers with the Daoris repository root.

**Fix.** Ask git for `rev-parse --show-toplevel` first and refuse unless it names the path being
diffed. That rejects a non-repository, a deleted tree, and a SUBDIRECTORY of a real repository — the
last of which git would have happily answered by silently narrowing the diff to that subtree. The test
now covers all three, including the plain-directory-inside-a-repository shape that made this real.

**The trap to inherit.** **A process that searches upward has no failure mode you can detect from its
exit code.** git, and every tool like it, will answer a question about somewhere else rather than
refuse — so a command run in a path the caller does not control needs the path CONFIRMED, not just
the exit code checked. And it is the `reaching-in` lesson in read-only form: the damage from reading
the wrong repository is attributing its work to someone who did not do it.

## A responsive rule outlived the thing it was written for, and stacked the icon rail on top of the page (2026-09-22)

**Symptom.** On a browser window under 768px the 48px activity bar became a **48×276 column above the
content**, leaving the page **105px** of height. Found by opening the platform in Chrome and resizing
it — not by any suite.

**Root cause.** `App.tsx`'s layout row carried `max-md:flex-col max-md:overflow-y-auto`. That rule was
written for the **15rem labelled sidebar** (D41 §2: *"the sidebar becomes a top bar — nothing is
hidden behind a hamburger; five items fit"*), and it worked because the sidebar answered it with
`max-md:w-full max-md:flex-row`. SURF10 retired the sidebar and replaced it with a 48px `ActivityBar`
that has **no responsive rules at all** — correctly, since a 48px rail needs none. The container's
half of the old pair stayed, so the stacking still happened and the rail had nothing to answer it
with.

**Why no gate caught it.** vitest has no layout, and every Playwright case ran at the default
viewport. The regression lives **only** at a width nothing measured, which is the same shape as the
tooltip one the day before: a defect in the *geometry of the assembled page*, invisible to a check
that never assembles it at that size.

**Fix.** The rule is gone — one layout at every width, which is also what keeps D55's "a window, not
a page" true on a narrow screen rather than only on a wide one. A Playwright case sets a 680px
viewport and asserts the rail is **beside** the content (`main.x >= bar.right`, same `y`), that it is
still under 60px wide, and that the content keeps its height. Sabotaged by putting the rule back, and
watched fail on the first assertion.

**The trap to inherit.** **A responsive rule is half a pair, and retiring the other half leaves it
pointing at nothing.** When a layout element is replaced by one with different rules, grep the
container for breakpoint classes that were written to cooperate with the old one — they compile, they
render, and they are only wrong at a width nobody opens. And when a suite's cases all run at one
viewport, that viewport is the only one that has ever been tested.

## A tooltip swallowed clicks on the control it described, once that control moved (2026-09-21)

**Symptom.** `npm run test:web` went red on the workspace-scope test, in a landing that changed no
scope code at all: Playwright clicked the `studio` option sixty times over thirty seconds and each
attempt reported *"`<div role="tooltip">`… from `<div data-radix-popper-content-wrapper>` subtree
intercepts pointer events"*. The control worked by hand on a first try, which is what made it look
like a flake.

**Root cause.** Radix's `Tooltip` renders its content inside a **popper wrapper**, and with hoverable
content enabled — the default — that wrapper is given `pointer-events: auto` so a person can move the
mouse *into* the tooltip. The workspace switcher wraps its whole `SelectField` in a `Tip`. In the
sidebar's foot the select opened upward, away from the tooltip; moved into the app strip (D56) it
opens **downward**, straight under a tooltip that is still open from the click that opened it. The
tooltip then sat on top of its own options list and ate every click.

**Why no test caught it before.** Nothing had: the arrangement did not exist until the control moved.
The unit suite drives `WorkspaceSwitcher` in isolation with no strip above it, so the two poppers
never met — the defect lives in the *geometry of the assembled window*, which is exactly the class
Playwright over the shipped bundle exists to catch, and it caught it on the first run.

**Fix.** `disableHoverableContent` on `Tooltip.Root` inside `Tip` — in `ui.tsx`, once, for every
tooltip in the platform. Nothing here is meant to be hovered into; each `Tip` carries one sentence.
Adding `pointer-events-none` to the tooltip's own content was tried first and **did not work**, which
is the informative half: the class lands on the content and the wrapper is a different element.

**The trap to inherit.** **Moving a control changes which way its popup opens**, and a component that
was safe in one corner of the window is not automatically safe in another — a tooltip and a menu that
never overlapped can start overlapping with no change to either. And when a click retries for thirty
seconds against something *visible, enabled and stable*, read the interceptor the error names rather
than reaching for a wait: the message said which element it was, on the first run.

## A refusal printed local time and called it UTC, and a fixture with an expiry date hid it (2026-09-21)

**Symptom.** Five checks in the family rehearsal's "which commit speaks" phase went red on a run that
changed nothing near them. The refusal read: *"`atelier` is already fed from a newer commit
(`754c583c`, 2026-09-21 **20:34Z**…)"* — on a machine whose UTC time was 10:34.

**Two root causes, and the second hid the first.**

The **fixture had an expiry date.** The phase fed a hard-coded `2026-09-21T10:00:00+00:00` as the
"newer" commit and compared it against a commit the driver had just made. That holds only while the
rehearsal runs *before* 10:00 UTC on that date. It passed all morning and aged out at lunchtime, with
nothing wrong in the product — the same class as a test that only passes on one machine, and the
reason it took a careful read rather than a bisect to believe.

The **sentence was wrong about time.** `{held.CommittedAt:yyyy-MM-dd HH:mm}Z` formats a
`DateTimeOffset` **in its own offset** and then hard-appends `Z`. A commit carries the committer's
offset, so the message printed a local wall clock and labelled it UTC. The ordering underneath was
right — the comparison uses the real instants — and only its *explanation* was wrong, which is the
worst shape for a sentence whose entire job is to convince a person that being refused is fine.

**Why no test caught it.** Every case in `FeedTests` fed `Z` times, so the offset was always zero and
the formatting was never exercised. The bug needed a non-zero offset to become visible, and nothing
in the suite had one.

**Fix.** `.UtcDateTime` before formatting, with a test that feeds `22:30+10:00` and asserts the
message says `12:30Z` and *not* `22:30Z` — watched failing first. The rehearsal's timestamps are now
relative to the run (`hoursFromNow(1)`, `(-48)`, `(24)`) instead of wall-clock literals.

**The traps to inherit.** A fixture that encodes an absolute date encodes an expiry date; make times
relative to the run unless the test is *about* a specific instant. And **a format string that appends
a timezone letter is a claim** — if the code writes `Z`, something has to convert to UTC, and the
only way to know it does is a test whose input is not already UTC.

## The publish build had been broken for two commits, and every gate was green (2026-09-21)

**Symptom.** `npm run rehearse` — the "would a release work?" gate — died at its first step, `npm pack`,
with `src/connect.ts(10,10): error TS6133: 'dirname' is declared but its value is never read.` Found
only because the budget work changed `check`'s exit semantics and the rehearsals were re-run to prove
nothing else moved.

**Root cause, in two halves.** The import went unused in SURF3 (`476a8a0`), two commits earlier. And
**nothing in the loop typechecks**: Node 24 strips types, so `node --test` runs the sources without
compiling them, and `npm run verify` ran the suite, `check`, the budgets and the version agreement —
none of which invoke `tsc`. The compiler only runs inside `prepack`, which only runs when packing, so
a type error reaches a release and nothing before it. `npm run typecheck` existed in the package all
along and was wired to nothing — and had itself never been green, failing on five test-tier errors
from importing an untyped workspace tool.

**Fix.** Remove the unused import; make the test-tier errors go away at their one site (an
`@ts-expect-error` on the untyped `.mjs` import and three annotated callbacks, rather than a
hand-written declaration that would be a second description of the tool to keep in step); and wire
`npm run typecheck -w daoris` into `verify` as its **first** step, so the cheapest and most
fundamental check fails first. Watched failing by re-introducing the exact import: the gate names the
file, the line and the symbol.

**The trap to inherit.** This is the fifth shape in `claims-need-checks` — *nothing runs the check* —
found the day it was written into the canon, in the same repository, about a different tool. A
compiler in the dependency tree is not a gate; a script in `package.json` is not a gate; **only what
the command people actually run invokes is a gate.** The tell was available and unread: a `typecheck`
script that no other script named.

## The desktop dev loop orphaned a service host on every restart (2026-09-21)

**Symptom.** `dotnet build src/Daoris.Service/Daoris.Service.Http` failed on a file copy: MSB3027,
"the file is locked by: daoris-knowledge-http (22216), daoris-knowledge-http (20600)". Two hosts from
this workspace's build were running with no window anywhere — and nobody had started them by hand.

**Root cause.** `tools/desktop.mjs kill|restart` stopped the shell with `Stop-Process -Force`. The
shell **owns** the host it spawns (`OnStopping`: end the driver loop, then `HostSupervisor.Stop()`),
and a forced kill skips that path entirely, so each restart left a host behind holding its port, its
store and a file lock on the assemblies the next build must overwrite. Every run of the day's tooling
added one. Nothing reported it, because an orphaned host answers `/api/status` perfectly well.

**Fix.** Close the main window first — the same path a person's × takes — and wait; force only as the
backstop after eight seconds. The tool still **never touches a host directly**: a host it did not
start belongs to whoever did, which is the distinction the supervisor itself draws (adopt, never
double-start). `doctor` now names a host running with no shell of this checkout, and says whose it
might be, rather than killing it.

**The trap to inherit:** a forced kill is not "the same thing, faster" for any process that owns
another. The teardown *is* the feature. And the damage here was invisible until an unrelated build
failed, which is how orphan bugs are usually found — count what is running before assuming a stop
stopped something.

## Every desktop refusal reached the person as a generic failure (2026-09-20)

**Symptom.** Found by the first tests the shell's IPC modules had ever had. A module refuses by
throwing with a written sentence — "`aurora` needs both an address and a key", "unknown adapter
'codex' — one of: claude-code, stub", "the driver is still coming up" — and **none of those sentences
reached the page.** The bridge answered `UNKNOWN_ERROR` carrying one parameter: the exception TYPE.
Five deliberate refusals, plus every `DriverException` the driver library raises, all rendered as the
same blank failure.

**Root cause.** Two halves that each looked right. The host maps an *unhandled* exception to a generic
code by design — its contract is a structured `code` + `parameters`, translated client-side
(`errors.{code}`), with `Message` documented as "untranslated fallback for logs/dev; not for end
users". The page, meanwhile, showed `(error as Error).message` — correct for the HTTP service, whose
refusals are prose rendered verbatim, and wrong for the bridge, which was never speaking prose.
Neither half was obviously broken on its own.

**Why it survived.** The page's own suite mocks the bridge and asserts the mock's invented string, so
it proved the page renders what it is given and nothing about what it is given. The host half had no
test project at all — 1,128 lines of shell behind a `net10.0-windows` TFM. **The contract was asserted
on neither side**, which is what a mock agreeing with a mock always means.

**Fix.** Refusals are declared once (`Refusals`) and thrown as `ShenoraException` with a code and
parameters; `DriverException` is mapped once at the module boundary to `DRIVER_REFUSED` carrying its
message, so the driver's own sentences travel verbatim — the same class as the service's, which this
platform has always shown word for word. The page gained one `sentence()` helper: a coded rejection
becomes `errors.{code}` interpolated, anything else stays the message it already was.

**Verification.** 39 tests in the new `Daoris.Desktop.Modules.Tests`, asserting the *code* rather than
the English (a refusal identified by its sentence is one no other language can render), plus a
catalogue test that every code a module can raise has an entry in **both** locale files — watched
failing before the entries existed. **The trap to inherit:** when two halves are mocked against each
other, the mock is the specification, and it is one nobody wrote down. Test the side that decides.

## A login-state pattern that matched its own negation (2026-09-20)

**Symptom.** Caught by a test written alongside SES3's harness descriptors, before anything ran: the
`codex` login check declared `in: /logged in/i` and `out: /not logged in/i`. The probe asks `in`
first, so **every logged-out profile reported as logged in** — "Not logged in" contains "logged in".

**Root cause.** The two supported harnesses answer the same question in two shapes. `claude auth
status` answers a FIELD (`"loggedIn": true` / `false`), where the two patterns are mutually exclusive
and order is irrelevant. `codex login status` answers a SENTENCE, where one answer is a superstring of
the other — so an unanchored pattern is not a test of the answer, it is a test of the vocabulary. The
descriptor was written by analogy with the field-shaped one, which is where the analogy stops holding.

**Fix.** Anchor it: `/^\s*logged in/im` and `/^\s*not logged in/im`. The `m` flag matters too — the
harness prints a warning line before the answer when its home does not exist.

**Verification.** The assertion that fails is `assert.ok(!codex.in.test('Not logged in'))`, watched
failing against the original pattern; a second case pins the warning-line form. The guarantee this
protects is the whole logged-out refusal: with the original pattern, a spawn onto an account nobody
had signed into would have proceeded silently and failed inside the harness instead of refusing with
the sentence that fixes it. **The trap to inherit:** when a check has a positive and a negative
pattern, assert the negative input against the POSITIVE pattern. Two patterns that each match their
own input prove nothing about the pair — and a sentence-shaped answer usually contains its own
negation.

## A surface crashed on a host answering an unexpected shape — twice now (2026-09-20)

**Symptom.** SES3's harness roster called `roster.data.harnesses.map(...)`; the existing shell test
suite mocks the bridge to answer one object for every request, so the machine settings page threw and
took the wiring card down with it. SES1 hit the identical failure in the session drawer, where a
mocked bridge answering the wrong object crashed the console.

**Root cause.** A page and a shell are versioned independently: **a shell older than a surface answers
a request it has never heard of with something else entirely**, and a mocked bridge is the same shape
as that older shell, which is why the tests find it. The failure is not the missing data — it is that
a new, optional capability took down the surface it was added to.

**Fix.** Validate the shape before using it (`Array.isArray(...) ? ... : null`) and render nothing when
it is not there; the rest of the page stands. Applied to the roster, the per-conversation profile
picker, and — since SES1 — the console.

**Verification.** `an answer that is not a roster leaves the wiring card standing` and its SES1 twin,
each driven by a bridge deliberately answering the wrong object. **The trap to inherit, now on its
second occurrence:** every new IPC request is an optional capability, so **the surface consuming it
must degrade to absent, never to a crash** — and the test that proves it is a bridge answering the
wrong shape, not a bridge answering nothing.

## `keys mint` indexed the server's own disk (2026-09-20)

**Symptom.** Caught by WSP3's new rehearsal phase, on its first run: a workspace's shared deployment
answered a registry containing seventeen repositories from the developer's own machine — every
sibling checkout in the folder above this one, this repository included — all of them in workspace
`default`, on a host that serves `aurora` and had never been fed anything but two scratch
repositories. The check that failed was the new "every row landed in the workspace the deployment IS".

**Root cause.** `KeysConsole` opened the deployment with `ServiceFactory.CreateAsync(options)`, and
that composition performs WSP2's once-per-store bootstrap import from the configured root (D48 §3).
The key console runs before any mode is passed down, so it composed as a LOCAL deployment: minting a
key on a server registered whatever sat beside the binary — **machine paths included** — into a store
that must be fed and never scanned (D47 §4). Two guards were in place and neither could see it: the
route refusals and the empty-source composition apply to the *serving* process, and the rehearsal's
byte-scan of the remote store looked for the fixture path, which these rows did not contain because
they were real.

**Fix.** `ServiceFactory.OpenKeysAsync` opens the SQLite store and the key store, and nothing else —
key administration has no business touching an index, and the narrow door is also the honest one.

**Verification.** The new check `Key_administration_never_bootstraps_a_registry` was watched failing
with the console's old call restored, then passing; the family rehearsal went 111/112 → 112/112 on the
same change. **The trap to inherit:** a convenience composition is a *behaviour*, not a wiring detail.
`CreateAsync` grew a side effect in WSP2 — a legitimate one — and every existing caller inherited it
silently, including one whose whole job was to print a credential and exit. When a factory acquires a
side effect, its callers are the change's blast radius; enumerate them in the same commit, and prefer
a narrow door for a caller that needs one field of what it opened.

## `refresh` re-read the repositories, never the folder (2026-09-20)

**Symptom.** Caught by WSP1's new rehearsal phase: two repositories born mid-run were registered,
scoped and addressable — and their knowledge answered no search at all. `POST /api/refresh` reported
success and a plausible entry count; nothing said a word about the two it had not looked at.

**Root cause.** `FileSystemKnowledgeSource.UnderFolder` enumerated the root's subdirectories **once,
when the source was constructed** — at host startup — and `ReadAsync` then re-scanned exactly that
list. So `knowledge_refresh`, whose stated promise is "re-read every repository from disk and rebuild
the index", re-read only the repositories that existed when the process launched. A project created
today was invisible until someone restarted the host. The registry never had the bug (it enumerates
per read), which is what made the failure so confusing: the repository was listed, declared and
quest-addressable while contributing nothing to the index.

**Fix.** The source holds the *folder*, not a snapshot of it: the constructor takes a
`Func<IReadOnlyList<string>>` and `UnderFolder` enumerates inside it, so every read lists the
directory afresh. The explicit-list constructor stays for a fixed set of roots.

**Verification.** Red first (`A_repository_that_appeared_after_startup_is_read_by_the_next_refresh` —
`["elder"]` where `["elder", "newborn"]` was expected), then green; the family rehearsal went 86/87 →
87/87 on the same change. **The trap to inherit:** this is the ghost rule's mirror image, and it fails
the same way — by looking fine. A ghost is data that outlived its source; this was a source that never
learned the world had grown. Anything that promises to "re-read from disk" must re-read *what is on
disk*, including what directory entries exist — a list captured at construction is a cache with no
invalidation and no name.

## The mirror-down fed itself back up, and an empty entries feed is a delete (2026-09-20)

**Symptom.** None yet — found by the post-redesign review (REV1), before any two-machine deployment
existed to lose data on. On the second tick after a remote sync, machine B's shared knowledge on the
remote would have been replaced with an empty set by machine A.

**Root cause.** `RemoteSyncPayloads.Joined` selected feed-up candidates on the `joined` flag alone,
and the mirror-down writes foreign registrations into the local registry carrying that same flag. The
next tick's feed-up loop therefore included the teammate's repository; this machine has no checkout of
it, `/api/entries` answered `[]`, and the entries feed is deliberately a replacement ("a repository
that deleted its knowledge means the deletion") — so the teammate's content was wiped by a machine
that never had it. The rehearsal missed it because its knowledge assertions ran before the pull that
plants the foreign row.

**Fix.** Feed-up requires a root: the local host answers roots to its loopback caller, and a checkout
is exactly what a root means — so a rootless joined row is a foreign one by construction, and the
authority stays with the machine that holds the checkout (D47 §5).

**Verification.** Red first (`A_mirrored_down_row_never_feeds_back_up`, on a fixture registry carrying
a rootless joined row), then green; the family rehearsal's remote phase stays 75/75. **The trap to
inherit:** a flag that travels with mirrored data cannot also be the selector for what feeds back —
pick a field the mirror structurally cannot carry.

## A shared deployment scanned its own disk through index-on-first-use (2026-09-20)

**Symptom.** None witnessed — found by the same review. A fresh shared host's first request (any read,
or the first feed against an empty store) would have scanned `DAORIS_KNOWLEDGE_ROOT` — defaulting to
the tree near the binary — and served whatever it found to every keyed caller.

**Root cause.** "A shared deployment is fed, not scanned" was enforced at one door: `/api/refresh`
answers 409 in shared mode. But `KnowledgeService.EnsureIndexedAsync` indexes on first use when the
store is empty, and every read and feed path funnels through it — the property held only for callers
who happened to hit the refused route first. The rehearsal masked it by pointing the shared host at an
empty scratch root.

**Fix.** The source became a composition choice like the embedder: shared mode composes
`EmptyKnowledgeSource`, so never-scans is true by construction rather than by route. The rehearsal now
plants a decoy repository beside the shared host and asserts it stays unserved.

**Verification.** The new check watched red under sabotage (source forced back to the filesystem:
74/75, exactly the decoy check) and green with the fix (75/75). **The trap to inherit:** a refusal at
a route is not a property of the deployment — anything "the deployment never does" belongs in its
composition, where no request ordering can route around it.

## A stale `dist/` shadowed the CLI sources in every gate that drives the `bin` (2026-09-20)

**Symptom.** DRV5's remote rehearsal failed at the sync: the registry showed a repository whose
manifest declared `remote.join: true` as `joined: false`, so nothing fed the remote. The service unit
tests and `npm test` were all green — only the gate that runs the real `bin/daoris.mjs` disagreed.

**Root cause.** `bin/daoris.mjs` prefers a built `dist/cli.js` over `src/cli.ts` (a consumer's Node
may be 22 and not strip types). A `prepack` from an earlier session had left a `dist/` on disk — **it
is gitignored, so it was invisible to `git status`** — carrying the pre-DRV5 `connect.js`, which had
no `join`/`shareKnowledge` in its payload. `node --test` runs the `.ts` sources directly and never
touched `dist/`, so every source-level gate passed while every `bin`-driven gate silently ran
month-old code. The split was masked for landings 1–5 because none of their gates exercised the new
connect fields; landing 6 was the first to drive the `bin` through the remote declaration.

**Fix.** Removed the stale `dist/`. The dev loop needs no build (D-conventions: Node 24 strips types),
so a `dist/` present outside a release is always stale and always shadowing.

**Verification.** `connect --dry-run` against a `remote`-declaring manifest now prints `join`/
`shareKnowledge` from the sources; the remote rehearsal phase went green. **The trap to inherit:** a
green `npm test` with a red `bin`-driven gate is the signature — suspect `dist/` first, and note that
`git status` will not show it because it is gitignored build output.

**Closed structurally 2026-09-20 (REV1).** The manual fix left the mechanism intact: `postpack` claimed
`--clean` and `stage-package.mjs` read no arguments, so every local `npm run rehearse` re-manufactured
the leftovers. `--clean` now exists and removes `dist/` with the staged files, the release rehearsal
asserts nothing staged outlives the pack, and the named dry-run verification above is a test
(`connect --dry-run prints the exact payload…`), bin-independent.

## "Single file" leaves the SQLite native library behind (2026-09-19)

**Symptom.** The installed `daoris-knowledge.exe` died on first store open with `DllNotFoundException`
for `e_sqlite3` — after the same binary had appeared to work when probed from inside the workspace.

**Root cause.** `PublishSingleFile` bundles managed assemblies but places **native** libraries beside
the executable by default; the install step copied only the exe. The in-workspace probe masked it
twice over: a stdio host under a null stdin exits immediately and *cleanly* before touching the store,
which a naive probe reads as a crash — or as success.

**Fix.** `IncludeNativeLibrariesForSelfExtract=true` in `tools/service-publish.mjs`, making the file
genuinely single.

**Verification.** The installed binaries, run from a neutral working directory: the MCP host starts,
warns exactly when no root is named and only then, and exits cleanly on stdin close; the HTTP host
serves both the API and the page. Asserted on behaviour, not on the process staying alive.

## A repository that left the disk never left the index (2026-09-19)

**Symptom.** The platform's Overview served a repository renamed weeks earlier — dozens of entries,
indistinguishable from a live project — on the one surface whose job is telling a person what exists.

**Root cause.** `KnowledgeIndex.RefreshAsync` replaced entries per repository it FOUND and said
nothing about repositories it did not. Replace-what-you-saw is silent about the absent, and the
absent is exactly where ghosts live.

**Fix.** After replacing, any repository held by the store but missing from the scan is replaced with
an empty set — guarded on the scan having found at least one repository, because a scan that saw
nothing is a mis-set root far more often than a family that emptied, and "refresh wiped the index" is
the wrong answer to a wrong path.

**Verification.** Test red first (`RefreshTests.A_repository_that_left_the_source_leaves_the_index`),
then green, plus the saw-nothing guard case. Then proven on the real store: one refresh retired both
ghosts (16 → 14 repositories) and `/api/repositories` lists only what is on disk.

## The HTTP host's documented defaults were both untrue (2026-09-19)

**Symptom.** Launched exactly as the README says — `dotnet run --project …Http` — the host bound
port 5000 rather than the documented 5177, and its default family root resolved to the service's own
tree, whose subprojects would have been indexed as though they were the family.

**Root cause.** Two unbacked claims, the `claims-need-checks` shape. Nothing set the port, so
Kestrel's default won. And the root's "parent of the current directory" heuristic assumed the CWD was
the workspace — `dotnet run` sets the CWD to the project directory. Every earlier run had supplied
both by environment, so the defaults themselves had never once been exercised.

**Fix.** The host defaults its own URL to 5177 (an explicit `ASPNETCORE_URLS` still wins), and the
family root walks up from the binary to this workspace's manifest exactly as the MCP host already
did, with the old heuristic as the last resort.

**Verification.** Launched with no environment at all: right port, right family. The family rehearsal
re-ran after the change, 22/22. No store pollution had occurred — no request ever reached the
mis-rooted instance.
