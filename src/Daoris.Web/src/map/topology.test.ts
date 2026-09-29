import { describe, expect, it } from 'vitest';
import type { Convergence, Quest, Registration, Session } from '../api';
import { fitRadius, frameMap, placeLabel } from './MapCanvas';
import { textWidth } from './measure';
import { buildTopology, keepQuests, LINE_KINDS, type LineKind, layoutRing, showLines } from './topology';

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

  /** A repository outside this circle is not a node, and it is still counted. An ask is its own line (MAP4b). */
  it('counts quests with an end off the map rather than inventing a node for it', () => {
    const map = buildTopology([repo('engine')], [
      quest('q1', 'ask #abc', 'engine'),
      quest('q2', 'elsewhere', 'engine', 'Done'),
      quest('q3', 'engine', 'engine'),
    ], [], []);

    expect(map.quests).toEqual([]);
    expect(map.outside).toBe(1);
    // What is still open to a repository is its own count, wherever the quest came from.
    expect(map.nodes[0]!.open).toBe(2);
  });

  it('draws what the asks became: one line from the asks to each repository they put work on', () => {
    const map = buildTopology([repo('engine'), repo('game')], [
      quest('q1', 'ask #abc', 'engine'),
      quest('q2', 'ask #abc', 'game', 'Done'),
      quest('q3', 'ask #def', 'engine', 'Done'),
      quest('q4', 'game', 'engine'),
    ], [], []);

    expect(map.asks.map((e) => [e.to, e.asks, e.quests.map((q) => q.id), e.open])).toEqual([
      ['engine', ['abc', 'def'], ['q1', 'q3'], 1],
      ['game', ['abc'], ['q2'], 0],
    ]);
    // A repository's own quest is still a quest line, and an ask's is not one.
    expect(map.quests.map((e) => [e.from, e.to])).toEqual([['game', 'engine']]);
  });

  it('draws a chain from the repository whose done quest published a step to the step\'s repository', () => {
    const first: Quest = { ...quest('q1', 'ask #abc', 'engine', 'Done') };
    const second: Quest = { ...quest('q2', 'ask #abc', 'game'), parent: 'q1', then: [{ to: 'docs', title: 'write it up', body: '' }] };
    const map = buildTopology([repo('engine'), repo('game'), repo('docs')], [first, second], [], []);

    // The step that happened, and the step still to come from the quest open now.
    expect(map.chains.map((e) => [e.from, e.to, e.steps.map((q) => q.id), e.waiting.map((s) => s.title)])).toEqual([
      ['engine', 'game', ['q2'], []],
      ['game', 'docs', [], ['write it up']],
    ]);
  });

  it('draws no step still to come from a quest that closed, since its close published it or ended the chain', () => {
    const declined: Quest = { ...quest('q1', 'game', 'engine', 'Declined'), then: [{ to: 'docs', title: 'never', body: '' }] };
    const map = buildTopology([repo('engine'), repo('game'), repo('docs')], [declined], [], []);
    expect(map.chains).toEqual([]);
  });

  it('draws only the quests a person keeps, and still reads a kept step\'s closed parent', () => {
    const done: Quest = { ...quest('q1', 'game', 'engine', 'Done') };
    const step: Quest = { ...quest('q2', 'game', 'docs'), parent: 'q1' };
    const map = buildTopology([repo('engine'), repo('game'), repo('docs')], [
      done, step, quest('q3', 'ask #abc', 'engine', 'Done'), quest('q4', 'elsewhere', 'game', 'Done'),
    ], [], [], keepQuests('open', Date.parse('2026-09-30T00:00:00Z')));

    expect(map.quests.map((e) => [e.from, e.to])).toEqual([['game', 'docs']]);
    expect(map.asks).toEqual([]);
    expect(map.outside).toBe(0);
    // The step is open, and its done parent still says where the chain came from.
    expect(map.chains.map((e) => [e.from, e.to, e.steps.map((q) => q.id)])).toEqual([['engine', 'docs', ['q2']]]);
    // A repository's open count is what is open to it now, whatever the map keeps.
    expect(map.nodes.find((n) => n.id === 'docs')!.open).toBe(1);
  });

  it('keeps what moved within a window, by when each quest last moved', () => {
    const now = Date.parse('2026-09-30T00:00:00Z');
    const at = (id: string, updated: string, status: Quest['status'] = 'Done'): Quest => ({ ...quest(id, 'game', 'engine', status), updated });
    const quests = [at('recent', '2026-09-27T00:00:00Z'), at('old', '2026-08-01T00:00:00Z'), at('lastMonth', '2026-09-05T00:00:00Z')];
    const ids = (when: Parameters<typeof keepQuests>[0]) =>
      buildTopology([repo('engine'), repo('game')], quests, [], [], keepQuests(when, now)).quests.flatMap((e) => e.quests.map((q) => q.id));

    expect(ids('week')).toEqual(['recent']);
    expect(ids('month')).toEqual(['recent', 'lastMonth']);
    expect(ids('all')).toEqual(['recent', 'old', 'lastMonth']);
  });

  it('shows only the kinds of line a person chose, and every repository still', () => {
    const map = buildTopology([repo('engine'), repo('game')], [
      quest('q1', 'ask #abc', 'engine'),
      quest('q2', 'game', 'engine'),
    ], [group(['engine', 'game'])], []);

    const shown = showLines(map, new Set<LineKind>(['asks']));
    expect(shown.asks).toHaveLength(1);
    expect(shown.quests).toEqual([]);
    expect(shown.knowledge).toEqual([]);
    expect(shown.nodes).toHaveLength(2);
    expect(showLines(map, new Set(LINE_KINDS))).toEqual(map);
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

  /**
   * UX5 U49: scoped to every workspace, a circle's repositories sit together on the ring, so its
   * quests stay within its arc, where by name alone the circles interleaved with nothing saying which.
   */
  it('keeps each circle together when it spans more than one, and counts them', () => {
    const one = buildTopology([repo('game', { workspace: 'default' }), repo('engine', { workspace: 'default' })], [], [], []);
    expect(one.circles).toBe(1);

    const two = buildTopology([
      repo('b-tools', { workspace: 'studio' }), repo('game', { workspace: 'default' }),
      repo('a-art', { workspace: 'studio' }), repo('engine', { workspace: 'default' }),
    ], [], [], []);
    expect(two.circles).toBe(2);
    expect(two.nodes.map((n) => [n.id, n.workspace])).toEqual([
      ['engine', 'default'], ['game', 'default'], ['a-art', 'studio'], ['b-tools', 'studio'],
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

describe('the drawing\'s frame (UX5 U44)', () => {
  const words = { parked: 'waiting on you', working: 'working now' };
  const three = buildTopology([repo('engine'), repo('game'), repo('newbie')], [quest('q1', 'game', 'engine')], [], [
    session('engine', 'awaiting-person'),
  ]);

  it('estimates a name at the size it is drawn, a Chinese character a whole em', () => {
    expect(textWidth('game', 12)).toBeGreaterThanOrEqual(4 * 7.6);
    expect(textWidth('在等你', 11)).toBe(33);
  });

  /**
   * 🔴 Seen on the window: a fixed 600-unit square left the lower third of a three-node ring blank,
   * with the legend 250px under the last node.
   */
  it('holds what is drawn and no more: three nodes leave no blank third', () => {
    const at = layoutRing(three.nodes.map((n) => n.id), 600);
    const frame = frameMap(three, at, words);
    const lowest = Math.max(...Object.values(at).map((p) => p.y));

    // Nothing is drawn below the lowest node's ring and its name beside it.
    expect(frame.y + frame.height).toBeLessThan(lowest + 60);
    expect(frame.height).toBeLessThan(600 * 0.75);
    // The top node's name and its second line are inside.
    expect(frame.y).toBeLessThan(placeLabel(at['engine']!.x, at['engine']!.y).y - 10);
  });

  /** 🔴 Seen on the window: *engine-asset-pipeline* on a side node was drawn *engine-asset-p*. */
  it('holds a long name on a side node whole', () => {
    const long = buildTopology([repo('a'), repo('engine-asset-pipeline'), repo('z')], [], [], []);
    const at = layoutRing(long.nodes.map((n) => n.id), 600);
    const frame = frameMap(long, at, words);
    const place = placeLabel(at['engine-asset-pipeline']!.x, at['engine-asset-pipeline']!.y);

    expect(place.anchor).toBe('start');
    expect(frame.x + frame.width).toBeGreaterThanOrEqual(place.x + textWidth('engine-asset-pipeline', 12));
  });

  it('draws the whole ring where nothing measures the room, as the map always was', () => {
    expect(fitRadius(three, undefined, words)).toBeCloseTo(204);
  });

  /** At 500 the square shrank to 400px and every name with it, to about 8px: the ring gives way instead. */
  it('draws a smaller ring in a narrow room rather than smaller names', () => {
    const radius = fitRadius(three, 400, words);
    const frame = frameMap(three, layoutRing(three.nodes.map((n) => n.id), 600, radius), words);

    expect(radius).toBeLessThan(204);
    expect(frame.width).toBeLessThanOrEqual(400);
  });

  /**
   * UX5 U59, the owner: content follows the window. A wide, tall card draws a larger ring, up to
   * what the window's height leaves it, with names still at their size. It stayed at the old
   * square's ring, 531px in a card three times as wide.
   */
  it('grows the ring with a wide card, as far as the height the window leaves it', () => {
    const radius = fitRadius(three, 1500, words, 870);
    const frame = frameMap(three, layoutRing(three.nodes.map((n) => n.id), 600, radius), words);

    expect(radius).toBeGreaterThan(300);
    expect(frame.height).toBeLessThanOrEqual(870);
    expect(frame.width).toBeLessThanOrEqual(1500);
    // Where nothing measures the height, the ring grows no further than it always was.
    expect(fitRadius(three, 1500, words)).toBeCloseTo(204);
  });

  it('never packs nodes closer than they can be told apart, and a large family keeps its spacing', () => {
    expect(fitRadius(three, 100, words)).toBe(110);
    const many = buildTopology(Array.from({ length: 16 }, (_, i) => repo(`r${i}`)), [], [], []);
    const radius = fitRadius(many, 100, words);
    const at = layoutRing(many.nodes.map((n) => n.id), 600, radius);
    const [a, b] = [at['r0']!, at['r1']!];
    expect(Math.hypot(a.x - b.x, a.y - b.y)).toBeGreaterThanOrEqual(89.9);
  });
});

describe('the ring layout', () => {
  it('takes the radius it is given', () => {
    const at = layoutRing(['a', 'b'], 600, 120);
    expect(at['a']).toEqual({ x: 300, y: 180 });
    expect(at['b']!.y).toBeCloseTo(420);
  });

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
