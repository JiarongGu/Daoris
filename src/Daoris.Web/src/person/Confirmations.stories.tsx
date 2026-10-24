import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { InTheme } from '../plugins/storyIcons';
import { AdoptionNotice as Notice, Confirmations, type Confirmation, type ConfirmationAnswer, type PressNames } from './Confirmations';

// What waits for the person's Confirm (PERSONDOOR1f, RPC1c, NOTIFY2b; D156 §4.2, the notification design §3), as *Notifications*
// draws it in the side bar: a terminal's `sessions say` in plain words, a press naming its session, one no catalogue words, the
// service's by its door's act, a press of many values and one of long words in their bounded region, a refusal said under its
// card. At the side bar's 300 px floor and at 45% of the install's window (R6), in English and 中文, light and dark.

const IN_TWO_MINUTES = new Date(Date.now() + 100_000).toISOString();
const A_MINUTE_AGO = new Date(Date.now() - 60_000).toISOString();

const SAY: Confirmation = {
  source: 'pipe', id: 'c1', module: 'DAORIS.DRIVER', route: 'SESSION_INPUT',
  fields: [
    { name: '/id', type: 'string', value: '3f2a91c4' }, { name: '/text', type: 'string', value: 'Run the database tests before you land.' },
    { name: '/level', type: 'string', value: 'urgent' }, { name: '/said', type: 'boolean', value: 'true' },
    { name: '/paths/0', type: 'string', value: 'src/feed/export.ts' }, { name: '/key', type: 'string', value: 'k-1' },
  ],
  named: [
    { role: 'session', name: '/id', value: '3f2a91c4' }, { role: 'words', name: '/text', value: 'Run the database tests before you land.' },
    { role: 'level', name: '/level', value: 'urgent' }, { role: 'files', name: '/paths/0', value: 'src/feed/export.ts' },
    { role: 'form', name: '/said', value: 'true' },
  ],
  asker: { process: 'bash', pid: 4120 }, asked: A_MINUTE_AGO, expires: IN_TWO_MINUTES, shown: true,
};

const STOP: Confirmation = {
  source: 'pipe', id: 'c2', module: 'DAORIS.DRIVER', route: 'STOP_SESSION',
  fields: [{ name: '/id', type: 'string', value: '9b1e0d2a' }, { name: '/said', type: 'boolean', value: 'true' }],
  named: [{ role: 'session', name: '/id', value: '9b1e0d2a' }],
  asker: { process: 'daoris-driver', pid: 4242 }, asked: A_MINUTE_AGO, expires: IN_TWO_MINUTES,
};

const UNWORDED: Confirmation = {
  source: 'pipe', id: 'c3', module: 'DAORIS.DRIVER', route: 'SET_MYSTERY', fields: [{ name: '/keep', type: 'number', value: '3' }],
  asker: { process: null, pid: 77 }, expires: IN_TWO_MINUTES,
};

const SERVICE: Confirmation = {
  source: 'service', id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict",
  fields: [
    { name: 'id', type: 'string', value: 'q12' }, { name: '/verdict', type: 'string', value: 'reviewed' },
    { name: '/setUp/machine', type: 'string', value: 'desk' }, { name: '/setUp/sequence', type: 'number', value: '3' },
  ],
  asker: null, asked: A_MINUTE_AGO, expires: IN_TWO_MINUTES,
};

const MANY: Confirmation = {
  ...UNWORDED, id: 'c4', route: 'SET_LANDING',
  fields: Array.from({ length: 256 }, (_, index) => ({ name: `/rules/${index}`, type: 'string', value: `rule ${index}` })),
};

const LONG: Confirmation = {
  ...SAY, id: 'c5', named: [{ role: 'session', name: '/id', value: '3f2a91c4' }, { role: 'words', name: '/text', value: 'Keep going. '.repeat(120) }],
};

const NAMES: PressNames = {
  session: (id) => ({ '3f2a91c4': { title: 'Fix the export feed', repository: 'game' }, '9b1e0d2a': { title: 'Conversation', repository: 'engine' } })[id] ?? null,
};

/** The side bar at a width: its 300 px floor, and 45% of the install's 1546 px window. */
const sideAt = (width: number): Decorator => (Story) => <div className="border border-line bg-page p-3" style={{ width }}><Story /></div>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta = {
  title: 'Shell/Confirmations',
  component: Confirmations,
  args: { waiting: [SAY], names: NAMES, onAnswer: async (): Promise<ConfirmationAnswer> => ({ answered: true }) },
  decorators: [sideAt(300)],
} satisfies Meta<typeof Confirmations>;

export default meta;
type Story = StoryObj<typeof meta>;

/** `sessions say` in plain words, at the side bar's floor: the session with its repository and short id, the words, the file. */
export const SessionsSay: Story = {};
export const SessionsSayChinese: Story = { decorators: [chinese] };
export const SessionsSayDark: Story = { decorators: [dark] };
export const SessionsSayChineseDark: Story = { decorators: [chinese, dark] };

/** At 45% of the install's window. */
export const SessionsSayWide: Story = { decorators: [sideAt(696)] };
export const SessionsSayWideChinese: Story = { decorators: [sideAt(696), chinese] };

/** Every source, waiting together: a stop naming its session, one no catalogue words, the service's. */
export const FourWaiting: Story = { args: { waiting: [SAY, STOP, UNWORDED, SERVICE] } };
export const FourWaitingChineseDark: Story = { args: { waiting: [SAY, STOP, UNWORDED, SERVICE] }, decorators: [chinese, dark] };

/** A press of 256 values and one of long words, each in a region bounded by its boxes of eight lines. */
export const Bounded: Story = { args: { waiting: [MANY, LONG] } };

export const NoLongerWaiting: Story = {
  args: { waiting: [STOP], onAnswer: async () => ({ answered: false, why: 'gone', message: '' }) },
};

/** A land asked hours ago, its card's door to the session's review beside its buttons (CONFIRMWAIT1c, the confirm-wait design §8.1). */
const LAND: Confirmation = {
  source: 'pipe', id: 'c6', module: 'DAORIS.DRIVER', route: 'LAND_SESSION_TREE',
  fields: [{ name: '/id', type: 'string', value: '3f2a91c4' }, { name: '/branch', type: 'string', value: 'main' }],
  named: [{ role: 'session', name: '/id', value: '3f2a91c4' }],
  asker: { process: 'bash', pid: 4120 }, asked: new Date(Date.now() - 3 * 3_600_000).toISOString(), expires: null, waitedOn: false,
};

export const PlanDoor: Story = { args: { waiting: [LAND], onOpenPlan: () => {} } };
export const PlanDoorChinese: Story = { args: { waiting: [LAND], onOpenPlan: () => {} }, decorators: [chinese] };

/** The adoption notice, which stays one line under the strip (§3.1). */
export const AdoptionNotice: Story = { render: () => <Notice onDismiss={() => {}} /> };
