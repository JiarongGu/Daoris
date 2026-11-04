import type { TFunction } from 'i18next';
import { doorLabel, toolOf } from '../../agents/tools';
import { agentOf, machineScope } from '../../settings/accounts/accounts';
import type { useAttendedSession } from '../head/useAttendedSession';
import type { HandOverRoster } from './HandOverAsk';

type Session = ReturnType<typeof useAttendedSession>;
export type Roster = NonNullable<Session['harnesses']['data']>['harnesses'];

/**
 * The agents a conversation may be started or handed over on, with their accounts: *New conversation*'s chips and the hand-over's
 * ask (MIXAGENT1b-i) choose from the one roster.
 */
export function agentRosterOf(
  t: TFunction,
  roster: Roster,
  accounts: Session['accounts']['data'],
): HandOverRoster {
  return {
    harnesses: roster.filter((row) => row.present).map((row) => row.harness),
    labels: Object.fromEntries(roster.map((row) => [row.harness, doorLabel(t, row)])),
    names: Object.fromEntries(roster.map((row) => [row.harness, row.product || row.harness])),
    accounts: Object.fromEntries(roster.map((row) => [row.harness, Array.isArray(row.profiles) ? row.profiles : []])),
    // The set's order of the agent's accounts of Daoris's, every workspace's (POOL1c, D197), by the accounts' owner (AGT7),
    // from the files the frame already reads.
    orderOf: (harness) => {
      const door = roster.find((row) => row.harness === harness);
      const use = door ? agentOf(accounts, toolOf(door)) : null;
      return use ? machineScope(use).list : null;
    },
  };
}
