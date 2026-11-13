# `daoris-dev-report` — a session's report to the person's own development agent

Daoris's own plugin, in the `daoris-` family. A session (Ask Daoris or a driven one) that traced a defect or has a finding to hand over
files a report here; the development agent the person works with reads it from a terminal. It declares two commands. A manifest with
interfaces must list a point, so it listens on the observation `session/ended` and does nothing with it.

It needs no network and knows no repository: a report is words, a time, and the session and place Daoris handed the round. Everything is
kept in the plugin's own private storage (`reports.json`, one record per report, written one at a time under the kit's lock).

## Install

`daoris plugin add examples/plugins/daoris-dev-report`, then switch on its storage and, for sessions, its tools in Plugins.
`daoris plugin try examples/plugins/daoris-dev-report` runs its checks without installing.

## What it needs

- **node** 18 or later on the PATH, which runs its `plugin.mjs`.
- **Its storage switched on** in Plugins, where the reports are kept.
- **Its tools switched on** for the places whose sessions file reports.

## `report`

A tool for agents (`daoris-plugins`'s `daoris-dev-report_report`), also run from the window's round card.

| Argument | |
|---|---|
| `title` | required, at most 200 characters |
| `body` | required Markdown, at most 5,000 bytes |
| `evidence` | optional: paths, log lines, at most 2,000 bytes |
| `severity` | `note` (the default), `defect` or `blocking` |

It answers `Report <id> is filed` in `said`, and `{ id, at, severity }` in `data`. An id is the filing time and a random tail, for
example `20261112T101500Z-3fa9c1`.

## `inbox`

A tool and a terminal verb.

```
daoris daoris-dev-report inbox                 every report, unread first, then newest first (unread ones starred)
daoris daoris-dev-report inbox --unread        only the unread
daoris daoris-dev-report inbox read <id>       one report whole: its words, its evidence, its session and place
daoris daoris-dev-report inbox mark <id>       marks it read
```

An `<id>` is the whole id or its tail (`3fa9c1`), where no other report ends or starts the same.

## How the development agent reads it

From a terminal on the machine where Daoris runs, with its own account (a session's terminal is refused, since a plugin press is the
person's): `daoris daoris-dev-report inbox --unread`, then `inbox read <id>`, then `inbox mark <id>` once it has acted. The plugin's tail
names it where no other installed plugin ends the same: `daoris dev-report inbox`. With Daoris closed the verb runs alone under the
home's lease against the same storage.

## Tests

`node --test examples/plugins/daoris-dev-report/*.test.mjs`: a report filed and read back over the wire as the driver speaks it, two
filed at once both kept, mark read and the list's order, and the refusals. `storage.mjs` and its test are the kit's, unchanged.
