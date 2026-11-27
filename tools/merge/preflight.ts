/**
 * The preflight: a builder runs its merge's fast plan from its own worktree before it hands back, so the merge
 * stops being where a branch's reds are first found. `node tools/merge-branch.mjs --preflight [--base <ref>] [--bound <minutes>]`
 * judges the branch the worktree has out against `main` (or `--base`) as the queue's scoped run judges its candidate: the paths
 * from their merge base to the branch's tip choose the gates (`scopedChoice`, the queue's own), of which it runs the fast ones
 * (`fastPlan`): never a Process or git half, a rehearsal or a browser spec, which it names deferred. Nothing on main, in the main
 * checkout or in the queue moves; its verdicts go to the one store, keyed by the tree they ran on, so a merge whose candidate holds
 * the same tree keeps them. A red writes the brief (`red-brief.ts`), each failure checked on main's tip in a tree of the
 * worktree's own under `local/scratch/main-tree`.
 *
 * It runs within its bound (`PREFLIGHT_BOUND_MINUTES` unless `--bound`): each minute it says what it waits on (`preflightRun`), and
 * at the bound it ends what it started, starts nothing more and says which gates passed, which failed, which it stopped and which
 * never started, and why. A preflight that ends unfinished is no pass, and says so.
 *
 * Here: what the preflight reads of the worktree before it runs, what it refuses, and its run within the bound.
 */
import { Refusal } from './args.ts';
import { stepTrees, type StepTrees } from './step-trees.ts';
import type { ScopedSet } from './fast-plan.ts';

type GitRun = (cwd: string, args: string[], options?: { allowFail?: boolean }) => { status: number | null; out: string };

/** The tree a merge's or preflight's failures are checked on: main's tip, detached, under the run's own scratch. */
export const MAIN_TREE = 'local/scratch/main-tree';

export type PreflightFacts = { branch: string; base: string; baseTip: string; from: string; tip: string; behind: number; lines: string[] };

/**
 * The branch this worktree has out, the base's tip and their merge base, read through `run`: refused on the base itself, on a
 * detached head, on an unknown base, on a branch that adds no commit, and on a tree with changes, which no merge would gate.
 * `lines` says what is judged, and how far the base has moved past the merge base.
 */
export function preflightFacts(root: string, base: string, run: GitRun): PreflightFacts {
  const read = (args: string[]): string | null => {
    const ran = run(root, args, { allowFail: true });
    return ran.status === 0 ? ran.out.trim() : null;
  };
  const branch = read(['symbolic-ref', '--quiet', '--short', 'HEAD']);
  if (!branch) throw new Refusal('--preflight gates a branch: this worktree has no branch out (a detached head)');
  const baseTip = read(['rev-parse', '--verify', '--quiet', `${base}^{commit}`]);
  if (!baseTip) throw new Refusal(`--preflight: no commit '${base}' to judge the branch against`);
  if (branch === base) throw new Refusal(`--preflight runs in a branch's worktree: this one has ${base} out, which is what a branch is merged into`);
  const changed = (read(['--no-optional-locks', 'status', '--porcelain']) ?? '').split('\n').filter(Boolean);
  if (changed.length) {
    throw new Refusal(`--preflight gates the branch's tip, as its merge does: commit or drop these first\n${changed.slice(0, 12).map((line) => `  ${line}`).join('\n')}${changed.length > 12 ? `\n  … ${changed.length - 12} more` : ''}`);
  }
  const tip = read(['rev-parse', 'HEAD']) as string;
  const from = read(['merge-base', baseTip, tip]);
  if (!from) throw new Refusal(`--preflight: ${branch} and ${base} share no history`);
  if (from === tip) throw new Refusal(`--preflight: ${branch} adds no commit to ${base}`);
  const behind = Number(read(['rev-list', '--count', `${tip}..${baseTip}`]) ?? 0);
  const lines = [`preflight of ${branch} at ${tip.slice(0, 7)} against ${base} at ${baseTip.slice(0, 7)}: the paths from their merge base ${from.slice(0, 7)} choose the gates, as its merge's do`];
  if (behind > 0) lines.push(`  ${base} is ${behind} commit${behind === 1 ? '' : 's'} past this branch: the gates run on the branch as it stands; merge ${base} into it to gate the tree its merge will make`);
  return { branch, base, baseTip, from, tip, behind, lines };
}

/** A builder's preflight gives up here unless `--bound` says otherwise: a fast plan's gates run in about ten minutes on a quiet machine. */
export const PREFLIGHT_BOUND_MINUTES = 25;

/** How often a running preflight says what it waits on. */
export const SAY_EVERY_MS = 60_000;

/** A gate's result as the run answers it. */
type Result = { gate: { name: string }; verdict: string; ms?: number };

/** The machine's admission as the gates' run takes it: a heavy gate asks its place as `<gate> gate`, and stops asking once `halted`. */
type Admission = { take: (options: { what: string; halted?: () => boolean }) => Promise<unknown> };

/** The clock's two waits, each answering how it is cleared. */
export type Timers = { every: (fn: () => void, ms: number) => () => void; after: (fn: () => void, ms: number) => () => void };

const REAL_TIMERS: Timers = {
  every: (fn, ms) => {
    const id = setInterval(fn, ms);
    id.unref?.();
    return () => clearInterval(id);
  },
  after: (fn, ms) => {
    const id = setTimeout(fn, ms);
    id.unref?.();
    return () => clearTimeout(id);
  },
};

type At = 'queued' | 'admission' | 'starting' | 'running' | 'done';

const minutes = (ms: number): string => `${Math.max(0, Math.round(ms / 60_000))} min`;
const names = (entries: { name: string }[]): string => entries.map((entry) => entry.name).join(', ');

/** The plan said before the run: the gates it runs and keeps, each it defers and why, and its bound. */
export function planLines(set: ScopedSet, boundMinutes: number): string[] {
  return [
    `the merge's fast plan: ${names(set.run) || 'no gate'}${set.kept.length ? `; kept, their pass standing on this tree: ${names(set.kept)}` : ''}`,
    ...set.deferred.map((entry) => `  deferred: ${entry.name}, ${entry.why}`),
    `  its bound: ${boundMinutes} min (--bound <minutes>); each minute it says what it waits on, and at the bound it ends what it started and says what ran`,
  ];
}

/**
 * A preflight's run of `gates` within its bound. `hooks(admission)` answers what the gates' run is handed: the step trees the bound
 * ends, `halt`, the start and the result it hears, and the admission watched, so each gate's wait is known. `start()` sets the
 * bound and the line each minute; `stop()` clears both; `summary(results)` says the outcome, with the code to exit with. `holders()`
 * says who holds the machine's places now, or null where nothing is shared.
 */
export function preflightRun({
  branch, gates, set, boundMinutes = PREFLIGHT_BOUND_MINUTES, started = Date.now(), now = Date.now, say = console.log,
  holders = () => null, timers = REAL_TIMERS, trees = stepTrees(),
}: {
  branch: string; gates: string[]; set: ScopedSet; boundMinutes?: number; started?: number; now?: () => number; say?: (line: string) => void;
  holders?: () => string | null; timers?: Timers; trees?: StepTrees;
}) {
  const states = new Map<string, { at: At; since: number }>(gates.map((name) => [name, { at: 'queued', since: now() }]));
  const waited = new Map<string, number>();
  const mark = (name: string, at: At) => {
    const before = states.get(name);
    if (!before) return;
    if (before.at === 'admission' || before.at === 'starting') waited.set(name, (waited.get(name) ?? 0) + now() - before.since);
    states.set(name, { at, since: now() });
  };
  const at = (wanted: At) => [...states].filter(([, state]) => state.at === wanted);
  const spans = (wanted: At) => at(wanted).map(([name, state]) => `${name} (${minutes(now() - state.since)})`).join(', ');
  const held = () => {
    const said = holders();
    return said ? `; ${said}` : '';
  };
  let bounded = false;
  let clear: Array<() => void> = [];

  /** What the run waits on now, as one line. */
  const line = (): string => {
    const parts = [
      at('running').length ? `running ${spans('running')}` : '',
      at('admission').length ? `waiting for the machine's heavy-run admission: ${spans('admission')}${held()}` : '',
      at('starting').length ? `admitted, waiting for free memory: ${spans('starting')}` : '',
      at('queued').length ? `waiting their turn: ${at('queued').map(([name]) => name).join(', ')}` : '',
    ].filter(Boolean);
    // Every gate has had its run: what is left is a gate run again alone (once the machine is free) and the brief.
    const said = parts.length ? parts.join('; ') : 'every gate has run once; finishing (a gate run again alone, once the machine is free, or the brief)';
    return `  preflight, ${minutes(now() - started)} of its ${boundMinutes} min bound: ${said}`;
  };

  const reach = () => {
    bounded = true;
    const running = at('running').map(([name]) => name);
    say(`  preflight: its bound of ${boundMinutes} min is reached: ${running.length ? `ending ${running.join(', ')}, and ` : ''}starting nothing more`);
    trees.end(`the preflight's bound of ${boundMinutes} min`);
  };

  return {
    trees,
    line,
    waits: (): Record<string, number> => Object.fromEntries(waited),
    hooks(admission: Admission | null) {
      const watched = admission && {
        ...admission,
        take: async (options: { what: string; halted?: () => boolean }) => {
          const name = options.what.replace(/ gate$/, '');
          mark(name, 'admission');
          const hold = await admission.take(options);
          // A wait the bound ended leaves the gate waiting for its place, which the summary says.
          if (hold || !options.halted?.()) mark(name, 'starting');
          return hold;
        },
      };
      return {
        trees, halt: () => Boolean(trees.ended), admission: watched,
        onStart: (gate: { name: string }) => mark(gate.name, 'running'),
        onResult: (result: Result) => mark(result.gate.name, 'done'),
      };
    },
    start() {
      clear = [
        timers.after(reach, Math.max(0, boundMinutes * 60_000 - (now() - started))),
        timers.every(() => {
          if (!bounded) say(line());
        }, SAY_EVERY_MS),
      ];
    },
    stop() {
      for (const each of clear) each();
      clear = [];
    },
    summary(results: Result[]): { lines: string[]; code: number; failed: Result[] } {
      const by = new Map(results.map((result) => [result.gate.name, result]));
      const passed = results.filter((result) => result.verdict === 'PASS' || result.verdict === 'FLAKE');
      const ended = results.filter((result) => result.verdict === 'ENDED');
      const failed = results.filter((result) => !['PASS', 'FLAKE', 'ENDED'].includes(result.verdict));
      const never = gates.filter((name) => !by.has(name));
      const took = minutes(now() - started);
      const deferred = set.deferred.length ? [`  deferred to the CI run on main, as the merge's fast tier defers them: ${names(set.deferred)}`] : [];
      const failedNames = failed.map((result) => result.gate.name).join(', ');
      if (!bounded && !ended.length && !never.length) {
        if (!failed.length) return { code: 0, failed, lines: [`merge-branch: the preflight of ${branch} passed in ${took}: ${passed.length} of its merge's fast plan ran green here.`, ...deferred] };
        return {
          code: 1, failed,
          lines: [`merge-branch: the preflight of ${branch} failed ${failed.length} of ${results.length} gates in ${took}: ${failedNames}. Fix the branch's reds and run it again; main's own reds are main's.`, ...deferred],
        };
      }
      const behind = ended.map((result) => result.gate.name);
      const why = (name: string): string => {
        const state = states.get(name)?.at;
        if (state === 'admission') return `waiting for the machine's heavy-run admission${held()}`;
        if (state === 'starting') return 'admitted, waiting for free memory';
        return `waiting its turn${behind.length ? ` behind ${behind.join(', ')}` : ''}`;
      };
      const lines = [
        `merge-branch: the preflight of ${branch} ${bounded ? `stopped at its bound of ${boundMinutes} min` : `ended in ${took}`}, unfinished and no pass: `
          + `${passed.length} passed, ${failed.length} failed, ${ended.length} stopped while running, ${never.length} never started`,
      ];
      if (passed.length) lines.push(`  passed: ${passed.map((result) => result.gate.name).join(', ')}`);
      if (failed.length) lines.push(`  failed: ${failedNames}`);
      if (ended.length) lines.push(`  stopped while running: ${ended.map((result) => `${result.gate.name} (${minutes(result.ms ?? 0)} in)`).join(', ')}`);
      if (never.length) lines.push(`  never started: ${never.map((name) => `${name}, ${why(name)}`).join('; ')}`);
      lines.push(...deferred, `  ${failed.length ? "Fix the branch's reds in the brief; then " : ''}run it again with a longer --bound, or when the machine is freer, for the gates it did not finish.`);
      return { code: 1, failed, lines };
    },
  };
}
