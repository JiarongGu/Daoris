import { useRegistry } from '../../data/queries';
import { workspaceOf } from '../../data/workspaces';
import { type Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { useWorkflowCurrent, useWorkflowEditor } from '../../bridge/shell';
import { FoldRow } from '../../projects/setup/FoldRow';
import { FOLD_SECTIONS, RepositorySetup, type RepositorySetupProps, SESSIONS_SECTIONS } from '../../projects/setup/RepositorySetup';
import { useRepositorySetup } from '../../projects/setup/useRepositorySetup';
import { useWorkflowTab } from '../../projects/useWorkflowTab';
import { type PlaceWorkflowProps, PlaceWorkflowView } from '../../workflow/place/PlaceWorkflowView';
import { WorkspaceSetupPart } from '../../projects/workspace/WorkspaceView';
import type { PlaceHold } from '../AgentsView';
import { repositoryId } from '../modes';

/**
 * **A repository place's Workflow section** (AGENTSPLACE1h; the section's body on the place page, SETUPUX1h, SETUPUX1o): its routes and the
 * chosen route's chart, Gates, the folds of the line and its landing from the one molecule Setup draws them with, and Change… and Edit steps….
 * A molecule: the setup and the workflow arrive as props, and every press goes out.
 */
export function PlaceWorkflowBodyView({ setup, workflow = null, landingAsked = false }: {
  setup: RepositorySetupProps | null;
  workflow?: PlaceWorkflowProps | null;
  /** A door asked for the landing rows (a refusal of *Set up…* whose cause is put right there): they open, and take the focus. */
  landingAsked?: boolean;
}) {
  if (!setup || !workflow) return null;
  return (
    <PlaceWorkflowView
      {...workflow}
      onAcceptIntegration={setup.landed?.onAcceptIntegration}
      landing={(hand) => (setup.work
        ? (
          <RepositorySetup
            {...setup}
            only={FOLD_SECTIONS}
            lead={hand.lead}
            gatesApart
            open={hand.asked ?? (landingAsked ? 'landing' : undefined)}
            openAgain={hand.again}
          />
        )
        : <FoldRow folds={hand.lead} />)}
    />
  );
}

/**
 * **A repository place's Sessions section**: the access, the language, helpers, hand-over and standing answer, and the reach and rules,
 * today's rows moved whole from Repositories' Setup tab, each with *Set for this repository* or *Clear*. The heavy gates went to Workflow's *More*.
 */
export function PlaceSessionsBodyView({ setup }: { setup: RepositorySetupProps | null }) {
  if (!setup) return null;
  return <RepositorySetup {...setup} only={SESSIONS_SECTIONS} />;
}

/** A repository place's setup, from the registry's row and the driver's answers; read only while the section that draws it is open. */
function useRepositoryPlace({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: PlaceHold['row'];
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  const registry = useRegistry('machine');
  const registration = (registry.data ?? []).find((row) => row.repository === place.repository && workspaceOf(row) === workspaceOf(place));
  const setup = useRepositorySetup({ registration, enabled: true, notify, onOpenPlugin, onOpenAppointed });
  return { registration, setup };
}

/**
 * **A place's Workflow section**: a repository's or a workspace's, from its Workflow answer for the route the person chose, read only
 * while the section is open. The route and the step are the shell's, held above the mounts.
 */
export function PlaceWorkflow({ place, notify, onOpenPlugin, landingAsked = false }: {
  place: PlaceHold;
  notify: Notify;
  /** A door asked for the landing rows (a refusal of *Set up…* whose cause is put right there): the Landing fold opens and takes the focus. */
  landingAsked?: boolean;
  onOpenPlugin?: (plugin: string) => void;
}) {
  if (place.kind === 'workspace') return <WorkspaceWorkflow place={place} notify={notify} onOpenPlugin={onOpenPlugin} />;
  return <RepositoryWorkflow place={place} notify={notify} onOpenPlugin={onOpenPlugin} landingAsked={landingAsked} />;
}

function RepositoryWorkflow({ place, notify, onOpenPlugin, landingAsked }: {
  place: PlaceHold; notify: Notify; onOpenPlugin?: (plugin: string) => void; landingAsked: boolean;
}) {
  const { registration, setup } = useRepositoryPlace({ place: place.row, notify, onOpenPlugin });
  const workflowOf = useWorkflowTab({
    repository: registration?.repository ?? null, enabled: setup !== null, ready: setup !== null, notify, route: place.view.route,
  });
  const workflow = registration ? workflowOf(registration) : null;
  // The route chosen before it, kept drawn while the next is read, is this repository's alone.
  const current = workflow?.current?.repository === registration?.repository ? workflow?.current : undefined;
  return (
    <PlaceWorkflowBodyView
      setup={setup}
      workflow={workflow && {
        ...workflow,
        current,
        name: registration!.repository,
        view: place.view,
        onView: place.onView,
        onOpenSection: place.onOpenSection,
      }}
      landingAsked={landingAsked}
    />
  );
}

/**
 * A workspace's own Workflow: its routes and chart, and the repositories that follow it or set their own, then the line's defaults it
 * gives each repository that sets none (SETUPUX1v, moved from the Setup tab): its line, landing, commits, review and second opinion.
 */
function WorkspaceWorkflow({ place, notify, onOpenPlugin }: { place: PlaceHold; notify: Notify; onOpenPlugin?: (plugin: string) => void }) {
  const workspace = place.row.workspace ?? '';
  const answer = useWorkflowCurrent({ workspace }, { route: place.view.route });
  const editor = useWorkflowEditor({ workspace });
  // The route chosen before it, kept drawn while the next is read, is this workspace's alone.
  const current = answer.data?.workspace === workspace ? answer.data : null;
  return (
    <>
      <PlaceWorkflowView
        page="workspace"
        name={workspace}
        current={current}
        reading={answer.isPending}
        refusal={answer.error ? sentence(answer.error) : null}
        doors={{ repository: (repository) => place.onGo(repositoryId(workspace, repository), { section: 'workflow' }) }}
        editor={editor}
        view={place.view}
        onView={place.onView}
      />
      <div className="mt-3">
        <WorkspaceSetupPart
          workspace={workspace}
          notify={notify}
          rows="defaults"
          open={place.asked === 'defaults' ? 'defaults' : undefined}
          onOpenPlugin={onOpenPlugin}
        />
      </div>
    </>
  );
}

/**
 * **A place's Sessions section** (AGENTSPLACE1f): how its sessions run. *This machine* holds none.
 *
 * An organism: it holds the bridge's hooks, so the molecules above hold none. It mounts only while its section is open.
 */
export function PlaceSessions({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: Pick<PlaceHold, 'kind' | 'row' | 'asked'>;
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  if (place.kind === 'repository') {
    return <RepositorySessions place={place.row} notify={notify} onOpenPlugin={onOpenPlugin} onOpenAppointed={onOpenAppointed} />;
  }
  // A workspace's Sessions hold its access and session rows and, since SETUPUX1v, Reach: its rules and the machines its remote counts undeclared.
  return (
    <WorkspaceSetupPart
      workspace={place.row.workspace ?? ''}
      notify={notify}
      rows="sessions"
      open={place.asked === 'access' || place.asked === 'reach' ? place.asked : undefined}
      onOpenPlugin={onOpenPlugin}
      onOpenAppointed={onOpenAppointed}
    />
  );
}

function RepositorySessions({ place, notify, onOpenPlugin, onOpenAppointed }: {
  place: PlaceHold['row'];
  notify: Notify;
  onOpenPlugin?: (plugin: string) => void;
  onOpenAppointed?: () => void;
}) {
  const { setup } = useRepositoryPlace({ place, notify, onOpenPlugin, onOpenAppointed });
  return <PlaceSessionsBodyView setup={setup} />;
}
