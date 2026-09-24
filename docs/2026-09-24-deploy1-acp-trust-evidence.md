# DEPLOY1 — does the protocol door need the trust flag? Measured (2026-09-24)

**Answer: for a repository's own allow-list, yes, on both doors. For the rules Daoris hands over at
spawn (PERM1), no** — see the second measurement below.

**The first measurement: yes, as far as an untrusted room can show.** Over the ACP door, Claude Code ignored an
untrusted room's own `permissions.allow`, the same as the pipe door. The driver's hold (DEPLOY1's
detection half) is therefore right for both doors. It was applied to the ACP door before anyone had
measured it, and this measurement confirms it.

## How

`tools/acp-trust-probe.mjs`, run with the owner's authorization for real sessions (2026-09-24), on
the machine's own signed-in account, with the enclosing agent session's `CLAUDE*` environment
stripped. `claude-agent-acp` 0.79.0 (pinned by `daoris agent pin` into a scratch Daoris home) and
Claude Code 2.1.281. Two fresh git rooms under `_fixtures/`, neither trusted by the account (the probe
reads `~/.claude.json` and refuses a trusted room). One prompt each, in `acceptEdits` mode as the
driver sets it: make one empty commit. Every permission request was refused, as Daoris refuses them
(D52).

| Room | Its `.claude/settings.json` | Permission requested? | Commit made? |
|---|---|---|---|
| control | none | yes | no (refused) |
| allowed | `permissions.allow: ["Bash(git commit:*)"]` | **yes** | no (refused) |

The control shows the probe can see a gate. The measurement shows the room's allow-list was not
applied.

## What it does not settle

- **Untrusted, or never read?** The same probe in a TRUSTED room would tell the two apart: whether
  the adapter honours a trusted room's allow-list, or reads project settings on no condition. Making
  a room trusted is the person's grant, so that half waits on the owner.
- **The first probe asked for `git status` and was inconclusive.** Claude Code approves read-only
  commands on its own, so the control ran it unasked. A probe of an allow-list has to ask for a
  write.

## The command-line tier is honoured untrusted, on both doors (measured the same day)

PERM1 (D72) hands every Claude Code session Daoris's own rules as the harness's command-line
settings tier. Measured in fresh, untrusted git rooms with the same one-commit prompt:

| Door | How the rule arrived | Permission requested? | Commit made? |
|---|---|---|---|
| ACP | `_meta.claudeCode.options.settings` on `session/new` (the probe's third room) | no | **yes** |
| pipe, control | nothing | (refused under `-p`) | no |
| pipe | `--settings <file>` allowing `Bash(git commit:*)` | no | **yes** |

**So the trust flag gates only a repository's OWN allow-list.** Rules Daoris hands over at spawn
reach an untrusted session on either door. With PERM1's `connector` default carried, a session can
take and close its quest without anyone trusting the folder. The driver's hold was written on the
opposite assumption, so it is now stricter than the facts. DEPLOY1(b)'s trust command still decides
whether a repository's own rules count, but it no longer has to gate driving.

## What it meant before PERM1

DEPLOY1's open question stands on both doors: should adoption **ask** and write the flag (b), or
does the pipe door stay documented as needing a person's first visit (c)? The same hold stops ACP2's
proof run (`tools/acp2-proof.mjs --drive` held on its scratch repository, 11/13, no login spent) and
would stop a real intake in its room (INT4f), until the person grants trust to those directories.

**Decided the same day: (b), recorded as D73.** Daoris asks per folder, then writes the flag: the
terminal's `daoris agent trust … --yes`, and the screen's *trust this folder…* on a hold the driver
is showing.
