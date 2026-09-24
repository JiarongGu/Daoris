# DEPLOY1 — does the protocol door need the trust flag? Measured (2026-09-24)

**Answer: yes, as far as an untrusted room can show.** Over the ACP door, Claude Code ignored an
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

## What it means

DEPLOY1's open question stands on both doors: should adoption **ask** and write the flag (b), or
does the pipe door stay documented as needing a person's first visit (c)? The same hold stops ACP2's
proof run (`tools/acp2-proof.mjs --drive` held on its scratch repository, 11/13, no login spent) and
would stop a real intake in its room (INT4f), until the person grants trust to those directories.
