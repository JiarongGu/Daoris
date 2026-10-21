import { describe, expect, it } from 'vitest';
import { linesSlice, sweepSlice, workSlice } from './lines';
import { workOf, type MilestoneUnit, type WorkState } from '../../work/head/milestone';

// The lines slice's page half (PUSH1b2, D182): one partition per repository, composed into the eight arrays `LINES` answers.

const part = (repository: string, branch: string | null, extra: Record<string, unknown> = {}) => ({
  lines: [{ repository, workspace: 'default', branch, source: branch ? 'repository' : 'none', ...extra }],
  landings: [{ repository, workspace: 'default' }],
  languages: [], reviews: [], opinions: [], helpers: [], commits: [],
  access: [{ repository, workspace: 'default', full: false, pauses: [{ pause: 'permissions', value: 'auto', source: 'default' }] }],
});

describe('the lines slice (PUSH1b2)', () => {
  it('is the lines entry, named as the shell names it', () => {
    expect(linesSlice.name).toBe('lines');
    expect(linesSlice.key).toEqual(['driver', 'lines']);
  });

  it("composes every repository's rows into the seven arrays, in the repositories' order", () => {
    const composed = linesSlice.compose(new Map([['zeta', part('zeta', 'main')], ['alpha', part('alpha', 'dev')]]), true) as
      { lines: { repository: string }[]; landings: { repository: string }[]; commits: unknown[] };
    expect(composed.lines.map((line) => line.repository)).toEqual(['alpha', 'zeta']);
    expect(composed.landings.map((line) => line.repository)).toEqual(['alpha', 'zeta']);
    expect(composed.commits).toEqual([]);
  });

  it("composes each repository's access setup as the shell pushes it (AUTONOMY1d)", () => {
    const composed = linesSlice.compose(new Map([['zeta', part('zeta', 'main')], ['alpha', part('alpha', 'dev')]]), true) as
      { access: { repository: string; pauses: { pause: string; value: string; source: string }[] }[] };
    expect(composed.access.map((row) => row.repository)).toEqual(['alpha', 'zeta']);
    expect(composed.access[0]!.pauses).toEqual([{ pause: 'permissions', value: 'auto', source: 'default' }]);
  });

  it('keeps a line not read yet as the flag says, and answers nothing while no repository is judged', () => {
    const composed = linesSlice.compose(new Map([['alpha', part('alpha', null, { notReadYet: true })]]), true) as
      { lines: { notReadYet?: boolean }[] };
    expect(composed.lines[0].notReadYet).toBe(true);
    expect(linesSlice.compose(new Map(), true)).toBeUndefined();
  });

  it('answers nothing for a partition of another shape, which leaves the pull', () => {
    expect(linesSlice.compose(new Map([['alpha', 'x']]), true)).toBeUndefined();
  });
});

const unitOf = (unit: string, repository: string, extra: Record<string, unknown> = {}) =>
  ({ unit, repository, entries: [], cleanup: { state: 'pending', waits: [], gone: [] }, ...extra });
const heldOf = (unit: string) => ({ unit, node: 'n', attempt: 1, plugin: 'p', step: 's', nodeState: 'held', why: 'unknown', at: 't', said: '' });

describe('the work slice (PUSH1c2b)', () => {
  it('is the work-state entry at its widest key, named as the shell names it', () => {
    expect(workSlice.name).toBe('work');
    expect(workSlice.key).toEqual(['driver', 'work-state', null]);
  });

  it("composes the repositories' parts in name order with <no checkout> last, and <held> as the held list", () => {
    const parts = new Map<string, unknown>([
      ['<no checkout>', { units: [unitOf('q-1', 'gamma')], recovery: [], unread: ['gamma'] }],
      ['zeta', { units: [unitOf('z-1', 'zeta')], recovery: [{ unit: 'r' }], unread: [] }],
      ['alpha', { units: [unitOf('a-1', 'alpha')], recovery: [], unread: ['alpha: x'] }],
      ['<held>', { held: [heldOf('a-1')], unreadable: [{ file: 'k.json', unit: 'a-1', said: 's' }] }],
    ]);
    const composed = workSlice.compose(parts, true) as WorkState;
    expect(composed.units.map((unit) => unit.unit)).toEqual(['a-1', 'z-1', 'q-1']);
    expect(composed.unread).toEqual(['alpha: x', 'gamma']);
    expect(composed.recovery).toHaveLength(1);
    expect(composed.held).toHaveLength(1);
    expect(composed.units[0]!.held).toHaveLength(1);
    expect(composed.units[1]!.held).toEqual([]);
    // FABRIC-PERFORM1j: the node records that do not read, from the held partition.
    expect(composed.unreadable.map((each) => each.file)).toEqual(['k.json']);
  });

  it('answers nothing until judged, or for a part of another shape, which keeps the pull', () => {
    expect(workSlice.compose(new Map([['alpha', { units: [], recovery: [], unread: [] }]]), false)).toBeUndefined();
    expect(workSlice.compose(new Map([['alpha', 'x']]), true)).toBeUndefined();
    expect(workSlice.compose(new Map(), true)).toMatchObject({ units: [], held: [], unreadable: [] });
  });
});

describe("a unit's variant of the work (PUSH1c2b)", () => {
  const all = {
    units: [
      unitOf('u-1', 'alpha', { session: 'S-One', branch: 'daoris/s-one', held: [heldOf('u-1')] }),
      unitOf('u-2', 'alpha', { entries: [{ unit: 'u-2', kind: 'landing-branch', resource: 'land/x' }] }),
      unitOf('u-3', 'beta', { branch: 'topic' }),
    ] as unknown as MilestoneUnit[],
    recovery: [{ unit: 'r' }] as never[], unread: [], held: [heldOf('u-1'), heldOf('u-3')] as never[],
    // FABRIC-PERFORM1j: one record naming a unit picked, one another's, one naming none.
    unreadable: [{ file: 'a.json', unit: 'u-1', said: 'a' }, { file: 'b.json', unit: 'quest:q7', said: 'b' }, { file: 'c.json', unit: null, said: 'c' }],
  } satisfies WorkState;

  it('is the whole where no unit is named', () => {
    expect(workOf(all, null)).toBe(all);
  });

  it('picks as the driver names a unit, and holds no recovery and only the picked units held', () => {
    const ids = (name: string) => workOf(all, name).units.map((unit) => unit.unit);
    expect(ids('U-1')).toEqual(['u-1']);
    expect(ids('s-one')).toEqual(['u-1']);
    expect(ids('topic')).toEqual(['u-3']);
    expect(ids('Topic')).toEqual([]);
    expect(ids('land/x')).toEqual(['u-2']);
    const picked = workOf(all, 'u-1');
    expect(picked.recovery).toEqual([]);
    expect(picked.held.map((each) => each.unit)).toEqual(['u-1']);
    expect(workOf(all, 'nothing').held).toEqual([]);
    expect(picked.unreadable.map((each) => each.file)).toEqual(['a.json']);
    expect(workOf(all, 'quest:q7').unreadable.map((each) => each.file)).toEqual(['b.json']);
  });

  it('picks a closure graph’s ends by the unit it names, a quest’s that no unit carries too, as the driver does (FABRIC-CLOSE1j2)', () => {
    const ends = [
      { unit: 'u-1', kind: 'overridden', at: '2026-10-21T08:00:00Z' },
      { unit: 'quest:q7', kind: 'partially-closed', at: '2026-10-21T09:00:00Z' },
      { unit: 'quest:q8', kind: 'overridden', at: '2026-10-21T10:00:00Z' },
    ];
    const units = (name: string) => (workOf({ ...all, ends }, name).ends ?? []).map((each) => each.unit);
    expect(units('quest:q7')).toEqual(['quest:q7']);
    expect(units('s-one')).toEqual(['u-1']);
    expect(units('nothing')).toEqual([]);
  });
});

const sweepRow = (repository: string, branch: string, extra: Record<string, unknown> = {}) =>
  ({ repository, workspace: 'default', branch, hasTree: false, kind: 'empty', commits: 0, removable: true, ...extra });
const landedRow = (repository: string, branch: string) =>
  ({ repository, workspace: 'default', branch, kind: 'inside', where: 'main', files: [], commits: 1, removable: true });

describe("the clean-up's list slice (PUSH1c4b)", () => {
  it("is the clean-up's list entry, named as the shell names it", () => {
    expect(sweepSlice.name).toBe('sweep');
    expect(sweepSlice.key).toEqual(['driver', 'sweep']);
  });

  it("composes each repository's branches and landings' branches in the repositories' name order, as SWEEP_PLAN answers them", () => {
    const parts = new Map<string, unknown>([
      ['zeta', { branches: [sweepRow('zeta', 'daoris/z-1')], landed: [landedRow('zeta', 'land/z')] }],
      ['Beta', { branches: [], landed: [landedRow('Beta', 'land/b')] }],
      ['alpha', {
        branches: [sweepRow('alpha', 'daoris/a-1'), sweepRow('alpha', 'daoris/a-2', { kind: 'unlanded', landable: true })],
        landed: [landedRow('alpha', 'land/a')],
      }],
    ]);
    const composed = sweepSlice.compose(parts, true) as { branches: { branch: string }[]; landed: { branch: string }[] };
    // By UTF-16 unit, as the door orders its checkouts (`LinesRoutes.Checkouts`, `Ordinal`): 'Beta' before 'alpha'.
    expect(composed.branches.map((row) => row.branch)).toEqual(['daoris/a-1', 'daoris/a-2', 'daoris/z-1']);
    expect(composed.landed.map((row) => row.branch)).toEqual(['land/b', 'land/a', 'land/z']);
    expect(composed.branches[1]).toEqual(sweepRow('alpha', 'daoris/a-2', { kind: 'unlanded', landable: true }));
  });

  it('composes an empty list judged with no repository, and nothing until judged or for a part of another shape, which keeps the pull', () => {
    expect(sweepSlice.compose(new Map(), true)).toEqual({ branches: [], landed: [] });
    expect(sweepSlice.compose(new Map([['alpha', { branches: [], landed: [] }]]), false)).toBeUndefined();
    expect(sweepSlice.compose(new Map([['alpha', 'x']]), true)).toBeUndefined();
    expect(sweepSlice.compose(new Map([['alpha', [sweepRow('alpha', 'daoris/a-1')]]]), true)).toBeUndefined();
  });
});
