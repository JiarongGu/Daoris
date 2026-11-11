import { useTranslation } from 'react-i18next';
import {
  retryNotice, standingOf, useDriver, useResumeWork, useRetryQuest, useSetDrivable, useSetHold, useSetTerminal,
} from '../bridge/shell';
import type { WorkScopeName } from '../work/pausing/pausing';
import { HelpPressLine } from './HelpPressLine';
import { TerminalTierLine } from './TerminalTierLine';
import { type TerminalPress, terminalActsOf } from '../person/terminalActs';
import { terminalTierOf } from '../settings/machine/TerminalTierSetting';
import { failure, type Notify } from '../design/ui';
import { type HelpPressNow, type StewardActNow, helpReverse, stewardReverse } from '../person/standing';
import { StewardLine } from './StewardLine';

/**
 * The steward's line as *Notifications* holds it: each act's reverse press sent as the window sends
 * the owner's ordinary press today: `SET_DRIVABLE`, `SET_HOLD`, or `RETRY_QUEST` with what it did said as *Try again* says
 * it. Who holds the supervisor and the steward is their roles'. The view draws it in its slot (`NotificationsView`'s
 * `standing`), so the view presses no hook itself. Above them, the terminal's tier with its *Back to ask*, sent as Settings' row sends it
 * (`SET_TERMINAL`), and what a terminal pressed under act, each opening what it named.
 */
export function NotificationsStanding({ notify, onOpenHelp, onOpenTarget }: {
  notify: Notify;
  onOpenHelp: () => void;
  /** Opens what a terminal's press named: a quest, an ask, a session or a repository. */
  onOpenTarget?: (scope: string, id: string) => void;
}) {
  const { t } = useTranslation();
  const driver = useDriver().data;
  const standing = standingOf(driver?.standingConfirmation);
  const setTerminal = useSetTerminal();
  const setDrivable = useSetDrivable();
  const setHold = useSetHold();
  const retry = useRetryQuest();
  const resumeWork = useResumeWork();
  /** The owner's own reverse press for a steward's act (§5.4), recorded as theirs by its ordinary route. */
  const reverse = (act: StewardActNow) => {
    const press = stewardReverse(act);
    const repository = act.repository ?? '';
    const onError = failure(notify);
    if (press === 'stopDriving' || press === 'drive') setDrivable.mutate({ repository, drivable: press === 'drive' }, { onError });
    else if (press === 'resume' || press === 'hold') setHold.mutate({ repository, held: press === 'hold' }, { onError });
    else if (press === 'retry' && act.quest) {
      const quest = act.quest;
      retry.mutate({ quest }, { onSuccess: (state) => notify(t(...retryNotice(quest, state))), onError });
    }
  };
  const reverseHelp = (press: HelpPressNow) => {
    if (helpReverse(press) === 'resume' && press.scope && press.target) {
      resumeWork.mutate({ scope: press.scope as WorkScopeName, id: press.target }, { onError: failure(notify) });
    }
  };
  const openPressed = (press: TerminalPress) => {
    if (press.scope && press.target) onOpenTarget?.(press.scope, press.target);
  };
  return (
    <>
      <TerminalTierLine
        tier={terminalTierOf(driver?.terminal)}
        acts={terminalActsOf(driver?.terminalActs)}
        busy={setTerminal.isPending}
        onBackToAsk={() => setTerminal.mutate({ tier: 'ask' }, {
          onSuccess: () => notify(t('settings.terminal.set.ask')),
          onError: failure(notify),
        })}
        onOpen={openPressed}
      />
      <HelpPressLine standing={standing} busy={resumeWork.isPending} onOpen={() => onOpenHelp()} onReverse={reverseHelp} />
      <StewardLine
        standing={standing}
        busy={setDrivable.isPending || setHold.isPending || retry.isPending}
        onReverse={reverse}
      />
    </>
  );
}
