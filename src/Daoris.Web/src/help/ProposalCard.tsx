import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../ui';

/** A proposal of Ask Daoris's the route takes (HELP1c): what it changes, and the command that does the same. */
export type HelpProposal = { id: string; kind: 'setting' | 'ask'; describe: string; terminal: string; why: string };

/**
 * One change Ask Daoris proposes (HELP1c, D89): what it changes, the terminal command that does the same
 * (D50), and why — with **apply** and **not now**, since every change is the person's press.
 *
 * @remarks
 * The description is the driver's sentence and renders verbatim, as every driver sentence does; only the
 * chrome around it translates. A molecule: the proposal arrives judged, a press goes out.
 */
export function ProposalCard({ proposal, pending = false, onApply, onDismiss }: {
  proposal: HelpProposal;
  pending?: boolean;
  onApply: (id: string) => void;
  onDismiss: (id: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <li className="rounded-control border border-accent/50 bg-raised px-3 py-2.5">
      <p className="m-0 text-small font-semibold text-accent">{t('help.proposal.title')}</p>
      <p className="m-0 mt-1 text-small text-ink"><Inline text={proposal.describe} /></p>
      <p className="m-0 mt-1 text-meta text-ink-faint"><Inline text={t('help.proposal.command', { command: proposal.terminal })} /></p>
      {/* The agent's reason quotes names as code, as the description does, so it renders as it does. */}
      {proposal.why && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why: proposal.why })} /></p>}
      <div className="mt-2 flex flex-wrap gap-2">
        <Button variant="primary" disabled={pending} onClick={() => onApply(proposal.id)}>{t('help.proposal.apply')}</Button>
        <Button variant="ghost" disabled={pending} onClick={() => onDismiss(proposal.id)}>{t('help.proposal.dismiss')}</Button>
      </div>
    </li>
  );
}
