# Event relay

Passes Daoris's work events (ids and codes, never words or names) for this whole Daoris to programs on this machine that ask for
them, such as `daoris events relay`. The design is `docs/2026-10-24-event-hook-design.md` §5.

## Install it

*Install from a folder* on the Plugins page, choosing this folder, then switch it on. Nothing starts at the press: the loop that
holds the home starts it at its next look, and only that loop does.

## What it does

- The driver tells it each `work/event` frame on the hook wire, and its handshake says the run, the last `seq`, the grammar and the
  build. A trial's run, `sample`, connects to nobody.
- A subscriber writes `subscribers/<its pid>.json`, `{ "pipe", "pid" }`, into this plugin's data folder once its pipe is served.
  The plugin watches that folder, reads it after setting the watch, reads it again every five minutes and whenever a connection
  ends, and connects to each file it holds no connection to. A file over 1 KB, or whose `pipe` is not `daoris-event-` and 32
  lowercase hex characters (a socket of that name inside the folder, off Windows), is said once and left.
- To each subscriber it writes one first line, `{ "plugin": "event-relay", "run", "seq", "grammar", "build" }`, then every frame
  unchanged, in order. It filters nothing. At most 8 subscribers are connected; at most 64 frames wait for one, the oldest
  dropped first; a write not done within 5 seconds closes that subscriber's connection. Every frame is answered at once.
- Its stderr names each subscriber by its process id, never its pipe.

## Its tests

`node --test` in this folder. `EVENT_RELAY_RECONCILE_MS` and `EVENT_RELAY_WRITE_MS` are the tests' short interval and write bound;
the driver sets neither.
