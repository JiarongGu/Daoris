import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { RunReviewAnswer } from '../run/runReviewWire';
import { Button, Inline, Segmented } from '../../../design/ui';
import { cn } from '../../../lib/cn';
import { sentence, stamp } from '../../../locales/format';
import type { NodeWordAnswer, NodeWordAsk } from '../../../workflow/run';
import { nodeWordRefused } from '../../../workflow/NodeWord';
import { type Answered, InlineConfirm } from '../../InlineConfirm';
import { SupervisorLandingCard } from '../../head/SupervisorLandingCard';
import type { ViewedMarks } from '../run/ReviewChanges';
import { contentOf } from '../run/viewedMarks';

/** What is accepted, in one sentence of the record's kind; *and Daoris applies it* and *brought back* only where a step follows. */
export function decidedSays(t: TFunction, answer: RunReviewAnswer): string {
  const says = answer.says;
  const kind = answer.record?.kind ?? says?.kind ?? 'answer';
  const follows = (answer.follows ?? []).length > 0;
  switch (kind) {
    case 'branch': return t('work.reviewCard.says.branch', { branch: says?.branch ?? answer.record?.place ?? '' });
    case 'checkout': return t('work.reviewCard.says.checkout', { place: answer.record?.place ?? '' });
    case 'folder': return t('work.reviewCard.says.folder', { count: says?.files ?? 0 });
    case 'copy': return t(follows ? 'work.reviewCard.says.copy' : 'work.reviewCard.says.copyEnds', { count: says?.files ?? 0 });
    case 'plan': return t(follows ? 'work.reviewCard.says.plan' : 'work.reviewCard.says.planEnds', {
      count: says?.plan ?? answer.operations?.length ?? 0, system: says?.system ?? answer.record?.place ?? '',
    });
    default: return t('work.reviewCard.says.answer');
  }
}

/** The outputs under review in one line: the changes with how many of their files the reader has viewed, each handback, a plan. */
function outputsLine(t: TFunction, answer: RunReviewAnswer, viewed: ViewedMarks): string {
  const files = answer.diff?.files ?? answer.files ?? answer.record?.rows ?? [];
  const seen = files.filter((file) => viewed.isViewed(file.path, contentOf(file))).length;
  return (answer.outputs ?? []).map((output) => {
    if (output.type === 'changes') return t('work.reviewCard.outputChanges', { count: files.length, viewed: seen });
    if (output.type === 'handback') return t('work.outputs.handbackOf', { title: output.title ?? '' });
    return t(`work.outputs.type.${output.type}`, { defaultValue: output.title ?? output.type });
  }).join(' · ');
}

type Asking = 'changes' | 'stop' | null;

/**
 * **The Accept card**: in the session's conversation, what the person accepts (the kind's sentence, the revision, the outputs with their
 * viewed marks) and what follows it, with *Accept* in one press, *Send back…* with their words, *Stop…* asked once, and *Open outputs*.
 *
 * @remarks
 * **Bound to what it draws.** Every press names the card's run, node, attempt and revision; a newer revision supersedes the card, which
 * then takes no press while a new card is drawn below it, and a press the driver refuses `moved` says so and offers the newer revision,
 * never retargeting itself. **States:** waiting; accepted, with what follows as it stands; superseded; held, the supervisor's decision
 * with *Overrule*; settled otherwise.
 *
 * A molecule: the answer, the marks and the presses arrive as props.
 */
export function ReviewCard({ answer, viewed, onWord, onOpenOutputs, onOverrule, overruling = false, overruleSaid = null, supersededBy = null }: {
  answer: RunReviewAnswer;
  viewed: ViewedMarks;
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  /** Open the Outputs view at an attempt, or at the newest with null. */
  onOpenOutputs?: (attempt: number | null) => void;
  onOverrule?: (decision: string) => void;
  overruling?: boolean;
  overruleSaid?: string | null;
  /** The newer attempt that supersedes this card's. */
  supersededBy?: number | null;
}) {
  const { t } = useTranslation();
  const record = answer.record ?? null;
  const [asking, setAsking] = useState<Asking>(null);
  const [words, setWords] = useState('');
  const [part, setPart] = useState<string | null>(null);
  const [pressing, setPressing] = useState(false);
  const [moved, setMoved] = useState(false);
  const [refused, setRefused] = useState<string | null>(null);
  if (!record) return null;

  const parts = record.parts;
  const chosenPart = part ?? parts.at(-1)?.quest ?? null;
  const presses = onWord && !supersededBy && !moved ? answer.presses ?? [] : [];
  const supervisor = answer.supervisor ?? null;
  const follows = answer.follows ?? [];
  const settled = answer.standing === record.attempt && answer.state !== 'active';
  const stateWord = (state: string) => t(`work.outputs.state.${state}`, { defaultValue: state });
  const ask = (rest: Omit<NodeWordAsk, 'run' | 'node'>) => onWord!({ run: answer.run ?? record.run, node: answer.node ?? record.node, ...rest });
  const close = () => { setAsking(null); setWords(''); setPart(null); };
  const press = (rest: Omit<NodeWordAsk, 'run' | 'node'>, answered: Answered) => {
    ask(rest).then((said) => (said.done ? answered.done() : answered.refused(nodeWordRefused(t, said)))).catch((error: unknown) => answered.refused(sentence(error)));
  };
  const accept = () => {
    setPressing(true);
    setRefused(null);
    ask({ act: 'accept', words: null, attempt: record.attempt, revision: record.revision })
      .then((said) => {
        if (said.done) return;
        if (said.refused === 'moved') setMoved(true);
        else setRefused(nodeWordRefused(t, said));
      })
      .catch((error: unknown) => setRefused(sentence(error)))
      .finally(() => setPressing(false));
  };

  return (
    <div
      role="group"
      aria-label={t('work.reviewCard.label', { place: record.place })}
      className={cn('grid gap-2 rounded-card border border-line-strong bg-raised px-3 py-2.5', supersededBy && 'opacity-60')}
    >
      <p className="m-0 flex flex-wrap items-baseline gap-x-3 text-meta text-ink-faint">
        <span className="font-semibold text-ink-soft">{t('work.reviewCard.label', { place: record.place })}</span>
        <span className="font-mono">{t('work.reviewCard.revision', { attempt: record.attempt, revision: record.revision.slice(0, 7) })}</span>
        {answer.state === 'active' && answer.since && !supersededBy && <span>{t('work.reviewCard.since', { at: stamp(answer.since) })}</span>}
      </p>
      <p className="m-0 text-body text-ink"><Inline text={decidedSays(t, answer)} /></p>
      {(answer.outputs ?? []).length > 0 && <p className="m-0 text-small text-ink-soft">{t('work.reviewCard.outputs', { list: outputsLine(t, answer, viewed) })}</p>}
      <p className="m-0 text-small text-ink-soft">
        {follows.length > 0 ? t('work.reviewCard.then', { steps: follows.map((step) => step.node).join(' → ') }) : t('work.reviewCard.thenNone')}
      </p>

      {supersededBy && <p className="m-0 text-small text-ink-soft">{t('work.reviewCard.superseded', { attempt: supersededBy })}</p>}
      {moved && (
        <p className="m-0 flex flex-wrap items-center gap-2 text-small text-warn">
          {t('work.reviewCard.moved', { attempt: record.attempt })}
          <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={() => onOpenOutputs?.(null)}>{t('work.reviewCard.readNewer')}</Button>
        </p>
      )}
      {refused && <p className="m-0 text-small text-danger">{refused}</p>}
      {!supersededBy && answer.ended && <p className="m-0 text-small text-ink-soft">{t('work.reviewCard.ended', { outcome: answer.ended })}</p>}
      {!supersededBy && !answer.ended && settled && (
        <p className="m-0 text-small text-ink-soft">
          {answer.state === 'reached' && follows.length > 0
            ? t('work.reviewCard.applying', { attempt: record.attempt, steps: follows.map((step) => `${step.node} · ${stateWord(step.state)}`).join(' · ') })
            : answer.state === 'reached' ? t('work.reviewCard.accepted', { attempt: record.attempt }) : t('work.reviewCard.settled')}
        </p>
      )}

      {supervisor && !supersededBy && (
        <SupervisorLandingCard
          landing={{
            id: supervisor.id, run: answer.run ?? '', node: answer.node ?? '', attempt: supervisor.attempt, do: supervisor.do,
            work: supervisor.work, state: supervisor.state, why: supervisor.why, keptAt: supervisor.keptAt, goesOnAt: supervisor.goesOnAt,
            stateAt: supervisor.stateAt, earlier: false,
          }}
          onOverrule={onOverrule}
          busy={overruling}
          said={overruleSaid}
        />
      )}

      {asking === null && (presses.length > 0 || onOpenOutputs) && (
        <div className="flex flex-wrap items-center gap-2">
          {presses.includes('accept') && <Button variant="primary" disabled={pressing} onClick={accept}>{pressing ? t('work.reviewCard.accepting') : t('work.reviewCard.accept')}</Button>}
          {presses.includes('changes') && <Button onClick={() => setAsking('changes')}>{t('work.reviewCard.sendBack')}</Button>}
          {presses.includes('stop') && <Button variant="ghost" onClick={() => setAsking('stop')}>{t('work.reviewCard.stop')}</Button>}
          {onOpenOutputs && <Button variant="ghost" className="ml-auto" onClick={() => onOpenOutputs(record.attempt)}>{t('work.reviewCard.openOutputs')}</Button>}
        </div>
      )}
      {asking === 'changes' && (
        <InlineConfirm
          tone="primary"
          block
          label={t('work.reviewCard.sendBackTitle')}
          says={t('work.reviewCard.sendBackSays')}
          meanIt={t('work.reviewCard.sendBackMeanIt')}
          ready={words.trim().length > 0}
          onConfirm={(answered) => press({ act: 'changes', words: words.trim(), part: chosenPart }, answered)}
          onClose={close}
        >
          {parts.length > 1 && (
            <Segmented
              label={t('work.reviewCard.sendBackPart')}
              value={chosenPart ?? ''}
              options={parts.map((each, at) => ({ value: each.quest, label: t('work.outputs.part', { part: at + 1, title: each.title }) }))}
              onChange={setPart}
            />
          )}
          <textarea
            aria-label={t('work.reviewCard.sendBackWords')}
            placeholder={t('work.reviewCard.sendBackWords')}
            rows={3}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-page px-2.5 py-1.5 text-body text-ink"
          />
        </InlineConfirm>
      )}
      {asking === 'stop' && (
        <InlineConfirm
          block
          label={t('work.reviewCard.stopTitle')}
          says={`${t('work.reviewCard.stopSays')} ${t(`work.reviewCard.stopStays.${record.kind}`, { defaultValue: '' })}`.trim()}
          meanIt={t('work.reviewCard.stopMeanIt')}
          onConfirm={(answered) => press({ act: 'stop', words: null, whole: true }, answered)}
          onClose={close}
        />
      )}
    </div>
  );
}
