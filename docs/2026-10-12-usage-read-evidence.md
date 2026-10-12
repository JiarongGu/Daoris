# Evidence: reading an account's plan usage without a prompt (USAGEREAD1)

Measured 2026-10-12 on the owner's machine for D165 point 1 (`docs/2026-10-12-account-switch-design.md` §1.1). The
accounts are labelled A, B and C here, not by their directory names, and no amount of money is quoted.

## The answer

1. 🔴 **Claude Code answers its plan's windows to a control request, with no prompt and no model request.** Asked of
   the native CLI **2.1.296** under an account's home (`CLAUDE_CONFIG_DIR`), `get_usage` with `skip_behaviors: true`
   answered in **1.5 to 2.6 s** from the spawn. The process wrote two lines, both `control_response` (the `initialize`'s
   and the `get_usage`'s): no `system/init`, no `assistant`, no `rate_limit_event`. The answer's `session.total_cost_usd`
   was 0. Two reads made back to back gave the same five-hour and weekly use.
2. **The reading is the account's whole use, and it differed from Daoris's.** Read against the install's `windows.json`
   the same hour:

   | Account | Plan | Read: five-hour, week | Daoris's reading | Its age |
   |---|---|---|---|---|
   | A | `team` | 23%, **79%** (`warning`) | week 60%, *near* by the agent's word | 15 h |
   | B | `max` | 4%, **100%** (`critical`) | week 99%, *near* | 2 h |
   | C | `team` | 0%, **100%** (`critical`) | week refused | 17 h |

   Account A's week had risen by 19 points since a session of Daoris's last spoke there, spent outside Daoris (C4). Over
   the minutes the measurement took it rose another point while other sessions ran on it, and back-to-back reads stayed
   level, so the read itself spent nothing.
3. **The server judges each window too.** The answer's `rate_limits.limits[]` rows carry `severity`: account A's week was
   `warning` at 79% with about 60% of its week gone, and B's and C's `critical` at 100%. Daoris records it as the maker's
   word and does not act on it alone, as with the CLI's warning (design §1.3).
4. **A home that is not signed in answers `rate_limits_available: false`**, `subscription_type: null` and no windows, in
   0.75 s. Measured by accident on a stray folder under the install's harness homes, which an earlier mistyped path had
   made and this measurement's own mistyped path wrote to again; the folder is the owner's to remove.

## The request and its answer's shape

Written to the CLI's standard input, one JSON line each, with `--input-format stream-json --output-format stream-json
--verbose --strict-mcp-config`, from an empty folder under `_fixtures/`:

```
{"type":"control_request","request_id":"init","request":{"subtype":"initialize"}}
{"type":"control_request","request_id":"usage","request":{"subtype":"get_usage","skip_behaviors":true}}
```

The reply is `{"type":"control_response","response":{"subtype":"success","request_id":"usage","response":{…}}}`, whose
body, as measured (the SDK 0.3.284 declaration, `sdk.d.ts:4195-4260`, names fewer fields):

| Field | Measured |
|---|---|
| `subscription_type` | `team`, `max`; `null` where not signed in |
| `rate_limits_available` | `true`; `false` where not signed in |
| `rate_limits.five_hour`, `.seven_day` | `utilization`, a whole percent 0 to 100; `resets_at`, ISO 8601 with microseconds and `+00:00`; and `limit_dollars`, `used_dollars`, `remaining_dollars`, `locked_reason`, all `null` here. A five-hour window not yet opened answered `resets_at: null` at 0% |
| `rate_limits.seven_day_opus`, `.seven_day_sonnet`, `.seven_day_oauth_apps` and about fifteen code-named windows | `null` on every account read |
| `rate_limits.limits[]` | the server's rows: `kind` (`session`, `weekly_all`, `weekly_scoped`), `group`, `percent`, `severity` (`normal`, `warning`, `critical`), `resets_at`, `scope` (a scoped row names a model family), `is_active` |
| `rate_limits.model_scoped[]`, `weekly_scoped_shares[]` | a model family's week and its share of the whole week |
| `rate_limits.extra_usage`, `.spend` | usage credits: enabled, the limit, what was used, the currency; not read by D165 |
| `session` | this process's own totals: `total_cost_usd` 0 |

## What this does not establish

- **The protocol door's CLI** (the SDK 0.3.284's bundled 2.1.284) was not run: its declaration has `get_usage`, so it is
  expected to answer, unmeasured. The read needs only the account's home, so USAGEREAD2 may ask the native CLI for
  either door's account.
- **Codex** was not measured here; CODEXUSE1 and CODEXUSE3 read its app server (`docs/2026-10-07-codex-usage-evidence.md`).
- **An expired sign-in**: every account read was signed in. Whether the CLI refreshes a stale token at `initialize` or
  answers `false`, and whether a read under a home a running session also uses ever disturbs that session, are not read.
- **The read writes to the home** as any start does: the stray folder gained `.claude.json` and `sessions/`.
- The request is declared *experimental, may change*: USAGEREAD2 parses defensively, and absent is never zero (D57).
