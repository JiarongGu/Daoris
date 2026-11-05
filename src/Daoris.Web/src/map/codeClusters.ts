import type { CodeDependency, CodeModule } from '../data/api';
import type { ListGroup } from '../design/canvas/CanvasList';
import type { SuppliedWorld } from '../design/canvas/SuppliedCanvas';
import type { Box } from '../design/canvas/viewMath';
import { layerModules } from './codeLayout';
import { createRouter } from './codeRoute';

// The code map as a positioned world (CANVAS1f; D224; the canvas design §3.4). Pure. Folders come from each module's path and are drawn as
// nested boxes packed by name; the modules of a folder are laid by `layerModules`; a cycle is condensed to one card and whole when
// opened. A folder is no evidence of an import: the only lines are the supplied dependencies, each kind as written, and those between
// closed groups are aggregated by pair and direction and counted until one opens. Each line is routed around the cards (`codeRoute`).

/** A card, in world units: the 136×36 box the code map always drew. */
export const CARD_W = 136;
export const CARD_H = 36;
/** Past this many modules the folders open closed. */
export const CLOSED_PAST = 60;
export const ROOT = 'folder:';

const GAP_X = 16;
const GAP_Y = 28;
const PAD = 12;
const TITLE = 28;
const MARGIN = 24;
/** How wide a folder's rows run before they wrap, in world units. */
const MAX_LINE = 1200;
const LANE = 8;

export type CodeGroup = {
  id: string;
  kind: 'folder' | 'cycle';
  /** A folder's name from its parent (joined where a folder holds only one), empty for a cycle and the root. */
  name: string;
  /** The folder's path, repository-relative. */
  dir: string;
  parent: string | null;
  /** Child groups in name order. */
  groups: string[];
  /** The modules it holds directly. */
  modules: string[];
  /** Every module beneath it. */
  members: string[];
  /** The folders a cycle's modules come from, where it crosses more than one. */
  across?: string[];
};

export type CodeTree = {
  groups: Map<string, CodeGroup>;
  /** The group each module is held directly by. */
  owner: Map<string, string>;
  modules: Map<string, CodeModule>;
  /** Only the dependencies whose two ends the map holds, once each. */
  dependencies: CodeDependency[];
};

/** One drawn line: a dependency between two modules, or the dependencies between two cards of which one is a closed group. */
export type CodeLine = {
  id: string;
  from: string;
  to: string;
  d: string;
  mid: { x: number; y: number };
  count: number;
  aggregated: boolean;
  /** The kind of each dependency it stands for, as the producer wrote it. */
  kinds: string[];
  deps: CodeDependency[];
};

export type CodeScene = {
  world: SuppliedWorld;
  lines: CodeLine[];
  /** The card each module stands for now: itself, or the closed group that holds it. */
  standsFor: Map<string, string>;
};

const byName = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

function dirOf(path: string): string {
  const clean = path.replace(/\\/g, '/').replace(/^\.\//, '').replace(/\/+$/, '');
  const cut = clean.lastIndexOf('/');
  return cut < 0 ? '' : clean.slice(0, cut);
}

/** The deepest folder that holds every one of `dirs`: their shared leading path segments. */
function commonDir(dirs: readonly string[]): string {
  const [first, ...rest] = dirs.map((dir) => (dir === '' ? [] : dir.split('/')));
  let shared = first ?? [];
  for (const parts of rest) {
    let at = 0;
    while (at < shared.length && at < parts.length && shared[at] === parts[at]) at += 1;
    shared = shared.slice(0, at);
  }
  return shared.join('/');
}

/** The strongly connected components of two or more modules (Tarjan, without recursion). */
function components(ids: readonly string[], edges: readonly CodeDependency[]): string[][] {
  const next = new Map<string, string[]>(ids.map((id) => [id, []]));
  for (const edge of edges) next.get(edge.from)!.push(edge.to);
  const index = new Map<string, number>();
  const low = new Map<string, number>();
  const stacked = new Set<string>();
  const stack: string[] = [];
  const found: string[][] = [];
  let counter = 0;
  for (const root of ids) {
    if (index.has(root)) continue;
    const work: { id: string; at: number }[] = [{ id: root, at: 0 }];
    index.set(root, counter); low.set(root, counter); counter += 1; stack.push(root); stacked.add(root);
    while (work.length > 0) {
      const frame = work[work.length - 1]!;
      const targets = next.get(frame.id)!;
      if (frame.at < targets.length) {
        const to = targets[frame.at]!;
        frame.at += 1;
        if (!index.has(to)) {
          index.set(to, counter); low.set(to, counter); counter += 1; stack.push(to); stacked.add(to);
          work.push({ id: to, at: 0 });
        } else if (stacked.has(to)) {
          low.set(frame.id, Math.min(low.get(frame.id)!, index.get(to)!));
        }
        continue;
      }
      work.pop();
      const parent = work[work.length - 1];
      if (parent) low.set(parent.id, Math.min(low.get(parent.id)!, low.get(frame.id)!));
      if (low.get(frame.id) === index.get(frame.id)) {
        const part: string[] = [];
        let each: string;
        do { each = stack.pop()!; stacked.delete(each); part.push(each); } while (each !== frame.id);
        if (part.length > 1) found.push(part);
      }
    }
  }
  return found;
}

/**
 * The folders of a repository's modules, with each cycle held as a group of its own: in its folder, or, where it crosses folders, in
 * the nearest folder that holds them all, condensed there for ranking and whole when opened.
 */
export function buildTree(input: readonly CodeModule[], supplied: readonly CodeDependency[]): CodeTree {
  const modules = new Map<string, CodeModule>();
  for (const module of input) if (!modules.has(module.id)) modules.set(module.id, module);
  const seen = new Set<string>();
  const dependencies = supplied.filter((edge) => {
    const key = `${edge.from}\u0000${edge.to}\u0000${edge.kind}`;
    if (edge.from === edge.to || !modules.has(edge.from) || !modules.has(edge.to) || seen.has(key)) return false;
    seen.add(key);
    return true;
  });

  const groups = new Map<string, CodeGroup>();
  const make = (id: string, kind: CodeGroup['kind'], name: string, dir: string, parent: string | null): CodeGroup => {
    const group: CodeGroup = { id, kind, name, dir, parent, groups: [], modules: [], members: [] };
    groups.set(id, group);
    return group;
  };
  make(ROOT, 'folder', '', '', null);
  const folder = (dir: string): CodeGroup => {
    const held = groups.get(`folder:${dir}`);
    if (held) return held;
    const cut = dir.lastIndexOf('/');
    const above = folder(cut < 0 ? '' : dir.slice(0, cut));
    const group = make(`folder:${dir}`, 'folder', dir.slice(cut + 1), dir, above.id);
    above.groups.push(group.id);
    return group;
  };

  const dirs = new Map([...modules.values()].map((module) => [module.id, dirOf(module.path)] as const));
  const inCycle = new Set<string>();
  for (const part of components([...modules.keys()], dependencies)) {
    const members = part.sort(byName);
    const across = [...new Set(members.map((id) => dirs.get(id)!))].sort(byName);
    // A cycle across folders is held by the nearest folder that holds them all, and says which folders it crosses.
    const dir = commonDir(across);
    const home = folder(dir);
    const cycle = make(`cycle:${dir}:${members[0]}`, 'cycle', '', dir, home.id);
    cycle.modules = members;
    if (across.length > 1) cycle.across = across;
    home.groups.push(cycle.id);
    for (const id of members) inCycle.add(id);
  }
  for (const module of modules.values()) if (!inCycle.has(module.id)) folder(dirs.get(module.id)!).modules.push(module.id);

  // A folder that holds nothing but one folder is joined to it; the root takes the contents of a lone top folder.
  const lone = (group: CodeGroup): CodeGroup | null => {
    const only = group.groups.length === 1 ? groups.get(group.groups[0]!)! : null;
    return group.modules.length === 0 && only?.kind === 'folder' ? only : null;
  };
  for (let again = true; again;) {
    again = false;
    const root = groups.get(ROOT)!;
    const top = lone(root);
    if (top) {
      root.modules = top.modules; root.groups = top.groups;
      for (const id of top.groups) groups.get(id)!.parent = ROOT;
      groups.delete(top.id);
      again = true;
      continue;
    }
    for (const group of [...groups.values()]) {
      if (group.id === ROOT || group.kind !== 'folder' || !groups.has(group.id)) continue;
      const child = lone(group);
      if (!child) continue;
      const above = groups.get(group.parent!)!;
      child.name = `${group.name}/${child.name}`;
      child.parent = above.id;
      above.groups[above.groups.indexOf(group.id)] = child.id;
      groups.delete(group.id);
      again = true;
    }
  }

  const owner = new Map<string, string>();
  const settle = (group: CodeGroup): string[] => {
    group.groups.sort((a, b) => byName(groups.get(a)!.name, groups.get(b)!.name) || byName(a, b));
    group.modules.sort(byName);
    for (const id of group.modules) owner.set(id, group.id);
    group.members = [...group.modules, ...group.groups.flatMap((id) => settle(groups.get(id)!))];
    return group.members;
  };
  settle(groups.get(ROOT)!);
  return { groups, owner, modules, dependencies };
}

/** The groups that start open: all of them up to `CLOSED_PAST` modules, none past it. */
export function defaultOpen(tree: CodeTree): Set<string> {
  return tree.modules.size <= CLOSED_PAST ? new Set([...tree.groups.keys()].filter((id) => id !== ROOT)) : new Set();
}

/** `open` with every group on the way to a module, or to a group, opened. */
export function openingPath(tree: CodeTree, open: ReadonlySet<string>, id: string): Set<string> {
  const next = new Set(open);
  let at: string | null | undefined = tree.groups.has(id) ? id : tree.owner.get(id);
  while (at && at !== ROOT) { next.add(at); at = tree.groups.get(at)?.parent; }
  return next;
}

type Part = { id: string; box: Box; inner?: Laid };
type Laid = { w: number; h: number; parts: Part[] };

/**
 * The world for the groups that are `open`: the root's folders packed by name, an open folder a frame holding its modules in
 * `layerModules`' layers (a cycle in it one condensed unit) and its subfolders packed beneath, a closed group one card.
 */
export function layoutCode(tree: CodeTree, open: ReadonlySet<string>, words: { cycle?: (count: number, folders: number) => string } = {}): CodeScene {
  const out = new Map<string, CodeDependency[]>();
  for (const edge of tree.dependencies) {
    const list = out.get(edge.from);
    if (list) list.push(edge); else out.set(edge.from, [edge]);
  }

  const laidGroup = (group: CodeGroup, root: boolean): Laid => {
    const [pad, title] = root ? [MARGIN, MARGIN] : [PAD, TITLE];
    const unitOf = new Map<string, string>(group.modules.map((id) => [id, id]));
    const cycles = group.groups.map((id) => tree.groups.get(id)!).filter((child) => child.kind === 'cycle');
    for (const cycle of cycles) for (const id of cycle.modules) unitOf.set(id, cycle.id);
    const units = [...group.modules, ...cycles.map((cycle) => cycle.id)];
    const ranking = new Set<string>();
    const edges: { from: string; to: string }[] = [];
    for (const id of unitOf.keys()) {
      for (const edge of out.get(id) ?? []) {
        const [from, to] = [unitOf.get(id)!, unitOf.get(edge.to)];
        if (to === undefined || from === to || ranking.has(`${from}\u0000${to}`)) continue;
        ranking.add(`${from}\u0000${to}`);
        edges.push({ from, to });
      }
    }

    const cell = (child: CodeGroup | undefined): { w: number; h: number; inner?: Laid } => {
      if (child && open.has(child.id)) { const inner = laidGroup(child, false); return { w: inner.w, h: inner.h, inner }; }
      return { w: CARD_W, h: CARD_H };
    };
    const perLine = Math.max(1, Math.floor((MAX_LINE + GAP_X) / (CARD_W + GAP_X)));
    const lines: { id: string; w: number; h: number; inner?: Laid }[][] = [];
    for (const row of units.length > 0 ? layerModules(units, edges) : []) {
      for (let at = 0; at < row.length; at += perLine) {
        lines.push(row.slice(at, at + perLine).map((id) => ({ id, ...cell(tree.groups.get(id)) })));
      }
    }
    const lineWidth = (cells: readonly { w: number }[]) => cells.reduce((sum, each) => sum + each.w, 0) + GAP_X * (cells.length - 1);
    const layersW = Math.max(0, ...lines.map(lineWidth));
    const folders = group.groups.map((id) => tree.groups.get(id)!).filter((child) => child.kind === 'folder')
      .map((child) => ({ id: child.id, ...cell(child) }));
    const area = folders.reduce((sum, each) => sum + (each.w + GAP_X) * (each.h + GAP_Y), 0);
    const target = Math.max(layersW, CARD_W, ...folders.map((each) => each.w), Math.ceil(Math.sqrt(area * 1.6)));

    const shelves: { id: string; w: number; h: number; inner?: Laid; x: number }[][] = [];
    let shelf: (typeof shelves)[number] = [];
    let used = 0;
    for (const each of folders) {
      if (shelf.length > 0 && used + GAP_X + each.w > target) { shelves.push(shelf); shelf = []; used = 0; }
      shelf.push({ ...each, x: used === 0 ? 0 : used + GAP_X });
      used = (used === 0 ? 0 : used + GAP_X) + each.w;
    }
    if (shelf.length > 0) shelves.push(shelf);
    const packW = Math.max(0, ...shelves.map((row) => row[row.length - 1]!.x + row[row.length - 1]!.w));
    const contentW = Math.max(layersW, packW, root ? 0 : CARD_W);

    const parts: Part[] = [];
    let y = 0;
    for (const cells of lines) {
      const rowH = Math.max(...cells.map((each) => each.h));
      let x = (contentW - lineWidth(cells)) / 2;
      for (const each of cells) {
        parts.push({ id: each.id, box: { x: pad + x, y: title + y, w: each.w, h: each.h }, ...(each.inner ? { inner: each.inner } : {}) });
        x += each.w + GAP_X;
      }
      y += rowH + GAP_Y;
    }
    for (const row of shelves) {
      const rowH = Math.max(...row.map((each) => each.h));
      for (const each of row) {
        parts.push({ id: each.id, box: { x: pad + each.x, y: title + y, w: each.w, h: each.h }, ...(each.inner ? { inner: each.inner } : {}) });
      }
      y += rowH + GAP_Y;
    }
    const contentH = Math.max(0, y - GAP_Y);
    return { w: contentW + pad * 2, h: title + contentH + pad, parts };
  };

  const laid = laidGroup(tree.groups.get(ROOT)!, true);
  const boxes = new Map<string, Box>();
  const order: string[] = [];
  const frames: { id: string; title: string; box: Box }[] = [];
  const standsFor = new Map<string, string>();
  const emit = (inside: Laid, ox: number, oy: number) => {
    for (const part of inside.parts) {
      const box = { x: ox + part.box.x, y: oy + part.box.y, w: part.box.w, h: part.box.h };
      const group = tree.groups.get(part.id);
      if (part.inner && group) {
        const cycleTitle = words.cycle ?? ((n) => `cycle ×${n}`);
        frames.push({ id: part.id, title: group.kind === 'cycle' ? cycleTitle(group.modules.length, group.across?.length ?? 1) : group.name, box });
        emit(part.inner, box.x, box.y);
        continue;
      }
      boxes.set(part.id, box);
      order.push(part.id);
      if (group) for (const id of group.members) standsFor.set(id, part.id); else standsFor.set(part.id, part.id);
    }
  };
  emit(laid, 0, 0);

  const aggregated = new Map<string, { from: string; to: string; deps: CodeDependency[] }>();
  const singles: { from: string; to: string; deps: CodeDependency[]; id: string }[] = [];
  for (const edge of tree.dependencies) {
    const [from, to] = [standsFor.get(edge.from), standsFor.get(edge.to)];
    if (from === undefined || to === undefined || from === to) continue;
    if (from === edge.from && to === edge.to) { singles.push({ id: `dep:${edge.from}>${edge.to}:${edge.kind}`, from, to, deps: [edge] }); continue; }
    const key = `agg:${from}>${to}`;
    const held = aggregated.get(key);
    if (held) held.deps.push(edge); else aggregated.set(key, { from, to, deps: [edge] });
  }
  const drawn = [...singles, ...[...aggregated].map(([id, each]) => ({ id, ...each }))];
  const pairs = new Set(drawn.map((each) => `${each.from}\u0000${each.to}`));
  const router = createRouter(boxes);
  const lines: CodeLine[] = drawn.map((each) => {
    const both = pairs.has(`${each.to}\u0000${each.from}`);
    const { d, mid } = router.route(each.from, each.to, both ? (each.from < each.to ? -LANE : LANE) : 0);
    return {
      id: each.id, from: each.from, to: each.to, d, mid, count: each.deps.length, aggregated: each.id.startsWith('agg:'),
      kinds: each.deps.map((dep) => dep.kind), deps: each.deps,
    };
  });

  const groupOf = (id: string): string | null => {
    let at: string | null | undefined = tree.owner.get(id) ?? tree.groups.get(id)?.parent;
    while (at && at !== ROOT && !open.has(at)) at = tree.groups.get(at)?.parent;
    return !at || at === ROOT ? null : at;
  };
  return {
    standsFor, lines,
    world: {
      frame: { x: 0, y: 0, w: Math.max(laid.w, 1), h: Math.max(laid.h, 1) },
      boxes, order, lines: lines.map(({ id, from, to }) => ({ id, from, to })),
      ...(frames.length > 0 ? { groups: frames } : {}), groupOf,
    },
  };
}

/** Every module under its folder, in folder-path order: the list a reader, a narrow window and Show as list get. */
export function listByFolder(tree: CodeTree): ListGroup[] {
  const held = new Map<string, string[]>();
  for (const [id, group] of tree.owner) {
    let home = tree.groups.get(group)!;
    if (home.kind === 'cycle') home = tree.groups.get(home.parent!)!;
    const list = held.get(home.id);
    if (list) list.push(id); else held.set(home.id, [id]);
  }
  const only = held.size === 1 && held.has(ROOT);
  return [...held]
    .map(([id, ids]) => ({ id: only ? '' : id, title: only ? '' : tree.groups.get(id)!.dir || '/', items: ids.sort(byName).map((each) => ({ id: each, title: each })) }))
    .sort((a, b) => byName(a.title, b.title));
}
