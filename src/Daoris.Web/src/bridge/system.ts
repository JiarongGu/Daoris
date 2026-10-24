import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../data/queries';
import type {
  DownloadCleared, DownloadedVersion, DownloadsListing, SpacePlan, SpaceStarted, SpaceUnit, SystemSpace, SystemWiring,
} from '../data/wiring';
import { call, refusedNoRoute } from './call';
import { pollUnlessTold } from './state/published';

// How this Daoris is wired (WIRING1b, the this-Daoris design §6, D50): Settings → This Daoris's read, the screen's door to
// `daoris-driver wiring`, one reader in the driver library. Shell-only: this machine's paths, variables and service (D47 §4).
// The shape is a wire shape (`data/wiring.ts`, where the cards that draw it can name it): a field is added, never renamed.
export type {
  DownloadCleared, DownloadedVersion, DownloadsListing, SpaceKind, SpacePlan, SpaceRun, SpaceStarted, SpaceUnit, SystemSpace, SystemWiring,
  WiringFile, WiringFolder, WiringLocated,
  WiringOld, WiringToolFolder, WiringVariable,
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

/**
 * The *Disk space* card (KEEP1c, the cleanup design §4.4): the free space, and each kind's size, rule, next and last clean, read only in
 * the desktop. The open counts nothing: `count` asks the kinds named, or every kind, each a bounded walk in the background, and each
 * kind's end tells `wiring`, which reads this again, so the card fills in kind by kind. A window with no bus reads again every two
 * seconds while a kind counts.
 */
export const useSystemSpace = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const query = useQuery({
    queryKey: keys.systemSpace,
    queryFn: () => call<SystemSpace>('SYSTEM_SPACE'),
    enabled: isAvailable,
    retry: (count, error) => !refusedNoRoute(error) && count < 2,
    // KEEP1e: a press's run under way reads again too, each kind's end told by the bus where there is one.
    refetchInterval: (current) => {
      const data = current.state.data;
      const running = data?.kinds?.some((kind) => kind.counting) || (data?.run != null && !data.run.outcome);
      return running ? pollUnlessTold(isAvailable, 2_000) : false;
    },
  });
  const count = async (kinds?: string[]) => {
    const asked = Date.now();
    const answered = await call<SystemSpace>('SYSTEM_SPACE', { measure: kinds ?? true });
    // A read that landed meanwhile is newer than this answer (FABRIC-EVENT5b, as the wiring's measure keeps it).
    if ((client.getQueryState(keys.systemSpace)?.dataUpdatedAt ?? 0) <= asked) client.setQueryData(keys.systemSpace, answered);
    return answered;
  };
  return { available: isAvailable, query, count };
};

/**
 * The Downloads row's list (CACHECLEAR1): every version the agents and tools downloaded under `cache/`, with what keeps each. Read
 * only once the row opens it, since it sizes each version's folder; a clearing's press tells `wiring`, which reads it again.
 */
export const useSystemDownloads = (open: boolean) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.systemDownloads,
    queryFn: () => call<DownloadsListing>('SYSTEM_DOWNLOADS'),
    enabled: isAvailable && open,
  });
};

/**
 * The press after the list (CACHECLEAR1, D88's shape): the rows the page showed that nothing keeps, each judged again by the driver
 * right before it goes. What it moved is asked again by the state hub, as the route declares it, and never by this hook.
 */
export const useClearDownloads = () =>
  useMutation({
    mutationFn: (versions: Pick<DownloadedVersion, 'kind' | 'name' | 'version' | 'unfinished'>[]) =>
      call<{ cleared: DownloadCleared[] }>('SYSTEM_DOWNLOADS_CLEAR', {
        versions: versions.map(({ kind, name, version, unfinished }) => ({ kind, name, version, unfinished })),
      }),
  });

/**
 * A listed unit as a press sends it back: its kind and id, why the list named it, whether it held files of its own, and a set-aside
 * folder's class and ref, which the run compares before it removes (KEEPSIZE1b).
 */
const sent = (unit: SpaceUnit) => ({
  kind: unit.kind, id: unit.id, why: unit.why, own: unit.own === true,
  ...(unit.class ? { class: unit.class, ref: unit.ref ?? null } : {}),
});

/**
 * *Free up space…* (KEEP1e, the cleanup design §3.3, D88's shape): `list` asks what each rule takes now and what could go early, and
 * changes nothing; `free` sends exactly the units the list showed, each judged again by the driver right before it goes, and is answered
 * at once with the run's id, its progress then read in `SYSTEM_SPACE`'s `run`. A press while another run holds Daoris's keeping is
 * answered `held`, nothing done. What a run moves is asked again by the state hub, as the route declares it.
 */
export const useSpaceFree = () => {
  const list = useMutation({ mutationFn: () => call<SpacePlan>('SYSTEM_SPACE_FREE', { plan: true }) });
  const free = useMutation({
    mutationFn: ({ now, early }: { now: SpaceUnit[]; early: SpaceUnit[] }) =>
      call<SpaceStarted>('SYSTEM_SPACE_FREE', { units: now.map(sent), early: early.map(sent) }),
  });
  return { list, free };
};
