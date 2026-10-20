import type { Meta, StoryObj } from '@storybook/react-vite';
import { Confirmations, type Confirmation, type ConfirmationAnswer } from './Confirmations';

// What waits for the person's Confirm (PERSONDOOR1f, RPC1c; D156 §4.2), in the shapes the bridge's `PERSON_CONFIRMATIONS` and the
// service's `GET /api/confirmations` answer: a terminal's press the page words, one it names by its route, the service's by its
// door's act, a refusal said under its card, and the adoption notice.

const IN_TWO_MINUTES = new Date(Date.now() + 2 * 60_000).toISOString();

const ARCHIVE: Confirmation = {
  source: 'pipe', id: 'c1', route: 'SESSION_ARCHIVE',
  fields: [
    { name: '/ids/0', type: 'string', value: 's1a2b3c4' }, { name: '/archived', type: 'boolean', value: 'true' },
    { name: '/said', type: 'boolean', value: 'true' },
  ],
  asker: { process: 'daoris-driver', pid: 4242 }, expires: IN_TWO_MINUTES,
};

const UNWORDED: Confirmation = {
  source: 'pipe', id: 'c2', route: 'SET_TREES', fields: [{ name: '/keep', type: 'number', value: '3' }],
  asker: { process: null, pid: 77 }, expires: IN_TWO_MINUTES,
};

const SERVICE: Confirmation = {
  source: 'service', id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict",
  fields: [
    { name: 'id', type: 'string', value: 'q12' }, { name: '/verdict', type: 'string', value: 'reviewed' },
    { name: '/setUp/machine', type: 'string', value: 'desk' }, { name: '/setUp/sequence', type: 'number', value: '3' },
  ],
  asker: null, expires: IN_TWO_MINUTES,
};

const meta = {
  title: 'Shell/Confirmations',
  component: Confirmations,
  args: { waiting: [], onAnswer: async (): Promise<ConfirmationAnswer> => ({ answered: true }), onDismissNotice: () => {} },
} satisfies Meta<typeof Confirmations>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ATerminalsArchive: Story = { args: { waiting: [ARCHIVE] } };

export const ThreeWaiting: Story = { args: { waiting: [ARCHIVE, UNWORDED, SERVICE] } };

export const NoLongerWaiting: Story = {
  args: { waiting: [ARCHIVE], onAnswer: async () => ({ answered: false, why: 'gone', message: '' }) },
};

export const AdoptionNotice: Story = { args: { waiting: [], notice: true } };
