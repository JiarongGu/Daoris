# Daoris.Desktop — the desktop client

**Status: not started.** This document is the brief.

## What it is

A desktop shell hosting `Daoris.Web`, built on the family's desktop runtime sibling — the project that
exists to be exactly this: a WinForms host, a WebView2 surface, and an IPC bridge, so a web application
becomes a desktop application without a second UI codebase.

## Why a desktop client at all

The web app needs a running service. A developer wants the family's accumulated knowledge available in
the same session where they are working, which is local, often offline, and frequently inside a private
repository whose contents should not leave the machine. A desktop shell can hold a local index and reach
a shared service when there is one — the browser cannot.

That also makes this the first real external consumer of the desktop sibling, which is worth something
on its own: a runtime with no consumer is unvalidated, exactly as a pack nobody installs is.

## Open questions

1. **Local-first or service-first?** If the service turns out to be better local-only (see
   `Daoris.Service`), this shell *is* the product and there is no deployment to run.
2. **Which platforms?** The desktop sibling is Windows-first. The family develops on Windows; that may
   simply be the answer.
