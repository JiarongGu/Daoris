# Update when idle — an install updates itself when its work allows

> UPDATE1, decided as D139 (2026-10-03). Paths are neutral by `sensitive-info`: `<install>` is the folder `Daoris.exe` sits
> in. Built as described here; the deployment rehearsal's phase 9 is the gate (D60), run by the parent at the merge.

## 1. What forced it

On 2026-10-03 four republishes of the owner's install each waited on running sessions and were done by hand: hold the
repository, wait in a script until no session worked, close the window, publish over the install, start it, release the
hold. One attempt failed on a safety check that found main's checkout dirty with documents. The application cannot replace
itself while it runs, and nothing else knew when its work allowed it.

## 2. The install's layout

```
<install>/
  Daoris.exe            the launcher: the one program that can replace the application (D93)
  app/                  the application
  data/                 the home (D63); the request lives here: data/update.json
  INSTALLED.md          the marker
  update/               owned by --stage and the swap; present once something was staged
    staged/             the staged build: Daoris.exe, INSTALLED.md, app/, build.json
    previous/           what the last swap replaced, until the new build confirms
    failed/             a build that was refused or would not come up, kept to look at, never retried
    swap.json           the launcher's journal and outcome
```

`build.json` (schema 1) names the build (`id`, `version`, `commit`, `at`) and every file with its size and SHA-256. Its
reader, the check and the swap are one file, `Daoris.Desktop.Driver/StagedBuild.cs`, which the driver library carries and
the launcher compiles in, so the launcher still references nothing and runs the same check the application does. The
publish's `STAGE`, `STAGED`, `BUILD_MANIFEST`, `SWAP_JOURNAL` and `STAGED_REQUIRED` are its twins, held by
`desktop-publish.test.ts`.

## 3. The flow

1. **Stage.** `npm run publish:desktop -- --to <install> --service --stage` builds what a publish in place builds into
   `update/.staging/`, writes `build.json`, and renames the folder to `update/staged/`. It refuses a folder that is no
   install, a swap under way (`swapping`, `started`, `confirmed`), and an install carrying its host staged without
   `--service`. It never refuses a running install. A publish in place removes `update/staged/`.
2. **Drain.** The application's `InstallUpdater` looks every two seconds and at every word. A staged build is installed
   when idle unless `update.json` says otherwise for that build. While it drains, the driver watch plans each look with
   `InstallUpdate.Drained`: every drivable repository held and no intake, so no quest, resume, carry-on or answered park
   starts. The look still syncs, reports endings and tells plugins. A hold the drain made reads `Blocked` with the update's
   sentence and the tick's `forUpdate`, which the page says in the reader's language; a hold the person made keeps theirs.
3. **Idle.** No driven session in the watch's running set, and no conversation with a turn in flight. A conversation
   between turns, a parked session and the person's terminal are idle: the close ends a conversation as any close does, and
   a parked record is touched neither by the close nor by the next start's sweep, which takes only `working`.
4. **Apply.** The application checks the build (§4), starts `Daoris.exe --update --after <pid>`, clears the request, and
   closes its main window: the person's close, so each driven session is recorded `stopped` and interrupted (D104).
5. **Swap.** The launcher waits for that process and for everything running from `app/`, then checks the build again
   and moves, writing each move to `swap.json` before the next: `app/`, `Daoris.exe` (its own running image, which
   Windows lets be renamed) and `INSTALLED.md` into `update/previous/`, then the staged three into place. It starts the new
   application and waits up to a minute for it to write `confirmed`, which it does once composed. Confirmed, `previous/` is
   removed and the journal says `installed`. Still running and silent at the minute, it is installed, `confirmed: false`.
6. **Start.** A start with a build staged and nothing running from `app/` swaps the same way first, unless the person said
   *Not now* of that build. A start beside a running application never swaps.
7. **Say it.** The application turns the journal into the window's banner and the machine log, once: `update.installed`
   at the start that confirmed, `update.rolled-back` or `update.refused` at the next start after the launcher's outcome.

## 4. The check

Every file `build.json` names is there with its size and SHA-256; nothing is there it does not name; no path leaves the
build; the launcher, the marker, `app/Daoris.Desktop.exe` and `app/Daoris.Desktop.App.dll` are among them, and the host when
the install carries one. The application checks before it closes; the launcher checks again before it moves a file. A
refusal by the application leaves everything as it was and ends the drain; a refusal by the launcher moves the build to
`update/failed/` and starts the build that is there.

## 5. Rolling back

- **A move fails** (a file in `app/` held): the moves made are undone in reverse; the journal says `rolled-back`, `busy`.
- **The new application ends before it confirms**, or will not start: its three are moved to `update/failed/` and
  `update/previous/` moved back; the old application starts; `exited` or `start`.
- **The launcher dies mid-swap**: the next start reads `swapping` and undoes the journal (`interrupted`), or reads
  `started` with nothing running from `app/` and undoes it (`exited`), or reads `confirmed` and finishes it.
- **Something still runs from `app/` after the wait**, or the launcher meets an error before it can swap: the build is
  refused, `busy`, and moved aside, so the old build is not closed for it again. An application whose close did not take
  stops draining once the build is gone.

No path retries a build: whatever did not install is in `update/failed/`, and the next stage replaces it.

## 6. The two doors (D50)

| | The screen | The terminal |
|---|---|---|
| What stands | the banner under the app strip (`DAORIS.UPDATE` · `STATE`, `UPDATE_STATE`), and Settings → Driver's row | `daoris-driver update [--install <folder>]` |
| When idle | *Update when idle* | `--when-idle` |
| Now | *Update now* | `--now` |
| Not now | *Not now* | `--cancel` |
| Put away an outcome | *Dismiss* | (the journal's `told`) |

Both write `$DAORIS_HOME/update.json` (`mode`, `build`, `at`). A word names the build staged when it was said, so *Not now*
holds for that build only. Absent or unreadable is when idle. The banner speaks both languages (D116), its buttons
glossary-checked. Settings → Driver's row (UPDATE1b, `settings/Update.tsx`) stands where the banner is gone once
dismissed: the staged build's id, version, commit and time, the drain and what it waits on, the last swap in the banner's
words, and the same three words. It says the last swap from `STATE`'s `last`, the journal's record told or not (UPDATE1d),
so it stands after a *Dismiss* and at every later start; `last` is null with no journal and while a swap is under way, and
a swap this start confirmed is installed, as the banner says it.

## 7. What it does not do

The first install of this build is a publish in place: a launcher from before it does not swap. The swap is run by the
launcher being replaced, so its protocol is the old build's; a launcher refuses a `build.json` schema it does not read. A new
application that confirms and fails later is not rolled back. Nothing reaches a network, and nothing goes under the user
profile (D63).
