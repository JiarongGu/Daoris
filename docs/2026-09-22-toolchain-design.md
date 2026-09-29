# The managed toolchain, its accounts, and what they cost

> Written 2026-09-22 from the owner's direction the same day: *"I still cannot see a proper credential
> management since we need this for both claude/codex, and other llm if possible (this is kind more
> from deepseek harness) and also we need to be able managed multiple account with usage management
> (for example multiple claude accounts) and currently we still don't have managed cli (still reading
> from the machine)."*
>
> The two questions that shaped it were put to the owner and answered the same day: **usage is both,
> measurement first**, and breadth is **native adapters that match each tool's own API and interfaces,
> *and* the ACP door** — not a provider registry. This is the contract; `TASKS.md` carries the build
> items.
>
> **Paths.** `~/.daoris/…` below is the Daoris home as it was when this was written. Since D63
> (2026-09-23) the home is `$DAORIS_HOME`, the install's own `data/`, and nothing lives under the user
> profile. The tree beneath it is unchanged.

## 1. What is true today, measured before anything was proposed

Doctrine oversold this, and the overclaim is what let it hide: CLAUDE.md said *"the toolchain is
Daoris's"*, which reads as managed. It is corrected.

| Claim | Reality (`toolchain.ts`, `Harnesses.cs`) |
|---|---|
| Many accounts per harness | **True.** `~/.daoris/harnesses/<harness>/<profile>/` is a real per-account configuration home, wired through `CLAUDE_CONFIG_DIR` / `CODEX_HOME`, with a machine default, an optional per-workspace default, and a probed login state |
| Daoris installs the harness | **Half true.** It runs the harness's own installer — `npm install -g @anthropic-ai/claude-code` — into the **machine's** global |
| Daoris manages the CLI | **False.** `binary: ['claude']` resolves off `PATH`. No Daoris-owned directory, no pin, no two versions side by side |
| Usage | **Absent.** `ProfileReport` is `(Name, Home, Login)`. The one structured signal that already arrives — ACP's `usage_update` — is rendered to a transcript line and discarded |
| Breadth | Two harnesses, declared twice (TS and C#), no provider-agnostic path |

**What the reference does, read before deciding** (`deepseek-harness`, read-only). Its `credentials/`
group **stores secrets itself**: configuration names a `CredentialRef` and a private local store holds
the value. Its `llm/token-meter` measures **per-session context pressure and message price** by
replaying the session log — deterministic, no model calls, provider-reported usage reused only for an
identical request envelope. Worth stating plainly because the names mislead: that is *prompt sizing*,
**not** account quota. Neither package answers "which of my three Claude accounts is spent".

## 2. The three parts, and why they are separable

**Part 1 — the managed CLI.** Daoris owns where a harness's binary lives and which version runs.

**Part 2 — measurement.** What a session actually consumed, recorded per session and aggregated per
account.

**Part 3 — breadth.** More native adapters, plus the ACP door, and no registry.

**Part 4 — rotation, held.** Designed here, built when measurement says what exhaustion looks like.

They are separable because each has its own truth: part 1 is a path, part 2 is a number, part 3 is a
seam. Building them together would mean one change that cannot be reviewed.

## 3. The managed CLI

- **A managed harness lives under `toolchain/<harness>/<version>/` in the Daoris home** (written
  here as `~/.daoris/…` before D63 moved the home into the application's own folder; the tree is the
  same under the new root), installed with the
  harness's own installer pointed at that directory rather than at the machine — for npm-published
  harnesses, `npm install --prefix <dir> <package>@<version>`, which is the same mechanism already
  declared, aimed somewhere Daoris owns. No vendoring, no bundling: **a harness's own packaging stays
  its own problem** (D53's note on dsh's 561 MB), and the version installed is asserted rather than
  assumed. **As built since AGT2b (2026-09-24), Claude Code and Codex are fetched from their makers'
  own channels and verified**; npm stays for what ships only there — the two ACP adapters and dsh.
  §3a is how.
- **Selection is: the explicit command, then the managed pin, then `PATH` — and it decides every
  question about that binary, not only which one to spawn.** *Is it installed? Which version? Is this
  profile logged in?* are all asked of the **resolved** binary. Stated because it was got wrong: the
  rule was implemented at the spawn and not at the presence check, so a working pin reported absent
  and refused to run, while the other twin reported the machine's own binary as the pinned one
  (FIX-LOG, 2026-09-22). An explicit
  `commands` entry in `driver.json` is a person saying exactly what to run and outranks everything.
  The managed pin is the standing choice. `PATH` is what happens when nobody has asked for any of
  this — which is **today's behaviour byte for byte**, and is the point: this is additive, like
  session trees and credential profiles before it (D48 §2a — a machine that installed `claude` itself
  keeps working, and so does a contributor who never ran Daoris). On Windows `PATH` is asked for what
  can be started, by PATHEXT, so an agent npm installed globally (a `.cmd`) is found by the desktop as
  the CLI finds it (USE1f, FIX-LOG 2026-09-30).
- **A pinned binary runs with the tool's own no-update switch** (AGT2), on every spawn Daoris makes
  of it: a session over either door, and the probe. Claude Code updates itself by default, npm copy
  included; a pinned copy under this directory reported its auto-updates enabled until it ran with
  `DISABLE_UPDATES=1`. A binary off `PATH` gains nothing: its updates are the machine's.
- **The pin resolves exactly as a profile does**: the person's pick, the workspace's default, the
  machine's, then none. One resolution rule, already written and tested in both artefacts, rather
  than a second rule that drifts from it.
- **The executable seam is ACP2's.** `CLAUDE_CODE_EXECUTABLE` already appears in ACP2's scope as
  "→ the managed `claude`"; this is what makes that sentence true. The pipe door gets the same answer
  through `HarnessToolchain.Command`.
- **Every session record already says which version ran** (`HarnessVersion`, D49 §4), so "what
  produced this work" stays answerable across a pin change. Nothing new is needed for that.
- **Two doors** (D50): `daoris agent pin <agent> <version>` / `unpin` (`daoris harness` until
  AGT1), and the Machine view's
  harness card. One file, either door, as with every other machine-local choice.

**Rejected: vendoring a harness into the Daoris package.** It would make the version certain and make
`daoris` a distributor of somebody else's tool, with their licence and their update cadence. The pin
plus an asserted version buys the same certainty at none of that cost.

**Rejected: making the managed path the default the moment it exists.** A machine that has been
driving on its own `claude` would silently switch tool versions under a running arrangement. Absent
means `PATH`, and a pin is a thing the person did.

## 3a. The makers' own channels, as built (AGT2b, 2026-09-24)

Every URL, shape and name below was checked first, and the checks are in
`docs/2026-09-24-agt2b-channel-evidence.md`. Nothing that document lists as unconfirmed is relied on.
A toolchain declares **a `channel` or a `package`, never both**, so where a pin came from is never an
open question. The CLI's `channels.ts` and the driver's `ReleaseChannel.cs` are twins.

**Claude Code, from Anthropic's release bucket.** Each step runs only if the one before it passed.
1. Fetch `<version>/manifest.json` and the detached signature beside it.
2. Verify the signature under the **pinned** release key: v4, RSA, SHA-512, fingerprint
   `31DD DE24 DDFA B679 F42D 7BD2 BAA9 29FF 1A7E CACE`. Daoris carries the key rather than fetching it.
   A test holds the carried key equal to the published one, and the verifier still checks the key
   against the pin.
3. Read the version the manifest signed. A genuine manifest served under another version's URL
   verifies perfectly, and this is the only check that stops it.
4. Fetch that platform's binary directly. Its SHA-256 and size must match the manifest.

It never runs the bootstrap or `claude install`: the bootstrap always fetches latest, and what the
subcommand verifies is undocumented.

The OpenPGP verifier has no dependency: packets are parsed by hand and the RSA is the platform's.
It reads one shape only (armoured, detached, binary document, v4 RSA) and refuses anything else by
name. The signer must be the pinned **primary** key; a subkey is refused rather than trusted.
Revocation and expiry are not read, because the pin is the decision. **A key rotation is a reviewed
change to one line in each twin.**

**Before 2.1.89 there is no signature, so there is no pin.** The first signed manifest is 2.1.89's;
2.1.87's `.sig` answers 404. An earlier version is refused before anything is fetched, and the
refusal says what still works: install it with its own tooling, and Daoris runs it from `PATH`.

**Codex, from its release's own package.** Each step runs only if the one before it passed.
1. Fetch `release.json`. If it does not answer (a 404 or no connection), ask the GitHub release
   instead. Metadata that answers and then fails a check is refused and never traded for a second
   opinion.
2. The metadata's tag must name the pinned version.
3. The `SHA256SUMS` file's hash must match the metadata's digest for it.
4. The package's line in that file must equal the metadata's own digest for the package. Two
   published hashes that disagree refuse before anything large is fetched.
5. Download the package and check its hash.
6. Unpack it whole, because the executable finds `rg` and its helpers through the package's own
   layout.

A download address must be HTTPS. The unpacking has no dependency (gzip is `node:zlib`) and refuses:
- an absolute name
- a `..` name
- a Windows stream name
- a link of either kind
- a header that fails its checksum
- an archive that stops before its end marker

**Codex has no signature to check** (evidence §2), so its trust is TLS to the vendor's host plus
two published hashes that must agree. That is less than Claude Code's chain, and the design says so
rather than implying parity.

**The layout.** A channel install is staged at `<version>.part` and moved to
`toolchain/<agent>/<version>/` only once everything verified. Every refusal removes the staging. So
`bin/<binary>` (`.exe` on Windows) exists only if it verified: **finding it is the proof**, and
re-pinning an installed version downloads nothing. `managedBinary` / `ManagedBinary` look for the
vendor's layout first and npm's after it, because a pin npm made before this change is still an
install somebody made.

**Staying pinned.** Claude Code keeps AGT2a's `DISABLE_UPDATES=1` on every pinned spawn. Codex
declares no switch: per the evidence, an executable outside Codex's own standalone layout gets no
update action. That is documented but not measured on a real pinned install, which spends a real
download; AGT2c is that measurement.

**Where the network is.** In the CLI it is still only in `service.ts`. `releaseFetcher()` is handed
to `agent pin` by the dispatcher (`cli.ts`), so `toolchain.ts` and everything that judges a download
import no network module. A dogfood test holds both halves, and the usage text names `agent pin` as
the one management verb that opens a connection itself (and, since USE1a, `agent update` beside it). The driver fetches through its own
`HttpClient`, with no timeout: a Claude Code binary is over 200 MB, and the person's *stop* cancels
it.

**Two doors (D50).**
- **Terminal:** `daoris agent pin claude-code <v>` and `daoris agent pin codex <v>`.
- **Screen:** the Machine view's pin control, for Claude Code only. The driver declares no `codex`
  toolchain; its Codex is the `codex-acp` door, an npm package that still pins from npm. Anything
  a screen can set, a terminal can, and this is the reverse case, which D50 allows.

**The vendor's terms** (evidence §3):
- **The binary is installed and run as published.** The bytes that verified are the bytes that
  land. Nothing is patched, wrapped or repacked, and setting the executable bit is the only change.
- **No credential is intermediated.** The fetch carries none. Accounts are still directories
  Daoris never reads (D49 §4, D66 §3), and a sign-in is still the tool's own flow.
- **Daoris redistributes nothing.** Each machine fetches from the vendor's own host because a
  person on that machine pinned a version. Running Claude Code *in a product or service* is under
  the Commercial Terms, and that question belongs to whoever deploys Daoris that way.

**Rejected:**
- **npm as a fallback when the channel refuses.** The same trust by another road: a verification
  failure must never quietly downgrade to a weaker check.
- **The bootstrap and `claude install <version>`.** One always fetches latest, and the other
  verifies nothing documented.
- **Fetching the release key at install time.** One more fetch, and a key that could be swapped in
  transit. The pin would still catch a swap, so this was rejected on cost, not safety.
- **The GitHub releases of `anthropics/claude-code`.** Signed, but no setup document names them, so
  whether they are a supported channel is unconfirmed.
- **Codex's bare `.exe` asset.** It loses the helpers the package carries.

**What the gates do not cover:**
- No test touches the network, so the real fetchers (`releaseFetcher`, the driver's `HttpClient`)
  against the real hosts are unexercised.
- The unpacking is tested on packages built in the test, never on a real Codex tarball.
- Musl detection and Windows arm64 are mapped but never run.
- The first real pin of each is AGT2c's to observe.

## 3b. The agent's trust in a folder, as built (D73, 2026-09-24)

An agent that keeps a trust record declares it: `trustFile` in the CLI's table and `TrustFile` in the
driver's, the same two entries on both sides (`claude-code` and `claude-code-acp`, whose account is
`claude-code`'s), with a twin test on each. The driver only reads the flag, before every start, and
holds on a definite no, but only where the rules it hands over (PERM1) would not let the session
call its connector. Those rules are honoured untrusted, so the flag otherwise decides only whether
the repository's own allow-list counts. It reports each hold as a fact as well as a sentence.

- **Terminal:** `daoris agent trust <agent> <folder> [--profile <name>] --yes`. Without `--yes` it is
  the question, and it exits 1 having written nothing. `--dry-run` prints the same and exits 0. The
  account is the profile named, else the machine's default, else the agent's own home (twin rule 3).
- **Desktop:** the bridge's `TRUST_FOLDER`, for a hold the loop's last tick produced, in the file
  that tick read.
- **The write** is one flag in the agent's own file: `trust.ts` and `ClaudeTrust.Grant`, twins held
  by the same cases. It is the only file outside the Daoris home a command writes, and only because
  a person named the folder.

**What the gates do not cover:** whether a key Daoris writes is honoured by the harness exactly as one
it wrote, and whether a trusted parent covers a child. Both are measurable in one real run (D73).

## 3c. Update, as built (USE1a, 2026-09-30)

Update on a pinned `claude-code-acp` answered "declares no updater": the action knew only a tool's
own updater, and a pinned door is a copy Daoris installed. Update now branches on the door, the same
way in both artefacts (`HarnessActions.UpdateAsync`, `daoris agent update`):

- **Pinned, with a package or a channel: the pin moves.** The newest release is resolved to one exact
  version, then pinned exactly as `pin` does, and the pin is written only once that version is
  installed. npm answers `npm view <package> version`, a spawn like the pin's own `npm install`. A
  channel answers its newest-release pointer: Claude Code's `latest` (plain text, evidence §1) and
  Codex's `channels/latest` (release metadata, evidence §2). The pointer is not signed, so it only
  chooses the version, and that version is verified as a typed one would be. A pin already at the
  newest, and installed, fetches nothing. A newest release older than the pin never moves it back.
- **Unpinned, with an updater of its own:** the tool's updater runs, as before.
- **Neither:** refused, as before, and the Agents surface offers no Update at all. The roster's
  `updates` field (`pin`, `tool` or null) is what the page reads, with a tip saying which it does.

The terminal's `update` takes `--workspace` to move that circle's pin; the screen moves the machine's.
The driver declares only the Claude Code channel (§3a), so its channel half is Claude Code's.

**What the gates do not cover:** npm and both pointers are stand-ins in every test, so whether the
real `npm view` and the real pointers answer as the evidence recorded is unexercised.

## 4. Measurement

- **The source is ACP's `usage_update`**, parsed structurally instead of rendered to a line. It is
  the only structured usage any door currently delivers, which makes this an argument *for* the ACP
  door rather than a reason to parse the pipe door's text. A pipe-door session records no usage, and
  says so rather than showing a zero. *(Amended by D76: since CONV3a the native door reads Claude
  Code's own `stream-json`, which reports context when a turn ends, so only a door that carries text
  alone records none. Since USAGE1, conversations count toward their account as well as driven
  sessions and intakes.)*
- 🔴 **Usage is machine-local material.** A profile name is already served only over loopback
  (`ToSession`: `Profile: loopback ? s.Profile : null`), and per-account usage names a profile — so
  the record inherits that boundary rather than inventing one. It lives under `~/.daoris/usage/`,
  reaches a surface over the shell's bridge like the console and the diff, and **has no HTTP route**.
  A teammate sees the session record, exactly as today.
- **Two readings, because they answer different questions.** *Per session*: context used against the
  window, at its high-water mark — which is what tells a person a session is about to compact. *Per
  account*: sessions run and context consumed over a period — which is what tells them which account
  is carrying the load.
- **It states what it does not know.** A session whose harness reported nothing reads as *not
  measured*, never as zero. The console's rule (SES1: the bound is stated, never hidden), applied to
  a different number.
- **No model is named and no price is claimed.** Daoris does not know what a token costs — that is
  the deployment's business (D24) — so this counts what the harness reported and stops there.
  Turning counts into money would mean a price table per model per provider, maintained here, wrong
  within a month.

**Rejected: putting usage on the service's session record.** It would travel, and per-account usage
names an account. The existing boundary already answers this, and re-deciding it per field is how a
boundary erodes.

## 5. Breadth: native adapters, and the ACP door

The owner's words are *"more native support (so its more match its api and interfaces) also with ACP
door"*. That is **not** the provider registry the components plan rejected, and it does not reopen
D24 — it is D23 applied more times.

- **A native adapter is for a tool worth matching properly**: its own flags, its own configuration
  home, its own login flow and version question, its own notion of a session. That is what
  `HarnessToolchain` and the adapter seam already are; there are simply more of them.
- **The ACP door is for tools that speak the protocol** (D53). A tool can have both: ACP for the
  session, a native toolchain entry for install, version, profile and pin. `claude-code` is exactly
  that shape after ACP2.
- **The declaration is duplicated in two artefacts by design** (TS and C#) because the file and the
  layout are the contract, not shared code — and each carries a test table saying so. Adding a
  harness means adding it twice, deliberately, and the existing tests already assert that every
  declared harness names a real mechanism for everything Daoris offers to do.
- **Still no model named.** The harness carries the model; Daoris pipes text (D24, SES2). A harness
  that fronts several providers — the reference is one — is one harness to Daoris, with its own
  configuration deciding what it talks to.

**Rejected again, with the owner's answer on the record: a provider/model registry.** It would make
Daoris the thing that knows about models, which is precisely what D24 says the deployment decides.
The owner asked for native *support*, not a native *catalogue*.

## 6. Rotation — designed, and deliberately not built

Held until measurement exists, on the owner's "measurement first".

The shape it will take, so the measurement is built to feed it: **exhaustion is observed, never
read.** Daoris cannot ask a provider what is left without a credential, and D49 §4 stands — so a
profile becomes *cooling off* when a session it ran ended with the harness's own rate-limit signal,
and the planner prefers a profile that is not cooling off when it picks one for a repository.

Three things that decide whether it is worth building, all of which measurement will answer:

1. **What does exhaustion actually look like** in each harness's output? A string match on somebody
   else's error text is the fragile part, and it should be written against real transcripts rather
   than guessed.
2. **How long is a cool-off?** Rate limits have windows; the number is an observation, not a
   constant.
3. **Does a rotated session stay reproducible?** The record already names the account that ran it,
   so the answer is probably yes — but a driver that silently changed accounts mid-arrangement is a
   surprise, and the person should be able to see it happened.

**Not in scope, and worth naming**: spend caps, billing, and anything that reads a provider's
console. All three need either a credential or an API Daoris has no business holding.

## 7. What does not move

- **D49 §4 — Daoris never sees, stores or copies a credential** *(amended by D67 §1, 2026-09-23: an
  account that is an API key is kept in the home's `keys.json`; a sign-in stays the tool's)*. Measurement needs no credential, and
  rotation as designed needs none either. The reference's approach is the opposite and was read
  before this was written; adopting it was considered and declined on those terms.
- **D24 — a feature is specified without naming a model.** No registry, no price table.
- **D23 — an adapter arrives deliberately, one owner per domain.** Breadth is more adapters, not a
  generic one.
- **D48 §2a — coexistence.** Every part of this is additive: absent pin means `PATH`, absent profile
  means the harness's own home, absent measurement means a session that says it was not measured.
- **D50 — two doors.** Every setting here has a terminal verb and a screen.
- **D47 §4 — machine-local material.** Usage joins the transcript, the tree path and the profile name.
- **The CLI's shape.** Zero runtime dependencies, offline, and `toolchain.ts` stays the only module
  that spawns.
