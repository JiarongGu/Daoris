# The first deployment — what running Daoris outside a checkout found

> Written 2026-09-22, at the owner's direction: *"setup and deploy the desktop version … and we can
> drive and log it properly for some real case study and testing."* This is the first time any of
> Daoris has run as an **installed application** rather than out of its own workspace, and the first
> driven run against a family that is not a fixture.
>
> Paths here are neutral by `sensitive-info`: the install folder is `<install>`, the family root
> `<family>`. The machine's own copies live in `local/`.

## 1. What was deployed, and how

| | |
|---|---|
| Shell | `daoris-desktop` published Release, **framework-dependent**, into `<install>/app` |
| Service | both hosts self-contained into `~/.daoris/bin` (`publish:service -- --install`) |
| Family | the three testbed repositories (`testbed-core`, `-ui`, `-ops`) in workspace `testbed` |
| Driver | `testbed-core` drivable, adapter `claude-code` (pipe door), cap 1 |
| Harness | Claude Code 2.1.278, the machine's own login |

**The shell is framework-dependent on purpose.** It already requires a Windows desktop runtime and
the WebView2 runtime; a self-contained publish would add ~150 MB to carry a .NET the machine has and
would still not carry WebView2. The **service** hosts are the opposite case and are self-contained —
they are what a server runs, possibly with no .NET at all (D43).

## 2. Four things only deploying found

Each of these was invisible from inside the workspace, and three of them were invisible *because* of
something the workspace provides.

### 2a. The shell looked for the installed host where the installer never puts it 🔴

`service-publish --install` gives the HTTP host a **directory of its own**, because its `wwwroot`
bundle has to travel beside the executable; the MCP host installs flat. `ServiceHostLocator` built
only the flat candidate, for both. The publish script prints the nested path on success, so the two
halves disagreed **in writing** and nothing compared them.

**What hid it is the interesting part.** The locator's next candidate is the workspace build, which
exists on every machine this had ever run on — so the shell always found *a* host and nobody asked
which one. On a deployed machine there is no workspace to fall through to, and the shell reports no
host at all. *The one place it is fatal is the one place it had never been run.* `docs/FIX-LOG.md`
carries it.

### 2b. There was no way to deploy at all

`tools/desktop.mjs` builds and runs **in place** — it is a dev loop and says so. Nothing published the
shell. `tools/desktop-publish.mjs` is now its sibling, for the same reason `service-publish.mjs`
exists: a workspace build is not a deployment, and the difference only shows up once you try.

Its guard is worth recording. The first version allowlisted the file types a publish emits and then
**refused its own second run**, because `dotnet publish` also drops `.xml` docs — and an allowlist is
a list people append to until it allows everything. It is now decided by a marker file the script
writes, which is decidable rather than heuristic: the same provenance test the dsh patch layer uses.
The refusal also names *what it found* rather than the first few entries it listed; a refusal that
points at the wrong file sends you to look in the wrong place.

### 2c. The testbed was not idempotent, and could not be driven

`tools/testbed.mjs` documents itself as *"idempotent: re-running re-syncs"* and died on its second run:
it re-ran `init`, which **refuses an existing manifest by design**. Adoption happens once; everything
after it repeats.

Worse, it never wrote a repository's own `.mcp.json`. The **pipe door leans on the repository's
connector**, and the driver may never reach in and write one — so a testbed session could not claim or
close its quest at all. Wiring it is the connector's job at adoption, which here means that script's.
A testbed that worked only over the protocol door (which carries its servers on `session/new`, ACP4)
would have been proving the easier half.

### 2d. No instrument reaches a deployed shell

`desktop.mjs shot|eval|click` find the window by **this checkout's executable path**, and a deployed
shell is a different path with no debug port. That is right for the port — a deployed application
should not expose one — but it means *"does the deployed thing actually render"* is answered today
only by a person looking at it. Recorded rather than fixed: the honest options are a deliberate
opt-in flag or nothing, and that is a decision, not a patch.

## 3. The boundary, on a real deployment

The testbed joins workspace `testbed`; this repository's own family is `default`. A quest across them,
posted to the live host:

```
HTTP 409
`Daoris` is in workspace `default` and `testbed-core` is in `testbed` — a quest does not cross
workspaces, because the workspace is the unit of sharing. Either wire one of them to the other's
workspace on this machine, or carry the request across yourself. Addressable from `default`: Daoris.
```

Both sides named, the receiver's own circle offered, and a `409` rather than a `400` — a state
conflict, not a malformed ask (D48 §4). This is the first time it has been observed anywhere but a
rehearsal.

## 4. The driven run — three attempts, all recorded `failed`, and the driver was right every time

One quest, published `testbed-ui` → `testbed-core`: *give `Account` a `displayName` separate from its
login handle*, written with a real reason and a real question ("what does a rejected value mean").
Adapter `claude-code` over the pipe door, cap 1, Claude Code 2.1.278, the machine's own login.

| | spawned | ran | record | quest |
|---|---|---|---|---|
| 1 | 08:19:52 | **7m11s** | `failed` — *exited without touching its quest* | Open |
| 2 | 08:30:22 | killed | `stopped` — orphaned, and 26s older than the fix it needed | Open |
| 3 | 08:32:37 | **9m35s** | `failed` — *exited without touching its quest* | Open |

**The driver's observation was correct in all three.** It concluded from the exit code, the quest's
state and the absence of commits (D46 §4) — never from what the session said about itself, and each
session said a great deal. That is the design working exactly as written.

### 4a. 🔴 The finding: a repository cannot grant its own trust

Attempt 3's transcript opens with the harness saying it outright:

> `Ignoring 9 permissions.allow entries from .claude/settings.json: this workspace has not been`
> `trusted. Run Claude Code interactively here once and accept the trust dialog, or set`
> `projects[…].hasTrustDialogAccepted: true in <profile>/.claude.json.`

So the chain is: the driver tells the session to claim its quest → ACP4 and the connector make the
tools *reachable* → the repository's `settings.json` says it *allows* them → **and the harness
ignores that file until a human has accepted a trust dialog for that exact path on that machine.**

The consequence is the important part: **the driven loop over the pipe door cannot complete on a
repository a person has never opened interactively.** Daoris can wire everything it owns — the
connector, the allow-list, the posture — and still be one machine-level flag short, in a file that is
the person's own harness configuration home and therefore not Daoris's to write (SES3).

**Why DRV4 did not see this.** Its entry names the dependency in a parenthetical — *"its own
`.claude/settings.local.json` trusting it"* — and the scratch repository had been opened by hand
during that session. The claim it confirmed, *"the connector tools load under the harness's
non-interactive mode"*, is true **given a trusted workspace**, and that qualifier lived in one clause
of one archive entry. This is the same shape as 2a: a thing that works everywhere it has been tried,
because everywhere it has been tried had something the tried-for case does not.

### 4b. What the sessions did instead, which is its own result

Both sessions **followed the doctrine Daoris had synced into the repository**, without being asked to:

- Attempt 1 refused to grant itself the missing permission, **citing `file-tool-discipline`** —
  *"routing around an approval gate is circumventing a safety control"*.
- Both refused to commit, **citing `autonomous-development`**: gates had not run, so nothing was green
  to land on.
- Both wrote every behavioural claim from the implementation and then **said which ones no run had
  confirmed**, which is `claims-need-checks` precisely.
- Attempt 3 ran a `post-feature` pass on itself, found its changelog restating what two other
  documents owned, and fixed it. It also worked out on its own that `.claude/knowledge/` is
  daoris-generated and tracked in the lock, so writing there would register as drift — and put its
  own documents in `docs/` instead.

That is the canon doing its job in a repository that had existed for four hours, driven by a session
nobody supervised. It is the strongest evidence in this document, and it arrived as a side effect of
the thing that failed.

Both trees are preserved on branches (`attempt-1-no-permissions`, `attempt-3-untrusted-workspace`):
33 tests written and never run, a validation vocabulary with a stable `(field, code)` contract, and
decision records explaining both.

### 4c. Two more defects the run exposed

- 🔴 **The transcript was not UTF-8.** Attempt 1 recorded an em-dash (`e2 80 94`) as `e9 88 a5 3f` —
  decoded as this machine's ANSI codepage (CP936) and re-encoded. .NET defaults a redirected stream
  to the console's codepage, and nothing set it. The result is still *valid* UTF-8, so nothing
  downstream can tell it was ever wrong — and a platform that speaks 简体中文 would lose every
  Chinese character in a session record. Fixed at the one place every adapter spawns through, both
  doors, with a test per adapter.
- 🔴 **A force-killed shell orphans its session.** Stopping the shell with `Stop-Process -Force` left
  the `claude` child running against the repository, parented to a dead process, with the record
  still saying `working`. The graceful path does end sessions and record them `stopped`; a forced
  one cannot, by definition. Worth knowing before anyone kills a shell that is driving.

### 4d. 🔴 A deployed shell can serve a page older than itself

Re-launching the install showed the **previous** UI. Nothing had failed: the shell **adopts an HTTP
host that is already running** rather than double-starting one (documented, and right), and the host
running was the machine-wide one under `~/.daoris/bin`, still serving the `wwwroot` from *its* last
install. The window was new, the page was old, and no surface said so.

Adoption compares nothing about what it is adopting. The two honest answers are for the host to
report the bundle it serves so the shell can say when they differ, or for `publish:desktop` to refuse
quietly diverging from the machine's installed service. Neither is built; the deployment was fixed by
reinstalling the service and restarting the host.

> **The second deployment (2026-09-23) met the same class with nothing running**: the locator ranked
> `~/.daoris/bin` above the host `--service` had just published beside the shell, so the shell
> *spawned* the older one. The order is reversed now — what the install carries comes first — and
> the deployment gate asserts the host it started is the install's own (`docs/FIX-LOG.md`). The
> reporting half is built in its simplest honest form the same day: the platform's page names its
> own bundle, so on adoption the shell compares what the running host serves with what the install
> carries and, when they differ, says so — once, naming both — and goes on, because the host works
> (`HostSupervisor.Notice`). And a third mechanism underneath both: the host sent no `Cache-Control`,
> so a page loaded once from a stale host was answered from the WebView2 profile's cache on every
> start after, with the right host running and never asked. The page is `no-cache` now and the
> hashed assets `immutable`; the deployment gate holds both headers.

A smaller one beside it: both installs copy `wwwroot` **over** the existing directory without
clearing it, so stale hashed bundles accumulate. Harmless — `index.html` names the current one — and
misleading to anybody trying to tell which is live by listing the folder, which is exactly what this
was diagnosed by.

## 5. What this says about the next deployment

- **The trust step is the blocker, and the detection half is now built.** The driver reads the
  harness's own record before spawning and **holds** rather than starting a session that could never
  close its quest:

  ```
  held  #7786da → testbed-core: `<family>	estbed-core` has never been trusted by this harness on
  this machine, so it ignores the repository's own `permissions.allow` — a session here can do the
  work but cannot take or close its quest. Run `claude` in that directory once and accept the trust
  prompt. Daoris does not set that flag for you: it is your grant to give.
  ```

  Nine minutes and a real login become an instant sentence. **Read, never written**, and only a
  definite *no* refuses — no file, an unreadable one, or a harness with no notion of trust are all
  unknown and permissive, the same rule the login question follows.

  What is still a decision, and the owner's: whether adoption should ever **ask** and write the flag,
  or whether the pipe door stays documented as needing a human's first visit. 🔴 Never silently: that
  flag *is* the grant, and a tool that gave it on someone's behalf would have removed the only step in
  the chain that was theirs.
- **The protocol door may not have this problem at all**, since the ACP adapter runs the Agent SDK
  rather than the CLI's interactive trust flow. That is now a concrete reason to prefer it, and a
  cheap thing to measure — it costs one driven run.
- **`.mcp.json`, `.claude/settings.json` and the trust flag are three things, and adoption writes
  one.** Anything calling itself "adopt this repository for driving" has to account for all three, or
  say which it cannot.
- **A deployment gate would have caught 2a and 4c and neither is exotic.** Both rehearsals run inside
  the workspace, where the workspace build and the console's own encoding paper over exactly these.
  What is missing is a gate that installs and runs *the artefact*, which is what `rehearse` does for
  the CLI package and nothing does for the desktop.
