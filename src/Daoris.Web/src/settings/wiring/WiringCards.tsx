import type { ReactNode } from 'react';
import type { SystemWiring } from '../../data/wiring';
import { WiringFiles } from './WiringFiles';
import { WiringHead } from './WiringHead';
import { WiringKnowledge } from './WiringKnowledge';
import { WiringLogs } from './WiringLogs';
import { WiringLoop } from './WiringLoop';
import { WiringService } from './WiringService';
import { WiringTerminal } from './WiringTerminal';
import { WiringToolHome } from './WiringToolHome';
import { WiringVariablesCard } from './WiringVariables';

/** Where This Daoris's doors lead, each named by the row that holds it; the domain knows the places. */
export type WiringDoors = {
  driver: () => void;
  ai: () => void;
  logs: () => void;
  /** A settings file's door, by the word the read names its place with. */
  file: (door: string) => void;
};

/**
 * Settings → This Daoris's cards in the page's order: what a person sets here first (the terminal, the service), then what
 * stands behind them. A molecule: `WiringDomain` holds the hooks, and a story hands a fixture. */
export function WiringCards({ wiring, onOpenLogFolder, doors, onOpen, terminalSetup, addressSetup }: {
  /** *Open* on a file row, by the id the read gave. */
  onOpen?: (id: string) => void;
  /** The Terminal card's set-up rows: *Put on my PATH* and *Copy a terminal line*. */
  terminalSetup?: ReactNode;
  /** The Service card's *Change the service's address*. */
  addressSetup?: ReactNode;
  wiring: SystemWiring;
  onOpenLogFolder: () => void;
  doors: WiringDoors;
}) {
  return (
    <>
      <WiringHead kind={wiring.folder.kind} />
      <WiringTerminal terminal={wiring.terminal} variables={wiring.variables} setup={terminalSetup} />
      <WiringService wiring={wiring} setup={addressSetup} />
      <WiringFiles files={wiring.files} onDoor={doors.file} onOpen={onOpen} />
      <WiringVariablesCard variables={wiring.variables} />
      <WiringLoop loop={wiring.loop} onDriver={doors.driver} />
      <WiringKnowledge knowledge={wiring.knowledge} tier={wiring.service.tier} tierWhy={wiring.service.tierWhy} onAi={doors.ai} />
      <WiringLogs logs={wiring.logs} onOpenFolder={onOpenLogFolder} onLog={doors.logs} />
      {wiring.toolHome && <WiringToolHome toolHome={wiring.toolHome} measuring={wiring.measuring} />}
    </>
  );
}
