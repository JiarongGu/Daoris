import type { Session } from '../data/api';
import type { RepositoryLine } from '../settings/repository/Lines';
import { type AgentSheet, byTool, type Tool, type ToolDoor } from '../agents/tools';
import { answeredPark } from '../design/ui';

/**
 * What this machine holds, as Ask Daoris's starters and the setup guide read
 * it: one reading for both, so a starter and a step never disagree.
 */
export type Machine = {
  /** Whether a shell answers. Without one only the registry is this page's to know. */
  attached: boolean;
  /** The repositories registered here. */
  repositories: readonly string[];
  drivable: readonly string[];
  tools: readonly Tool[];
  /**
   * How many sessions wait on the person. Not a park they answered: the same session goes on with it at the
   * driver's next look, as the session list already says.
   */
  waiting: number;
  /**
   * Driven repositories with no line set and none git can name. One imported and never driven is not counted:
   * it has no line to want until the driver drives it.
   */
  unnamedLines: readonly string[];
  /** The agent Ask Daoris runs on, or null. */
  helper: string | null;
  /** The agent an ask the declarations leave opens an intake on, or null. */
  intake: string | null;
};

/**
 * The machine, read from what the page's queries answered. Pure, so every machine is an argument.
 *
 * @remarks
 * **A browser reads the registry and nothing else**, whatever it is handed: the rest is this machine's,
 * which a browser may not learn, and its queries never fire there anyway. An answer not yet in
 * reads as empty; the caller says whether the reading has settled.
 */
export function readMachine(answers: {
  attached: boolean;
  registry?: readonly { repository: string }[];
  /** The driver's standing state. The adapters are `""` for none: the bridge leaves a null out. */
  driver?: { drivable?: readonly string[]; helperAgent?: string; intakeAgent?: string };
  harnesses?: readonly ToolDoor[];
  agents?: readonly AgentSheet[];
  sessions?: readonly Pick<Session, 'state' | 'said'>[];
  lines?: readonly RepositoryLine[];
}): Machine {
  const repositories = (answers.registry ?? []).map((row) => row.repository);
  if (!answers.attached) {
    return {
      attached: false, repositories, drivable: [], tools: [], waiting: 0, unnamedLines: [], helper: null, intake: null,
    };
  }

  const drivable = answers.driver?.drivable ?? [];
  return {
    attached: true,
    repositories,
    drivable,
    tools: byTool(answers.harnesses ?? [], answers.agents),
    waiting: (answers.sessions ?? []).filter((session) => session.state === 'awaiting-person' && !answeredPark(session)).length,
    unnamedLines: (answers.lines ?? [])
      .filter((line) => !line.notReadYet && line.source === 'none' && drivable.includes(line.repository))
      .map((line) => line.repository),
    helper: answers.driver?.helperAgent || null,
    intake: answers.driver?.intakeAgent || null,
  };
}
