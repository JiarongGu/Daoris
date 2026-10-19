import { useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { EntryAcceptPlan } from '../../work/EntryAccept';
import type { LandingConflict, LeftOutCommit, PluginLanding } from './trees';
import { call, pluginBound, pressBound } from '../call';

export type { EntryAcceptPart, EntryAcceptPlan } from '../../work/EntryAccept';

// A line of work as one (D167): its one Accept and its ask's (ACCEPTALL1, the amendment of 2026-10-15), over the driver's
// `EntriesRoutes`. Each part lands by its own landing, so what a part's answer carries is what `LAND_SESSION_TREE` answers.

/** One part's outcome at an entry's Accept: `landed`, `refused`, `left-out`, `same-tree` or `not-tried`, with its landing's words. */
export type EntryPartLanded = {
  session: string;
  outcome: string;
  done?: boolean | null;
  message?: string | null;
  branch?: string | null;
  plugin?: PluginLanding | null;
  leftOut?: LeftOutCommit[] | null;
  conflict?: LandingConflict | null;
};

/** What an entry's Accept came to: whether every part it tried landed, its sentence, the branch, and each part. */
export type EntryLanded = { entry: string; repository: string; done: boolean; message: string; branch?: string | null; parts: EntryPartLanded[] };

/** What an ask's Accept came to (point 4): per repository, a refusal holding only its own. */
export type AskLanded = { ask: string; done: boolean; repositories: EntryLanded[] };

/**
 * How long an Accept of `parts` may take: each part one replay and its plugin's push, one after another, as the host lands them.
 * The host's own bounds, spelled in `call.ts`.
 */
const landsBound = (parts: number) => pressBound(parts) + Math.max(1, parts) * pluginBound;

/**
 * Where a line of work's one Accept would land its parts (`ENTRY_LANDING`), read only while its ask is open: the plan reads git
 * for every part, so a list never asks it of every line of work. Under the landings' key, so a landing anywhere asks it again.
 */
export const useEntryLanding = (entry: string | null, open: boolean) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.entryLanding(entry ?? ''),
    queryFn: () => call<EntryAcceptPlan>('ENTRY_LANDING', { entry }),
    enabled: isAvailable && entry !== null && open,
  });
};

/** Where an ask's one Accept would land each of its lines of work (`ASK_LANDING`), one plan per repository, read while its ask is open. */
export const useAskLanding = (ask: string | null, open: boolean) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.askLanding(ask ?? ''),
    queryFn: () => call<{ ask: string; entries: EntryAcceptPlan[] }>('ASK_LANDING', { ask }),
    enabled: isAvailable && ask !== null && open,
  });
};

/**
 * A line of work's one Accept (`LAND_ENTRY`): its parts land in the order they ran, leaving out only those the person chose. What
 * it moved is the route's to declare and the state hub's to ask again, in every window (D169); the hook asks nothing itself.
 */
export const useLandEntry = () =>
  useMutation({
    // Keyed by its route, so every view reads it under way (STATEHUB1c).
    mutationKey: ['LAND_ENTRY'],
    mutationFn: ({ entry, parts, leaveOut }: { entry: string; parts: number; leaveOut: readonly string[] }) =>
      call<EntryLanded>('LAND_ENTRY', { entry, ...(leaveOut.length ? { leaveOut: [...leaveOut] } : {}) }, { timeoutMs: landsBound(parts) }),
  });

/** An ask's one Accept (`LAND_ASK`): each of its lines of work lands in its own repository, answered per repository. */
export const useLandAsk = () =>
  useMutation({
    mutationKey: ['LAND_ASK'],
    mutationFn: ({ ask, parts, leaveOut }: { ask: string; parts: number; leaveOut: readonly string[] }) =>
      call<AskLanded>('LAND_ASK', { ask, ...(leaveOut.length ? { leaveOut: [...leaveOut] } : {}) }, { timeoutMs: landsBound(parts) }),
  });
