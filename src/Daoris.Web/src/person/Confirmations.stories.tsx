import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { InTheme } from '../plugins/storyIcons';
import type { UpdateState } from '../update/UpdateBanner';
import { AdoptionNotice as Notice, Confirmations, type Confirmation, type ConfirmationAnswer, type PressNames } from './Confirmations';

// What waits for the person's Confirm, as *Notifications*
// draws it in the side bar: a terminal's `sessions say` in plain words, a press naming its session, one no catalogue words, the
// service's by its door's act, a press of many values and one of long words in their bounded region, a refusal said under its
// card. At the side bar's 300 px floor and at 45% of the install's window (R6), in English and 中文, light and dark.

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
  asker: { process: 'bash', pid: 4120 }, asked: A_MINUTE_AGO, shown: true,
};

const STOP: Confirmation = {
  source: 'pipe', id: 'c2', module: 'DAORIS.DRIVER', route: 'STOP_SESSION',
  fields: [{ name: '/id', type: 'string', value: '9b1e0d2a' }, { name: '/said', type: 'boolean', value: 'true' }],
  named: [{ role: 'session', name: '/id', value: '9b1e0d2a' }],
  asker: { process: 'daoris-driver', pid: 4242 }, asked: A_MINUTE_AGO,
};

const UNWORDED: Confirmation = {
  source: 'pipe', id: 'c3', module: 'DAORIS.DRIVER', route: 'SET_MYSTERY', fields: [{ name: '/keep', type: 'number', value: '3' }],
  asker: { process: null, pid: 77 },
};

const SERVICE: Confirmation = {
  source: 'service', id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict",
  fields: [
    { name: 'id', type: 'string', value: 'q12' }, { name: '/verdict', type: 'string', value: 'reviewed' },
    { name: '/setUp/machine', type: 'string', value: 'desk' }, { name: '/setUp/sequence', type: 'number', value: '3' },
  ],
  asker: null, asked: A_MINUTE_AGO,
};

const MANY: Confirmation = {
  ...UNWORDED, id: 'c4', route: 'SET_LINE',
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

/** A sweep asked hours ago, its card's door to the workspace's Branches beside its buttons. */
const LAND: Confirmation = {
  source: 'pipe', id: 'c6', module: 'DAORIS.DRIVER', route: 'SWEEP',
  fields: [{ name: '/workspace', type: 'string', value: 'studio' }],
  named: [{ role: 'workspace', name: '/workspace', value: 'studio' }],
  asker: { process: 'bash', pid: 4120 }, asked: new Date(Date.now() - 3 * 3_600_000).toISOString(), waitedOn: false,
};

export const PlanDoor: Story = { args: { waiting: [LAND], onOpenPlan: () => {} } };
export const PlanDoorChinese: Story = { args: { waiting: [LAND], onOpenPlan: () => {} }, decorators: [chinese] };

const BEYOND: Confirmation = {
  ...STOP, id: 'c9', asker: { process: null, pid: 77, session: 'h1', help: true },
  helpPlace: { place: { kind: 'workspace', workspace: 'Work' }, beyond: true, focus: { kind: 'workspace', workspace: 'Games' } },
};
export const AskDaorisBeyondItsFocus: Story = { args: { waiting: [BEYOND] } };
export const AskDaorisBeyondItsFocusChineseDark: Story = { args: { waiting: [BEYOND] }, decorators: [chinese, dark] };

/** A department Ask Daoris opens, on its card in restricted mode: every value, each step in order, the quests it asks and the command it is. */
const CREATE: Confirmation = {
  ...STOP, id: 'c10', route: 'REGISTRY_CREATE', asker: { process: null, pid: 77, session: 'h1', help: true },
  fields: [{ name: '/name', type: 'string', value: 'ledger' }, { name: '/purpose', type: 'string', value: 'keeps the books' }],
  view: {
    kind: 'repository-create',
    facts: {
      name: 'ledger', workspace: 'aurora', folder: 'D:/work/aurora/ledger', purpose: 'keeps the books', line: 'main', author: 'Fixture <fixture@example.test>',
    },
    command: 'daoris repository create ledger --workspace aurora --purpose "keeps the books" --work "Build the first screen."',
    also: [
      { kind: 'repository-create-no-remote', facts: {} },
      { kind: 'repository-create-step-folder', facts: { n: '1', folder: 'D:/work/aurora/ledger' } },
      { kind: 'repository-create-step-history', facts: { n: '2', line: 'main', author: 'Fixture <fixture@example.test>' } },
      { kind: 'repository-create-step-registration', facts: { n: '3', name: 'ledger', workspace: 'aurora' } },
      { kind: 'repository-create-step-driving-off', facts: { n: '4', name: 'ledger', step: 'push' } },
      { kind: 'repository-create-step-setup', facts: { n: '5', name: 'ledger' } },
      {
        kind: 'repository-create-quest-setup',
        facts: { title: 'Set up this repository for every agent (2026-11-12)', head: 'What is asked, and whose it is. Take up the shared doctrine in the layout every agent reads.' },
      },
      { kind: 'repository-create-quest-work', facts: { title: 'Build the first screen.', head: 'Build the first screen with totals by month.' } },
    ],
  },
};
export const AskDaorisOpensADepartment: Story = { args: { waiting: [CREATE] } };
export const AskDaorisOpensADepartmentChineseDark: Story = { args: { waiting: [CREATE] }, decorators: [chinese, dark] };

/** A room Ask Daoris asks of a repository, on its card: whose it is, the folder and the reason, the one quest it asks and the command it is. */
const ROOM: Confirmation = {
  ...CREATE, id: 'c11', route: 'PLACE_APPLY',
  fields: [{ name: '/repositories/0', type: 'string', value: 'engine' }, { name: '/room', type: 'string', value: 'tools/web' }],
  view: {
    kind: 'place-room',
    facts: { repositories: 'engine', workspace: 'aurora', room: 'tools/web', purpose: 'the web app needs its own conventions' },
    command: 'daoris place engine --room tools/web --purpose "the web app needs its own conventions" --apply',
    also: [
      { kind: 'place-room-quest', facts: { title: 'Add a room for tools/web', head: 'What is asked, and whose it is. Give `tools/web` a room of its own in this repository.' } },
    ],
  },
};
export const AskDaorisAsksForARoom: Story = { args: { waiting: [ROOM] } };
export const AskDaorisAsksForARoomChineseDark: Story = { args: { waiting: [ROOM] }, decorators: [chinese, dark] };

/**
 * A Confirm held by an update's drain: the card says what the drain waits on and since when, and offers *Install later*
 * beside Refuse and Confirm. The story presses Confirm, as a person would, so the held answer is drawn.
 */
const DRAIN: UpdateState = {
  state: 'draining', staged: { id: 'b1', version: '0.1.9' }, mode: 'when-idle', driven: 2, turns: 1, problem: null, outcome: null, last: null,
  since: new Date(Date.now() - 20 * 60_000).toISOString(),
};
const held: Story['play'] = async ({ canvasElement }) => {
  const confirm = [...canvasElement.querySelectorAll<HTMLButtonElement>('button')].find((each) => /^(Confirm|确认)$/.test(each.textContent ?? ''));
  confirm?.click();
  await new Promise((resolve) => setTimeout(resolve, 50));
};
const HELD_ARGS = {
  waiting: [STOP], update: DRAIN, onSayUpdate: () => {},
  onAnswer: async (): Promise<ConfirmationAnswer> => ({ answered: false, why: 'draining', message: 'Daoris is about to restart; this still waits.' }),
};
export const HeldByDrain: Story = { args: HELD_ARGS, play: held };
export const HeldByDrainChineseDark: Story = { args: HELD_ARGS, play: held, decorators: [chinese, dark] };
export const HeldByRestart: Story = { args: { ...HELD_ARGS, update: { ...DRAIN, state: 'applying' } }, play: held };

/** The adoption notice, which stays one line under the strip (§3.1). */
export const AdoptionNotice: Story = { render: () => <Notice onDismiss={() => {}} /> };
