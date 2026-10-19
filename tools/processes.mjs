/**
 * Which processes run from a given executable, asked of Windows — by PATH, never guessed by name.
 *
 * One home for the query (REV3 CLEAN1): the dev loop, the deployment gate and the desktop publish
 * each wrote their own `Where-Object { $_.Path -eq … }`, and the quoting that makes it match a path
 * with an apostrophe in it had been fixed in two of them.
 */
import { spawnSync } from 'node:child_process';

/** Run a PowerShell script and answer its standard output — empty when it wrote none. */
export const powershell = (script) =>
  spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).stdout ?? '';

/**
 * Text as a PowerShell single-quoted literal. Backslashes stay literal there (doubling them makes a
 * path never match), and the one character that needs escaping is `'`, doubled (REV3): a checkout
 * under a folder with an apostrophe ended the string early, and the query matched nothing.
 */
export const psQuote = (text) => `'${String(text).replaceAll("'", "''")}'`;

/**
 * Run `each` — a PowerShell block with the process as `$_` — for every process started from `exe`,
 * and answer what it printed.
 *
 * Quoted by {@link psQuote}: a comparison that never matches is a kill that silently no-ops, leaving
 * the old window holding the port.
 */
export const processesAt = (exe, each) => powershell(
  `Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq ${psQuote(exe)} } | `
  + `ForEach-Object { ${each} }`);

/** The pids running from a given executable. */
export const running = (exe) => {
  if (!exe) return [];
  return processesAt(exe, '$_.Id')
    .split('\n').map((line) => Number(line.trim())).filter((pid) => Number.isInteger(pid) && pid > 0);
};

/**
 * Whether a command line is one of the engine's own processes (CHR4). Chromium starts its renderer,
 * GPU and utility processes from the app's executable, told which with `--type=`. They go with the
 * browser process that started them, and one closed or killed on its own is a page that crashes,
 * not a window that closes.
 */
export const isEngineProcess = (commandLine) => /(?:^|\s)--type=/.test(commandLine ?? '');

/**
 * The argument that makes the application's executable Daoris's browser (CHR8, D99), first on its
 * command line. A twin of `EngineBrowser.Argument` (`Daoris.Desktop.Modules`), which the application
 * routes on and the shell starts the browser with; `desktop-tool.test.ts` reads both.
 */
export const BROWSER_ARGUMENT = '--daoris-browser';

/** A command line's arguments, its executable left out: quoted as Windows quotes one, or up to the first space. */
const argumentsOf = (commandLine) => {
  const line = (commandLine ?? '').trimStart();
  const end = line.startsWith('"') ? line.indexOf('"', 1) + 1 : line.search(/\s|$/);
  return end <= 0 ? '' : line.slice(end).trim();
};

/**
 * Whether a command line is Daoris's browser (CHR8, D99): the application's own executable, started
 * with {@link BROWSER_ARGUMENT} first — the rule the application routes on — and not one of the
 * engine's processes. A fourth kind from the same path: not the application, whose stop it follows on
 * its own, and not an engine process, because it holds windows of its own.
 */
export const isBrowserProcess = (commandLine) =>
  !isEngineProcess(commandLine) && argumentsOf(commandLine).split(/\s+/)[0] === BROWSER_ARGUMENT;

/** `pid|command line` rows as pids and command lines, the malformed ones left out. */
const rowsOf = (rows) => rows.split('\n')
  .map((line) => line.trim())
  .filter(Boolean)
  .map((line) => {
    const bar = line.indexOf('|');
    return { pid: Number(line.slice(0, bar)), commandLine: line.slice(bar + 1) };
  })
  .filter(({ pid }) => Number.isInteger(pid) && pid > 0);

/**
 * The applications running from `exe`, the engine's own processes and the browser left out, from
 * `pid|command line` rows. Exported for its test: the query is PowerShell's, the judgement is this.
 */
export function applicationsIn(rows) {
  return rowsOf(rows)
    .filter(({ commandLine }) => !isEngineProcess(commandLine) && !isBrowserProcess(commandLine))
    .map(({ pid }) => pid);
}

/** The browsers among `pid|command line` rows (CHR8): {@link isBrowserProcess}, as pids. */
export function browsersIn(rows) {
  return rowsOf(rows).filter(({ commandLine }) => isBrowserProcess(commandLine)).map(({ pid }) => pid);
}

/**
 * Every process running from `exe`, as `pid|command line` rows: what the judgements above read. Exported
 * for `shot`, which reads both judgements from one query (SHOTPICK1): each costs a PowerShell start.
 */
export const commandLinesAt = (exe) => powershell(
  'Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | '
  + `Where-Object { $_.ExecutablePath -eq ${psQuote(exe)} } | `
  + 'ForEach-Object { "$($_.ProcessId)|$($_.CommandLine)" }');

/** The pids of the applications running from `exe`: {@link running}, without the engine's own or the browser. */
export const applicationsAt = (exe) => (exe ? applicationsIn(commandLinesAt(exe)) : []);

/** The pids of Daoris's browsers running from `exe`, the application's own executable since CHR8. */
export const browsersAt = (exe) => (exe ? browsersIn(commandLinesAt(exe)) : []);

/** {@link processesAt} for the applications only: `each` runs for every one {@link applicationsAt} finds. */
export const eachApplicationAt = (exe, each) => {
  const pids = applicationsAt(exe);
  return pids.length === 0 ? '' : powershell(
    `Get-Process -Id ${pids.join(',')} -ErrorAction SilentlyContinue | ForEach-Object { ${each} }`);
};

/**
 * Stop the shells running from `exe` — by CLOSING them, so their own shutdown path runs, with the
 * force as the backstop rather than the method.
 *
 * 🔴 That path is what stops the HTTP host the shell spawned and owns (`OnStopping`: the driver
 * loop, then the host). A forced kill used to orphan a `daoris-knowledge-http` that kept the port and
 * held the build's own DLLs. Measured: seven of them, after a session of restarts, failing the next
 * `build` with MSB3027. Since LOG2a the host a shell started reads a pipe whose other end only that
 * shell holds, and stops when it breaks, so a killed shell's host goes too (observed by hand, not gated);
 * the close is still the method, because it is what ends and records the loop's sessions. Nothing
 * here touches a host directly, because a host this tool did not start belongs to whoever did (the
 * shell's own supervisor makes exactly that distinction).
 *
 * 🔴 `CloseMainWindow` closes whichever window WINDOWS calls main, and since SURF8 that may be the
 * monitor rather than the application — closing it leaves the app running, the wait expires, and the
 * force kill lands. So this closes REPEATEDLY, re-reading the handle each time: the secondary
 * windows go first, the main window last, and the app exits on its own terms. Same trap as the one
 * `shot --window` exists for, in its third disguise.
 *
 * 🔴 The applications only (CHR4): on Chromium the engine's renderer, GPU and utility processes run
 * from the same executable with no window. Walked like the rest, each cost fifteen seconds of
 * waiting and then a forced kill that crashed a page; they exit with the application. The browser
 * runs from it too since CHR8, and is left alone for the same reason from the other side: it has
 * windows, and closing them is the person's; it closes itself once the application has gone.
 */
export const stopAll = (exe) => stopProcesses(applicationsAt(exe));

/** How one process is closed: its windows, repeatedly, and the force last. */
const CLOSE = `
    $process = $_
    for ($attempt = 0; $attempt -lt 6 -and -not $process.HasExited; $attempt++) {
      $process.Refresh()
      if ($process.MainWindowHandle -ne 0) { $process.CloseMainWindow() | Out-Null }
      $process.WaitForExit(2500) | Out-Null
    }
    if (-not $process.HasExited) { $process | Stop-Process -Force }
  `;

/** Close these processes as {@link stopAll} closes an executable's applications. */
export const stopProcesses = (pids) => (pids.length === 0 ? '' : powershell(
  `Get-Process -Id ${pids.join(',')} -ErrorAction SilentlyContinue | ForEach-Object { ${CLOSE} }`));

/**
 * End what a killed process left running (FAMILYFAIL1): every process `parent` started, at or after `since` (epoch
 * milliseconds), each with everything it started in turn. Answers the pids it ended.
 *
 * 🔴 On Windows a kill reaches the one process it names. A command run in a shell and killed by its timeout loses the shell
 * alone, and what the shell started runs on, its parent's id still naming the dead shell: the family rehearsal's driver, a
 * `dotnet` under `cmd.exe`, kept its home's lease after its bound, and every phase after it read or raced what it went on to
 * do. `since` keeps a later process that reused the id out of it. Elsewhere a shell's single command replaces the shell, so
 * the kill reaches it, and this ends nothing.
 */
export function endOrphans(parent, since) {
  if (process.platform !== 'win32' || !Number.isInteger(parent) || parent <= 0) return [];
  const orphans = powershell(
    `Get-CimInstance Win32_Process -Filter "ParentProcessId = ${parent}" -ErrorAction SilentlyContinue | `
    + `Where-Object { $_.CreationDate -ge [DateTimeOffset]::FromUnixTimeMilliseconds(${Math.floor(since) - 1000}).LocalDateTime } | `
    + 'ForEach-Object { $_.ProcessId }')
    .split('\n').map((line) => Number(line.trim())).filter((pid) => Number.isInteger(pid) && pid > 0);
  for (const pid of orphans) spawnSync('taskkill', ['/F', '/T', '/PID', String(pid)], { stdio: 'ignore', windowsHide: true });
  return orphans;
}

/**
 * End what a killed run's shells left running (CIPRIO1e): every process created at or after `since` (epoch milliseconds)
 * whose command line names `folder` and whose parent no longer runs, each with its tree. Answers the pids it ended.
 *
 * 🔴 {@link endOrphans} lists by the dead run's pid, but a killed node's non-detached children end with it on Windows: a
 * step's `cmd.exe` goes, and its child runs on under a dead parent id no listing by the run's pid finds. The gate tree's
 * folder in the command line is what such a process still carries. Only processes whose parent is gone are taken, so a
 * live run's own tree (its parent alive) is never ended, and `since` keeps an older process out.
 */
export function endStranded(folder, since) {
  if (process.platform !== 'win32' || typeof folder !== 'string' || folder.length === 0) return [];
  const forms = [folder.replaceAll('/', '\\'), folder.replaceAll('\\', '/')]
    .map((form) => `'${form.replaceAll("'", "''").toLowerCase()}'`).join(',');
  const stranded = powershell(
    '$all = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue); '
    + '$ids = @{}; foreach ($p in $all) { $ids[[int]$p.ProcessId] = $true }; '
    + `$forms = @(${forms}); `
    + `$from = [DateTimeOffset]::FromUnixTimeMilliseconds(${Math.floor(since) - 1000}).LocalDateTime; `
    + 'foreach ($p in $all) { '
    + 'if ($p.ProcessId -eq $PID -or -not $p.CommandLine -or $p.CreationDate -lt $from -or $ids.ContainsKey([int]$p.ParentProcessId)) { continue }; '
    + '$cl = $p.CommandLine.ToLower(); '
    + 'foreach ($form in $forms) { if ($cl.Contains($form)) { $p.ProcessId; break } } }')
    .split('\n').map((line) => Number(line.trim())).filter((pid) => Number.isInteger(pid) && pid > 0);
  for (const pid of stranded) spawnSync('taskkill', ['/F', '/T', '/PID', String(pid)], { stdio: 'ignore', windowsHide: true });
  return stranded;
}
