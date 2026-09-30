import { useRef } from 'react';
import { useSessions } from '../queries';
import { useAskConversation } from './AskConversation';
import { AskPanel } from './AskPanel';
import { setupProgress, setupSteps } from './setup';
import { type StarterDoor, starters } from './starters';
import { useMachine } from './useMachine';
import { attendedOf, type HelpWhere } from './where';

/**
 * Ask Daoris's organism (HELP1, D89): it reads what the machine holds — the registry, the driver, the
 * agents, the sessions and the lines, through the one reading the setup guide shares (D97) — and hands
 * the panel what it lacks, and, where an agent is named, the conversation (HELP1a). Shell-only: most of
 * it is this machine's, which a browser may not learn (D47 §4).
 */
export function AskDaoris({
  where, attending = null, framed = true, opening, onOpened, width, range, onResize, onResetWidth, onGo, onClose,
}: {
  /** What is on the screen, told to the conversation ahead of the person's words (HELP1b). */
  where?: Omit<HelpWhere, 'session'>;
  /**
   * A question already asked, from the palette (DOCK1d) or the setup guide (SETUP1b): sent once per id,
   * as a typed one is.
   */
  opening?: { text: string; id: number } | null;
  /** Told once the opening is sent, so its holder lets it go and a later drawing does not send it again. */
  onOpened?: () => void;
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
  const { machine, settled } = useMachine();
  const scroller = useRef<HTMLDivElement>(null);
  // Every session, ended ones included: the same query Sessions makes, so it is asked once.
  const everything = useSessions(null, true);
  const helper = machine.helper;
  const conversation = useAskConversation(
    scroller, where ? { ...where, session: attendedOf(attending, everything.data ?? []) } : undefined, opening,
    helper, onOpened);

  const found = starters(machine);
  // The setup guide's standing, from the same reading (D97): the starters lead to it while it is not done.
  const setup = settled ? setupProgress(setupSteps(machine)) ?? undefined : undefined;

  return (
    <AskPanel
      starters={found}
      helper={helper}
      setup={setup}
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
