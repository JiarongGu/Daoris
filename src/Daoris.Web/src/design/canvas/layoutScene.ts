// The layout of a large positioned world (CANVAS1d; the canvas design §4): pure and synchronous, so it runs inline for a small scene and
// in a worker for a large one (`layoutClient.ts`). A scene is nodes in groups and the edges between them; each group is layered on its
// own (ranks by longest path with cycles broken, order by barycentre sweeps), and the groups are packed in shelves, each at a size its
// member count predicts, so an overview needs nothing measured.

export type SceneNode = { id: string; title: string; group?: string };
export type SceneEdge = { id: string; from: string; to: string; kind?: string };
export type SceneGroup = { id: string; title: string };
export type Scene = { nodes: SceneNode[]; edges: SceneEdge[]; groups?: SceneGroup[] };

/** A node's size in world units, and the gaps between nodes. */
export const NODE_W = 160;
export const NODE_H = 56;
const GAP_X = 24;
const GAP_Y = 40;
/** A group's padding, and the height of its title above its nodes. */
const PAD = 32;
const TITLE = 28;
/** Most nodes in a row of a rank; a wider rank wraps into more rows. */
const WIDE = 12;

export type PlacedGroup = { id: string; title: string; x: number; y: number; w: number; h: number; count: number };
/** Edges between two groups, summed by direction and kind: the exact count and every member's id. */
export type Aggregate = { id: string; from: string; to: string; kind: string; count: number; members: string[] };

export type Layout = {
  revision: number;
  width: number;
  height: number;
  /** In scene order: x, y of each node's box; every box is NODE_W × NODE_H. */
  ids: string[];
  at: Float64Array;
  groups: PlacedGroup[];
  aggregates: Aggregate[];
};

type Local = { x: number; y: number };

/** Layers `count` nodes joined by `pairs` (indexes): ranks by longest path with back edges ignored, then two barycentre sweeps. */
function layer(count: number, pairs: [number, number][]): { places: Local[]; w: number; h: number } {
  const out: number[][] = Array.from({ length: count }, () => []);
  for (const [from, to] of pairs) if (from !== to) out[from]!.push(to);
  // Cycles broken by an iterative depth-first search: an edge to a node on the stack is a back edge and is left out of the ranks.
  const state = new Uint8Array(count);
  const kept: number[][] = Array.from({ length: count }, () => []);
  const indegree = new Int32Array(count);
  for (let root = 0; root < count; root += 1) {
    if (state[root]) continue;
    const stack: [number, number][] = [[root, 0]];
    state[root] = 1;
    while (stack.length > 0) {
      const top = stack[stack.length - 1]!;
      const next = out[top[0]]![top[1]];
      if (next === undefined) { state[top[0]] = 2; stack.pop(); continue; }
      top[1] += 1;
      if (state[next] === 1) continue;
      kept[top[0]]!.push(next);
      indegree[next]! += 1;
      if (state[next] === 0) { state[next] = 1; stack.push([next, 0]); }
    }
  }
  const rank = new Int32Array(count);
  const queue: number[] = [];
  const degree = Int32Array.from(indegree);
  for (let at = 0; at < count; at += 1) if (degree[at] === 0) queue.push(at);
  for (let head = 0; head < queue.length; head += 1) {
    const node = queue[head]!;
    for (const next of kept[node]!) {
      rank[next] = Math.max(rank[next]!, rank[node]! + 1);
      degree[next]! -= 1;
      if (degree[next] === 0) queue.push(next);
    }
  }
  const ranks: number[][] = [];
  for (let at = 0; at < count; at += 1) (ranks[rank[at]!] ??= []).push(at);
  const slot = new Float64Array(count);
  ranks.forEach((members) => members.forEach((node, index) => { slot[node] = index; }));
  const preds: number[][] = Array.from({ length: count }, () => []);
  kept.forEach((list, from) => list.forEach((to) => preds[to]!.push(from)));
  const mean = (node: number, list: number[]) => (list.length === 0 ? slot[node]! : list.reduce((sum, other) => sum + slot[other]!, 0) / list.length);
  for (let sweep = 0; sweep < 2; sweep += 1) {
    for (let at = 1; at < ranks.length; at += 1) {
      const keys = new Map(ranks[at]!.map((node) => [node, mean(node, preds[node]!)] as const));
      ranks[at]!.sort((a, b) => keys.get(a)! - keys.get(b)!);
      ranks[at]!.forEach((node, index) => { slot[node] = index; });
    }
    for (let at = ranks.length - 2; at >= 0; at -= 1) {
      const keys = new Map(ranks[at]!.map((node) => [node, mean(node, kept[node]!)] as const));
      ranks[at]!.sort((a, b) => keys.get(a)! - keys.get(b)!);
      ranks[at]!.forEach((node, index) => { slot[node] = index; });
    }
  }
  const places: Local[] = new Array<Local>(count);
  let y = 0;
  let widest = 0;
  for (const members of ranks) {
    if (!members) continue;
    members.forEach((node, index) => {
      places[node] = { x: (index % WIDE) * (NODE_W + GAP_X), y: y + Math.floor(index / WIDE) * (NODE_H + GAP_Y) };
    });
    widest = Math.max(widest, Math.min(members.length, WIDE));
    y += Math.ceil(members.length / WIDE) * (NODE_H + GAP_Y);
  }
  return { places, w: Math.max(0, widest * (NODE_W + GAP_X) - GAP_X), h: Math.max(0, y - GAP_Y) };
}

/** A scene laid out whole: every node placed, every group boxed, the edges between groups aggregated. */
export function layoutScene(scene: Scene, revision = 0): Layout {
  const grouped = (scene.groups?.length ?? 0) > 1;
  const groupIds = grouped ? scene.groups!.map((group) => group.id) : [''];
  const titles = new Map(scene.groups?.map((group) => [group.id, group.title]) ?? []);
  const members = new Map<string, number[]>(groupIds.map((id) => [id, []]));
  const groupOf: string[] = new Array<string>(scene.nodes.length);
  scene.nodes.forEach((node, index) => {
    const id = grouped && node.group !== undefined && members.has(node.group) ? node.group : groupIds[0]!;
    groupOf[index] = id;
    members.get(id)!.push(index);
  });
  const indexOf = new Map(scene.nodes.map((node, index) => [node.id, index] as const));
  const local = new Map<string, Local[]>();
  const sizes = new Map<string, { w: number; h: number }>();
  const within = new Map<string, [number, number][]>(groupIds.map((id) => [id, []]));
  const aggregates = new Map<string, Aggregate>();
  const position = new Int32Array(scene.nodes.length);
  members.forEach((list) => list.forEach((node, at) => { position[node] = at; }));
  for (const edge of scene.edges) {
    const [from, to] = [indexOf.get(edge.from), indexOf.get(edge.to)];
    if (from === undefined || to === undefined) continue;
    if (groupOf[from] === groupOf[to]) within.get(groupOf[from]!)!.push([position[from]!, position[to]!]);
    else {
      const kind = edge.kind ?? '';
      const key = `${groupOf[from]}\u0000${groupOf[to]}\u0000${kind}`;
      const held = aggregates.get(key);
      if (held) { held.count += 1; held.members.push(edge.id); }
      else aggregates.set(key, { id: key, from: groupOf[from]!, to: groupOf[to]!, kind, count: 1, members: [edge.id] });
    }
  }
  for (const id of groupIds) {
    const laid = layer(members.get(id)!.length, within.get(id)!);
    local.set(id, laid.places);
    sizes.set(id, grouped ? { w: laid.w + 2 * PAD, h: laid.h + 2 * PAD + TITLE } : { w: laid.w, h: laid.h });
  }
  // Shelves: groups left to right to a width the total area suggests, then the next shelf below.
  const area = groupIds.reduce((sum, id) => sum + sizes.get(id)!.w * sizes.get(id)!.h, 0);
  const target = Math.max(...groupIds.map((id) => sizes.get(id)!.w), Math.sqrt(area) * 1.6);
  const placed: PlacedGroup[] = [];
  let [x, y, shelf, width] = [0, 0, 0, 0];
  for (const id of groupIds) {
    const size = sizes.get(id)!;
    if (x > 0 && x + size.w > target) { x = 0; y += shelf + GAP_Y; shelf = 0; }
    placed.push({ id, title: titles.get(id) ?? '', x, y, w: size.w, h: size.h, count: members.get(id)!.length });
    x += size.w + GAP_X;
    shelf = Math.max(shelf, size.h);
    width = Math.max(width, x - GAP_X);
  }
  const at = new Float64Array(scene.nodes.length * 2);
  placed.forEach((box) => {
    const origin = { x: box.x + (grouped ? PAD : 0), y: box.y + (grouped ? PAD + TITLE : 0) };
    const places = local.get(box.id)!;
    members.get(box.id)!.forEach((node, index) => {
      at[node * 2] = origin.x + places[index]!.x;
      at[node * 2 + 1] = origin.y + places[index]!.y;
    });
  });
  return {
    revision, width, height: y + shelf, ids: scene.nodes.map((node) => node.id), at, groups: grouped ? placed : [], aggregates: [...aggregates.values()],
  };
}
