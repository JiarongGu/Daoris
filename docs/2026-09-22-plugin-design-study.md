# Plugins — what Daoris already has, what to take, and what to decline again

> Written 2026-09-22 at the owner's direction: *"we should also have plugin system (you might check
> how yaorin did or if deepseek-harness has a even better plugin design you can also take)"*. This is
> **ARCH1**, which the backlog held for exactly this question, widened by one reference.
>
> Both references were **read on this machine**, never built or modified — one is another
> repository's installed application and the other a vendored package tree. Nothing here changed
> either.
>
> **What followed:** the plugin design is `2026-09-23-plugin-design.md` (D64). PLUG2, the question of
> D4's "no opt-out", was decided as **D71**: a pack may switch a core row off, and the repository
> confirms it.

## 1. The finding that reorders the question

**Daoris already has three extension systems, and none of them is called one.**

| What extends | How | Loads code? |
|---|---|---|
| **Doctrine** | `canon/packs/<name>/pack.json` + `rules/`, `knowledge/`, `skills/` | no |
| **Gates** | `daoris.gates.json` — a named list of commands | no |
| **Harnesses** | the **ACP door** — any tool speaking the protocol (D53) | no |

So the useful question is not "should Daoris have plugins" but **"where is extension still impossible,
and what do the two references do better where it is possible?"** Answering it that way keeps three
standing decisions intact instead of reopening them by accident.

## 2. Yaorin — a manifest, a capability list, and definitions beside code

Read from its installed `data/plugins/<id>/`:

```json
{ "id": "yaorin.source", "apiVersion": 3, "name": "…", "version": "1.0.0",
  "entry": "Yaorin.Plugins.Source.dll", "capabilities": ["browse"], "definitions": true }
```

Four things are worth taking, and one is worth taking **now**:

- 🔴 **`apiVersion` is an explicit integer.** The host knows before loading whether it can. Daoris's
  `pack.json` has **no such field**: a pack written against a newer canon, installed by an older CLI,
  fails in whatever way it happens to fail. This is the cheapest and highest-value thing in either
  reference.
- **Declared `capabilities`.** The host knows what a plugin claims *before* loading it, so a roster can
  be built and a conflict refused without executing anything.
- **`definitions: true` — data beside code.** A plugin may extend by **declaration** (site catalogues)
  as well as by DLL, and most of them do. This is the same instinct as a canon pack, arrived at
  independently, which is the strongest kind of agreement.
- **Official plugins are bundles**: one id groups many providers under one capability, rather than
  twenty ids nobody can keep straight.

## 3. dsh — a runtime, and three ideas worth more than it

dsh's plugins are Cordis packages composed by ordered patch layers. Three ideas, verified in its tree:

- **A capability seam is complete only with all three roles** — Service Definition, Service Provider,
  Consumer. A seam with a provider and no definition is a hook somebody added, not a seam.
- 🔴 **Registrations are effects.** Confirmed in `dsh-skill-filesystem`: contributions are made inside
  `ctx.effect(function* (…))`, so unloading unwinds every one of them. Anything that can be loaded at
  runtime must be able to *leave*, and a plugin system whose plugins cannot leave is an installer.
- **Composition is ordered layers, and a layer may disable a row** — `- id: x / disabled: true`.
  Daoris's manifest `packs: []` is a flat set with no override and no disable.

**The runtime itself is declined again**, on the evidence already gathered: 561 MB, a developer
preview whose tree moved 1,687 commits in the week it was measured, and D52/D53 both took its
*structure* while refusing to live inside it. Nothing in the owner's sentence requires the runtime.

## 4. Where extension is genuinely impossible today, and what to do

| Gap | Verdict |
|---|---|
| A pack cannot say which canon it needs | 🔴 **Fix — PLUG1.** Yaorin's `apiVersion`, exactly. |
| A pack cannot disable or override what core installs | **Consider — PLUG2.** dsh's layer rule; wants a decision, since D4 says core installs with no opt-out. |
| A third party cannot add a session adapter | **Decline, again.** D23, D24 and TOOL5 all say *not a registry* — and the **ACP door is already the answer**: a harness speaking the protocol needs no Daoris code at all. This is a protocol where the references have a binary API, which is strictly the better position. |
| A third party cannot add a view | **Decline, again.** D52 rejected the plugin runtime for the surface, and nothing here changes that evidence. |
| Nothing is *named* a plugin system | 🔴 **Fix — PLUG3.** Packs, gates and the ACP door are three extension points nobody is told about. |

## 5. The rule to adopt before any of it

Whatever Daoris ever loads at runtime, it takes dsh's rule wholesale: **a registration is an effect
that returns its disposer.** Daoris has no dynamic loading today, which is precisely why the rule is
cheap to adopt now and expensive later — a seam built without it acquires unload bugs that are
indistinguishable from leaks.

And Yaorin's, which costs nothing: **a manifest declares its API version and its capabilities, and the
host reads both before it loads anything.**

## 5a. Read again after D64 (2026-09-23) — what else the two references offer, and why not now

Both trees were read a second time once the plugin system existed, looking for the next thing worth
taking. Recorded so the next reader does not redo the survey:

| Reference feature | Daoris's answer | Verdict |
|---|---|---|
| dsh's hook bridges (`dsh-hooks-claude-code`, `-codex`) map a harness's hook files onto its extension points | HELP3 — *where the guard lives* — has a measured third answer (`--settings` at spawn, ACP4's shape) | **Owner's** (a design question), not a build |
| dsh's scheduled tasks and `/loop` | A plugin on `quest/consider` (the tracked example holds by title; a clock is one line more) | **Plugin territory now**, nothing for the host |
| dsh's session query and search over session logs | The Work frame lists sessions; nothing searches transcripts | Not asked for; transcripts are machine-local by D47 §4, so it would be shell-only — **held until somebody wants it** |
| dsh's workflow engine, agent teams, PTC runtime | — | **D1**: Daoris is process tooling, not an LLM product |
| dsh's `--dump-config` (see the tree your machine boots) | `daoris status --machine`, `daoris plugin list`, `daoris harness list` | Already three doors; no fourth |
| Yaorin's self-improvement cortex (memory, scoring, traces around every model call) | The knowledge index is the memory; no model runs in Daoris (D24) | **Declined** — the harness owns its own learning |
| Yaorin's plugin tasks as durable jobs with progress | A session IS the job, with a record, a transcript and a console | Already true |
| Yaorin's UI federation for plugin screens | D52: no plugin views | **Declined again** |
| Yaorin's cross-device pairing | The remote (D47) | Already true, differently |

The system taken from the two references is complete for what a plugin can do today; what would
change it is a plugin somebody writes asking for a point the host lacks (PLUG7).

## 6. What this study does not settle

- **Whether core should be overridable at all** (PLUG2) reopens D4's "no opt-out", which was a
  deliberate choice about doctrine rather than a limitation. It is the owner's.
- **No measurement of a third-party author's experience**, because there are no third parties: the
  live consumer count is zero, and a plugin API validated by nobody is a draft that looks like a
  contract — the same trap `canon-authoring` names for packs.
