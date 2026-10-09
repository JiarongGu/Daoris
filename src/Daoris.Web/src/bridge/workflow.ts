import { useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { CurrentWorkflow, WorkflowScope } from '../workflow/current';
import { call } from './call';

// How work moves in a repository or a workspace (WORKFLOW1b; D157 point 7, the workflow design §2.7, §6.1): its Current
// workflow, which the driver derives from this machine's driver file, plugins and registry exactly as `daoris driver
// workflow show` reads them. The page draws it and derives nothing.

/**
 * A repository's or a workspace's Current. Desktop-only: the rules, the plugins and the workflows are this machine's
 * (design §2.4), so a browser asks nothing and its page has no Workflow tab. `enabled: false` asks nothing, so a page asks
 * only while its Workflow tab shows; and it is asked again each time the tab is shown, since a rule changed on Setup, a
 * plugin switched on or a repository moved to another workspace each changes what it draws.
 */
export const useWorkflowCurrent = (scope: WorkflowScope | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const [kind, name] = scope === null ? ['repository' as const, ''] : 'repository' in scope
    ? ['repository' as const, scope.repository]
    : ['workspace' as const, scope.workspace];
  return useQuery({
    queryKey: keys.workflow(kind, name),
    queryFn: () => call<CurrentWorkflow>('WORKFLOW_CURRENT', kind === 'repository' ? { repository: name } : { workspace: name }),
    enabled: isAvailable && enabled && scope !== null,
    staleTime: 0,
  });
};
