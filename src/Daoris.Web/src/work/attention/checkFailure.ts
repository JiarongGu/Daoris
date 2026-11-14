import i18n from '../../locales/i18n';

export type CheckFailure = {
  name: string | null;
  tip: string | null;
  tail: string | null;
  state: string;
};

export function checkFailureWorded(check: CheckFailure) {
  const name = check.name ?? i18n.t('work.attention.checkFailed.all');
  const commit = check.tip ?? i18n.t('work.attention.checkFailed.unknown');
  return {
    title: i18n.t(check.state === 'stopped' ? 'work.attention.checkFailed.stopped' : 'work.attention.checkFailed.title', { name }),
    detail: [i18n.t('work.attention.checkFailed.detail', { commit }), check.tail].filter(Boolean).join('\n\n'),
  };
}
