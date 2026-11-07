import { useTranslation } from 'react-i18next';
import { acceptGatesPress, useResolveHeld, useSetLanded, useVerify, useWorkflowCurrent, useWorkflowEditor, useWorkflowRun } from '../bridge/shell';
import type { Registration } from '../data/api';
import { workspaceOf } from '../data/workspaces';
import type { Notify } from '../design/ui';
import { sentence } from '../locales/format';
import type { WorkflowDoors } from '../workflow/WorkflowChart';
import type { WorkflowTabProps } from '../workflow/WorkflowTab';

/**
 * **A repository's Workflow as its molecules take it**: the Agents
 * place's Workflow section and the dark Workflow tab hold the one reading of its Current as the driver answers it, read only while
 * `enabled`, a refusal said in its place, the person's *Accept the gates* and *Verify…*, the verifications on its line with their verdicts,
 * and the step editor's bridge.
 * Returns the props for a registration, or null while the driver has not answered (`ready` false).
 *
 * @remarks
 * A hook, so a view that shows the workflow holds it and the molecule stays props. A step its workspace set opens the workspace's
 * Setup through the door the holder hands, which is told the workspace.
 */
export function useWorkflowTab({ repository, enabled, ready = true, notify, route }: {
  /** The repository whose Current is read; null reads none. */
  repository: string | null;
  enabled: boolean;
  /** The driver has answered. */
  ready?: boolean;
  notify: Notify;
  /** The route whose effective graph the answer also carries: `default`, `kind:<name>` or `landed`; none, and the answer is as before. */
  route?: string | null;
}): (registration: Registration, doors?: (workspace: string) => WorkflowDoors) => WorkflowTabProps | null {
  const { t } = useTranslation();
  const current = useWorkflowCurrent(repository === null ? null : { repository }, { enabled, route });
  // Its step editor, asked only once it opens.
  const editor = useWorkflowEditor({ repository: repository ?? '', workspace: current.data?.workspace ?? null }, { enabled });
  const setLanded = useSetLanded();
  const verify = useVerify();
  // The repository's verifications and their verdicts, read only where its landed route names a verification pipeline.
  const verifying = !!current.data?.start?.landed && current.data.start.landed.workflow !== 'current';
  const line = useWorkflowRun(repository === null ? null : { line: repository }, { enabled: enabled && verifying });
  const resolveHeld = useResolveHeld();

  return (registration, doors) => {
    if (!ready) return null;
    const landed = current.data?.start?.landed;
    return {
      current: current.data,
      reading: current.isPending,
      refusal: current.error ? sentence(current.error) : null,
      doors: doors?.(current.data?.workspace ?? workspaceOf(registration)),
      // The person's *Accept the gates*, the route's own pipeline again, which accepts the tip's declaration by its blob.
      onAcceptGates: landed && landed.workflow !== 'current' ? (answered) => setLanded.mutate(acceptGatesPress(registration.repository, landed), {
        onSuccess: () => {
          notify(t('projects.landed.accepted', { repository: registration.repository }));
          answered.done();
        },
        onError: (error) => answered.refused(sentence(error)),
      }) : undefined,
      accepting: setLanded.isPending,
      // *Verify…* of a branch or a commit under the accepted gates, `daoris gate verify`'s twin; a refusal is an answer.
      onVerify: (ask, answered) => verify.mutate({ repository: registration.repository, ...ask }, {
        onSuccess: (answer) => {
          if (answer.done) {
            notify(t('projects.verify.started', { repository: registration.repository }));
            answered.done();
          } else answered.refused(answer.message);
        },
        onError: (error) => answered.refused(sentence(error)),
      }),
      verifications: verifying
        ? { answer: line.data, reading: line.isPending, refusal: line.error ? sentence(line.error) : null, onHeld: resolveHeld.mutateAsync }
        : undefined,
      editor,
    };
  };
}
