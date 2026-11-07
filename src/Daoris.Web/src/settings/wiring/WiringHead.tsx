import { useTranslation } from 'react-i18next';
import { Pill } from '../../design/ui';

/**
 * The page's head: a development run says so before any card, with what follows from it. An
 * install, or a home named for one start, has nothing to say here.
 */
export function WiringHead({ kind }: { kind: string | null | undefined }) {
  const { t } = useTranslation();
  if (kind !== 'development') return null;
  return (
    <p className="m-0 border-l-[3px] border-accent bg-page/60 px-3.5 py-2 text-body text-ink-soft">
      <Pill tone="taken">{t('settings.daoris.pill.development')}</Pill>{' '}
      <span>{t('settings.daoris.development')}</span>
    </p>
  );
}
