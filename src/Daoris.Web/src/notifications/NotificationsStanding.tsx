import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  retryNotice, standingOf, useDriver, useRetryQuest, useSetAppointment, useSetDrivable, useSetHold, useSetStandingConfirmation, useSetTrees,
} from '../bridge/shell';
import { failure, type Notify } from '../design/ui';
import { appointmentSaid, standingSaid, type StewardActNow, stewardAppointmentSaid, stewardReverse } from '../person/standing';
import { StandingLine } from './StandingLine';
import { StewardLine } from './StewardLine';
import { SupervisorLine } from './SupervisorLine';

/**
 * The standing confirmation's line as *Notifications* holds it (TERMTRUST1a, D201 point 7): the driver's state read, and its **Turn
 * off**, which sends no presses and asks nothing (the terminal-trust design §4.3); a refusal said under the line (R3). Beside it the
 * supervisor's appointments, each with its own **Turn off** (SUPERVISOR1a-ii, D208 §5, §6.4), which turns the appointment off at once,
 * and the steward's line (STEWARDROLE1b, D209 §5.3, §5.4): its **Turn off**, and each act's reverse press sent as the window sends
 * the owner's ordinary press today: `SET_DRIVABLE`, `SET_TREES`, `SET_HOLD`, or `RETRY_QUEST` with what it did said as *Try again* says it.
 * The view draws it in its slot (`NotificationsView`'s `standing`), so the view presses no hook itself.
 */
export function NotificationsStanding({ notify, onOpen }: { notify: Notify; onOpen: () => void }) {
  const { t } = useTranslation();
  const standing = standingOf(useDriver().data?.standingConfirmation);
  const setStanding = useSetStandingConfirmation();
  const setAppointment = useSetAppointment();
  const [refused, setRefused] = useState<string | null>(null);
  const [dismissRefused, setDismissRefused] = useState<string | null>(null);
  const setDrivable = useSetDrivable();
  const setTrees = useSetTrees();
  const setHold = useSetHold();
  const retry = useRetryQuest();
  const [stewardRefused, setStewardRefused] = useState<string | null>(null);
  /** The owner's own reverse press for a steward's act (§5.4), recorded as theirs by its ordinary route. */
  const reverse = (act: StewardActNow) => {
    const press = stewardReverse(act);
    const repository = act.repository ?? '';
    setStewardRefused(null);
    const onError = failure(notify);
    if (press === 'stopDriving' || press === 'drive') setDrivable.mutate({ repository, drivable: press === 'drive' }, { onError });
    else if (press === 'treesOff') setTrees.mutate({ repository, ownTree: false }, { onError });
    else if (press === 'resume' || press === 'hold') setHold.mutate({ repository, held: press === 'hold' }, { onError });
    else if (press === 'retry' && act.quest) {
      const quest = act.quest;
      retry.mutate({ quest }, { onSuccess: (state) => notify(t(...retryNotice(quest, state))), onError });
    }
  };
  return (
    <>
      <StandingLine
        standing={standing}
        busy={setStanding.isPending}
        refused={refused}
        onTurnOff={() => {
          setRefused(null);
          setStanding.mutate([], {
            onSuccess: (answer) => { if (!answer.done) setRefused(standingSaid(t, answer.why, answer.message)); },
            onError: failure(notify),
          });
        }}
        onOpen={onOpen}
      />
      <SupervisorLine
        appointments={standing?.appointments ?? []}
        busy={setAppointment.isPending}
        refused={dismissRefused}
        onTurnOff={(appointment) => {
          setDismissRefused(null);
          // SUPERVISOR1e: a workspace's appointment is turned off by its workspace, a repository's by its repository.
          const scope = appointment.workspace ? { workspace: appointment.workspace } : { repository: appointment.repository };
          setAppointment.mutate({ role: 'supervisor', ...scope, on: false }, {
            onSuccess: (answer) => { if (!answer.done) setDismissRefused(appointmentSaid(t, answer.why, answer.message)); },
            onError: failure(notify),
          });
        }}
      />
      <StewardLine
        standing={standing}
        busy={setAppointment.isPending}
        refused={stewardRefused}
        onTurnOff={() => {
          setStewardRefused(null);
          setAppointment.mutate({ role: 'steward', on: false }, {
            onSuccess: (answer) => { if (!answer.done) setStewardRefused(stewardAppointmentSaid(t, answer.why, answer.message)); },
            onError: failure(notify),
          });
        }}
        onReverse={reverse}
      />
    </>
  );
}
