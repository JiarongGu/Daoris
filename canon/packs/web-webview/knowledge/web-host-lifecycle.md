---
name: web-host-lifecycle
applies_when: standing up an embedded web host — custom schemes, environment creation, navigation gating, or debugging a dev-mode inspector
enforces: register a scheme in every place it must appear; gate readiness on content, not navigation; scope the environment to its owner; expect the dev inspector's arguments to be silently dropped
---

# Standing up an embedded web host

The invariants are in the `embedded-web-ui` rule. This is the surrounding detail — the parts that cost a
debugging session each rather than shipping a defect.

## A custom scheme is registered in several places, and one omission fails identically

Serving from a private scheme takes agreement between the environment's options, the registration list,
and the handler filter. Miss any single one and the failure is the same generic network error, which
makes bisecting by symptom useless. Treat them as one unit: add all of them together, and when adding a
second scheme, check every list the first appears in.

A mistyped prefix belongs to the same family. Nothing validates it, so it simply never matches, and the
page reports only that the request failed. **Fail loudly from the layer that knows the requirement** —
the component that owns the prefix is the one that can say the prefix is wrong, and no layer below it
can tell a typo from a request it was never meant to serve.

## Readiness gates on content, not on navigation

A gate that closes when navigation *starts* opens on a page that has not parsed. Close it when content
begins loading, and also on process failure — otherwise a crashed renderer leaves the gate waiting
forever and the timeout is the only thing that ends it.

Navigation policy has a matching trap: a decision that requires awaiting something cannot be made in a
synchronous navigation callback. The event has already returned by the time the answer arrives, and the
navigation proceeds. Take a deferral, or decide earlier.

## Scope the environment to its owner

An environment is expensive, so it gets cached — and caching it *process-globally* outlives the thing it
belongs to. Scope it per profile and per owner. Prewarming stays behind whatever gate enforces a single
instance, because environment creation takes the user-data directory and a second one racing for it
fails in a way that looks like corruption.

Re-check cancellation after any multi-second acquire and before publishing the result: browser startup
is measured in seconds, and the caller that asked may be long gone.

## Synchronization objects outlive their waiters

Cancelling waiters and immediately disposing the primitive they were waiting on disposes it out from
under them. The cancellation has to be observed first. This is the same shape as disposing a token source
whose callbacks have not run yet, and it produces an exception from a stack that has nothing to do with
the code that caused it.

A subscribe method on a pooled object needs a disposed check exactly as much as an operation does —
handing out a subscription to a returned object is how a listener ends up attached to someone else's
session.

## The dev inspector's arguments get dropped

Setting the host's additional-browser-arguments option in code makes the runtime **ignore the environment
variable** carrying the same thing. A development host that sets any argument of its own must re-append
the variable's value itself, or the remote-debugging port is silently never opened and the inspector
simply finds nothing to attach to. Two applications in this family hit this; keep the fix inside whatever
builds the argument string, so it cannot be forgotten at one call site.

## Injected script values are serialized, never interpolated

A value pasted into a script string is code. Serialize it — the same reasoning as any other injection, and
the reason it is easy to get wrong here is that the "template" looks like configuration rather than a
program.
