import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { OutsideAgent } from '../../work/team/outsideAgents';
import { call } from '../call';
import { askHeld } from '../state/held';

// The agents outside Daoris on a channel (CHANNELJOIN1c, the event-channel design §5.8): who is on a line's channel, for *At work*, and the
// person's *Remove*. The driver's `ChannelOutsideRoutes` answers over the service's doors. Shell-only: a browser has no driver.

const text = (value: unknown): string | null => (typeof value === 'string' && value ? value : null);

/** The members an answer holds, read field by field: one with no id, or no moment it joined, is left out rather than guessed at. */
export function outsideAgentsOf(answer: unknown): OutsideAgent[] {
  const rows = (answer as { members?: unknown } | null)?.members;
  if (!Array.isArray(rows)) return [];
  return rows.flatMap((row): OutsideAgent[] => {
    const shaped = row as Record<string, unknown> | null;
    const id = text(shaped?.id);
    const joined = text(shaped?.joined);
    if (!shaped || id === null || joined === null || text(shaped.left) !== null) return [];
    return [{ id, agent: text(shaped.agent) ?? 'other', repository: text(shaped.repository), joined, seen: text(shaped.seen) }];
  });
}

/**
 * The agents outside Daoris on the line a session is a part of, oldest first, under the asks' key so a join or a leave, which the
 * service journals and the state hub declares, reads them again. Only for a session of this machine's, and only in the shell.
 */
export const useOutsideAgents = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.allAsks, 'outside', session ?? 'none'] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('CHANNEL_MEMBERS', client, queryKey, { line: session ?? '' }, outsideAgentsOf),
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/** What *Remove* answered: whether the membership ended, else the service's code. */
export type OutsideRemoved = { removed: boolean; code: string | null };

/** The person's *Remove* on one outside agent: it reads and posts nothing after, and its messages keep their poster. */
export const useRemoveOutsideAgent = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (member: string): Promise<OutsideRemoved> => {
      const answer = (await call('CHANNEL_MEMBER_REMOVE', { member })) as { removed?: unknown; code?: unknown } | null;
      return { removed: answer?.removed === true, code: text(answer?.code) };
    },
    onSettled: () => client.invalidateQueries({ queryKey: [...keys.allAsks, 'outside'] }),
  });
};
