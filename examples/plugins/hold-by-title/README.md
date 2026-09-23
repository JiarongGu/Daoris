# `hold-by-title` — the smallest plugin that speaks

A plugin is a folder with a `plugin.json` (`docs/2026-09-23-plugin-design.md`, D64). This one does
the second of the two things a plugin can do: it **speaks** — a process of its own that the driver
starts with its loop and asks at two points. It declares no harness; a plugin that only declares is a
manifest and nothing else, and never runs.

```sh
daoris plugin add examples/plugins/hold-by-title     # copies it under the home's plugins/
daoris plugin list                                   # what it declares and speaks on
daoris plugin disable hold-by-title                  # a row in plugins.json, never a rename
```

Or from the Machine view's Plugins card, which edits the same folder and the same file.

## What it does

- **`quest/consider`** — before the driver spends anything on a planned start, every enabled plugin
  that listens here is asked in catalogue order. This one answers `hold` for a quest whose title
  carries `[hold]`, with a sentence naming what to do about it; that sentence becomes the quest's
  own reason for sitting on the Overview. A plugin that answers late, wrongly or not at all holds
  too — the waterfall fails closed, because the driver spends real accounts.
- **`session/ended`** — after a session concludes, every listener is told. Nothing said here changes
  the record; this plugin appends a line per ending to `ended.log` in **its data folder**.

## The two folders

The install folder (`plugins/hold-by-title/`) is replaced wholesale by the next `add`, so nothing
of the plugin's own is written there. What it keeps lives beside it in `plugins/.data/hold-by-title/`,
which an update never touches and a remove names rather than deletes. Both are told to the process
in its environment, along with its id and the Daoris home — a plugin cannot work out where it is.

## The wire

JSON-RPC 2.0, one frame per line, on the process's own stdin and stdout; stderr is the plugin's
console, shown under `plugin:hold-by-title`. `initialize` states the wire version and the points the
manifest declared, and the answer names the points the process actually listens on — a subset, never
more. `shutdown` is the notice before the driver ends the process. `hooks.mjs` is the whole of it.

`npm run rehearse:family` installs this folder with the real `daoris plugin add` and drives it: one
quest held with its sentence, one ending in its log, and the switch from a terminal.
