# Daoris.Service — the cross-repository knowledge service

**Status: not started.** This document is the brief.

## What it is for

A session in any repository can read that repository's doctrine, because `sync` put it on disk. It
cannot read what the *other* repositories learned. Every decision record, fix log and task outcome in
the family is invisible from anywhere but the repository that holds it — which is how the same problem
gets solved twice by the same person in two directories.

The service is the query layer over all of it: doctrine, decisions, and past task outcomes, across every
adopting repository.

## Why it comes after the canon, not before

Indexing content that is still divergent indexes the divergence. Six copies of a rule that disagree
produce six answers with no way to tell which is current — so the canon has to exist first, which it now
does. This is also why a generated wiki is a complement rather than a competitor (D16): a wiki is
derived from code and fails by going stale; doctrine is authored because something went wrong and fails
by diverging. The service indexes the second kind.

## Shape

- **ASP.NET Core**, so it can compose the family's existing LLM work rather than rebuild it — semantic
  memory, the embedder seam, the vector store and MCP hosting all already ship in the cognition sibling.
  That dependency becomes correct here precisely because this is a separate deployable; the CLI keeps
  its zero dependencies and never learns about this.
- **The canon is an input, not a copy.** `canon/` at the workspace root is the same tree the CLI
  materializes; the service reads it rather than holding its own.
- **Two clients, one UI** — see `Daoris.Web` and `Daoris.Desktop`.

## Open questions, to settle before writing code

1. **Where does per-repository content come from?** Pushed by a devkit gate, pulled from git remotes, or
   indexed from local checkouts. Each has a different privacy story, and several siblings are private.
2. **What must never leave a machine?** Private repository names, machine paths and local notes are
   deliberately untracked today. A service that indexes them centralises exactly what `sensitive-info`
   keeps out of tracked files, so the boundary needs deciding before the first line.
3. **Does it need to be hosted at all?** A local-only service that a session queries over MCP would
   answer most of the need without a deployment, an account, or a privacy boundary. Worth pricing before
   assuming a hosted one.
