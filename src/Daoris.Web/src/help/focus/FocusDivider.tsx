import { type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { type Place } from '../focus';
import { type FocusMark } from '../history';

const placeWords = (t: (key: string, options?: Record<string, string>) => string, place: Place) =>
  place.kind === 'machine' ? t('help.focus.divider.machine')
    : place.kind === 'workspace' ? t('help.focus.divider.workspace', { workspace: place.workspace })
      : t('help.focus.divider.repository', { repository: place.repository });

/**
 * The transcript's line above the message that carried a change of focus (`docs/2026-11-11-ask-scope-ux-design.md` §5): a rule
 * with the change between, a widening the driver made for a place no longer here saying so, and the time. Drawn from the
 * message's record, so a reload, another window and a later reading draw it alike.
 */
export function FocusDivider({ mark }: { mark: FocusMark }) {
  const { t } = useTranslation();
  const said = t(mark.widened ? 'help.focus.divider.widened' : 'help.focus.divider.changed', { from: placeWords(t, mark.from), to: placeWords(t, mark.to) });
  const time = mark.at ? new Date(mark.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false }) : null;
  return (
    <div role="separator" aria-label={time ? `${said} · ${time}` : said} className="flex items-center gap-2 text-meta text-ink-faint">
      <span aria-hidden className="h-px flex-1 bg-line" />
      <span className="min-w-0 max-w-[85%] text-center wrap-anywhere">{said}{time && <> · {time}</>}</span>
      <span aria-hidden className="h-px flex-1 bg-line" />
    </div>
  );
}

/** A conversation's dividers by the id of the message that carried each change; a mark that names no message draws none. */
export function focusDividers(marks: readonly FocusMark[] | undefined): Record<string, ReactNode> {
  const lines: Record<string, ReactNode> = {};
  for (const mark of marks ?? []) if (mark.word) lines[mark.word] = <FocusDivider key={mark.word} mark={mark} />;
  return lines;
}
