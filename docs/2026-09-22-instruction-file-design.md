# The instruction file — where the always-loaded tier lives

> Written 2026-09-22 (CANON8), after the owner saw a candidate adopter's layout and said it *"made a
> common management style for different agents, which is good to take"*. This is the contract for
> moving the always-loaded tier out of `.claude/rules/` and into the file every harness reads. Read
> with `docs/decisions/D7.md` (the tier is the directory — amended here), **D19** (the sync state
> space — extended here), **D13** (drift is measured against the lock) and **D48 §2a** (doctrine must
> not hard-require Daoris).

## 1. What was measured

`docs/2026-09-21-dsh-evaluation.md` §6.5 has the table and how each row was obtained. The three facts
that decide everything here:

- **`AGENTS.md` is the only file all three harnesses load.** Claude Code reads `CLAUDE.md`; dsh reads
  both; codex reads `AGENTS.md` and treats a Claude Code layout as something to *migrate*, not read.
- **`.claude/rules/` is read by exactly one harness.** dsh says so in its own limitations; codex's
  binary pairs `CLAUDE.md` with `"Migrate skills from … to …"` rather than with loading anything.
- **`@path` imports are Claude Code's**, not a convention. dsh does not interpret them either.

So Daoris has been writing an "always-loaded tier" whose always-loaded-ness was a property of one
agent harness rather than a guarantee the canon made. A repository adopted here and driven through
dsh or codex had `repository-owns-its-work` on disk and unread — which is the failure
`skills-workflow` was written from, arriving through the packaging instead of through a skipped step.

## 2. The decision

> **Amended by D128** (`docs/2026-10-02-setup-pilot-lessons-design.md` §2, WSSETUP14a): the region keeps the rules
> and their table; the knowledge and skill tables move to a generated index at the target's root, which the region
> points to, so the region no longer grows with a repository's own documents.

**The always-loaded tier lives in `AGENTS.md`, inside a region Daoris owns.** `.claude/rules/` is no
longer written. `CLAUDE.md` carries a one-line import so the harness that looks for the other name
finds it.

```
repo/
  AGENTS.md          the repository's own always-loaded text
                     + <!-- daoris:rules … --> … the core rules … <!-- /daoris:rules -->
  CLAUDE.md          @AGENTS.md
  .claude/
    knowledge/       read on demand        (unchanged)
    skills/          invoked by name       (unchanged)
```

**Why a region rather than a file of Daoris's own.** The adopter already owns `AGENTS.md` — the
repository this was measured on has 133 lines of its own in it — and a second file only a pointer
reaches is a file two of the three harnesses do not follow. A region is the only shape that puts one
copy of the rules where all three already look.

**What this buys beyond reach.** The repository's own always-loaded text and Daoris's now sit in one
file, separated by markers: the adopter reads their doctrine in one place, and the boundary between
what they maintain and what Daoris maintains is visible on the page rather than inferred from a
directory name. That is the "common management style" the observation named.

**D7 is amended, and its better half survives.** *The tier is the directory* becomes **the tier is the
location**: always-loaded is the region, on-demand is `.claude/knowledge/`, by-name is
`.claude/skills/`. There is still no `tier:` field to disagree with, and the always-loaded footprint is
still measurable — the region has a byte count exactly as the directory did, so CANON7's budget keeps
working and keeps meaning the same thing.

## 3. What does not move, and why

- **Knowledge stays `.claude/knowledge/`.** It is read on demand, which is a thing an agent does by
  being told a path — no harness has to auto-load it for it to work.
- **Skills stay `.claude/skills/`.** Invoked by name, and dsh's own bundle format is `<name>/SKILL.md`,
  exactly this layout — HELP2 adds the root rather than converting anything.
- **`daoris.lock` is still the authority.** Anything absent from it is invisible to the tool (D5), and
  that is what keeps a repository's own files safe. The region is a tracked artefact in the lock like
  any other; what changes is that one lock entry describes a *span inside a file* rather than a file.
- **Local always-loaded rules stay the repository's own** — they live in `AGENTS.md` **outside** the
  region, which is where they were always going to end up once the file held both.
- **Doctrine still does not hard-require Daoris** (D48 §2a). The region is committed text; it survives
  the tool's absence exactly as the directory did.

## 4. The state space — D19, for a region

D19 exists because this area was corrected four times before anyone wrote the table. A region has
every state a file has, plus three the file never had. Enumerated before implementation, as D19 says.

**Per rule, inside the region**, D19's table applies unchanged — in canon × in lock × on disk × disk
vs lock × disk vs canon — because each rule keeps its provenance comment, so drift and `upstream` stay
per rule rather than per region. The rows below are about the **region itself**.

| `AGENTS.md` | Region | Markers | Outcome |
|---|---|---|---|
| absent | — | — | create the file, region only |
| present | absent | — | **append** the region; the adopter's text is never read, moved or rewritten |
| present | present | intact | replace the region's body; everything outside it is preserved byte for byte |
| present | present | **one marker missing** | 🔴 **refuse** — say which marker and where the other is |
| present | present | **out of order** (close before open) | 🔴 **refuse** |
| present | **two or more regions** | — | 🔴 **refuse**, naming every line a marker sits on |
| present | present | intact, body hand-edited | per-rule drift, exactly as a drifted file today |

🔴 **The three refusals are the whole safety argument, and they are one rule: never guess at a
boundary.** `file-tool-discipline` states it for editing — *"never delete a region by computed
offsets; from this heading to the next is a guess about structure, and when the guess is wrong it
takes the rest of the file with it"* — and it is more true here than anywhere, because the file on the
other side of that guess is the adopter's own doctrine. A damaged marker is a person's half-finished
edit or a bad merge, and the only safe answer is to stop and name it.

**`CLAUDE.md` is the same mechanism, smaller.** Absent: create it holding the import. Present without
the import: append a one-line region carrying it. Present with the import: nothing. It is never
rewritten, and an adopter whose `CLAUDE.md` holds real content keeps every word of it.

## 5. Migration

An adopter on the old shape has `.claude/rules/` in its lock. `sync` moves it: the canonical rules
leave the directory and enter the region, the lock entries move with them, and **a rule that had
drifted is refused rather than moved** — the same refusal a drifted file gets today, for the same
reason, because a move is a write and drift means the repository changed it. Local rules in that
directory are not Daoris's and are left exactly where they are, with the retirement advice D19's last
row already gives.

## 6. What this costs, stated plainly

- **Every adopter's diff is large once.** The tier is the biggest thing Daoris writes, and it all
  moves. There are no live consumers, which is the moment to do it.
- **Two harnesses gain a tier they never had; one gains nothing.** Claude Code already loaded these
  rules. This is not an improvement for the repository that prompted it — it is the removal of a
  silent gap in the other two.
- **`upstream` gets harder to implement and no harder to use.** Extracting one rule from a region is
  more work than reading a file; the verb and its refusals are unchanged.
- **The region is a merge conflict surface.** It is generated, so a conflict inside it is resolved by
  re-running `sync` — but a person who does not know that will hand-merge it, and the marker refusals
  are what makes that visible rather than silent.
