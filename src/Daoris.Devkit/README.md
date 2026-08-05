# Daoris.Devkit — the shared developer toolkit, shipped as a binary

**Status: built.** One self-contained 2.7 MB binary, 39 tests, and it runs this repository's own gates.
The two questions this document was written to settle are settled — as `docs/DECISIONS.md` D26 and D27.

## The problem, measured

Eleven repositories in this family carry a hand-copied `devtools/dev.mjs`. Surveyed 2026-08-05:

| Repositories carrying it | Smallest | Largest | Spread |
|---:|---:|---:|---:|
| 11 | 2.6 KB | 52.6 KB | **20×** |

Nine also carry a `project.config.mjs`, which is the part that was *meant* to differ. The other twenty
thousand lines are the same tool, re-derived and diverged — the exact pathology Daoris exists to fix,
one layer below the documents it already fixes.

## Why a binary rather than a copied script

This reverses the original design note, which argued the CLI should stay Node because "what devtools
actually do is orchestrate subprocesses, and a compiled binary that spawns a build buys nothing." That
reasoning weighed the *execution* cost and missed the *distribution* one:

- A copied script is copied, and copies diverge. That is the whole thesis.
- A `.NET` repository that carries a Node devtools script has a Node dependency it does not otherwise
  need — for tooling alone. A binary removes it.
- A binary has a version. A pasted script has whatever the paste contained.

The CLI stays Node and zero-dependency; this is a separate artefact with a separate job.

## The four universal gates

Extracted from the eleven copies by reading the extremes and keeping only what they share. Everything
else in those copies — build, test, pack, sample apps, screen capture, input injection — is
stack-specific and is *declared* by the repository rather than built in.

| Gate | What it answers | Skips when |
|---|---|---|
| `sensitive` | Would this commit leak a machine path, a private name, or a credential? | never — it is the one that has to run |
| `version` | Does one file own the version, does everything agree, and was it stamped rather than typed? | no `version.source` declared |
| `docs` | Has a document fallen behind the code it claims to describe? | no `docs.tracked` declared |
| `doctrine` | Has this repository's doctrine drifted? — **delegated to `daoris check`** | no `daoris.json` |

`doctrine` is four lines of real work on purpose. `daoris check` already answers that question against a
lock whose semantics took four corrections to get right (D19); a second implementation here would be a
second answer to a question that already has one, committed by the tool that exists to remove exactly
that. The two artefacts meet at a process boundary and an integer.

### What the sensitive scan carries over

It is canonized from the one copy that had been through a real incident — a leak that reached history
and needed a rewrite to remove. Four properties are deliberate, and each exists because something got
through without it:

- **Paths are scanned as well as content.** A file whose *name* contains a banned token used to pass.
- **It fails closed when the private pattern list is missing.** The structural patterns are publishable
  by construction; the tokens that are actually secret load from a gitignored file. When that file was
  absent, earlier versions printed a notice and carried on — so on a fresh clone the half of the guard
  that knew the private names silently did not run. Opting out is now explicit.
- **Renames count.** `--diff-filter=ACM` misses the `R` that `git mv` produces.
- **Commit messages are scanned.** They are history, and they are the half nobody reviews.

Findings are redacted in the report. A gate that prints the secret it caught has written that secret to
a build log, which is frequently more public than the commit it just blocked.

## Declaring gates

`daoris.gates.json`, at the repository root. Separate from `daoris.json` on purpose (D26): the manifest
is inert data the CLI parses on every invocation, and this file names commands that execute.

```json
{
  "devkit": "0.0.1",
  "gates": [
    { "name": "build", "run": "dotnet build MySolution.slnx" },
    { "name": "test",  "run": "npm test", "cwd": "src/web" }
  ],
  "sensitive": { "patternsFile": "local/sensitive-patterns.txt" },
  "version":   { "source": "Directory.Build.props", "mirrors": ["src/web/package.json"] },
  "doctrine":  { "command": "daoris" },
  "docs": {
    "changelog": "CHANGELOG.md",
    "graceDays": 0,
    "tracked": [{ "document": "README.md", "describes": ["src/Core"] }]
  },
  "disabled": []
}
```

`.props`, `.csproj` and `package.json` version shapes are known, so the common case declares no
`pattern`. A gate named in `disabled` does not run **and is printed on every run** — off because
someone wrote it down is a decision; off because nobody wired it up is an accident, and from the
outside those look identical unless the run says so.

## Running it

```
daoris-devkit verify          # universal gates, then the declared ones; stops at the first failure
daoris-devkit scan            # the sensitive scan on staged changes — what the pre-commit hook runs
daoris-devkit scan --tree     # …on every tracked file
daoris-devkit scan --history  # …on everything the repository has EVER contained
daoris-devkit init            # write a starter daoris.gates.json
daoris-devkit install-hooks   # write .githooks/ and point core.hooksPath at it
```

### `--history` is an audit, not a gate

Run it at moments, not on every commit: **before making a repository public**, and after a history
rewrite to prove the rewrite worked. It reads every reachable object, so its cost grows with the
history — wiring it into `verify` would tax every run forever to re-check commits that were already
checked when they were made. It is deliberately not one of the four gates.

It exists because the other scopes cannot answer the question that matters at those moments. Deleting a
leak edits the current checkout and leaves the copy in history untouched, and after a push there are
copies you no longer control. It covers three things the working tree cannot: **every reachable blob**,
**every commit message**, and **every path any file ever had** — a name can be the leak on its own, and
deleting a file does not delete the name it had.

One `git cat-file --batch-all-objects` process streams the whole object database rather than one process
per object; this repository audits in about two seconds.

### Acknowledging a reviewed object

A history audit finds things that are genuinely fine — most often a test fixture that deliberately
contains the shape the scanner hunts for. Those are recorded by sha:

```json
"sensitive": { "reviewedObjects": ["04801cb"] }
```

**Consulted for `--history` only, never for staged changes or the working tree.** That asymmetry is the
whole safety argument, and it is why this is not an ignore-list. A path-based ignore silences a *file*,
so the next secret written into that file is silent too. An acknowledgement names one **immutable object
by content hash**, so it cannot cover anything that does not already exist — a new leak is a new object
with a new sha, and it is reported.

Acknowledge only after reading the object and understanding why it is benign. A real secret is a
**history** problem, and the answer to that is a rewrite plus a credential rotation, never a line in a
config file.

Exit codes match the CLI's: `0` clean · `1` a gate failed · `2` the devkit could not run. The
distinction matters to a script — "the gates found something" is not "the gates could not run".

Universal gates run **before** declared ones, and the run stops at the first failure. Running a
twelve-minute build before discovering a staged secret wastes the twelve minutes and, worse, teaches
people to start it and walk away.

A **skipped** gate is reported as skipped rather than as a pass. "7 passed" when three had nothing to
check reads as coverage and is not.

## Distribution

Release assets, with the sha256 recorded by the consuming repository and verified **offline** (D27).
The devkit is never downloaded implicitly: a missing binary is an error naming the command to run.

That is not an inconvenience worked around — it falls out of D8. Nothing in the CLI may touch the
network, and there is now a test that greps for the primitives, so the CLI could not be the thing that
fetches a binary even if that seemed convenient. Hash-pinning is the same shape as `daoris.lock`:
record the digest locally, verify against the record, need nothing else.

## Building

```
dotnet test src/Daoris.Devkit
dotnet publish src/Daoris.Devkit/Daoris.Devkit.Cli -c Release -r win-x64
```

Native AOT needs a C++ toolchain. On Windows the ILCompiler shells out to `vswhere.exe`, which the
Visual Studio installer does **not** put on `PATH` — publish fails with an unhelpful `MSB3073` naming a
linker that is in fact installed. Add `C:\Program Files (x86)\Microsoft Visual Studio\Installer` to
`PATH` and it builds.
