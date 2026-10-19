import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../data/queries';
import type { SystemWiring } from '../data/wiring';
import { call, refusedNoRoute } from './call';
import { pollUnlessTold } from './state/published';

// How this Daoris is wired (WIRING1b, the this-Daoris design §6, D50): Settings → This Daoris's read, the screen's door to
// `daoris-driver wiring`, one reader in the driver library. Shell-only: this machine's paths, variables and service (D47 §4).
// The shape is a wire shape (`data/wiring.ts`, where the cards that draw it can name it): a field is added, never renamed.
export type {
  SystemWiring, WiringFile, WiringFolder, WiringLocated, WiringOld, WiringToolFolder, WiringVariable,
} from '../data/wiring';
// The page tells a shell older than it by this refusal, and reaches the route helper through the domain, not around it.
export { refusedNoRoute } from './call';

/**
 * While a walk is under way a window with no bus reads again every two seconds, so each size arrives as it is counted; else not at
 * all. A desktop window is told instead (FABRIC-EVENT5b): the driver publishes `wiring` as a walk starts and as its sizes land.
 */
export const wiringAgainIn = (wiring: SystemWiring | undefined, hears: boolean): number | false =>
  (wiring?.measuring ? pollUnlessTold(hears, 2_000) : false);

/**
 * The wiring, read only in the desktop: the open asks no measure, so it walks no folder (the second opinion's point 3).
 * `measure` asks for the sizes, a walk in the background, and the answer it gives is kept.
 */
export const useSystemWiring = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const query = useQuery({
    queryKey: keys.systemWiring,
    queryFn: () => call<SystemWiring>('SYSTEM_WIRING'),
    enabled: isAvailable,
    // A shell older than the route answers `NO_ROUTE` every time, so asking again only delays the page saying so.
    retry: (count, error) => !refusedNoRoute(error) && count < 2,
    refetchInterval: (current) => wiringAgainIn(current.state.data, isAvailable),
  });
  const measure = async () => {
    const asked = Date.now();
    const answered = await call<SystemWiring>('SYSTEM_WIRING', { measure: true });
    // A read that landed while this was on its way is newer than the walk this answer saw start (FABRIC-EVENT5b): the walk's end
    // is told, and its read must not be put back to `measuring`. Where none landed the answer is kept.
    if ((client.getQueryState(keys.systemWiring)?.dataUpdatedAt ?? 0) <= asked) {
      client.setQueryData(keys.systemWiring, answered);
    }
    return answered;
  };
  return { available: isAvailable, query, measure };
};
