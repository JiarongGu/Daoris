import { useTranslation } from 'react-i18next';
import type { Ask } from '../api';
import { ago } from '../format';
import { Button, Inline } from '../ui';

/**
 * **An ask's kind of work** (WORKFLOW1i-c, the workflow design §4.3, D157): the kind the person chose for its work, the latest
 * standing, and each proposal its intake kept with its reason, which only their press applies.
 *
 * @remarks
 * **The person's alone to set** (D74): an intake proposes and never applies, so a proposal is shown beside the choice with
 * *Apply*. The kind is said as written: the page has no label for it, the workspace's declaration being the driver's. The press
 * carries the workflow the latest choice has, since a post replaces that choice whole. No kind picker: the choice is made in a
 * terminal or by a proposal.
 *
 * A molecule: it is handed the ask and the press.
 */
export function AskWorkflowKind({ ask, busy = false, onChoose }: {
  ask: Ask;
  busy?: boolean;
  /** Apply a proposal's kind, with the workflow the latest choice has. Absent where nothing can set it. */
  onChoose?: (kind: string, workflow?: string | null) => void;
}) {
  const { t } = useTranslation();
  const latest = ask.workflowChoices?.at(-1) ?? null;
  // A proposal stands until the person's choice has its kind: then it was applied, or chosen anyway.
  const proposals = (ask.workflowProposals ?? []).filter((proposal) => proposal.kind !== latest?.kind);
  const live = ask.state !== 'Closed';

  return (
    <div className="grid gap-2">
      <p className="m-0 text-body">
        {latest?.kind
          ? <Inline text={t(latest.workflow ? 'asks.record.kindChosenWorkflow' : 'asks.record.kindChosen', { kind: latest.kind, workflow: latest.workflow, ago: ago(latest.at) })} />
          : <span className="text-ink-soft">{t('asks.record.kindNone')}</span>}
      </p>

      {proposals.map((proposal) => (
        <p key={`${proposal.kind}:${proposal.at}`} className="m-0 flex flex-wrap items-center gap-x-2 gap-y-1 border-l-[3px] border-line-strong pl-2 text-small text-ink-soft">
          <span className="min-w-0 flex-1 basis-64">
            <Inline text={t('asks.record.kindProposal', { kind: proposal.kind, reason: proposal.reason })} />
          </span>
          {live && onChoose && (
            <Button disabled={busy} onClick={() => onChoose(proposal.kind, latest?.workflow)}>{t('asks.record.kindApply')}</Button>
          )}
        </p>
      ))}
    </div>
  );
}
