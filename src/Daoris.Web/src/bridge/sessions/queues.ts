import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { readQueueRows } from '../../overview/queuePlaces';
import { call, type Payload } from '../call';
import { askHeld } from '../state/held';
import { useConsidered } from '../driverConsiderations';

/**
 * Every item waiting in the machine's queues, the one reader `daoris queue` prints (`QUEUES`): the last look's waiting starts and
 * the checks waiting for a slot, each row with its sort inputs; the page composes each place with its twin of the comparator
 * (`overview/queuePlaces.ts`). Told by event, since the waiters are files the executor writes. A read.
 */
export const useQueues = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queues = useQuery({
    queryKey: keys.queues,
    queryFn: () => askHeld('QUEUES', client, keys.queues, {}, readQueueRows),
    enabled: isAvailable && enabled,
    retry: false,
  });
  // Asked again when the driver's look moves, as its considerations' slice is pushed, never on a clock: an idle window asks nothing.
  const looked = useConsidered().dataUpdatedAt;
  useEffect(() => {
    if (looked > 0) void client.invalidateQueries({ queryKey: keys.queues });
  }, [client, looked]);
  return queues;
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
