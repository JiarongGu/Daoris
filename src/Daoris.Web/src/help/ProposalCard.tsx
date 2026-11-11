import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../design/ui';

/** A proposal of Ask Daoris's the route takes: the one kind left is a go, the screen where what it is for is done. */
export type HelpProposal = {
  id: string;
  /** A digest of the plan the card shows: the Apply sends it back, and the host applies only a plan still that one. */
  revision?: string;
  kind: 'go';
  describe: string;
  terminal: string;
  why: string;
};

/**
 * One screen Ask Daoris suggests, with *Go there* and *Dismiss*. The description is the driver's sentence, rendered verbatim;
 * only the chrome translates. A go changes nothing and carries no command: its press is *go there*, and *dismiss* only takes the
 * card away, told to nobody.
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
      <p className="m-0 text-small font-semibold text-accent">{t('help.proposal.titleGo')}</p>
      <p className="m-0 mt-1 text-small text-ink"><Inline text={proposal.describe} /></p>
      <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.goNote')}</p>
      {/* The agent's reason quotes names as code, as the description does, so it renders as it does. */}
      {proposal.why && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why: proposal.why })} /></p>}
      <div className="mt-2 flex flex-wrap gap-2">
        <Button variant="primary" disabled={pending} onClick={() => onApply(proposal.id)}>{t('help.proposal.go')}</Button>
        <Button variant="ghost" disabled={pending} onClick={() => onDismiss(proposal.id)}>{t('help.proposal.dismissGo')}</Button>
      </div>
    </li>
  );
}
