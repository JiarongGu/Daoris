import { useEffect, useRef, useState } from 'react';
import { setupSteps } from './help/setup';
import type { StarterDoor } from './help/starters';
import { useMachine } from './help/useMachine';
import { store, stored } from './lib/stored';
import { GetStarted } from './settings/GetStarted';

/**
 * Whether this viewer turned off opening Get started at start (SETUP1b): `off`, or nothing. A per-viewer
 * convenience in this browser's storage, never machine state: a refused storage reads as not chosen.
 */
export const AT_START = 'daoris.setup.atStart';

const atStartChosen = () => stored(AT_START) !== 'off';

/**
 * Settings → Get started (SETUP1a, D97): the organism over the guide. It reads the machine through the
 * one reading Ask Daoris's starters share, and hands the steps to the molecule that draws them.
 *
 * @remarks
 * The doors lead out of Settings — to another domain, to Projects, to the Workspace menu's drawers — so
 * where they go is the application's to decide, as Ask Daoris's starters' doors are. So is the hand-off
 * (SETUP1b): *Set up with Ask Daoris* opens the side bar on a first message, which only a shell has, so
 * a browser is handed neither it nor *Don't open at start*.
 */
export function GetStartedDomain({ attached, onGo, onAsk }: {
  attached: boolean;
  onGo: (door: StarterDoor) => void;
  onAsk?: (message: string) => void;
}) {
  const { machine, settled } = useMachine();
  const [atStart, setAtStart] = useState(atStartChosen);

  return (
    <GetStarted
      steps={setupSteps(machine)}
      // Until every answer is in, a step would read *to do* of a machine that has it done.
      reading={!settled}
      helper={machine.helper}
      onGo={onGo}
      {...(attached ? {
        onAsk,
        atStart,
        onAtStart: (open: boolean) => {
          setAtStart(open);
          store(AT_START, open ? null : 'off');
        },
      } : {})}
    />
  );
}

/**
 * Open Get started at start (SETUP1b, D97 §2): once per start, on a machine missing any of the first three
 * steps, unless this viewer turned it off.
 *
 * @remarks
 * **Decided once, when the machine has been read**: before the roster answers, a machine with an agent
 * reads as one without. Whatever that decision was, nothing reopens it that start — a step coming undone
 * later is the status bar's and the starters' to say, not a reason to pull the person away.
 *
 * **Never from where the person went**: a view changed before the reading settled is the person's
 * choice, and the guide does not replace it.
 */
export function useSetupAtStart({ settled, needed, view, open }: {
  /** Whether every answer the reading needs is in. */
  settled: boolean;
  /** Whether one of the first three steps is to do on this machine. */
  needed: boolean;
  /** The view in front of the person. */
  view: string;
  open: () => void;
}) {
  const first = useRef(view);
  const decided = useRef(false);
  useEffect(() => {
    if (decided.current || !settled) return;
    decided.current = true;
    if (needed && view === first.current && atStartChosen()) open();
    // Only the reading settling decides; `open` is this render's and not a reason to decide again.
  }, [settled, needed, view]);
}
