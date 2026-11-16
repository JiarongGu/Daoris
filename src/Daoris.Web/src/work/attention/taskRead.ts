import i18n from '../../locales/i18n';
import type { ActorWait, HumanTaskWait } from './attentionPushed';

/** A step waiting on an actor the person holds, read as a task is: why it waits is that actor and the act in its plugin's words. */
export const actorRead = (actor: ActorWait, title?: string) => ({
  title: title ?? `#${actor.run}`,
  detail: i18n.t('work.attention.actorWhy', { actor: actor.actor, node: actor.node, words: actor.words ?? '' }).trim(),
  actor,
});

/**
 * A person's task's row as *What needs you* reads it: named by the run's work quest where its title is in hand, else by the run,
 * and why it waits: at its node for the person's *Done*, or for their *Approve* or *Reject*.
 */
export const taskRead = (task: HumanTaskWait, title?: string) => ({
  title: title ?? `#${task.run}`,
  detail: i18n.t(task.acts.includes('approve') ? 'work.attention.approvalWhy' : 'work.attention.taskWhy', { node: task.node }),
  task,
});
