import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { span } from '../../locales/format';
import { Icon } from '../../design/ui';
import type { CallKind, FoldLine } from './turnFold';

/** Calls by kind in the reader's words, one phrase each: *2 edits*, *1 command*. */
export const callWords = (t: TFunction, calls: readonly { kind: CallKind; count: number }[]): string[] =>
  calls.map(({ kind, count }) => t(`work.catchup.call.${kind}`, { count }));

/**
 * An ended turn's one line, which opens the turn whole and folds it again: its calls by kind, its failures in the failed tone, the
 * files it changed, whether it thought and how long it took. What never folds stands under it either way.
 */
export function TurnFoldLine({ line, open, onToggle }: { line: FoldLine; open: boolean; onToggle: () => void }) {
  const { t, i18n } = useTranslation();
  const said = [
    ...callWords(t, line.calls),
    line.files > 0 ? t('work.catchup.fold.files', { count: line.files }) : null,
    line.thought ? t('work.conversation.thought') : null,
    line.took !== undefined ? span(line.took, i18n.language) : null,
  ].filter((part): part is string => part !== null);
  const words = said.length > 0 ? said.join(' · ') : t('work.conversation.worked');
  const failed = line.failed > 0 ? t('work.conversation.failed', { count: line.failed }) : null;

  return (
    <button
      type="button"
      aria-expanded={open}
      // One name for the whole line: the failed count is its own span for its tone.
      aria-label={failed ? `${words} · ${failed}` : words}
      onClick={onToggle}
      className="flex items-center gap-1.5 justify-self-start border-0 bg-transparent p-0 text-small text-ink-faint hover:text-ink"
    >
      <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} />
      {words}
      {failed && <span className="text-fail-ink">{`· ${failed}`}</span>}
    </button>
  );
}
