import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../ui';

/**
 * A proposal of Ask Daoris's the route takes (HELP1c): what it changes, and the command that does the same.
 * Since HELP6 a kind for each door built since: an agent's update or pin, an account's model and effort, a
 * delete of a record made by mistake, and a go to a screen, which has no command since it changes nothing.
 */
export type HelpProposal = {
  id: string;
  kind: 'setting' | 'ask' | 'agent' | 'account' | 'delete' | 'go';
  describe: string;
  terminal: string;
  why: string;
};

/**
 * One change Ask Daoris proposes (HELP1c, D89): what it changes, the terminal command that does the same
 * (D50), and why — with **apply** and **not now**, since every change is the person's press.
 *
 * @remarks
 * The description is the driver's sentence and renders verbatim, as every driver sentence does; only the
 * chrome around it translates. A molecule: the proposal arrives judged, a press goes out.
 *
 * **Each kind says what its press does** (HELP6): a delete is titled as one, says it cannot be undone as
 * the drawer's own confirmation does, and its press is *delete*; a go is a suggestion, not a change, so
 * it carries no command and its press is *go there*.
 */
export function ProposalCard({ proposal, pending = false, onApply, onDismiss }: {
  proposal: HelpProposal;
  pending?: boolean;
  onApply: (id: string) => void;
  onDismiss: (id: string) => void;
}) {
  const { t } = useTranslation();
  const deleting = proposal.kind === 'delete';
  const going = proposal.kind === 'go';

  return (
    <li className={deleting
      ? 'rounded-control border border-st-declined/50 bg-raised px-3 py-2.5'
      : 'rounded-control border border-accent/50 bg-raised px-3 py-2.5'}
    >
      <p className={deleting ? 'm-0 text-small font-semibold text-st-declined' : 'm-0 text-small font-semibold text-accent'}>
        {t(deleting ? 'help.proposal.titleDelete' : going ? 'help.proposal.titleGo' : 'help.proposal.title')}
      </p>
      <p className="m-0 mt-1 text-small text-ink"><Inline text={proposal.describe} /></p>
      {deleting && <p className="m-0 mt-1 text-meta text-st-declined">{t('help.proposal.deleteNote')}</p>}
      {going && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.goNote')}</p>}
      {proposal.terminal && (
        <p className="m-0 mt-1 text-meta text-ink-faint"><Inline text={t('help.proposal.command', { command: proposal.terminal })} /></p>
      )}
      {/* The agent's reason quotes names as code, as the description does, so it renders as it does. */}
      {proposal.why && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why: proposal.why })} /></p>}
      <div className="mt-2 flex flex-wrap gap-2">
        <Button variant={deleting ? 'danger' : 'primary'} disabled={pending} onClick={() => onApply(proposal.id)}>
          {t(deleting ? 'help.proposal.delete' : going ? 'help.proposal.go' : 'help.proposal.apply')}
        </Button>
        <Button variant="ghost" disabled={pending} onClick={() => onDismiss(proposal.id)}>{t('help.proposal.dismiss')}</Button>
      </div>
    </li>
  );
}
