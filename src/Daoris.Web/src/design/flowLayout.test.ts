import { describe, expect, it } from 'vitest';
import { APPEND_AFTER, APPEND_BEFORE, DENSE, type Fixture, HARDEST, LIFECYCLE, MANY_CURRENT, NONE_CURRENT, UNEQUAL } from './flowFixtures';
import { type FlowShape, findStacks, layoutFlow, needs, placementFor, slotCrossings } from './flowLayout';

// FLOWCHART1a1 (D186; the flowchart design §3.1, §3.2, §3.5, §7 traps 1, 2, 4 and the first half of 3): the layout is a
// pure function of the graph's shape. It finds the back edges, ranks by the longest path, threads a waypoint through each
// rank an edge skips, orders each rank by four barycentre sweeps, and says what each placement needs.

const shape = (nodes: string[], edges: [string, string, string?][], entries = [nodes[0]!]): FlowShape => ({
  nodes: nodes.map((id) => ({ id })),
  edges: edges.map(([from, to, style], index) => ({ id: `e${index}`, from, to, style: style as FlowShape['edges'][number]['style'] })),
  entries,
});

const ofFixture = (fixture: Fixture): FlowShape => ({ nodes: fixture.nodes, edges: fixture.edges, entries: fixture.entries });

/** Each rank's slots as words: a node by its id, a waypoint as `·`. */
const ranks = (shape: FlowShape) => layoutFlow(shape).ranks.map((rank) => rank.map((slot) => (slot.kind === 'node' ? slot.id : '·')));

/** The four statuses and the moves `QuestTransitions.Allows` allows (D46 §3). */
const QUEST = shape(['Open', 'Taken', 'Done', 'Declined'],
  [['Open', 'Taken'], ['Open', 'Done'], ['Open', 'Declined'], ['Taken', 'Done'], ['Taken', 'Declined']]);

describe('the back edges (trap 1)', () => {
  it('finds a reciprocal pair by the walk, ranks it as if reversed, and ends', () => {
    // working ⇄ awaiting-person: the walk reaches working first, so the move back to it is the return.
    const layout = layoutFlow(shape(['queued', 'working', 'awaiting'], [['queued', 'working'], ['working', 'awaiting'], ['awaiting', 'working']]));
    expect(layout.paths.filter((path) => path.back).map((path) => `${path.from}>${path.to}`)).toEqual(['awaiting>working']);
    expect(layout.rank).toEqual({ queued: 0, working: 1, awaiting: 2 });
  });

  it('lays the lifecycle\'s multigraph out, every return found and each on a lane of its own', () => {
    const layout = layoutFlow(ofFixture(LIFECYCLE));
    const back = layout.paths.filter((path) => path.back);
    expect(back.map((path) => `${path.from}>${path.to}`).sort()).toEqual([
      'failed>ready', 'invalidated>blocked', 'invalidated>ready', 'ready>blocked', 'rejected>ready', 'skipped>blocked',
      'stopped>ready', 'unknown>active',
    ]);
    // No two returns share a lane (§3.5), and the lanes are 0..n-1.
    expect(back.map((path) => path.lane).sort((a, b) => a! - b!)).toEqual(back.map((_, index) => index));
    expect(layout.lanes).toBe(back.length);
    // A forward path has no lane, and a return no waypoint.
    for (const path of layout.paths) expect(path.back ? path.ways : path.lane).toEqual(path.back ? [] : null);
  });

  it('puts a short return nearer the drawing than a long one, so their runs nest rather than cross', () => {
    const layout = layoutFlow(shape(['a', 'b', 'c', 'd'], [['a', 'b'], ['b', 'c'], ['c', 'd'], ['d', 'a'], ['c', 'b']]));
    const lane = (from: string, to: string) => layout.paths.find((path) => path.from === from && path.to === to)!.lane;
    expect(lane('c', 'b')).toBeLessThan(lane('d', 'a')!);
  });

  it('walks a node no entry reaches after the entries, and ranks it from nothing', () => {
    const layout = layoutFlow(shape(['a', 'b', 'lone', 'after'], [['a', 'b'], ['lone', 'after']], ['a']));
    expect(layout.rank).toEqual({ a: 0, b: 1, lone: 0, after: 1 });
    expect(layout.paths.every((path) => !path.back)).toBe(true);
  });

  it('takes a move to itself as a return, and never loops on it', () => {
    const layout = layoutFlow(shape(['a', 'b'], [['a', 'b'], ['b', 'b']]));
    expect(layout.paths.find((path) => path.from === 'b')!.back).toBe(true);
  });
});

describe('the ranks and their waypoints', () => {
  it('ranks each node by the longest path to it, and threads a waypoint through each rank an edge skips', () => {
    // Open → Done and Open → Declined skip Taken's rank: each takes a slot there, the sweeps putting each beside its end.
    expect(ranks(QUEST)).toEqual([['Open'], ['·', 'Taken', '·'], ['Done', 'Declined']]);
    const layout = layoutFlow(QUEST);
    const skip = layout.paths.find((path) => path.from === 'Open' && path.to === 'Done')!;
    expect(skip.ways).toHaveLength(1);
    expect(layout.ranks[1]!.some((slot) => slot.kind === 'way' && slot.path === skip.id)).toBe(true);
  });

  it('gives an edge that skips three ranks a waypoint in each of them', () => {
    const layout = layoutFlow(shape(['a', 'b', 'c', 'd', 'e'], [['a', 'b'], ['b', 'c'], ['c', 'd'], ['d', 'e'], ['a', 'e']]));
    expect(layout.paths.find((path) => path.from === 'a' && path.to === 'e')!.ways).toHaveLength(3);
  });

  it('reads in rank order, then each rank in its order, nodes alone', () => {
    expect(layoutFlow(QUEST).order).toEqual(['Open', 'Taken', 'Done', 'Declined']);
  });
});

describe('the order (§3.1 step 4)', () => {
  it('starts each rank in declared order and keeps it on a tie', () => {
    expect(ranks(shape(['s', 'b', 'a', 'c'], [['s', 'b'], ['s', 'a'], ['s', 'c']]))).toEqual([['s'], ['b', 'a', 'c']]);
  });

  it('orders a node appended to a rank by its neighbours, so a grown graph moves its nodes (the design\'s own example)', () => {
    expect(ranks(ofFixture(APPEND_BEFORE))).toEqual([['A', 'B'], ['X', 'Y']]);
    expect(ranks(ofFixture(APPEND_AFTER))).toEqual([['A', 'B'], ['X', 'Z', 'Y']]);
  });

  it('uncrosses what declared order crosses', () => {
    // b's target is declared first, so the first rank's order would cross both edges.
    const crossed = shape(['a', 'b', 'y', 'x'], [['a', 'x'], ['b', 'y']], ['a', 'b']);
    expect(ranks(crossed)).toEqual([['a', 'b'], ['x', 'y']]);
    expect(slotCrossings(layoutFlow(crossed))).toBe(0);
  });

  it('counts a crossing it cannot avoid: two entries each wired to both of two targets cross once in any order', () => {
    expect(slotCrossings(layoutFlow(shape(['a', 'b', 'x', 'y'], [['a', 'x'], ['a', 'y'], ['b', 'x'], ['b', 'y']], ['a', 'b'])))).toBe(1);
  });
});

describe('parallel edges (§3.4)', () => {
  it('draws those of one style as one path that carries each edge, and those of different styles apart', () => {
    const layout = layoutFlow(ofFixture(LIFECYCLE));
    const between = (from: string, to: string) => layout.paths.filter((path) => path.from === from && path.to === to);
    // unknown → reached, by its authority and by the person: one style, one path, both edges.
    expect(between('unknown', 'reached')).toHaveLength(1);
    expect(between('unknown', 'reached')[0]!.edges).toHaveLength(2);
    // A failure's dashes and a plain move between the same two nodes are separate paths.
    const mixed = layoutFlow(shape(['a', 'b'], [['a', 'b'], ['a', 'b', 'failed'], ['a', 'b']]));
    expect(mixed.paths.map((path) => [path.style, path.edges.length])).toEqual([['next', 2], ['failed', 1]]);
  });
});

describe('the same graph, the same picture (trap 2)', () => {
  it('lays each fixture out the same way twice', () => {
    for (const fixture of HARDEST) expect(layoutFlow(ofFixture(fixture)), fixture.name).toEqual(layoutFlow(ofFixture(fixture)));
  });

  it('reads only the shape: what is lit, a state, a path or a time moves nothing', () => {
    expect(layoutFlow(ofFixture(MANY_CURRENT))).toEqual(layoutFlow(ofFixture(NONE_CURRENT)));
    const lit = {
      ...QUEST,
      nodes: QUEST.nodes.map((node, index) => ({ ...node, current: index === 1, state: { word: 'taken', tone: 'taken' }, at: '2026-10-20T14:02:00Z' })),
      edges: QUEST.edges.map((edge) => ({ ...edge, path: { order: 1, at: '2026-10-20T14:02:00Z' } })),
    };
    expect(layoutFlow(lit)).toEqual(layoutFlow(QUEST));
  });
});

describe('the placement by the pane\'s width (§3.2, trap 4)', () => {
  // The worked budgets at the floors, a rem being 16 px: a node track 160, a waypoint 16, a gap 24 between tracks, a return's
  // lane 16 and its gap 8, a rank gap left to right 48.
  const chain = (count: number) => shape(Array.from({ length: count }, (_, index) => `n${index}`),
    Array.from({ length: count - 1 }, (_, index) => [`n${index}`, `n${index + 1}`] as [string, string]));
  const fork = (wide: number, extra: [string, string][] = []) =>
    shape(['s', ...Array.from({ length: wide }, (_, index) => `t${index}`), 'end'],
      [...Array.from({ length: wide }, (_, index) => ['s', `t${index}`] as [string, string]),
        ...Array.from({ length: wide }, (_, index) => [`t${index}`, 'end'] as [string, string]), ...extra]);
  // One node wide, with three returns: three lanes beside it (232 px).
  const oneWithLanes = shape(['a', 'b', 'c', 'd'], [['a', 'b'], ['b', 'c'], ['c', 'd'], ['b', 'a'], ['c', 'a'], ['d', 'a']]);

  const rows: [name: string, flow: FlowShape, at300: string, at400: string, at544: string, at1200: string][] = [
    ['one node wide, two ranks', chain(2), 'down', 'across', 'across', 'across'],
    ['one node wide, three returns', oneWithLanes, 'down', 'down', 'down', 'across'],
    ['two wide', fork(2), 'rail', 'down', 'down', 'across'],
    ['two wide and a return', fork(2, [['end', 's']]), 'rail', 'down', 'down', 'across'],
    ['two wide and a waypoint', fork(2, [['s', 'end']]), 'rail', 'down', 'down', 'across'],
    ['two wide, a waypoint and a return', fork(2, [['s', 'end'], ['end', 's']]), 'rail', 'rail', 'down', 'across'],
    ['three wide', fork(3), 'rail', 'rail', 'down', 'across'],
    ['three wide and a waypoint', fork(3, [['s', 'end']]), 'rail', 'rail', 'rail', 'across'],
    ['four wide', fork(4), 'rail', 'rail', 'rail', 'down'],
    ['six ranks', chain(6), 'down', 'down', 'down', 'across'],
    ['seven ranks', chain(7), 'down', 'down', 'down', 'down'],
    ['a quest\'s four statuses', QUEST, 'rail', 'down', 'down', 'across'],
  ];

  it.each(rows)('places %s at 300, 400, 544 and 1200 px', (_, flow, at300, at400, at544, at1200) => {
    const layout = layoutFlow(flow);
    expect([300, 400, 544, 1200].map((width) => placementFor(layout, width))).toEqual([at300, at400, at544, at1200]);
  });

  it('counts what each placement needs, at the floors the design works', () => {
    expect(needs(layoutFlow(fork(2)))).toEqual({ across: 3 * 160 + 2 * 48, down: 344 });
    expect(needs(layoutFlow(fork(2, [['end', 's']]))).down).toBe(368);
    expect(needs(layoutFlow(fork(2, [['s', 'end']]))).down).toBe(384);
    expect(needs(layoutFlow(fork(3))).down).toBe(528);
    expect(needs(layoutFlow(fork(3, [['s', 'end']]))).down).toBe(568);
    expect(needs(layoutFlow(oneWithLanes)).down).toBe(232);
    expect(needs(layoutFlow(chain(6))).across).toBe(1200);
  });

  it('is the rail where nothing measured the pane, and wherever the person chose the list', () => {
    expect(placementFor(layoutFlow(QUEST), undefined)).toBe('rail');
    expect(placementFor(layoutFlow(QUEST), 1200, true)).toBe('rail');
  });

  // FLOWCHART1d (the flowchart design §5.1): a workflow's step holds its settings and its run's facts, so its caller keeps a wider
  // floor for the node track, 18rem, and draws it top to bottom where it fits, never left to right; the side bar's 300 px floor is
  // 276 px inside its gutters, the main area's 400 px floor 376.
  it('keeps a caller\'s wider floor for its node tracks, and never places left to right where the caller rules it out', () => {
    const steps = layoutFlow(chain(5));
    expect(needs(steps, 16, 18)).toEqual({ across: (5 * 18 + 4 * 3) * 16, down: 288 });
    expect([276, 376, 576, 1232].map((width) => placementFor(steps, width, false, 16, { least: 18, across: false })))
      .toEqual(['rail', 'down', 'down', 'down']);
    // A failure path beside the next step is a rank of two: 37.5rem.
    expect(needs(layoutFlow(fork(2)), 16, 18).down).toBe(600);
    expect(placementFor(layoutFlow(fork(2)), 576, false, 16, { least: 18, across: false })).toBe('rail');
    // Left to right stays the default's wherever a caller does not rule it out.
    expect(placementFor(layoutFlow(chain(2)), 1200, false, 16, { across: false })).toBe('down');
    expect(placementFor(layoutFlow(chain(2)), 1200)).toBe('across');
  });
});

describe('the crossing table (trap 3, its first half: the slots)', () => {
  // A ceiling per fixture, never a minimum (§3.1 step 4): the sweeps are a heuristic. The measured-box check counts the
  // drawn paths' crossings in a rendered browser, which this cannot (jsdom lays nothing out). Each ceiling is what the four
  // sweeps gave on 2026-10-20: a change that orders worse fails here, and one that orders better lowers it.
  const ceilings: Record<string, number> = {
    lifecycle: 5,
    dense: 155,
    unequal: 0,
    'append-before': 0,
    'append-after': 0,
    'many-current': 0,
    'none-current': 0,
  };

  it.each(HARDEST.map((fixture) => [fixture.name, fixture] as const))('holds %s at or under its ceiling', (name, fixture) => {
    expect(slotCrossings(layoutFlow(ofFixture(fixture)))).toBeLessThanOrEqual(ceilings[name]!);
  });

  it('holds the dense graph at the bounds: 24 authored steps and 96 wires', () => {
    expect(DENSE.edges).toHaveLength(96);
    expect(DENSE.nodes.filter((node) => !/^(j[A-D]|s\d|t\d)$/.test(node.id))).toHaveLength(24);
    expect(new Set(DENSE.edges.map((edge) => `${edge.from}>${edge.to}`)).size).toBe(96);
  });

  it('keeps the unequal fixture\'s boxes unequal in both languages, so the check measures what it is meant to', () => {
    const long = UNEQUAL.nodes.find((node) => node.id === 'lint')!.title;
    expect(long.zh.length).toBeGreaterThan(long.en.length * 5);
  });
});

describe('stacks (FLOWCHART1c; the flowchart design §3.5, §3.6 step 1)', () => {
  /** A due entry's tries, each its own node, `like` the try's code: a chain from the entry to where it landed. */
  const tries = (codes: string[], extra: [string, string][] = []): FlowShape => ({
    nodes: [{ id: 'due' }, ...codes.map((code, index) => ({ id: `t${index}`, stack: code })), { id: 'landed' }],
    edges: [
      ...['due', ...codes.map((_, index) => `t${index}`), 'landed'].slice(1).map((to, index, all) => ({
        id: `e${index}`, from: index === 0 ? 'due' : all[index - 1]!, to,
      })),
      ...extra.map(([from, to], index) => ({ id: `x${index}`, from, to })),
    ],
    entries: ['due'],
  });

  it('stacks three or more like nodes that follow each other, and names the edges across its boundary and between its members', () => {
    expect(findStacks(tries(['held', 'held', 'held', 'landed']))).toEqual([
      { id: 'stack:t0', key: 'held', members: ['t0', 't1', 't2'], into: ['e0'], out: ['e3'], within: ['e1', 'e2'] },
    ]);
  });

  it('keeps two like nodes apart, so a single retry stays visible', () => {
    expect(findStacks(tries(['held', 'held', 'landed']))).toEqual([]);
  });

  it('breaks a run where the code changes, and stacks each run of three on its own', () => {
    const found = findStacks(tries(['uncommitted', 'uncommitted', 'uncommitted', 'held', 'held', 'held', 'held', 'landed']));
    expect(found.map((stack) => [stack.key, stack.members])).toEqual([
      ['uncommitted', ['t0', 't1', 't2']],
      ['held', ['t3', 't4', 't5', 't6']],
    ]);
  });

  it('stacks only a chain: a member with a second edge in or out ends the run before it', () => {
    // t1 has a second edge out, so it is no link of a chain: t2, t3 and t4 still follow each other.
    const found = findStacks(tries(['held', 'held', 'held', 'held', 'held'], [['t1', 'landed']]));
    expect(found.map((stack) => stack.members)).toEqual([['t2', 't3', 't4']]);
  });

  it('stacks nothing a caller did not mark like, and nothing in a loop of like nodes', () => {
    expect(findStacks(shape(['a', 'b', 'c', 'd'], [['a', 'b'], ['b', 'c'], ['c', 'd']]))).toEqual([]);
    const loop: FlowShape = {
      nodes: ['a', 'b', 'c'].map((id) => ({ id, stack: 'same' })),
      edges: [{ id: 'ab', from: 'a', to: 'b' }, { id: 'bc', from: 'b', to: 'c' }, { id: 'ca', from: 'c', to: 'a' }],
      entries: ['a'],
    };
    expect(findStacks(loop)).toEqual([]);
  });

  it('reads only the shape: the same graph stacks the same way twice', () => {
    const graph = tries(['held', 'held', 'held', 'opinion', 'landed']);
    expect(findStacks(graph)).toEqual(findStacks(graph));
  });
});
