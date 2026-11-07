import { type GroupFold, groupFolds } from '../../design/groupFolds';
import { type Diagram, type DiagramNode, placeId } from './roleStructure';

/**
 * **The agents' structure as bands**: one band per place in tree order, this machine, each
 * workspace and its repositories that set something of their own; in each, the roles placed or appointed there, stacked; beside them, once
 * each, the agents their choices name, ordered by where the roles leaning on them stand. Only the *staffed* edges are drawn, role to agent
 * across a gutter; *placed* and *appointed* are what the band says, and stay in each node's description and the list. Pure.
 *
 * @remarks
 * - **One identity, many occurrences:** a role is one node in the diagram (one selection, one detail) and is drawn in each band it is
 *   placed in; the first occurrence's key is the node's id, a later one's is `<id>@<band>`. An appointment is drawn per place it applies to.
 * - **Offered and unplaced:** a role or an appointment that no place holds is named in the first band, where it is offered.
 * - **Folds** past 24 nodes, as the three columns had them: each workspace and its places fold to one band, every staffed edge across its
 *   boundary drawn from it to the agent, one for each agent, counted (`design/groupFolds.ts`). Opening one widens no read.
 */

/** A role or an appointment drawn in a band. */
export type Cell = { key: string; node: string; kind: 'role' | 'appointed'; dashed: boolean };

/** A place's band, or a workspace folded to one. */
export type Band = { key: string; place: string; depth: number; fold: GroupFold | null; cells: Cell[] };

/** A staffed edge from a role's occurrence (or a fold) to an agent; `count` is how many edges a fold's one stands for. */
export type BandEdge = { id: string; edge: string; from: string; to: string; dashed: boolean; position: string; count: number };

export type Bands = {
  bands: Band[];
  /** The agents drawn, ordered by the bands their roles stand in. */
  agents: string[];
  edges: BandEdge[];
  /** Where each place folded away is drawn: its workspace's fold. */
  foldOf: Map<string, string>;
};

const depthOf = (place: Extract<DiagramNode, { kind: 'place' }>) => (place.repository !== null ? 2 : place.workspace !== null ? 1 : 0);

export function structureBands(diagram: Diagram, opened: ReadonlySet<string> = new Set()): Bands {
  const places = diagram.nodes.filter((node): node is Extract<DiagramNode, { kind: 'place' }> => node.kind === 'place');
  const folds = groupFolds(
    {
      nodes: diagram.nodes.map((node) => ({ id: node.id, ...(node.kind === 'place' && node.group !== null ? { group: node.group } : {}) })),
      edges: diagram.edges,
    },
    opened,
    (group) => placeId({ workspace: group }),
  );
  const foldOf = new Map(folds.flatMap((fold) => fold.members.map((member) => [member, fold.id] as const)));
  const foldById = new Map(folds.map((fold) => [fold.id, fold]));

  const bands: Band[] = [];
  for (const place of places) {
    const held = foldOf.get(place.id);
    if (held === undefined) bands.push({ key: place.id, place: place.id, depth: depthOf(place), fold: null, cells: [] });
    else if (foldById.get(held)!.members[0] === place.id) bands.push({ key: held, place: held, depth: 1, fold: foldById.get(held)!, cells: [] });
  }
  const bandAt = new Map(bands.map((band, index) => [band.key, index]));
  const bandOfPlace = (place: string) => foldOf.get(place) ?? place;

  // The roles and appointments each band holds, from the placed and appointed edges.
  const occurrences = new Map<string, Cell[]>();
  const add = (band: Band, node: string, kind: Cell['kind'], dashed: boolean) => {
    if (band.cells.some((cell) => cell.node === node)) return;
    const seen = occurrences.get(node) ?? [];
    const cell: Cell = { key: seen.length === 0 ? node : `${node}@${band.key}`, node, kind, dashed };
    seen.push(cell);
    occurrences.set(node, seen);
    band.cells.push(cell);
  };
  for (const edge of diagram.edges) {
    if (edge.kind === 'staffed') continue;
    const band = bands[bandAt.get(bandOfPlace(edge.from)) ?? -1];
    if (!band || band.fold) continue;
    add(band, edge.to, edge.kind === 'appointed' ? 'appointed' : 'role', edge.kind === 'appointed' && edge.dashed);
  }
  // A role or an appointment no unfolded place holds is named in the first band, where it is offered, unless a fold holds it.
  const heldByFold = new Set(diagram.edges.filter((edge) => edge.kind !== 'staffed' && foldOf.has(edge.from)).map((edge) => edge.to));
  const first = bands[0];
  if (first) {
    for (const node of diagram.nodes) {
      if ((node.kind === 'role' || node.kind === 'appointed') && !occurrences.has(node.id) && !heldByFold.has(node.id)) add(first, node.id, node.kind, false);
    }
  }

  // The staffed edges: from each occurrence of the role, and from a fold once for each agent its roles lean on.
  const edges: BandEdge[] = [];
  const folded = new Map<string, BandEdge>();
  const fromFold = (role: string) => diagram.edges.filter((edge) => edge.kind === 'placed' && edge.to === role && foldOf.has(edge.from)).map((edge) => foldOf.get(edge.from)!);
  for (const edge of diagram.edges) {
    if (edge.kind !== 'staffed') continue;
    for (const cell of occurrences.get(edge.from) ?? []) {
      edges.push({
        id: cell.key === edge.from ? edge.id : `${edge.id}@${cell.key}`, edge: edge.id, from: cell.key, to: edge.to, dashed: edge.dashed, position: edge.position, count: 1,
      });
    }
    for (const fold of new Set(fromFold(edge.from))) {
      const id = `${fold}>${edge.to}`;
      const found = folded.get(id);
      if (found) { found.count += 1; found.dashed = found.dashed && edge.dashed; continue; }
      const made: BandEdge = { id, edge: edge.id, from: fold, to: edge.to, dashed: edge.dashed, position: edge.position, count: 1 };
      folded.set(id, made);
      edges.push(made);
    }
  }

  // The agents, once each, by the mean band of the roles that lean on them; a tie in declared order, one nothing leans on last.
  const declared = new Map(diagram.nodes.map((node, index) => [node.id, index]));
  const standsIn = new Map<string, number>([...bandAt, ...bands.flatMap((band, index) => band.cells.map((cell) => [cell.key, index] as const))]);
  const weight = (agent: string) => {
    const at = edges.filter((edge) => edge.to === agent).map((edge) => standsIn.get(edge.from) ?? 0);
    return at.length === 0 ? Infinity : at.reduce((sum, value) => sum + value, 0) / at.length;
  };
  const agents = diagram.nodes.filter((node) => node.kind === 'agent').map((node) => node.id)
    .sort((a, b) => (weight(a) === weight(b) ? 0 : weight(a) - weight(b)) || declared.get(a)! - declared.get(b)!);
  return { bands, agents, edges, foldOf };
}

/** The nodes in reading order: each band's label then its roles, top to bottom, then the agents. */
export function readingOrder(drawn: Bands): string[] {
  return [...drawn.bands.flatMap((band) => [band.key, ...band.cells.map((cell) => cell.key)]), ...drawn.agents];
}
