---
name: fresh-design-pass
applies_when: planning a foundational redesign, when new ideas keep bending to old mechanisms in a long-running project, or at a periodic whole-product review
enforces: independent designs from one purpose brief, a prior-art survey whose claims say how they are known, and one comparison against the code naming the route, what would flip it and a trial judged by criteria written first; the owner's closed questions answered as decisions
---
<!-- daoris: core/core/knowledge/fresh-design-pass.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# The fresh-design pass: design from the purpose, then compare with what exists

**Write two designs from a short purpose brief alone, with no code, documents or decisions in hand, survey the prior
art, then have one reader compare all of it with the current product. The value is the comparison, never either
design.**

## Why

Months of context make every new idea arrive already shaped like the mechanisms that exist: a reader who
knows why each part is the way it is proposes changes that keep it that way. A design written from the
purpose alone cannot inherit those shapes, so it names the gaps the history hides, and setting it beside
the code turns a vague direction into questions the owner can answer in one line.

Each step below is there because its absence went wrong once. Two designs by one maker were counted as two
views, though they shared every blind spot. A survey's claims written from memory read as facts, and a route
was argued from a feature the cited project did not have. An outside briefing that had already read the decision
it was judging was offered as a third vote. A comparison that only ranked ideas left the owner to work out which
way to go and what would change their mind. And a trial with no criteria written first was kept because it had
been built.

## How to apply

1. **The purpose brief.** One page in the owner's picture, deliberately without architecture, names, files or
   decisions:

   ```markdown
   # <Product>: the purpose
   - What it is for, and who uses it.
   - Its parts, as the owner sees them.
   - What the owner must be able to do, and to see.
   - The goals, in the owner's words.
   ```

   The brief is everything a designer is handed. The repository's history (its code, decisions, backlog and
   earlier designs) stays out: that is the method's point.

2. **Independent designs.** Two makers of different model families where two are ready, otherwise two strong
   models, each at its highest reasoning setting, each in a session of its own, each writing one file from the
   brief alone. Neither reads the code or the other's design. Each design records its independence: its maker
   and account, what it read (the brief, by a digest), and whether it saw another answer. Where a condition did
   not hold (one family, a shared session, a glimpse of the other design), the record says so, and the pair counts
   as one view.

3. **The prior-art survey.** One reader, with the web where the harness has it, lists how comparable products
   and published designs solve the same purpose. Each claim is marked by how it is known: **confirmed** (read at a
   cited source in this survey), **from knowledge** (remembered, not read now) or **not read** (a source named
   but not opened). A route argued from an unconfirmed claim says so beside it.

4. **The comparison.** One reader, who may read the code, sets the designs and the survey beside the current
   product and writes:
   - where the product today is worse, the top items ranked by value over effort;
   - **the route** it recommends (evolve what exists, rebuild a part, or adopt from prior art), with its reasons;
   - **the evidence that would flip it**: each observation, stated so it can be checked, that would make another
     route the better one;
   - **the trial**: the one area it is tried on first, the baseline recorded before it starts, and the keep and
     retire criteria, written before the trial begins;
   - the questions only the owner can answer.

   Its output is a route and a sorted list of requests, never a decision by itself.

5. **The owner's closed questions.** Each says what it is, what yes changes, what no changes and where to look,
   so it can be answered in one line. Each answer is recorded as a decision, stated as the choice and its reason,
   not as the owner's words; a question left unanswered keeps the recommended route's default and says so.

6. **Adoption by trial.** A structural idea is tried first on one area, measured against the baseline (what a
   change reads before its first edit, time and tokens per change, the main line's red rate, owner
   interventions), and kept or retired by the criteria written in step 4. A retired design stays in the library
   as reference.

## What it is not good at

- It cannot see live constraints: permissions, data flows, a migration's records already written. The
  comparison is where those come back in.
- It costs a full design per model; run it for a foundation or a periodic review, not for a feature.
- Adopting one design whole throws away what the comparison found. Take the parts the comparison ranks.
- A survey is only as good as its confirmed claims; one made mostly from knowledge is a starting list, not
  evidence.
