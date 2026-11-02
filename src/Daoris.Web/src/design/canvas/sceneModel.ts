import { type Aggregate, type Layout, NODE_H, NODE_W, type PlacedGroup, type Scene } from './layoutScene';
import { OVERVIEW_PAST } from './tiers';
import type { Box } from './viewMath';

// A laid-out scene as the canvas reads it (CANVAS1d; the canvas design §4): every node's box, which of them are shown (a grouped world
// past 1,000 nodes shows its groups, and one group opened at a time shows its nodes), and the edges each node holds.

export const GROUP = 'group:';

export type SceneModel = {
  scene: Scene;
  layout: Layout;
  nodeBoxes: Map<string, Box>;
  indexOf: Map<string, number>;
  groupOf: Map<string, string>;
  groupBoxes: Map<string, Box>;
  groups: Map<string, PlacedGroup>;
  /** Whether a closed group is a card: the world is grouped and past the overview bound. */
  overview: boolean;
  edgesByNode: Map<string, number[]>;
};

export function buildModel(scene: Scene, layout: Layout): SceneModel {
  const nodeBoxes = new Map<string, Box>();
  const indexOf = new Map<string, number>();
  const groupOf = new Map<string, string>();
  scene.nodes.forEach((node, index) => {
    nodeBoxes.set(node.id, { x: layout.at[index * 2]!, y: layout.at[index * 2 + 1]!, w: NODE_W, h: NODE_H });
    indexOf.set(node.id, index);
    if (node.group !== undefined && layout.groups.length > 0) groupOf.set(node.id, node.group);
  });
  const edgesByNode = new Map<string, number[]>();
  scene.edges.forEach((edge, index) => {
    for (const end of edge.from === edge.to ? [edge.from] : [edge.from, edge.to]) {
      const held = edgesByNode.get(end);
      if (held) held.push(index);
      else edgesByNode.set(end, [index]);
    }
  });
  return {
    scene, layout, nodeBoxes, indexOf, groupOf, edgesByNode,
    groups: new Map(layout.groups.map((group) => [group.id, group])),
    groupBoxes: new Map(layout.groups.map((group) => [group.id, { x: group.x, y: group.y, w: group.w, h: group.h }])),
    overview: layout.groups.length > 1 && scene.nodes.length > OVERVIEW_PAST,
  };
}

export type Display = {
  /** Every shown item's box, nodes by id and a closed group's card by `group:<id>`. */
  boxes: Map<string, Box>;
  /** The shown items in reading order. */
  ids: string[];
  at: Map<string, number>;
  /** The groups drawn as frames: all when the world is not an overview, else the opened one. */
  frames: { group: PlacedGroup; opened: boolean }[];
  /** The aggregates between groups drawn as lines: those of an overview. */
  aggregates: Aggregate[];
};

/** What the model shows with `opened` the group opened in an overview (null for none). */
export function displayOf(model: SceneModel, opened: string | null): Display {
  const boxes = new Map<string, Box>();
  const frames: Display['frames'] = [];
  if (!model.overview) {
    for (const [id, box] of model.nodeBoxes) boxes.set(id, box);
    for (const group of model.layout.groups) frames.push({ group, opened: false });
    return finish(boxes, frames, []);
  }
  const showing = opened !== null && model.groups.has(opened) ? opened : null;
  for (const group of model.layout.groups) {
    if (group.id !== showing) boxes.set(GROUP + group.id, model.groupBoxes.get(group.id)!);
    else frames.push({ group, opened: true });
  }
  if (showing !== null) for (const [id, box] of model.nodeBoxes) if (model.groupOf.get(id) === showing) boxes.set(id, box);
  return finish(boxes, frames, model.layout.aggregates);
}

function finish(boxes: Map<string, Box>, frames: Display['frames'], aggregates: Aggregate[]): Display {
  const ids = [...boxes.keys()];
  return { boxes, ids, at: new Map(ids.map((id, index) => [id, index + 1])), frames, aggregates };
}

/**
 * The edges (indexes into the scene's) held by any of `ids` with both ends shown, stopping once there are more than `cap`: the number
 * is what the budget asks, and the cap keeps the asking cheap.
 */
export function edgesAmong(model: SceneModel, display: Display, ids: Iterable<string>, cap: number): Set<number> {
  const found = new Set<number>();
  for (const id of ids) {
    for (const index of model.edgesByNode.get(id) ?? []) {
      if (found.has(index)) continue;
      const edge = model.scene.edges[index]!;
      if (display.boxes.has(edge.from) && display.boxes.has(edge.to)) found.add(index);
      if (found.size > cap) return found;
    }
  }
  return found;
}
