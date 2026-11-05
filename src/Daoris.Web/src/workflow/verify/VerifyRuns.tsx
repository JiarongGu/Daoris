import { useTranslation } from 'react-i18next';
import { Inline, Pill, Prose, SkeletonRows } from '../../design/ui';
import { ago } from '../../locales/format';
import { PageSection } from '../../work/frame/ViewMain';
import { CheckHeld } from '../CheckHeld';
import { heldCheck, lineBatch, lineTone, lineWord } from './lineSaid';
import type { CheckHeldAnswer, CheckHeldAsk, WorkflowRun, WorkflowRunAnswer } from '../run';

/** What a repository's Workflow tab is handed of its line: the answer, or that it is on its way or was refused, and the person's word on a held check. */
export type VerifyRunsProps = {
  /** What `WORKFLOW_RUN { line }` answered; absent while it is read, or where it was refused. */
  answer?: WorkflowRunAnswer | null;
  reading?: boolean;
  /** The read was refused, in the driver's sentence that says why. */
  refusal?: string | null;
  /** The person's *Run again* or *Not now* on a check held unknown (`WORK_HELD`); none, and nothing is offered. */
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
};

/**
 * **A repository's verifications** (FABRIC-PIPELINE1d, the pipeline design §4.4): its newest verification runs, newest first, each the branch
 * and commit it judged, who asked it and when, its verdict as a status word, and each gate as its check ran it. A check Daoris could not
 * establish offers *Run again* and *Not now* under its row (`CheckHeld`).
 *
 * A molecule: the answer and the press arrive as props; a refusal is said in its place, in the driver's words.
 */
export function VerifyRuns({ answer, reading = false, refusal = null, onHeld }: VerifyRunsProps) {
  const { t } = useTranslation();
  const title = t('workflow.run.line.title');
  return (
    <PageSection title={title}>
      {!answer
        ? (
          <div aria-busy={reading || undefined} className="grid gap-2">
            <Prose className="text-small"><Inline text={refusal ?? t('workflow.run.line.reading')} /></Prose>
            {!refusal && <SkeletonRows rows={2} />}
          </div>
        )
        : answer.runs.length === 0
          ? <Prose className="text-small">{t('workflow.run.line.none')}</Prose>
          : (
            <div className="grid min-w-0 gap-2">
              <Prose className="text-small">{t('workflow.run.line.body')}</Prose>
              <ul aria-label={title} className="m-0 grid list-none gap-2 p-0">
                {answer.runs.map((run) => <Verification key={run.line?.run ?? run.repository} run={run} onHeld={onHeld} />)}
              </ul>
            </div>
          )}
    </PageSection>
  );
}

/** One verification: its verdict, what it judged and when, its gates, and the person's words on a check held for them. */
function Verification({ run, onHeld }: { run: WorkflowRun; onHeld?: VerifyRunsProps['onHeld'] }) {
  const { t } = useTranslation();
  const { line } = run;
  if (!line) return null;
  const commit = line.commit.slice(0, 7);
  const held = heldCheck(run);
  const batch = lineBatch(t, line);
  return (
    <li className="grid min-w-0 gap-1 border-b border-line pb-2 last:border-b-0">
      <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <Pill tone={lineTone(run)}>{lineWord(t, line)}</Pill>
        <span className="min-w-0 font-mono text-small text-ink [overflow-wrap:anywhere]">
          {line.branch ? t('workflow.run.line.of', { branch: line.branch, commit }) : commit}
        </span>
        <span className="text-meta text-ink-faint">
          {t(line.level === 'person' ? 'workflow.run.line.asked' : 'workflow.run.line.started', { when: ago(line.at) })}
        </span>
        {batch && <span className="text-meta text-ink-soft">{batch}</span>}
      </div>
      {run.problem && <p className="m-0 text-small text-ink-soft">{t('workflow.run.line.unread')}</p>}
      {line.gates.length > 0 && (
        <ul aria-label={t('workflow.run.line.gates')} className="m-0 flex list-none flex-wrap gap-x-3 gap-y-0.5 p-0 text-meta text-ink-soft">
          {line.gates.map((gate) => (
            <li key={gate.name} className="font-mono">
              {[
                `${gate.name} ${gate.verdict}`,
                ...(gate.exit != null ? [t('workflow.run.line.exit', { exit: gate.exit })] : []),
                ...(gate.wallSeconds != null ? [t('workflow.run.line.wall', { seconds: Math.round(gate.wallSeconds) })] : []),
              ].join(t('workflow.run.line.join'))}
            </li>
          ))}
        </ul>
      )}
      {held && <CheckHeld step={held} onHeld={onHeld} />}
    </li>
  );
}
