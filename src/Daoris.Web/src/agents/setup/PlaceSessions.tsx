import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useRegistry } from '../../data/queries';
import { workspaceOf } from '../../data/workspaces';
import type { Notify } from '../../design/ui';
import { RepositorySetup, type RepositorySetupProps, SESSIONS_SECTIONS } from '../../projects/setup/RepositorySetup';
import { useRepositorySetup } from '../../projects/setup/useRepositorySetup';
import { WorkspaceSetupPart } from '../../projects/workspace/WorkspaceView';
import type { PlaceRow } from '../../data/wiring';

/** A titled section of a place's page, as Roles' is: its heading, then what it holds. */
function PageSection({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <section aria-labelledby={id} className="mt-5 grid gap-3">
      <h2 id={id} className="m-0 text-body font-medium text-ink">{title}</h2>
      {children}
    </section>
  );
}

/**
 * **A repository place's Driving, Line and landing, and Sessions** (AGENTSPLACE1f), drawn from the one molecule the Repositories page's
 * Setup tab drew, so each row, its *Set for this repository* and *Clear*, and its terminal twin are today's. A molecule: the setup arrives
 * as props, and every press goes out.
 *
 * @remarks
 * - **Sessions** holds the access, the heavy gates and checks, the language, helpers, hand-over and standing answer, and the reach and rules.
 * - **Driving** (drive, hold, a tree per session, the supervisor's door) and **Line and landing** (its line, landing, commits, review, opinion,
 *   the landed route and the integration declaration) are drawn here too, since the tab they were on goes with this row (design §6). They are
 *   the page head's and Workflow's (AGENTSPLACE1h), and move there when that row builds them.
 */
export function PlaceRepositorySetupView({ setup }: { setup: RepositorySetupProps | null }) {
  const { t } = useTranslation();
  if (!setup) return null;
  return (
    <>
      <RepositorySetup {...setup} only={['driving']} />
      {setup.work && (
        <PageSection id="place-workflow" title={t('agents.setup.workflow.title')}>
          <RepositorySetup {...setup} only={['work']} />
        </PageSection>
      )}
      <PageSection id="place-sessions" title={t('agents.setup.sessions.title')}>
        <RepositorySetup {...setup} only={SESSIONS_SECTIONS} />
      </PageSection>
    </>
  );
}

/** A repository place's Setup, from the registry's row and the driver's answers: the organism for the view above. */
function PlaceRepositorySetup({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: PlaceRow;
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  const registry = useRegistry('machine');
  const registration = (registry.data ?? []).find((row) => row.repository === place.repository && workspaceOf(row) === workspaceOf(place));
  const setup = useRepositorySetup({ registration, enabled: true, notify, onOpenPlugin, onOpenAppointed });
  return <PlaceRepositorySetupView setup={setup} />;
}

/**
 * **A place's Sessions** (AGENTSPLACE1f, the Agents-place design §3): how its sessions run, today's rows moved whole from Repositories'
 * Setup tab and a workspace's Setup, each with *Set for this repository* or *Clear*. A repository's page also holds its Driving and Line and
 * landing until their own rows build them; *This machine* and a repository not driven hold none.
 *
 * An organism: it holds the bridge's hooks, so the molecules below hold none.
 */
export function PlaceSessions({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: { kind: 'workspace'; row: PlaceRow } | { kind: 'repository'; row: PlaceRow };
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  const { t } = useTranslation();
  if (place.kind === 'repository') {
    return <PlaceRepositorySetup place={place.row} notify={notify} onOpenPlugin={onOpenPlugin} onOpenAppointed={onOpenAppointed} />;
  }
  return (
    <PageSection id="place-sessions" title={t('agents.setup.sessions.title')}>
      <WorkspaceSetupPart workspace={place.row.workspace ?? ''} notify={notify} rows="sessions" onOpenPlugin={onOpenPlugin} onOpenAppointed={onOpenAppointed} />
    </PageSection>
  );
}
