import { useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { CurrentWorkflow, WorkflowScope } from '../workflow/current';
import type { RunScope, WorkflowRunAnswer } from '../workflow/run';
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

/**
 * Where a session's, a quest's or an ask's work stands in its workflow (WORKFLOW1c; D157 point 11, the workflow design §5.1, §7):
 * the runs the driver reads from this machine's records, one per chain and repository. Desktop-only, as Current is: the landing
 * record and the sessions' records are this machine's (D47 §4), so a browser asks nothing and draws no run. `enabled: false`
 * asks nothing, so a page asks only while what draws it shows; every tick asks it again, under the driver's key.
 */
export const useWorkflowRun = (scope: RunScope | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const [kind, id] = scope === null ? ['session' as const, ''] : 'session' in scope
    ? ['session' as const, scope.session]
    : 'quest' in scope ? ['quest' as const, scope.quest] : ['ask' as const, scope.ask];
  return useQuery({
    queryKey: keys.workflowRun(kind, id),
    queryFn: () => call<WorkflowRunAnswer>('WORKFLOW_RUN', { [kind]: id }),
    enabled: isAvailable && enabled && scope !== null && id !== '',
    staleTime: 0,
  });
};
