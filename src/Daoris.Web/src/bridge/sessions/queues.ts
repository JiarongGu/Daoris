import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { type QueueRow, readQueueRows } from '../../overview/queuePlaces';
import { call, type Payload } from '../call';
import { askHeld } from '../state/held';

/**
 * How soon the machine's queues are asked again: every 5 s while anything waits, so a slot taken or a start begun is seen within
 * seconds; every 30 s while nothing does, since nothing wakes the page when a check is sent or a look plans.
 */
export const queuesAgainIn = (rows: readonly QueueRow[] | undefined): number => (rows && rows.length > 0 ? 5_000 : 30_000);

/**
 * Every item waiting in the machine's queues, the one reader `daoris queue` prints (`QUEUES`): the last look's waiting starts and
 * the checks waiting for a slot, each row with its sort inputs; the page composes each place with its twin of the comparator
 * (`overview/queuePlaces.ts`). Told by event, since the waiters are files the executor writes. A read.
 */
export const useQueues = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.queues,
    queryFn: () => askHeld('QUEUES', client, keys.queues, {}, readQueueRows),
    enabled: isAvailable && enabled,
    retry: false,
    refetchInterval: (query: { state: { data?: QueueRow[] } }) => queuesAgainIn(query.state.data),
  });
};

/**
 * One class set on an ask or a quest, the operation `daoris priority` calls (`PRIORITY_SET`): the person's press, the driver's refusal
 * its sentence verbatim, and the answer the class standing in the driver's words. The queues are asked again once it lands.
 */
export const useSetPriority = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: Payload<'PRIORITY_SET'>) => call('PRIORITY_SET', change) as Promise<{ said?: string }>,
    onSuccess: () => client.invalidateQueries({ queryKey: keys.queues }),
  });
};
