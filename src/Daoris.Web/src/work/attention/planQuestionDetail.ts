import i18n from '../../locales/i18n';
import type { PushedAttention } from './attentionPushed';

/**
 * What a delivery plan's question says beside its text on *What needs you* and the bell: what yes does, what waits on it and what happens
 * if nobody answers, each as the lead wrote it, so the person reads what an answer does before opening the ask where they give it.
 */
export function planQuestionDetail(question: NonNullable<PushedAttention['question']>): string | null {
  const lines = [
    question.yes && i18n.t('asks.plan.question.yes', { text: question.yes }),
    question.blocks && i18n.t('asks.plan.question.blocks', { text: question.blocks }),
    question.default && i18n.t('asks.plan.question.default', { text: question.default }),
  ].filter((line) => line !== '');
  return lines.length > 0 ? lines.join(' · ') : null;
}
