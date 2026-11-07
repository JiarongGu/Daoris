import { useEffect } from 'react';
import { type QueryClient, type QueryKey, skipToken, useIsMutating, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../../data/queries';
import { readUnderWay } from '../driver';
import { HELP_HISTORY, HELP_PROPOSALS, HELP_PUBLISHED, HELP_WIRED } from '../help';
import { ALL_HISTORY } from '../sessions/history';
import { treesSyncScopeKey } from '../repositories/lines';
import { notPushed } from './slices';
import { type Answered, answered } from './held';
import { ALL_TOOLS } from '../agents/tools';
import { AT_WORK } from '../sessions/atWork';

/**
 * What the driver says moved: the scopes, in its words, and the sessions the change is about where
 * the route named one; absent is any. Never what it is now: each window reads that again through the queries it holds.
 */
export type StateChanged = { scopes?: unknown; sessions?: unknown };

/**
 * Each scope the driver names, and the page's queries it asks again. The one place a press's change reaches
 * the cache, so the window that pressed, a second window and a view mounted later read alike. `sessions` narrows only what is
 * one session's own: a landing is every plan's, since each is judged against the line it moved.
 */
const scopes = new Map<string, (sessions: string[] | null) => QueryKey[]>([
  // The sessions, and each unit's milestone, which reads their records.
  ['sessions', () => [keys.allSessions, keys.allWorkState]],
  // Every plan and gate, with the second opinions and what of them waits, which sit under the landings' key; and each unit's milestone.
  ['landing', () => [keys.allLandings, keys.allWorkState]],
  ['diff', (sessions) => sessions?.map((session) => keys.diff(session)) ?? [keys.allDiffs]],
  ['sweep', () => [keys.sweep, keys.allWorkState]],
  ['treesSync', () => [treesSyncScopeKey]],
  // Each repository's rules, and the Current workflow drawn from them, which sits under their key.
  ['lines', () => [keys.lines]],
  // The quests, with each quest's and ask's work, which sits under their key.
  ['quests', () => [keys.allQuests]],
  ['asks', () => [keys.allAsks]],
  // The driver's state and everything read under its key: its standing choices, the lines, the plans, the lists.
  ['driver', () => [keys.driver]],
  ['rules', () => [keys.rules]],
  // Every clear's plan, whatever it is asked of.
  ['history', () => [ALL_HISTORY]],
  ['sync', () => [keys.allSync]],
  // The workspaces and repositories registered: the registry, and the repositories the service lists from it.
  // And who is at work, whose hint reads a repository's lanes from the registry.
  ['registry', () => [keys.allRegistry, keys.allRepositories, AT_WORK]],
  ['plugins', () => [keys.plugins]],
  // The agents' roster, with what each start would run on and the accounts, which sit under its key.
  ['harnesses', () => [keys.harnesses]],
  // The tools, with what a switch of git would change; Ask Daoris's history and what each
  // conversation proposed; the browser's settings and favorites.
  ['tools', () => [ALL_TOOLS]],
  // And the receipts *What it has* shows, so one a resume or a change of mode records while it is open appears
  // (only an enabled query, the open panel's, asks again). And what a conversation published, which an Apply adds.
  ['help', () => [HELP_HISTORY, HELP_PROPOSALS, HELP_WIRED, HELP_PUBLISHED]],
  ['browser', () => [keys.browserSettings]],
  // How this Daoris is wired, told by the driver as a walk of its sizes starts and ends, and by the Downloads
  // row's clearing, with that row's list; and the Disk space card, told as each kind's walk ends.
  ['wiring', () => [keys.systemWiring, keys.systemDownloads, keys.systemSpace]],
  // Done and failed, which the notices slice pushes whole, so no query is asked again.
  ['notices', () => []],
  // The knowledge index fed further, told by a look while it feeds, so the status bar's size is read again.
  ['repositories', () => [keys.allRepositories]],
  // A repository's integration, the Integration view's look.
  ['integration', () => [['integration']]],
  // Who is at work, told by its read's own judge naming no session, so every mounted read is asked again.
  ['atWork', () => [AT_WORK]],
  // A checkout's index moved by a person's stage, unstage or Undo, which the Changes tab reads again.
  ['checkouts', () => [['git', 'changes']]],
]);

const words = (value: unknown): string[] =>
  Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string' && each.length > 0) : [];

/**
 * Ask the queries under `queryKey` again, and once more after any read already on its way. A query with no data yet
 * has nothing to cancel, so an invalidation reuses the read in flight, whose answer pre-dates the change and whose success clears
 * the invalidation: a proposal written, or a walk ended, while a first read is out would be lost to it.
 */
function askAgain(client: QueryClient, queryKey: QueryKey) {
  const cache = client.getQueryCache();
  // A slice the shell pushes is written by its pushes, so a scope asks again only what is read beneath it.
  const predicate = notPushed(client);
  const inFlight = cache.findAll({ queryKey, predicate }).filter((query) => query.state.fetchStatus === 'fetching');
  void client.invalidateQueries({ queryKey, predicate });
  if (inFlight.length === 0) return;
  const stop = cache.subscribe(() => {
    if (inFlight.some((query) => query.state.fetchStatus === 'fetching')) return;
    stop();
    void client.invalidateQueries({ queryKey, predicate });
  });
}

/**
 * Ask again what `change` names. A scope this page does not know is no change here (a wire's words are added to),
 * and an event of any other shape is none.
 */
export function stateChanged(client: QueryClient, change: StateChanged | null | undefined) {
  const sessions = words(change?.sessions);
  for (const scope of words(change?.scopes)) {
    for (const queryKey of scopes.get(scope)?.(sessions.length > 0 ? sessions : null) ?? []) {
      askAgain(client, queryKey);
    }
  }
}

/**
 * One press the driver has not answered yet: its route, and the session its payload named; the entry
 * or ask it is for where it names one, and the step its handler says it has reached, each added beside
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
 * Keep what the driver says is under way, replaced whole at the window's root. A telling numbered at or before the
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

/** The session a press of this window's is sent for: the session itself, or the `id` that names it, as the routes declare. */
function sessionPressed(variables: unknown): string | null {
  if (typeof variables === 'string') return variables;
  const id = (variables as { id?: unknown } | null | undefined)?.id;
  return typeof id === 'string' ? id : null;
}

/**
 * Whether a press of `route` is under way for `session`, read alike by every view of that session, so
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
 * The step the driver says a press of `route` for `session` has reached, or null where none is under way or its
 * handler has said none. Another window's press, or this one's, since the driver tells every window. The page words each step.
 */
export function useUnderWayStep(route: string, session: string | null): string | null {
  const told = useTold();
  if (session === null) return null;
  return told.presses.find((press) => press.route === route && press.session === session)?.step ?? null;
}

/** The entry or ask a press of this window's is sent for, as `LAND_ENTRY` and `LAND_ASK` name it. */
function subjectPressed(variables: unknown): string | null {
  const named = variables as { entry?: unknown; ask?: unknown } | null | undefined;
  if (typeof named?.entry === 'string') return named.entry;
  return typeof named?.ask === 'string' ? named.ask : null;
}

/**
 * Whether a press of `route` for this entry or ask (`LAND_ENTRY`'s `entry`, `LAND_ASK`'s `ask`) is under way, read
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
 * The page's half of the driver's state as events: every `STATE_CHANGED` the shell forwards, at one place, and what is
 * under way (`UNDER_WAY`), kept at the root so a view mounted later reads it. Mounted by `ShellSignals` in both roots,
 * so every window of the desktop hears it; a plain browser has no bus, and polls.
 *
 * A window opened, or a page reloaded, while a press runs hears none of its tellings, so the hub reads the driver's
 * list each time it mounts, and keeps it as it keeps a telling: by its number, so a telling heard before the answer, or after,
 * is kept over it where it is newer. A list the driver could not answer leaves what the telling says.
 */
export function useStateHub() {
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  useShenoraEvent<StateChanged>('DAORIS', 'STATE_CHANGED', (change) => stateChanged(client, change));
  useShenoraEvent<{ seq?: unknown; presses?: unknown }>('DAORIS', 'UNDER_WAY', (told) => underWay(client, told));
  // What a read the shell answered from what it held went on to answer, into the query that asked.
  useShenoraEvent<Answered>('DAORIS', 'ANSWERED', (told) => answered(client, told));
  useEffect(() => {
    if (!isAvailable) return;
    void (async () => {
      try {
        underWay(client, await readUnderWay());
      } catch {
        // Nothing read: what is told stands, and the next telling says the rest.
      }
    })();
  }, [client, isAvailable]);
  // The root's own read of it: what is told stays while no view of it is mounted, as long as the press it names may run.
  useTold();
}
