/**
 * The run of a reached plan's commands, one per group: each class its report shows passing is recorded under its key; a group that
 * failed has its failed classes run again alone once, so a pass alone is a flake, and a failure is main's red or new
 * (`reached-classify.ts`). A flake is never recorded as a pass. The run's counts end as one line the merge reads.
 */
import { mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { unitId, type PassedClasses } from './class-reuse.ts';
import { classify, outcomeLine, type ClassOutcome, type FailedUnit } from './reached-classify.ts';
import { failedTestLines, passesOf, withReport } from './reached-reports.ts';
import { reachedCommands, type ReachedGroup, type ReachedPlan } from './reached.ts';

/** Runs `command` with `args` in `cwd`, giving its exit code; asynchronous where a hold's beat must keep sounding while it runs. */
export type Exec = (command: string, args: string[], cwd: string) => number | null | Promise<number | null>;

const FOLDER = '_fixtures/test-timing';

export type RunOptions = {
  root: string; plan: ReachedPlan; keys: ReadonlyMap<string, string>; store: PassedClasses; exec: Exec; say: (line: string) => void;
  mainRedOf: (unit: FailedUnit) => boolean; now?: () => string; replayed?: number; deferred?: readonly string[];
  night?: Readonly<Record<string, number>>;
  inPlace?: <T>(group: ReachedGroup, fn: () => Promise<T>) => Promise<T>;
};

/** Runs a group's command whole; the classes that passed, and every class of the group where it exited otherwise with none named. */
async function runGroup(group: ReachedGroup, name: string, { root, exec, say }: Pick<RunOptions, 'root' | 'exec' | 'say'>): Promise<{ passed: ReachedGroup['units']; failed: ReachedGroup['units'] }> {
  const [each] = reachedCommands({ groups: [group] });
  if (!each) return { passed: [], failed: [] };
  const { args, report } = withReport(each, name);
  mkdirSync(join(root, FOLDER), { recursive: true });
  rmSync(join(root, report), { force: true });
  say(`reached: ${each.label}: ${each.command} ${each.args.slice(0, 2).join(' ')} …`);
  const status = await exec(each.command, args, each.cwd);
  const passed = passesOf(group, status, join(root, report), root);
  for (const line of failedTestLines(group, join(root, report))) say(`reached: ${each.label}: ${line}`);
  const failed = status === 0 ? [] : group.units.filter((unit) => !passed.includes(unit));
  // A command that failed with every class passing failed outside a class (a build, a crash): all of them are asked again.
  return { passed, failed: status !== 0 && failed.length === 0 ? group.units : failed };
}

export async function runPlan(options: RunOptions): Promise<ClassOutcome> {
  const { plan, keys, store, say, mainRedOf } = options;
  const now = options.now ?? (() => new Date().toISOString());
  const place = options.inPlace ?? (<T>(_group: ReachedGroup, fn: () => Promise<T>) => fn());
  const stillFailed: FailedUnit[] = [];
  const passedAlone = new Set<string>();
  let ran = 0;
  for (const [index, group] of plan.groups.entries()) {
    ran += group.units.length;
    await place(group, async () => {
      const first = await runGroup(group, `reached-${group.suite}-${index + 1}`, options);
      for (const unit of first.passed) store.add(unitId(unit), keys.get(unitId(unit)) as string, now());
      if (!first.failed.length) return;
      say(`reached: ${group.suite}: ${first.failed.length} failed, run again alone once: ${first.failed.map((unit) => unit.unit).join(', ')}`);
      const again = await runGroup({ ...group, units: first.failed }, `reached-${group.suite}-${index + 1}-again`, options);
      for (const unit of first.failed) {
        if (again.passed.includes(unit) && !again.failed.includes(unit)) passedAlone.add(unitId(unit));
        else stillFailed.push({ suite: unit.suite, unit: unit.unit });
      }
    });
  }
  const split = classify({ failed: [...aloneUnits(plan, passedAlone), ...stillFailed], passedAlone, mainRedOf });
  const outcome: ClassOutcome = { ran, replayed: options.replayed ?? 0, ...split, deferred: [...(options.deferred ?? [])] };
  const unrun = [...Object.values(plan.left), ...Object.values(options.night ?? {})].reduce((sum, count) => sum + count, 0);
  if (unrun > 0) outcome.unrun = unrun;
  for (const id of outcome.flaked) say(`reached: FLAKE: ${id} failed, then passed alone; it is not recorded as a pass`);
  for (const id of outcome.mainRed) say(`reached: main's red: ${id} fails here and main already stands it red`);
  for (const id of outcome.red) say(`reached: RED: ${id} failed again alone`);
  say(outcomeLine(outcome));
  return outcome;
}

/** The units of the plan whose ids passed alone, as failed units for the classification. */
function aloneUnits(plan: ReachedPlan, passedAlone: ReadonlySet<string>): FailedUnit[] {
  return plan.groups.flatMap((group) => group.units).filter((unit) => passedAlone.has(unitId(unit))).map((unit) => ({ suite: unit.suite, unit: unit.unit }));
}
