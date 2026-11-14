import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { askHeld, isPending } from '../state/held';
import type { TreePoolSlot } from '../../projects/pool/TreePoolSection';

/** The pool's numbered slots, read from the desktop store and refreshed with the driver's next look. */
export function useTreePool(workspace: string, repository: string) {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.driver, 'tree-pool', workspace, repository] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('TREE_POOL', client, queryKey, { workspace, repository }, (answer): TreePoolSlot[] | null => {
      if (isPending(answer)) return null;
      const slots = (answer as { slots?: TreePoolSlot[] } | null)?.slots;
      return Array.isArray(slots) ? slots : [];
    }),
    enabled: isAvailable,
  });
}
