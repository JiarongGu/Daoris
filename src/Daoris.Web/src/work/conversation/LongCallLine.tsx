import { useTranslation } from 'react-i18next';
import { Button, Tip } from '../../design/ui';
import { elapsed } from '../../locales/format';
import { useNow } from '../useNow';
import type { RunningCall } from './conversation';

/** How long a tool call runs before the conversation's line says so. */
export const LONG_CALL_MS = 2 * 60_000;

/**
 * A tool call running past {@link LONG_CALL_MS}, said on the conversation's line with how long it has run, stepping with the clock,
 * and *Stop* beside it. A word only: nothing ends the call but the person's press.
 */
export function LongCallLine({ call, stopping, onStop }: { call: RunningCall; stopping: boolean; onStop: () => void }) {
  const { t } = useTranslation();
  const now = useNow(15_000);
  if (now - new Date(call.since).getTime() < LONG_CALL_MS) return null;
  return (
    <p role="status" className="m-0 flex flex-wrap items-center gap-2 text-small text-ink-soft">
      <span className="wrap-anywhere">{t('work.conversation.longCall', { title: call.title, elapsed: elapsed(call.since, new Date(now).toISOString()) })}</span>
      <Tip content={t('help.stopTip')}>
        <Button type="button" disabled={stopping} onClick={onStop}>{t('help.stop')}</Button>
      </Tip>
    </p>
  );
}
