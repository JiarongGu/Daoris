import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import { SessionRelations } from './SessionRelations';

// Who a session worked with (SESS1): asked for by another session, carried on after an answer, and
// what it asked of other repositories, one answered and one not.

const quest = (over: Partial<Quest> & { id: string }): Quest => ({
  from: 'engine', to: 'game', title: over.id, body: '', status: 'Open', filed: '2026-09-29T01:00:00Z', updated: '2026-09-29T01:00:00Z',
  ...over,
});
const ASKER: Session = {
  id: 'a1b2c3d4', quest: null, repository: 'game', adapter: 'claude-code-acp', state: 'completed', kind: 'driven',
  created: '2026-09-29T00:00:00Z', updated: '2026-09-29T00:30:00Z',
};

const meta = {
  title: 'Work/SessionRelations',
  component: SessionRelations,
  args: { onQuest: () => {}, onSession: () => {} },
} satisfies Meta<typeof SessionRelations>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Everything: Story = {
  args: {
    relations: {
      askedBy: { id: ASKER.id, session: ASKER },
      resumedAfter: quest({ id: 'q7', to: 'tools', title: 'Which chunk size does the loader expect?', status: 'Done', note: '64 KiB, from the pack header.' }),
      asked: [
        { quest: quest({ id: 'q8', to: 'assets', title: 'Expose the streaming budget per level', status: 'Taken' }), question: false },
        { quest: quest({ id: 'q9', to: 'net', title: 'Does the replication tick read the budget?', status: 'Done', note: 'No: it reads its own cap, set at load.' }), question: true },
      ],
    },
  },
};

export const AskedByAnotherMachine: Story = {
  args: { relations: { askedBy: { id: 'z9y8x7w6', session: null }, resumedAfter: null, asked: [] } },
};
