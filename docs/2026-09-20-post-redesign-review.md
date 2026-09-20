# Post-redesign review — the D45–D47 arc audited (2026-09-20)

The driver and remote arc (DRV2–DRV5, ~94 files, +7,190 lines) landed across two days with the gates
green throughout. This review swept every artefact it touched — five parallel read-only audits over the
service, the desktop, the CLI + tooling, the web platform, and the documentation — verifying each claim
against the source. Findings below, grouped by disposition. Checked items are fixed in this review's
commits; unchecked items are deliberately deferred with the reason inline.

## Bugs the gates had not caught

- [x] **The mirror-down feeds itself back up, and an empty entries feed is a delete.** `RemoteSync`'s
  `Joined()` selects on the `joined` flag alone (`src/Daoris.Desktop/Daoris.Desktop.Driver/RemoteSync.cs:103`),
  and mirror-down writes foreign registrations into the local registry carrying that flag. On the next
  tick the feed-up loop includes the teammate's repository, this machine has no checkout, `/api/entries`
  answers `[]`, and the remote's `ReplaceRepositoryAsync` wipes the teammate's shared knowledge. The
  rehearsal's knowledge assertions run before the pull that would trigger it. Fix: the machine holding
  the checkout is the authority, and a checkout is what `root` means — `Joined()` requires a root.
- [x] **Shared mode scans the server's disk through the back door.** `/api/refresh` refuses in shared
  mode, but `EnsureIndexedAsync` (`src/Daoris.Service/Daoris.Service.Core/KnowledgeService.cs:234`)
  scans unconditionally on the first read or feed against an empty store — a fresh shared deployment's
  first request indexes whatever sits near the binary and serves it to keyed callers.
- [ ] **The session ledger's state parse accepts numeric strings.** `SessionLedger.Parse` omits
  `Enum.IsDefined`, so `"99"` becomes `(SessionState)99` and escapes the `UnknownState` branch; the feed
  door uses the strict `Session.TryParse`, so the two doors disagree about what a state name is. A test
  claims they share the tolerance; the claim is currently untrue.
- [ ] **`postpack --clean` was never implemented.** `tools/stage-package.mjs` reads no arguments, so
  `postpack` re-stages instead of cleaning — the staged `canon/`, `LICENSE`, `README.md` and the built
  `dist/` outlive every pack, gitignored and invisible. This is the exact mechanism of the FIX-LOG's
  stale-`dist/` entry, still manufacturable by one local `npm run rehearse`.
- [ ] **A hung driver freezes the family rehearsal.** Phase 7's `drive()` passes no timeout, so the
  kill-timeout promise `run()` documents is false for its four driver invocations; `driveA` also drops
  the `NO_REMOTE` hermeticity guard the file's own prelude declares for "every host and driver".
- [ ] **The desktop splash can wait forever.** `DriverLoop.RunAsync` has no try around
  `EnsureAsync`; a host that is located but unstartable faults the task, `_hostReady` never completes,
  and `MainForm.BringUpAsync` awaits it indefinitely — the exact dark-window failure the form's own
  fallback was written to prevent.
- [ ] **`/api/feed/quests` validates two fields and lets four nulls through** to non-coalesced SQLite
  parameters (a 500 where its sibling door answers 400). **Conflict shapes diverge**: the quest door
  teaches clients 409 means "someone got there first"; the session doors answer 400 for the same class.
- [ ] **A corrupt manifest escapes the CLI's exit-code contract.** `JSON.parse` is unguarded in
  `config.ts`, and `"remote": null` passes the `!== undefined` guard and dereferences null — a tool
  error (exit 2 by contract) surfaces as an unhandled stack trace with exit 1, which a gate reads as
  policy failure.

## Dedup and refactor

- [ ] **`Daoris.Service.Http/Program.cs` (745 lines, 18 routes, four jobs)** — split: wire contracts to
  `ApiContracts.cs`, the `keys` console verb to `KeysConsole.cs`, the mode-forked feed doors and the
  quest/session routes to route-group files; a shared `HostComposition` for the ~50 lines the two hosts
  copy-paste (embedder ternary, root/db defaults, `IsOllamaRoot`).
- [ ] **`ParseKinds`/`ParseSet` duplicated verbatim across the two doors** — query judgement belongs in
  Core (D36) so a kind alias cannot land in one host only.
- [ ] **The sessions feed door judges in the host; the entries door judges in Core** — move the
  joined-repository check into Core so a future door shares it.
- [ ] **`RemoteSync.cs` holds four types and three responsibilities** — split `RemoteTarget` and
  `RemoteSyncPayloads` into their own files; extract feed-up/mirror-down phases; give `RunOnceAsync` the
  `HttpClient` seam `ServiceClient` already has, so its ordering contract becomes testable.
- [ ] **HTTP plumbing duplicated three ways in the driver** with three error conventions — one helper;
  `ServiceClient.GetAsync` stops discarding the service's refusal sentence.
- [ ] **The watch loop exists twice** (`Driver.Host/Program.cs`, `App/DriverLoop.cs`) — a shared
  `DriverWatch` owning construction, config re-read, and delay.
- [ ] **The rehearsal harness is copy-pasted across four files** (`check`/`section`/transcript/capture
  in both rehearsals; `copyTree` five times across tools and web scripts) — extract a rehearsal kit and
  a shared `copyTree`.
- [ ] **Three driver helpers in the family rehearsal, diverged three ways** — one parameterized helper
  with the timeout and `NO_REMOTE` always applied.
- [ ] **The claude-code layout is still hardcoded in `analyze.ts`/`twins.ts`** beside the harness
  descriptor that owns it — route the tier names, target, and index filename through the descriptor.
- [x] Web: status→tone casts copy-pasted three times (one narrowed wrongly); invalidations bypass the
  `keys` factory; a hand-rolled entry fetch beside a file of TanStack hooks; `refresh` skips the `post`
  helper and loses the refusal sentence; debounce duplicated in two views.

## Dead code and stale claims in code

- [ ] Service: the csproj's "Read-only by construction (D31) … no auth for writes" comment (8 write
  routes and a gate say otherwise); `ApiKeys.cs`'s present-tense reference to the retired single-key
  gate; `SqliteKnowledgeStore`'s "every entry can be re-read from the repositories" rationale (untrue on
  a shared deployment); `RegisterRequest.CanonSource` parsed and never read; dead `using`; `Planner.cs`'s
  unread `busy` set; `SyncReport`'s five counts computed and discarded.
- [ ] CLI: `declare()` exported and never called; `LockLike` documented and consumed by nothing;
  unused imports in `config.ts` and six test files (`noUnusedLocals` now on); three references to
  `.mjs` modules that became `.ts` (one inside a user-facing error message); `stage-package.mjs`'s
  shadowing rationale describing a hazard `canon.ts` closed.
- [x] Web: the "key-gated endpoints" sentence the README fix missed in `QuestsView.tsx`; the "keyed
  deployment refusing a browser write" example (a shared deployment serves no page); dead type
  re-exports; an unused icon; two unused i18n keys in both catalogs; four `DriverState` fields the page
  never reads.
- [ ] Tools: ten blank-line scars where the retired `key:` lines were deleted; the two rehearsals
  disagreeing on `process.exit` vs `exitCode`; `release-prep.mjs --check` dead (the only enforcement of
  the example-family version pins) and the argless `npm run release-prep` that can only print usage.

## Documentation corrections (wrong, then stale)

- [ ] `CLAUDE.md` names `daoris request` — no such command exists (quests live in the service, D31 as
  amended); its release-workflow claims miss the four-gate run and the three-OS devkit matrix; the
  `rehearse:family` description stops at D46.
- [ ] `src/Daoris.Service/README.md` says "81 tests" (158) and still carries "does shared mode need
  hosting at all?" as an open question D47 priced and declined. `ROADMAP.md` still prescribes
  git-as-store, says "Four artefacts" over a five-row table, "4 core knowledge documents" (5), and
  points at a backlog id that archived (CANON4).
- [ ] `CHANGELOG.md` (Unreleased) says "Eight commands" (nine; `connect` has no bullet) and "Three
  packs" (six). `TASKS.md` says the rehearsal is 43 checks in one place and 74 in another.
- [ ] `src/Daoris.Desktop/README.md` lists shipped session controls as "Remaining" and never mentions
  the remote sync; the two binaries' header comments omit the remote env trio they read.
- [ ] Root `README.md` documents neither `domain` nor `remote` in the manifest — the arc's biggest doc
  gap: a shipped, review-gated disclosure field with no user-facing documentation. `status` cannot
  report it either (the one manifest field it cannot answer for).
- [ ] `docs/2026-09-20-remote-design.md` §9 lacks the second D47 amendment note (the registry mirrored
  down, foreign rows only) that §7 got for the first; §3's "today" paragraph describes the retired gate
  with no pointer. `docs/DECISIONS.md` D36 lacks the supersession note the file's convention gives
  every other superseded entry.
- [ ] `src/Daoris.Web/README.md`: the five-view table predates the session surface and controls; the
  convergence threshold reads 0.75 where the decision record and the view both say 0.70; the "same
  build is intended for Daoris.Desktop" sentence describes a shipped thing as future.

## Test gaps worth closing now

- [ ] `connect --dry-run` printing `join`/`shareKnowledge` — the FIX-LOG's own named verification for
  the stale-`dist/` regression, currently manual.
- [ ] `RemoteConfig.Load` (Core) — zero tests, while its driver twin is table-tested; the guarantee is
  security-relevant ("never a mix of an env URL with the file's key").
- [ ] A rootless joined registry row yields no feed-up (with the mirror-down fix); the `Entries`
  payload asserts no machine path survives.
- [ ] The ledger answers `UnknownState` for `"99"` (with the parse fix); a shared deployment never
  scans (with the back-door fix).
- [x] Web: stop is absent in a browser (the arc's central read-only claim, unasserted in both loops);
  `SET_HOLD` reaches the IPC module; the freshest-attempt-per-quest reduction with two records.

## Deferred, with reasons

- **Surfacing `cap`/`adapter`/`pollSeconds` in a driver settings surface** — a feature, not a cleanup;
  the fields are trimmed from the page's type instead and return with the surface that reads them.
- **A `TestFamily` fixture for the service tests** (~80 duplicated lines) and Storybook stories for the
  session states — real, but below the line for this sweep; noted here so the next test-heavy task
  picks them up.
- **`FeedQuestRecord`/`QuestResponse` merge** — the nullability difference is validation-bearing;
  deliberately left as two records. (`FeedSessionRecord`'s missing `Transcript` is load-bearing and was
  never a candidate.)
- **Moving the web e2e host off port 5199** — done as part of this sweep only if trivially safe;
  otherwise it stays a noted collision hazard with the family rehearsal.
- **`daoris declare` as a command** — `declare()` is deleted rather than wired; a command nobody asked
  for is doctrine nobody chose.
- **Wiring the page to the host's `NUDGE` (start-now)** — the host verb exists and the driver's
  `Nudge()` is real, but no page surface calls it yet; wiring it is a small feature (publish →
  look-now), not a cleanup, so it stays deferred and the desktop brief says so honestly.
- **Storybook stories for the session states and driver controls** — the arc's components have no
  stories; the README's "every component state" claim is softened instead, and the stories arrive with
  the next design pass.
