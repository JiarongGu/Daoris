# Fix log

Root cause, fix, and verification for non-trivial defects — the record version control cannot carry:
a diff shows what changed and never why the old behaviour was wrong. Newest first. The knowledge
service indexes this file per entry, so a sibling can ask "has anyone hit this" without opening the
repository.

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
