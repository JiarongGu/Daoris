# What each agent in use says of an account's plan (2026-11-24)

Measured for ACCOUNTPLAN1 on this machine at commit `92d1c0352b`, one read per account, only the fields named here kept.
Never a credential's value, an email or an organisation.

| Agent | Where it says the plan | Words seen | Names a size |
|---|---|---|---|
| Claude Code 2.1.331 | `claude auth status --json`, `subscriptionType` and `planTier` | `team` with `planTier: other`; `max` with `max_5x`; `max` with `max_20x` | the tier does (`max_5x`, `max_20x`); `max` and `team` do not |
| Claude Code 2.1.331 | the usage read (`get_usage`), `subscription_type` | `team`, `max` (recorded in `ClaudeUsageTests`); `null` signed out | no: `max` is the same word for both Max sizes |
| Codex 0.171.0 | the app server's `account/rateLimits/read`, `rateLimits.planType` | `team` (the one account on this machine) | no |

- **Where the tier is.** Only the status tells `max_5x` from `max_20x`; the usage reading says `max` for both. So the status's tier
  is read first and a usage reading's bare type never replaces a kept tier of that type (`AccountWindows.Planned`).
- **Codex.** The answer carries `planType` beside the windows, and Lyntai's reader already returns it (`AgentAllowanceReading.Plan`);
  Daoris's reader and its layer binding dropped it. Neither the answer nor the binary states a size for any plan, so no size is
  declared: its plans weigh as the base, and a size is declared only from a measured word.
- **Outside use.** Neither agent's usage answer separates the sessions Daoris started from other use: the fields read here and in
  the recorded answers (`ClaudeUsageTests`, `CodexUsageTests`, `docs/archive/2026-10-07-codex-usage-evidence.md`) give a share of
  a window and a reset, and none names who spent it. The person narrowed the row on 2026-11-24 to drop that share; an account they
  do not want used is switched off (ACCOUNTSWITCH1).
- **Units.** `max_5x` and `max_20x` name their multiple in the word; `pro` as the base they are multiples of is the maker's meaning
  of the word, not a measured field, and no account on this machine is on it.
