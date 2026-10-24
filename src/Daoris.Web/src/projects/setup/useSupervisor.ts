import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { standingOf, useDriver, useSetAppointment } from '../../bridge/shell';
import { appointmentSaid } from '../../person/standing';
import type { SupervisorSetup } from './SupervisorRow';

/** The scope one row of the supervisor appoints for: a repository with the workspace the registry places it in, or a workspace. */
export type SupervisorTarget = { repository: string; workspace: string } | { workspace: string; repository?: undefined };

const same = (left: string | null | undefined, right: string) => (left ?? '').toLowerCase() === right.toLowerCase();

/**
 * The supervisor's appointment for one scope (SUPERVISOR1a-ii, SUPERVISOR1e, D208 §5): the record's, live or dormant, a repository's
 * workspace's that covers it while it has none of its own, and its presses. *Appoint* in the ask is the owner's own press, with the
 * overrule window set there: the window starts the holder's terminal, and the answer, which never carries a key, says how it ended in
 * the ask itself; *Turn off* asks nothing, a refusal said under the row. Absent where the shell answers no standing.
 */
export function useSupervisor(onError: (error: unknown) => void): (target: SupervisorTarget) => SupervisorSetup | undefined {
  const { t } = useTranslation();
  const driver = useDriver();
  const setAppointment = useSetAppointment();
  // What the last press here came to, by the scope it was made for.
  const [appointed, setAppointed] = useState<{ scope: string; started: boolean; refused: string | null } | null>(null);

  return (target) => {
    const standing = standingOf(driver.data?.standingConfirmation);
    if (!standing) return undefined;
    const scope = target.repository !== undefined ? `repository:${target.repository}` : `workspace:${target.workspace}`;
    const mine = appointed && same(appointed.scope, scope) ? appointed : null;
    const supervisors = standing.appointments.filter((each) => each.role === 'supervisor');
    const workspaceOwn = supervisors.find((each) => same(each.scope, `workspace:${target.workspace}`)) ?? null;
    const sent = { role: 'supervisor', ...(target.repository !== undefined ? { repository: target.repository } : { workspace: target.workspace }) };
    return {
      // STEWARDROLE1a-i (D209 §2.4): the supervisor's own flag, never the steward's nor D201's.
      offered: standing.supervisorOffered,
      appointment: target.repository !== undefined
        ? supervisors.find((each) => same(each.scope, scope)) ?? null
        : workspaceOwn,
      // SUPERVISOR1e (§5): a repository's workspace's appointment answers there while it has none of its own.
      covering: target.repository !== undefined ? workspaceOwn : null,
      holders: standing.holders,
      started: mine?.started,
      refused: mine?.refused ?? null,
      busy: setAppointment.isPending,
      onAppoint: (answered, share, window) => {
        setAppointed(null);
        setAppointment.mutate({ ...sent, on: true, share, window }, {
          onSuccess: (answer) => {
            if (answer.done) {
              setAppointed({ scope, started: true, refused: null });
              answered.done();
            } else {
              answered.refused(appointmentSaid(t, answer.why, answer.message));
            }
          },
          onError: (error) => answered.refused(error instanceof Error ? error.message : String(error)),
        });
      },
      onTurnOff: () => {
        setAppointed(null);
        setAppointment.mutate({ ...sent, on: false }, {
          onSuccess: (answer) => {
            if (!answer.done) setAppointed({ scope, started: false, refused: appointmentSaid(t, answer.why, answer.message) });
          },
          onError,
        });
      },
    };
  };
}
