import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../locales/format';
import { Button } from '../design/ui';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import type { NodeAct, NodeWordAnswer, NodeWordAsk, RunStep } from './run';
import { isReturnAct, RETURN_ACTS, ReturnChoice, returnWaits } from './return/ReturnChoice';
import { isTaskAct, TASK_ACTS, TaskPress } from './task/TaskPress';

/** The refusals of `WORKFLOW_NODE` the page words in the reader's language; any other is the driver's own sentence. */
const WORDED: ReadonlySet<string> = new Set([
  'worded', 'ended', 'no-words', 'not-skippable', 'not-in-table', 'not-held', 'promoted', 'not-request', 'not-review', 'not-sendable',
  'not-task', 'not-waiting', 'not-return', 'not-plugin-step',
]);

const ACTS: readonly NodeAct[] = ['skip', 'stop', 'send', 'ask-again', 'go-on', 'try-again', 'changes', ...TASK_ACTS, ...RETURN_ACTS];

/** What a refused word says, in the reader's language where the page words its code, else in the driver's sentence. */
export function nodeWordRefused(t: ReturnType<typeof useTranslation>['t'], answer: NodeWordAnswer): string {
  return answer.refused && WORDED.has(answer.refused)
    ? t(`workflow.run.node.refused.${answer.refused}`, { node: answer.node })
    : answer.message ?? answer.refused ?? '';
}

/**
 * **The person's skip and stop of a graph run's node**: under a node, the acts the
 * driver's one table takes where it stands (`acts`), each asking once in the page, a skip with the words it keeps; and a word the
 * person gave and the driver's next look has not applied yet (`asked`), drawn with their words in place of the presses.
 *
 * @remarks
 * **The driver judges, the page draws**: which acts a node offers is the driver's, so a go-ahead nobody raised is passed over here
 * and a landing is offered its stop alone. Each press goes out with its run and node (`onWord`) and comes back answered: kept closes the ask,
 * a refusal is said in it by its code, in the driver's sentence where the page has no words for the code.
 *
 * **A skip goes on without the node; a stop ends what waits on it**, so the stop asks in danger's hue and the skip in the primary's. *
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
    const act = ACTS.find((each) => each === step.asked?.act) ?? 'stop';
    return (
      <div className="grid gap-0.5">
        <p className="m-0 text-small text-ink-soft">{t(`workflow.run.node.asked.${act}`)}</p>
        {step.asked.words && <p className="m-0 text-small text-ink">{t('workflow.run.quoted', { words: step.asked.words })}</p>}
      </div>
    );
  }

  const acts = (step.acts ?? []).filter((act): act is NodeAct => act !== 'changes' && ACTS.includes(act as NodeAct));
  const choosing = acts.some(isReturnAct) && returnWaits(step);
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
      {choosing && asking === null && <ReturnChoice run={run} step={step} onWord={onWord} refusal={(answer) => nodeWordRefused(t, answer)} />}
      {asking === null && (
        <div className="flex flex-wrap items-center gap-1.5">
          {acts.filter(isTaskAct).map((act) => (
            <Button key={act} variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking(act)}>
              {t(`workflow.run.node.${act}`)}
            </Button>
          ))}
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
          {acts.includes('send') && (
            <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking('send')}>
              {t('workflow.run.node.send')}
            </Button>
          )}
          {acts.includes('ask-again') && (
            <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking('ask-again')}>
              {t('workflow.run.node.askAgain')}
            </Button>
          )}
          {acts.includes('go-on') && (
            <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking('go-on')}>
              {t('workflow.run.node.goOn')}
            </Button>
          )}
          {/* A failed plugin step is tried again, as a new attempt its plugin is sent. */}
          {acts.includes('try-again') && (
            <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking('try-again')}>
              {t('workflow.run.node.tryAgain')}
            </Button>
          )}
        </div>
      )}
      {asking === 'try-again' && (
        <InlineConfirm
          tone="primary"
          block
          label={t('workflow.run.node.tryAgainTitle', { node })}
          says={t('workflow.run.node.tryAgainSays', { node })}
          meanIt={t('workflow.run.node.tryAgainMeanIt')}
          onConfirm={(answered) => send('try-again', answered)}
          onClose={close}
        />
      )}
      {asking !== null && isTaskAct(asking) && (
        <TaskPress block act={asking} node={node} words={words} onWords={setWords} onConfirm={(answered) => send(asking, answered)} onClose={close} />
      )}
      {asking === 'send' && (
        <InlineConfirm
          tone="primary"
          block
          label={t('workflow.run.node.sendTitle', { node })}
          says={t('workflow.run.node.sendSays', { node })}
          meanIt={t('workflow.run.node.sendMeanIt')}
          onConfirm={(answered) => send('send', answered)}
          onClose={close}
        />
      )}
      {asking === 'ask-again' && (
        <InlineConfirm
          tone="primary"
          block
          label={t('workflow.run.node.askAgainTitle', { node })}
          says={t('workflow.run.node.askAgainSays', { node })}
          meanIt={t('workflow.run.node.askAgainMeanIt')}
          onConfirm={(answered) => send('ask-again', answered)}
          onClose={close}
        />
      )}
      {asking === 'go-on' && (
        <InlineConfirm
          tone="primary"
          block
          label={t('workflow.run.node.goOnTitle', { node })}
          says={t('workflow.run.node.goOnSays', { node })}
          meanIt={t('workflow.run.node.goOnMeanIt')}
          ready={words.trim().length > 0}
          onConfirm={(answered) => send('go-on', answered)}
          onClose={close}
        >
          <textarea
            aria-label={t('workflow.run.node.goOnWords')}
            placeholder={t('workflow.run.node.goOnWords')}
            rows={2}
            value={words}
            onChange={(event) => setWords(event.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </InlineConfirm>
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
