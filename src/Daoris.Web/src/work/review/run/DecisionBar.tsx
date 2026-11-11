import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { RunReviewAnswer } from './runReviewWire';
import { Button, Inline, Segmented } from '../../../design/ui';
import { stamp, sentence } from '../../../locales/format';
import type { NodeWordAnswer, NodeWordAsk } from '../../../workflow/run';
import { nodeWordRefused } from '../../../workflow/NodeWord';
import { type Answered, InlineConfirm } from '../../InlineConfirm';
import { SupervisorLandingCard } from '../../head/SupervisorLandingCard';

/** What is decided, in one sentence of the record's kind; *and Daoris applies it* and *brought back* only where a step follows. */
export function decidedSays(t: TFunction, answer: RunReviewAnswer): string {
  const says = answer.says;
  const kind = answer.record?.kind ?? says?.kind ?? 'answer';
  const follows = (answer.follows ?? []).length > 0;
  switch (kind) {
    case 'branch': return t('work.runReview.says.branch', { branch: says?.branch ?? answer.record?.place ?? '' });
    case 'checkout': return t('work.runReview.says.checkout', { place: answer.record?.place ?? '' });
    case 'folder': return t('work.runReview.says.folder', { count: says?.files ?? 0 });
    case 'copy': return t(follows ? 'work.runReview.says.copy' : 'work.runReview.says.copyEnds', { count: says?.files ?? 0 });
    case 'plan': return t(follows ? 'work.runReview.says.plan' : 'work.runReview.says.planEnds', {
      count: says?.plan ?? answer.operations?.length ?? 0, system: says?.system ?? answer.record?.place ?? '',
    });
    default: return t('work.runReview.says.answer');
  }
}

type Asking = 'accept' | 'changes' | 'stop' | null;

/**
 * **The decision bar**: what is decided, the revision drawn and since when it waits, and the presses the attempt it stands on takes,
 * *Accept…*, *Send back…* and *Stop…*, each asking once. It stays at the top of the review as the reader scrolls, so an Accept never needs
 * every row passed first. The supervisor's held decision stands on it with *Overrule*.
 *
 * @remarks
 * **Every press names what is drawn.** The Accept carries the record's attempt and revision, so a page drawn before the work moved is
 * refused `moved`, never applied to newer work; *Send back…* is the run's `changes` word with the person's words, to the part they choose
 * (the newest until they name another); the review's *Stop…* is the run's stop, `whole`, and says what stays by the kind of work.
 *
 * A molecule: the answer and the press arrive as props; with no press (a browser), the bar says and offers nothing.
 */
export function DecisionBar({ answer, onWord, onOverrule, overruling = false, overruleSaid = null }: {
  answer: RunReviewAnswer;
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  onOverrule?: (decision: string) => void;
  overruling?: boolean;
  overruleSaid?: string | null;
}) {
  const { t } = useTranslation();
  const record = answer.record ?? null;
  const parts = record?.parts ?? [];
  const [asking, setAsking] = useState<Asking>(null);
  const [words, setWords] = useState('');
  const [part, setPart] = useState<string | null>(null);
  const presses = onWord && record ? answer.presses ?? [] : [];
  const supervisor = answer.supervisor ?? null;
  const chosenPart = part ?? parts.at(-1)?.quest ?? null;

  const close = () => { setAsking(null); setWords(''); setPart(null); };
  const press = (ask: Omit<NodeWordAsk, 'run' | 'node'>, answered: Answered) => {
    onWord!({ run: answer.run ?? record!.run, node: answer.node ?? record!.node, ...ask })
      .then((said) => (said.done ? answered.done() : answered.refused(nodeWordRefused(t, said))))
      .catch((error: unknown) => answered.refused(sentence(error)));
  };

  return (
    <header className="sticky top-0 z-10 grid gap-2 border-b border-line bg-page px-3 py-2.5">
      <p className="m-0 text-body text-ink"><Inline text={decidedSays(t, answer)} /></p>
      <p className="m-0 flex flex-wrap gap-x-3 text-meta text-ink-faint">
        {record && <span className="font-mono">{t('work.runReview.revision', { revision: record.revision.slice(0, 7) })}</span>}
        {answer.state === 'active' && answer.since && <span>{t('work.runReview.since', { at: stamp(answer.since) })}</span>}
      </p>
      {answer.ended && <p className="m-0 text-small text-ink-soft">{t('work.runReview.ended', { outcome: answer.ended })}</p>}
      {!answer.ended && record && answer.standing === record.attempt && answer.state !== 'active' && (
        <p className="m-0 text-small text-ink-soft">{t('work.runReview.settled')}</p>
      )}

      {supervisor && (
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

      {asking === null && presses.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          {presses.includes('accept') && <Button variant="primary" onClick={() => setAsking('accept')}>{t('work.runReview.accept')}</Button>}
          {presses.includes('changes') && <Button onClick={() => setAsking('changes')}>{t('work.runReview.sendBack')}</Button>}
          {presses.includes('stop') && <Button variant="ghost" onClick={() => setAsking('stop')}>{t('work.runReview.stop')}</Button>}
        </div>
      )}
      {asking === 'accept' && record && (
        <InlineConfirm
          tone="primary"
          label={t('work.runReview.acceptTitle')}
          says={<Inline text={decidedSays(t, answer)} />}
          meanIt={t('work.runReview.acceptMeanIt')}
          onConfirm={(answered) => press({ act: 'accept', words: null, attempt: record.attempt, revision: record.revision }, answered)}
          onClose={close}
        />
      )}
      {asking === 'changes' && (
        <InlineConfirm
          tone="primary"
          block
          label={t('work.runReview.sendBackTitle')}
          says={t('work.runReview.sendBackSays')}
          meanIt={t('work.runReview.sendBackMeanIt')}
          ready={words.trim().length > 0}
          onConfirm={(answered) => press({ act: 'changes', words: words.trim(), part: chosenPart }, answered)}
          onClose={close}
        >
          {parts.length > 1 && (
            <Segmented
              label={t('work.runReview.sendBackPart')}
              value={chosenPart ?? ''}
              options={parts.map((each, at) => ({ value: each.quest, label: t('work.runReview.part', { part: at + 1, title: each.title }) }))}
              onChange={setPart}
            />
          )}
          <textarea
            aria-label={t('work.runReview.sendBackWords')}
            placeholder={t('work.runReview.sendBackWords')}
            rows={3}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </InlineConfirm>
      )}
      {asking === 'stop' && record && (
        <InlineConfirm
          block
          label={t('work.runReview.stopTitle')}
          says={`${t('work.runReview.stopSays')} ${t(`work.runReview.stopStays.${record.kind}`, { defaultValue: '' })}`.trim()}
          meanIt={t('work.runReview.stopMeanIt')}
          onConfirm={(answered) => press({ act: 'stop', words: null, whole: true }, answered)}
          onClose={close}
        />
      )}
    </header>
  );
}
