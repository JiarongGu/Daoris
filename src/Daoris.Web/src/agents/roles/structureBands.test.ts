import { describe, expect, it } from 'vitest';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { buildStructure, NO_FILTERS } from './roleStructure';
import { readingOrder, structureBands } from './structureBands';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS, WORKFLOWS, largeStructure } from './structureFixtures';

// CANVAS1c (D224; the canvas design §3.2): the structure as bands. One band per place in tree order, this machine, each workspace and its
// repositories that set something of their own; in each the roles placed or appointed there; beside them the agents, once each, by the
// bands of the roles leaning on them; only the staffed edges. Past 24 nodes each workspace folds to one band. Pure.

const diagramOf = (appointed?: Parameters<typeof buildStructure>[0]['appointed']) => buildStructure({
  structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: LINES,
  driver: DRIVER, workflows: WORKFLOWS, filters: NO_FILTERS, appointed,
}).diagram;
const SUPERVISED = {
  stewardOffered: true, supervisorOffered: true,
  appointments: [
    { role: 'steward', scope: 'machine', workspace: '', repository: '', dormant: false },
    { role: 'supervisor', scope: 'workspace:aurora', workspace: 'aurora', repository: '', dormant: true },
  ],
};
const largeDiagram = () => buildStructure({
  ...largeStructure(), tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: null, driver: null, workflows: {}, filters: NO_FILTERS,
}).diagram;

describe('the bands', () => {
  it('stand in tree order, this machine first and each workspace before its repositories, indented by depth', () => {
    const { bands } = structureBands(diagramOf());
    expect(bands.map((band) => band.key)).toEqual(['place:machine', 'place:aurora', 'place:aurora/engine', 'place:default/tools']);
    expect(bands.map((band) => band.depth)).toEqual([0, 1, 2, 2]);
  });

  it('hold the roles placed in them and the appointments made there, and name an offered role nobody holds in the first band', () => {
    const { bands } = structureBands(diagramOf(SUPERVISED));
    const held = (key: string) => bands.find((band) => band.key === key)!.cells.map((cell) => cell.key);
    expect(held('place:machine')).toContain('role:build');
    expect(held('place:machine')).toContain('appointed:steward');
    expect(held('place:aurora')).toEqual(['appointed:supervisor']);
    expect(held('place:aurora/engine')).toEqual(['role:engine-build']);
    const offered = structureBands(diagramOf({ stewardOffered: true, supervisorOffered: false, appointments: [] })).bands[0]!;
    expect(offered.cells.map((cell) => cell.key)).toContain('appointed:steward');
    // A dormant appointment is dashed in its band, a live one whole.
    const cells = bands.flatMap((band) => band.cells);
    expect(cells.find((cell) => cell.node === 'appointed:supervisor')!.dashed).toBe(true);
    expect(cells.find((cell) => cell.node === 'appointed:steward')!.dashed).toBe(false);
  });

  it('draw a role in each band it is placed in under one identity: the first occurrence keyed by the node, a later one by its band', () => {
    const diagram = diagramOf();
    const twice = { ...diagram, edges: [...diagram.edges, { id: 'placed:place:aurora>role:build', kind: 'placed' as const, from: 'place:aurora', to: 'role:build' }] };
    const drawn = structureBands(twice);
    const cells = drawn.bands.flatMap((band) => band.cells).filter((cell) => cell.node === 'role:build');
    expect(cells.map((cell) => cell.key)).toEqual(['role:build', 'role:build@place:aurora']);
    // Each occurrence staffs the same agents: the edges are drawn from both and map back to the one edge of the diagram.
    const staffed = drawn.edges.filter((edge) => edge.edge === 'staffed:role:build>agent:claude-code:1');
    expect(staffed.map((edge) => edge.from)).toEqual(['role:build', 'role:build@place:aurora']);
    expect(new Set(staffed.map((edge) => edge.id)).size).toBe(2);
  });

  it('draw only the staffed edges, dashed where the last reading says the choice cannot run, numbered by position', () => {
    const { edges } = structureBands(diagramOf());
    expect(edges.every((edge) => edge.edge.startsWith('staffed:'))).toBe(true);
    const codex = edges.find((edge) => edge.edge === 'staffed:role:build>agent:codex:2')!;
    expect(codex).toMatchObject({ dashed: true, position: '2', to: 'agent:codex' });
    expect(edges.find((edge) => edge.edge === 'staffed:role:build>agent:claude-code:1')).toMatchObject({ dashed: false, position: '1' });
  });

  it('put each agent once, ordered by the bands of the roles leaning on them, a tie in declared order', () => {
    const { agents, bands, edges } = structureBands(diagramOf());
    expect(new Set(agents).size).toBe(agents.length);
    const band = (key: string) => bands.findIndex((each) => each.key === key || each.cells.some((cell) => cell.key === key));
    const mean = (agent: string) => {
      const at = edges.filter((edge) => edge.to === agent).map((edge) => band(edge.from));
      return at.reduce((sum, value) => sum + value, 0) / at.length;
    };
    expect(agents.map(mean)).toEqual([...agents.map(mean)].sort((a, b) => a - b));
    // Codex's staffing is in the first band alone; Claude Code's reaches the repository's, so it stands lower.
    expect(agents.indexOf('agent:codex')).toBeLessThan(agents.indexOf('agent:claude-code'));
  });

  it('read in order: each band\'s label then its roles, top to bottom, then the agents', () => {
    const drawn = structureBands(diagramOf());
    const order = readingOrder(drawn);
    expect(order.slice(0, 2)).toEqual(['place:machine', drawn.bands[0]!.cells[0]!.key]);
    expect(order.indexOf('place:aurora')).toBeGreaterThan(order.indexOf('role:build'));
    expect(order.slice(-drawn.agents.length)).toEqual(drawn.agents);
    expect(new Set(order).size).toBe(order.length);
  });
});

describe('the bands past 24 nodes', () => {
  it('fold each workspace to one band with its places, and draw one edge from a fold to each agent, counted', () => {
    const diagram = largeDiagram();
    expect(diagram.nodes.length).toBeGreaterThan(24);
    const drawn = structureBands(diagram);
    const folded = drawn.bands.filter((band) => band.fold);
    expect(folded.map((band) => band.key).sort()).toEqual(['place:aurora', 'place:borealis']);
    expect(folded.every((band) => band.cells.length === 0)).toBe(true);
    const fromFold = drawn.edges.filter((edge) => edge.from === 'place:aurora');
    expect(fromFold.length).toBeGreaterThan(0);
    expect(new Set(fromFold.map((edge) => edge.to)).size).toBe(fromFold.length);
    expect(fromFold.every((edge) => edge.count >= 1)).toBe(true);
    // Every place is kept: either a band of its own or its workspace's fold.
    const places = diagram.nodes.filter((node) => node.kind === 'place').map((node) => node.id);
    expect(places.every((id) => drawn.bands.some((band) => band.key === id) || drawn.foldOf.has(id))).toBe(true);
  });

  it('open a workspace to its places whole again, and every role kept', () => {
    const diagram = largeDiagram();
    const opened = structureBands(diagram, new Set(['place:aurora']));
    expect(opened.bands.filter((band) => band.fold).map((band) => band.key)).toEqual(['place:borealis']);
    expect(opened.bands.some((band) => band.key === 'place:aurora' && band.fold === null)).toBe(true);
    const roles = new Set(opened.bands.flatMap((band) => band.cells).filter((cell) => cell.kind === 'role').map((cell) => cell.node));
    for (const node of diagram.nodes.filter((each) => each.kind === 'role')) {
      const heldByFold = structureBands(diagram).foldOf.size > 0;
      expect(roles.has(node.id) || heldByFold).toBe(true);
    }
  });
});
