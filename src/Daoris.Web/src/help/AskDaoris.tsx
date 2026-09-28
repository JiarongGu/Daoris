import { useRegistry, useSessions } from '../queries';
import { useDriver, useHarnesses, useLines } from '../shell';
import { byTool } from '../tools';
import { AskPanel } from './AskPanel';
import { type StarterDoor, starters } from './starters';

/**
 * Ask Daoris's organism (HELP1, D89): it reads what the machine holds — the registry, the driver, the
 * agents, the sessions and the lines — and hands the panel what it lacks. Shell-only: most of it is
 * this machine's, which a browser may not learn (D47 §4).
 */
export function AskDaoris({ onGo, onClose }: { onGo: (door: StarterDoor) => void; onClose: () => void }) {
  const registry = useRegistry('machine');
  const driver = useDriver();
  const roster = useHarnesses();
  const sessions = useSessions(null, false);
  const lines = useLines();

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

  return <AskPanel starters={found} helper={helper} onGo={onGo} onClose={onClose} />;
}
