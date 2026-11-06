import i18n from '../../locales/i18n';
import type { HumanTaskWait } from './attentionPushed';

/**
 * A person's task's row as *What needs you* reads it (NODEKIT1 HUMAN): named by the run's work quest where its title is in hand, else by the run,
 * and why it waits: at its node for the person's *Done*, or for their *Approve* or *Reject*.
 */
export const taskRead = (task: HumanTaskWait, title?: string) => ({
  title: title ?? `#${task.run}`,
  detail: i18n.t(task.acts.includes('approve') ? 'work.attention.approvalWhy' : 'work.attention.taskWhy', { node: task.node }),
  task,
});
