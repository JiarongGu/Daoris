<!-- daoris: core/core/skills/set-up-documents/templates/brief.md @ 0.0.1 — canonical; edit via `daoris upstream` -->
# <Repository> — the brief

## What this is

<Three to five sentences: what it is for, who uses it, and the one constraint every change serves.>

## Before changing anything

- <A non-negotiable, in one line, with a link to where its reason lives.>
- <Three to seven of them. A rule with no home is a rule nobody can judge an edge case by.>

## Layout

<Only what reading the tree would not tell: which folders are generated, which are copies of another,
which have instructions of their own, and what must never be edited by hand. Not a tour of the folders.>

## Areas

<One row per area document. A change starts at its area's document.>

| Area | Document |
|---|---|
| <the part of the code> | `<path>` |

## Build, test, verify

- Quick checks, after every change: `<the commands>`.
- Done means: `<the one command>`.
- <The rest are in the gates declaration, at `<path>`. What never to run, and why: a check that needs
  the machine quiet, a command that publishes.>

## Conventions

<Only those that differ from the language's and the tools' defaults.>

## Where things are

<One row per record this repository keeps. Where a tool generates this list from a declaration, declare
the records there and let it write the list.>

| Role | Where |
|---|---|
| decisions | `<path>` |
| backlog | `<path>` |
| archive | `<path>` |
| fixes | `<path>` |
| changelog | `<path>` |
| gates | `<path>` |
