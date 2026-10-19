import { describe, expect, it, vi } from 'vitest';

// The quests and the asks as the shell pushes them (PUSH1d3, D182): a partition per workspace keyed by record, composed into the widest
// list each route answers (every repository, closed included, every workspace), from which each view's list is read.

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke: vi.fn() }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import { type Ask, fileKept, type Quest } from '../../data/api';
import { ASKS_SLICE, asksIn, keys, QUESTS_SLICE, questsIn } from '../../data/queries';
import { asksSlice, questsSlice } from './quests';
import { mergeKeyed } from '../state/slices';

const quest = (id: string, extra: Partial<Quest> = {}): Quest => ({
  id, from: 'person', to: 'engine', title: `quest ${id}`, body: 'the body', status: 'Open', filed: '2026-10-19T08:00:00+00:00',
  updated: '2026-10-19T09:00:00+00:00', workspace: 'default', ...extra,
});

const ask = (id: string, extra: Partial<Ask> = {}): Ask => ({
  id, workspace: 'default', sentence: `ask ${id}`, state: 'Published', tier: 'declarations', asked: '2026-10-19T08:00:00+00:00',
  updated: '2026-10-19T09:00:00+00:00', links: [], attachments: [], proposal: [], quests: [], ...extra,
});

const keyed = <T extends { id: string }>(...records: T[]) => Object.fromEntries(records.map((each) => [each.id, each]));

describe('the quests, pushed (PUSH1d3)', () => {
  it('feeds the widest entry the pull answers, read from the one snapshot route, its deltas merged by key', () => {
    expect(questsSlice.name).toBe(QUESTS_SLICE);
    expect(questsSlice.key).toEqual(keys.quests(null, true, null));
    expect(questsSlice.merge).toBe(mergeKeyed);
  });

  it("composes every workspace's quests in the service's order: open, then taken, then the rest, each by when it was filed", () => {
    const composed = questsSlice.compose(new Map<string, unknown>([
      ['games', keyed(
        quest('g-done', { workspace: 'games', status: 'Done', filed: '2026-10-19T01:00:00+00:00' }),
        // 09:30 at +02:00 is 07:30, before 08:00 though later as a string.
        quest('g-open', { workspace: 'games', filed: '2026-10-19T09:30:00+02:00' }),
      )],
      ['default', keyed(
        quest('d-taken', { status: 'Taken', filed: '2026-10-19T02:00:00+00:00' }),
        quest('d-open', { filed: '2026-10-19T08:00:00+00:00' }),
        quest('d-declined', { status: 'Declined', filed: '2026-10-19T00:30:00+00:00' }),
      )],
    ]), true) as Quest[];

    expect(composed.map((each) => each.id)).toEqual(['g-open', 'd-open', 'd-taken', 'd-declined', 'g-done']);
  });

  it('composes nothing before the owner judged, and an empty list once it judged none', () => {
    expect(questsSlice.compose(new Map(), false)).toBeUndefined();
    expect(questsSlice.compose(new Map(), true)).toEqual([]);
    expect(questsSlice.compose(new Map<string, unknown>([['default', 'x'], ['games', keyed(quest('g1'))]]), true)).toEqual([quest('g1')]);
  });

  it("reads each view's list from the widest as the service filters it: the receiver, the workspace, the outstanding", () => {
    const all = [
      quest('open-engine'),
      quest('done-engine', { status: 'Done' }),
      quest('held-engine', { status: 'Done', held: true }),
      quest('taken-site', { to: 'site', status: 'Taken', workspace: 'Games' }),
      quest('upper', { to: 'Engine' }),
    ];

    expect(questsIn(all, null, true, null)).toBe(all);
    // The receiver named exactly, as the service's column compares it.
    expect(questsIn(all, 'engine', true, null).map((each) => each.id)).toEqual(['open-engine', 'done-engine', 'held-engine']);
    // Outstanding: open, taken, or a done a departure holds for the person's yes.
    expect(questsIn(all, null, false, null).map((each) => each.id)).toEqual(['open-engine', 'held-engine', 'taken-site', 'upper']);
    expect(questsIn(all, null, true, ' games').map((each) => each.id)).toEqual(['taken-site']);
    expect(questsIn(all, 'engine', false, 'DEFAULT').map((each) => each.id)).toEqual(['open-engine', 'held-engine']);
  });

  it('reads a file as kept here from a path pulled or `kept` pushed, and never from neither', () => {
    const file = { name: 'trace.log', sha256: 'a'.repeat(64), bytes: 10 };
    expect(fileKept({ ...file, path: 'X:/kept/trace.log' })).toBe(true);
    expect(fileKept({ ...file, kept: true })).toBe(true);
    expect(fileKept({ ...file, kept: false })).toBe(false);
    expect(fileKept(file)).toBe(false);
  });
});

describe('the asks, pushed (PUSH1d3)', () => {
  it('feeds the widest entry the pull answers, read from the one snapshot route, its deltas merged by key', () => {
    expect(asksSlice.name).toBe(ASKS_SLICE);
    expect(asksSlice.key).toEqual(keys.asks(true, null));
    expect(asksSlice.merge).toBe(mergeKeyed);
  });

  it("composes every workspace's asks in the service's order: newest asked first", () => {
    const composed = asksSlice.compose(new Map<string, unknown>([
      ['games', keyed(ask('g-old', { workspace: 'games', asked: '2026-10-18T08:00:00+00:00' }))],
      ['default', keyed(
        ask('d-mid', { asked: '2026-10-19T08:00:00+00:00' }),
        // 07:00 at -02:00 is 09:00, the newest, though the oldest as a string.
        ask('d-new', { asked: '2026-10-19T07:00:00-02:00' }),
        ask('d-ten', { asked: '2026-10-19T08:30:00+00:00' }),
      )],
    ]), true) as Ask[];

    expect(composed.map((each) => each.id)).toEqual(['d-new', 'd-ten', 'd-mid', 'g-old']);
  });

  it('composes nothing before the owner judged, and an empty list once it judged none', () => {
    expect(asksSlice.compose(new Map(), false)).toBeUndefined();
    expect(asksSlice.compose(new Map(), true)).toEqual([]);
  });

  it("reads each view's list from the widest as the service filters it: the workspace, and neither a closed nor a done one unless asked", () => {
    const all = [ask('published'), ask('open', { state: 'Open', workspace: 'Games' }), ask('done', { state: 'Done' }), ask('closed', { state: 'Closed' })];

    expect(asksIn(all, true, null)).toBe(all);
    expect(asksIn(all, false, null).map((each) => each.id)).toEqual(['published', 'open']);
    expect(asksIn(all, true, 'games ').map((each) => each.id)).toEqual(['open']);
    expect(asksIn(all, false, 'Default').map((each) => each.id)).toEqual(['published']);
  });
});
