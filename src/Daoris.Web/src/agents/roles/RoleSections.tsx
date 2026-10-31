import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { cn } from '../../lib/cn';
import { PageHead, ViewMain } from '../../work/frame/ViewMain';
import { ROLE_SECTIONS, type RoleSection } from '../modes';

/** The sections that are built; the others are listed and say they come later (COMPANYVIEW1b). */
export const BUILT_SECTIONS: readonly RoleSection[] = ['working', 'appointed', 'work', 'accounts', 'repository'];

/**
 * **The Roles mode's list** (COMPANYVIEW1b, the company-view design §1): the five sections, each a row of the list
 * (`data-list-row`), the one chosen marked. A section not built yet says so in one quiet line under its name, and one with a start
 * held says that (COMPANYVIEW1d; the bar counts nothing new, D205 point 4).
 *
 * A molecule: the chosen section arrives, and each press goes out.
 */
export function RoleSectionList({ section, onSection, held = {} }: {
  section: RoleSection;
  onSection: (section: RoleSection) => void;
  /** The sections a start is held in, from the driver's read. */
  held?: Partial<Record<RoleSection, boolean>>;
}) {
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
            {held[each] && <span data-held="" className="block truncate text-small text-warn">{t('agents.roles.held')}</span>}
          </button>
        </li>
      ))}
    </ul>
  );
}

/** How the main area shows the mode (COMPANYVIEW1f): the chosen section as its list, or the diagram. */
export type RolesView = 'list' | 'diagram';

/**
 * The chosen section's main area: the built section's page as it arrives, and for one not built its name and the one quiet line that
 * it comes later. Its head offers *Show as diagram*, and back *Show as list*, where the mode hands the toggle in.
 */
export function RoleSectionMain({ section, view, onView, children }: {
  section: RoleSection; view?: RolesView; onView?: (view: RolesView) => void; children?: ReactNode;
}) {
  const { t } = useTranslation();
  const toggle = onView && (
    <Button variant="ghost" onClick={() => onView(view === 'diagram' ? 'list' : 'diagram')}>
      {t(view === 'diagram' ? 'agents.roles.view.list' : 'agents.roles.view.diagram')}
    </Button>
  );
  return (
    <ViewMain header={<PageHead title={t(`nav.roles.${section}`)} acts={toggle || undefined} />}>
      {BUILT_SECTIONS.includes(section) ? children : <p className="m-0 text-body text-ink-soft">{t('agents.roles.laterOf')}</p>}
    </ViewMain>
  );
}
