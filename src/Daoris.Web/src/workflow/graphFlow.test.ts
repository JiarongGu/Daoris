import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { layoutFlow, placementFor, slotCrossings } from '../design/flowLayout';
import i18n from '../locales/i18n';
import { drawnFromJson, FINISHED, graphFlow, WORKFLOW_TRACK } from './graphFlow';
import type { ChartNode } from './chart/stepKinds';
import { MERGE_GRAPH, RELEASE_GRAPH, TWO_OUTCOMES_GRAPH } from './graphFixtures';

// One drawing of a workflow graph for the place, the editor and a run, always
// top to bottom in sections and never left to right, its endings drawn as they are, its holds in one group, on a 14rem track. The
// shipped workflows are read from the driver's own data, so a changed default is a changed drawing here.

const t = i18n.t.bind(i18n);

const DEFAULTS = join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Defaults', 'workflows');
const SHIPPED = readdirSync(DEFAULTS).filter((file) => file.endsWith('.json')).sort()
  .map((file) => JSON.parse(readFileSync(join(DEFAULTS, file), 'utf8')) as { id: string; graph: Record<string, unknown> });
const LANDS_NOTHING = ['in-copy', 'in-place', 'plan-review-apply'];
const shipped = (id: string) => SHIPPED.find((each) => each.id === id)!;

const draw = (graph: unknown, options: Partial<Parameters<typeof graphFlow>[2]> = {}) =>
  graphFlow(t, drawnFromJson(t, i18n, graph), { label: 'How work moves', ...options });

/** Where the drawing is placed in a pane `rem` wide, as `Flow` places it: the track its floor, sections passed. */
const placedAt = (graph: unknown, rem: number, options: Partial<Parameters<typeof graphFlow>[2]> = {}) => {
  const flow = draw(graph, options);
  const layout = layoutFlow({ nodes: flow.nodes.map((node) => ({ id: node.id })), edges: flow.edges, entries: flow.entries });
  return { flow, layout, placement: placementFor(layout, rem * 16, false, 16, { least: WORKFLOW_TRACK.least, across: flow.sections.length === 0 }) };
};

describe('the shipped workflows, read from the driver\'s own data', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('are the eleven this drawing is proved over, so a default added or changed is looked at here', () => {
    expect(SHIPPED.map((each) => each.id)).toEqual([
      'branch-to-push', 'documents', 'in-copy', 'in-place', 'integrated-no-press', 'integrated', 'merge-after-accept', 'merge-automatic',
      'plan-review-apply', 'pull-request', 'release',
    ]);
    const release = shipped('release').graph as { nodes: unknown[]; wires: unknown[]; end: string };
    expect([release.nodes.length, release.wires.length, release.end]).toEqual([12, 14, 'line']);
  });

  it('are what the stories draw: their fixtures equal the files', () => {
    expect(RELEASE_GRAPH).toEqual(shipped('release').graph);
    expect(MERGE_GRAPH).toEqual(shipped('merge-after-accept').graph);
  });

  it('are drawn in sections, so never left to right, at any width', () => {
    for (const each of SHIPPED) {
      for (const rem of [20, 25, 39, 60, 80, 120]) {
        const { flow, placement } = placedAt(each.graph, rem);
        expect(flow.sections.length, each.id).toBeGreaterThan(0);
        expect(placement, `${each.id} at ${rem}rem`).not.toBe('across');
      }
    }
  });

  it('name sections the landing divides, each from a drawn step, and one section of steps where nothing lands', () => {
    for (const each of SHIPPED) {
      const flow = draw(each.graph);
      const drawn = new Set(flow.nodes.map((node) => node.id));
      for (const section of flow.sections) expect(drawn.has(section.from), `${each.id}: ${section.title}`).toBe(true);
      expect(flow.sections.map((section) => section.title)[0], each.id).toBe(LANDS_NOTHING.includes(each.id) ? 'Steps' : 'Before it lands');
    }
    expect(draw(shipped('in-place').graph).sections).toEqual([{ title: 'Steps', from: 'work' }]);
    expect(draw(shipped('release').graph).sections).toEqual([
      { title: 'Before it lands', from: 'work' }, { title: 'After it lands', from: 'line' },
    ]);
    expect(draw(shipped('merge-after-accept').graph).sections.map((section) => section.title)).toEqual(['Before it lands', 'After it lands']);
  });

  it('cross nothing, and draw no node twice or edge to a node that is not drawn', () => {
    for (const each of SHIPPED) {
      const { flow, layout } = placedAt(each.graph, 80);
      const ids = flow.nodes.map((node) => node.id);
      expect(new Set(ids).size, each.id).toBe(ids.length);
      for (const edge of flow.edges) expect(ids, `${each.id}: ${edge.id}`).toEqual(expect.arrayContaining([edge.from, edge.to]));
      expect(new Set(flow.edges.map((edge) => edge.id)).size, each.id).toBe(flow.edges.length);
      expect(slotCrossings(layout), each.id).toBe(0);
    }
  });

  it('draws the release graph top to bottom at 80 and 39rem, whose two-way branch needs 31rem, and as the rail at 25rem', () => {
    const release = shipped('release').graph;
    expect(placedAt(release, 80).placement).toBe('down');
    expect(placedAt(release, 39).placement).toBe('down');
    expect(placedAt(release, 25).placement).toBe('rail');
    expect(WORKFLOW_TRACK).toEqual({ least: 14, most: 36 });
  });

  it('draws a single chain, without a branch, top to bottom from the 14rem track and nowhere across', () => {
    expect(placedAt(shipped('merge-after-accept').graph, 25).placement).toBe('down');
    expect(placedAt(shipped('merge-after-accept').graph, 14).placement).toBe('down');
  });

  it('flags the configured end where the work is done, and says steps after it still run only where some do', () => {
    for (const each of SHIPPED) {
      const flow = draw(each.graph);
      const marked = flow.nodes.filter((node) => node.mark);
      expect(marked.map((node) => node.id), each.id).toEqual([(each.graph as { end: string }).end]);
      expect(marked[0]!.mark!.word).toBe('Work done here');
    }
    expect(draw(shipped('release').graph).nodes.find((node) => node.id === 'line')!.mark).toEqual({ word: 'Work done here', foot: 'Steps after it still run' });
    expect(draw(shipped('merge-after-accept').graph).nodes.find((node) => node.id === 'line')!.mark).toEqual({ word: 'Work done here', foot: 'Steps after it still run' });
  });

  it('draws Finished from no graph, only from a run\'s record', () => {
    for (const each of SHIPPED) {
      const flow = draw(each.graph);
      expect(flow.nodes.some((node) => node.id === FINISHED || node.title === 'Finished'), each.id).toBe(false);
      expect(flow.edges.some((edge) => edge.to === FINISHED), each.id).toBe(false);
    }
    const ended = draw(shipped('merge-after-accept').graph, { finished: { word: 'done', tone: 'done' } });
    expect(ended.nodes.at(-1)).toMatchObject({ id: FINISHED, title: 'Finished', state: { word: 'done', tone: 'done' } });
    expect(ended.edges.filter((edge) => edge.to === FINISHED).map((edge) => edge.from)).toEqual(['tree']);
    // A record that has not finished draws none.
    expect(draw(shipped('merge-after-accept').graph, { finished: null }).nodes.some((node) => node.id === FINISHED)).toBe(false);
  });

  it('draws a drawing-only ◎ for an outcome with no route, and for none that is routed', () => {
    const release = draw(shipped('release').graph);
    const none = release.nodes.filter((node) => node.title === 'No further step');
    expect(none).toHaveLength(1);
    expect(none[0]).toMatchObject({ glyph: 'stepNoFurther', faint: true });
    expect(release.edges.find((edge) => edge.to === none[0]!.id)).toMatchObject({ from: 'find', style: 'when', port: 'none', label: 'none' });
    // The routed outcome stays an edge to its step.
    expect(release.edges.find((edge) => edge.from === 'find' && edge.to === 'release')).toMatchObject({ style: 'when', port: 'release', label: 'release' });
    for (const each of SHIPPED.filter((one) => one.id !== 'release')) {
      expect(draw(each.graph).nodes.some((node) => node.title === 'No further step'), each.id).toBe(false);
    }
  });

  // A run passes the outcome a decision returned, and the drawing says which route it took.
  it('draws a run\'s decision: the route its outcome took reached, the others not taken, and Finished after the one taken', () => {
    const decided = (outcome: 'release' | 'none') => {
      const graph = drawnFromJson(t, i18n, shipped('release').graph);
      const taken = outcome === 'release' ? ['publish', 'release'] : [];
      const nodes = graph.nodes.map((node) => ({
        ...node,
        reached: !['release', 'publish'].includes(node.id) || taken.includes(node.id),
        ...(node.id === 'find' ? { outcome } : {}),
      }));
      return graphFlow(t, { ...graph, nodes }, { label: 'How work moves', finished: { word: 'done', tone: 'done' } });
    };

    // `none` is routed to nothing: its ◎ is reached, and the run finished there, not after the steps it never took.
    const none = decided('none');
    const nowhere = none.nodes.find((node) => node.title === 'No further step')!;
    expect(nowhere.reached).toBe(true);
    expect(none.edges.filter((edge) => edge.to === FINISHED).map((edge) => edge.from)).toEqual(['tree', nowhere.id]);

    // `release` is routed: the run finished after the last step it took, and the unrouted outcome is a ◎ not taken.
    const release = decided('release');
    const left = release.nodes.find((node) => node.title === 'No further step')!;
    expect(left.reached).toBe(false);
    expect(left.state).toMatchObject({ word: 'route not taken' });
    expect(release.edges.filter((edge) => edge.to === FINISHED).map((edge) => edge.from)).toEqual(['tree', 'publish']);
  });

  it('folds the declared requests and holds into one group, counted, and opens it on request', () => {
    const release = draw(shipped('release').graph);
    expect(release.nodes.map((node) => node.id)).not.toEqual(expect.arrayContaining(['requests', 'holds', 'content']));
    const group = release.nodes.find((node) => node.title === 'Daoris makes sure · 3')!;
    expect(group.glyph).toBe('stepHold');
    // The edge into the first member and out of the last are drawn to and from the group.
    expect(release.edges.find((edge) => edge.to === group.id)).toMatchObject({ from: 'work' });
    expect(release.edges.find((edge) => edge.from === group.id)).toMatchObject({ to: 'check' });
    expect(release.edges.some((edge) => edge.from === 'holds' || edge.to === 'holds')).toBe(false);
    // Two are a group too: `Flow` stacks only three like nodes, and the documents workflow holds two.
    expect(draw(shipped('documents').graph).nodes.some((node) => node.title === 'Daoris makes sure · 2')).toBe(true);

    const opened = draw(shipped('release').graph, { holds: { open: true } });
    expect(opened.nodes.map((node) => node.id)).toEqual(expect.arrayContaining(['requests', 'holds', 'content']));
    expect(opened.nodes.some((node) => node.title.startsWith('Daoris makes sure'))).toBe(false);
    expect(opened.edges.find((edge) => edge.from === 'requests')).toMatchObject({ to: 'holds' });
  });

  it('words the renamed steps as the glossary names them', () => {
    const titles = (id: string) => draw(shipped(id).graph).nodes.map((node) => node.title);
    // The pull request is a plugin's step, titled by its node and its plugin; the push is Daoris's own.
    expect(titles('pull-request')).toEqual(expect.arrayContaining(['The push', 'pull-request by daoris-github']));
    expect(draw(shipped('pull-request').graph).nodes.find((node) => node.title.startsWith('pull-request by'))).toMatchObject({ glyph: 'stepPullRequest' });
    expect(titles('integrated')).toContain('The integration request');
    expect(titles('release')).toEqual(expect.arrayContaining(['The work', 'Your review', 'The landing', 'On the line']));
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    const flow = draw(shipped('release').graph);
    expect(flow.nodes.find((node) => node.id === 'line')!.mark).toEqual({ word: '工作在此完成', foot: '其后的步骤仍会运行' });
    expect(flow.nodes.map((node) => node.title)).toEqual(expect.arrayContaining(['Daoris 核实 · 3', '无后续步骤']));
    expect(flow.sections.map((section) => section.title)).toEqual(['落地之前', '落地之后']);
  });
});

describe('a graph drawn through the module', () => {
  const node = (id: string, kind: string, extra: object = {}) => ({ id, kind, version: 1, reach: 'performed', ...extra });

  it('draws a way back as a return, labelled, and a failure in its own style', () => {
    const flow = draw({
      entries: ['a'], end: 'b',
      nodes: [node('a', 'claimed'), node('b', 'review'), node('c', 'check')],
      wires: [
        { id: 'w1', kind: 'after', from: 'a', to: 'b' },
        { id: 'w2', kind: 'back', from: 'b', on: 'rejected', to: 'a' },
        { id: 'w3', kind: 'failed', from: 'b', to: 'c' },
      ],
    });
    expect(flow.edges.find((edge) => edge.id === 'w2')).toMatchObject({ from: 'b', to: 'a', style: 'back', label: 'sent back' });
    expect(flow.edges.find((edge) => edge.id === 'w3')).toMatchObject({ style: 'failed', says: 'if it fails' });
    // "The run stops" is the absence of a failed wire, never a node of its own.
    expect(draw({ entries: ['a'], end: 'a', nodes: [node('a', 'claimed')], wires: [] }).nodes).toHaveLength(1);
  });

  it('leaves a section of Steps where nothing lands', () => {
    const flow = draw({ entries: ['a'], end: 'b', nodes: [node('a', 'claimed'), node('b', 'check')], wires: [{ id: 'w1', kind: 'after', from: 'a', to: 'b' }] });
    expect(flow.sections).toEqual([{ title: 'Steps', from: 'a' }]);
  });

  it('does not fold a hold that has a way of its own, or one alone', () => {
    const alone = draw({
      entries: ['a'], end: 'a', nodes: [node('a', 'claimed'), node('h', 'quest.holds'), node('c', 'check')],
      wires: [{ id: 'w1', kind: 'after', from: 'a', to: 'h' }, { id: 'w2', kind: 'after', from: 'h', to: 'c' }],
    });
    expect(alone.nodes.map((each) => each.id)).toEqual(['a', 'h', 'c']);
    const failing = draw({
      entries: ['a'], end: 'a',
      nodes: [node('a', 'claimed'), node('h', 'quest.holds'), node('i', 'content.holds'), node('c', 'check')],
      wires: [
        { id: 'w1', kind: 'after', from: 'a', to: 'h' }, { id: 'w2', kind: 'after', from: 'h', to: 'i' },
        { id: 'w3', kind: 'after', from: 'i', to: 'c' }, { id: 'w4', kind: 'failed', from: 'h', to: 'c' },
      ],
    });
    expect(failing.nodes.map((each) => each.id)).toEqual(['a', 'h', 'i', 'c']);
  });

  it('lights the group from its members: current if any is, reached if any is', () => {
    const graph = drawnFromJson(t, i18n, shipped('release').graph);
    const lit = graph.nodes.map((each) => (each.id === 'holds'
      ? { ...each, current: true, reached: true, state: { word: 'waiting', tone: 'open' as const } }
      : each.id === 'requests' ? { ...each, reached: true, state: { word: 'done', tone: 'done' as const }, settled: true } : each));
    const group = graphFlow(t, { ...graph, nodes: lit }, { label: 'x' }).nodes.find((each) => each.title.startsWith('Daoris makes sure'))!;
    expect(group).toMatchObject({ current: true, reached: true, state: { word: 'waiting', tone: 'open' } });
  });
});

// The components are handed what to draw: each step's variant, a decision's outcomes as ports, each unrouted
// outcome a ◎, the terminals and the group for what they are, and the data wires apart from the ways on.
describe('a graph handed to the chart\'s components (FLOWCHART2a)', () => {
  const stepOf = (flow: ReturnType<typeof draw>, id: string) => (flow.nodes.find((each) => each.id === id) as ChartNode).step!;
  const kindOf = (each: { graph: Record<string, unknown> }) => new Map((each.graph.nodes as { id: string; kind: string }[]).map((node) => [node.id, node.kind]));

  it('draws every step of every shipped graph with a variant its kind has, none unknown', () => {
    for (const each of SHIPPED) {
      const flow = draw(each.graph, { holds: { open: true } });
      const kinds = kindOf(each);
      for (const node of flow.nodes) {
        const step = (node as ChartNode).step;
        expect(step, `${each.id}: ${node.id}`).toBeDefined();
        if (step!.terminal) continue;
        expect(step!.variant, `${each.id}: ${node.id} (${kinds.get(node.id)})`).not.toBe('unknown');
      }
    }
  });

  it('gives the release graph each kind its variant', () => {
    const flow = draw(shipped('release').graph, { holds: { open: true } });
    const variants = Object.fromEntries(flow.nodes.map((node) => [node.id, (node as ChartNode).step!.variant]));
    expect(variants).toMatchObject({
      work: 'work', requests: 'holds', check: 'check', review: 'you', landing: 'landing', line: 'line',
      find: 'decision', release: 'plugin', publish: 'plugin',
    });
  });

  it('draws each outcome of a decision as a port, in the declared order, and each route from its own', () => {
    for (const each of SHIPPED) {
      const flow = draw(each.graph);
      const outcomes = new Map((each.graph.nodes as { id: string; outcomes?: string[] }[]).map((node) => [node.id, node.outcomes ?? []]));
      for (const [id, declared] of outcomes) {
        const drawn = flow.nodes.find((node) => node.id === id);
        if (!drawn) continue;
        expect((drawn as ChartNode).step!.ports.map((port) => port.outcome), `${each.id}: ${id}`).toEqual(declared);
      }
      for (const edge of flow.edges.filter((one) => one.style === 'when')) {
        expect(stepOf(flow, edge.from).ports.map((port) => port.outcome), `${each.id}: ${edge.id}`).toContain(edge.port);
      }
    }
    const find = stepOf(draw(shipped('release').graph), 'find');
    expect(find.ports).toEqual([{ outcome: 'release', routed: true }, { outcome: 'none', routed: false }]);
  });

  it('ends each unrouted outcome at a ◎ terminal, one for each, and no other node is one', () => {
    for (const each of SHIPPED) {
      const flow = draw(each.graph);
      const unrouted = flow.nodes.flatMap((node) => (node as ChartNode).step!.ports.filter((port) => !port.routed).map((port) => `${node.id}:${port.outcome}`));
      const ends = flow.nodes.filter((node) => (node as ChartNode).step!.terminal === 'none');
      expect(ends.map((node) => node.id).sort(), each.id).toEqual(unrouted.map((one) => one.replace(':', ':none:')).sort());
      for (const end of ends) expect(flow.edges.filter((edge) => edge.to === end.id), end.id).toHaveLength(1);
    }
  });

  it('draws the data wires beside the ways on, named by what they carry, and no shipped graph but the release one has any', () => {
    const release = draw(shipped('release').graph);
    expect(release.edges.filter((edge) => edge.style === 'data')).toEqual([
      { id: 'i', from: 'find', to: 'release', style: 'data', says: 'with what it made', label: 'release' },
      { id: 'k', from: 'release', to: 'publish', style: 'data', says: 'with what it made', label: 'release' },
    ]);
    for (const each of SHIPPED.filter((one) => one.id !== 'release')) {
      expect(draw(each.graph).edges.some((edge) => edge.style === 'data'), each.id).toBe(false);
    }
  });

  it('flags Finished and the group for what they are', () => {
    const flow = draw(shipped('release').graph, { finished: { word: 'done', tone: 'done' } });
    expect(stepOf(flow, FINISHED)).toMatchObject({ terminal: 'finished' });
    expect(stepOf(flow, 'holds:requests')).toMatchObject({ variant: 'holds', group: { count: 3, open: false } });
    expect(stepOf(draw(shipped('release').graph, { holds: { open: true } }), 'requests').group).toMatchObject({ count: 3, open: true });
  });

  const node = (id: string, kind: string, extra: object = {}) => ({ id, kind, version: 1, reach: 'performed', ...extra });

  it('draws two outcomes that reach one step each from its own port, as two edges', () => {
    const flow = draw(TWO_OUTCOMES_GRAPH);
    const edges = flow.edges.filter((edge) => edge.from === 'find');
    expect(edges.map((edge) => [edge.to, edge.port, edge.style])).toEqual([['record', 'release', 'when'], ['record', 'again', 'when'], [expect.stringContaining('none'), 'none', 'when']]);
    expect(new Set(edges.map((edge) => edge.id)).size).toBe(3);
    expect(stepOf(flow, 'find').ports.map((port) => [port.outcome, port.routed])).toEqual([['release', true], ['again', true], ['none', false]]);
    const layout = layoutFlow({ nodes: flow.nodes.map((each) => ({ id: each.id })), edges: flow.edges, entries: flow.entries });
    expect(layout.paths.filter((path) => path.from === 'find' && path.to === 'record').map((path) => path.port)).toEqual(['release', 'again']);
  });

  it('draws an outcome routed back as a routed port with its way, and no ◎', () => {
    const flow = draw({
      entries: ['start'], end: 'check',
      nodes: [node('start', 'claimed'), node('decide', 'p/decide', { outcomes: ['again', 'done'] }), node('check', 'check')],
      wires: [
        { id: 'w1', kind: 'after', from: 'start', to: 'decide' },
        { id: 'w2', kind: 'when', from: 'decide', outcome: 'done', to: 'check' },
        { id: 'w3', kind: 'back', from: 'decide', on: 'again', to: 'start' },
      ],
    });
    expect(flow.nodes.some((each) => (each as ChartNode).step!.terminal === 'none')).toBe(false);
    expect(stepOf(flow, 'decide').ports).toEqual([{ outcome: 'again', routed: true }, { outcome: 'done', routed: true }]);
    expect(flow.edges.find((edge) => edge.id === 'w3')).toMatchObject({ style: 'back', port: 'again' });
  });

  it('draws a run\'s decision with the outcomes its answer holds and no others', () => {
    const graph = drawnFromJson(t, i18n, shipped('release').graph);
    const returned = { ...graph, nodes: graph.nodes.map((each) => (each.id === 'find' ? { id: each.id, kind: each.kind, title: each.title, outcome: 'release' } : each)) };
    const flow = graphFlow(t, returned, { label: 'x' });
    expect(stepOf(flow, 'find').ports).toEqual([{ outcome: 'release', routed: true, taken: true }]);
    expect(flow.nodes.some((each) => (each as ChartNode).step!.terminal === 'none')).toBe(false);
  });
});
