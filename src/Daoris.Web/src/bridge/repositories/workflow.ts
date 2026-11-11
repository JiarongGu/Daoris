import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { CurrentWorkflow, WorkflowScope, WorkflowStart } from '../../workflow/current';
import type { WorkflowSaveAnswer, WorkflowSaveAsk, WorkflowUseAnswer, WorkflowUseAsk } from '../../workflow/edit/answers';
import type { NodeWordAnswer, NodeWordAsk, RunScope, WorkflowRunAnswer } from '../../workflow/run';
import { call, pressBound } from '../call';

// How work moves in a repository or a workspace: its Current
// workflow, which the driver derives from this machine's driver file, plugins and registry exactly as `daoris
// workflow show` reads them. The page draws it and derives nothing.

/**
 * A repository's or a workspace's Current. Desktop-only: the rules, the plugins and the workflows are this machine's
 * so a browser asks nothing and its page has no Workflow tab. `enabled: false` asks nothing, so a page asks
 * only while its Workflow tab shows; and it is asked again each time the tab is shown, since a rule changed on Setup, a
 * plugin switched on or a repository moved to another workspace each changes what it draws.
 *
 * Given a `route` (`default`, `kind:<name>` or `landed`), the answer also carries `effective`, the graph that route follows; the
 * read writes nothing, and the answer of the route before it stays in place while the next is read.
 */
export const useWorkflowCurrent = (scope: WorkflowScope | null, { enabled = true, route }: { enabled?: boolean; route?: string | null } = {}) => {
  const { isAvailable } = useShenora();
  const [kind, name] = scope === null ? ['repository' as const, ''] : 'repository' in scope
    ? ['repository' as const, scope.repository]
    : ['workspace' as const, scope.workspace];
  return useQuery({
    queryKey: keys.workflow(kind, name, route),
    queryFn: () => call('WORKFLOW_CURRENT', {
      ...(kind === 'repository' ? { repository: name } : { workspace: name }),
      ...(route ? { route } : {}),
    }) as Promise<CurrentWorkflow>,
    enabled: isAvailable && enabled && scope !== null,
    staleTime: 0,
    // A route chosen over another keeps the page drawn until its own answer lands; the holder checks the answer is its place's.
    placeholderData: route ? (previous) => previous : undefined,
  });
};

/**
 * The Current workflow of each repository named: By repository asks only the repositories a person has opened, for
 * their integration declaration's state, so a list of many repositories reads none until a row opens.
 */
export const useWorkflowCurrents = (repositories: readonly string[]) => {
  const { isAvailable } = useShenora();
  return useQueries({
    queries: repositories.map((name) => ({
      queryKey: keys.workflow('repository', name),
      queryFn: () => call('WORKFLOW_CURRENT', { repository: name }) as Promise<CurrentWorkflow>,
      enabled: isAvailable,
      staleTime: 0,
    })),
    combine: (results) => repositories.map((name, at) => ({ name, result: results[at]! })),
  });
};

/** What `WORKFLOW_LANDED` answers: the start node as it now stands, and what the change asks less of the person, in the driver's words. */
export type LandedAnswer = { start: WorkflowStart; lowered: string[] };

/**
 * What *Accept the gates* sends (AGENTSPLACE1a0): the route's own workflow again, and the blob of the declaration the page showed at
 * the line's tip, so the driver refuses `moved` a tip the person did not read. A tip not read sends no blob, as before.
 */
export const acceptGatesPress = (repository: string, landed: { workflow: string; tip?: { gates: string } | null }) => ({
  repository, workflow: landed.workflow, ...(landed.tip?.gates ? { gates: landed.tip.gates } : {}),
});

/**
 * The person's press on the landed route: Setup's route row and the Workflow tab's
 * *Accept the gates*. `workflow` is a verification workflow's id, `current` for no verification, or null for the workspace's again;
 * for a repository naming one, the driver reads the `daoris.gates.json` at its line's tip and accepts it by its blob. Every workflow
 * answer is asked again after it, since each page's start node reads the route.
 */
export const useSetLanded = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: ({ repository: string } | { workspace: string }) & { workflow: string | null; gates?: string }) =>
      call('WORKFLOW_LANDED', variables) as Promise<LandedAnswer>,
    onSuccess: () => client.invalidateQueries({ queryKey: keys.workflow('repository', '').slice(0, 3) }),
  });
};

/**
 * What `INTEGRATION_GATES` answers: the start node as it now stands, whether the acceptance is kept, and the refusal's code (`moved`
 * where the tip does not hold the blob the person read, `unread`) with the driver's sentence.
 */
export type IntegrationAnswer = { start: WorkflowStart | null; done: boolean; refused: string | null; message: string | null };

/**
 * The person's acceptance of a repository's integration declaration: the blob of the
 * `daoris.gates.json` `integration` they read, or `clear` to withdraw it. A blob that is not the tip's is refused `moved`, as an
 * answer, never thrown. Every workflow answer is asked again after it, since each start node reads the acceptance.
 */
export const useAcceptIntegration = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: { repository: string; gates?: string; clear?: true }) => call('INTEGRATION_GATES', variables) as Promise<IntegrationAnswer>,
    onSuccess: () => client.invalidateQueries({ queryKey: keys.workflow('repository', '').slice(0, 3) }),
  });
};

/**
 * Where a session's, a quest's or an ask's work stands in its workflow:
 * the runs the driver reads from this machine's records, one per chain and repository; or a repository's line, its newest verifications
 * with their verdicts. Desktop-only, as Current is: the landing
 * record and the sessions' records are this machine's, so a browser asks nothing and draws no run. `enabled: false`
 * asks nothing, so a page asks only while what draws it shows; every tick asks it again, under the driver's key.
 */
export const useWorkflowRun = (scope: RunScope | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const [kind, id] = scope === null ? ['session' as const, ''] : 'session' in scope
    ? ['session' as const, scope.session]
    : 'quest' in scope ? ['quest' as const, scope.quest]
      : 'line' in scope ? ['line' as const, scope.line] : ['ask' as const, scope.ask];
  return useQuery({
    queryKey: keys.workflowRun(kind, id),
    queryFn: () => call('WORKFLOW_RUN', { [kind]: id }) as Promise<WorkflowRunAnswer>,
    enabled: isAvailable && enabled && scope !== null && id !== '',
    staleTime: 0,
  });
};

/** What `WORKFLOW_VERIFY` answers: the run bound, or refused by its code (`no-checkout`, `refused`, `unread`) with the driver's sentence. */
export type VerifyAnswer = { repository: string; done: boolean; refused: string | null; message: string };

/**
 * The person's verification of a branch or a commit, the page's half of `daoris gate verify`: bound under the
 * landed route's gates they accepted, for the running driver to verify after any verification ahead of it. A refusal is an answer with the
 * driver's sentence, never thrown. Its verdict shows on the repository's line (`useWorkflowRun({ line })`), which the state hub asks again
 * under the driver's key.
 */
export const useVerify = () => useMutation({
  mutationFn: ({ repository, branch, commit }: { repository: string; branch?: string; commit?: string }) =>
    call('WORKFLOW_VERIFY', { repository, ...(branch ? { branch } : {}), ...(commit ? { commit } : {}) }) as Promise<VerifyAnswer>,
});

/**
 * The person's skip or stop of a graph run's node, the page's half of
 * `daoris workflow skip|stop`: kept on the run's record by the driver for its next look, which applies it. Answers what it kept, or
 * the refusal's code and the driver's sentence, never thrown; the run's view is asked again by the state hub under the driver's key.
 */
export const useRunNodeWord = () => useMutation({
  mutationFn: async (ask: NodeWordAsk): Promise<NodeWordAnswer> => {
    const answer = (await call('WORKFLOW_NODE', nodeWordPayload(ask), ask.act === 'accept' ? { timeoutMs: pressBound(1) } : undefined)) as Partial<NodeWordAnswer> | null;
    return {
      run: answer?.run ?? ask.run, node: answer?.node ?? ask.node, act: answer?.act ?? ask.act, done: answer?.done === true,
      refused: answer?.refused ?? null, message: answer?.message ?? null, revision: answer?.revision ?? null, early: answer?.early === true,
      landing: answer?.landing ?? null, press: answer?.press ?? null,
    };
  },
});

/** What `WORKFLOW_NODE` is sent for a word: only the fields given, so a skip or a stop sends what it always sent. */
export const nodeWordPayload = ({ run, node, act, words, attempt, revision, answers, unchecked, part, whole }: NodeWordAsk) => ({
  run, node, act,
  ...(words ? { words } : {}),
  ...(attempt != null ? { attempt } : {}),
  ...(revision ? { revision } : {}),
  ...(answers ? { answers } : {}),
  ...(unchecked ? { unchecked: true } : {}),
  ...(part ? { part } : {}),
  ...(whole ? { whole: true } : {}),
});

/**
 * `WORKFLOW_SAVE` and `WORKFLOW_USE`, this domain's writes as `WorkflowRoutes.cs` answers them: a version saved, or
 * judged unsaved with `plan`, and a version chosen for a place. The step editor's bridge (`workflowEdit.ts`) presses them through these.
 */
export const askWorkflowSave = (ask: WorkflowSaveAsk) => call('WORKFLOW_SAVE', ask) as Promise<WorkflowSaveAnswer>;
export const askWorkflowUse = (ask: WorkflowUseAsk) => call('WORKFLOW_USE', ask) as Promise<WorkflowUseAnswer>;
