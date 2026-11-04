import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { keys } from '../data/queries';
import type { AccountWaitTick, Consideration, TrustHold } from '../data/signals';
import { call } from './call';
import { readPushed, type SliceSpec } from './state/slices';
import { readSlice } from './driver';

/** The considerations' slice, as the shell's owner names it (`ConsiderationsStateSlice`). */
export const CONSIDERATIONS_SLICE = 'considerations';

/**
 * What the driver's last look said and the page draws as standing facts (FOUNDATION1s, D182): why each open quest is sitting
 * (D46 §3), the starts held for the agent's trust (D73), and the starts held on cooling accounts, an ask's intake among them (UX6d)
 * and on signed-out accounts with none cooling (UX6d1).
 */
export type Considerations = { considered: Consideration[]; untrusted: TrustHold[]; waits: AccountWaitTick[] };

const NOTHING_CONSIDERED: Considerations = { considered: [], untrusted: [], waits: [] };

const listOf = <T,>(part: unknown): T[] => (Array.isArray(part) ? (part as T[]) : []);

/**
 * The considerations as the shell pushes them (`ConsiderationsStateSlice`): each list a partition pushed whole when it moves, composed
 * into the one entry the readers select from. Nothing before the shell has judged them, which is before its first look.
 */
export const considerationsSlice: SliceSpec = {
  name: CONSIDERATIONS_SLICE,
  key: keys.considerations,
  read: () => readSlice(CONSIDERATIONS_SLICE),
  compose: (partitions, judged): Considerations | undefined => judged
    ? {
      considered: listOf<Consideration>(partitions.get('considered')),
      untrusted: listOf<TrustHold>(partitions.get('untrusted')),
      waits: listOf<AccountWaitTick>(partitions.get('waits')),
    }
    : undefined,
};

/**
 * One list of the considerations, from memory: written by the shell's pushes and never fetched, so no tick, focus or staleness asks
 * for it. Empty in a browser, where nothing is pushed, and before the shell's first look.
 */
function useConsiderations<T>(select: (all: Considerations) => T) {
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.considerations,
    queryFn: () => readPushed(client, CONSIDERATIONS_SLICE, async () => NOTHING_CONSIDERED),
    select,
    staleTime: Infinity,
    gcTime: Infinity,
    refetchOnWindowFocus: false,
  });
}

const consideredOf = (all: Considerations) => all.considered;
const untrustedOf = (all: Considerations) => all.untrusted;

/** Why every open quest is sitting, in the driver's own words, as of its last look (D46 §3). */
export const useConsidered = () => useConsiderations(consideredOf);

/** The starts the driver held because the agent has not been trusted where they would run (D73), as of its last look. */
export const useUntrusted = () => useConsiderations(untrustedOf);

/** What a grant answered: the key it wrote, whether it wrote, whether a re-read says yes, and why. */
export type TrustGranted = { folder: string; key: string; changed: boolean; verified: boolean; message: string };

/**
 * The person's grant of a folder the driver is holding (D73) — the screen's half of
 * `daoris agent trust … --yes`. The shell writes it only for a hold its last tick produced, in the
 * file that tick read, and the driver looks again at once. Its route publishes the driver's state, which the state hub asks
 * again in every window (STATEHUB1h, D169). A grant it verified takes the hold off the considerations' slice as it answers, and
 * the push shows that in every window at once (FOUNDATION1s): the page writes nothing of its own.
 */
export const useTrustFolder = () => useMutation({
  mutationFn: (hold: Pick<TrustHold, 'folder' | 'trustFile'>) =>
    call('TRUST_FOLDER', { folder: hold.folder, trustFile: hold.trustFile }) as Promise<TrustGranted>,
});
