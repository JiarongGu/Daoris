import { useRegistry, useSessions } from '../queries';
import { useDriver, useHarnesses, useLines } from '../shell';
import { type Machine, readMachine } from './machine';

/**
 * The machine as the page's queries answer it, read once for Ask Daoris's starters, the setup guide,
 * the status bar's count and the opening at start (SETUP1a, D97). Every query is one another reader
 * already makes, so reading it here asks nothing twice.
 *
 * @remarks
 * `settled` is whether every answer the reading needs is in: a guide that opened, or a count that
 * showed, before the roster answered would say *no agent* of a machine that has one. A browser settles
 * on the registry alone, since the rest is never asked there (D47 §4). The sessions only count what
 * waits on the person, for the starters, so the guide does not wait on them.
 */
export function useMachine(): { machine: Machine; settled: boolean } {
  const registry = useRegistry('machine');
  const driver = useDriver();
  const roster = useHarnesses();
  const sessions = useSessions(null, false);
  const lines = useLines();
  const attached = driver.data !== undefined;

  const machine = readMachine({
    attached,
    registry: registry.data,
    driver: driver.data,
    harnesses: Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [],
    sessions: sessions.data,
    lines: Array.isArray(lines.data?.lines) ? lines.data.lines : [],
    landings: Array.isArray(lines.data?.landings) ? lines.data.landings : [],
  });

  // A shell's driver still answering is not a browser: until it has, nothing is known of the machine.
  const settled = registry.isSuccess && !driver.isLoading && (!attached || (roster.isSuccess && lines.isSuccess));
  return { machine, settled };
}
