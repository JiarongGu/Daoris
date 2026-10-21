import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { InTheme } from '../plugins/storyIcons';
import type { Notice } from '../bridge/notices';
import type { Confirmation, PressNames } from '../person/Confirmations';
import { NotificationsView } from './NotificationsView';

// *Notifications* (NOTIFY2b, D196 point 3, the notification design §2): the presses waiting for confirmation first, each its whole
// card, then *What needs you* (here a stand-in for the band's rows, which `work/attention/attention.stories.tsx` draws whole); with
// nothing in any, *Nothing needs you*. At the side bar's 300 px floor and at 45% of the install's window (R6), in English and 中文,
// light and dark.

const IN_TWO_MINUTES = new Date(Date.now() + 100_000).toISOString();

const SAY: Confirmation = {
  source: 'pipe', id: 'c1', module: 'DAORIS.DRIVER', route: 'SESSION_INPUT', shown: true,
  fields: [
    { name: '/id', type: 'string', value: '3f2a91c4' }, { name: '/text', type: 'string', value: 'Run the database tests before you land.' },
    { name: '/paths/0', type: 'string', value: 'src/feed/export.ts' }, { name: '/said', type: 'boolean', value: 'true' },
  ],
  named: [
    { role: 'session', name: '/id', value: '3f2a91c4' }, { role: 'words', name: '/text', value: 'Run the database tests before you land.' },
    { role: 'files', name: '/paths/0', value: 'src/feed/export.ts' }, { role: 'form', name: '/said', value: 'true' },
  ],
  asker: { process: 'bash', pid: 4120 }, asked: new Date(Date.now() - 60_000).toISOString(), expires: IN_TWO_MINUTES,
};

const SERVICE: Confirmation = {
  source: 'service', id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict",
  fields: [{ name: '/verdict', type: 'string', value: 'reviewed' }], asker: null, expires: IN_TWO_MINUTES,
};

const NAMES: PressNames = { session: (id) => (id === '3f2a91c4' ? { title: 'Fix the export feed', repository: 'game' } : null) };

/** A stand-in for *What needs you*'s rows, as the band draws them in the view. */
const ROWS = (
  <ul className="m-0 grid list-none gap-1 border-l-[3px] border-l-st-open p-0 pl-3 text-body">
    <li>Fix the menu · parked at a checkpoint</li>
    <li>Cap the hydration per frame · proposed, not yet published</li>
  </ul>
);

/** The side bar at a width: its 300 px floor, and 45% of the install's 1546 px window; its height a short window's. */
const sideAt = (width: number): Decorator => (Story) => (
  <div className="flex flex-col border border-line bg-page" style={{ width, height: 560 }}><Story /></div>
);
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta = {
  title: 'Shell/Notifications',
  component: NotificationsView,
  args: {
    presses: [SAY, SERVICE], needs: 2, working: 1, attention: ROWS, names: NAMES, onAnswer: async () => ({ answered: true as const }),
  },
  decorators: [sideAt(300)],
} satisfies Meta<typeof NotificationsView>;

export default meta;
type Story = StoryObj<typeof meta>;

/** At the side bar's floor: a terminal's `sessions say` and the service's press, then What needs you. */
export const AtTheFloor: Story = {};
export const AtTheFloorChinese: Story = { decorators: [chinese] };
export const AtTheFloorDark: Story = { decorators: [dark] };
export const AtTheFloorChineseDark: Story = { decorators: [chinese, dark] };

/** At 45% of the install's window. */
export const Wide: Story = { decorators: [sideAt(696)] };
export const WideChinese: Story = { decorators: [sideAt(696), chinese] };

/** What needs you alone, no press waiting. */
export const NoPress: Story = { args: { presses: [] } };

/** *Done and failed* (NOTIFY2f): a session that failed and a press that ran out of time unread, a session that completed read. */
const NOTICES: Notice[] = [
  { id: 'expired-p1', kind: 'expired', at: new Date(Date.now() - 2 * 60_000).toISOString(), read: false, route: 'SESSION_INPUT' },
  {
    id: 'ended-s1', kind: 'ended', at: new Date(Date.now() - 14 * 60_000).toISOString(), read: false, session: 's1',
    repository: 'game', state: 'failed',
  },
  {
    id: 'ended-s2', kind: 'ended', at: new Date(Date.now() - 3 * 3600_000).toISOString(), read: true, session: 's2',
    repository: 'engine', state: 'completed',
  },
];
export const DoneAndFailed: Story = { args: { notices: NOTICES } };
export const DoneAndFailedChinese: Story = { args: { notices: NOTICES }, decorators: [chinese] };
export const DoneAndFailedDark: Story = { args: { notices: NOTICES }, decorators: [dark] };
export const DoneAndFailedWide: Story = { args: { notices: NOTICES }, decorators: [sideAt(696)] };
/** Nothing waits on the person, and what ended is listed under the line that says so. */
export const OnlyDoneAndFailed: Story = { args: { presses: [], needs: 0, notices: NOTICES } };

/** Nothing in any group: *Nothing needs you* and the sessions working. */
export const Nothing: Story = { args: { presses: [], needs: 0 } };
export const NothingChinese: Story = { args: { presses: [], needs: 0 }, decorators: [chinese] };
