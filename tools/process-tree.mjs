#!/usr/bin/env node
/**
 * Which of Daoris's processes run outside its application's tree, by what started them: a read of this machine, read-only.
 *
 *   node tools/process-tree.mjs [--json]
 *
 * ## Why
 *
 * Task Manager lists a process under its parent only while that parent runs, and Windows cannot give a running process a new
 * parent. A run a terminal started from a shell that has since exited is listed on its own, however it is held. A heavy run is
 * the running Daoris's own child (`daoris gate run` asks it); this read says what is still outside, so the steward can check an
 * install.
 *
 * ## What it reads
 *
 * - **The applications**: each `Daoris.Desktop.exe` that is neither one of the engine's processes nor Daoris's browser
 *   (`isEngineProcess`, `isBrowserProcess`); its install is the folder above its `app/`.
 * - **Daoris's processes**: a program of Daoris's own by its name (`Daoris*.exe`, `daoris-*.exe`), one whose executable or
 *   command line names an install, and everything any of them started that still runs.
 * - **Inside**: an application and every process descended from it; a parent counts only where it started no later than its
 *   child, so a process id used again is no parent. What started an application (its launcher) is neither inside nor outside.
 * - **Outside**: the rest of Daoris's processes, each under the topmost of them whose parent is not Daoris's or no longer runs,
 *   which is what started it.
 *
 * Exit codes: 0 read · 2 it could not read (not Windows, or the query failed).
 */
import { isEngineProcess, isBrowserProcess, powershell } from './processes.mjs';
import { isMain } from './fsx.mjs';

const OWN_NAME = /^daoris(?:[-.][^\\/]*)?\.exe$/i;
const APPLICATION = /^daoris\.desktop\.exe$/i;

/** The folder above an executable's `app\` folder, or null where it runs from none. */
export const installOf = (path) => {
  const parts = String(path ?? '').split(/[\\/]/);
  const app = parts.findLastIndex((part) => part.toLowerCase() === 'app');
  return app > 0 ? parts.slice(0, app).join('\\') : null;
};

/**
 * The census of `processes`, each `{ pid, ppid, name, path, line, created }` (`created` in milliseconds): the applications with
 * how many run in each's tree, and the processes outside it grouped by their root, what started it, and what runs under it.
 */
export function census(processes) {
  const byPid = new Map(processes.map((process) => [process.pid, process]));
  const parentOf = (process) => {
    const parent = byPid.get(process.ppid);
    return parent && parent.pid !== process.pid && (parent.created ?? 0) <= (process.created ?? 0) ? parent : null;
  };
  const children = new Map();
  for (const process of processes) {
    const parent = parentOf(process);
    if (parent) children.set(parent.pid, [...(children.get(parent.pid) ?? []), process]);
  }
  const under = (root) => {
    const found = [];
    const next = [...(children.get(root.pid) ?? [])];
    while (next.length > 0) {
      const process = next.pop();
      found.push(process);
      next.push(...(children.get(process.pid) ?? []));
    }
    return found;
  };

  const applications = processes.filter((process) => APPLICATION.test(process.name ?? '')
    && !isEngineProcess(process.line) && !isBrowserProcess(process.line));
  const installs = [...new Set(applications.map((app) => installOf(app.path)).filter(Boolean))].map((root) => root.toLowerCase());
  const inside = new Set();
  const launchers = new Set();
  for (const app of applications) {
    inside.add(app.pid);
    for (const process of under(app)) inside.add(process.pid);
    for (let parent = parentOf(app); parent; parent = parentOf(parent)) launchers.add(parent.pid);
  }

  const inInstall = installs.map((root) => new RegExp(`${root.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:[\\\\/"'\\s]|$)`, 'm'));
  const named = (process) => OWN_NAME.test(process.name ?? '')
    || inInstall.some((pattern) => pattern.test(`${process.path ?? ''}\n${process.line ?? ''}`.toLowerCase()));
  const daoris = new Set();
  for (const process of processes.filter(named)) {
    daoris.add(process.pid);
    for (const child of under(process)) daoris.add(child.pid);
  }

  const outside = processes.filter((process) => daoris.has(process.pid) && !inside.has(process.pid) && !launchers.has(process.pid));
  const outsideSet = new Set(outside.map((process) => process.pid));
  const roots = outside.filter((process) => !outsideSet.has(parentOf(process)?.pid));
  return {
    applications: applications.map((app) => ({ pid: app.pid, install: installOf(app.path), inside: under(app).length + 1 })),
    outside: roots.map((root) => {
      const parent = parentOf(root);
      const counts = new Map();
      for (const process of under(root)) counts.set(process.name, (counts.get(process.name) ?? 0) + 1);
      return {
        pid: root.pid,
        name: root.name,
        line: root.line ?? '',
        startedBy: parent ? { pid: parent.pid, name: parent.name } : { pid: root.ppid, name: null },
        under: [...counts].map(([name, count]) => ({ name, count })).sort((a, b) => a.name.localeCompare(b.name)),
      };
    }),
  };
}

/** The census in the terminal's words, a line each. */
export function said({ applications, outside }) {
  const lines = applications.length === 0
    ? ['process-tree: no Daoris application runs here.']
    : applications.map((app) => `process-tree: Daoris.Desktop.exe (pid ${app.pid}${app.install ? `, install ${app.install}` : ''}): `
      + `${app.inside} process${app.inside === 1 ? '' : 'es'} in its tree.`);
  if (outside.length === 0) return [...lines, 'process-tree: no Daoris process runs outside it.'];
  const count = outside.reduce((all, root) => all + 1 + root.under.reduce((sum, each) => sum + each.count, 0), 0);
  lines.push(`process-tree: ${count} Daoris process${count === 1 ? '' : 'es'} outside it, by what started them:`);
  for (const root of outside) {
    const by = root.startedBy.name ? `${root.startedBy.name} (pid ${root.startedBy.pid})` : `pid ${root.startedBy.pid}, which no longer runs`;
    const below = root.under.length === 0 ? '' : `; under it: ${root.under.map((each) => (each.count === 1 ? each.name : `${each.name} ×${each.count}`)).join(', ')}`;
    lines.push(`  ${root.name} (pid ${root.pid}), started by ${by}: ${root.line}${below}`);
  }
  return lines;
}

/** Every process of this machine as the census reads them, or null where the query did not answer. */
export function machine() {
  const text = powershell('Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | ForEach-Object { [pscustomobject]@{ '
    + 'pid = $_.ProcessId; ppid = $_.ParentProcessId; name = $_.Name; path = $_.ExecutablePath; line = $_.CommandLine; '
    + 'created = if ($_.CreationDate) { ([DateTimeOffset]$_.CreationDate).ToUnixTimeMilliseconds() } else { 0 } } } | ConvertTo-Json -Compress');
  try {
    const rows = JSON.parse(text);
    return (Array.isArray(rows) ? rows : [rows]).filter((row) => row && row.pid !== process.pid && row.ppid !== process.pid);
  } catch {
    return null;
  }
}

if (isMain(import.meta.url)) {
  if (process.platform !== 'win32') {
    console.error('process-tree: a process tree is read on Windows alone.');
    process.exit(2);
  }
  const rows = machine();
  if (rows === null) {
    console.error('process-tree: Windows did not answer the query of its processes.');
    process.exit(2);
  }
  const read = census(rows);
  console.log(process.argv.includes('--json') ? JSON.stringify(read, null, 2) : said(read).join('\n'));
}
