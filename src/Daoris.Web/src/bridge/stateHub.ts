import { type QueryClient, type QueryKey, useQueryClient } from '@tanstack/react-query';
import { useShenoraEvent } from '@shenora/react';
import { keys } from '../data/queries';
import { ALL_HISTORY } from './history';
import { treesSyncScopeKey } from './lines';

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
  ['sessions', () => [keys.allSessions]],
  // Every plan and gate, with the second opinions and what of them waits, which sit under the landings' key.
  ['landing', () => [keys.allLandings]],
  ['diff', (sessions) => sessions?.map((session) => keys.diff(session)) ?? [keys.allDiffs]],
  ['handOff', (sessions) => sessions?.map((session) => keys.handOff(session)) ?? [allHandOffs]],
  ['sweep', () => [keys.sweep]],
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
  ['registry', () => [keys.allRegistry]],
  ['plugins', () => [keys.plugins]],
  // The agents' roster, with what each start would run on and the accounts, which sit under its key.
  ['harnesses', () => [keys.harnesses]],
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
 * The page's half of the driver's state as events (D169): every `STATE_CHANGED` the shell forwards, at one place. Mounted by
 * `ShellSignals` in both roots, so every window of the desktop hears it; a plain browser has no bus, and polls as before.
 */
export function useStateHub() {
  const client = useQueryClient();
  useShenoraEvent<StateChanged>('DAORIS', 'STATE_CHANGED', (change) => stateChanged(client, change));
}
