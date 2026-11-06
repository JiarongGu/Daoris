import { useTranslation } from 'react-i18next';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';

/** A person's task's presses (NODEKIT1 HUMAN): a task's *Done*, an approval's *Approve* and *Reject*. */
export type TaskAct = 'done' | 'approve' | 'reject';

export const TASK_ACTS: readonly TaskAct[] = ['done', 'approve', 'reject'];

export const isTaskAct = (act: string): act is TaskAct => (TASK_ACTS as readonly string[]).includes(act);

/** The note's words: a refusal is the run's rejection, which takes the person's reason, as a review's changes do; the rest take a note they may give. */
const wordsKey = (act: TaskAct) => (act === 'reject' ? 'workflow.run.node.rejectWords' : 'workflow.run.node.taskWords');

/**
 * **The person's press of a waiting task** (NODEKIT1 HUMAN; D245's second note): asked once, with a note they may give, which the run's record
 * keeps with the reach; a *Reject* waits for its reason, which the run's route for rejected carries. The run's view and *What needs you* each press it as the run's node word, so both say it in these words.
 *
 * A molecule: the act, the node, the note and the press arrive as props.
 */
export function TaskPress({ act, node, words, onWords, busy, block, className, onConfirm, onClose }: {
  act: TaskAct;
  node: string;
  words: string;
  onWords: (words: string) => void;
  busy?: boolean;
  /** Under a step of the run's view, the ask takes the whole width. */
  block?: boolean;
  className?: string;
  onConfirm: (answered: Answered) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      tone="primary"
      block={block}
      className={className}
      label={t(`workflow.run.node.${act}`)}
      says={t(`workflow.run.node.${act}Says`, { node })}
      meanIt={t(`workflow.run.node.${act}MeanIt`)}
      busy={busy}
      ready={act !== 'reject' || words.trim() !== ''}
      onConfirm={onConfirm}
      onClose={onClose}
    >
      <input
        aria-label={t(wordsKey(act))}
        placeholder={t(wordsKey(act))}
        value={words}
        onChange={(event) => onWords(event.target.value)}
        className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
      />
    </InlineConfirm>
  );
}
