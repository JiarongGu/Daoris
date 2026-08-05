---
name: desktop-verification
applies_when: verifying a change in a desktop application, or claiming a UI behaviour works
enforces: drive the running app rather than a mock; synthetic events do not prove an interaction; capture before and after; confirm which instance you attached to
---

# Verifying a desktop application — drive the real one, and prove it

**Verify against the running application, with real input, and capture what happened. A synthetic event
dispatched into the page is not a user, and a green unit test is not a working window.**

## Why

Three applications in this family arrived at the same practice independently, because the alternatives
all produce confident false results. A mock proves the code you wrote agrees with the mock you wrote. A
synthetic event proves a handler runs, which is rarely the thing in doubt.

The failure that keeps recurring is subtler: **you verified something, but not the thing you were
looking at.** A stale bundle, an orphaned process from the last run, a second window with no debugging
target — each gives a real answer about the wrong instance, and nothing in the output says so.

## How to apply

- **Synthetic input does not prove an interaction.** Dispatching a click or an event bypasses hit
  testing, focus, z-order, pointer capture and anything the platform does before your handler — which is
  where interaction bugs actually live. Use the platform's own input for anything a person would do, and
  keep the debugging protocol for reading *state*.
- **Capture before and after, and look at both.** A screenshot pair is the evidence that something
  changed; an assertion that a property is now `true` is the evidence that you set it.
- **Know which instance you are attached to.** A relaunch can leave the previous process alive, and a
  debugger will happily attach to it. Confirm the target before trusting anything it reports, and expect
  secondary windows to have no target at all when they are created with their own environment.
- **A rebuilt front end is not a reloaded one.** Building assets does not restart the host or invalidate
  what it already served — verifying against a stale bundle is the most common way to "confirm" a change
  that never ran.
- **Verify against the real backend at least once.** An offline or stubbed path is worth having and
  proves nothing about the wiring that only exists in the live one.
- **Never kill a shared runtime by process name** to clear a stuck instance — it takes every other
  application embedding it. Kill your own process and let it take its children.
