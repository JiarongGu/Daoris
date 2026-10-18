# Cross-agent brainstorming — fan a problem across models, synthesize the divergence

Filed 2026-10-19 by the owner from a sibling workshop repository (one Daoris can drive). An idea for Daoris, because
cross-agent collaboration is its goal and this is one shape of it that a single harness cannot give itself.

## The idea

A single model brainstorming its own problem is bounded by its own blind spots. The value of *cross-agent*
brainstorming is **model diversity** — Claude and Codex (gpt) are trained differently and fail differently,
so fanning one well-scoped question to both and a third, then synthesising, surfaces options neither finds
alone. Daoris already speaks ACP to Codex and can spawn Claude; the missing piece is a *brainstorm* fan-out
distinct from the *implement* fan-out.

**Shape:**
1. **Scope** one question tightly, with the current-state context attached (the agents do not share the
   caller's memory: a subagent is handed its prompt, not the caller's hooks or context).
2. **Fan out** to heterogeneous agents: Claude-Opus (judgement), Codex-gpt (a different model's angle),
   optionally a Sonnet builder for feasibility. Each returns a STRUCTURED proposal list:
   `{idea, rationale, effort, risk, reversible?}` — not prose, so the synthesis can rank them.
3. **Synthesise** with one agent that dedupes, ranks, and — crucially — flags any proposal that would do
   something hard to reverse (archive files, change a verdict, mutate state) for an adversarial verify pass
   before it is believed. This mirrors a review desk: divergent generation, then a skeptical gate.
4. The output is a ranked proposal doc, not a change. A human or the caller decides what to build.

The motivating gap: from that workshop today, Codex is set up but only reachable via the
`@agentclientprotocol/codex-acp` adapter — there is no one-shot CLI, so a Claude session cannot fan a
question to Codex without an ACP client. Daoris IS that client. A `daoris brainstorm "<question>" --agents
claude,codex` that returns the synthesised proposals would make cross-model brainstorming a one-liner for
every repo Daoris drives.

## Worked example — the seed ideas it would have produced (2026-10-19)

The filer captured one model's side of a real brainstorm so the first run is not empty: about a dozen
proposals across three topics of the sibling workshop (a media comparison's speed, a game-text hook, the code's
layering), each one line with its mechanism and why. That example names the sibling's own parts, so it is
kept with the owner's private notes rather than here. Its shape is what matters for Daoris: per topic, a list
of ideas each with a mechanism and a reason, from one maker only; the other maker's side is exactly what is
missing without Daoris.

## Why this belongs in Daoris, not the workshop

The workshop is a set of single-purpose tools driven by one model and a person. Orchestrating *several*
agents (heterogeneous models, fan-out, synthesis, the ACP bridge to Codex) is exactly the cross-agent problem
Daoris exists for. Building it in the workshop would be the "god tool" mistake one level up. So the capability
goes here, and the workshop is a consumer that asks Daoris to brainstorm.
