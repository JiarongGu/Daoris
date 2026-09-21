import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Session } from '../api';
import { RepositoryGroup } from './RepositoryGroup';
import { SessionRow } from './SessionRow';

// The five states the group header carries, each on the shipped component. The rows beneath are
// real `SessionRow`s rather than placeholders, because how the two read TOGETHER is the thing a
// reviewer is actually judging.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: at(134),
  updated: at(4),
};

const rows = (
  <>
    <SessionRow session={SESSION} />
    <SessionRow
      session={{ ...SESSION, id: 'b2c3d4e5', kind: 'chat', quest: null, state: 'awaiting-person', created: at(22), updated: at(3) }}
    />
  </>
);

const meta: Meta<typeof RepositoryGroup> = {
  title: 'Work/RepositoryGroup',
  component: RepositoryGroup,
  args: { repository: 'engine', count: 2, children: rows },
  decorators: [(Story) => (
    <div className="w-[18rem] border border-line bg-raised">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof RepositoryGroup>;

/** Nothing asserted: the state a browser would see, and the honest answer to an unasked question. */
export const Unknown: Story = {};

/** The driver works here and a session holds the registered root. */
export const Drivable: Story = { args: { drivable: true, busy: true } };

/** Held by the person: the standing choice is suspended, so the header stops claiming it. */
export const Held: Story = { args: { drivable: true, held: true } };

/** A second tree, because D51 made the tree the unit of exclusion and this is where you see it. */
export const BusyInItsOwnTree: Story = { args: { drivable: true, busy: 'streaming-budget' } };

/** Sessions run; quests cannot be addressed here until the repository adopts (D48 §4). */
export const NotAdopted: Story = { args: { repository: 'sandbox', adopted: false, count: 1, children: <SessionRow session={{ ...SESSION, repository: 'sandbox' }} /> } };

/** A teammate's registration, mirrored: there is a record here and no working tree (D48 §3). */
export const NoCheckoutHere: Story = {
  args: {
    hasCheckout: false,
    count: 1,
    children: <SessionRow session={{ ...SESSION, id: 'person@machine-a/s1a2b3c4' }} />,
  },
};

/** Long and CJK repository names — the header truncates, the count never moves. */
export const LongName: Story = {
  args: { repository: 'platform-infrastructure-and-tooling', drivable: true, busy: 'a-rather-long-branch-name' },
};

export const ChineseName: Story = { args: { repository: '世界流式加载引擎', drivable: true, busy: true } };
