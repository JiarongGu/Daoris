import i18n from '../../locales/i18n';
import type { PushedAttention } from './attentionPushed';

/** A design pass's closed question as a row says it: what yes and no change, as its comparison wrote them. */
export function designRead(row: PushedAttention): { title: string; detail: string | null } {
  const design = row.design;
  return {
    title: design?.question ?? row.id,
    detail: design
      ? [i18n.t('work.attention.designYes', { text: design.yes }), i18n.t('work.attention.designNo', { text: design.no }),
        i18n.t('work.attention.designWhere', { where: design.where })].join(' · ')
      : null,
  };
}
