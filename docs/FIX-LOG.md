# Fix log

Root cause, fix, and verification for non-trivial defects — the record version control cannot carry:
a diff shows what changed and never why the old behaviour was wrong. Newest first. The knowledge
service indexes this file per entry, so a sibling can ask "has anyone hit this" without opening the
repository.

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

**Commit.** pending.

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
configuration home under `C:\Users\Some One\` breaks; and it would put a version string a person typed
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
