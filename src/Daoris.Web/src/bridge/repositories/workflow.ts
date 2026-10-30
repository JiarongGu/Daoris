import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { CurrentWorkflow, WorkflowScope, WorkflowStart } from '../../workflow/current';
import type { NodeWordAnswer, NodeWordAsk, RunScope, WorkflowRunAnswer } from '../../workflow/run';
import { call } from '../call';

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

/** What `WORKFLOW_LANDED` answers: the start node as it now stands, and what the change asks less of the person, in the driver's words. */
export type LandedAnswer = { start: WorkflowStart; lowered: string[] };

/**
 * The person's press on the landed route (FABRIC-PIPELINE1a; the pipeline design §1.3, §4.2): Setup's route row and the Workflow tab's
 * *Accept the gates*. `workflow` is a verification workflow's id, `current` for no verification, or null for the workspace's again;
 * for a repository naming one, the driver reads the `daoris.gates.json` at its line's tip and accepts it by its blob. Every workflow
 * answer is asked again after it, since each page's start node reads the route.
 */
export const useSetLanded = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: ({ repository: string } | { workspace: string }) & { workflow: string | null }) =>
      call<LandedAnswer>('WORKFLOW_LANDED', variables),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.workflow('repository', '').slice(0, 3) }),
  });
};

/**
 * What `INTEGRATION_GATES` answers: the start node as it now stands, whether the acceptance was kept, and the refusal's code (`moved`
 * where the tip no longer holds the blob the person read, `unread`) with the driver's sentence.
 */
export type IntegrationAnswer = { start: WorkflowStart | null; done: boolean; refused: string | null; message: string | null };

/**
 * The person's acceptance of a repository's integration declaration (INTEGRATE1d; the integration design §17.7): the blob of the
 * `daoris.gates.json` `integration` they read, or `clear` to withdraw it. A blob that is no longer the tip's is refused `moved`, as an
 * answer, never thrown. Every workflow answer is asked again after it, since each start node reads the acceptance.
 */
export const useAcceptIntegration = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: { repository: string; gates?: string; clear?: true }) => call<IntegrationAnswer>('INTEGRATION_GATES', variables),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.workflow('repository', '').slice(0, 3) }),
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

/** What `WORKFLOW_VERIFY` answers: the run bound, or refused by its code (`no-checkout`, `refused`, `unread`) with the driver's sentence. */
export type VerifyAnswer = { repository: string; done: boolean; refused: string | null; message: string };

/**
 * The person's verification of a branch or a commit (FABRIC-PIPELINE1d part 5), the page's half of `daoris-driver verify`: bound under the
 * landed route's gates they accepted, for the running driver to verify after any verification ahead of it. A refusal is an answer with the
 * driver's sentence, never thrown. Its verdict shows on the run's view, which the state hub asks again under the driver's key.
 */
export const useVerify = () => useMutation({
  mutationFn: ({ repository, branch, commit }: { repository: string; branch?: string; commit?: string }) =>
    call<VerifyAnswer>('WORKFLOW_VERIFY', { repository, ...(branch ? { branch } : {}), ...(commit ? { commit } : {}) }),
});

/**
 * The person's skip or stop of a graph run's node (FABRIC-CUTOVER1f; D190 point 6, the cutover design §5.2), the page's half of
 * `daoris-driver workflow skip|stop`: kept on the run's record by the driver for its next look, which applies it. Answers what was kept, or
 * the refusal's code and the driver's sentence, never thrown; the run's view is asked again by the state hub under the driver's key.
 */
export const useRunNodeWord = () => useMutation({
  mutationFn: async ({ run, node, act, words }: NodeWordAsk): Promise<NodeWordAnswer> => {
    const answer = await call<Partial<NodeWordAnswer> | null>('WORKFLOW_NODE', { run, node, act, ...(words ? { words } : {}) });
    return {
      run: answer?.run ?? run, node: answer?.node ?? node, act: answer?.act ?? act, done: answer?.done === true,
      refused: answer?.refused ?? null, message: answer?.message ?? null,
    };
  },
});
