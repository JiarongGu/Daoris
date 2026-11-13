import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Pill } from '../../design/ui';
import type { AskPlanEvidence, AskPlanRelease, AskPlanRevision } from '../askPlan';
import { PlanSettleForm, type PlanSettling } from './PlanSettle';

const place = (evidence: AskPlanEvidence) => `${evidence.repository} ${evidence.file}${evidence.range ? `:${evidence.range}` : ''}`;

/**
 * **A plan's delivery**: each release order with its reason, the source it stands on and the evidence's place, marked stale where the
 * file holds another blob here and waived where the person waived it; then what the person is asked, each question once with what yes
 * does, what it blocks and the default; then what they answered. With `onSettle` a question is answered and an order waived here; without
 * it (a browser) they are only read.
 *
 * @remarks
 * A molecule, props only. What a question or an order says is the lead's own words, shown as written; the labels are the page's.
 */
export function AskPlanDelivery({ revision, onSettle }: { revision: AskPlanRevision; onSettle?: PlanSettling }) {
  const { t } = useTranslation();
  return (
    <>
      {revision.releases.length > 0 && (
        <ul className="m-0 grid list-none gap-1.5 p-0 text-meta text-ink-faint">
          {revision.releases.map((release) => (
            <Release key={`${release.deployable}-${release.source}-${release.on}`} release={release} onSettle={onSettle} />
          ))}
        </ul>
      )}
      {revision.questions.length > 0 && (
        <section className="grid gap-1" aria-label={t('asks.plan.questions')}>
          <p className="m-0 text-meta text-ink-faint">{t('asks.plan.questions')}</p>
          <ul className="m-0 grid list-none gap-2 p-0">
            {revision.questions.map((question) => (
              <li key={question.key} className="grid min-w-0 gap-0.5 text-meta">
                <span className="wrap-anywhere text-body text-ink">{question.text}</span>
                {question.yes && <span className="text-ink-faint">{t('asks.plan.question.yes', { text: question.yes })}</span>}
                {question.blocks && <span className="text-ink-faint">{t('asks.plan.question.blocks', { text: question.blocks })}</span>}
                {question.default && <span className="text-ink-faint">{t('asks.plan.question.default', { text: question.default })}</span>}
                {onSettle && (
                  <PlanSettleForm
                    label={t('asks.plan.answer.words')} press={t('asks.plan.answer.press')}
                    onSettle={(words, answered) => onSettle({ key: question.key, words }, answered)}
                  />
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
      {revision.answers.length > 0 && (
        <ul className="m-0 grid list-none gap-1 p-0 text-meta text-ink-faint">
          {revision.answers.map((answer) => (
            <li key={answer.key} className="wrap-anywhere">{t('asks.plan.answered', { key: answer.key })}: <q className="whitespace-pre-wrap text-ink">{answer.words}</q></li>
          ))}
        </ul>
      )}
    </>
  );
}

function Release({ release, onSettle }: { release: AskPlanRelease; onSettle?: PlanSettling }) {
  const { t } = useTranslation();
  const [waiving, setWaiving] = useState(false);
  return (
    <li className="grid min-w-0 gap-0.5">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2 wrap-anywhere">
        <span>{t('asks.plan.release', { deployable: release.deployable, on: release.on })}: {release.why}</span>
        <Pill tone={release.source === 'policy' ? 'warn' : 'neutral'}>{t(`asks.plan.source.${release.source}`, { defaultValue: release.source })}</Pill>
        {release.stale && <Pill tone="warn">{t('asks.plan.stale')}</Pill>}
        {release.waived !== null && <Pill tone="neutral">{t('asks.plan.waived')}</Pill>}
        {release.evidence && <span>{t('asks.plan.evidence', { where: place(release.evidence) })}</span>}
        {onSettle && release.waived === null && !waiving && (
          <Button variant="ghost" onClick={() => setWaiving(true)}>{t('asks.plan.waive.open')}</Button>
        )}
      </span>
      {release.waived !== null && <span className="wrap-anywhere">{t('asks.plan.waived.reason', { reason: release.waived })}</span>}
      {onSettle && waiving && (
        <PlanSettleForm
          label={t('asks.plan.waive.reason')} press={t('asks.plan.waive.press')}
          onSettle={(reason, answered) => onSettle(
            { deployable: release.deployable, on: release.on, reason },
            { done: () => { answered.done(); setWaiving(false); }, refused: answered.refused },
          )}
        />
      )}
    </li>
  );
}
