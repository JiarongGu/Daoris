import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../../locales/format';
import { Button } from '../../design/ui';
import type { NodeWordAnswer, NodeWordAsk, RunStep } from '../run';

/** A choice for one file a folder's return found changed in both the folder and the session's copy. */
export type ReturnAct = 'keep-yours' | 'take-session' | 'keep-both';

export const RETURN_ACTS: readonly ReturnAct[] = ['keep-yours', 'take-session', 'keep-both'];

export const isReturnAct = (act: string): act is ReturnAct => (RETURN_ACTS as readonly string[]).includes(act);

/** What a return found at a path it refuses to bring back rather than guess at, as its wait names it. */
export type ReturnRefusal = 'escapes' | 'link-copy' | 'link-folder' | 'type-copy' | 'type-folder' | 'collision';

const lines = (text: string | undefined): string[] => (text ?? '').split('\n').filter((line) => line.length > 0);

/** The paths a return waits on the person for, as its wait names them; none where it waits on none. */
export const waitingPaths = (step: RunStep): string[] =>
  step.wait?.code === 'both-changed' ? lines(step.wait.values?.paths) : [];

/** The paths a return refuses, each with what it found there: a line of the wait's `refused`, the path, a tab and the code. */
export const refusedPaths = (step: RunStep): { path: string; found: ReturnRefusal }[] =>
  step.wait?.code === 'both-changed'
    ? lines(step.wait.values?.refused).map((line) => {
      const tab = line.lastIndexOf('\t');
      return { path: line.slice(0, tab), found: line.slice(tab + 1) as ReturnRefusal };
    })
    : [];

/** Whether a return waits on the person for any path, changed on both sides or refused. */
export const returnWaits = (step: RunStep): boolean => waitingPaths(step).length + refusedPaths(step).length > 0;

/**
 * **The person's choice for each file changed on both sides**: each path with *Keep yours*, *Take the session's* and *Keep both*, each
 * pressed as the run's node word naming that path; a path the return refused says what it found there and offers *Keep yours* alone.
 * Nothing is chosen for the person: a path pressed says the choice made, and a refusal is said on its row, in the reader's words.
 *
 * A molecule: the step, the press and the refusal's words arrive as props.
 */
export function ReturnChoice({ run, step, onWord, refusal }: {
  run: string;
  step: RunStep;
  onWord: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  refusal: (answer: NodeWordAnswer) => string;
}) {
  const { t } = useTranslation();
  const [chosen, setChosen] = useState<Record<string, ReturnAct>>({});
  const [refused, setRefused] = useState<Record<string, string>>({});
  const node = step.step.id;

  const press = (path: string, act: ReturnAct) => {
    onWord({ run, node, act, words: path })
      .then((answer) => (answer.done
        ? setChosen((was) => ({ ...was, [path]: act }))
        : setRefused((was) => ({ ...was, [path]: refusal(answer) }))))
      .catch((error: unknown) => setRefused((was) => ({ ...was, [path]: sentence(error) })));
  };

  const row = (path: string, acts: readonly ReturnAct[], found?: ReturnRefusal) => (
    <li key={path} className="flex min-w-0 flex-wrap items-center gap-1.5">
      <code className="text-small [overflow-wrap:anywhere]">{path}</code>
      {found && <span className="text-small text-ink-soft">{t(`workflow.run.node.returnRefused.${found}`)}</span>}
      {chosen[path]
        ? <span className="text-small text-ink-soft">{t(`workflow.run.node.asked.${chosen[path]}`)}</span>
        : acts.map((act) => (
          <Button key={act} variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => press(path, act)}>
            {t(`workflow.run.node.${act}`)}
          </Button>
        ))}
      {refused[path] && !chosen[path] && <span className="basis-full text-small text-ink-danger">{refused[path]}</span>}
    </li>
  );
  const changed = waitingPaths(step);
  const found = refusedPaths(step);

  return (
    <div className="grid min-w-0 gap-1">
      {changed.length > 0 && <p className="m-0 text-small text-ink-soft">{t('workflow.run.node.returnChoice')}</p>}
      {found.length > 0 && <p className="m-0 text-small text-ink-soft">{t('workflow.run.node.returnRefusedHead')}</p>}
      <ul className="m-0 grid list-none gap-1 p-0">
        {changed.map((path) => row(path, RETURN_ACTS))}
        {found.map((each) => row(each.path, ['keep-yours'], each.found))}
      </ul>
    </div>
  );
}
