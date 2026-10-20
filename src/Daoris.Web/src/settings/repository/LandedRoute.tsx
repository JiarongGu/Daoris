import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, SettingRow } from '../../design/ui';
import { shellWord } from '../../lib/shellWord';
import { type Answered, Refused } from '../../work/InlineConfirm';
import type { WorkflowLanded, WorkflowStart } from '../../workflow/current';
import { whySaid } from '../../workflow/routeSaid';

/** What the landed route names, for its Setup and its press: a verification workflow's id, `current` for none, or the workspace's again. */
export type LandedChoice = string | null;

/** The landed route as Setup is handed it: the start node the driver answered, and the person's press. */
export type LandedSetup = {
  start: WorkflowStart;
  onChoose: (workflow: LandedChoice, answered: Answered) => void;
};

/** A repository's landed route in its Setup's folded line: the workflow that verifies the work that lands, or none. */
export function landedSummary(t: TFunction, start: WorkflowStart): string {
  const { landed } = start;
  return landed.workflow === 'current'
    ? t('settings.landedRoute.summaryNone')
    : t('settings.landedRoute.summary', { name: landed.name ?? landed.workflow });
}

/** Where the route's gates stand against the line's tip, in the Workflow tab's words; nothing where it names no verification. */
function gatesSaid(t: TFunction, landed: WorkflowLanded): string {
  if (landed.workflow === 'current') return '';
  const tip = landed.tip ?? null;
  const at = tip ? { branch: tip.line, commit: tip.commit.slice(0, 7) } : null;
  if (landed.state === 'accepted' && at) {
    return landed.gates === 'none'
      ? t('workflow.start.landed.acceptedNone', at)
      : t('workflow.start.landed.accepted', { ...at, blob: (landed.gates ?? '').slice(0, 7) });
  }
  if (landed.state === 'changed') return t('workflow.start.landed.changed');
  if (landed.state === 'unaccepted') return t('workflow.start.landed.unaccepted');
  return landed.problem ?? t('workflow.start.landed.unread');
}

/**
 * **The landed route** of a repository (FABRIC-PIPELINE1a; D192 point 2, the pipeline design §1.3): Setup's route row, the verification
 * workflow the work that lands follows, what chose it, where its gates stand, and the person's choice among the verification workflows
 * saved here, no verification, or its workspace's again where it names one of its own. The same `landed` route `daoris driver workflow
 * use … --landed` sets (D50), kept on this machine.
 *
 * @remarks
 * **Choosing one accepts the declaration at the line's tip** (§4.2): the driver reads the `daoris.gates.json` there and keeps its blob,
 * so its checks run those gates unasked, and an edit of it asks again on the Workflow tab. The person's press alone sets it (D156): an
 * agent never lowers the person's part (D157 point 10). The page judges nothing: a refusal is the driver's sentence, said under the
 * choice.
 *
 * A molecule: every state is reached by its props, and the press goes out.
 */
export function LandedRoute({ repository, workspace, start, busy = false, onChoose }: {
  repository: string;
  workspace: string;
  start: WorkflowStart;
  busy?: boolean;
  onChoose: (workflow: LandedChoice, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  const { landed } = start;
  const own = landed.level === 'repository-landed';
  const target = landed.workflow === 'current' ? t('workflow.start.noVerification') : landed.name ?? landed.workflow;
  const why = whySaid(t, landed.level, { repository, workspace }, null, true);
  const says = [t('settings.landedRoute.says', { target, why }), gatesSaid(t, landed)].filter(Boolean).join(' ');
  const choose = (workflow: LandedChoice) => {
    setRefused(null);
    onChoose(workflow, { done: () => undefined, refused: setRefused });
  };
  const option = (label: string, workflow: LandedChoice, pressed: boolean) => (
    <Button
      key={workflow ?? '<workspace>'}
      variant="ghost"
      aria-pressed={pressed}
      disabled={busy}
      className="min-h-0 border border-line px-2.5 py-0.5 text-small aria-pressed:border-accent aria-pressed:text-accent"
      onClick={() => { if (!pressed) choose(workflow); }}
    >
      {label}
    </Button>
  );

  return (
    <SettingRow
      label={t('settings.landedRoute.label')}
      hint={t('settings.landedRoute.twin', { repository: shellWord(repository, '<repository>') })}
      why={t('settings.landedRoute.why')}
    >
      <div className="grid gap-2">
        <p className="m-0 text-small text-ink-soft"><Inline text={says} /></p>
        <div role="group" aria-label={t('settings.landedRoute.choices', { repository })} className="flex flex-wrap gap-1.5">
          {start.verifications.map((each) => option(each.name, each.id, own && landed.workflow === each.id))}
          {option(t('workflow.start.noVerification'), 'current', landed.workflow === 'current' && landed.level !== 'workspace-landed')}
          {/* The workspace's again: offered where this repository names a route of its own, or follows its workspace's now. */}
          {landed.level !== 'current' && option(t('settings.landedRoute.follow', { workspace }), null, landed.level === 'workspace-landed')}
        </div>
        {start.verifications.length === 0 && <p className="m-0 text-small text-ink-soft"><Inline text={t('settings.landedRoute.none')} /></p>}
        <p className="m-0 text-meta text-ink-faint"><Inline text={t('settings.landedRoute.accepts')} /></p>
        {refused && <Refused sentence={refused} />}
      </div>
    </SettingRow>
  );
}
