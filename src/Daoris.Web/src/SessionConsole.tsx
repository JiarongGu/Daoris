import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useSessionConsole } from './shell';

/**
 * A session's console, live (D49 §2) — the transcript capture as it happens.
 *
 * @remarks
 * **Verbatim, and never translated**: output is data, which is where the platform's i18n boundary
 * sits. The chrome around it speaks the active language; what the session said does not.
 *
 * It renders only where a shell is attached, because only a driver has a stream to give — in a
 * browser the hook asks nobody and this stays absent, which is the same rule the stop control
 * follows. The window is bounded, so what fell out of it is stated rather than quietly skipped.
 *
 * Shared by the quest drawer (a driven session's record) and the chat drawer (a conversation), for
 * the reason those two are one entity in the first place: the console does not care which way in a
 * session was entered.
 */
export function SessionConsole({ id, tall }: { id: string; tall?: boolean }) {
  const { t } = useTranslation();
  const { lines, live, dropped } = useSessionConsole(id);
  const well = useRef<HTMLPreElement>(null);

  // Follow the tail while it is running: a console a person has to scroll to watch is one they will
  // stop watching. Only while live — scrolling a finished log out from under a reader is rude.
  useEffect(() => {
    if (live && well.current) well.current.scrollTop = well.current.scrollHeight;
  }, [lines, live]);

  if (lines.length === 0 && !live) return null;

  return (
    <div className="mt-3">
      <p className="mb-1 flex items-baseline gap-2 text-[0.72rem] text-ink-faint">
        <span>{t('quests.session.console')}</span>
        {live && (
          <span className="inline-flex items-center gap-1 text-accent">
            <span className="inline-block size-1.5 animate-pulse rounded-full bg-accent" />
            {t('quests.session.streaming')}
          </span>
        )}
        {dropped > 0 && <span>{t('quests.session.dropped', { count: dropped })}</span>}
      </p>
      <pre
        ref={well}
        className={`m-0 overflow-auto whitespace-pre-wrap break-words rounded-control border border-line bg-raised px-3 py-2.5 font-mono text-[0.75rem] leading-relaxed text-ink-soft ${tall ? 'max-h-[26rem] min-h-40' : 'max-h-72'}`}
      >
        {lines.map((line) => line.text).join('\n')}
      </pre>
    </div>
  );
}
