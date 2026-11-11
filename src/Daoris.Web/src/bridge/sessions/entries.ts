import { useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { EntryAcceptPlan } from '../../work/review/EntryAccept';
import type { LandingConflict, LeftOutCommit } from './trees';
import { call, pressBound } from '../call';

export type { EntryAcceptPart, EntryAcceptPlan } from '../../work/review/EntryAccept';

// A line of work as one: its one Accept and its ask's, over the driver's
// `EntriesRoutes`. Each part lands by its own landing, so what a part's answer carries is what `LAND_SESSION_TREE` answers.

/** One part's outcome at an entry's Accept: `landed`, `refused`, `left-out`, `same-tree` or `not-tried`, with its landing's words. */
export type EntryPartLanded = {
  session: string;
  outcome: string;
  done?: boolean | null;
  message?: string | null;
  branch?: string | null;
  leftOut?: LeftOutCommit[] | null;
  conflict?: LandingConflict | null;
};

/** What an entry's Accept came to: whether every part it tried landed, its sentence, the branch, and each part. */
export type EntryLanded = { entry: string; repository: string; done: boolean; message: string; branch?: string | null; parts: EntryPartLanded[] };

/** What an ask's Accept came to (point 4): per repository, a refusal holding only its own. */
export type AskLanded = { ask: string; done: boolean; repositories: EntryLanded[] };

/**
 * Where a line of work's one Accept would land its parts (`ENTRY_LANDING`), read only while its ask is open: the plan reads git
 * for every part, so a list never asks it of every line of work. Under the landings' key, so a landing anywhere asks it again.
 */
export const useEntryLanding = (entry: string | null, open: boolean) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.entryLanding(entry ?? ''),
    queryFn: () => call('ENTRY_LANDING', { entry: entry ?? '' }) as Promise<EntryAcceptPlan>,
    enabled: isAvailable && entry !== null && open,
  });
};

/** Where an ask's one Accept would land each of its lines of work (`ASK_LANDING`), one plan per repository, read while its ask is open. */
export const useAskLanding = (ask: string | null, open: boolean) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.askLanding(ask ?? ''),
    queryFn: () => call('ASK_LANDING', { ask: ask ?? '' }) as Promise<{ ask: string; entries: EntryAcceptPlan[] }>,
    enabled: isAvailable && ask !== null && open,
  });
};

/**
 * A line of work's one Accept (`LAND_ENTRY`): its parts land in the order they ran, leaving out only those the person chose. What
 * it moved is the route's to declare and the state hub's to ask again, in every window; the hook asks nothing itself.
 */
export const useLandEntry = () =>
  useMutation({
    // Keyed by its route, so every view reads it under way.
    mutationKey: ['LAND_ENTRY'],
    mutationFn: ({ entry, parts, leaveOut, revisions }: {
      entry: string; parts: number; leaveOut: readonly string[]; revisions?: readonly { session: string; revision: string }[];
    }) =>
      call('LAND_ENTRY', {
        entry, ...(leaveOut.length ? { leaveOut: [...leaveOut] } : {}), ...(revisions?.length ? { revisions: [...revisions] } : {}),
      }, { timeoutMs: pressBound(parts) }) as Promise<EntryLanded>,
  });

/** Each part's review revision the plan drew, which the entry's Accept names so a run whose record moved is refused alone. */
export const drawnRevisions = (plan: { parts: readonly { session: string; revision?: string | null }[] } | null | undefined) =>
  (plan?.parts ?? []).flatMap((part) => (part.revision ? [{ session: part.session, revision: part.revision }] : []));

/** An ask's one Accept (`LAND_ASK`): each of its lines of work lands in its own repository, answered per repository. */
export const useLandAsk = () =>
  useMutation({
    mutationKey: ['LAND_ASK'],
    mutationFn: ({ ask, parts, leaveOut }: { ask: string; parts: number; leaveOut: readonly string[] }) =>
      call('LAND_ASK', { ask, ...(leaveOut.length ? { leaveOut: [...leaveOut] } : {}) }, { timeoutMs: pressBound(parts) }) as Promise<AskLanded>,
  });
