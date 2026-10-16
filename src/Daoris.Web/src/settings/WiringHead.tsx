import { useTranslation } from 'react-i18next';
import { Pill } from '../design/ui';

/**
 * The page's head (the this-Daoris design §7): a development run says so before any card, with what follows from it. An
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

/**
 * A shell older than the page (§7): it answers the route `NO_ROUTE`, so there is nothing to draw but this line. Driver's
 * home row is what the shell can show, and the page says where it is.
 */
export function WiringOlder() {
  const { t } = useTranslation();
  return (
    <p className="m-0 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">{t('settings.daoris.older')}</p>
  );
}
