import { type ReactNode, useEffect, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, Inline, SettingList, Tip } from '../../design/ui';

/** One fold of a place's Workflow: its name, the line naming its values, its rows, and whether the person's need opens it. */
export type FoldSpec = {
  id: string;
  title: string;
  /** Its values in a line, each with where it stands; said to a reader and in the fold's tip, and above the rows once it is open. */
  summary: string;
  body: ReactNode;
  /** Something here waits for the person, so it opens itself. */
  opens?: boolean;
};

/** A name set as code, read as words: a tip and a description are plain text. */
const plain = (text: string) => text.replace(/`/g, '');

/**
 * **The folds of a place's Workflow**: *Gates*, *Landing*, *Second opinion*, *Your look*,
 * *Landed route*, *The line* and *More* as one wrapped row of names, so the head and the three section lines keep the first screen. Each
 * name opens its rows below the row, and says its values to a reader and in its tip. One opens itself for a need, and again each time a
 * door asks for it.
 *
 * A molecule: every state is reached by props, and the rows' presses are their owners'.
 */
export function FoldRow({ folds, open, again }: { folds: readonly FoldSpec[]; open?: string; again?: number }) {
  const { t } = useTranslation();
  const base = useId();
  const needing = folds.filter((fold) => fold.opens).map((fold) => fold.id).join(' ');
  const [opened, setOpened] = useState<ReadonlySet<string>>(() => new Set(needing ? needing.split(' ') : []));
  useEffect(() => {
    if (needing) setOpened((was) => new Set([...was, ...needing.split(' ')]));
  }, [needing]);
  useEffect(() => {
    if (open) setOpened((was) => new Set([...was, open]));
  }, [open, again]);

  const toggle = (id: string) => setOpened((was) => {
    const next = new Set(was);
    if (!next.delete(id)) next.add(id);
    return next;
  });

  return (
    <div className="grid min-w-0 gap-2">
      <div role="group" aria-label={t('workflow.place.folds')} className="flex flex-wrap gap-x-4 gap-y-0.5">
        {folds.map((fold) => {
          const shown = opened.has(fold.id);
          return (
            <Tip key={fold.id} content={plain(fold.summary)}>
              <button
                type="button"
                data-place-row={fold.id}
                aria-expanded={shown}
                aria-controls={`${base}-${fold.id}`}
                aria-describedby={`${base}-${fold.id}-says`}
                onClick={() => toggle(fold.id)}
                className="inline-flex min-h-[1.5rem] items-center gap-1 rounded-control text-small text-ink-soft hover:text-ink aria-expanded:font-medium aria-expanded:text-ink"
              >
                <Icon name={shown ? 'chevronDown' : 'chevronRight'} size={12} />
                {fold.title}
              </button>
            </Tip>
          );
        })}
      </div>
      {folds.map((fold) => <span key={fold.id} id={`${base}-${fold.id}-says`} hidden>{plain(fold.summary)}</span>)}
      {folds.filter((fold) => opened.has(fold.id)).map((fold) => (
        <section
          key={fold.id}
          id={`${base}-${fold.id}`}
          aria-labelledby={`${base}-${fold.id}-name`}
          className="grid min-w-0 gap-1.5 rounded-control border border-line px-3 py-2"
        >
          <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-0.5">
            <h3 id={`${base}-${fold.id}-name`} className="m-0 text-body font-semibold text-ink">{fold.title}</h3>
            <p className="m-0 min-w-0 text-small text-ink-soft"><Inline text={fold.summary} /></p>
          </div>
          <SettingList>{fold.body}</SettingList>
        </section>
      ))}
    </div>
  );
}
