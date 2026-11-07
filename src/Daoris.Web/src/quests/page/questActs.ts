import { type Quest } from '../../data/api';
import { type IconName } from '../../design/ui';

/** The acts a quest's page offers: its header's, then its body's. */
export type QuestAct =
  | 'take' | 'done' | 'pause' | 'decline' | 'abandon' | 'delete' | 'clear' | 'clearFailed' | 'accept' | 'resume' | 'retry'
  | 'trust' | 'session' | 'reopen' | 'goOn';

/** Each act's name, the one its button says, and its glyph in a menu. */
export const QUEST_ACT: Record<QuestAct, { label: string; icon?: IconName }> = {
  take: { label: 'quests.detail.take' },
  done: { label: 'quests.detail.done', icon: 'check' },
  pause: { label: 'quests.detail.pause', icon: 'pause' },
  decline: { label: 'quests.detail.decline' },
  abandon: { label: 'quests.detail.abandon' },
  delete: { label: 'quests.detail.delete', icon: 'remove' },
  clear: { label: 'quests.detail.clear' },
  clearFailed: { label: 'quests.detail.clearFailed' },
  accept: { label: 'quests.requirements.accept', icon: 'check' },
  resume: { label: 'quests.detail.resume', icon: 'resume' },
  retry: { label: 'quests.detail.retry', icon: 'refresh' },
  trust: { label: 'trust.open', icon: 'shield' },
  session: { label: 'work.open', icon: 'frameWork' },
  reopen: { label: 'quests.detail.reopen' },
  goOn: { label: 'quests.reopened.goOn', icon: 'resume' },
};

/**
 * A header act's button: the one loud control is the quest's next step, taking it while it is open and closing
 * it once it is taken; Pause… plain; the rest quiet.
 */
export function buttonOf(act: QuestAct, status: Quest['status']): 'primary' | 'default' | 'ghost' {
  if (act === 'take') return 'primary';
  if (act === 'done') return status === 'Taken' ? 'primary' : 'default';
  if (act === 'pause') return 'default';
  return 'ghost';
}
