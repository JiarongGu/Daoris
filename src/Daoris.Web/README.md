# Daoris.Web — the knowledge UI

**Status: not started.** This document is the brief.

## What it is

A React application over `Daoris.Service`: search and read doctrine, decisions and task outcomes across
every repository in the family, and see where a rule came from and which repositories carry it.

## One UI, two shells

This app is the **only** UI. It is served over HTTP for the browser, and the same build is hosted inside
`Daoris.Desktop` — which is what the desktop sibling exists to make possible. Two shells, one codebase;
a second hand-written desktop UI would be the same divergence problem in a new place.

## Open questions

1. **What is the primary view?** Search is the obvious answer and often the wrong one — the survey work
   that built this canon was mostly *comparison* (which repositories carry this rule, and how far have
   the copies drifted), which is a different screen entirely.
2. **Does it write, or only read?** Editing doctrine from a web UI competes with `upstream`, which
   deliberately routes improvements through review in the repository that found them.
