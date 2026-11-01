import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import type { GraphProblem } from './answers';
import { refusalSaid } from './editWords';

// The driver's refusals where they are (WORKFLOWEDIT1b): each in the reader's words, the driver's beneath where its code is one this page
// words, and the driver's alone where it is a rule newer than the page. And the head's list of them (FLOWCHART2c): a door to the step each
// names, and *Ask why* and *Fix it* to put the refusal in Ask Daoris's box where this window has one.

export function Refusals({ problems }: { problems: readonly GraphProblem[] }) {
  if (problems.length === 0) return null;
  return <ul className="m-0 grid list-disc gap-1 pl-5">{problems.map((problem, at) => <Refusal key={at} problem={problem} />)}</ul>;
}

function Refusal({ problem }: { problem: GraphProblem }) {
  const { t, i18n } = useTranslation();
  const worded = i18n.exists(`workflow.edit.refusal.${problem.code}`);
  return (
    <li className="text-small text-ink-danger">
      {worded ? refusalSaid(t, i18n, problem) : <Inline text={problem.words} />}
      {worded && <p className="m-0 text-meta text-ink-faint"><Inline text={t('workflow.edit.judge.driver', { words: problem.words })} /></p>}
    </li>
  );
}

/** One refusal in the head: the step it names (the way's source for a way), and that step's title. */
export type PutRight = { problem: GraphProblem; step: string | null; title: string | null };

const SMALL = 'min-h-0 border border-line py-0.5 text-small';

/** What is to put right, each with a door to its step and the two presses that hand it to Ask Daoris. */
export function ToPutRight({ items, onDoor, onAsk }: {
  items: readonly PutRight[];
  onDoor: (step: string) => void;
  /** Absent where this window has no Ask Daoris to fill in. */
  onAsk?: (words: string) => void;
}) {
  const { t, i18n } = useTranslation();
  return (
    <ul className="m-0 grid list-none gap-1 p-0">
      {items.map(({ problem, step, title }, at) => {
        const words = refusalSaid(t, i18n, problem);
        const where = title === null ? words : t('workflow.edit.problems.at', { step: title, words });
        const ask = (key: 'why' | 'fix') => (
          <Button variant="ghost" className={SMALL} aria-label={t(`workflow.edit.ask.${key}For`, { what: where })}
            onClick={() => onAsk?.(t(`workflow.edit.ask.${key}Text${title === null ? '' : 'At'}`, { step: title, words }))}>
            {t(`workflow.edit.ask.${key}`)}
          </Button>
        );
        return (
          <li key={at} className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1 text-small">
            {step
              ? (
                <button type="button" onClick={() => onDoor(step)}
                  className="min-w-0 cursor-pointer border-0 bg-transparent p-0 text-left text-ink-danger underline-offset-2 [overflow-wrap:anywhere] hover:underline">
                  {where}
                </button>
              )
              : <span className="min-w-0 text-ink-danger [overflow-wrap:anywhere]"><Inline text={words} /></span>}
            {onAsk && <span className="flex gap-1">{ask('why')}{ask('fix')}</span>}
          </li>
        );
      })}
    </ul>
  );
}
