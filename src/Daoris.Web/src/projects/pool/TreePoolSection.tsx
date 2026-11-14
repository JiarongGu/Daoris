import { useTranslation } from 'react-i18next';
import { Prose } from '../../design/ui';
import { figure } from '../../locales/format';
import { PageSection } from '../../work/frame/ViewMain';

export type TreePoolSlot = { number: number; state: string; line: string; reason: string };

/** A slot names only the line the desktop says it serves; a restoring slot's wait is worded from its reason code. */
export function TreePoolSection({ slots, failed = false }: { slots?: readonly TreePoolSlot[] | null; failed?: boolean }) {
  const { t } = useTranslation();
  return (
    <PageSection title={t('projects.pool.title')}>
      {failed ? <Prose>{t('projects.pool.failed')}</Prose>
        : !slots ? <Prose>{t('projects.pool.reading')}</Prose>
          : slots.length === 0 ? <Prose>{t('projects.pool.empty')}</Prose>
            : <ul className="m-0 grid list-none gap-3 p-0">
              {slots.map((slot) => (
                <li key={slot.number} aria-label={t('projects.pool.slot', { number: figure(slot.number) })} className="grid min-w-0 gap-1">
                  <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-small">
                    <span className="font-semibold">{t('projects.pool.slot', { number: figure(slot.number) })}</span>
                    <span className="text-ink-faint">{t(`projects.pool.state.${slot.state}`, { defaultValue: t('projects.pool.state.unknown') })}</span>
                    {slot.line && <code className="wrap-anywhere">{slot.line}</code>}
                  </div>
                  {slot.reason === 'tree.kept-restoring' && <Prose className="text-small">{t('projects.pool.restoring')}</Prose>}
                </li>
              ))}
            </ul>}
    </PageSection>
  );
}
