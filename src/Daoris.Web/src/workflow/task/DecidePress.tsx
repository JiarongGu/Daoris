import { useTranslation } from 'react-i18next';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';

/** The prefix of a decision's answer as the driver offers it, one act per outcome: `decide:<outcome>`. */
const DECIDE = 'decide:';

/** The outcomes a decision's step offers its answer with, in the driver's order; none on any other step. */
export const decideOutcomes = (acts: readonly string[]): string[] =>
  acts.filter((act) => act.startsWith(DECIDE)).map((act) => act.slice(DECIDE.length)).filter((outcome) => outcome.length > 0);

/**
 * **The person's answer to a run's decision**: one outcome, asked once, with the reason they may give, which the run's record keeps
 * with the answer. The outcome is the step's own, shown as its workflow writes it. The step its answer closes is passed, and they may
 * answer again until the run ends.
 *
 * A molecule: the outcome, the node, the reason and the press arrive as props.
 */
export function DecidePress({ outcome, node, words, onWords, busy, className, onConfirm, onClose }: {
  outcome: string;
  node: string;
  words: string;
  onWords: (words: string) => void;
  busy?: boolean;
  className?: string;
  onConfirm: (answered: Answered) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      tone="primary"
      block
      className={className}
      label={t('workflow.run.node.decide', { outcome })}
      says={t('workflow.run.node.decideSays', { node, outcome })}
      meanIt={t('workflow.run.node.decideMeanIt')}
      busy={busy}
      onConfirm={onConfirm}
      onClose={onClose}
    >
      <input
        aria-label={t('workflow.run.node.decideWords')}
        placeholder={t('workflow.run.node.decideWords')}
        value={words}
        onChange={(event) => onWords(event.target.value)}
        className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
      />
    </InlineConfirm>
  );
}
