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
import { atName } from '../platform/casefold.ts';
import { normalizeWorkspace } from '../service/remotemap.ts';
import type { DriverChoices } from './driverconfig.ts';

/** A cycle as the file holds it: a JSON object, kept whole. */
export type Cycle = Record<string, unknown>;

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

/** A rule's `cycle` as the driver reads it: an object is kept whole, anything else is none. */
export function cycleOf(value: unknown): Cycle | undefined {
  return value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Cycle : undefined;
}

/**
 * The work cycles under one key, as the driver's `WorkCycleMap` reads them: each owner's kinds, a kind read only where it is one of
 * `WORK_CYCLE_KINDS` and its cycle an object, kept as written; an owner left with none is not read.
 */
export function workCycleMap(value: unknown): Record<string, Record<string, Cycle>> {
  const held: Record<string, Record<string, Cycle>> = {};
  if (cycleOf(value) === undefined) return held;
  for (const [owner, kinds] of Object.entries(value as Record<string, unknown>)) {
    if (cycleOf(kinds) === undefined) continue;
    const read: Record<string, Cycle> = {};
    for (const [kind, cycle] of Object.entries(kinds as Record<string, unknown>)) {
      const object = cycleOf(cycle);
      if (WORK_CYCLE_KINDS.includes(kind) && object !== undefined) read[kind] = object;
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
