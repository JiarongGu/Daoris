import { useEffect } from 'react';
import { type QueryClient, type QueryKey, skipToken, useIsMutating, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../data/queries';
import { readUnderWay } from './driver';
import { HELP_HISTORY, HELP_PROPOSALS } from './help';
import { ALL_HISTORY } from './history';
import { treesSyncScopeKey } from './lines';
import { ALL_TOOLS } from './tools';

/**
 * What the driver says moved (STATEHUB1b, D169 point 3): the scopes, in its words, and the sessions the change was about where
 * the route named one; absent is any. Never what it is now: each window reads that again through the queries it holds.
 */
export type StateChanged = { scopes?: unknown; sessions?: unknown };

/** Every session's hand-off plan: the prefix `keys.handOff` keys each under. */
const allHandOffs: QueryKey = keys.handOff('').slice(0, -1);

/**
 * Each scope the driver names, and the page's queries it asks again (D169 point 6). The one place a press's change reaches
 * the cache, so the window that pressed, a second window and a view mounted later read alike. `sessions` narrows only what is
 * one session's own: a landing is every plan's, since each is judged against the line it moved.
 */
const scopes = new Map<string, (sessions: string[] | null) => QueryKey[]>([
  // The sessions, and each unit's milestone, which reads their records (FABRIC-MILESTONE1b).
  ['sessions', () => [keys.allSessions, keys.allWorkState]],
  // Every plan and gate, with the second opinions and what of them waits, which sit under the landings' key; and each unit's milestone.
  ['landing', () => [keys.allLandings, keys.allWorkState]],
  ['diff', (sessions) => sessions?.map((session) => keys.diff(session)) ?? [keys.allDiffs]],
  ['handOff', (sessions) => sessions?.map((session) => keys.handOff(session)) ?? [allHandOffs]],
  ['sweep', () => [keys.sweep, keys.allWorkState]],
  ['treesSync', () => [treesSyncScopeKey]],
  // Each repository's rules, and the Current workflow drawn from them, which sits under their key.
  ['lines', () => [keys.lines]],
  // The rest of the routes (STATEHUB1d, D169 point 10): each family its hooks asked again by hand before.
  // The quests, with each quest's and ask's work, which sits under their key.
  ['quests', () => [keys.allQuests]],
  ['asks', () => [keys.allAsks]],
  // The driver's state and everything read under its key: its standing choices, the lines, the plans, the lists.
  ['driver', () => [keys.driver]],
  ['rules', () => [keys.rules]],
  // Every clear's plan, whatever it was asked of.
  ['history', () => [ALL_HISTORY]],
  ['sync', () => [keys.allSync]],
  // The workspaces and repositories registered: the registry, and the repositories the service lists from it (STATEHUB1h).
  ['registry', () => [keys.allRegistry, keys.allRepositories]],
  ['plugins', () => [keys.plugins]],
  // The agents' roster, with what each start would run on and the accounts, which sit under its key.
  ['harnesses', () => [keys.harnesses]],
  // The remaining presses (STATEHUB1h): the tools, with what a switch of git would change; Ask Daoris's history and what each
  // conversation proposed; the browser's settings and favorites.
  ['tools', () => [ALL_TOOLS]],
  ['help', () => [HELP_HISTORY, HELP_PROPOSALS]],
  ['browser', () => [keys.browserSettings]],
]);

const words = (value: unknown): string[] =>
  Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string' && each.length > 0) : [];

/**
 * Ask again what `change` names. A scope this page does not know is no change here (a wire's words are added to, D169 point 3),
 * and an event of any other shape is none.
 */
export function stateChanged(client: QueryClient, change: StateChanged | null | undefined) {
  const sessions = words(change?.sessions);
  for (const scope of words(change?.scopes)) {
    for (const queryKey of scopes.get(scope)?.(sessions.length > 0 ? sessions : null) ?? []) {
      void client.invalidateQueries({ queryKey });
    }
  }
}

/**
 * One press the driver has not answered yet (STATEHUB1c, D169 point 7): its route, and the session its payload named; the entry
 * or ask it is for where it names one (ACCEPTALL1e), and the step its handler says it has reached (PRESSSTEP1), each added beside
 * the first two and absent where there is none.
 */
export type PressUnderWay = { route: string; session?: string; subject?: string; step?: string };

/** What the driver last told under way, whole, and its number, so a telling the bus delivered late changes nothing. */
type UnderWay = { seq: number | null; presses: PressUnderWay[] };

const NOTHING: UnderWay = { seq: null, presses: [] };

/** The presses a telling names; a press of any other shape is none, and a telling with no list is no telling. */
const pressesOf = (value: unknown): PressUnderWay[] | null => (Array.isArray(value)
  ? value.flatMap((each: unknown) => {
    const press = each as { route?: unknown; session?: unknown; subject?: unknown; step?: unknown } | null;
    if (typeof press?.route !== 'string') return [];
    return [{
      route: press.route,
      ...(typeof press.session === 'string' ? { session: press.session } : {}),
      ...(typeof press.subject === 'string' ? { subject: press.subject } : {}),
      ...(typeof press.step === 'string' ? { step: press.step } : {}),
    }];
  })
  : null);

/**
 * Keep what the driver says is under way (D169 point 7), replaced whole at the window's root. A telling numbered at or before the
 * one kept is older, however late the bus delivered it; one of any other shape changes nothing.
 */
export function underWay(client: QueryClient, told: { seq?: unknown; presses?: unknown } | null | undefined) {
  const presses = pressesOf(told?.presses);
  if (!presses) return;
  const seq = typeof told?.seq === 'number' ? told.seq : null;
  const kept = client.getQueryData<UnderWay>(keys.underWay)?.seq ?? null;
  if (seq !== null && kept !== null && seq <= kept) return;
  client.setQueryData<UnderWay>(keys.underWay, { seq, presses });
}

/** Never fetched: written by the hub alone, and read by every view through the one query, which the hub's own read keeps. */
const useTold = () => useQuery<UnderWay>({
  queryKey: keys.underWay, queryFn: skipToken, initialData: NOTHING, staleTime: Infinity, gcTime: Infinity,
}).data ?? NOTHING;

/** The session a press of this window's was sent for: the session itself, or the `id` that names it, as the routes declare. */
function sessionPressed(variables: unknown): string | null {
  if (typeof variables === 'string') return variables;
  const id = (variables as { id?: unknown } | null | undefined)?.id;
  return typeof id === 'string' ? id : null;
}

/**
 * Whether a press of `route` is under way for `session` (STATEHUB1c, D169 point 7), read alike by every view of that session, so
 * the head's press holds the review's *Accept* and a view mounted mid-press says *落地中…*. This window's own press is under
 * way from its send, kept by the window's client and not the component that pressed (its mutation, keyed by its route), and
 * in a plain browser too; another window's from the driver's telling. No session attended is nothing under way.
 */
export function useUnderWay(route: string, session: string | null): boolean {
  const told = useTold();
  const own = useIsMutating({
    mutationKey: [route],
    predicate: (mutation) => session !== null && sessionPressed(mutation.state.variables) === session,
  });
  return session !== null && (own > 0 || told.presses.some((press) => press.route === route && press.session === session));
}

/**
 * The step the driver says a press of `route` for `session` has reached (PRESSSTEP1), or null where none is under way or its
 * handler has said none. Another window's press, or this one's, since the driver tells every window. The page words each step.
 */
export function useUnderWayStep(route: string, session: string | null): string | null {
  const told = useTold();
  if (session === null) return null;
  return told.presses.find((press) => press.route === route && press.session === session)?.step ?? null;
}

/** The entry or ask a press of this window's was sent for, as `LAND_ENTRY` and `LAND_ASK` name it. */
function subjectPressed(variables: unknown): string | null {
  const named = variables as { entry?: unknown; ask?: unknown } | null | undefined;
  if (typeof named?.entry === 'string') return named.entry;
  return typeof named?.ask === 'string' ? named.ask : null;
}

/**
 * Whether a press of `route` for this entry or ask (`LAND_ENTRY`'s `entry`, `LAND_ASK`'s `ask`) is under way (ACCEPTALL1e), read
 * alike by every view of it in every window: this window's own press from its send, another's from the driver's telling, which
 * carries the entry or the ask the declaration names. No entry or ask attended is nothing under way.
 */
export function useUnderWaySubject(route: string, subject: string | null): boolean {
  const told = useTold();
  const own = useIsMutating({
    mutationKey: [route],
    predicate: (mutation) => subject !== null && subjectPressed(mutation.state.variables) === subject,
  });
  return subject !== null && (own > 0 || told.presses.some((press) => press.route === route && press.subject === subject));
}

/**
 * The page's half of the driver's state as events (D169): every `STATE_CHANGED` the shell forwards, at one place, and what is
 * under way (`UNDER_WAY`, STATEHUB1c), kept at the root so a view mounted later reads it. Mounted by `ShellSignals` in both roots,
 * so every window of the desktop hears it; a plain browser has no bus, and polls as before.
 *
 * A window opened, or a page reloaded, while a press runs heard none of its tellings (STATEHUB1c2), so the hub reads the driver's
 * list each time it mounts, and keeps it as it keeps a telling: by its number, so a telling heard before the answer, or after,
 * is kept over it where it is newer. A list the driver could not answer leaves what was told.
 */
export function useStateHub() {
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  useShenoraEvent<StateChanged>('DAORIS', 'STATE_CHANGED', (change) => stateChanged(client, change));
  useShenoraEvent<{ seq?: unknown; presses?: unknown }>('DAORIS', 'UNDER_WAY', (told) => underWay(client, told));
  useEffect(() => {
    if (!isAvailable) return;
    void (async () => {
      try {
        underWay(client, await readUnderWay());
      } catch {
        // Nothing read: what was told stands, and the next telling says the rest.
      }
    })();
  }, [client, isAvailable]);
  // The root's own read of it: what was told stays while no view of it is mounted, as long as the press it names may run.
  useTold();
}
