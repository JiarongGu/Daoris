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
  assumed.
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
  keeps working, and so does a contributor who never ran Daoris).
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

## 4. Measurement

- **The source is ACP's `usage_update`**, parsed structurally instead of rendered to a line. It is
  the only structured usage any door currently delivers, which makes this an argument *for* the ACP
  door rather than a reason to parse the pipe door's text. A pipe-door session records no usage, and
  says so rather than showing a zero.
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

- **D49 §4 — Daoris never sees, stores or copies a credential.** Measurement needs no credential, and
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
