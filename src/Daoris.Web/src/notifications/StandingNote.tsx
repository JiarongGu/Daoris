import { type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';

/**
 * A standing line of *Notifications* drawn compactly: one line holding its words, its presses (*Back to ask*), *Show* for the detail
 * below it and *Got it*, which says the person has seen what is there. The detail is folded until asked for.
 *
 * A molecule: drawn from props.
 */
export function StandingNote({ dataAttribute, label, words, actions, show, hide, onSeen, defaultOpen = false, children }: {
  /** The marker the line's element carries (`data-terminal-tier-line`). */
  dataAttribute: string;
  label: string;
  /** What the line says: each as one short sentence, run together on one line. */
  words: ReactNode[];
  /** The line's own presses, before *Show*. */
  actions?: ReactNode;
  show: string;
  hide: string;
  /** *Got it*: with none, the line has no such press. */
  onSeen?: () => void;
  defaultOpen?: boolean;
  /** The detail behind *Show*; with none, there is no *Show*. */
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  return (
    <section {...{ [dataAttribute]: '' }} aria-label={label} className="grid gap-1 border-l-[3px] border-l-warn pl-2">
      <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5">
        <p className="m-0 min-w-0 text-small text-ink-soft">
          {words.map((each, at) => <span key={at}>{at > 0 && <span aria-hidden="true"> · </span>}<span>{each}</span></span>)}
        </p>
        <span className="flex flex-wrap items-center gap-1">
          {actions}
          {children && (
            <Button variant="ghost" aria-expanded={open} aria-controls={`${id}-detail`} onClick={() => setOpen((was) => !was)}>{open ? hide : show}</Button>
          )}
          {onSeen && <Button variant="ghost" onClick={onSeen}>{t('notifications.standing.seen')}</Button>}
        </span>
      </div>
      {open && children && <div id={`${id}-detail`} className="grid gap-2">{children}</div>}
    </section>
  );
}
