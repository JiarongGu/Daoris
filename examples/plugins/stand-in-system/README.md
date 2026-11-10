# Stand-in system (`stand-in-system`)

An adapter plugin for a place reached only through its console: a small key-value store with an etag on each key, kept in the
plugin's data folder as `state.json`. It is the proof's system for a `system` department: a session never changes it directly;
its change set is planned against it, reviewed, and applied under each key's etag, and a plan read before someone else changed a
key is refused as stale and planned again. The same store is declared a second time as `stand-in-unguarded`, guarded by `none`:
its apply checks nothing, and Daoris reads each key just before writing it and reads it back after.

## The folder

| File | What it is |
|---|---|
| `plugin.json` | the manifest: the two system points it speaks and the two systems it declares, `stand-in` guarded by `etag` and `stand-in-unguarded` by `none` |
| `hooks.mjs` | the hook wire: the handshake, `plan/read`, `apply/perform` and `shutdown` |
| `system.mjs` | the store: read, plan, apply, each key's etag |
| `system.test.mjs` | `node --test`: the plan's types and preconditions, an apply and a stale apply, and both points over the wire |

## Where it speaks

- **`plan/read`**: `{ system, changes: [{ resource, value } | { resource, remove: true }] }` is answered with
  `{ operations: [{ resource, op, before, after, precondition: { kind, version } }] }`, the kind the system's declared one:
  `create` where the key is absent (its version null, so it must still be absent), `update` and `delete` under the etag read now.
  A change that changes nothing is left out.
- **`apply/perform`**: `{ system, operations }` answers `{ state: "unchanged", applied: [] }` where the store already holds every
  operation. Otherwise `stand-in` checks every precondition first: all holding, each is written and the answer is
  `{ state: "applied", applied: [resource…] }`; one not holding, nothing is written and the answer is
  `{ state: "stale", applied: [], refused, present: [{ resource, exists, value, version }] }`. `stand-in-unguarded` writes each
  without a check.

## What it needs

Node 18 or later. It reaches nothing outside its data folder.
