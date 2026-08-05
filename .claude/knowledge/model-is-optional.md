---
name: model-is-optional
applies_when: building any feature that uses a language model, an embedding model, or any AI service
enforces: every such feature has a floor that works without the model, reports which tier it ran at, and takes the model as swappable configuration
---
<!-- daoris: core/core/knowledge/model-is-optional.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# A model is a tier of fidelity, never a prerequisite

**No feature may require a model to function.** Anything that can use one must have a **floor that works
without it**, must **say which tier it ran at**, and must take the model as **configuration that can be
swapped or removed**.

## Why

Two reasons, and the near one is easy to miss behind the far one.

**The models turn over faster than your project will.** What is best today will not be best in a year.
A feature welded to one ages at the speed of the fastest-moving part of the stack rather than its own —
and the parts of a codebase that encode hard-won judgement should turn over far more slowly than the
inference layer underneath them. This is not aspirational future-proofing; it is refusing to let a
yearly-churning dependency set the shape of something that should outlast it.

**And most machines have no model available.** A developer without an endpoint, a build agent with no
credentials, a contributor on a laptop, an air-gapped environment. A feature that returns nothing
without one has quietly made an optional dependency mandatory — while discarding the work it could
have done regardless. That is the worst of both: the dependency's cost, none of the resilience.

The failure that earns this rule is specific. A feature built model-first returns empty, its author
reports the missing endpoint as *blocked*, and the blockage is treated as an external constraint rather
than as the design decision it actually was. The dependency was never required by the problem — only by
the implementation.

## How to apply

- **Ask what the feature can do with no model at all, and build that first.** Usually more than
  expected: exact matching, structural comparison, counting, ranking by overlap. Ship that as the
  floor, then let the model raise the ceiling.
- **If the honest answer is "nothing", it is a model wrapper.** That is allowed — but make it an
  explicit opt-in, and make the unconfigured path a clear message rather than silence or an error.
- **Name the tiers, and report which one ran.** A reader who cannot tell why a category is empty will
  assume a bug, and will be right to. Degrading silently is worse than not degrading.
- **A failure in the model half must not fail the whole.** Wrap it, carry the reason, continue with the
  floor — and never swallow cancellation, which belongs to the caller.
- **Take the provider, endpoint and model as configuration**, never as a hard-coded choice. Avoid
  defaults that quietly imply a particular runtime is installed.
- **Ask what the model is genuinely better at,** and give it only that. It is not better at exact
  comparison, at counting, or at anything with a deterministic answer — using it there is slower, more
  expensive and less correct.
