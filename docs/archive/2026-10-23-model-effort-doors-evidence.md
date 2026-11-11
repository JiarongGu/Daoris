# What each door accepts for model and effort, measured (MODELROLE1a, D205)

**Carried by:** D205 and its design, `2026-10-23-actors-design.md` §3.2, §3.3 and row a of §14. A record, not a
contract: rows b to h read it, and the stubs in `AcpTests` and `ClaudeStreamJsonTests` speak its shapes.

**Date:** 2026-10-23 · **Versions:** `claude-agent-acp` 0.84.0 (its bundled SDK's CLI), `codex-acp` 2.1.1, Claude
Code 2.1.309 on the pipe door · **dsh:** not installed on this machine, so not measured · **Accounts:** four
`claude-code` accounts, called A and B (both on a Team plan) and C and D (both on Max) here, and one `codex`
account (a ChatGPT Team plan).

## How it was measured

Every run started in a scratch folder under the repository's gitignored `_fixtures/`, with the one-line prompt
*Reply with the single word OK and nothing else.* No sign-in or login command ran, and no file of this repository
was the agents' to touch.

- **Real turns, few.** Real-endpoint prompts: four on the ACP door (two ran, on B and D; A and C answered with
  their weekly limit), seven on the pipe door (two ran, on B and D; five were refused before any model answered,
  by a limit or by the model check below), and one on `codex-acp`. An account at its weekly limit still
  answers the model check, so the refusal words were read on such an account, at no cost.
- **Model and effort read off the request itself, with no quota spent.** A loopback endpoint in the scratch
  folder (`ANTHROPIC_BASE_URL=http://127.0.0.1:<port>`) kept only each request body's `model`, `output_config`
  and `thinking`, never a header, and answered every request with an error. Twenty pipe turns and two ACP turns
  ran this way, on C. A turn's first request is the tool's own title side request (effort `medium`, a JSON
  schema output); the figures below are the main turn's.
- **Settings files watched.** Before every run, a scratch script hashed and copied every settings file the run
  could touch: each Claude account home's `settings.json` and `settings.local.json`, the person's user-tier
  `settings.json`, the Codex account's `config.toml` and the scratch folder's own `.claude/settings.json`; it
  compared them after. The credential stores and the tools' state files were hashed and watched only, never
  copied or written. The first eight snapshots predate the fourth account's home and the Codex file being
  listed; both were listed before either was first run.

**Outcome: no settings file changed in any run, so nothing was restored.** Every Claude account's
`settings.json` and `settings.local.json` was absent before and after, the user tier kept one hash throughout,
and the Codex `config.toml` was absent before and after. The tools changed their own state files (`.claude.json`)
on every run, and one account's credential store once, the tool refreshing its own sign-in; neither was written by
the measurement.

## The ACP door: `claude-agent-acp` 0.84.0

The driven shape: `initialize`, `session/new` with `_meta.claudeCode.options.settings` naming a rules file, the
posture through `session/set_mode` (`auto`), then `session/set_config_option` before the first prompt.

### What `session/new` offers

Four options, the same on all four accounts, Team and Max alike:

| id | category | opening value | values |
|---|---|---|---|
| `mode` | `mode` | `default` | `default, acceptEdits, plan, auto, bypassPermissions` |
| `model` | `model` | `default` | `default, opus, claude-fable-5-1, sonnet, haiku, claude-sonnet-5, claude-opus-5, claude-fable-5, claude-opus-4-8, claude-opus-4-7, claude-opus-4-6, claude-sonnet-4-6` |
| `effort` | `thought_level` | `default` | `default, low, medium, high, xhigh, max` |
| `fast` | `model_config` | `off` | `on, off` |

- **Model and effort apart**, with `default` a value of each. No `models` field (the older model list) came
  back.
- **The offer is the endpoint's, not the version's.** Under the loopback endpoint, the same version on the same
  account offered `default, opus[1m], claude-fable-5-1[1m], sonnet, sonnet[1m], haiku`. A reading is good for the
  account and the endpoint it was read under (design §9.2's key).
- **Offers did not differ by account** on this install: the four accounts, on two plans, answered the same list.
  No model was found that one account offers and another lacks, so a per-account difference is not observed here.

### What `set_config_option` answers

```
→ session/set_config_option {"sessionId":"…","configId":"model","value":"sonnet"}
← {"configOptions":[ mode=auto, model=sonnet, effort=default {default,low,medium,high,xhigh,max} ]}
→ session/set_config_option {"sessionId":"…","configId":"effort","value":"low"}
← {"configOptions":[ mode=auto, model=sonnet, effort=low ]}
```

- **The answer returns the whole option list** on every accepted change, never an empty result.
- **An accepted alias returns the requested id.** `sonnet`, `opus`, `haiku` and `max` came back as the current
  value exactly as asked.
- 🔴 **A full id not in the list is accepted and returned as its alias.** `claude-sonnet-5-5` asked, `sonnet`
  returned. So a returned value that differs from the request is not by itself a refusal (§3.2, round 3's rule
  holds: only another value the list offers, other than the alias of the asked id, counts as one).
- **The effort list follows the model.** On `sonnet` and `opus` it stays `default…max`; on `haiku` the `effort`
  option **disappears** from the list, and a later `effort` set answers *Unknown config option: effort*. The
  `fast` option disappears on `sonnet` and `haiku`.
- 🔴 **A model change can move the posture.** Setting `haiku` after `auto` sent
  `{"sessionUpdate":"current_mode_update","currentModeId":"acceptEdits"}`, and the returned list's `mode` read
  `acceptEdits`. Setting `sonnet` or `opus` left `auto` in place.
- **A refusal is a JSON-RPC error, before any prompt:**

  ```
  {"code":-32603,"message":"Internal error","data":{"details":"Invalid value for config option model: claude-nonexistent-9"}}
  {"code":-32603,"message":"Internal error","data":{"details":"Invalid value for config option effort: bogus"}}
  {"code":-32603,"message":"Internal error","data":{"details":"Unknown config option: effort"}}
  ```

  A model the list does not offer is refused the same way, so on this door a model is refused at the set, never
  on a turn.
- **What ran is what was set.** Through the loopback endpoint, `sonnet` and `low` became a main request of
  `claude-sonnet-5-5` with `output_config.effort: "low"`; the default model with `xhigh` became `claude-opus-5-5`
  with `xhigh`.
- **`set_config_option` writes no settings file.** Across eight set-and-open runs on four accounts the account
  homes' `settings.json` stayed absent and the user tier unchanged. D98's boundary holds: a session's choice does
  not move the account's default.

### Whether a turn reports its model

Yes, twice. Each `usage_update` during the turn carries the main model:

```
{"sessionUpdate":"usage_update","used":27029,"size":1000000,"_meta":{"_claude/model":"claude-sonnet-5-5"}}
```

and the prompt's answer carries every model the turn used, the tool's own side model included:

```
{"stopReason":"end_turn","usage":{…},"_meta":{"quota":{"token_count":{…},"model_usage":[
  {"model":"claude-haiku-4-5-20251001","token_count":{…}},{"model":"claude-sonnet-5-5","token_count":{…}}]}}}
```

A limited account's prompt is refused as `{"code":-32603,"message":"Internal error: You've hit your weekly limit · resets …","data":{"errorKind":"rate_limit"}}`.

## The ACP door: `codex-acp` 2.1.1

Its workspace did not refuse for credits: the turn ran.

| id | category | opening value | values |
|---|---|---|---|
| `mode` | `mode` | `agent` | `read-only, workspace-write, agent, agent-full-access` |
| `collaboration_mode` | `collaboration_mode` | `default` | `default, plan` |
| `model` | `model` | `gpt-6.1-sol` | `gpt-6.1-sol, gpt-6-astra, gpt-6-sol, gpt-6-luna, gpt-5.6-sol, gpt-5.6-terra, gpt-5.6-luna` |
| `reasoning_effort` | `thought_level` | `medium` | `low, medium, high, xhigh, max, ultra` |
| `fast-mode` | `model_config` | `off` | `off, on` |

- **Model and effort apart in `configOptions`, and together in the older `models` list** beside it:
  `current: "gpt-6.1-sol[medium]"`, available `gpt-6.1-sol[low]` … `gpt-5.6-luna[max]`. The categories are the
  ones Daoris reads; the option ids differ from Claude's (`reasoning_effort`, not `effort`), so a carrier finds an
  option by its **category**, never by its id.
- **No `default` value**: the opening values are the account's.
- **The answer returns the whole list, the requested id as the current value**, and the effort list follows the
  model: `gpt-6-luna` dropped `ultra`.
- **A refusal is a bare JSON-RPC error**, with no details: `{"code":-32602,"message":"Invalid params"}`, for an
  unknown model and for an effort the model does not offer.
- **The turn reports its model in the prompt's answer only**:
  `"_meta":{"quota":{…,"model_usage":[{"model":"gpt-6-luna",…}]}}`. Its `usage_update` carries no model.
- The Codex `config.toml` stayed absent.

## The pipe door: `claude -p` (Claude Code 2.1.309)

```
claude -p "Reply with the single word OK and nothing else." --output-format stream-json --verbose \
       --permission-mode acceptEdits [--model <value>] [--effort <value>] [--resume <id>]
```

Each row is the main request's body through the loopback endpoint, on one account.

| Case | Model sent | Effort sent |
|---|---|---|
| Nothing named | `claude-opus-5-5` | `medium` |
| `--model opus` / `sonnet` / `haiku` | `claude-opus-5-5` / `claude-sonnet-5-5` / `claude-haiku-5-5` | `medium` |
| `--model claude-opus-4-8` (a full id) | `claude-opus-4-8` | `high` (that model's own default) |
| Saved `modelSettings.<model>.effortLevel: low`, keyed by id or by alias, in the project's settings or a `--settings` file; no `--effort` | `claude-sonnet-5-5` | `low` |
| Saved `effortLevel: low` in the project's settings; no `--effort` | `claude-sonnet-5-5` | `low` |
| **`--effort high` against a saved per-model `low`** (either key) | `claude-sonnet-5-5` | **`high`** |
| `--model opus --effort max` | `claude-opus-5-5` | `max` |
| `--model haiku --effort high` | `claude-haiku-5-5` | `high` |
| `CLAUDE_CODE_EFFORT_LEVEL=low`, nothing else | `claude-sonnet-5-5` | `low` |
| **`CLAUDE_CODE_EFFORT_LEVEL=low` against `--effort high`** | `claude-sonnet-5-5` | **`low`** |
| `CLAUDE_CODE_EFFORT_LEVEL=low` against a saved per-model `high` | `claude-sonnet-5-5` | `low` |
| `ANTHROPIC_MODEL=haiku` against `--model sonnet` | `claude-sonnet-5-5` | `medium` |
| A first run with `--model opus --effort low` | `claude-opus-5-5` | `low` |
| **`--resume` that run with `--model sonnet`** | **`claude-sonnet-5-5`** | `medium` |
| **`--resume` that run with nothing named** | `claude-opus-5-5` (the account's default) | **`medium`** |

What follows from it:

- **An alias is resolved** to the current id, which `system/init.model`, the `assistant` message's `model` and
  `result.modelUsage` name. A real turn on B and on D with `--model sonnet` reported `claude-sonnet-5-5` in all
  three.
- **`--effort` holds against a conflicting saved per-model effort**, and `max` is carried. The design's carrier
  stands with no file fallback.
- 🔴 **`CLAUDE_CODE_EFFORT_LEVEL` outranks `--effort`.** Where the start's environment sets it, an appended
  `--effort` does not hold; §3.2's *your environment* reading is the right one.
- **`--model` outranks `ANTHROPIC_MODEL`**, as documented.
- 🔴 **`--model` holds on a `--resume` run, and nothing carries over without it.** A resumed session takes the
  account's model and effort again, not the ones its first run was given. So every run, a go-on and a second try
  included, must carry both flags (design §3.2, *On every run*).
- **The turn says no effort.** `system/init` carries `per_turn_effort_active: true` and no effort value; the
  effort is visible only in the request.

### When and in what words a model is refused

On the turn, after the prompt was handed and after `system/init` (which names the asked model unchecked), with no
output and no tool use; the process exits `1`:

```
{"type":"assistant","message":{"model":"<synthetic>","content":[{"type":"text","text":"There's an issue with the selected model (claude-3-opus-20240229). It may not exist or you may not have access to it. Run --model to pick a different model."}]},"error":"model_not_found",…}
{"type":"result","subtype":"success","is_error":true,"api_error_status":404,"num_turns":1,"modelUsage":{},
 "result":"There's an issue with the selected model (claude-3-opus-20240229). It may not exist or you may not have access to it. Run --model to pick a different model.",…}
```

- **One sentence for unknown and for retired** (`claude-nonexistent-9` and `claude-3-opus-20240229` answered
  alike), and the tool names it *may not exist or you may not have access*, so an unentitled model would read the
  same. No model was found that one of these accounts is not entitled to, so that case is the tool's own wording
  and not observed.
- **Recognised by** the `assistant` frame's `error: "model_not_found"` (and `api_error_status: 404`), not by the
  sentence. A limit is told apart the same way: `error: "rate_limit"`, `api_error_status: 429`, *You've hit your
  weekly limit · resets …*.
- **The model check comes before the limit**: an account at its weekly limit still answered `model_not_found`.

## What this settles in the design

- §3.2's ACP carrier: `set_config_option` for the `model` category, then the `thought_level` category, found by
  category, works on both installed protocol agents, returns the list, and writes no settings file.
- §3.2's *what the agent acknowledged*: both agents return the list. Claude returns the alias for a full id
  that has one, so a returned value counts as a refusal only where it is another offered value that is not the
  asked id's alias; until an id-to-alias map is kept, round 3's rule stands.
- §3.3: an unoffered model is refused at the set on both protocol doors, before any prompt, as a JSON-RPC error.
  The pipe door's refusal is the measured `model_not_found` frame on a silent turn.
- New for rows b and c: a model change on Claude's ACP door can move the posture (`haiku` from `auto` to
  `acceptEdits`), so the posture is read back after the model is set; and `haiku` on that door offers no effort,
  so an effort choice on it is *not offered* there (the pipe door carried `--effort high` to `claude-haiku-5-5`).
- Not measured: dsh (not installed); a model one account offers and another lacks; an unentitled model's refusal
  on a real account.
