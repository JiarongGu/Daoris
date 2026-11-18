import { useContext, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { LinkOpener } from '../../design/links';
import { useSetWorkflowValues } from '../../bridge/driver';
import { usePluginCommand } from '../../bridge/pluginCommand';
import { usePlugins } from '../../bridge/plugins';
import { useGitRemotes } from '../../bridge/repositories/git';
import { interfacesOf } from '../../plugins/interfaces';
import type { RunStep, WorkflowRun } from '../run';
import { ApprovalCard } from './ApprovalCard';
import { ReleaseTargetForm } from './ReleaseTargetForm';
import {
  APPROVE_COMMAND, approvalOf, canApproveHere, foundOf, isReleaseKind, pluginOfKind, releaseNodesOf, targetValues, type FoundReleases,
  type ReleaseTargetChoice,
} from './releaseTarget';

const TARGET_FAILURES = ['no-target', 'target-changed'];

/** The repository's origin, the plugin's way to find it: its checkout in `workspace` (a name may stand in several), else any with one. */
function useOrigin(repository: string, enabled: boolean, workspace?: string): string | null {
  const remotes = useGitRemotes({ repository, enabled });
  const checkouts = remotes.data ?? [];
  const origin = (each: (typeof checkouts)[number]) => each.remotes.find((remote) => remote.name === 'origin')?.address ?? null;
  const own = checkouts.find((each) => each.workspace === workspace);
  return (own && origin(own)) ?? checkouts.map(origin).find((address) => address !== null) ?? null;
}

/**
 * **The release target, found and set**, for the run's failed release step and the repository's Setup:
 * *Find release targets* asks the plugin's `discover-releases`, the holder picks with a reason, and the one door sets the choice on every
 * release step of the workflow, each as `daoris workflow values` would.
 */
export function ReleaseTarget({ repository, workflow, plugin, nodes }: { repository: string; workflow: string; plugin: string; nodes: string[] }) {
  const { t, i18n } = useTranslation();
  const origin = useOrigin(repository, true);
  const discover = usePluginCommand();
  const setValues = useSetWorkflowValues();
  const [found, setFound] = useState<FoundReleases | null>(null);
  const [said, setSaid] = useState<string | null>(null);

  const find = () => {
    if (!origin) { setSaid(t('workflow.release.target.noOrigin')); return; }
    setSaid(t('workflow.release.target.finding'));
    discover.mutateAsync({ id: plugin, command: 'discover-releases', args: { origin }, answers: {}, place: repository, language: i18n.language })
      .then((round) => { setFound(foundOf(round.data)); setSaid(round.exit === 0 ? null : round.said.join(' ')); })
      .catch((error: unknown) => setSaid(String(error)));
  };
  const save = async (choice: ReleaseTargetChoice, why: string) => {
    for (const node of nodes) await setValues.mutateAsync({ repository, workflow, node, values: targetValues(choice), clear: false, why });
    return { done: true };
  };

  return (
    <div className="grid min-w-0 gap-1">
      {found ? <ReleaseTargetForm found={found} onSave={save} /> : (
        <Button variant="ghost" className="min-h-0 justify-self-start px-1.5 py-0.5 text-small" onClick={find}>{t('workflow.release.target.find')}</Button>
      )}
      {said && <p className="m-0 text-small text-ink-soft">{said}</p>}
    </div>
  );
}

/** The approval card on a release step waiting on its approver, with the press the plugin's channel allows. */
function Approval({ run, step }: { run: WorkflowRun; step: RunStep }) {
  const { i18n } = useTranslation();
  const opener = useContext(LinkOpener);
  const plugin = pluginOfKind(step.node ?? '');
  const catalogue = usePlugins();
  const entry = catalogue.data?.plugins.find((each) => each.id === plugin) as (Record<string, unknown> | undefined);
  const commands = interfacesOf(entry?.interfaces).commands;
  const asked = approvalOf(step.wait?.values?.words, step.wait?.values?.url);
  const origin = useOrigin(run.repository, Boolean(asked), run.workspace);
  const command = usePluginCommand();
  const url = step.wait?.values?.url ?? null;

  const decide = async (decision: 'approve' | 'reject', comment: string) => {
    if (!asked || !origin) return { done: false, message: null };
    const round = await command.mutateAsync({ id: plugin, command: APPROVE_COMMAND, args: { origin, release: asked.release, stage: asked.stage, decision, comment },
      answers: {}, place: run.repository, language: i18n.language });
    return { done: round.exit === 0, message: round.said.join(' ') };
  };
  const open = (address: string) => (opener ? opener(address) : window.open(address, '_blank', 'noopener'));

  return <ApprovalCard words={step.wait?.values?.words ?? ''} url={url} canApprove={Boolean(asked && origin) && canApproveHere(commands)} onDecide={decide} onOpen={open} />;
}

/**
 * A release step's own doors in the run's view: on one failed for its target (`no-target`, `target-changed`), the target's form; on the
 * approval step waiting on its approver, the approval card. Nothing on any other step.
 */
export function ReleaseStepDoors({ run, step }: { run: WorkflowRun; step: RunStep }) {
  if (!isReleaseKind(step.node)) return null;
  if (step.state === 'failed' && TARGET_FAILURES.includes(step.code ?? '') && run.workflow.id) {
    const nodes = releaseNodesOf(run.steps.map((each) => ({ id: each.step.id, kind: each.node ?? '' })));
    return <ReleaseTarget repository={run.repository} workflow={run.workflow.id} plugin={pluginOfKind(step.node ?? '')} nodes={nodes} />;
  }
  if (step.wait?.values?.actor === 'approver' && /\/release-approve$/.test(step.node ?? '')) return <Approval run={run} step={step} />;
  return null;
}
