import { useTranslation } from 'react-i18next';
import { useSessionConsole } from './shell';
import { MonoWell } from './ui';

/**
 * A session's console, live (D49 §2) — the transcript capture as it happens.
 *
 * @remarks
 * An organism, deliberately: it holds the hook so that `MonoWell` — which does the rendering, the
 * tail-following and the drop footer — holds none, and can therefore be reviewed in every state
 * without a driver attached.
 *
 * It renders only where a shell is attached, because only a driver has a stream to give — in a
 * browser the hook asks nobody and this stays absent, which is the same rule the stop control
 * follows.
 *
 * Shared by the quest drawer (a driven session's record) and the chat drawer (a conversation), for
 * the reason those two are one entity in the first place: the console does not care which way in a
 * session was entered.
 */
export function SessionConsole({ id, tall }: { id: string; tall?: boolean }) {
  const { t } = useTranslation();
  const { lines, live, dropped } = useSessionConsole(id);

  if (lines.length === 0 && !live) return null;

  return (
    <div className="mt-3">
      <MonoWell
        label={t('console.label')}
        live={live}
        dropped={dropped}
        tall={tall}
        text={lines.map((line) => line.text).join('\n')}
      />
    </div>
  );
}
