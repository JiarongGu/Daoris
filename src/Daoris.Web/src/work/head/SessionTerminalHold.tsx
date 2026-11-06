import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLetGoOfTerminal, useSessionHold } from '../../bridge/hold';
import { TerminalHold } from './TerminalHold';

/** The hold of `session` as the shell tells it, with its door; nothing where no terminal holds it. */
export function SessionTerminalHold({ session }: { session: string }) {
  const { t } = useTranslation();
  const hold = useSessionHold(session);
  const letGo = useLetGoOfTerminal();
  const [said, setSaid] = useState<string | null>(null);
  if (!hold) return null;
  const press = async () => {
    setSaid(null);
    const answer = await letGo.mutateAsync(session).catch((error: Error) => ({ done: false, why: null, message: error.message }));
    if (!answer.done) setSaid(answer.why === 'not-held' ? t('work.head.notHeld') : answer.message ?? null);
  };
  return <TerminalHold hold={hold} busy={letGo.isPending} said={said} onLetGo={() => void press()} />;
}
