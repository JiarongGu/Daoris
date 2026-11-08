import type * as React from 'react';
import { type OpenPart, workspaceItem } from '../opener';
import { PlaceDrivingScope } from '../../agents/setup/PlaceDriving';
import { PlaceSessions, PlaceWorkflow } from '../../agents/setup/PlaceSessions';
import { PlaceSetUpPanel } from '../../agents/setup/PlaceSetUpPanel';
import { placeIdOf } from '../../agents/setup/setupTreeModel';
import { useSay } from '../../bridge/sessions/conversation';
import { useDriver } from '../../bridge/shell';
import { type View } from '../../menus/commands';
import { usePluginsView } from '../../plugins/PluginsView';
import { useAgentsView } from '../../agents/AgentsView';
import { type AgentPart } from '../../agents/agents';
import { type ListPanes } from '../../work/frame/listPanes';
import { type Notify } from '../../design/atoms/Toasts';
import { type PlaceAt } from '../../agents/setup/placeShell';
import { type PlaceAsk } from '../../agents/setup/placePlan';

export function useSideViews({ view, lists, notify, attached, askSetup, agentsModes, agentPart, setAgentPart, open }: {
  view: View;
  lists: ListPanes;
  notify: Notify;
  attached: boolean;
  askSetup: (message: string) => void;
  agentsModes: { mode: "agents" | "setup"; onMode: (next: "agents" | "setup") => void; place: string | null; onPlace: (next: string) => void; onPlaceAt: (next: string, at: PlaceAt) => void; arrival: { place: string; at: PlaceAt; } | null; onArrived: () => void; seeded: { place: string; ask: PlaceAsk; } | null; onSeed: (target: string, ask: PlaceAsk) => void; onSeeded: () => void; };
  agentPart: AgentPart | null;
  setAgentPart: React.Dispatch<React.SetStateAction<AgentPart | null>>;
  open: (target: View, item?: string | null | undefined, part?: OpenPart | undefined) => void;
}) {
  // The Plugins view: held on every view, so what its pages hold — a trial's report, an update's
  // plan — stays while Daoris is open; it asks the driver for nothing until it is in front.
  const driver = useDriver();
  const plugins = usePluginsView({
    active: view === 'plugins',
    chosen: lists.pane('plugins').chosen,
    onChoose: (item) => lists.choose('plugins', item),
    notify,
    onAsk: attached && driver.data?.helperAgent ? askSetup : undefined,
  });

  // The Agents place: held on every view, as Plugins is; it reads the roster the frame holds and the
  // accounts' files, and opening it asks no account. A sign-in started there outlives leaving it.
  const agentsPane = lists.pane('agents');
  // The words to the session that proposed a rule an *Accept* on the page kept: the Agents place is handed the
  // door, its own view pressing no say hook.
  const sayToSession = useSay();
  const agents = useAgentsView({
    say: (session, text) => sayToSession.mutateAsync({ id: session, text }),
    active: view === 'agents',
    // The Roles mode is this machine's, as AI features' machine half is: a browser has the agents alone.
    modes: attached ? agentsModes : undefined,
    chosen: agentsPane.chosen,
    onChoose: (item) => lists.choose('agents', item),
    notify,
    part: agentPart,
    onAnchored: () => setAgentPart(null),
    onOpenPlugin: (plugin) => open('plugins', plugin),
    // A place's Workflow and Sessions: their rows press the driver's hooks, which the application hands in so
    // the place imports none. Each is an element, mounted only while its section is open.
    bodies: (place) => ({
      workflow: (
        <PlaceWorkflow
          place={place}
          landingAsked={place.asked === 'landing'}
          notify={notify}
        />
      ),
      sessions: (
        <PlaceSessions
          place={place}
          notify={notify}
          onOpenPlugin={(plugin) => open('plugins', plugin)}
          onOpenGit={() => open('settings', 'git')}
        />
      ),
    }),
    // A repository's driving, for its page head: the hold, and the ⋯'s drive and trees.
    driving: (row, render) => (
      <PlaceDrivingScope
        row={row}
        notify={notify}
        onOpenAppointed={() => agentsModes.onPlaceAt(placeIdOf(row), { section: 'roles', row: 'appointed' })}
        onOpenRepositories={() => open('projects', row.repository)}
      >
        {render}
      </PlaceDrivingScope>
    ),
    // A place's *Set up…*: it reads the plan and presses Apply, which the application hands in for the same reason.
    setUp: (place, page, doorFor) => <PlaceSetUpPanel place={place} page={page} notify={notify} doorFor={doorFor} onOpenQuest={(quest) => open('quests', quest)} />,
    // The Roles mode's doors: each thing's home, the Agents mode at an agent's accounts among them.
    roleDoors: {
      agent: (agent) => open('agents', agent, { agentPart: 'accounts', agentsMode: 'agents' }),
      repository: (repository, door) => open('agents', null, { placeOf: repository, ...(door === 'workflow' ? { placeSection: 'workflow' as const } : {}) }),
      workspace: (workspace) => open('projects', workspaceItem(workspace), { workspaceSection: 'defaults' }),
      plugin: (plugin) => open('plugins', plugin),
    },
  });
  return { plugins, agents };
}
