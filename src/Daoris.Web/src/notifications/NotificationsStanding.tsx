import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  retryNotice, standingOf, useDriver, useRetryQuest, useSetDrivable, useSetHold, useSetTrees,
} from '../bridge/shell';
import { failure, type Notify } from '../design/ui';
import { type StewardActNow, stewardReverse } from '../person/standing';
import { StewardLine } from './StewardLine';

/**
 * The steward's line as *Notifications* holds it (STEWARDROLE1b, D209 §5.3, §5.4): each act's reverse press sent as the window sends
 * the owner's ordinary press today: `SET_DRIVABLE`, `SET_TREES`, `SET_HOLD`, or `RETRY_QUEST` with what it did said as *Try again* says
 * it. Who holds the supervisor and the steward is their roles' (D233 point 2). The view draws it in its slot (`NotificationsView`'s
 * `standing`), so the view presses no hook itself.
 */
export function NotificationsStanding({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const standing = standingOf(useDriver().data?.standingConfirmation);
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
      <StewardLine
        standing={standing}
        refused={stewardRefused}
        onReverse={reverse}
      />
    </>
  );
}
