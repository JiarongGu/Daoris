---
name: embedded-web-ui
applies_when: serving resources to, or calling into, a web view embedded in a native application
enforces: answer requests off the UI thread with a response object, never bytes; the browser object is UI-affine; publish files atomically; fail closed on init and health checks
---

# Embedded web UI — the four invariants that fail silently

**A resource request is answered *off* the UI thread, with a response the host streams. The browser
object is thread-affine. Anything served from disk is published atomically. Initialization and health
checks fail closed.**

## Why

Two applications in this family arrived at these independently, from different symptoms: one profiling a
window that froze under load, one debugging thumbnails that were merely slow. Each of the four fails
*quietly* — a frozen frame, a stale asset, a hang with no exception — so none of them surfaces as an
error anyone can search for. That is what makes them worth stating rather than discovering twice.

## How to apply

- **Take the deferral and answer asynchronously.** Serving a request inline blocks the thread that draws
  the window, so the symptom is a frozen UI rather than a slow response, and it scales with request
  count. Hand back a response *object* the host reads from — never materialize the whole payload to
  return it.
- **Do no expensive work in the request path.** Decoding, resizing, or transcoding on the way out is
  slower than the I/O it accompanies and is easily mistaken for a slow disk. Serve the bytes that are
  already on disk; do the conversion once, ahead of time.
- **The browser object is UI-affine — marshal through one owner.** Every property and method belongs to
  the thread that created it. Hand-rolled marshalling in each call site is where the races live; give the
  control a single owner and route through it.
- **Publish atomically: write beside, then rename.** A consumer that reads a file mid-write gets a
  truncated image or a half-written document, and the failure looks like corruption rather than a race.
  Coalesce concurrent fetches of the same target so N requests do one download.
- **Cache deliberately: never the entry document, forever the content-hashed assets.** The document is
  what points at the current build, so a cached one pins the whole application to an old version. Assets
  whose names contain their hash can be immutable, because a change produces a different name.
- **Contain every path that reaches the filesystem, in every method that does.** A request path is
  attacker-controlled input; one un-checked join serves anything the process can read. Check in each
  method rather than at the entrance, because the entrance is what gets refactored.
- **Fail closed.** An initialization that can hang needs a timeout, and a health probe that swallows its
  own timeout reports healthy forever — the exact opposite of its purpose. Never cache a *faulted*
  initialization task: the memoizing assignment stores a failure as happily as a success, and every later
  caller gets the original error with no way to retry.
- **Keep exception text out of responses.** It reaches a page that may render it, and it describes the
  host's internals to whatever is on the other side.
