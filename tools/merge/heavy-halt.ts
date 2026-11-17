/**
 * A heavy tier's reached run asked to stop by a newer tier (`heavy-stack.ts`), through the file `--halt-file` names, `{ by, commit }`:
 * the newer tier and its commit. From the moment the file is there no class group starts (`asked`). The group whose command is in
 * flight is ended where the newer commit changes a path one of its classes reads (`reachedByNewer`: its key changes there, so it
 * runs again there and finishing it here is time spent twice), or where what the newer commit changed cannot be read; it is
 * finished otherwise, its passes and failures recorded as every run's are, since its keys stand on the newer commit. An ended group
 * hands its classes on (`ended`), with every group not started. A third landing's halt is judged again, on its own commit.
 */
import { existsSync, readFileSync } from 'node:fs';
import type { Exec } from './reached-run.ts';
import type { ReachedGroup } from './reached.ts';

export type HaltAsk = { by: string; commit: string };

/** The halt as the newer tier wrote it, or null where there is none or it does not read. */
export function readHalt(file: string): HaltAsk | null {
  try {
    if (!existsSync(file)) return null;
    const parsed = JSON.parse(readFileSync(file, 'utf8')) as Partial<HaltAsk> | null;
    return parsed && typeof parsed.by === 'string' && typeof parsed.commit === 'string' && parsed.by && parsed.commit ? { by: parsed.by, commit: parsed.commit } : null;
  } catch {
    return null;
  }
}

type Reaches = (unit: { suite: string; unit: string }, path: string) => boolean;

/** Whether a path the newer commit changed is one a class of the group reads. */
export const reachedByNewer = (group: ReachedGroup, paths: readonly string[], reaches: Reaches): boolean => group.units.some((unit) => paths.some((path) => reaches(unit, path)));

/** What `runPlan` asks of a halt: whether a newer tier asked, and whether it ended a group's command. */
export type Halt = { asked: () => boolean; ended: (group: ReachedGroup) => boolean };

export type HaltRun = Halt & {
  /** Reads the file again, and judges the group in flight against a newer commit it names. */
  look: () => void;
  /** The group placed, and the group's place let go. */
  enter: (group: ReachedGroup) => void;
  leave: () => void;
  /** The group's command started (its pid) or ended (null). */
  child: (pid: number | null) => void;
  /** Looks every `ms` until the answer is called. */
  watch: (ms: number) => () => void;
};

/** A command run whose start and end are told to `started`, so a halt can end the process the run started. */
export type TrackedExec = (command: string, args: string[], cwd: string, started: (pid: number | null) => void) => Promise<number | null>;

export function createHalt({ file, changedSince, reaches, end, say, read = readHalt }: {
  file: string; changedSince: (commit: string) => string[]; reaches: Reaches; end: (pid: number) => void; say: (line: string) => void; read?: (file: string) => HaltAsk | null;
}): HaltRun {
  let ask: HaltAsk | null = null;
  let inFlight: ReachedGroup | null = null;
  let pid: number | null = null;
  let judged: string | null = null;
  const ended = new Set<ReachedGroup>();
  const changed = new Map<string, string[] | null>();
  const changedOf = (commit: string): string[] | null => {
    if (!changed.has(commit)) {
      try {
        changed.set(commit, changedSince(commit));
      } catch {
        changed.set(commit, null);
      }
    }
    return changed.get(commit) ?? null;
  };
  const judge = (): void => {
    if (!ask || !inFlight || pid === null || ended.has(inFlight) || judged === ask.commit) return;
    judged = ask.commit;
    const paths = changedOf(ask.commit);
    const what = `${inFlight.suite} (${inFlight.units.length} ${inFlight.units.length === 1 ? 'class' : 'classes'})`;
    if (paths !== null && !reachedByNewer(inFlight, paths, reaches)) {
      say(`reached: the run finishes ${what}: ${ask.by}'s commit changes nothing they read, so their passes stand there`);
      return;
    }
    ended.add(inFlight);
    say(`reached: the run ends ${what}: ${paths === null ? `what ${ask.by}'s commit changed cannot be read` : `${ask.by}'s commit changes what they read`}, so they run there`);
    end(pid);
  };
  const look = (): void => {
    const seen = read(file);
    if (!seen) return;
    if (ask?.commit !== seen.commit) {
      ask = seen;
      say(`reached: ${seen.by} on ${seen.commit.slice(0, 7)} asks this run to stop: it starts no class more, and hands what it leaves unrun to it`);
    }
    judge();
  };
  return {
    look,
    asked: () => {
      look();
      return ask !== null;
    },
    ended: (group) => ended.has(group),
    enter: (group) => {
      inFlight = group;
      pid = null;
      judged = null;
    },
    leave: () => {
      inFlight = null;
      pid = null;
    },
    child: (started) => {
      pid = started;
      if (started === null) return;
      if (inFlight && ended.has(inFlight)) end(started);
      else judge();
    },
    watch: (ms) => {
      const timer = setInterval(look, ms);
      timer.unref?.();
      return () => clearInterval(timer);
    },
  };
}

/** A plan's `exec` and `inPlace` told to the halt: which group is in flight, and the pid of the command it started. */
export function wired(halt: HaltRun, exec: TrackedExec, place?: <T>(group: ReachedGroup, fn: () => Promise<T>) => Promise<T>): { exec: Exec; inPlace: <T>(group: ReachedGroup, fn: () => Promise<T>) => Promise<T> } {
  return {
    exec: (command, args, cwd) => exec(command, args, cwd, (pid) => halt.child(pid)),
    inPlace: async <T>(group: ReachedGroup, fn: () => Promise<T>): Promise<T> => {
      halt.enter(group);
      try {
        return await (place ? place(group, fn) : fn());
      } finally {
        halt.leave();
      }
    },
  };
}
