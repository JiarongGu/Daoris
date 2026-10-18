import { useTranslation } from 'react-i18next';
import type { Quest } from '../data/api';
import { Pill, QUEST_TONE } from '../design/ui';
import { shownStatus } from './records';

/** What a quest's status is read from: its own state, its submission in a closure graph, and the hold on it. */
export type QuestFacts = Pick<Quest, 'status' | 'submission' | 'hold'>;

/**
 * A quest's status where a list or a line draws it (FABRIC-CLOSE1d2): *submitted* for a closure graph's quest whose agent made
 * its work ready, the pill's tone staying the status's (a submission is not a status, §E5), and *source reopened* beside it for
 * a step held so. Keyed on the hold, not the status: a withdrawn step is Open. A quest no closure holds draws as it always did.
 *
 * @remarks
 * One reading for the places that do not draw the quest's page (the chain, the map, an ask's work, the session's relations),
 * as `QuestList` reads it beside the title.
 */
export function QuestStatus({ quest }: { quest: QuestFacts }) {
  const { t } = useTranslation();
  const shown = shownStatus(quest);
  return (
    <>
      <Pill tone={QUEST_TONE[quest.status]} title={t(`statusHint.${shown}`)}>{t(`status.${shown}`)}</Pill>
      {quest.hold === 'source-reopened' && <Pill tone="open" title={t('quests.card.reopenedHint')}>{t('quests.card.reopened')}</Pill>}
    </>
  );
}
