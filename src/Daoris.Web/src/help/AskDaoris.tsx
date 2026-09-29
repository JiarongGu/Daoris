import { useRef } from 'react';
import { useRegistry, useSessions } from '../queries';
import { useDriver, useHarnesses, useLines } from '../shell';
import { byTool } from '../tools';
import { useAskConversation } from './AskConversation';
import { AskPanel } from './AskPanel';
import { type StarterDoor, starters } from './starters';
import { attendedOf, type HelpWhere } from './where';

/**
 * Ask Daoris's organism (HELP1, D89): it reads what the machine holds — the registry, the driver, the
 * agents, the sessions and the lines — and hands the panel what it lacks, and, where an agent is named,
 * the conversation (HELP1a). Shell-only: most of it is this machine's, which a browser may not learn
 * (D47 §4).
 */
export function AskDaoris({
  where, attending = null, framed = true, opening, width, range, onResize, onResetWidth, onGo, onClose,
}: {
  /** What is on the screen, told to the conversation ahead of the person's words (HELP1b). */
  where?: Omit<HelpWhere, 'session'>;
  /** A question already asked, from the palette (DOCK1d): sent once per id, as a typed one is. */
  opening?: { text: string; id: number } | null;
  /** The session attended, found here among every session — an ended one is still what the person reads. */
  attending?: string | null;
  /** Its own region (true), or a tab of Sessions' right dock, whose frame and close are the dock's. */
  framed?: boolean;
  width?: number;
  range?: { min: number; max: number };
  onResize?: (width: number) => void;
  onResetWidth?: () => void;
  onGo: (door: StarterDoor) => void;
  onClose: () => void;
}) {
  const registry = useRegistry('machine');
  const driver = useDriver();
  const roster = useHarnesses();
  const sessions = useSessions(null, false);
  const lines = useLines();
  const scroller = useRef<HTMLDivElement>(null);
  // Every session, ended ones included: the same query Sessions makes, so it is asked once.
  const everything = useSessions(null, true);
  const conversation = useAskConversation(
    scroller, where ? { ...where, session: attendedOf(attending, everything.data ?? []) } : undefined, opening);

  const helper = driver.data?.helperAdapter || null;
  const found = starters({
    repositories: (registry.data ?? []).map((row) => row.repository),
    drivable: driver.data?.drivable ?? [],
    tools: byTool(Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : []),
    waiting: (sessions.data ?? []).filter((session) => session.state === 'awaiting-person').length,
    unnamedLines: (Array.isArray(lines.data?.lines) ? lines.data.lines : [])
      .filter((line) => line.source === 'none')
      .map((line) => line.repository),
    helper,
  });

  return (
    <AskPanel
      starters={found}
      helper={helper}
      conversation={helper ? conversation : undefined}
      scroller={scroller}
      framed={framed}
      width={width}
      range={range}
      onResize={onResize}
      onResetWidth={onResetWidth}
      onGo={onGo}
      onClose={onClose}
    />
  );
}
