/**
 * The run of a reached plan's commands in lanes side by side: one builds each project the plan runs classes of once (`reached-build.ts`),
 * runs each .NET group on that build (`--no-build --no-restore`; a project whose build failed has its classes failed unrun), then each
 * end-to-end or Process group; beside it each light command (`runsBeside`) has a lane of its own, its chunks in turn. The outcome reads
 * the groups back in the plan's order. Each class its report shows passing is recorded under its key; a group that failed has its
 * failed classes run again alone once, after every lane has ended and one group at a time (so nothing beside them loads the machine), so a pass alone is a flake, and a failure is main's red or new (`reached-classify.ts`). A
 * flake is never recorded as a pass. Each build, and each suite's discovery and run, is a step on the run's clock
 * (`reached-steps.ts`). The run's counts, the projects it left built and its steps end as one line the merge reads.
 */
import { mkdirSync, readFileSync, rmSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { unitId, type PassedClasses } from './class-reuse.ts';
import { onBuilt } from './reached-build.ts';
import { classify, outcomeLine, type ClassOutcome, type FailedUnit } from './reached-classify.ts';
import { failedTestLines, passesOf, runStartOf, withReport } from './reached-reports.ts';
import { reachedCommands, type ReachedGroup, type ReachedPlan } from './reached.ts';
import { outsideTests, seconds, stepsLine, type Step } from './reached-steps.ts';
import type { Halt } from './heavy-halt.ts';
import { isHeavySuite } from './heavy-tier.ts';

/** Runs `command` with `args` in `cwd`, giving its exit code; asynchronous where a hold's beat must keep sounding while it runs. */
export type Exec = (command: string, args: string[], cwd: string) => number | null | Promise<number | null>;

export type Clock = {
  time: <T>(name: string, fn: () => T | Promise<T>, carve?: (result: T, ms: number) => { step: string; ms: number } | null) => Promise<T>;
  steps: () => Step[];
};

export type BuildStep = (projects: string[]) => Promise<{ ready: string[]; failed: ReadonlyMap<string, string> }>;

const FOLDER = '_fixtures/test-timing';

export type RunOptions = {
  root: string; plan: ReachedPlan; keys: ReadonlyMap<string, string>; store: PassedClasses; exec: Exec; say: (line: string) => void;
  mainRedOf: (unit: FailedUnit) => boolean; now?: () => string; replayed?: number; deferred?: readonly string[];
  night?: Readonly<Record<string, number>>;
  inPlace?: <T>(group: ReachedGroup, fn: () => Promise<T>) => Promise<T>;
  /** A newer heavy tier's ask to stop (`heavy-halt.ts`): no group starts after it, and a group it ended hands its classes on. */
  halt?: Halt;
  build?: BuildStep;
  clock?: Clock;
  epoch?: () => number;
  processSuites?: ReadonlySet<string>;
  record?: boolean;
};

const untimed: Clock = { time: async (_name, fn) => fn(), steps: () => [] };

/** Runs a group's command whole; the classes that passed, and every class of the group where it exited otherwise with none named. */
async function runGroup(group: ReachedGroup, name: string, ready: ReadonlySet<string>, options: RunOptions): Promise<{ passed: ReachedGroup['units']; failed: ReachedGroup['units'] }> {
  const { root, exec, say } = options;
  const [each] = reachedCommands({ groups: [group] });
  if (!each) return { passed: [], failed: [] };
  const built = group.runner === 'dotnet' && group.project !== undefined && ready.has(group.project);
  const { args, report } = withReport(built ? { ...each, args: onBuilt(each.args) } : each, name);
  mkdirSync(join(root, FOLDER), { recursive: true });
  rmSync(join(root, report), { force: true });
  say(`reached: ${each.label}: ${each.command} ${each.args.slice(0, 2).join(' ')}${built ? ' --no-build' : ''} …`);
  const epoch = options.epoch ?? Date.now;
  const started = epoch();
  const discovery = (): { step: string; ms: number } | null => {
    if (group.runner !== 'dotnet' || !existsSync(join(root, report))) return null;
    const start = runStartOf(readFileSync(join(root, report), 'utf8'));
    return start === null ? null : { step: `discovery ${group.suite}`, ms: start - started };
  };
  const status = await (options.clock ?? untimed).time(`run ${group.suite}`, () => exec(each.command, args, each.cwd), discovery);
  const passed = passesOf(group, status, join(root, report), root);
  for (const line of failedTestLines(group, join(root, report))) say(`reached: ${each.label}: ${line}`);
  const failed = status === 0 ? [] : group.units.filter((unit) => !passed.includes(unit));
  // A command that failed with every class passing failed outside a class (a build, a crash): all of them are asked again.
  return { passed, failed: status !== 0 && failed.length === 0 ? group.units : failed };
}

type Again = { group: ReachedGroup; index: number; failed: ReachedGroup['units'] };

type GroupResult = { ran: number; stillFailed: FailedUnit[]; passedAlone: string[]; handed: string[] };

/** A light suite's group (Vitest's, `node --test`'s; no end-to-end or Process suite's) needs nothing the build makes. */
export const runsBeside = (group: ReachedGroup, processSuites: ReadonlySet<string> = new Set()): boolean => group.runner !== 'dotnet' && !isHeavySuite(group.suite, processSuites);

export async function runPlan(options: RunOptions): Promise<ClassOutcome> {
  const { plan, keys, store, say, mainRedOf } = options;
  const now = options.now ?? (() => new Date().toISOString());
  const place = options.inPlace ?? (<T>(_group: ReachedGroup, fn: () => Promise<T>) => fn());
  const projects = [...new Set(plan.groups.flatMap((group) => (group.runner === 'dotnet' && group.project !== undefined ? [group.project] : [])))];
  const { halt } = options;
  let builds: { ready: string[]; failed: ReadonlyMap<string, string> } = { ready: [], failed: new Map() };
  let ready = new Set<string>();
  const again: Again[] = [];
  const results: GroupResult[] = plan.groups.map(() => ({ ran: 0, stillFailed: [], passedAlone: [], handed: [] }));
  const runOne = async (group: ReachedGroup, index: number): Promise<void> => {
    const result = results[index]!;
    // The classes a newer heavy tier's halt leaves to it: those of the groups not started, and of a group whose command it ended.
    const hand = (units: readonly { suite: string; unit: string }[]): void => { result.handed.push(...units.map(unitId)); };
    if (halt?.asked()) {
      hand(group.units);
      return;
    }
    const unbuilt = group.project === undefined ? undefined : builds.failed.get(group.project);
    if (group.runner === 'dotnet' && unbuilt !== undefined) {
      result.ran += group.units.length;
      say(`reached: ${group.suite}: ${group.units.length} class(es) not run: ${group.project} ${unbuilt}`);
      result.stillFailed.push(...group.units.map((unit) => ({ suite: unit.suite, unit: unit.unit })));
      return;
    }
    await place(group, async () => {
      if (halt?.asked()) {
        hand(group.units);
        return;
      }
      const first = await runGroup(group, `reached-${group.suite}-${index + 1}`, ready, options);
      for (const unit of first.passed) store.add(unitId(unit), keys.get(unitId(unit)) as string, now());
      if (first.passed.length && options.record !== false) store.save();
      if (halt?.ended(group)) {
        result.ran += first.passed.length;
        hand(group.units.filter((unit) => !first.passed.includes(unit)));
        return;
      }
      result.ran += group.units.length;
      if (first.failed.length) again.push({ group, index, failed: first.failed });
    });
  };
  /** The classes a group's first run failed run again alone: after every build and run of the plan has ended, one group at a time, so nothing beside them shares their machine. */
  const runAgain = async ({ group, index, failed }: Again): Promise<void> => {
    const result = results[index]!;
    const hand = (units: readonly { suite: string; unit: string }[]): void => { result.handed.push(...units.map(unitId)); };
    await place(group, async () => {
      if (halt?.asked()) {
        result.ran -= failed.length;
        hand(failed);
        return;
      }
      say(`reached: ${group.suite}: ${failed.length} failed, run again alone once, after every other run of this check has ended: ${failed.map((unit) => unit.unit).join(', ')}`);
      const second = await runGroup({ ...group, units: failed }, `reached-${group.suite}-${index + 1}-again`, ready, options);
      const cut = halt?.ended(group) === true;
      for (const unit of failed) {
        if (second.passed.includes(unit) && !second.failed.includes(unit)) result.passedAlone.push(unitId(unit));
        else if (cut) {
          result.ran -= 1;
          hand([unit]);
        } else result.stillFailed.push({ suite: unit.suite, unit: unit.unit });
      }
    });
  };
  const inTurn = async (entries: [number, ReachedGroup][]): Promise<void> => {
    for (const [index, group] of entries) await runOne(group, index);
  };
  const entries = [...plan.groups.entries()];
  const beside = (group: ReachedGroup): boolean => runsBeside(group, options.processSuites);
  const laneOf = (group: ReachedGroup): string => `${group.suite}|${group.runner}|${group.cwd ?? ''}`;
  const besideLanes = [...new Set(entries.filter(([, group]) => beside(group)).map(([, group]) => laneOf(group)))];
  await Promise.all([
    (async () => {
      // A run a newer heavy tier asked to stop before its build builds nothing: every group is handed on.
      if (options.build && projects.length && !halt?.asked()) builds = await options.build(projects);
      ready = new Set(builds.ready);
      await inTurn(entries.filter(([, group]) => !beside(group)));
    })(),
    ...besideLanes.map((lane) => inTurn(entries.filter(([, group]) => beside(group) && laneOf(group) === lane))),
  ]);
  for (const each of [...again].sort((a, b) => a.index - b.index)) await runAgain(each);
  const stillFailed = results.flatMap((result) => result.stillFailed);
  const passedAlone = new Set(results.flatMap((result) => result.passedAlone));
  const handed = results.flatMap((result) => result.handed);
  const ran = results.reduce((sum, result) => sum + result.ran, 0);
  const split = classify({ failed: [...aloneUnits(plan, passedAlone), ...stillFailed], passedAlone, mainRedOf });
  const outcome: ClassOutcome = { ran, replayed: options.replayed ?? 0, ...split, deferred: [...(options.deferred ?? [])] };
  if (handed.length) {
    outcome.handed = handed;
    say(`reached: ${handed.length} class(es) handed to the newer heavy tier, which runs them on its commit: ${handed.join(', ')}`);
  }
  const unrun = [...Object.values(plan.left), ...Object.values(options.night ?? {})].reduce((sum, count) => sum + count, 0);
  if (unrun > 0) outcome.unrun = unrun;
  if (ready.size) outcome.built = [...ready];
  const steps = (options.clock ?? untimed).steps();
  if (steps.length) outcome.steps = steps;
  for (const id of outcome.flaked) say(`reached: FLAKE: ${id} failed, then passed alone; it is not recorded as a pass`);
  for (const id of outcome.mainRed) say(`reached: main's red: ${id} fails here and main already stands it red`);
  for (const id of outcome.red) say(`reached: RED: ${id} failed again alone`);
  if (steps.length) say(`reached: steps: ${stepsLine(steps)}; ${seconds(outsideTests(steps))} outside the tests`);
  say(outcomeLine(outcome));
  return outcome;
}

/** The units of the plan whose ids passed alone, as failed units for the classification. */
function aloneUnits(plan: ReachedPlan, passedAlone: ReadonlySet<string>): FailedUnit[] {
  return plan.groups.flatMap((group) => group.units).filter((unit) => passedAlone.has(unitId(unit))).map((unit) => ({ suite: unit.suite, unit: unit.unit }));
}
