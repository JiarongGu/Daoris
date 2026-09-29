import type { RepositoryLanding } from '../settings/Landings';
import type { RepositoryLine } from '../settings/Lines';
import { byTool, type Tool, type ToolDoor } from '../tools';

/**
 * What this machine holds, as Ask Daoris's starters (HELP1d) and the setup guide (SETUP1a, D97) read
 * it: one reading for both, so a starter and a step never disagree.
 */
export type Machine = {
  /** Whether a shell answers. Without one only the registry is this page's to know (D47 §4). */
  attached: boolean;
  /** The repositories registered here. */
  repositories: readonly string[];
  drivable: readonly string[];
  tools: readonly Tool[];
  /** How many sessions wait on the person. */
  waiting: number;
  /** Repositories with no line set and none git can name. */
  unnamedLines: readonly string[];
  /** The agent Ask Daoris runs on, or null. */
  helper: string | null;
  /** The agent an ask the declarations leave opens an intake on, or null. */
  intake: string | null;
  /** Driven repositories whose work lands by the default: no rule set for them or their workspace. */
  unlanded: readonly string[];
};

/**
 * The machine, read from what the page's queries answered. Pure, so every machine is an argument.
 *
 * @remarks
 * **A browser reads the registry and nothing else**, whatever it is handed: the rest is this machine's,
 * which a browser may not learn (D47 §4), and its queries never fire there anyway. An answer not yet in
 * reads as empty; the caller says whether the reading has settled.
 */
export function readMachine(answers: {
  attached: boolean;
  registry?: readonly { repository: string }[];
  /** The driver's standing state. The adapters are `""` for none: the bridge leaves a null out. */
  driver?: { drivable?: readonly string[]; helperAdapter?: string; intakeAdapter?: string };
  harnesses?: readonly ToolDoor[];
  sessions?: readonly { state: string }[];
  lines?: readonly RepositoryLine[];
  landings?: readonly RepositoryLanding[];
}): Machine {
  const repositories = (answers.registry ?? []).map((row) => row.repository);
  if (!answers.attached) {
    return {
      attached: false, repositories, drivable: [], tools: [], waiting: 0, unnamedLines: [], helper: null, intake: null, unlanded: [],
    };
  }

  const drivable = answers.driver?.drivable ?? [];
  return {
    attached: true,
    repositories,
    drivable,
    tools: byTool(answers.harnesses ?? []),
    waiting: (answers.sessions ?? []).filter((session) => session.state === 'awaiting-person').length,
    unnamedLines: (answers.lines ?? []).filter((line) => line.source === 'none').map((line) => line.repository),
    helper: answers.driver?.helperAdapter || null,
    intake: answers.driver?.intakeAdapter || null,
    unlanded: (answers.landings ?? [])
      .filter((landing) => landing.source === 'default' && drivable.includes(landing.repository))
      .map((landing) => landing.repository),
  };
}
