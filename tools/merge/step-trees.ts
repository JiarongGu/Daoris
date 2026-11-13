/**
 * The trees of the steps a run has running, and ending one by its pid: the run's stop reaches each step and what it started.
 */
import { spawnSync } from 'node:child_process';

export type StepTrees = { ended: string | null; hold(pid: number): void; release(pid: number): boolean; end(why: string): void };
type Run = (command: string, args: string[], options: { stdio?: 'ignore'; windowsHide?: boolean; encoding?: 'utf8' }) => { status?: number | null; stdout?: unknown };
type EndOptions = {
  platform?: string; run?: Run; kill?: (pid: number, signal: NodeJS.Signals) => void; pause?: (ms: number) => void; tries?: number;
};

/**
 * The step trees a run has running, each by its step's own pid, so a superseded or stopped run ends every one at
 * once. `end(why)` ends each held tree (`endTree`, the seam) and leaves `ended` saying why; a step started after it starts
 * nothing (`guarded`). `release` answers whether the run ended that step, so its own exit is not read as the gate's.
 */
export function stepTrees({ end = endTree }: { end?: (pid: number) => unknown } = {}): StepTrees {
  const live = new Set<number>();
  const ended = new Set<number>();
  const trees: StepTrees = {
    ended: null,
    hold(pid) {
      live.add(pid);
    },
    release(pid) {
      live.delete(pid);
      return ended.delete(pid);
    },
    end(why) {
      trees.ended ??= why;
      for (const pid of [...live]) {
        ended.add(pid);
        end(pid);
      }
    },
  };
  return trees;
}

/**
 * End a step's whole tree by the step's own pid, never by image name (the owner's processes share the names).
 * On Windows a kill reaches the one process it names, and a shell's command outlives it, so `taskkill /T`
 * ends what the step started too. Elsewhere `ps` says what is under it, each then killed. What a process of the tree left
 * running after its own parent ended is in no tree, and no build server is shut down: the run's are its own
 * (`BESIDE_ENVIRONMENT`), and the owner's are not to be touched. Answers whether the root is gone: taskkill's refusal is asked
 * again, and a tree still standing after the last try is answered false so its caller names the leak rather than waiting on it.
 */
export function endTree(pid: number, {
  platform = process.platform, run = spawnSync as Run, kill = (each, signal) => process.kill(each, signal), pause = pauseFor, tries = END_TRIES,
}: EndOptions = {}): boolean {
  if (platform === 'win32') {
    for (let attempt = 1; ; attempt += 1) {
      const { status } = run('taskkill', ['/T', '/F', '/PID', String(pid)], { stdio: 'ignore', windowsHide: true });
      if (status === 0 || status === TASKKILL_NO_PROCESS) return true;
      if (attempt >= tries) return false;
      pause(1000);
    }
  }
  const listed = run('ps', ['-A', '-o', 'pid=', '-o', 'ppid='], { encoding: 'utf8' });
  for (const each of [pid, ...descendants(String(listed.stdout ?? ''), pid)]) {
    try {
      kill(each, 'SIGKILL');
    } catch {
      // Already ended.
    }
  }
  return true;
}

const END_TRIES = 3;

const TASKKILL_NO_PROCESS = 128;

/** On the caller's thread: the callers end trees from synchronous paths. */
function pauseFor(ms: number): void {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

/** The pids under `pid` in a `ps -o pid= -o ppid=` listing, at any depth. */
export function descendants(listing: string, pid: number): number[] {
  const children = new Map<number, number[]>();
  for (const line of listing.split('\n')) {
    const [child, parent] = line.trim().split(/\s+/).map(Number);
    if (Number.isInteger(child) && Number.isInteger(parent)) children.set(parent as number, [...(children.get(parent as number) ?? []), child as number]);
  }
  const found: number[] = [];
  const walk = (at: number): void => {
    for (const child of children.get(at) ?? []) {
      if (child === pid || found.includes(child)) continue;
      found.push(child);
      walk(child);
    }
  };
  walk(pid);
  return found;
}
