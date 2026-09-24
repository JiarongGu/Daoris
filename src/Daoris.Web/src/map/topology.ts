/**
 * The workspace topology (MAP2, D67 §3; `docs/2026-09-23-map-design.md` §1): the circle's
 * repositories, and what actually moved between them.
 *
 * @remarks
 * Pure functions over what the service already serves: the registry for the nodes, the quest store for
 * the directed edges, the convergence detector for the undirected ones, the session records for
 * *working now*. No model and no machine path, so the map is the same in a browser, on the desktop,
 * and against a shared deployment (D47 §4).
 *
 * **Declarations are not edges.** What a repository says it owns and accepts is in a node's detail;
 * the edges are what happened. A map that drew both would draw the intent and the fact as the same
 * line.
 */
import type { Convergence, Quest, Registration, Session } from '../api';

export type MapNode = {
  id: string;
  summary?: string;
  owns: string[];
  accepts: string[];
  /** Quests to it still open or taken — wherever they came from. */
  open: number;
  /** Whether a session is working there now. */
  working: boolean;
  /**
   * Whether a session there is parked on the person — its own fact, and the one the person acts on.
   * It held the tree and was ringed "working now" until the window showed it (POLISH4).
   */
  parked: boolean;
};

/** Every quest that went from one repository on the map to another, one edge per direction. */
export type QuestEdge = { from: string; to: string; quests: Quest[]; open: number };

/** Two repositories that learned the same thing in different words (D17), once per pair. */
export type KnowledgeEdge = { a: string; b: string; groups: number };

export type Topology = {
  nodes: MapNode[];
  quests: QuestEdge[];
  knowledge: KnowledgeEdge[];
  /** Quests with an end that is not a repository on this map: an ask, or another circle. */
  outside: number;
};

const isOpen = (quest: Quest) => quest.status === 'Open' || quest.status === 'Taken';
const WORKING: ReadonlySet<Session['state']> = new Set(['starting', 'working']);

export function buildTopology(
  registry: readonly Registration[],
  quests: readonly Quest[],
  convergence: readonly Convergence[],
  sessions: readonly Session[],
): Topology {
  const ids = new Set(registry.map((row) => row.repository));
  const nodes = [...registry]
    .sort((a, b) => a.repository.localeCompare(b.repository))
    .map<MapNode>((row) => ({
      id: row.repository,
      ...(row.summary ? { summary: row.summary } : {}),
      owns: row.owns,
      accepts: row.accepts,
      open: quests.filter((q) => q.to === row.repository && isOpen(q)).length,
      working: sessions.some((s) => s.repository === row.repository && WORKING.has(s.state)),
      parked: sessions.some((s) => s.repository === row.repository && s.state === 'awaiting-person'),
    }));

  let outside = 0;
  const edges = new Map<string, QuestEdge>();
  for (const q of quests) {
    if (!ids.has(q.from) || !ids.has(q.to)) {
      outside += 1;
      continue;
    }
    // A repository's quest to itself moved nothing between two places; it is its own count above.
    if (q.from === q.to) continue;
    const key = `${q.from}\u0000${q.to}`;
    const edge = edges.get(key) ?? { from: q.from, to: q.to, quests: [], open: 0 };
    edge.quests.push(q);
    if (isOpen(q)) edge.open += 1;
    edges.set(key, edge);
  }

  const pairs = new Map<string, KnowledgeEdge>();
  for (const found of convergence) {
    const on = [...new Set(found.repositories)].filter((r) => ids.has(r)).sort();
    for (let i = 0; i < on.length; i += 1) {
      for (let j = i + 1; j < on.length; j += 1) {
        const key = `${on[i]}\u0000${on[j]}`;
        const pair = pairs.get(key) ?? { a: on[i]!, b: on[j]!, groups: 0 };
        pair.groups += 1;
        pairs.set(key, pair);
      }
    }
  }

  const byEnds = (a: string, b: string) => a.localeCompare(b);
  return {
    nodes,
    quests: [...edges.values()].sort((x, y) => byEnds(x.from, y.from) || byEnds(x.to, y.to)),
    knowledge: [...pairs.values()].sort((x, y) => byEnds(x.a, y.a) || byEnds(x.b, y.b)),
    outside,
  };
}

/**
 * Positions on a ring inside a `size` square, in the order given, starting at the top. Deterministic,
 * so two looks at the same data draw the same picture. A family is small, which is why a ring is
 * enough and no layout engine is carried (design §1).
 */
export function layoutRing(ids: readonly string[], size: number): Record<string, { x: number; y: number }> {
  const centre = size / 2;
  if (ids.length === 0) return {};
  if (ids.length === 1) return { [ids[0]!]: { x: centre, y: centre } };

  const radius = centre * 0.68;
  return Object.fromEntries(ids.map((id, index) => {
    const angle = -Math.PI / 2 + (2 * Math.PI * index) / ids.length;
    return [id, { x: centre + radius * Math.cos(angle), y: centre + radius * Math.sin(angle) }];
  }));
}
