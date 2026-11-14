import { mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { HEAVY_PREFIX, appendHeavy, heavyStanding, readHeavy, runnerArgs, settleHeavy, startHeavy, type HeavyRun } from './heavy-after.ts';
import type { ClassOutcome } from './reached-classify.ts';

const commit = '0123456789abcdef0123456789abcdef01234567';
const input = { commit, base: 'fedcba9876543210fedcba9876543210fedcba98', branches: ['feature-a'], log: 'local/scratch/heavy.log' };
const outcome = (over: Partial<ClassOutcome> = {}): ClassOutcome => ({ ran: 4, replayed: 2, flaked: [], mainRed: [], red: [], deferred: [], ...over });

describe('the heavy run started when a merge lands', () => {
  it('starts the runner in the background on the new tip with a stand-in, and records it running', () => {
    const started: Array<{ args: string[]; log: string }> = [];
    const records: HeavyRun[] = [];
    const { run, lines } = startHeavy(input, {
      start: (args, log) => { started.push({ args, log }); return 4242; }, append: (record) => records.push(record), now: () => '2026-11-14T10:00:00.000Z',
    });
    expect(started).toEqual([{ args: runnerArgs(run), log: input.log }]);
    expect(started[0]!.args).toEqual(expect.arrayContaining(['--commit', commit, '--base', input.base, '--branches', 'feature-a']));
    expect(records).toEqual([run]);
    expect(run).toMatchObject({ state: 'running', commit, branches: ['feature-a'] });
    expect(lines[0]).toMatch(new RegExp(`^${HEAVY_PREFIX} heavy-0123456-\\d+ started in the background on 0123456`));
  });

  it('says a runner that could not start as an error, loudly, and holds nothing', () => {
    const records: HeavyRun[] = [];
    const { run, lines } = startHeavy(input, { start: () => null, append: (record) => records.push(record), now: () => '2026-11-14T10:00:00.000Z' });
    expect(run.state).toBe('error');
    expect(records).toEqual([run]);
    expect(lines[0]).toContain('could not be started');
  });
});

describe('the heavy run settled', () => {
  const running: HeavyRun = { id: 'heavy-0123456-1', state: 'running', commit, base: input.base, branches: ['feature-a', 'feature-b'], at: '2026-11-14T10:00:00.000Z' };

  it('is not green when it ran nothing while the range reaches classes it left unrun', () => {
    const settled = settleHeavy(running, outcome({ ran: 0, replayed: 0, unrun: 40 }), 0, '2026-11-14T10:20:00.000Z');
    expect(settled.run.state).toBe('error');
    expect(settled.run.note).toContain('40');
    expect(settled.lines[0]).toMatch(/ERROR .*40 reached classes/);
    expect(settleHeavy(running, outcome({ ran: 0, replayed: 0, unrun: 0 }), 0, '2026-11-14T10:20:00.000Z').run.state).toBe('green');
  });

  it('is green with no finding where every class passed, noting what flaked', () => {
    const settled = settleHeavy(running, outcome({ flaked: ['driver-process|A.FlakyTests'] }), 0, '2026-11-14T10:20:00.000Z');
    expect(settled.run.state).toBe('green');
    expect(settled.sightings).toEqual([]);
    expect(settled.lines[0]).toContain(`${HEAVY_PREFIX} GREEN 0123456`);
    expect(settled.lines[0]).toContain('A.FlakyTests');
  });

  it('is red with one finding for each class that failed again alone, naming the merge commit and the class', () => {
    const settled = settleHeavy(running, outcome({ red: ['driver-process|A.BrokenTests'], mainRed: ['modules-process|B.OldTests'] }), 1, '2026-11-14T10:20:00.000Z');
    expect(settled.run).toMatchObject({ state: 'red', ended: '2026-11-14T10:20:00.000Z' });
    expect(settled.sightings).toHaveLength(1);
    expect(settled.sightings[0]).toMatchObject({ gate: 'driver-process', unit: 'A.BrokenTests', test: 'A.BrokenTests', commit, kind: 'heavy' });
    expect(settled.sightings[0]!.message).toContain('0123456');
    expect(settled.lines[0]).toContain(`${HEAVY_PREFIX} RED 0123456 (feature-a, feature-b)`);
    expect(settled.lines[0]).toContain('driver-process|A.BrokenTests');
  });

  it('is an error, said, where the run ended with no counts', () => {
    const settled = settleHeavy(running, null, null, '2026-11-14T10:20:00.000Z');
    expect(settled.run.state).toBe('error');
    expect(settled.sightings).toEqual([]);
    expect(settled.lines[0]).toContain(`${HEAVY_PREFIX} ERROR`);
  });
});

describe('the heavy runs record', () => {
  const dir = join('_fixtures', 'heavy-after-test');
  afterEach(() => rmSync(dir, { recursive: true, force: true }));

  it('keeps each run as its newest line, and the next merge reads a red or a running one as main\'s', () => {
    mkdirSync(dir, { recursive: true });
    const running: HeavyRun = { id: 'r1', state: 'running', commit, base: input.base, branches: ['feature-a'], at: '2026-11-14T10:00:00.000Z' };
    appendHeavy(dir, running);
    expect(heavyStanding(readHeavy(dir), Date.parse('2026-11-14T10:05:00.000Z'))[0]).toContain('running on 0123456');
    appendHeavy(dir, { ...running, state: 'red', ended: '2026-11-14T10:30:00.000Z', classes: { ran: 1, replayed: 0, flaked: [], red: ['driver-process|A.BrokenTests'], mainRed: [] } });
    expect(readHeavy(dir)).toHaveLength(1);
    expect(heavyStanding(readHeavy(dir), Date.parse('2026-11-14T11:00:00.000Z'))).toEqual([`${HEAVY_PREFIX} main is RED from 0123456 (feature-a): driver-process|A.BrokenTests`]);
    expect(heavyStanding(readHeavy(dir), Date.parse('2026-11-16T11:00:00.000Z'))).toEqual([]);
  });
});
