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
 * The applications running from `exe`, the engine's own processes left out, from `pid|command line`
 * rows. Exported for its test: the query is PowerShell's, the judgement is this.
 */
export function applicationsIn(rows) {
  return rows.split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const bar = line.indexOf('|');
      return { pid: Number(line.slice(0, bar)), commandLine: line.slice(bar + 1) };
    })
    .filter(({ pid, commandLine }) => Number.isInteger(pid) && pid > 0 && !isEngineProcess(commandLine))
    .map(({ pid }) => pid);
}

/** The pids of the applications running from `exe`: {@link running}, without the engine's own. */
export const applicationsAt = (exe) => {
  if (!exe) return [];
  return applicationsIn(powershell(
    'Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | '
    + `Where-Object { $_.ExecutablePath -eq ${psQuote(exe)} } | `
    + 'ForEach-Object { "$($_.ProcessId)|$($_.CommandLine)" }'));
};

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
 * loop, then the host). A forced kill orphans a `daoris-knowledge-http` that keeps the port and holds
 * the build's own DLLs. Measured: seven of them, after a session of restarts, failing the next
 * `build` with MSB3027. Nothing here touches a host directly, because a host this tool did not start
 * belongs to whoever did (the shell's own supervisor makes exactly that distinction).
 *
 * 🔴 `CloseMainWindow` closes whichever window WINDOWS calls main, and since SURF8 that may be the
 * monitor rather than the application — closing it leaves the app running, the wait expires, and the
 * force kill lands. So this closes REPEATEDLY, re-reading the handle each time: the secondary
 * windows go first, the main window last, and the app exits on its own terms. Same trap as the one
 * `shot --window` exists for, in its third disguise.
 *
 * 🔴 The applications only (CHR4): on Chromium the engine's renderer, GPU and utility processes run
 * from the same executable with no window. Walked like the rest, each cost fifteen seconds of
 * waiting and then a forced kill that crashed a page; they exit with the application.
 */
export const stopAll = (exe) => eachApplicationAt(exe, `
    $process = $_
    for ($attempt = 0; $attempt -lt 6 -and -not $process.HasExited; $attempt++) {
      $process.Refresh()
      if ($process.MainWindowHandle -ne 0) { $process.CloseMainWindow() | Out-Null }
      $process.WaitForExit(2500) | Out-Null
    }
    if (-not $process.HasExited) { $process | Stop-Process -Force }
  `);
