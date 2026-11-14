# A session's context of Daoris's own, rendered into each agent's format (CONTEXT1)

Status: proposed, 2026-11-14, under decision D293 point 5. Source: brainstorm `764f2b79`, **one maker** (Anthropic `g1`, synthesis
`s1`): the OpenAI pass was unavailable because the Codex workspace's spend cap was reached; its opinion is asked once the cap is
raised. The packet left out the two conversation carries and cut ContextBrief and the course decision (BRAINPACKET1), so claims
marked *(to check)* are read against the code first. It builds on SESSIONLIFE1's session record.

**The second maker's reading** (DeepSeek, `268f92b4`, a secondary opinion): agrees on one renderer the brief and the standing block
fold into, a pure course decision choosing resume or a fresh start, a port whose forms each provider declares, a resume handed only
what it cannot know, size bounded by channel (inline under the budget, past it a file the first prompt names), and proof on the
stand-ins by characters handed. Taken from it: the rendered context is cached under a stamp of the records it covered and rendered
again only when one moves. Not taken: starting every step fresh even on the same agent (its own confidence low, its risk high, and
against the person's aim of one continued context).

## The shape

1. **The context is a log of typed entries per session, kept with the session.** Entry kinds: the work, what was decided, what was
   found, where it stands, what the tree holds. Two writers: the agent's door reads back each turn's end, and the workflow writes
   each step's result. An entry replaces an earlier one by id; nothing is deleted and no model rewrites it. The log has no bound;
   each rendering has one. This is ContextBrief generalised: today it is a pure function of the line's records with no model.
2. **One provider port, `IContextRendering`.** It declares its abilities and offers `Render(context, form)` and `ReadBack(turn)`.
   Three forms: **Resume** (the agent's own conversation plus the entries since, as words), **Prompt** (a bounded head plus files),
   and **Seed** (a native transcript; absent until measured, see 6). Claude Code's and Codex's conversation carries go behind Resume;
   a plugin's door gets Prompt. Daoris chooses the form; the provider only translates. Read-back maps the wire to entries and drops
   Daoris's own composed words. *(to check: that both carries fit one Resume operation.)*
3. **Two tiers on every provider.** A bounded head in the agent's first words (the work, the decisions, where it stands, what the
   tree holds, an index of the findings) under the provider's budget, and the rest as files outside the tree plus a `context_read`
   tool on Daoris's connector. This generalises the standing block's fitted text and pointer file, and is the floor for any agent
   that cannot import a conversation.
4. **One course decision chooses the form.** From numbers Daoris already reads: same agent and account, whether the conversation is
   held, time since its last turn, its last input tokens, the head's weight, the model's window. Warm on the same account: Resume
   plus the new entries. Cold: Resume only while the transcript is small next to the head, else Prompt. Another agent, a lost or
   refused conversation, or a transcript past the window: Prompt. Its thresholds are measured before they decide anything.
5. **"Found" is filled two ways.** A `context_note` tool on the connector (as `memory_write` is): one finding with path and lines,
   kept as that agent's claim at the head it was made. And a reading map from the wire with no model: per file, which turn read it,
   how often, at which head. At render, git says whether each cited path changed since, so a finding is said to still hold or to
   need reading again. This aims at the measured waste: a brief names the files a turn read, never what it learned.
6. **A seeded native transcript is an experiment, never the floor.** Per provider, a minimal native conversation written from the
   context is kept only where measured to load and cost less than Prompt.

## The migration, one way per job

1. `SessionContext` comes in; `ContextBrief.Compose` becomes its Prompt rendering, held byte-identical by a golden test.
2. The standing block becomes the rendering's place section; its fitted text and pointer, the file tier.
3. The two conversation carries become each provider's Resume.
4. Go-on, briefed move, hand-over and limit move keep their triggers and end in one wake that asks the course decision its form.

## Proof

A cost reading per wake (form, characters handed, first-turn tokens, calls and seconds before the first write), stand-in tests on
both doors (a hand-over's new turn holds the head and an earlier finding; a re-wake after a check hands only the verdict), and one
real task moved Claude Code to Codex and back, measured before and after against today's hand-over (2 minutes and a fresh read).

## Decided (2026-11-14, the steward for the person, who set the principle in D293 point 5)

| Choice | Decided |
|---|---|
| Where the context's model lives | **In Lyntai**, beside what it already has for this (`Lyntai.Core.Agents`: `AgentTranscript`'s turns, `AgentHandover`'s resume-or-brief judgement, `AgentBriefComposer`): the typed entries, the rendering port and the bounded head are provider-neutral and belong to the agent library; Daoris writes its sessions' workflow entries and keeps them with the session record on the service, so another machine can resume it |
| Native transcript import | **Not now**: an experiment per provider, kept only where it measures cheaper |
| The `context_note` tool | **Yes**, beside the reading map that needs no agent's help |

Lyntai's part is asked of Lyntai as its own quest (a change spanning two repositories is two changes and a request); Daoris's part
follows SESSIONLIFE1's session record in the same goal.
