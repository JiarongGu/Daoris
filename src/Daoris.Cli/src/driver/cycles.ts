/**
 * The cycle of work a repository or a workspace defines (FABRIC-GRAPH1; the fabric events design §C2, §D7, §E7): a branch's on its
 * landing rule's `cycle`, a graph fragment after the landing, and a quest's or an ask's under `workCycles` and `workspaceWorkCycles`,
 * by repository or workspace, then by kind. Inherited as D164's amendment inherits a rule: a repository's own replaces its
 * workspace's whole, so a repository rule with no cycle takes the default, never its workspace's cycle.
 *
 * The driver's twin is `NodeGraphHomes` (`NodeGraphs.cs`) with `DriverConfig`'s `WorkCycleMap` and `RuleMap`; both read
 * `test/fixtures/graph-homes.json`, row for row (`cycles.test.ts`, `NodeGraphHomesTests`). Each keeps a cycle as written, whether or
 * not its graph holds: the driver judges the graph where it reads it, and says one that does not hold. This module imports nothing
 * of `driverconfig.ts` but its types, which reads through it.
 */
import { atName, findName } from '../platform/casefold.ts';
import { normalizeWorkspace } from '../service/remotemap.ts';
import type { DriverChoices } from './driverconfig.ts';

/**
 * A cycle as the file holds it, kept whole: a graph's object, or anything else the file says there, which the driver says does not read
 * (FABRIC-GRAPH1's review, finding 2). Only null is none.
 */
export type Cycle = unknown;

/** The kinds `workCycles` keys — the driver's `NodeGraphHomes.WorkCycleKinds`. A branch's cycle is its landing rule's alone (§D7). */
export const WORK_CYCLE_KINDS: readonly string[] = ['quest', 'ask'];

/** Where the governing cycle came from — the driver's `LandingSource`. */
export type CycleSource = 'repository' | 'workspace' | 'default';

/** The cycle governing a unit of a kind, and where it came from; null where none is set, which is today's behaviour. */
export interface CycleHome {
  kind: string;
  source: CycleSource;
  cycle: Cycle | null;
}

/** A rule's `cycle` as the driver reads it: kept whole whatever it is, null and absence being none. */
export function cycleOf(value: unknown): Cycle | undefined {
  return value === null ? undefined : value;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

/**
 * The work cycles under one key, as the driver's `WorkCycleMap` reads them: each owner's kinds, every kind kept as written, null being
 * none, so an edit beside one keeps it (the review's finding 2); only `WORK_CYCLE_KINDS` govern, and `cycleStrays` says the rest. An owner
 * naming no kind is not read, and the first owner written in any case governs (finding 3).
 */
export function workCycleMap(value: unknown): Record<string, Record<string, Cycle>> {
  const held: Record<string, Record<string, Cycle>> = {};
  if (!isObject(value)) return held;
  for (const [owner, kinds] of Object.entries(value)) {
    if (!isObject(kinds) || findName(Object.keys(held), owner) !== null) continue;
    const read: Record<string, Cycle> = {};
    for (const [kind, cycle] of Object.entries(kinds)) {
      if (cycle !== null) read[kind] = cycle;
    }
    if (Object.keys(read).length > 0) held[owner] = read;
  }
  return held;
}

/**
 * A branch's cycle in `repository`: its effective landing rule's — its own, else its workspace's (one in no workspace is in
 * `default`'s), else the merge with none — the rule matched as the driver's `LandingRules.Choose` matches it (and `workflows.ts`'s
 * `landingFor`), in any case, never trimmed. The driver's `NodeGraphHomes.Current`.
 */
export function cycleFor(choices: Pick<DriverChoices, 'landings' | 'workspaceLandings'>, repository: string, workspace: string | null): CycleHome {
  const own = atName(choices.landings, repository);
  if (own !== undefined) return { kind: 'branch', source: 'repository', cycle: own.cycle ?? null };
  const shared = atName(choices.workspaceLandings, normalizeWorkspace(workspace));
  return shared !== undefined
    ? { kind: 'branch', source: 'workspace', cycle: shared.cycle ?? null }
    : { kind: 'branch', source: 'default', cycle: null };
}

/** A value the file keeps under the cycle's homes that no unit reads — the driver's `CycleStray`: where, its code and its sentence. */
export interface CycleStray {
  at: string[];
  code: 'kind' | 'shadowed';
  words: string;
}

/**
 * What the file keeps under the cycle's homes that no unit reads (FABRIC-GRAPH1's review, findings 2 and 3), each kept as written and said
 * here: a kind with no work cycle, a branch's included; and a name an earlier entry of `landings`, `workspaceLandings` or the work cycles
 * spells in another case, where the first written governs. The driver's `NodeGraphHomes.Strays`, row for row (`graph-homes.json`'s `strays`).
 */
export function cycleStrays(choices: Pick<DriverChoices, 'source' | 'landings' | 'workspaceLandings' | 'workCycles' | 'workspaceWorkCycles'>): CycleStray[] {
  if (choices.source === undefined) return [];
  const file = JSON.parse(choices.source) as Record<string, unknown>;
  const strays: CycleStray[] = [];
  const map = (name: string, read: string[], governing?: (owner: string, value: Record<string, unknown>) => void): void => {
    const entries = file[name];
    if (!isObject(entries)) return;
    const seen: string[] = [];
    for (const [entry, value] of Object.entries(entries)) {
      // The entry its reader kept for this name in any case; entries before it did not read, and those after it are shadowed.
      const governs = findName(read, entry);
      if (governs === null) continue;
      if (findName(seen, entry) !== null) {
        strays.push({ at: [name, entry], code: 'shadowed',
          words: `\`${entry}\` in \`${name}\` is spelled in another case by \`${governs}\` before it, which governs; it is kept as written, and nothing reads it.` });
      } else if (entry === governs) {
        seen.push(entry);
        if (governing && isObject(value)) governing(entry, value);
      }
    }
  };
  const kinds = (name: string) => (owner: string, value: Record<string, unknown>): void => {
    for (const [kind, cycle] of Object.entries(value)) {
      if (cycle === null || WORK_CYCLE_KINDS.includes(kind)) continue;
      strays.push({ at: [name, owner, kind], code: 'kind',
        words: `\`${kind}\` under \`${owner}\` in \`${name}\` is no kind of work with a cycle of its own — ${WORK_CYCLE_KINDS.map((each) => `\`${each}\``).join(' or ')}; `
          + 'a branch\'s is its landing rule\'s `cycle`. It is kept as written, and nothing reads it.' });
    }
  };
  map('landings', Object.keys(choices.landings));
  map('workspaceLandings', Object.keys(choices.workspaceLandings));
  map('workCycles', Object.keys(choices.workCycles), kinds('workCycles'));
  map('workspaceWorkCycles', Object.keys(choices.workspaceWorkCycles), kinds('workspaceWorkCycles'));
  return strays;
}

/**
 * The cycle governing a unit of `kind`: a branch's is `cycleFor`'s; a quest's or an ask's the repository's own for that kind, else its
 * workspace's, else none. A repository naming one kind takes its workspace's for another. The driver's `NodeGraphHomes.Work`.
 */
export function workCycleFor(
  choices: Pick<DriverChoices, 'landings' | 'workspaceLandings' | 'workCycles' | 'workspaceWorkCycles'>,
  repository: string, workspace: string | null, kind: string,
): CycleHome {
  if (kind === 'branch') return cycleFor(choices, repository, workspace);
  if (WORK_CYCLE_KINDS.includes(kind)) {
    const own = atName(choices.workCycles, repository.trim())?.[kind];
    if (own !== undefined) return { kind, source: 'repository', cycle: own };
    const shared = atName(choices.workspaceWorkCycles, normalizeWorkspace(workspace))?.[kind];
    if (shared !== undefined) return { kind, source: 'workspace', cycle: shared };
  }
  return { kind, source: 'default', cycle: null };
}
