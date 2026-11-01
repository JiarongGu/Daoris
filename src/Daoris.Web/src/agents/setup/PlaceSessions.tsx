import { useTranslation } from 'react-i18next';
import { useRegistry } from '../../data/queries';
import { workspaceOf } from '../../data/workspaces';
import { Button, type Notify } from '../../design/ui';
import { RepositorySetup, type RepositorySetupProps, SESSIONS_SECTIONS } from '../../projects/setup/RepositorySetup';
import { useRepositorySetup } from '../../projects/setup/useRepositorySetup';
import { useWorkflowTab } from '../../projects/useWorkflowTab';
import { type PlaceWorkflowProps, PlaceWorkflowView } from '../../workflow/place/PlaceWorkflowView';
import { WorkspaceSetupPart } from '../../projects/workspace/WorkspaceView';
import type { PlaceRow } from '../../data/wiring';

/**
 * **A repository place's Workflow section** (AGENTSPLACE1h; the section's body on the place page, SETUPUX1h): its route and chart, Gates,
 * the line's landing rows from the one molecule Setup draws them with, and Change… and Edit steps…. A molecule: the setup and the workflow
 * arrive as props, and every press goes out.
 */
export function PlaceWorkflowBodyView({ setup, workflow = null }: { setup: RepositorySetupProps | null; workflow?: PlaceWorkflowProps | null }) {
  if (!setup || !workflow) return null;
  return (
    <PlaceWorkflowView
      {...workflow}
      onAcceptIntegration={setup.landed?.onAcceptIntegration}
      landing={(asked) => (setup.work ? <RepositorySetup {...setup} only={['work']} gatesApart open={asked ?? setup.open} /> : null)}
    />
  );
}

/**
 * **A repository place's Sessions section**: the access, the heavy gates and checks, the language, helpers, hand-over and standing answer,
 * and the reach and rules, today's rows moved whole from Repositories' Setup tab, each with *Set for this repository* or *Clear*.
 */
export function PlaceSessionsBodyView({ setup }: { setup: RepositorySetupProps | null }) {
  if (!setup) return null;
  return <RepositorySetup {...setup} only={SESSIONS_SECTIONS} />;
}

/** A repository place's setup, from the registry's row and the driver's answers; read only while the section that draws it is open. */
function useRepositoryPlace({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: PlaceRow;
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  const registry = useRegistry('machine');
  const registration = (registry.data ?? []).find((row) => row.repository === place.repository && workspaceOf(row) === workspaceOf(place));
  const setup = useRepositorySetup({ registration, enabled: true, notify, onOpenPlugin, onOpenAppointed });
  return { registration, setup };
}

type PlaceOf = { kind: 'workspace'; row: PlaceRow } | { kind: 'repository'; row: PlaceRow };

/**
 * **A place's Workflow section**: a repository's, from its Workflow answer; a workspace's is the Repositories page's tab until the
 * workspace's own section is built, so it says so and holds a door there.
 */
export function PlaceWorkflow({ place, notify, onOpenPlugin, onOpenWorkspaceFlow }: {
  place: PlaceOf;
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenWorkspaceFlow?: (workspace: string) => void;
}) {
  const { t } = useTranslation();
  if (place.kind === 'workspace') {
    return (
      <div className="flex flex-wrap items-center gap-2 py-1 text-small text-ink-soft">
        <span>{t('agents.setup.page.workspaceFlow')}</span>
        {onOpenWorkspaceFlow && (
          <Button variant="ghost" onClick={() => onOpenWorkspaceFlow(place.row.workspace ?? '')}>{t('agents.setup.page.workspaceFlowOpen')}</Button>
        )}
      </div>
    );
  }
  return <RepositoryWorkflow place={place.row} notify={notify} onOpenPlugin={onOpenPlugin} />;
}

function RepositoryWorkflow({ place, notify, onOpenPlugin }: { place: PlaceRow; notify: Notify; onOpenPlugin?: (plugin: string) => void }) {
  const { registration, setup } = useRepositoryPlace({ place, notify, onOpenPlugin });
  const workflowOf = useWorkflowTab({ repository: registration?.repository ?? null, enabled: setup !== null, ready: setup !== null, notify });
  const workflow = registration ? workflowOf(registration) : null;
  return <PlaceWorkflowBodyView setup={setup} workflow={workflow && { ...workflow, name: registration!.repository }} />;
}

/**
 * **A place's Sessions section** (AGENTSPLACE1f): how its sessions run. *This machine* holds none.
 *
 * An organism: it holds the bridge's hooks, so the molecules above hold none. It mounts only while its section is open.
 */
export function PlaceSessions({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: PlaceOf;
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  if (place.kind === 'repository') {
    return <RepositorySessions place={place.row} notify={notify} onOpenPlugin={onOpenPlugin} onOpenAppointed={onOpenAppointed} />;
  }
  return <WorkspaceSetupPart workspace={place.row.workspace ?? ''} notify={notify} rows="sessions" onOpenPlugin={onOpenPlugin} onOpenAppointed={onOpenAppointed} />;
}

function RepositorySessions({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: PlaceRow;
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  const { setup } = useRepositoryPlace({ place, notify, onOpenPlugin, onOpenAppointed });
  return <PlaceSessionsBodyView setup={setup} />;
}
