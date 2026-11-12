/**
 * The processes a gate started, by pid and creation time: read once its steps end, stopped with each checked again against its
 * creation time (a pid is reused), and never any process the gate did not start. An exit that says the process ended rather
 * than that a check failed is named abnormal, so the run goes on with the next gate.
 */
import { spawnSync } from 'node:child_process';

export type Proc = { pid: number; ppid: number; created: string; command: string };
export type Owned = { pid: number; created: string };
export type Stopped = Owned & { stopped: boolean };

const key = (proc: { pid: number; created: string }): string => `${proc.pid}@${proc.created}`;

/**
 * The processes in `after` that are not in `before`, were created at or after `startedAt`, and descend by parent pid from one of
 * the gate's step pids (`roots`). A parent that has ended still names its children, so the chain holds through it.
 */
export function ownedAfter({ before, after, roots, startedAt }: { before: Proc[]; after: Proc[]; roots: number[]; startedAt: string }): Proc[] {
  const known = new Set(before.map(key));
  const fresh = after.filter((proc) => !known.has(key(proc)) && proc.created >= startedAt);
  const mine = new Set<number>(roots);
  const owned: Proc[] = [];
  for (let grew = true; grew;) {
    grew = false;
    for (const proc of fresh) {
      if (!mine.has(proc.pid) && mine.has(proc.ppid)) {
        mine.add(proc.pid);
        owned.push(proc);
        grew = true;
      }
    }
  }
  return owned.sort((a, b) => a.pid - b.pid);
}

/**
 * Each owned process still running with the same creation time ended by `kill` (which answers whether it is gone); one gone, or
 * whose pid is another process now, is left alone and counted stopped.
 */
export function stopOwned(owned: Owned[], { list, kill }: { list: () => Proc[]; kill: (pid: number) => boolean }): Stopped[] {
  const running = new Set(list().map(key));
  return owned.map((each) => ({ ...each, stopped: running.has(key(each)) ? kill(each.pid) : true }));
}

/** A process-level end: a signal, a shell that could not run the command, a Windows crash status; never a check's verdict. */
export function abnormalEnd(code: number): boolean {
  return code === 126 || code === 127 || code === 9009 || (code >= 128 && code <= 255) || code >= 0xC0000000 || code < 0;
}

const iso = (text: string): string => {
  const parsed = Date.parse(text);
  return Number.isFinite(parsed) ? new Date(parsed).toISOString() : text;
};

export function parseWindowsList(text: string): Proc[] {
  const json = text.replace(/^﻿/, '').trim();
  const parsed: unknown = json ? JSON.parse(json) : [];
  const rows = (Array.isArray(parsed) ? parsed : [parsed]) as Array<Record<string, unknown>>;
  return rows.map((row) => ({
    pid: Number(row.ProcessId), ppid: Number(row.ParentProcessId), created: iso(String(row.CreationDate ?? '')), command: String(row.CommandLine ?? ''),
  }));
}

const WINDOWS_QUERY = '[Console]::OutputEncoding = [Text.Encoding]::UTF8; '
  + 'ConvertTo-Json -Compress -InputObject @(Get-CimInstance Win32_Process | ForEach-Object { [pscustomobject]@{ ProcessId = $_.ProcessId; ParentProcessId = $_.ParentProcessId; CreationDate = $_.CreationDate.ToUniversalTime().ToString("o"); CommandLine = $_.CommandLine } })';

/** The machine's processes with parent and creation time; empty where the platform's tool does not answer. */
export function listProcesses(): Proc[] {
  try {
    if (process.platform === 'win32') {
      const ran = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', WINDOWS_QUERY], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
      return ran.status === 0 ? parseWindowsList(ran.stdout) : [];
    }
    const ran = spawnSync('ps', ['-A', '-o', 'pid=', '-o', 'ppid=', '-o', 'lstart=', '-o', 'args='], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    if (ran.status !== 0) return [];
    return ran.stdout.split('\n').flatMap((line) => {
      const found = /^\s*(\d+)\s+(\d+)\s+(\w{3} \w{3}\s+\d+ [\d:]{8} \d{4})\s+(.*)$/.exec(line);
      return found ? [{ pid: Number(found[1]), ppid: Number(found[2]), created: iso(`${found[3]} UTC`), command: found[4] ?? '' }] : [];
    });
  } catch {
    return [];
  }
}

/** End one process and what it started, by its pid; whether it is gone. */
export function killTree(pid: number): boolean {
  if (process.platform === 'win32') {
    const ran = spawnSync('taskkill', ['/T', '/F', '/PID', String(pid)], { stdio: 'ignore', windowsHide: true });
    return ran.status === 0 || ran.status === 128;
  }
  try {
    process.kill(pid, 'SIGKILL');
  } catch {
    // Already gone.
  }
  return true;
}
