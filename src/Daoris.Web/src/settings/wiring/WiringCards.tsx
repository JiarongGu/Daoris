import type { SystemWiring } from '../../data/wiring';
import type { UpdateState } from '../../update/UpdateBanner';
import { WiringFiles } from './WiringFiles';
import { WiringFolders } from './WiringFolders';
import { WiringHead } from './WiringHead';
import { WiringKnowledge } from './WiringKnowledge';
import { WiringLogs } from './WiringLogs';
import { WiringLoop } from './WiringLoop';
import { WiringService } from './WiringService';
import { WiringTerminal } from './WiringTerminal';
import { WiringToolHome } from './WiringToolHome';
import { WiringVariablesCard } from './WiringVariables';
import { WiringVersions } from './WiringVersions';

/** Where This Daoris's doors lead, each named by the row that holds it; the domain knows the places. */
export type WiringDoors = {
  driver: () => void;
  tools: () => void;
  ai: () => void;
  logs: () => void;
  /** A settings file's door, by the word the read names its place with (the this-Daoris design §3.4). */
  file: (door: string) => void;
};

/**
 * Settings → This Daoris's cards, in the order the page shows them (WIRING1c, the this-Daoris design §2), drawn from the
 * shell's one answer. The domain (`WiringDomain`) holds the hooks and hands the answer here; a story hands a fixture, so
 * the whole domain is drawn at the frame's widths (the UI/UX design R6, UIFIT1). A molecule: every state by props.
 */
export function WiringCards({ wiring, update, busy = false, onMeasure, onOpenLogFolder, doors }: {
  wiring: SystemWiring;
  /** The install's update, the shell's own state (UPDATE1b): the Versions card's line. */
  update?: UpdateState;
  busy?: boolean;
  onMeasure: () => void;
  onOpenLogFolder: () => void;
  doors: WiringDoors;
}) {
  return (
    <>
      <WiringHead kind={wiring.folder.kind} />
      <WiringFolders wiring={wiring} busy={busy} onMeasure={onMeasure} />
      <WiringService wiring={wiring} />
      <WiringLoop loop={wiring.loop} onDriver={doors.driver} />
      <WiringFiles files={wiring.files} onDoor={doors.file} />
      <WiringVariablesCard variables={wiring.variables} />
      <WiringVersions
        versions={wiring.versions}
        update={update}
        development={wiring.folder.kind === 'development'}
        onTools={doors.tools}
        onDriver={doors.driver}
      />
      <WiringTerminal terminal={wiring.terminal} variables={wiring.variables} />
      <WiringKnowledge knowledge={wiring.knowledge} tier={wiring.service.tier} tierWhy={wiring.service.tierWhy} onAi={doors.ai} />
      <WiringLogs logs={wiring.logs} onOpenFolder={onOpenLogFolder} onLog={doors.logs} />
      {wiring.toolHome && <WiringToolHome toolHome={wiring.toolHome} measuring={wiring.measuring} />}
    </>
  );
}
