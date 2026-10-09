import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Icon } from '../ui';
import { waitsOnYou, type WorkflowRun } from './run';
import { runShort } from './runSaid';

/**
 * **A record's work in its workflow, in one line** (WORKFLOW1c; the workflow design §7): a quest's or an ask's page carries it
 * among its head's facts, *Workflow: waits for your look*, and its door opens the run in the session's side bar. An ask's work in
 * several repositories says each run apart (design §3.8), each its own door.
 *
 * @remarks
 * Where a run waits on the person its words wear open's hue, the person's alone (platform-ux §3), beside the word that says it,
 * so it is never hue alone. With no run, nothing is drawn: a browser has no driver to read one (D47 §4), and a quest not
 * published has none yet.
 *
 * A molecule: the runs arrive as a prop, and the door goes out.
 */
export function WorkflowLine({ runs, onOpen }: {
  runs: readonly WorkflowRun[];
  /** Open the run: its session attended, the side bar on its *Workflow*. Absent, the line is said with no door. */
  onOpen?: (run: WorkflowRun) => void;
}) {
  const { t } = useTranslation();
  if (runs.length === 0) return null;
  const named = runs.length > 1;

  return (
    <span className="inline-flex min-w-0 flex-wrap items-baseline gap-x-1">
      <Icon name="workflow" size={12} className="translate-y-0.5 text-ink-faint" />
      {runs.map((run, index) => {
        const says = runShort(t, run);
        const words = named
          ? t('workflow.run.lineIn', { repository: run.repository, says })
          : t('workflow.run.line', { says });
        const tone = waitsOnYou(run) ? 'text-ink-open' : 'text-ink-soft';
        return (
          <Fragment key={`${run.repository}:${run.quests[0] ?? index}`}>
            {index > 0 && <span aria-hidden>·</span>}
            {onOpen && run.session
              ? (
                <button
                  type="button"
                  onClick={() => onOpen(run)}
                  title={t('workflow.run.viewNamed')}
                  className={cn(
                    'cursor-pointer border-0 bg-transparent p-0 text-small underline decoration-line-strong underline-offset-2',
                    'hover:text-accent hover:decoration-accent',
                    tone,
                  )}
                >
                  {words}
                </button>
              )
              : <span className={tone}>{words}</span>}
          </Fragment>
        );
      })}
    </span>
  );
}
