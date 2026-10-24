import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { standingOf, useDriver, useSetAppointment, useSetStandingConfirmation } from '../bridge/shell';
import { failure, type Notify } from '../design/ui';
import { appointmentSaid, standingSaid } from '../person/standing';
import { StandingLine } from './StandingLine';
import { SupervisorLine } from './SupervisorLine';

/**
 * The standing confirmation's line as *Notifications* holds it (TERMTRUST1a, D201 point 7): the driver's state read, and its **Turn
 * off**, which sends no presses and asks nothing (the terminal-trust design §4.3); a refusal said under the line (R3). Beside it the
 * supervisor's appointments, each with its own **Turn off** (SUPERVISOR1a-ii, D208 §5, §6.4), which turns the appointment off at once.
 * The view draws it in its slot (`NotificationsView`'s `standing`), so the view presses no hook itself.
 */
export function NotificationsStanding({ notify, onOpen }: { notify: Notify; onOpen: () => void }) {
  const { t } = useTranslation();
  const standing = standingOf(useDriver().data?.standingConfirmation);
  const setStanding = useSetStandingConfirmation();
  const setAppointment = useSetAppointment();
  const [refused, setRefused] = useState<string | null>(null);
  const [dismissRefused, setDismissRefused] = useState<string | null>(null);
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
        onTurnOff={(repository) => {
          setDismissRefused(null);
          setAppointment.mutate({ role: 'supervisor', repository, on: false }, {
            onSuccess: (answer) => { if (!answer.done) setDismissRefused(appointmentSaid(t, answer.why, answer.message)); },
            onError: failure(notify),
          });
        }}
      />
    </>
  );
}
