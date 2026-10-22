import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { standingOf, useDriver, useSetStandingConfirmation } from '../bridge/shell';
import { failure, type Notify } from '../design/ui';
import { standingSaid } from '../person/standing';
import { StandingLine } from './StandingLine';

/**
 * The standing confirmation's line as *Notifications* holds it (TERMTRUST1a, D201 point 7): the driver's state read, and its **Turn
 * off**, which sends no presses and asks nothing (the terminal-trust design §4.3); a refusal said under the line (R3). The view draws
 * it in its slot (`NotificationsView`'s `standing`), so the view presses no hook itself.
 */
export function NotificationsStanding({ notify, onOpen }: { notify: Notify; onOpen: () => void }) {
  const { t } = useTranslation();
  const standing = standingOf(useDriver().data?.standingConfirmation);
  const setStanding = useSetStandingConfirmation();
  const [refused, setRefused] = useState<string | null>(null);
  return (
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
  );
}
