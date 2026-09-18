---
name: autonomous-development
applies_when: planning how a task will be run and verified, or about to pause a reversible step for mid-task approval
enforces: the person sets the target and verifies the outcome; agents execute the steps between, verified by gates; destructive, irreversible, cross-repository and publishing actions stay explicitly human
---

# Autonomous development — the person at the ends, gates in the middle

**Development here is automation-first. A person states the target and verifies the result; the
steps between are executed by agents and verified by gates, not by per-step approval.**

## Why

Step-by-step interaction does not scale past a small system. A large application is built as many
subsystems and many verification runs, and a person approving every reversible step becomes the
bottleneck on exactly the work that needed no judgement — while their attention is spent long before
the one step that did.

Approval fatigue is the quieter half, and the dangerous one. A reviewer asked to confirm forty
mechanical steps stops reading them, and the step that deserved a real decision then arrives to a
reader trained to click through. Concentrating human attention at two points — the target at the
start, the diff and the gate results at the end — is what keeps that attention worth something.

The middle is not less verified for having no person in it; it is verified *better*. A gate runs
every time, identically, and exits non-zero; a person verifies occasionally, differently each time,
and tires. What a person uniquely supplies is judgement about outcomes — so the process should hand
them an outcome to judge: a reviewable diff, a green gate run, and a record of what was decided and
why.

## How to apply

- **Take the target, then run.** A reversible step that serves the stated target is executed and
  recorded, not proposed and awaited. Questions that arise mid-run are batched to the next
  checkpoint unless the work is genuinely blocked on the answer.
- **Done means gates green plus a reviewable record.** The tests, the drift and budget checks, the
  repository's own verification — and the diff, with the task and decision records updated. That
  bundle is what the person verifies, in one sitting, at the end.
- **The carve-outs do not move.** Destructive or irreversible actions, anything that leaves the
  repository — a write into a sibling, a publish, a release, a history rewrite — and committing the
  result remain explicit human decisions, however automated everything else is. Autonomy earns
  trust by never spending it there.
- **Record as you go, because nobody is watching the middle.** The decision log, the backlog and
  the fix log are what make an unattended run reviewable afterwards — a step that exists only in a
  session transcript did not happen, as far as the final verification can tell.
- **Work for another repository is published, never performed.** An agent that reaches a
  neighbour's boundary files the request through the proper channel and continues with its own
  work. Autonomy is scoped to the repository it runs in.
- **A step that genuinely needs a human choice surfaces as a decision, not a pause.** State the
  options, the recommendation and the reason at the checkpoint. A question arriving with its
  analysis is decided in seconds; a bare "may I?" in the middle of a run is a stall that teaches
  the person to stop reading.
