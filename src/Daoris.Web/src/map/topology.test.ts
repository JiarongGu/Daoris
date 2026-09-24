import { describe, expect, it } from 'vitest';
import type { Convergence, Quest, Registration, Session } from '../api';
import { placeLabel } from './MapCanvas';
import { buildTopology, layoutRing } from './topology';

// MAP2 (D67 §3, `docs/2026-09-23-map-design.md` §1): the workspace's repositories and what actually
// moved between them, read from data the service already serves — no model, no machine path.

const repo = (repository: string, extra: Partial<Registration> = {}): Registration => ({
  repository, adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 0, ...extra,
});

const quest = (id: string, from: string, to: string, status: Quest['status'] = 'Open'): Quest => ({
  id, from, to, title: `quest ${id}`, body: '', status, filed: '2026-09-23T00:00:00Z', updated: '2026-09-23T00:00:00Z',
});

const session = (repository: string, state: Session['state']): Session => ({
  id: `s-${repository}-${state}`, repository, adapter: 'stub', state, created: '', updated: '',
});

const group = (repositories: string[]): Convergence => ({
  method: 'Convergent', similarity: 0.8, repositories, entries: [], suggestion: '',
});

describe('the workspace topology', () => {
  it('has a node per registered repository, ordered by name so the picture does not move', () => {
    const map = buildTopology([repo('game'), repo('engine', { summary: 'the engine' })], [], [], []);

    expect(map.nodes.map((n) => n.id)).toEqual(['engine', 'game']);
    expect(map.nodes[0]!.summary).toBe('the engine');
  });

  it('draws one quest edge per direction, counting what went that way and what is still open', () => {
    const map = buildTopology([repo('game'), repo('engine')], [
      quest('q1', 'game', 'engine'),
      quest('q2', 'game', 'engine', 'Done'),
      quest('q3', 'engine', 'game', 'Taken'),
    ], [], []);

    expect(map.quests.map((e) => [e.from, e.to, e.quests.length, e.open])).toEqual([
      ['engine', 'game', 1, 1],
      ['game', 'engine', 2, 1],
    ]);
  });

  /** An ask's sender, or a repository outside this circle, is not a node — and it is still counted. */
  it('counts quests with an end off the map rather than inventing a node for it', () => {
    const map = buildTopology([repo('engine')], [
      quest('q1', 'ask #abc', 'engine'),
      quest('q2', 'elsewhere', 'engine', 'Done'),
      quest('q3', 'engine', 'engine'),
    ], [], []);

    expect(map.quests).toEqual([]);
    expect(map.outside).toBe(2);
    // What is still open to a repository is its own count, wherever the quest came from.
    expect(map.nodes[0]!.open).toBe(2);
  });

  it('marks where a session is working now', () => {
    const map = buildTopology([repo('game'), repo('engine')], [], [], [
      session('engine', 'working'), session('game', 'completed'),
    ]);

    expect(map.nodes.map((n) => [n.id, n.working])).toEqual([['engine', true], ['game', false]]);
  });

  /**
   * 🔴 Seen on the window (POLISH4): a repository whose one session was parked, waiting on the
   * person, was ringed "working now". Parked is its own fact, and the one the person acts on.
   */
  it('marks a session parked on the person apart from one working', () => {
    const map = buildTopology([repo('game'), repo('engine')], [], [], [
      session('engine', 'awaiting-person'), session('game', 'working'),
    ]);

    expect(map.nodes.map((n) => [n.id, n.working, n.parked])).toEqual([
      ['engine', false, true], ['game', true, false],
    ]);
  });

  /** Two repositories that learned the same thing (D17): undirected, once per pair, counted. */
  it('draws a knowledge edge per pair that converged, once however the group lists them', () => {
    const map = buildTopology([repo('a'), repo('b'), repo('c')], [], [
      group(['b', 'a']), group(['a', 'b', 'c']), group(['a', 'elsewhere']),
    ], []);

    expect(map.knowledge.map((e) => [e.a, e.b, e.groups])).toEqual([
      ['a', 'b', 2], ['a', 'c', 1], ['b', 'c', 1],
    ]);
  });
});

describe('where a name goes', () => {
  /** 🔴 Seen on the window: with names always below, the arrow into the top node crossed its name. */
  it('sits on the side facing away from the centre, where no line arrives', () => {
    expect(placeLabel(300, 80)).toMatchObject({ anchor: 'middle' });
    expect(placeLabel(300, 80).y).toBeLessThan(80);
    expect(placeLabel(300, 520).y).toBeGreaterThan(520);
    expect(placeLabel(520, 300)).toMatchObject({ anchor: 'start' });
    expect(placeLabel(520, 300).x).toBeGreaterThan(520);
    expect(placeLabel(80, 300)).toMatchObject({ anchor: 'end' });
  });
});

describe('the ring layout', () => {
  it('places nothing for nothing and one node at the centre', () => {
    expect(layoutRing([], 400)).toEqual({});
    expect(layoutRing(['only'], 400)).toEqual({ only: { x: 200, y: 200 } });
  });

  it('is deterministic, starts at the top, and keeps every node inside the square', () => {
    const first = layoutRing(['a', 'b', 'c', 'd'], 400);

    expect(layoutRing(['a', 'b', 'c', 'd'], 400)).toEqual(first);
    expect(first['a']!.x).toBeCloseTo(200);
    expect(first['a']!.y).toBeLessThan(200);
    for (const { x, y } of Object.values(first)) {
      expect(x).toBeGreaterThan(0);
      expect(x).toBeLessThan(400);
      expect(y).toBeGreaterThan(0);
      expect(y).toBeLessThan(400);
    }
  });
});
