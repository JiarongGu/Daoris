import { describe, expect, it } from 'vitest';
import { arrive, giveUp, handOver, look, release, type Stack, type Tier } from './heavy-stack.ts';

const tier = (id: string, commit: string, base: string, since: string, pid: number): Tier => ({ id, pid, commit, base, branches: [`branch-${id}`], since, beat: since });
const A = tier('heavy-a', 'c1', 'c0', '2026-11-18T10:00:00.000Z', 1);
const B = tier('heavy-b', 'c2', 'c1', '2026-11-18T10:05:00.000Z', 2);
const C = tier('heavy-c', 'c3', 'c2', '2026-11-18T10:06:00.000Z', 3);
const EMPTY: Stack = { holder: null, waiting: null, handed: null };
const all = () => true;
const twoLanded = () => arrive(arrive(EMPTY, A, all).stack, B, all).stack;

describe('the heavy stack: one tier holds the heavy tree, a newer one supersedes it', () => {
  it('lets the first tier hold, and asks the holder to stop starting classes when a newer landing arrives', () => {
    const first = arrive(EMPTY, A, all);
    expect(first.role).toBe('hold');
    expect(first.took).toBeNull();
    const second = arrive(first.stack, B, all);
    expect(second.role).toBe('wait');
    expect(second.halt?.id).toBe('heavy-a');
    expect(second.stack.holder).toMatchObject({ id: 'heavy-a', supersededBy: 'heavy-b' });
    expect(second.stack.waiting?.id).toBe('heavy-b');
  });

  it('hands the older tier its unrun classes with its range, so the newer runs the union from the older base, once', () => {
    let stack = twoLanded();
    expect(look(stack, B, all, B.since).role).toBe('wait');
    const handed = handOver(stack, A, { unrun: ['driver-git|G1'], from: 'c0', took: null });
    expect(handed.to?.id).toBe('heavy-b');
    stack = handed.stack;
    expect(stack.holder).toBeNull();
    const taken = look(stack, B, all, B.since);
    expect(taken.role).toBe('hold');
    expect(taken.took).toEqual({ base: 'c0', from: [expect.objectContaining({ id: 'heavy-a', commit: 'c1', base: 'c0', unrun: ['driver-git|G1'] })] });
    expect(taken.stack.handed).toBeNull();
    expect(taken.stack.holder?.id).toBe('heavy-b');
  });

  it('supersedes a waiting tier with a third, which carries both ranges from the oldest base', () => {
    const third = arrive(twoLanded(), C, all);
    expect(third.superseded?.id).toBe('heavy-b');
    expect(third.stack.waiting?.id).toBe('heavy-c');
    expect(third.stack.holder?.supersededBy).toBe('heavy-c');
    expect(look(third.stack, B, all, B.since)).toMatchObject({ role: 'superseded', by: { id: 'heavy-c' } });
    const stack = handOver(third.stack, A, { unrun: [], from: 'c0', took: null }).stack;
    const taken = look(stack, C, all, C.since);
    expect(taken.role).toBe('hold');
    expect(taken.took?.base).toBe('c0');
    expect(taken.took?.from.map((each) => each.id)).toEqual(['heavy-a', 'heavy-b']);
    expect(taken.took?.from[1]?.unrun).toBeNull();
  });

  it('carries on what the holder carried when it hands over in turn', () => {
    const carried = { base: 'c0', from: [{ id: 'heavy-a', commit: 'c1', base: 'c0', branches: ['branch-heavy-a'], since: A.since, unrun: ['driver-git|G1'] }] };
    const held = arrive(EMPTY, B, all).stack;
    const stack = handOver(arrive(held, C, all).stack, B, { unrun: ['driver-git|G1', 'driver-git|G2'], from: 'c0', took: carried }).stack;
    const taken = look(stack, C, all, C.since);
    expect(taken.took?.base).toBe('c0');
    expect(taken.took?.from.map((each) => each.id)).toEqual(['heavy-a', 'heavy-b']);
  });

  it('carries the range of a holder whose process ended without handing over', () => {
    const held = arrive(EMPTY, A, all).stack;
    const next = arrive(held, B, (each) => each.id !== 'heavy-a');
    expect(next.role).toBe('hold');
    expect(next.dead.map((each) => each.id)).toEqual(['heavy-a']);
    expect(next.took).toEqual({ base: 'c0', from: [expect.objectContaining({ id: 'heavy-a', unrun: null })] });
  });

  it('lets the newer tier take the tree with nothing carried where the older one finished every class', () => {
    const taken = look(release(twoLanded(), A), B, all, B.since);
    expect(taken.role).toBe('hold');
    expect(taken.took).toBeNull();
  });

  it('carries the range of a tier that gave up waiting to the next tier, from the oldest base', () => {
    const stack = giveUp(twoLanded(), B);
    expect(stack.waiting).toBeNull();
    const next = handOver(stack, A, { unrun: ['driver-git|G1'], from: 'c0', took: null }).stack;
    expect(next.handed?.base).toBe('c0');
    expect(next.handed?.from.map((each) => each.id)).toEqual(['heavy-a', 'heavy-b']);
  });
});
