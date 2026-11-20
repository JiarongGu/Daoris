import { createContext, type ReactNode, useContext, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useChildHistory, useSessionConsole } from '../../bridge/shell';
import { SessionConsole } from '../console/SessionConsole';
import type { BesideReader, ChildRead } from './besides';

/** The session whose subagents' and tasks' rows the reader below reads for. */
const ReadingSession = createContext('');

/** A subagent's own record, read through the driver for the row that names it. */
function ChildReader({ child, running = false, children }: { child: string; running?: boolean; children: (read: ChildRead) => ReactNode }) {
  return children(useChildHistory(useContext(ReadingSession), child, running));
}

/** A background task's last lines, from its own console stream (`<session>/task/<id>`), and when the page heard its last. */
function TaskTail({ task, children }: { task: string; children: (lines: readonly string[], heard: number | null) => ReactNode }) {
  const tail = useSessionConsole(`${useContext(ReadingSession)}/task/${task}`);
  const last = tail.lines[tail.lines.length - 1]?.sequence ?? 0;
  const [heard, setHeard] = useState<number | null>(null);
  useEffect(() => { if (last > 0) setHeard(Date.now()); }, [last]);
  return children(tail.lines.slice(-3).map((line) => line.text), heard);
}

/** A background task's whole console, in the panel's focus. */
function TaskStream({ task }: { task: string }) {
  const { t } = useTranslation();
  return <SessionConsole id={`${useContext(ReadingSession)}/task/${task}`} quiet={t('work.panel.streamSilent')} />;
}

/** How the rows, the story and the live band read what a session runs beside itself, under `BesideReading`. */
export const BESIDE_READER: BesideReader = { Child: ChildReader, Tail: TaskTail, Stream: TaskStream };

/** The session whose subagents and tasks `BESIDE_READER` reads: the conversation's, the story's and the live band's. */
export function BesideReading({ session, children }: { session: string; children: ReactNode }) {
  return <ReadingSession.Provider value={session}>{children}</ReadingSession.Provider>;
}
