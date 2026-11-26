/**
 * Which processes run from a given executable, asked of the machine reader (`tools/machine-read.mjs`: no PowerShell, no WMI) —
 * by PATH, never guessed by name.
 *
 * One home for the query: the dev loop, the deployment gate and the desktop publish share it. What is DONE to a process
 * (closing its windows) is still a PowerShell's, and the quoting that makes it match a path with an apostrophe in it is written once.
 */
import { spawnSync } from 'node:child_process';
import { realpathSync } from 'node:fs';
import { readProcesses as readTable } from './machine-read.mjs';

/** Run a PowerShell script and answer its standard output — empty when it wrote none. */
export const powershell = (script) =>
  spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8', windowsHide: true }).stdout ?? '';

/**
 * Text as a PowerShell single-quoted literal. Backslashes stay literal there (doubling them makes a
 * path never match), and the one character that needs escaping is `'`, doubled: a checkout
 * under a folder with an apostrophe ended the string early, and the query matched nothing.
 */
export const psQuote = (text) => `'${String(text).replaceAll("'", "''")}'`;

/** A path as it is compared: Windows' paths are the same in any case and with either slash. */
const samePathText = (text) => String(text ?? '').replaceAll('/', '\\').toLowerCase();

/** The path as the file system has it, a junction or a link resolved; the path itself where it is not there. */
const resolvedPath = (path) => {
  try {
    return realpathSync.native(path);
  } catch {
    return path;
  }
};

/**
 * The processes started from `exe`, by their full path, as the machine reader's rows. A comparison that never matches is a kill
 * that silently no-ops, leaving the old window holding the port, so the path is compared whole, in any case, with either slash,
 * and as the system names it: the system names the file a junction or a link leads to (a Node started through a junction runs from
 * the folder it points at), so `exe` matches by itself or resolved.
 * Throws, saying why, where the table could not be read.
 */
export const startedFrom = (exe, options = {}) => {
  const wanted = new Set([samePathText(exe), samePathText(resolvedPath(exe))]);
  return readTable(options).filter((row) => row.path !== null && wanted.has(samePathText(row.path)));
};

/** The pids running from a given executable. */
export const running = (exe) => {
  if (!exe) return [];
  return startedFrom(exe).map((row) => row.pid).filter((pid) => Number.isInteger(pid) && pid > 0);
};

/**
 * Whether a command line is one of the engine's own processes. Chromium starts its renderer,
 * GPU and utility processes from the app's executable, told which with `--type=`. They go with the
 * browser process that started them, and one closed or killed on its own is a page that crashes,
 * not a window that closes.
 */
export const isEngineProcess = (commandLine) => /(?:^|\s)--type=/.test(commandLine ?? '');

/**
 * The argument that makes the application's executable Daoris's browser, first on its
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
 * Whether a command line is Daoris's browser: the application's own executable, started
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
 * `pid|command line` rows. Exported for its test: the query is the machine reader's, the judgement is this.
 */
export function applicationsIn(rows) {
  return rowsOf(rows)
    .filter(({ commandLine }) => !isEngineProcess(commandLine) && !isBrowserProcess(commandLine))
    .map(({ pid }) => pid);
}

/** The browsers among `pid|command line` rows: {@link isBrowserProcess}, as pids. */
export function browsersIn(rows) {
  return rowsOf(rows).filter(({ commandLine }) => isBrowserProcess(commandLine)).map(({ pid }) => pid);
}

/**
 * Every process running from `exe`, as `pid|command line` rows: what the judgements above read. Exported
 * for `shot`, which reads both judgements from one query: each costs a start of the reader.
 */
export const commandLinesAt = (exe) => startedFrom(exe).map((row) => `${row.pid}|${row.command}`).join('\n');

/** The pids of the applications running from `exe`: {@link running}, without the engine's own or the browser. */
export const applicationsAt = (exe) => (exe ? applicationsIn(commandLinesAt(exe)) : []);

/** The pids of Daoris's browsers running from `exe`, the application's own executable. */
export const browsersAt = (exe) => (exe ? browsersIn(commandLinesAt(exe)) : []);

/** The applications only, as `running` finds them: `each` runs for every one {@link applicationsAt} finds. */
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
 * loop, then the host). A forced kill can orphan a `daoris-knowledge-http` that keeps the port and
 * holds the build's own DLLs, failing the next `build`. The host a shell starts reads a pipe whose
 * other end only that shell holds, and stops when it breaks, so a killed shell's host goes too (observed by hand, not gated);
 * the close is still the method, because it is what ends and records the loop's sessions. Nothing
 * here touches a host directly, because a host this tool did not start belongs to whoever did (the
 * shell's own supervisor makes exactly that distinction).
 *
 * 🔴 `CloseMainWindow` closes whichever window WINDOWS calls main, and that may be the
 * monitor rather than the application — closing it leaves the app running, the wait expires, and the
 * force kill lands. So this closes REPEATEDLY, re-reading the handle each time: the secondary
 * windows go first, the main window last, and the app exits on its own terms. Same trap as the one
 * `shot --window` exists for, in its third disguise.
 *
 * 🔴 The applications only: on Chromium the engine's renderer, GPU and utility processes run
 * from the same executable with no window. Walked like the rest, each cost fifteen seconds of
 * waiting and then a forced kill that crashed a page; they exit with the application. The browser
 * runs from it too, and is left alone for the same reason from the other side: it has
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
 * Every process on the machine as `{ pid, ppid, command }`, read and changing nothing: null where the list could not be read, never an
 * empty machine. One start of the machine reader, bounded by its read timeout; `run` and `helper` are a test's.
 */
export function processList(options = {}) {
  try {
    const rows = readTable(options).filter((row) => row.pid > 0).map(({ pid, ppid, command }) => ({ pid, ppid, command }));
    return rows.length > 0 ? rows : null;
  } catch {
    return null;
  }
}

/**
 * End what a killed process left running: every process `parent` started, at or after `since` (epoch
 * milliseconds), each with everything it started in turn. Answers the pids it ended.
 *
 * 🔴 On Windows a kill reaches the one process it names. A command run in a shell and killed by its timeout loses the shell
 * alone, and what the shell started runs on, its parent's id still naming the dead shell: the family rehearsal's driver, a
 * `dotnet` under `cmd.exe`, kept its home's lease after its bound, and every phase after it read or raced what it went on to
 * do. `since` keeps a later process that reused the id out of it. Elsewhere a shell's single command replaces the shell, so
 * the kill reaches it, and this ends nothing.
 */
export function endOrphans(parent, since, until = null) {
  if (process.platform !== 'win32' || !Number.isInteger(parent) || parent <= 0) return [];
  const bounded = Number.isFinite(until) && until !== null;
  let table;
  try {
    table = readTable();
  } catch {
    return [];
  }
  const orphans = table
    .filter((row) => row.ppid === parent && row.created >= Math.floor(since) - 1000 && (!bounded || row.created < Math.floor(until)))
    .map((row) => row.pid).filter((pid) => Number.isInteger(pid) && pid > 0);
  for (const pid of orphans) spawnSync('taskkill', ['/F', '/T', '/PID', String(pid)], { stdio: 'ignore', windowsHide: true });
  return orphans;
}

/**
 * The roots of what a run's tests left running, from `processes` (`{ pid, ppid, created, command }`, `created` in epoch
 * milliseconds): each process created at or after `since` whose parent is in no row (nothing holds it) and which, or something
 * under it, has `folder` in its command line (as a folder: the name ends there or goes on with a separator). The run's own end
 * reaches its steps by their parent ids, so what a test killed mid-way had already orphaned is held by no tree; it is found by
 * the folder it names, or by what it started naming it, since a gate run as `node qgate.mjs check` names nothing. `except` lists
 * the pids the run is made of, and a root that is one of them or holds one is left out. A root older than `since` is never taken.
 */
export function strandedRoots(processes, { folder, since, except = [] }) {
  const flat = (text) => String(text ?? '').replaceAll('\\', '/').toLowerCase();
  const wanted = flat(folder).replace(/\/+$/, '');
  if (!wanted) return [];
  const names = (command) => {
    const text = flat(command);
    for (let at = text.indexOf(wanted); at >= 0; at = text.indexOf(wanted, at + 1)) {
      const next = text[at + wanted.length];
      if (next === undefined || /[/\s"']/.test(next)) return true;
    }
    return false;
  };
  const ids = new Set(processes.map((each) => each.pid));
  const children = new Map();
  for (const each of processes) children.set(each.ppid, [...(children.get(each.ppid) ?? []), each]);
  /** Everything under `root`, itself included; a pid listed twice in a cycle (a reused id) is walked once. */
  const tree = (root) => {
    const seen = new Set([root.pid]);
    const found = [root];
    for (let i = 0; i < found.length; i += 1) {
      for (const child of children.get(found[i].pid) ?? []) {
        if (!seen.has(child.pid)) {
          seen.add(child.pid);
          found.push(child);
        }
      }
    }
    return found;
  };
  return processes
    .filter((each) => each.created >= since && !ids.has(each.ppid))
    .map((each) => ({ each, under: tree(each) }))
    .filter(({ under }) => !under.some((one) => except.includes(one.pid)) && under.some((one) => names(one.command)))
    .map(({ each }) => each);
}

/**
 * End what a run's tests left running under `folder`: the {@link strandedRoots} created at or after `since` (epoch
 * milliseconds), each with its tree. Answers the pids it ended, roots only. Only Windows ends any: there a kill reaches the one process
 * it names, and what a killed test had started runs on under a dead parent id, in no tree the run holds. Elsewhere the run's end walks
 * `ps` for what is under each step (`endTree`), and this ends nothing.
 *
 * A root must be created since the run began, name the folder or hold a process that does, and neither be the runner nor hold it (`except`).
 */
export function endStrandedUnder(folder, since, { except = [], platform = process.platform, list = readTable, end =(pid) => spawnSync('taskkill', ['/F', '/T', '/PID', String(pid)], { stdio: 'ignore', windowsHide: true }) } = {}) {
  if (platform !== 'win32' || typeof folder !== 'string' || folder.length === 0) return [];
  let table;
  try {
    table = list();
  } catch {
    return [];
  }
  const roots = strandedRoots(table, { folder, since: Math.floor(since) - 1000, except: [...except, process.pid] }).map((each) => each.pid);
  for (const pid of roots) end(pid);
  return roots;
}
