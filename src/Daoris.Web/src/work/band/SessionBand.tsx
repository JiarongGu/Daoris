import { type ComponentProps, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSessionStreams, useStopTask } from '../../bridge/shell';
import type { Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { BESIDE_READER, BesideReading } from '../conversation/SessionConversation';
import type { BandTold } from './bandRows';
import { LiveBand } from './LiveBand';

/** Below this height the window is short, and the band folds to its count. */
const SHORT = 640;

/**
 * The live band's organism, above the dock: what the attended session's conversation told it runs, the tasks its door can stop now
 * (its streams' `canStop`, which the driver sets only where its harness marked the task and its door stops one), and the stop itself,
 * asked of the session that runs the task and kept as the person's.
 */
export function SessionBand({ session, told, notify, exchanges, onOpenSession, nameOf }: {
  session: string;
  told: BandTold | null;
  notify: Notify;
  /** Its line's requests, as the at-work read answers them: each a session took up and the line waits on is a line. */
  exchanges?: ComponentProps<typeof LiveBand>['exchanges'];
  onOpenSession?: (session: string) => void;
  nameOf?: (agent: string) => string;
}) {
  const { t } = useTranslation();
  const streams = useSessionStreams(session);
  const stopTask = useStopTask();
  const short = useShortWindow();
  const prefix = `${session}/task/`;
  const stoppable = useMemo(
    () => new Set(streams.filter((row) => row.live && row.canStop === true && row.key.startsWith(prefix)).map((row) => row.key.slice(prefix.length))),
    [streams, prefix]);
  if (!told || told.session !== session) return null;
  const stop = async (task: string, name: string) => {
    try {
      const { stopped } = await stopTask.mutateAsync({ id: session, key: `${prefix}${task}` });
      if (!stopped) notify(t('work.band.notStopped', { name }), 'error');
      return stopped;
    } catch (error) {
      notify(sentence(error), 'error');
      return false;
    }
  };
  return (
    <BesideReading session={session}>
      <LiveBand
        lifetimes={told.lifetimes} act={told.act} reader={BESIDE_READER} stoppable={stoppable} onStop={stop} onOpen={told.open} short={short}
        exchanges={exchanges} onOpenSession={onOpenSession} nameOf={nameOf}
      />
    </BesideReading>
  );
}

/** Whether the window is short, read again as it is sized. */
function useShortWindow(): boolean {
  const [short, setShort] = useState(() => window.innerHeight < SHORT);
  useEffect(() => {
    const read = () => setShort(window.innerHeight < SHORT);
    window.addEventListener('resize', read);
    return () => window.removeEventListener('resize', read);
  }, []);
  return short;
}
