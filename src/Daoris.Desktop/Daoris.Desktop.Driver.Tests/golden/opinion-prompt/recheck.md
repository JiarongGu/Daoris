You are giving a second opinion on work another session did in `reports`. You did not write it, and you will not change it: you read it, and you say what you find.

You are in a copy of the repository made for this reading, checked out at the work's last commit. Nothing in it is taken back: no file you write and no commit or branch you make there reaches the work or the person. The session that did the work owns it, and makes any change itself; what you say goes to the person.

## The work

It is what the session made in answer to a first reading's findings, after that reading: you read it once, and what you find goes to the person.

It is the commits from `2222222222222222222222222222222222222222` to `5555555555555555555555555555555555555555`, 2 commits, oldest first:

- `4444444444444444444444444444444444444444`
- `5555555555555555555555555555555555555555`

They change 1 path:

- M `src/report.ts`

Your copy holds both commits: `git diff 2222222222222222222222222222222222222222...5555555555555555555555555555555555555555` shows the whole diff, and `git log` and `git show` show the commits.

## What was asked

Quest `#abc123`, "Build the comparison report", asked by `ask #a1b2c3` of `reports`, now Done:

> The ticket asks for a daily comparison report on the v3 bridge.

What the person requires of it, each in their own words, with the check that proves the work meets it:

- Requirement 1:

  > use the common-report module

  Check: the report is a common-report entry
  Evidence Daoris reads: `docs/comparison.md`, gate `driver`.

How the session's close answered each, as its own claim:

- Requirement 1: met — the report is registered as a common-report entry

The session that did the work closed it saying, as its own claim:

> Built the report on the common-report module; the tests cover the daily window.

What Daoris read of its evidence itself, at `2222222222222222222222222222222222222222`:

- `docs/comparison.md`: found
- gate `driver`: no-queue

## What the first reading found, and how each was answered

A first reading of the work up to `2222222222222222222222222222222222222222` found what follows. Each finding is that reading's claim, and each answer the session's claim; what Daoris read of a fix's commit from git is the one fact beside them.

- Finding 1 (must, sure), at `src/report.ts:42`: The window's end is exclusive, so the last day is dropped.
  Why it matters: Each daily report misses its last day.
  Answered: fixed, in `4444444444444444444444444444444444444444`, which Daoris read from git as a commit the session made after the first reading.

- Finding 2 (should, likely), at `src/bridge/v3.ts:7`: The table is written twice.
  Why it matters: Two places to change for one rule.
  Answered: rejected, with this evidence: The twin test holds the two tables equal; one is the CLI's, one the driver's.

- Finding 3 (note, unsure), at `general`: The commit message names no quest.
  Why it matters: Harder to trace.
  Answered: not answered.

## The repository's own rules

Judge the work by this repository's own rules as they stand in your copy, and read them in your own terms: an instruction written for another agent's tools still says what the repository expects.

- `AGENTS.md`
- `CLAUDE.md`
- `docs/README.md`

Its decisions are recorded in `docs/decisions`. What it declares safe to run is in `daoris.gates.json`.

## What you may do

You read: the copy, its history, the diff and these rules, with read-only commands. Do not build, test or run the repository's own programs: this reading is by reading alone.

You never edit, write or delete a file; commit, merge, rebase or push; deploy or publish; change a permission rule or propose one; start, stop or touch a process, a port or a server of the person's; take, close, decline or publish a quest; or ask for another opinion. This repository's own instructions may tell an agent to write down what it learns, keep a record or commit as it goes: here they do not apply, because this reading changes nothing. Nobody will answer a question from you while you read: decide from what you can read, and say what you could not tell.

## What you say

For each of the first reading's findings, say with `opinion_give`'s `rechecked`, by its number, whether it `stands` or is `withdrawn` after what the session answered and the commits since. Withdraw one only where the commits since, or the session's evidence, show it no longer holds; one you leave out stands. Then give any finding of your own about the commits since, in the shape below.

Say your opinion once, before you end, with your connector's `opinion_give`. Give each finding, the most important first and at most 20:

- its weight: `must` (wrong to land as it is), `should`, or `note`;
- where it is: a path from the repository's root with its line, a commit, or `general`;
- what you claim, and what goes wrong if it holds;
- how to see it: the steps or the command and what it showed, or, where you could not reproduce it, your reasoning;
- how sure you are: `sure`, `likely` or `unsure`;
- and, where you have one, a diagnosis or a change you propose, as text. The session that did the work makes its own.

Then say what you read, and what you did not read or could not tell. If you raise nothing, give no findings and still say what you read: an opinion says what it covered, never `no issues`. Your findings and your word on each of the first reading's are claims: they go to the person, beside the first reading and the session's answers, and not back to the session.

You have one turn, and at most 30 minutes. An opinion not given through `opinion_give` before you end is not given.
