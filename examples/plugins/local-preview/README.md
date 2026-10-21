# Local preview (`local-preview`)

A Daoris plugin that **speaks**: a process of its own, which the Daoris driver starts and asks at the
points below, over JSON-RPC 2.0 on its stdin and stdout. It was made by Daoris's plugin kit; what it
does is what `plugin.mjs` does.

> An example performed step: it writes a preview of the attempt's revision into its own data folder,
> once per attempt, and removes it once the work is over. It touches nothing outside that folder.

This is the example the plugin kit's performer is filled in from (FABRIC-PERFORM1e): `daoris-driver plugins
new local-preview --point work/perform` wrote every file here, and only `act()`, `check()` and `remove()` in
`plugin.mjs`, the manifest's `description` and this paragraph were written by hand. Its "platform" is
`previews/<invocation>/` under its data folder, so the whole cycle runs on any machine and reaches nothing:

- `act()` writes the attempt's revision and inputs, and last a `done` file, and says it reached on
  `local-preview/receipt@1:<invocation>`, having made `local-preview/resource@1` `<invocation>`;
- `check()`, asked about an attempt a process before it began, answers done where `done` is there, and
  otherwise clears what was begun and answers not-started, so the redelivery makes it whole;
- `remove()` removes the preview, and answers already-gone where it is not there.

Nothing runs until a person installs it, turns it on, and a work graph names its step.

A plugin is code that runs on the person's machine, as them. So it is made like any other change: in a
repository, with its tests, reviewed as a diff. It is installed only when a person adds it.

## The folder

| File | What it is |
|---|---|
| `plugin.json` | the manifest: its id, the plugin API version, the command and points it speaks on, and the tools it runs |
| `plugin.mjs` | the wire script: the handshake, a handler for each point, the shutdown |
| `plugin.test.mjs` | its wire test: `node --test` here, with nothing of Daoris's installed |
| `README.md` | this: the wire, the rules, and each point's frame and answer |
| `work.mjs` | the kit's half of working: the journal, the work points' answers, the stand-in world; written by the kit, not by hand |
| `plugin.contract.test.mjs` | its contract test: what the kit promises of a performer, checked over the wire |
| `cycle.json` | the cycle `daoris-driver plugins try --cycle` drives it through |

## Where it speaks

A frame's `root` is the repository's checkout on the machine. In the samples below it is `{root}`,
which the wire test and `try` replace with an empty scratch folder.

### `work/watch`: an observation

Only by the Daoris that holds the machine, and only while some unit awaits one of this plugin's steps: the whole set of units awaiting them, each with its repository, branch, tip and the steps it awaits, every time the set changes. `generation` counts this process's starts; where `resync` is true, the plugin sends again, at `work/step`, everything it knows of each awaited step, each under the id it was first sent with. The kit's `work.mjs` does both.

It is sent:

```json
{
  "generation": 1,
  "resync": true,
  "units": [
    {
      "repository": "engine",
      "workspace": "default",
      "root": "{root}",
      "branch": "feature/0fda18-expose-a-streaming-budget",
      "tip": "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a",
      "line": "main",
      "pullRequest": null,
      "awaiting": [
        "perform"
      ]
    }
  ]
}
```

It answers `{}`. The answer is not read, but it is waited for. Late, wrong or no answer: the driver would say so in the plugin's health and tell it the set again with `resync`. The driver waits 10 seconds for it.

### `work/step`: a event

Never asked: the plugin's own notification, the one frame it sends unasked, `{ "jsonrpc": "2.0", "method": "work/step", "params": … }` with no id. A performed step's outcome names the `invocation` it was asked; an observed step's names the `repository`, `branch` and full `commit` it is about. `id` names the event, and sending it again under the same id is the same event. Only reached outcomes carry `outputs`; any outcome may name `resources` it made.

It sends, as `params`:

```json
{
  "step": "local-preview/perform",
  "invocation": "i-0fda18a2b3c4",
  "state": "reached",
  "evidence": {
    "ref": "local-preview/receipt@1:i-0fda18a2b3c4",
    "message": "deployed"
  },
  "resources": [
    {
      "schema": "local-preview/resource@1",
      "id": "r-i-0fda18a2b3c4"
    }
  ],
  "at": "2026-10-21T09:02:00.0000000+00:00",
  "id": "i-0fda18a2b3c4"
}
```

It is answered nothing: it is never answered. A frame the driver cannot read: the driver would refuse the frame, count it under the plugin's name, and ask it to send again what it knows.

### `work/perform`: an act

Once a performed step is due, its attempt: written on Daoris's record before it is sent, and sent again under the same `idempotencyKey` after a restart or a lost answer, which must never do the work twice. It is answered on receipt, before the work; the outcome comes later at `work/step`, naming the `invocation`. Asked beside it: `work/reconcile` (what became of an attempt) and `work/cleanup` (one thing it made, removed), which the kit's `work.mjs` answers.

It is sent:

```json
{
  "unit": "u-0fda18a2",
  "step": "local-preview/perform",
  "stepVersion": 1,
  "invocation": "i-0fda18a2b3c4",
  "idempotencyKey": "k-3f2a9c1e5b7d4a6f",
  "subjectRevision": "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a",
  "inputs": [],
  "deadlineAt": "2026-10-21T09:10:00.0000000+00:00"
}
```

It answers `{ "invocation": "…" }`, the invocation it was sent, and nothing more. Late, wrong or no answer: the attempt's outcome would be unknown, and the driver asks `work/reconcile` before any other attempt. The driver waits 10 seconds for it.

## Working

This plugin performs a step: Daoris asks it to do a piece of work for a unit (a branch, a quest), once the step's place in
the work's graph is due, and it says what became of it. `plugin.json` declares the step, `perform`, with what the kit
promises of it:

- **`idempotent`**: an attempt Daoris sends again under the same `idempotencyKey`, after a restart or a lost answer, never
  does the work twice. `work.mjs` writes each key to its journal before `act()` is called, and calls it once per key.
- **`reconcile`**: asked `work/reconcile` about an attempt, it answers what became of it: `done` with its outcome,
  `not-started`, `running`, `failed` with its outcome, or `unknown`. `work.mjs` answers from its journal, and asks `check()`
  only about an attempt a process before this one began, which only the platform can tell.
- **`transient`**: the failure codes worth trying again, under the step's `retry`. Any other code stops the step for the
  person. An attempt whose outcome is unknown is never tried again by itself.
- **`resources`**: what `act()` makes, by schema, `local-preview/resource@1` here, `temporary`: Daoris asks `work/cleanup` to remove
  each one once the work's end is over, under the same key at every try, and `work.mjs` calls `remove()` once per resource
  and answers `already-gone` when it is asked again.

So you write three external calls in `plugin.mjs` and nothing else: `act()`, the work; `check()`, what became of an
attempt an earlier process began; `remove()`, one thing `act()` made. Daoris supplies the ordering, the references,
recovering a dispatch, the deadlines, the validation and the cleanup's accounting.

What a plugin may not do, whatever it can: write a store, a rule or a ref of Daoris's; remove anything except through
`work/cleanup`; stand in for a person; make another step's evidence; use a publishing right or a credential it was not
given; write into another repository. It acts with its own authority, on its own platform, and nothing else.

### The stand-in world

`DAORIS_PLUGIN_WORLD`, when it is set, names a JSON file that answers in the platform's place: `act()`, `check()` and
`remove()` are not called, so a test or a trial reaches nothing. The kit's tests and `daoris-driver plugins try` set it,
and nothing is sent to a plugin that does not take it. `work.mjs` says what the file may script.

## Its contract test

`plugin.contract.test.mjs` runs beside the wire test, with nothing of Daoris's installed, in the stand-in world. It shows:

- one external effect under duplicate delivery, before and after a restart;
- each `work/reconcile` answer;
- `already-gone` on a repeated cleanup;
- an event sent again keeping its id, so Daoris takes it once;
- no frame claiming what a plugin may not, and nothing written to the Daoris home it is told.

It checks the protocol, not the code's honesty: what `act()` really does on its platform is for a test of your own, with
a fake of that platform.

`daoris-driver plugins try <this folder> --cycle cycle.json` drives the same plugin through the driver itself: a step
made due, its attempt delivered twice, a failure, a timeout, a restart and a new revision's attempt, then its resource
removed twice. `cycle.json` names the step and what each attempt is given.

## The wire

JSON-RPC 2.0, one frame per line. Daoris calls and the plugin answers; the plugin asks Daoris nothing.

1. **`initialize`.** Daoris sends `{ "protocolVersion": 1, "plugin": "<id>", "home": "<the Daoris home>",
   "data": "<its data folder>", "points": [ … ] }`, the points being what `plugin.json` declares. The
   plugin answers `{ "protocolVersion": 1, "points": [ … ] }`: the points it actually listens on, never
   more than the manifest declares. The kit's test and `try` also want every declared point listened on.
2. **`hook/<point>`**, one per question, answered with that point's shape above. An error answer
   (`"error": { "code": -32603, "message": "…" }`) is a refusal, and Daoris says it in the plugin's name.
3. **`shutdown`**, a notification with no answer: the plugin leaves. The driver ends a plugin still
   there two seconds later.

## The rules

- **stdout is the wire.** Nothing but frames goes there. Anything else, a log line or a debug print,
  goes to stderr, which Daoris shows under `plugin:local-preview`.
- **What it keeps goes in its data folder**: `DAORIS_PLUGIN_DATA`, or `${data}` in a manifest command.
  The install folder is replaced whole by the next `daoris plugin add`, and removing a plugin names its
  data folder rather than deleting it.
- **Where it is, is told.** `DAORIS_PLUGIN_ID`, `DAORIS_PLUGIN_FOLDER`, `DAORIS_PLUGIN_DATA` and
  `DAORIS_HOME` are in its environment, and `${plugin}` in a manifest command is its install folder.
  Never work a path out from the working directory.
- **Every request is answered, promptly.** A decision that is late, wrong or missing holds the quest,
  naming this plugin: the driver fails closed, because it spends real accounts. An observation that
  fails is a line on the console. A landing has two minutes.
- **A platform's tool on Windows is often a `.cmd` script** (npm's shims, `az`). Node cannot start one
  without cmd, and cmd reads the line a second time, so quote every argument, and refuse one holding a
  double quote, a percent sign or a line break rather than pass it. `daoris-driver plugins new <id> --point work/land` writes a `run()` helper that does exactly that.
- **Nothing runs until a person says so.** An installed plugin runs once it is turned on, and a landing
  plugin only once a workspace's landing rule names it.

## The tools it runs

`tools` in `plugin.json` names each program the plugin's process runs, so a person sees them before
turning it on and Daoris can check them: `[]` until it runs one. Each is `{ "id": "…" }`, with:

| Field | What it says |
|---|---|
| `id` | `git`, `node`, `pwsh`, `gh` or `az`, which Daoris knows and finds the way the person set; or any other name, found on the PATH and never downloaded |
| `versions` | the versions it works with: `>=2.60`, `>=2.60 <3`, or one exact version. Absent is any |
| `for` | one sentence: why the plugin runs it |
| `ready` | at most four checks, each `{ "run": ["…"], "says": "…", "fix": "…" }`: a command whose exit 0 means ready, what that means, and the command a person runs when it is not |
| `name`, `command`, `versionArguments` | only for a tool Daoris does not know: what a person calls it, the program to find (the id when absent), and what prints its version |

A check runs only at a trial (`daoris-driver plugins try`, or *Try* on its page), one at a time, with ten
seconds each; Daoris never runs `fix`. A problem in `tools` never stops the plugin: it is said beside
the tool, and a trial says each in its own sentence.

## Testing it

```sh
node --test                                        # in this folder: the wire test, no Daoris needed
daoris-driver plugins try <this folder>            # the same frames, through the driver itself
daoris-driver plugins try <this folder> --point <point> --frame <file.json>
```

`plugin.test.mjs` starts the plugin as the driver does, then checks the handshake, one frame at every
declared point, the shutdown, and that stdout held nothing but frames. The frames' `root` is an empty
scratch folder where git is told there is no repository, so a plugin that pushes first fails on the
machine and reaches nothing. A plugin that runs a platform's tool deserves a test of its own: a fake of
that tool first on the PATH, recording its arguments, and a bare repository behind `origin`, so nothing
reaches a network.

## Installing it

Once it is reviewed and landed: `daoris plugin add <this folder>`, or copy the folder under the Daoris
home's `plugins/`. Then turn it on with `daoris plugin enable local-preview` or in the Plugins place.
