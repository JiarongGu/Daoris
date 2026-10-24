import type { Meta, StoryObj } from '@storybook/react-vite';
import { storyWidths } from '../../design/storyWidths';
import type { AppointmentNow } from '../../person/standing';
import { SupervisorRow } from './SupervisorRow';

// The supervisor's row in a repository's Setup → Driving (SUPERVISOR1a-ii, D208; the supervisor design §5, §6.4): its states, and the
// ask *Appoint…* and *Appoint again…* open, at the main area's widths. The key is never drawn: the row holds none.

const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();
const live: AppointmentNow = {
  role: 'supervisor', repository: 'engine', holder: 'terminal', since: hoursAgo(2), dormant: false, answers: 3, scope: 'repository:engine', holderId: 'h1',
  acts: [], setup: false, workspace: '', window: 5,
};

const meta = {
  title: 'Projects/SupervisorRow',
  component: SupervisorRow,
  args: {
    repository: 'engine',
    supervisor: { offered: true, appointment: null, onAppoint: (answered) => answered.done(), onTurnOff: () => {} },
  },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof SupervisorRow>;
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

/**
 * A workspace's ask (SUPERVISOR1e, D208 §5): every repository of it, a later one included, a repository's own winning; the overrule
 * window set in the ask, 1 to 60 minutes; at the 400 px floor.
 */
export const WorkspaceAsking: Story = {
  args: { repository: undefined, workspace: 'games', open: true },
  decorators: [storyWidths.narrow],
};

/** A repository with none of its own, covered by its workspace's appointment, which its own would win over (SUPERVISOR1e). */
export const CoveredByItsWorkspace: Story = {
  args: {
    supervisor: {
      offered: true, appointment: null,
      covering: { ...live, repository: '', scope: 'workspace:games', workspace: 'games', window: 15 },
      onAppoint: () => {}, onTurnOff: () => {},
    },
  },
};

/** Appointed with a window of its own: how long its answers are held for the overrule. */
export const AppointedWithItsWindow: Story = {
  args: { supervisor: { offered: true, appointment: { ...live, window: 20 }, onAppoint: () => {}, onTurnOff: () => {} } },
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
