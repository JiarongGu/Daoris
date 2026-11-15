import { describe, expect, it } from 'vitest';
import { landMerge, landMessage, notReadyLines, readiness } from './land-merge.ts';
import type { HeavyRun } from './heavy-after.ts';
import { landedOf, measureLanding, type LandedCommit } from './landing-measure.ts';
import { startAfterLanding } from './heavy-after.ts';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const pass = { verdict: 'PASS' };

describe('what a merge needs before the tool commits it', () => {
  it('is ready when every chosen gate has a passing or flaked verdict no changed path reaches', () => {
    expect(readiness(['a', 'b'], { a: pass, b: { verdict: 'FLAKE' } }, {}).ready).toBe(true);
  });

  it('names each failed, missing and stale gate', () => {
    const state = readiness(['a', 'b', 'c', 'd'], { a: pass, b: { verdict: 'FAIL' }, d: pass }, { d: ['src/x.ts'] });
    expect(state).toEqual({ ready: false, lacking: ['c'], failed: ['b'], stale: ['d'] });
    const said = notReadyLines('feature', state).join('\n');
    expect(said).toContain('failed: b');
    expect(said).toContain('no verdict yet: c');
    expect(said).toContain('stale');
  });
});

describe('the landing a --commit makes', () => {
  const run = (over: { unbudgeted: boolean; prune?: boolean; measure?: (landed: LandedCommit[]) => void; measured?: LandedCommit[][] }) => {
    const said: string[] = [];
    const records: HeavyRun[] = [];
    const started: string[][] = [];
    const pruned: number[] = [];
    const messages: string[] = [];
    const landed = landMerge({ branch: 'feature', gates: 7, trailer: 'Co-Authored-By: X <x@example.com>', prune: over.prune ?? true, heavy: true, unbudgeted: over.unbudgeted }, {
      commit: (message) => { messages.push(message); return { tip: 'abcdef0123456789', base: '0123456789abcdef' }; },
      prune: () => { pruned.push(1); },
      measure: over.measure ?? ((landed) => { over.measured?.push(landed); }),
      start: (args) => { started.push(args); return 77; },
      append: (record) => records.push(record),
      now: () => '2026-11-14T10:00:00.000Z',
      log: 'local/scratch/heavy.log', say: (line) => said.push(line),
    });
    return { landed, said, records, started, pruned, messages };
  };

  it('commits a no-timeout merge, starts the heavy run on the new tip, records it running and prunes', () => {
    const { landed, said, records, started, pruned, messages } = run({ unbudgeted: true });
    expect(messages[0]).toContain('no time budget on a gate (--no-timeout)');
    expect(messages[0]).toContain('7 gates passed');
    expect(messages[0]).toMatch(/Co-Authored-By: X/);
    expect(records).toHaveLength(1);
    expect(records[0]).toMatchObject({ state: 'running', commit: 'abcdef0123456789', base: '0123456789abcdef', branches: ['feature'], id: landed.heavy });
    expect(started[0]).toEqual(expect.arrayContaining(['--commit', 'abcdef0123456789']));
    expect(pruned).toHaveLength(1);
    expect(said.join('\n')).toContain('committed feature as abcdef0');
  });

  it('records the landing once after the commit, and still lands when the measure throws', () => {
    const measured: LandedCommit[][] = [];
    run({ unbudgeted: false, measured });
    expect(measured).toEqual([[{ commit: 'abcdef0123456789', branch: 'feature' }]]);
    const failing = run({ unbudgeted: false, measure: () => { throw new Error('transcript unreadable'); } });
    expect(failing.landed.tip).toBe('abcdef0123456789');
    expect(failing.pruned).toHaveLength(1);
  });

  it('writes the queue promotion\'s members through the same function the --commit route uses', () => {
    const through: LandedCommit[] = [];
    measureLanding(landedOf({ tip: 'tip1', members: [{ branch: 'a' }, { branch: 'b' }] }), (each) => { through.push(each); });
    expect(through).toEqual([{ commit: 'tip1', branch: 'a' }, { commit: 'tip1', branch: 'b' }]);
    const home = mkdtempSync(join('_fixtures', 'landing-measure-'));
    try {
      mkdirSync(join(home, 'tools'));
      writeFileSync(join(home, 'tools', 'heavy-run.mjs'), '');
      startAfterLanding(home, { tip: 'tip1', base: 'base1', members: [{ branch: 'steward/q' }] });
      const rows = readFileSync(join(home, 'local', 'metrics', 'landings.jsonl'), 'utf8').trim().split('\n').map((line) => JSON.parse(line));
      expect(rows).toHaveLength(1);
      expect(rows[0]).toMatchObject({ schema: 1, commit: 'tip1', branch: 'steward/q', builder: null });
    } finally {
      try { rmSync(home, { recursive: true, force: true }); } catch { /* the runner just started holds the folder */ }
    }
  });

  it('says nothing of budgets for a normal run and leaves the prune off only when asked', () => {
    expect(landMessage({ branch: 'f', gates: 1, unbudgeted: false, trailer: 't' })).not.toContain('--no-timeout');
    const { pruned, said } = run({ unbudgeted: false, prune: false });
    expect(pruned).toHaveLength(0);
    expect(said.join('\n')).toContain('prune: skipped (--no-prune)');
  });
});
