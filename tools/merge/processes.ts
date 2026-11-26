/** The processes a gate started, by pid and creation time (a pid is reused), stopped and never any the gate did not start; an exit that says the process ended is abnormal. */
import { spawnSync } from 'node:child_process';
import { readProcesses, type ProcessRow } from '../machine-read.mjs';

export type Proc = { pid: number; ppid: number; created: string; command: string };
export type Owned = { pid: number; created: string };
export type Stopped = Owned & { stopped: boolean };

const key = (proc: { pid: number; created: string }): string => `${proc.pid}@${proc.created}`;

/** The processes in `after` not in `before`, created at or after `startedAt`, descending by parent pid from a step pid (`roots`); a parent that has ended still names its children. */
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

/** Each owned process still running with the same creation time ended by `kill`; one gone, or whose pid is another process now, is left alone and counted stopped. */
export function stopOwned(owned: Owned[], { list, kill }: { list: () => Proc[]; kill: (pid: number) => boolean }): Stopped[] {
  const running = new Set(list().map(key));
  return owned.map((each) => ({ ...each, stopped: running.has(key(each)) ? kill(each.pid) : true }));
}

/** A process-level end: a signal, a shell that could not run the command, a Windows crash status; never a check's verdict. */
export function abnormalEnd(code: number): boolean {
  return code === 126 || code === 127 || code === 9009 || (code >= 128 && code <= 255) || code >= 0xC0000000 || code < 0;
}

export function procsOf(rows: ProcessRow[]): Proc[] {
  return rows.map((row) => ({ pid: row.pid, ppid: row.ppid, created: new Date(row.created).toISOString(), command: row.command }));
}

/** The machine's processes (the machine reader's rows, each creation time an instant); empty where it does not answer. */
export function listProcesses(): Proc[] {
  try {
    return procsOf(readProcesses());
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
