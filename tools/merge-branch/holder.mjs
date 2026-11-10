/**
 * Who holds a lock, by pid and creation time: any later process may take a pid once its holder is gone, so a lock whose holder
 * is killed outright would read as held for as long as the machine runs. The creation time is the identity beside the pid.
 */
import { spawnSync } from 'node:child_process';

/** Whether a process runs with this pid: signal 0 asks without sending, and EPERM is one that runs as someone else. */
export function pidAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === 'EPERM';
  }
}

/** How far two readings of one process's creation time may differ: the platforms round it to a second or so. */
export const START_TOLERANCE_MS = 2000;

/** When the process `pid` began, in epoch milliseconds; null where it does not run or the machine would not say. */
export function processStart(pid, { platform = process.platform, run = spawnSync } = {}) {
  if (!Number.isInteger(pid) || pid <= 0) return null;
  if (pid === process.pid) return Math.round(Date.now() - process.uptime() * 1000);
  const asked = platform === 'win32'
    ? run('powershell', ['-NoProfile', '-Command',
      `$p = Get-Process -Id ${pid} -ErrorAction SilentlyContinue; if ($p) { [DateTimeOffset]::new($p.StartTime).ToUnixTimeMilliseconds() }`],
    { encoding: 'utf8', windowsHide: true })
    : run('ps', ['-o', 'lstart=', '-p', String(pid)], { encoding: 'utf8', env: { ...process.env, LC_ALL: 'C' } });
  const text = String(asked.stdout ?? '').trim();
  if (asked.status !== 0 || !text) return null;
  const at = platform === 'win32' ? Number(text) : Date.parse(text);
  return Number.isFinite(at) ? at : null;
}

/** Whether the run a lock file's `text` names still runs: its pid is alive and began when the lock says. */
export function holderRuns(text, alive) {
  try {
    const held = JSON.parse(text);
    const pid = Number(held?.pid);
    return alive(pid) && sameHolder(pid, held?.started);
  } catch {
    return false;
  }
}

/**
 * Whether the process now holding `pid` is the one that began at `started`. A record with no start and a machine that will not
 * say are taken as the same, so the pid alone decides there.
 */
export function sameHolder(pid, started, startOf = processStart) {
  if (!Number.isFinite(started)) return true;
  const now = startOf(pid);
  return now === null || Math.abs(now - started) <= START_TOLERANCE_MS;
}
