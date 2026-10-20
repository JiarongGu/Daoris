import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { ATTENTION_SLICE, inCircle, keys } from '../data/queries';
import { useScope } from '../data/scope';
import { type AttentionPushed, attentionInOrder, type PushedAttention } from '../work/attention/attention';
import { readSlice } from './driver';
import { mergeKeyed, readPushed, type SliceSpec, usePushedResyncing } from './state/slices';

// Who needs the person, as the shell pushes it (PUSH1d4, D182 points 2-4 and 6): *What needs you*'s rows and what the Sessions badge
// counts, judged in .NET once for every window (`AttentionStateSlice`). The page orders the rows, words them and keeps their acts.

/** The partition of the rows, each under its kind and id, and of what the Sessions badge counts. */
const ROWS = 'rows';
const SESSIONS = 'sessions';

const keyed = (value: unknown): Record<string, unknown> | null =>
  typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : null;

/** A pushed row of a shape the page reads, or null: a kind and an id, its wait a time or null. */
function rowOf(value: unknown): PushedAttention | null {
  const row = keyed(value);
  if (!row || typeof row.kind !== 'string' || typeof row.id !== 'string') return null;
  return { ...row, since: typeof row.since === 'string' ? row.since : null } as PushedAttention;
}

/**
 * The slice's two partitions composed into one entry (PUSH1d4): the rows in the band's order (`attentionInOrder`, D182's PUSH1d4
 * note), and the keys of what the Sessions badge counts. Nothing until the owner judged and both partitions are held, so a window
 * never reads half a judgement as the whole: before, the band draws its skeleton (PUSHUX1).
 */
export function composeAttention(partitions: ReadonlyMap<string, unknown>, judged: boolean): AttentionPushed | undefined {
  const rows = keyed(partitions.get(ROWS));
  const sessions = keyed(partitions.get(SESSIONS));
  if (!judged || !rows || !sessions) return undefined;
  return {
    rows: attentionInOrder(Object.values(rows).map(rowOf).filter((row): row is PushedAttention => row !== null)),
    sessions: Object.entries(sessions).map(([key, value]) => {
      const workspace = keyed(value)?.workspace;
      return { key, workspace: typeof workspace === 'string' ? workspace : null };
    }),
  };
}

/** Who needs the person as the shell pushes it (PUSH1d4): keyed by row, so one row's move is one row's delta. */
export const attentionSlice: SliceSpec = {
  name: ATTENTION_SLICE,
  key: keys.attention,
  read: () => readSlice(ATTENTION_SLICE),
  compose: composeAttention,
  merge: mergeKeyed,
};

/**
 * What a window knows of who needs the person: in a plain browser, nothing, since the judgement is the app's (`app`); in a shell,
 * nothing yet until the owner has judged (`reading`), never an empty band (PUSHUX1); then the rows in its scope and the Sessions
 * badge's count, and whether the slice is being read again whole, which holds those rows dimmed (`resyncing`).
 */
export type AttentionRead =
  | { state: 'app' }
  | { state: 'reading' }
  | { state: 'known'; rows: PushedAttention[]; sessions: number; resyncing: boolean };

/**
 * Who needs the person in this window (PUSH1d4): the pushed rows, read from memory and never pulled, since no route answers them;
 * those judged from the service's sessions, quests and asks filtered to the window's scope as the lists they came from were, the
 * driver's own everywhere. Nothing a tick or a focus asks again.
 */
export const useAttention = (): AttentionRead => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const { workspace } = useScope();
  const resyncing = usePushedResyncing(ATTENTION_SLICE);
  const read = useQuery({
    queryKey: keys.attention,
    // Read from the pushes once they are judged; until then nothing, which the pushes replace as they come.
    queryFn: () => readPushed<AttentionPushed | null>(client, ATTENTION_SLICE, async () => null),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
  if (!isAvailable) return { state: 'app' };
  if (!read.data) return { state: 'reading' };
  const scoped = <T extends { workspace?: string | null }>(each: T) => each.workspace == null || inCircle(each.workspace, workspace);
  return { state: 'known', rows: read.data.rows.filter(scoped), sessions: read.data.sessions.filter(scoped).length, resyncing };
};
