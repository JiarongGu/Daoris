import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { PassedClasses, unitId } from './class-reuse.ts';
import { fixtureFolder } from './fixture-home.ts';
import { createHalt, readHalt, reachedByNewer, wired, type TrackedExec } from './heavy-halt.ts';
import { outcomeLine, parseOutcome } from './reached-classify.ts';
import { runPlan } from './reached-run.ts';
import type { ReachedGroup, ReachedPlan, ReachedUnit } from './reached.ts';

const unit = (suite: string, name: string): ReachedUnit => ({ suite, kind: 'dotnet', unit: name, name, file: `${name}.cs` });
const group = (suite: string, names: string[]): ReachedGroup => ({ runner: 'dotnet', suite, settings: 'process.runsettings', units: names.map((name) => unit(suite, name)) });
/** What each class reads: the git classes read Git.cs, the Process class Proc.cs, the rehearsal Rehearse.cs. */
const READS: Record<string, string> = { 'driver-process|P1': 'Proc.cs', 'driver-git|G1': 'Git.cs', 'driver-git|G2': 'Git.cs', 'driver-rehearsals|R1': 'Rehearse.cs' };
const reaches = (each: { suite: string; unit: string }, path: string) => READS[unitId(each)] === path;
const PLAN: ReachedPlan = {
  groups: [group('driver-process', ['P1']), group('driver-git', ['G1', 'G2']), group('driver-rehearsals', ['R1'])], left: {}, reached: 4, replayed: {},
};
const keys = new Map(PLAN.groups.flatMap((each) => each.units).map((each) => [unitId(each), `key-${each.unit}`]));

/**
 * A plan run whose second command, the git half, is in flight when a newer landing asks the run to stop: `newer` is what the
 * newer commit changed. The git half ends when the halt ends its process, or passes on its own once `finish` is called.
 */
async function haltedDuringGitHalf(newer: string[]) {
  const { dir, remove } = fixtureFolder('heavy-halt');
  const file = join(dir, 'halt.json');
  const said: string[] = [];
  const ended: number[] = [];
  const ends = new Map<number, () => void>();
  let calls = 0;
  const halt = createHalt({ file, changedSince: (commit) => (commit === 'c2' ? newer : []), reaches, end: (pid) => { ended.push(pid); ends.get(pid)?.(); }, say: (line) => said.push(line) });
  const exec: TrackedExec = (_command, _args, _cwd, started) => {
    calls += 1;
    const pid = 100 + calls;
    started(pid);
    if (calls !== 2) return Promise.resolve(0);
    return new Promise((done) => {
      ends.set(pid, () => done(1));
      writeFileSync(file, JSON.stringify({ by: 'heavy-b', commit: 'c2' }));
      halt.look();
      setTimeout(() => done(0), 20);
    });
  };
  const store = new PassedClasses(join(dir, 'store.json'));
  const outcome = await runPlan({ root: dir, plan: PLAN, keys, store, say: (line) => said.push(line), mainRedOf: () => false, halt, ...wired(halt, exec) });
  const passed = PLAN.groups.flatMap((each) => each.units).map(unitId).filter((id) => store.has(id, keys.get(id) as string));
  remove();
  return { outcome, passed, ended, said, calls };
}

describe('a heavy tier asked to stop by a newer one', () => {
  it('ends the group in flight where the newer commit changes what its classes read, and hands it on with every group not started', async () => {
    const { outcome, passed, ended, said } = await haltedDuringGitHalf(['Git.cs']);
    expect(ended).toEqual([102]);
    expect(passed).toEqual(['driver-process|P1']);
    expect(outcome).toMatchObject({ ran: 1, red: [], flaked: [], handed: ['driver-git|G1', 'driver-git|G2', 'driver-rehearsals|R1'] });
    expect(parseOutcome(outcomeLine(outcome))?.handed).toEqual(outcome.handed);
    expect(said.join('\n')).toMatch(/heavy-b on c2 asks this run to stop/);
    expect(said.join('\n')).toMatch(/ends driver-git/);
  });

  it('finishes the group in flight where the newer commit changes nothing its classes read, its passes standing there', async () => {
    const { outcome, passed, ended, said, calls } = await haltedDuringGitHalf(['docs/README.md']);
    expect(ended).toEqual([]);
    expect(calls).toBe(2);
    expect(passed).toEqual(['driver-process|P1', 'driver-git|G1', 'driver-git|G2']);
    expect(outcome).toMatchObject({ ran: 3, handed: ['driver-rehearsals|R1'] });
    expect(said.join('\n')).toMatch(/finishes driver-git/);
  });

  it('ends at once a command the ended group starts after the halt, and reads a halt only as the newer tier wrote it', () => {
    const { dir, remove } = fixtureFolder('heavy-halt-read');
    try {
      const file = join(dir, 'halt.json');
      expect(readHalt(file)).toBeNull();
      writeFileSync(file, '{ not json');
      expect(readHalt(file)).toBeNull();
      writeFileSync(file, JSON.stringify({ by: 'heavy-b', commit: 'c2' }));
      expect(readHalt(file)).toEqual({ by: 'heavy-b', commit: 'c2' });
      expect(reachedByNewer(PLAN.groups[1] as ReachedGroup, ['Git.cs'], reaches)).toBe(true);
      expect(reachedByNewer(PLAN.groups[1] as ReachedGroup, ['Proc.cs'], reaches)).toBe(false);
      const ended: number[] = [];
      const halt = createHalt({ file, changedSince: () => ['Git.cs'], reaches, end: (pid) => ended.push(pid), say: () => undefined });
      const git = PLAN.groups[1] as ReachedGroup;
      halt.enter(git);
      expect(halt.asked()).toBe(true);
      expect(halt.ended(git)).toBe(false);
      halt.child(7);
      expect(halt.ended(git)).toBe(true);
      halt.child(8);
      expect(ended).toEqual([7, 8]);
      halt.leave();
    } finally {
      remove();
    }
  });
});
