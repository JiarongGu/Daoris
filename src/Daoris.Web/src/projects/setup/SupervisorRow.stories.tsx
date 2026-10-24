import type { Meta, StoryObj } from '@storybook/react-vite';
import { storyWidths } from '../../design/storyWidths';
import type { AppointmentNow } from '../../person/standing';
import { SupervisorRow, type SupervisorSetup } from './SupervisorRow';

// The supervisor's row in a repository's Setup → Driving, or a workspace's Setup (SUPERVISOR1a-ii, SUPERVISOR1e, D208; the supervisor
// design §5, §6.4): its states, the overrule window, and the ask *Appoint…* and *Appoint again…* open, at the main area's widths. The key
// is never drawn: the row holds none.

const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();
const live: AppointmentNow = {
  role: 'supervisor', repository: 'engine', holder: 'terminal', since: hoursAgo(2), dormant: false, answers: 3, scope: 'repository:engine', holderId: 'h1',
  acts: [], setup: false,
  workspace: null, window: null,
};
const games: AppointmentNow = { ...live, repository: '', scope: 'workspace:games', workspace: 'games', window: 20 };

/** The row for a repository, or for a workspace where `workspace` is named (SUPERVISOR1e). */
function Row({ repository = 'engine', workspace, repositories = [], ...rest }: {
  repository?: string; workspace?: string; repositories?: string[]; supervisor: SupervisorSetup; open?: boolean;
}) {
  return workspace !== undefined
    ? <SupervisorRow workspace={workspace} repositories={repositories} {...rest} />
    : <SupervisorRow repository={repository} {...rest} />;
}

const meta = {
  title: 'Projects/SupervisorRow',
  component: Row,
  args: {
    repository: 'engine',
    supervisor: { offered: true, appointment: null, onAppoint: (answered) => answered.done(), onTurnOff: () => {} },
  },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof Row>;
export default meta;

type Story = StoryObj<typeof meta>;

/** This build: not offered yet, and nothing appoints. */
export const NotOffered: Story = {
  args: { supervisor: { offered: false, appointment: null, onAppoint: () => {}, onTurnOff: () => {} } },
};

/** Offered and off: *Appoint…*. */
export const Off: Story = {};

/** The ask open, naming its scope, with its plain sentences; at the 400 px floor. */
export const Asking: Story = { args: { open: true }, decorators: [storyWidths.narrow] };

/**
 * The ask open while a role's terminal lives (STEWARDROLE1a-i, D209 §2.2): add the role to it or give it a terminal of its own, what
 * sharing means said, neither preselected and *Appoint* waiting for the choice; at the 400 px floor.
 */
export const AskingWithATerminalOpen: Story = {
  args: {
    open: true,
    supervisor: {
      offered: true, appointment: null, holders: [{ id: 'h1', since: hoursAgo(1), roles: ['steward'] }],
      onAppoint: (answered) => answered.done(), onTurnOff: () => {},
    },
  },
  decorators: [storyWidths.narrow],
};

/** Appointed: since when, held by the terminal Daoris started, its answers this run; *Appoint again…* and *Turn off*. */
export const Appointed: Story = {
  args: { supervisor: { offered: true, appointment: live, started: true, onAppoint: () => {}, onTurnOff: () => {} } },
};

/** Found as Daoris started: dormant until *Appoint again*. */
export const Dormant: Story = {
  args: { supervisor: { offered: true, appointment: { ...live, dormant: true, answers: 0 }, onAppoint: () => {}, onTurnOff: () => {} } },
};

/** *Turn off* refused, said under the row. */
export const Refused: Story = {
  args: {
    supervisor: {
      offered: true, appointment: live, refused: 'Its record could not be written, so the next start may read it as dormant.',
      onAppoint: () => {}, onTurnOff: () => {},
    },
  },
};

/** The ask's overrule window set outside 1 to 60: said, and *Appoint* held (SUPERVISOR1e); at the 400 px floor. */
export const AskingWithAWindowSet: Story = {
  args: { open: true, supervisor: { offered: true, appointment: { ...live, window: 45 }, onAppoint: (answered) => answered.done(), onTurnOff: () => {} } },
  decorators: [storyWidths.narrow],
};

/** A workspace's appointment asked (SUPERVISOR1e): every repository of it here, those added later, a repository's own winning. */
export const WorkspaceAsking: Story = {
  args: { workspace: 'games', repositories: ['engine', 'tools', 'site'], open: true },
  decorators: [storyWidths.narrow],
};

/** A workspace appointed, with the window the owner set. */
export const WorkspaceAppointed: Story = {
  args: { workspace: 'games', repositories: ['engine', 'tools'], supervisor: { offered: true, appointment: games, onAppoint: () => {}, onTurnOff: () => {} } },
};

/** A repository its workspace's appointment covers: said, and an appointment here wins over it. */
export const CoveredByItsWorkspace: Story = {
  args: { supervisor: { offered: true, appointment: null, covering: games, onAppoint: () => {}, onTurnOff: () => {} } },
};
