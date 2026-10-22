# The desktop's start: what runs before the first page has its data (STARTFAST1, 2026-10-23)

**Standing:** a reading and a proposal. STARTFAST1's first part built only the timing (`start.stages`, the machine-log design's
row); nothing that starts was changed. The cut proposed in §4 is STARTFAST1b. Lines are read at the commit that added this note.

## 1. The start that asked for it

An install's start on a just-booted machine (2026-10-23) logged, from the process's start: the desktop's `app.started` at 11 s with
nothing between, the host's `app.started` 5 s later, the Sessions view open at 18 s, `splash.lifted by=bound ms=15435`,
`help.room.look` 3.2 s, `look.slow` for `stage=follow` and `stage=tidy` at 5 s each, and `request.failed
route=/api/confirmations/waiting ms=20002`. From the next start, `start.stages` says each stage in ms from the process's start:
`main`, `home`, `driver`, `pipe`, `host`, `shown`, `browser`, `page`, `splash` and `by` (`StartStages`,
`src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StartStages.cs`).

## 2. The path, in order

| When | What runs | Where |
|---|---|---|
| 0 → `main` | the runtime starts behind CEF's launcher executable | `Daoris.Desktop.App/MainForm.cs:360-361` (the executable is CEF's launcher) |
| → `home` | the home established; an install's layout moved only where a move is planned | `Program.cs:57`; `HomePlacing.cs:77` returns early otherwise |
| → Build | the log, the host located (a few `File.Exists`), DI composed; `UseChromiumEngine` only registers the engine | `Program.cs:62-363` |
| `app.Run()` | the kit's runner: the single-instance guard, then **`cef_initialize` synchronously on the main thread**, then the `OnStarting` hooks | `Program.cs:368`; Shenora v0.22.0 `WindowsHostExtensions.cs:189-242`, `CefStartup.cs:146` |
| → `driver` (`app.started`) | `loop.Start()`: **the host is spawned only now**, behind Chromium's start, though it needs nothing of it | `Program.cs:323` → `DriverLoop.cs:558` → `HostSupervisor.EnsureAsync` (`HostSupervisor.cs:95-184`, a 300 ms poll, `:160-177`) |
| meanwhile | the form made, the splash's stopwatch started (`SplashLift`'s field), the window shown | kit `WindowsHostExtensions.cs:244-289`; `MainForm.cs:120-124` |
| → `host` | the host's own start: SQLite open and eight stores' schema checks, the registry, the change stream's first sample; its `app.started` is said just before it listens | `Daoris.Service.Http/Program.cs:188, 233, 2462-2475`; `ServiceFactory.cs:296-313` |
| → `browser` | the form waits on `HostReady`, then makes the page's browser | `MainForm.cs:432-448` |
| → `page` | the page says `CLOSE_SPLASH` only once **every** query has settled and 150 ms passed with none fetching | `src/Daoris.Web/src/app/splash.ts:22-39`, `App.tsx:250` |
| → `splash` | the lift waits for the page's word or 10 s **from the browser made** | `MainForm.cs:453`; `SplashLift.cs:28` |

## 3. What runs before the first page's data that the page does not need

1. **Chromium's start before the host's spawn.** The 11 s to `app.started` is the runtime and `cef_initialize` on a cold disk (and a
   security product's first look at each binary); Daoris's own code before Build is well under a second (`InstallHome.Establish`,
   `MachineLog.Open`, `ServiceHostLocator.Locate`, `KeptScheme.Read`). The host's 5 s then begins only after it, at `Program.cs:323`.
   This is the largest structural cost: two cold starts in series that need nothing of each other.
2. **The first look's follow and tidy.** `DriverWatch.cs:954` (`tidy`) and `:980` (`follow`) run beside the look (`beside: true`),
   so they block neither the look nor the window, but they take the background's git start places (`ProcessStarts.cs:51-54`, three of
   four) at the moment the page's git-backed pulls (`LINES`, `LinesRoutes.cs:80-92`) and the harness probes (`HARNESSES`,
   `AgentsRoutes.cs:38-56`) ask for them. Their 5 s each (`look.slow`, `LookStages.cs:119`) is spent competing with the first page.
   The orphan sweep and the uncertain words run in the look itself (`DriverWatch.cs:673, 686`).
3. **The help room's session look.** `help.room.look` (3.2 s) is the machine facts' sessions judge (`HelpRoutes.cs:219-245`), started
   with the facts at `DriverLoop.cs:716`, the same `SessionGroups.LookAsync` the groups slice (`GroupsStateSlice.cs:135`) and the
   page's `SESSION_GROUPS` (`SessionsRoutes.cs:562`) take: up to three looks against a host that has just come up. Ask Daoris is not
   on the first screen.
4. **`confirmations/waiting` held 20 s is no slowness.** It is the desktop's own long poll, `wait=20`
   (`ServiceConfirmations.cs:38`, started at `DriverLoop.cs:720`, awaited by nothing). The page does not ask it. The host's
   `request.failed` exempts only the change stream's held wait (`Daoris.Service.Http/Program.cs:284-292`), so a wait held by design
   is written as a failure. It costs the start nothing; the line misleads whoever reads it.
5. **On the window's thread**, nothing measured is seconds: the constructor's registry read, icon and tray icon
   (`MainForm.cs:397-413, 364-375`; `SessionNotifier.cs:82-87`), the window-state read and the stall watch's timer. The tray icon
   can wait on an Explorer still starting at boot; `shown` will say it.

## 4. Why the splash lifted by its bound

The lift's 10 s bound starts at `browser` (`MainForm.cs:448-453`), while its `ms` counts from the form's construction
(`SplashLift.cs:31`): 15 435 ms is 5.4 s waiting on the host, then the full 10 s. The page did not say within those 10 s because
`useSplashLift` waits for `useIsFetching()` to be nought (`splash.ts:27`), which counts every query the page started, not the first
screen's: the harness probes and `LINES` queued behind follow and tidy, and each `DRIVER_TICK` and `DRIVER_READY` invalidates the
sessions, quests, asks and driver queries again (`ShellSignals.tsx:88-101`), so the page rarely sees 150 ms of quiet while the first
looks run. The Sessions view was open at 18 s; the splash still held it until about 26 s.

## 5. The proposed cut (STARTFAST1b), with expected savings

| Cut | Where | Expected |
|---|---|---|
| a. Spawn (or adopt) the host before `app.Run()`, right after `Locate`, so its cold start overlaps Chromium's; `HostReady` unchanged | `Program.cs:117` → `HostSupervisor.EnsureAsync` started early; `DriverLoop.RunAsync` awaits the same task | ~5 s off `host`, `browser` and `page` (the host's whole start hidden under `cef_initialize`) |
| b. Lift on the first screen's queries, not every query: the view says its own data is in (its queries named), the global quiet kept as the fallback | `splash.ts`, the view's root | `splash` at `page` ≈ the 18 s view instead of 26 s; ~8 s |
| c. Hold the first look's `follow` and `tidy`, and the facts' sessions judge, until the page's word or a short bound (30 s) | `DriverWatch.cs:954, 980`; `DriverLoop.cs:716` | the page's git and probe pulls not queued behind them; 1-3 s on `page`, read from `start.stages` before and after |
| d. Exempt `confirmations/waiting`'s held wait from `request.failed` as the change stream's is | `Daoris.Service.Http/Program.cs:285` | nothing on the clock; one false failure a start fewer |

Together: a cold start's first data from about 18 s toward 12-13 s, the splash down with it rather than at 26 s; a warm start gains
the overlap alone. Each cut is proved by `start.stages` on the install before and after, and (b) and (d) by their own tests.
