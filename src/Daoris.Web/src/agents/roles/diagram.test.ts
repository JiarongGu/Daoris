import { describe, expect, it } from 'vitest';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { DIAGRAM_REM, diagramPlacement, diagramShape, diagramTrack, layoutDiagram } from './diagram';
import { buildStructure, NO_FILTERS } from './roleStructure';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS, WORKFLOWS, largeStructure } from './structureFixtures';

// COMPANYVIEW1f (the company-view design §4, *Ranked by design/flowLayout.ts, placed by its own rule*): the diagram's ranks are its three
// columns, places, roles and agents. A node's floor is 12rem, so across needs three tracks and two 3rem gaps, 42rem of the main area: it is
// across at 80rem, and its list (the rail) at 39 and 25rem. Past 24 nodes each workspace folds to one node.

const diagramOf = (appointed?: Parameters<typeof buildStructure>[0]['appointed']) => buildStructure({
  structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: LINES,
  driver: DRIVER, workflows: WORKFLOWS, filters: NO_FILTERS, appointed,
}).diagram;

/** The main area's inner widths at 80, 39 and 25rem: less its 24 px gutters, and 12 px at the narrow width. */
const INNER = { wide: 80 * 16 - 48, half: 39 * 16 - 48, narrow: 25 * 16 - 24 } as const;

describe('the diagram, laid out', () => {
  it('ranks its three columns: places, then roles, then agents', () => {
    const layout = layoutDiagram(diagramShape(diagramOf()));
    expect(layout.ranks).toHaveLength(3);
    const column = (index: number) => layout.ranks[index]!.flatMap((slot) => (slot.kind === 'node' ? [slot.id.slice(0, slot.id.indexOf(':'))] : []));
    expect(new Set(column(0))).toEqual(new Set(['place']));
    expect(new Set(column(1))).toEqual(new Set(['role']));
    expect(new Set(column(2))).toEqual(new Set(['agent']));
    // No edge skips a column, so none takes a waypoint.
    expect(layout.ranks.flat().every((slot) => slot.kind === 'node')).toBe(true);
  });

  it('keeps an offered role nobody holds in the roles column, though no edge reaches it', () => {
    const layout = layoutDiagram(diagramShape(diagramOf({ stewardOffered: true, supervisorOffered: false, appointments: [] })));
    expect(layout.rank['appointed:steward']).toBe(1);
    expect(layout.order.indexOf('appointed:steward')).toBeGreaterThan(layout.order.indexOf('role:build'));
    expect(layout.order.indexOf('appointed:steward')).toBeLessThan(layout.order.indexOf('agent:claude-code'));
  });

  it('dashes a choice that cannot run and a dormant appointment, and draws the rest whole', () => {
    const shape = diagramShape(diagramOf({
      stewardOffered: false, supervisorOffered: true,
      appointments: [{ role: 'supervisor', scope: 'workspace:aurora', workspace: 'aurora', repository: '', dormant: true }],
    }));
    const style = (id: string) => shape.edges.find((edge) => edge.id === id)?.style;
    expect(style('staffed:role:build>agent:claude-code:1')).toBe('next');
    expect(style('staffed:role:build>agent:codex:2')).toBe('conditional');
    expect(style('appointed:place:aurora>appointed:supervisor')).toBe('conditional');
    expect(style('placed:place:machine>role:build')).toBe('next');
  });

  it('is across where three 12rem columns and their gaps fit, 42rem, and its list below that', () => {
    const layout = layoutDiagram(diagramShape(diagramOf()));
    expect(DIAGRAM_REM.node).toBe(12);
    expect(diagramPlacement(layout, 42 * 16)).toBe('across');
    expect(diagramPlacement(layout, 42 * 16 - 1)).toBe('rail');
    // 80rem holds it with its detail beside it; 39 and 25rem are its list.
    expect(diagramPlacement(layout, INNER.wide - 20 * 16 - 24)).toBe('across');
    expect(diagramPlacement(layout, INNER.half)).toBe('rail');
    expect(diagramPlacement(layout, INNER.narrow)).toBe('rail');
    // Nothing measured, and the list chosen.
    expect(diagramPlacement(layout, undefined)).toBe('rail');
    expect(diagramPlacement(layout, INNER.wide, true)).toBe('rail');
  });

  it('gives each node track what the pane leaves it, from its 12rem floor to 16rem, and draws nothing smaller', () => {
    const layout = layoutDiagram(diagramShape(diagramOf()));
    expect(diagramTrack(layout, 42 * 16)).toBe(12 * 16);
    expect(diagramTrack(layout, INNER.wide)).toBe(16 * 16);
    expect(diagramTrack(layout, 50 * 16)).toBe(Math.floor((50 * 16 - 2 * 48) / 3));
  });

  it('folds each workspace past 24 nodes, every place and every role kept', () => {
    const large = buildStructure({
      ...largeStructure(), tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: null, driver: null, workflows: {},
      filters: NO_FILTERS,
    }).diagram;
    expect(large.nodes.length).toBeGreaterThan(24);
    const shape = diagramShape(large);
    const folded = shape.nodes.filter((node) => node.fold);
    expect(folded.map((node) => node.id).sort()).toEqual(['place:aurora', 'place:borealis']);
    expect(shape.nodes.length).toBeLessThan(large.nodes.length);
    expect(shape.nodes.filter((node) => node.id.startsWith('role:')).length).toBe(large.nodes.filter((node) => node.kind === 'role').length);
    // Opened, a workspace's places are drawn whole again.
    expect(diagramShape(large, new Set(['place:aurora'])).nodes.filter((node) => node.fold).map((node) => node.id)).toEqual(['place:borealis']);
  });
});
