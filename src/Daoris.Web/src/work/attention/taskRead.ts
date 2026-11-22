import i18n from '../../locales/i18n';
import type { StepWaitRow } from './attentionPushed';

/**
 * A run step's row as *What needs you* reads it: named by the run's work quest where its title is in hand, else by the run, and why
 * it waits, one way whoever holds it: on its role, by its holder (`you` for the person), at its node, with the act in its plugin's words.
 */
export const stepHolder = (step: Pick<StepWaitRow, 'holder' | 'holderName'>) =>
  step.holder !== 'agent' ? i18n.t('workflow.wait.holder.person')
    : step.holderName ? i18n.t('workflow.wait.holder.agent', { name: step.holderName }) : i18n.t('workflow.wait.holder.anyAgent');

export const stepRead = (step: StepWaitRow, title?: string) => ({
  title: title ?? `#${step.run}`,
  detail: step.exit
    ? i18n.t(`work.attention.stepExit.${step.exit.kind}`, { node: step.node, role: step.role })
    : i18n.t('work.attention.stepWhy', {
      role: step.role,
      holder: stepHolder(step),
      node: step.node,
      words: step.words ?? '',
    }).trim(),
  step,
});
