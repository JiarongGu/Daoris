import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { PageHead, ViewMain } from '../../work/frame/ViewMain';
import { ROLE_SECTIONS, type RoleSection } from '../modes';

/** The sections that are built; the others are listed and say they come later (COMPANYVIEW1b). */
export const BUILT_SECTIONS: readonly RoleSection[] = ['working'];

/**
 * **The Roles mode's list** (COMPANYVIEW1b, the company-view design §1): the five sections, each a row of the list
 * (`data-list-row`), the one chosen marked. A section not built yet says so in one quiet line under its name.
 *
 * A molecule: the chosen section arrives, and each press goes out.
 */
export function RoleSectionList({ section, onSection }: { section: RoleSection; onSection: (section: RoleSection) => void }) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 list-none p-0" aria-label={t('nav.roles')}>
      {ROLE_SECTIONS.map((each) => (
        <li key={each} data-list-row="" className="flex items-center">
          <button
            type="button"
            aria-current={section === each || undefined}
            onClick={() => onSection(each)}
            className={cn(
              'block w-full min-w-0 border-l-[3px] px-2.5 py-1.5 text-left transition-colors duration-(--speed)',
              'hover:bg-accent-soft/50',
              section === each ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
            )}
          >
            <span className="block truncate text-body text-ink">{t(`nav.roles.${each}`)}</span>
            {!BUILT_SECTIONS.includes(each) && <span className="block truncate text-small text-ink-faint">{t('agents.roles.later')}</span>}
          </button>
        </li>
      ))}
    </ul>
  );
}

/**
 * The chosen section's main area: the built section's page as it arrives, and for one not built its name and the one quiet line that
 * it comes later.
 */
export function RoleSectionMain({ section, children }: { section: RoleSection; children?: ReactNode }) {
  const { t } = useTranslation();
  return (
    <ViewMain header={<PageHead title={t(`nav.roles.${section}`)} />}>
      {BUILT_SECTIONS.includes(section) ? children : <p className="m-0 text-body text-ink-muted">{t('agents.roles.laterOf')}</p>}
    </ViewMain>
  );
}
