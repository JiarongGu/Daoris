import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { PageHead, ViewMain } from '../../work/frame/ViewMain';
import type { RoleSection } from '../modes';

/** The sections that are built; the others are listed and say they come later (COMPANYVIEW1b). */
export const BUILT_SECTIONS: readonly RoleSection[] = ['working', 'appointed', 'work', 'accounts', 'repository'];

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
