import type { ReactNode } from 'react';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import type { ProposalMaker } from './proposalTake';

// Ask Daoris's proposal above the editor's chart: what it proposes, the agent and the model that made it, its
// reason in the agent's words, how it would judge taken whole, how many of its changes are taken, left and still to decide, and the presses
// over it whole: *Take all*, and *Not now* while nothing is taken or *Leave the rest* once something is. Its changes stand below it, one by
// one. A molecule: what it says arrives as props, and every press goes out.

export type ProposalBarProps = {
  maker: ProposalMaker | null;
  why: string;
  /** What a save of the proposal's graph would refuse, counted. */
  problems: number;
  counts: { taken: number; left: number; open: number };
  /** This window does not hold the draft the proposal is made against: none of its changes can be taken. */
  unknownBase: boolean;
  /** The press that settles it: on its way, answered, or refused in the driver's words. */
  settling: { state: 'pending' | 'told' } | { state: 'failed'; why: string } | null;
  /** Whether *Take all* has a change it can take. */
  canTakeAll: boolean;
  busy?: boolean;
  onTakeAll: () => void;
  onLeaveRest: () => void;
  onDismiss: () => void;
  /** Ask Daoris filled in with what no longer fits; absent where this window has no Ask Daoris. */
  onAskAgain?: () => void;
  /** The list of its changes. */
  children: ReactNode;
};

export function ProposalBar({
  maker, why, problems, counts, unknownBase, settling, canTakeAll, busy = false, onTakeAll, onLeaveRest, onDismiss, onAskAgain, children,
}: ProposalBarProps) {
  const { t } = useTranslation();
  const headId = useId();
  const press = 'min-h-0 border border-line py-0.5 text-small';
  const told = settling?.state === 'told';
  const model = maker && maker.models.length > 0 ? maker.models.join(' · ') : t('workflow.edit.proposal.modelNone');
  return (
    <section aria-labelledby={headId} data-proposal="" className="grid min-w-0 gap-2 rounded-card border border-accent/50 bg-raised p-3">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h4 id={headId} className="m-0 text-body font-semibold text-accent">
          <span aria-hidden className="mr-1">✦</span>
          {t('workflow.edit.proposal.title')}
        </h4>
        <p className="m-0 text-meta text-ink-soft [overflow-wrap:anywhere]">
          {maker ? t('workflow.edit.proposal.by', { agent: maker.agent, model }) : t('workflow.edit.proposal.byUnknown')}
        </p>
      </div>
      {/* The agent's reason, as it wrote it: content, not chrome. */}
      {why && <p className="m-0 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why })} /></p>}
      {told
        ? <p role="status" className="m-0 text-small text-ink">{t('workflow.edit.proposal.settled')}</p>
        : (
          <>
            <p className="m-0 text-small text-ink">
              {unknownBase ? t('workflow.edit.proposal.unknownBase')
                : problems > 0 ? t('workflow.edit.proposal.problems', { count: problems }) : t('workflow.edit.proposal.holds')}
            </p>
            {!unknownBase && <p className="m-0 text-small text-ink-soft">{t('workflow.edit.proposal.counts', counts)}</p>}
            {settling?.state === 'failed' && (
              <p className="m-0 text-small text-ink-danger"><Inline text={t('workflow.edit.proposal.unsettled', { why: settling.why })} /></p>
            )}
            <div className="flex flex-wrap items-center gap-1.5">
              {canTakeAll && <Button variant="primary" className="min-h-0 py-0.5 text-small" disabled={busy} onClick={onTakeAll}>{t('workflow.edit.proposal.act.takeAll')}</Button>}
              {counts.taken > 0
                ? counts.open > 0 && <Button variant="ghost" className={press} disabled={busy} onClick={onLeaveRest}>{t('workflow.edit.proposal.act.leaveRest')}</Button>
                : <Button variant="ghost" className={press} disabled={busy} onClick={onDismiss}>{t('workflow.edit.proposal.act.dismiss')}</Button>}
              {unknownBase && onAskAgain && <Button variant="ghost" className={press} disabled={busy} onClick={onAskAgain}>{t('workflow.edit.proposal.act.askAgain')}</Button>}
            </div>
            <p className="m-0 text-meta text-ink-faint">{t('workflow.edit.proposal.note')}</p>
            {!unknownBase && children}
          </>
        )}
    </section>
  );
}
