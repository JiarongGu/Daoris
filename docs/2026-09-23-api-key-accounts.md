# An account that is an API key (AGT3, D67 §1)

The owner decided that Daoris may keep an API key for an account (D67 §1). This is how, for the one
agent measured so far.

## 1. What the tool does with a key (measured 2026-09-23, Claude Code 2.1.280, no key spent)

- **`ANTHROPIC_API_KEY` in the environment is enough.** `claude auth status` then answers
  `loggedIn: true`, `authMethod: api_key`, `apiKeySource: ANTHROPIC_API_KEY`, and **no email**.
  A non-interactive run (`-p`) takes the key with no approval prompt.
- 🔴 **Neither checks the key.** With an invalid key, `auth status` still says logged in. The first
  request gets 401 `authentication_failed`, and the tool retries up to ten times with growing delays
  (seven attempts had taken 41 s when the measurement was stopped). A wrong key therefore costs a
  session minutes, and the roster cannot tell it from a good one.
- Codex is not installed here, so nothing is claimed about it. Its documented key sign-in
  (`codex login --with-api-key`) stores the key in its own home, which is a different shape. It
  gets its own measurement before any Codex key account exists.

## 2. The design

- **A key account is an account**: a directory under `harnesses/<agent>/`, named `account-N` like a
  sign-in (D66 §3), so the tool's settings and history stay isolated per account. The key is kept
  **beside it, not in it**: that directory is the tool's (D49 §4), and Daoris owns its location only.
- **The key lives in `keys.json` under the home**, keyed by agent and then account. It is written
  atomically, tracked by nothing, and read by both twins: the CLI writes it as well as the desktop
  (D50). It is plaintext at rest, the same as `remotes.json`'s deployment keys and the tools' own
  credential files. The home is the application's own data folder (D63).
- **It is shown only as a handle**: the key's last four characters (`…abcd`). WSP3 shows a
  deployment key's prefix, but every Anthropic key begins `sk-ant-api03-`, so for these keys the
  tail is the part that tells two apart. The key never crosses the bridge back, never reaches a
  session record, and has no HTTP surface at all (D47 §4).
- **It reaches the agent at spawn through the tool's own variable**, which the toolchain declares
  (`KeyVariable`: `ANTHROPIC_API_KEY` for Claude Code). The probe asks `auth status` with the key
  set. 🔴 **The roster does not show the tool's "logged in" for a key account.** The tool says it for
  any key, so the account reads *unchecked* on the page and in `agent list`.
- 🔴 **Only the direct door, for now.** The protocol door resolves accounts under its own name
  (`harnesses/claude-code-acp/…`), not under the agent whose account it declares it runs as
  (`accountOf`). That predates this design, and a key follows the account, so a key account reaches
  Claude Code over the direct door only until AGT7 routes a door to its owner's accounts.
- **Removing the account removes the key** with the directory (D66 §3).
- **Two doors.** The desktop has *Add an API key* beside *Sign in to another account*: a password
  field, and a save that answers with the new account's handle. The terminal has
  `daoris agent key <agent>`, which reads the key from **stdin**, never from an argument, because an
  argument is visible in the process list and the shell's history.
- **Only agents that declare a key variable** offer it. Today that is Claude Code; the others offer
  nothing until they are measured.

**Rejected.**
- **The key inside the account's directory** (a settings file the tool reads). That directory is
  the tool's, and Daoris writing into it would make the tool's own home something Daoris has to
  keep in step.
- **OS-protected storage (DPAPI).** The CLI twin could not write it without native code, and Linux
  has no equivalent. That would break the terminal door (D50) for a gain the tools' own plaintext
  credential files do not have either.
- **The key in `harnesses.json`.** That file is wiring a person reads and edits by hand. A secret
  belongs in a file that holds nothing else.

## 3. Left for later

- **A refused key is news at the first 401, not after ten retries** (AGT3b). The pipe door's stream
  says `api_retry` with `error_status: 401` on the first attempt. Ending the session there, with a
  sentence naming the account, saves the minutes the retries cost. Expired sign-ins would benefit
  from it too.
- **Codex key accounts**, once measured.
- **A door runs as its owner's accounts** (AGT7). `accountOf` is declared and the page groups by
  it, but the driver resolves a door's accounts, defaults and keys under the door's own name.
  So an account made for Claude Code is invisible to Claude Code over the protocol door, and a
  Codex account never reaches `codex-acp`.
