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
import type { Convergence, Quest, QuestStep, Registration, Session } from '../api';
import { answeredPark } from '../ui';

export type MapNode = {
  id: string;
  /** The circle it belongs to, as the registry says; said in its detail when the map spans several. */
  workspace?: string;
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
  /** How many sessions are there now, working or waiting on the person (MAP4d). */
  sessions: number;
};

/**
 * Whether a session is on one of a line's quests now (MAP4d): waiting on the person leads, as it does
 * on a node, since it is the one the person acts on; else working; else none.
 */
export type Live = 'parked' | 'working' | null;

/** Every quest that went from one repository on the map to another, one edge per direction. */
export type QuestEdge = { from: string; to: string; quests: Quest[]; open: number; live: Live };

/** Two repositories that learned the same thing in different words (D17), once per pair. */
export type KnowledgeEdge = { a: string; b: string; groups: number };

/**
 * What the asks became on one repository (MAP4b): every quest an ask put on it, directly or as a
 * later step, since a step is published on behalf of the same asker. The asks are one source on the
 * map, the person's, and which asks is the line's detail.
 */
export type AskEdge = { to: string; asks: string[]; quests: Quest[]; open: number; live: Live };

/**
 * One hop of a chain (MAP4b, D65 §4): the repository whose done quest published the next step, to
 * that step's repository. A step keeps the original asker as its sender, so no quest line draws this
 * flow. `waiting` holds the steps an open quest will publish when it closes done.
 */
export type ChainEdge = { from: string; to: string; steps: Quest[]; waiting: QuestStep[]; live: Live };

/**
 * What a repository says it uses (MAP4e, D91): its own declaration, `domain.uses`, drawn from it to
 * each repository it names on the map. A declaration, not something that happened, so it is a line of
 * its own kind, never merged with the quests.
 */
export type DependsEdge = { from: string; to: string };

/**
 * The asks' place on a map (MAP4b): one source, the person's, from which each repository an ask put
 * work on is reached. Its id holds a character no repository's name can. The layers stand it in the
 * first column, as any asker; the ring puts it at the centre.
 */
export const ASKS = '\u0001asks';

/** The kinds of line a person can show or hide, in the order the switches list them. */
export const LINE_KINDS = ['quests', 'asks', 'chains', 'depends', 'knowledge'] as const;
export type LineKind = (typeof LINE_KINDS)[number];

export type Topology = {
  nodes: MapNode[];
  quests: QuestEdge[];
  asks: AskEdge[];
  chains: ChainEdge[];
  depends: DependsEdge[];
  knowledge: KnowledgeEdge[];
  /** Quests with an end that is not a repository on this map, nor an ask: another circle. */
  outside: number;
  /**
   * How many circles the nodes belong to: more than one only when the page is scoped to every
   * workspace and the deployment holds several (UX5 U49).
   */
  circles: number;
};

const isOpen = (quest: Quest) => quest.status === 'Open' || quest.status === 'Taken';
const WORKING: ReadonlySet<Session['state']> = new Set(['starting', 'working']);
/**
 * Parked on the person. Not a park they answered (ANSWER1e): the same session goes on with it at the driver's next look,
 * so until then it is marked as a queued session is, neither parked nor working.
 */
const parkedOnPerson = (s: Session) => s.state === 'awaiting-person' && !answeredPark(s);
/** The sender every quest an ask becomes is published by (the service's `AskDesk.SenderOf`). */
const ASK = /^ask #(\S+)$/;

/**
 * Which quests the map draws (MAP4c), so a busy circle stays readable: all of them, the open ones, or
 * what moved within a week or a month, by when each quest last moved.
 */
export const WHENS = ['all', 'open', 'week', 'month'] as const;
export type When = (typeof WHENS)[number];
const DAY = 24 * 60 * 60 * 1000;

export function keepQuests(when: When, now: number): (quest: Quest) => boolean {
  if (when === 'open') return isOpen;
  if (when === 'all') return () => true;
  const since = now - (when === 'week' ? 7 : 30) * DAY;
  return (quest) => Date.parse(quest.updated) >= since;
}

/**
 * @param keep - which quests the lines draw (`keepQuests`). A repository's open count is what is open
 *   to it now, whatever is kept, and a kept step still finds its parent among every quest.
 */
export function buildTopology(
  registry: readonly Registration[],
  quests: readonly Quest[],
  convergence: readonly Convergence[],
  sessions: readonly Session[],
  keep: (quest: Quest) => boolean = () => true,
): Topology {
  const ids = new Set(registry.map((row) => row.repository));
  const circles = new Set(registry.map((row) => row.workspace ?? '')).size;
  // By name, and by circle first when there are several, so a circle's repositories sit together on
  // the ring and its quests stay within its arc: by name alone the circles interleaved (UX5 U49).
  const circleOf = (row: Registration) => (circles > 1 ? row.workspace ?? '' : '');
  const nodes = [...registry]
    .sort((a, b) => circleOf(a).localeCompare(circleOf(b)) || a.repository.localeCompare(b.repository))
    .map<MapNode>((row) => ({
      id: row.repository,
      ...(row.workspace ? { workspace: row.workspace } : {}),
      ...(row.summary ? { summary: row.summary } : {}),
      owns: row.owns,
      accepts: row.accepts,
      open: quests.filter((q) => q.to === row.repository && isOpen(q)).length,
      working: sessions.some((s) => s.repository === row.repository && WORKING.has(s.state)),
      parked: sessions.some((s) => s.repository === row.repository && parkedOnPerson(s)),
      sessions: sessions.filter((s) => s.repository === row.repository && (WORKING.has(s.state) || parkedOnPerson(s))).length,
    }));

  // Which quests a session is on now, and how (MAP4d): waiting on the person leads.
  const onQuest = new Map<string, Live>();
  for (const s of sessions) {
    if (!s.quest) continue;
    if (parkedOnPerson(s)) onQuest.set(s.quest, 'parked');
    else if (WORKING.has(s.state) && onQuest.get(s.quest) !== 'parked') onQuest.set(s.quest, 'working');
  }
  const liveOf = (list: readonly Quest[]): Live => {
    const states = list.map((q) => onQuest.get(q.id));
    return states.includes('parked') ? 'parked' : states.includes('working') ? 'working' : null;
  };

  let outside = 0;
  const edges = new Map<string, QuestEdge>();
  const asked = new Map<string, AskEdge>();
  const kept = quests.filter(keep);
  for (const q of kept) {
    const ask = ASK.exec(q.from);
    if (ask && ids.has(q.to)) {
      const edge = asked.get(q.to) ?? { to: q.to, asks: [], quests: [], open: 0, live: null };
      if (!edge.asks.includes(ask[1]!)) edge.asks.push(ask[1]!);
      edge.quests.push(q);
      if (isOpen(q)) edge.open += 1;
      asked.set(q.to, edge);
      continue;
    }
    if (!ids.has(q.from) || !ids.has(q.to)) {
      outside += 1;
      continue;
    }
    // A repository's quest to itself moved nothing between two places; it is its own count above.
    if (q.from === q.to) continue;
    const key = `${q.from}\u0000${q.to}`;
    const edge = edges.get(key) ?? { from: q.from, to: q.to, quests: [], open: 0, live: null };
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

  // The chains: each step from its parent's repository, and what an open quest will still publish.
  // A closed quest's `then` is not still to come: a done close published its first step (which
  // carries the rest), and a decline ended the chain.
  const byId = new Map(quests.map((q) => [q.id, q]));
  const hops = new Map<string, ChainEdge>();
  const hop = (from: string, to: string) => {
    const key = `${from}\u0000${to}`;
    const edge = hops.get(key) ?? { from, to, steps: [], waiting: [], live: null };
    hops.set(key, edge);
    return edge;
  };
  const onMap = (from: string, to: string) => ids.has(from) && ids.has(to) && from !== to;
  for (const q of kept) {
    const parent = q.parent ? byId.get(q.parent) : undefined;
    if (parent && onMap(parent.to, q.to)) hop(parent.to, q.to).steps.push(q);
    if (!isOpen(q)) continue;
    let from = q.to;
    for (const step of q.then ?? []) {
      if (onMap(from, step.to)) hop(from, step.to).waiting.push(step);
      from = step.to;
    }
  }

  // What each says it uses, to a repository on this map; a name the registry does not hold draws
  // nothing, since the host has already read the list by its rule (D91).
  const depends: DependsEdge[] = registry.flatMap((row) =>
    (row.uses ?? []).filter((to) => ids.has(to) && to !== row.repository).map((to) => ({ from: row.repository, to })));

  for (const edge of edges.values()) edge.live = liveOf(edge.quests);
  for (const edge of asked.values()) edge.live = liveOf(edge.quests);
  for (const edge of hops.values()) edge.live = liveOf(edge.steps);

  const byEnds = (a: string, b: string) => a.localeCompare(b);
  return {
    nodes,
    quests: [...edges.values()].sort((x, y) => byEnds(x.from, y.from) || byEnds(x.to, y.to)),
    asks: [...asked.values()].sort((x, y) => byEnds(x.to, y.to)),
    chains: [...hops.values()].sort((x, y) => byEnds(x.from, y.from) || byEnds(x.to, y.to)),
    depends: depends.sort((x, y) => byEnds(x.from, y.from) || byEnds(x.to, y.to)),
    knowledge: [...pairs.values()].sort((x, y) => byEnds(x.a, y.a) || byEnds(x.b, y.b)),
    outside,
    circles,
  };
}

/**
 * The map with only the kinds of line a person chose (MAP4b). Every repository stays: hiding a kind of
 * line hides what moved, never who is in the circle.
 */
export function showLines(topology: Topology, shown: ReadonlySet<LineKind>): Topology {
  return {
    ...topology,
    quests: shown.has('quests') ? topology.quests : [],
    asks: shown.has('asks') ? topology.asks : [],
    chains: shown.has('chains') ? topology.chains : [],
    depends: shown.has('depends') ? topology.depends : [],
    knowledge: shown.has('knowledge') ? topology.knowledge : [],
  };
}

/**
 * Positions on a ring about the centre of a `size` square, in the order given, starting at the top.
 * Deterministic, so two looks at the same data draw the same picture. A family is small, which is
 * why a ring is enough and no layout engine is carried (design §1).
 *
 * @param radius - the ring's radius; the square's 0.68 by default. The canvas passes a smaller one
 *   when its card is narrow, so the ring gives way rather than the names (UX5 U44).
 */
export function layoutRing(
  ids: readonly string[], size: number, radius = (size / 2) * 0.68,
): Record<string, { x: number; y: number }> {
  const centre = size / 2;
  if (ids.length === 0) return {};
  if (ids.length === 1) return { [ids[0]!]: { x: centre, y: centre } };

  return Object.fromEntries(ids.map((id, index) => {
    const angle = -Math.PI / 2 + (2 * Math.PI * index) / ids.length;
    return [id, { x: centre + radius * Math.cos(angle), y: centre + radius * Math.sin(angle) }];
  }));
}
