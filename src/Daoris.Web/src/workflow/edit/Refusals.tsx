import { useTranslation } from 'react-i18next';
import { Inline } from '../../design/ui';
import type { GraphProblem } from './answers';
import { refusalSaid } from './editWords';

// The driver's refusals where they are (WORKFLOWEDIT1b): each in the reader's words, the driver's beneath where its code is one this page
// words, and the driver's alone where it is a rule newer than the page.

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
