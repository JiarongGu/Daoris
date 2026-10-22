import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../locales/format';
import { Button } from '../design/ui';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import type { NodeAct, NodeWordAnswer, NodeWordAsk, RunStep } from './run';

/** The refusals of `WORKFLOW_NODE` the page words in the reader's language; any other is the driver's own sentence (D182). */
const WORDED: ReadonlySet<string> = new Set(['worded', 'ended', 'no-words', 'not-skippable', 'not-in-table', 'not-held']);

/** What a refused word says, in the reader's language where the page words its code, else in the driver's sentence. */
export function nodeWordRefused(t: ReturnType<typeof useTranslation>['t'], answer: NodeWordAnswer): string {
  return answer.refused && WORDED.has(answer.refused)
    ? t(`workflow.run.node.refused.${answer.refused}`, { node: answer.node })
    : answer.message ?? answer.refused ?? '';
}

/**
 * **The person's skip and stop of a graph run's node** (FABRIC-CUTOVER1f; D190 point 6, the cutover design §5.2): under a node, the acts the
 * driver's one table takes where it stands (`acts`), each asking once in the page (UXFIX2), a skip with the words it keeps; and a word the
 * person gave and the driver's next look has not applied yet (`asked`), drawn with their words in place of the presses.
 *
 * @remarks
 * **The driver judges, the page draws** (D157 §7): which acts a node offers is the driver's, so a go-ahead nobody raised is passed over here
 * and a landing is offered its stop alone. Each press goes out with its run and node (`onWord`) and comes back answered: kept closes the ask,
 * a refusal is said in it by its code, in the driver's sentence where the page has no words for the code.
 *
 * **A skip goes on without the node; a stop ends what waits on it**, so the stop asks in danger's hue and the skip in the primary's.
 *
 * A molecule: the step and the press arrive as props.
 */
export function NodeWord({ run, step, onWord }: {
  /** The run, as its record names it (`graph.run`). */
  run: string;
  step: RunStep;
  /** The person's press, answered by the driver; absent where nothing presses it (a browser), and only the word kept is drawn. */
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState<NodeAct | null>(null);
  const [words, setWords] = useState('');
  const node = step.step.id;

  if (step.asked) {
    const act = step.asked.act === 'skip' ? 'skip' : 'stop';
    return (
      <div className="grid gap-0.5">
        <p className="m-0 text-small text-ink-soft">{t(`workflow.run.node.asked.${act}`)}</p>
        {step.asked.words && <p className="m-0 text-small text-ink">{t('workflow.run.quoted', { words: step.asked.words })}</p>}
      </div>
    );
  }

  const acts = (step.acts ?? []).filter((act): act is NodeAct => act === 'skip' || act === 'stop');
  if (!onWord || acts.length === 0) return null;

  const close = () => { setAsking(null); setWords(''); };
  const send = (act: NodeAct, answered: Answered) => {
    const said = words.trim();
    onWord({ run, node, act, words: said.length > 0 ? said : null })
      .then((answer) => (answer.done ? answered.done() : answered.refused(nodeWordRefused(t, answer))))
      .catch((error: unknown) => answered.refused(sentence(error)));
  };

  return (
    <div className="grid min-w-0 gap-1.5">
      {asking === null && (
        <div className="flex flex-wrap items-center gap-1.5">
          {acts.includes('skip') && (
            <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking('skip')}>
              {t('workflow.run.node.skip')}
            </Button>
          )}
          {acts.includes('stop') && (
            <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking('stop')}>
              {t('workflow.run.node.stop')}
            </Button>
          )}
        </div>
      )}
      {asking === 'skip' && (
        <InlineConfirm
          tone="primary"
          block
          label={t('workflow.run.node.skipTitle', { node })}
          says={t('workflow.run.node.skipSays', { node })}
          meanIt={t('workflow.run.node.skipMeanIt')}
          ready={words.trim().length > 0}
          onConfirm={(answered) => send('skip', answered)}
          onClose={close}
        >
          <textarea
            aria-label={t('workflow.run.node.skipWords')}
            placeholder={t('workflow.run.node.skipWords')}
            rows={2}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </InlineConfirm>
      )}
      {asking === 'stop' && (
        <InlineConfirm
          block
          label={t('workflow.run.node.stopTitle', { node })}
          says={t('workflow.run.node.stopSays', { node })}
          meanIt={t('workflow.run.node.stopMeanIt')}
          onConfirm={(answered) => send('stop', answered)}
          onClose={close}
        >
          <input
            aria-label={t('workflow.run.node.stopWords')}
            placeholder={t('workflow.run.node.stopWords')}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}
    </div>
  );
}
